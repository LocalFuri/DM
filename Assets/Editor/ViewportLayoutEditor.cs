using DM.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class ViewportLayoutEditor : EditorWindow
{
  private static readonly int[] SnapValues = { 1, 2, 4, 8 };

  private const string OverlayObjectName = "___DM_ViewportReferenceOverlay";
  private const string PrefsLayoutGuidKey = "ViewportLayoutEditor.LayoutGuid";
  private const string PrefsReferenceTextureGuidKey =
      "ViewportLayoutEditor.ReferenceTextureGuid";

  private const int FrameWidth = 320;
  private const int FrameHeight = 200;
  private const int DungeonViewBoundaryX = 224;

  [System.NonSerialized]
  private ViewportLayout layout;
  private Vector2 scroll;
  private bool[] rememberedEnabledStates;
  private int snap = 1;

  // Single source of truth for selection.
  private int selectedPieceIndex;
  private bool selectionChangedThisFrame;

  private Texture2D referenceTexture;
  private bool showOverlay;
  private float overlayOpacity = 0.5f;
  private RawImage cachedViewportImage;

  private GameObject overlayObject;
  private RawImage overlayImage;

  [MenuItem("Tools/Viewport Layout Editor")]
  public static void Open()
  {
    GetWindow<ViewportLayoutEditor>("Viewport Layout");
  }

  private void OnEnable()
  {
    RestorePersistedAssets();
    EditorApplication.update += MaintainOverlayVisual;
    EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
  }

  private void OnDisable()
  {
    SaveAssetGuid(PrefsLayoutGuidKey, layout);
    SaveAssetGuid(PrefsReferenceTextureGuidKey, referenceTexture);

    EditorApplication.update -= MaintainOverlayVisual;
    EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
    DestroyOverlayObject();
    RepaintGameViews();
  }

  private void HandlePlayModeStateChanged(PlayModeStateChange state)
  {
    cachedViewportImage = null;
    DestroyOverlayObject();

    if (state == PlayModeStateChange.ExitingPlayMode)
      RepaintGameViews();
  }

  private void OnGUI()
  {
    selectionChangedThisFrame = false;

    DrawOverlayControls();

    EditorGUI.BeginChangeCheck();
    ViewportLayout newLayout = (ViewportLayout)EditorGUILayout.ObjectField(
        "Viewport Layout",
        layout,
        typeof(ViewportLayout),
        false);
    if (EditorGUI.EndChangeCheck())
    {
      layout = newLayout;
      rememberedEnabledStates = null;
      selectedPieceIndex = 0;
      SaveAssetGuid(PrefsLayoutGuidKey, layout);
    }

    if (layout == null)
    {
      EditorGUILayout.HelpBox("Select a ViewportLayout asset.", MessageType.Info);
      return;
    }

    ClampSelectedPieceIndex();
    HandlePieceKeyboardNudge();
    DrawSelectedPieceHeader();
    DrawDungeonViewBoundaryGuide();

    EditorGUI.BeginChangeCheck();

    DrawSnapToolbar();

    using (new EditorGUI.DisabledScope(rememberedEnabledStates == null))
    {
      if (GUILayout.Button("Restore Enabled States"))
      {
        RestoreEnabledStates();
        PersistChanges();
      }
    }

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Pieces (render order)", EditorStyles.boldLabel);

    scroll = EditorGUILayout.BeginScrollView(scroll);

    bool changed = false;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      bool isSelected = i == selectedPieceIndex;
      DrawPieceCard(i, piece, isSelected, ref changed);
    }

    EditorGUILayout.EndScrollView();

    if (EditorGUI.EndChangeCheck() || changed)
      PersistChanges();

    if (selectionChangedThisFrame)
      Repaint();
  }

  private void DrawPieceCard(
      int index,
      ViewportPiece piece,
      bool isSelected,
      ref bool changed)
  {
    Color previousBg = GUI.backgroundColor;
    if (isSelected)
      GUI.backgroundColor = new Color(0.2f, 0.55f, 1f, 1f);

    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
    GUI.backgroundColor = previousBg;

    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField(
        isSelected ? $"▶ {piece.Name}" : piece.Name,
        EditorStyles.boldLabel);
    if (GUILayout.Button("Select", GUILayout.Width(60)))
      SelectPiece(index);
    EditorGUILayout.EndHorizontal();

    EditorGUI.BeginChangeCheck();
    piece.Name = EditorGUILayout.TextField("Name", piece.Name);
    piece.Enabled = EditorGUILayout.Toggle("Enabled", piece.Enabled);
    piece.Graphic = (DungeonGraphicType)EditorGUILayout.EnumPopup("Graphic", piece.Graphic);
    if (EditorGUI.EndChangeCheck())
    {
      SelectPiece(index);
      changed = true;
    }

    if (DrawIntStepper("X", ref piece.X, snap))
    {
      SelectPiece(index);
      changed = true;
    }

    if (DrawIntStepper("Y", ref piece.Y, snap))
    {
      SelectPiece(index);
      changed = true;
    }

    EditorGUILayout.BeginHorizontal();

    using (new EditorGUI.DisabledScope(index <= 0))
    {
      if (GUILayout.Button("Move Up"))
      {
        SwapPieces(index, index - 1);
        if (selectedPieceIndex == index)
          SelectPiece(index - 1);
        else if (selectedPieceIndex == index - 1)
          SelectPiece(index);
        changed = true;
      }
    }

    using (new EditorGUI.DisabledScope(index >= layout.Pieces.Count - 1))
    {
      if (GUILayout.Button("Move Down"))
      {
        SwapPieces(index, index + 1);
        if (selectedPieceIndex == index)
          SelectPiece(index + 1);
        else if (selectedPieceIndex == index + 1)
          SelectPiece(index);
        changed = true;
      }
    }

    if (GUILayout.Button("Solo"))
    {
      SelectPiece(index);
      SoloPiece(index);
      changed = true;
    }

    EditorGUILayout.EndHorizontal();
    EditorGUILayout.EndVertical();
  }

  private void SelectPiece(int index)
  {
    if (layout == null || layout.Pieces.Count == 0)
    {
      selectedPieceIndex = 0;
      selectionChangedThisFrame = true;
      GUI.FocusControl(null);
      return;
    }

    int clamped = Mathf.Clamp(index, 0, layout.Pieces.Count - 1);
    if (clamped != selectedPieceIndex)
      selectionChangedThisFrame = true;

    selectedPieceIndex = clamped;
    GUI.FocusControl(null);
  }

  private void DrawSelectedPieceHeader()
  {
    EditorGUILayout.Space();
    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

    EditorGUILayout.BeginHorizontal();

    using (new EditorGUI.DisabledScope(layout.Pieces.Count == 0))
    {
      if (GUILayout.Button("Previous Piece"))
      {
        SelectPiece(
            selectedPieceIndex <= 0
                ? layout.Pieces.Count - 1
                : selectedPieceIndex - 1);
      }

      if (GUILayout.Button("Next Piece"))
      {
        SelectPiece(
            selectedPieceIndex >= layout.Pieces.Count - 1
                ? 0
                : selectedPieceIndex + 1);
      }
    }

    EditorGUILayout.EndHorizontal();

    if (layout.Pieces.Count == 0)
    {
      EditorGUILayout.LabelField("No viewport pieces.", EditorStyles.boldLabel);
      EditorGUILayout.EndVertical();
      return;
    }

    ClampSelectedPieceIndex();
    ViewportPiece selected = layout.Pieces[selectedPieceIndex];
    string pieceLabel = string.IsNullOrEmpty(selected.Name)
        ? "(unnamed)"
        : selected.Name;

    EditorGUILayout.LabelField(
        $"Selected: {pieceLabel}  ({selectedPieceIndex + 1}/{layout.Pieces.Count})",
        EditorStyles.boldLabel);

    GUIStyle positionStyle = new GUIStyle(EditorStyles.boldLabel)
    {
      fontSize = 18,
      alignment = TextAnchor.MiddleLeft
    };

    EditorGUILayout.LabelField($"X: {selected.X}    Y: {selected.Y}", positionStyle);
    EditorGUILayout.LabelField(
        "Arrows nudge 1px. Hold Shift for 10px.",
        EditorStyles.miniLabel);

    EditorGUILayout.EndVertical();
    EditorGUILayout.Space();
  }

  private void DrawDungeonViewBoundaryGuide()
  {
    EditorGUILayout.LabelField("Dungeon View Boundary", EditorStyles.boldLabel);
    EditorGUILayout.LabelField(
        $"Framebuffer {FrameWidth}×{FrameHeight}  ·  Guide at X = {DungeonViewBoundaryX}",
        EditorStyles.miniLabel);

    Rect area = GUILayoutUtility.GetRect(
        FrameWidth,
        FrameHeight,
        GUILayout.Width(FrameWidth),
        GUILayout.Height(FrameHeight));

    EditorGUI.DrawRect(area, new Color(0.12f, 0.12f, 0.12f, 1f));

    Rect dungeonRect = new Rect(
        area.x,
        area.y,
        DungeonViewBoundaryX,
        area.height);
    EditorGUI.DrawRect(dungeonRect, new Color(0.16f, 0.28f, 0.22f, 1f));

    Rect interfaceRect = new Rect(
        area.x + DungeonViewBoundaryX,
        area.y,
        FrameWidth - DungeonViewBoundaryX,
        area.height);
    EditorGUI.DrawRect(interfaceRect, new Color(0.28f, 0.16f, 0.16f, 1f));

    EditorGUI.DrawRect(
        new Rect(area.x + DungeonViewBoundaryX, area.y, 2f, area.height),
        new Color(1f, 0.9f, 0.2f, 1f));

    GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
    {
      alignment = TextAnchor.MiddleCenter,
      normal = { textColor = Color.white }
    };

    GUI.Label(dungeonRect, "Dungeon View", labelStyle);
    GUI.Label(interfaceRect, "Interface Area", labelStyle);

    EditorGUILayout.Space();
  }

  private void HandlePieceKeyboardNudge()
  {
    Event current = Event.current;
    if (current.type != EventType.KeyDown)
      return;

    if (EditorGUIUtility.editingTextField)
      return;

    if (layout.Pieces.Count == 0)
      return;

    ClampSelectedPieceIndex();
    ViewportPiece piece = layout.Pieces[selectedPieceIndex];
    int step = current.shift ? 10 : 1;
    bool moved = false;

    switch (current.keyCode)
    {
      case KeyCode.LeftArrow:
        piece.X -= step;
        moved = true;
        break;
      case KeyCode.RightArrow:
        piece.X += step;
        moved = true;
        break;
      case KeyCode.UpArrow:
        piece.Y += step;
        moved = true;
        break;
      case KeyCode.DownArrow:
        piece.Y -= step;
        moved = true;
        break;
    }

    if (!moved)
      return;

    current.Use();
    PersistChanges();
  }

  private void ClampSelectedPieceIndex()
  {
    if (layout == null || layout.Pieces.Count == 0)
    {
      selectedPieceIndex = 0;
      return;
    }

    selectedPieceIndex = Mathf.Clamp(
        selectedPieceIndex,
        0,
        layout.Pieces.Count - 1);
  }

  private void DrawOverlayControls()
  {
    EditorGUILayout.LabelField("Reference Overlay", EditorStyles.boldLabel);

    EditorGUI.BeginChangeCheck();
    referenceTexture = (Texture2D)EditorGUILayout.ObjectField(
        "Reference Texture",
        referenceTexture,
        typeof(Texture2D),
        false);
    if (EditorGUI.EndChangeCheck())
      SaveAssetGuid(PrefsReferenceTextureGuidKey, referenceTexture);

    EditorGUI.BeginChangeCheck();

    showOverlay = EditorGUILayout.Toggle("Show Overlay", showOverlay);

    overlayOpacity = EditorGUILayout.Slider(
        "Overlay Opacity",
        overlayOpacity,
        0f,
        1f);

    if (EditorGUI.EndChangeCheck())
    {
      MaintainOverlayVisual();
      RepaintGameViews();
    }

    EditorGUILayout.Space();
  }

  private void RestorePersistedAssets()
  {
    string savedLayoutGuid =
        EditorPrefs.GetString(PrefsLayoutGuidKey, string.Empty);

    layout = LoadViewportLayoutByGuid(savedLayoutGuid);

    if (layout == null && string.IsNullOrEmpty(savedLayoutGuid))
      layout = FindSingleViewportLayoutAsset();

    if (layout != null)
      SaveAssetGuid(PrefsLayoutGuidKey, layout);

    referenceTexture = LoadTextureByGuid(
        EditorPrefs.GetString(PrefsReferenceTextureGuidKey, string.Empty));
  }

  private static ViewportLayout LoadViewportLayoutByGuid(string guid)
  {
    if (string.IsNullOrEmpty(guid))
      return null;

    string path = AssetDatabase.GUIDToAssetPath(guid);
    if (string.IsNullOrEmpty(path))
      return null;

    ViewportLayout loaded =
        AssetDatabase.LoadAssetAtPath<ViewportLayout>(path);
    if (loaded != null)
      return loaded;

    Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
    for (int i = 0; i < assets.Length; i++)
    {
      if (assets[i] is ViewportLayout viewportLayout)
        return viewportLayout;
    }

    return null;
  }

  private static ViewportLayout FindSingleViewportLayoutAsset()
  {
    string[] guids = AssetDatabase.FindAssets("t:ViewportLayout");
    if (guids == null || guids.Length != 1)
      return null;

    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
    if (string.IsNullOrEmpty(path))
      return null;

    return AssetDatabase.LoadAssetAtPath<ViewportLayout>(path);
  }

  private static Texture2D LoadTextureByGuid(string guid)
  {
    if (string.IsNullOrEmpty(guid))
      return null;

    string path = AssetDatabase.GUIDToAssetPath(guid);
    if (string.IsNullOrEmpty(path))
      return null;

    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    if (texture != null)
      return texture;

    Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
    for (int i = 0; i < assets.Length; i++)
    {
      if (assets[i] is Texture2D texture2D)
        return texture2D;
    }

    Object main = AssetDatabase.LoadMainAssetAtPath(path);
    if (main is Sprite sprite && sprite.texture != null)
      return sprite.texture;

    return null;
  }

  private static void SaveAssetGuid(string prefsKey, Object asset)
  {
    if (asset == null)
    {
      EditorPrefs.SetString(prefsKey, string.Empty);
      return;
    }

    string path = AssetDatabase.GetAssetPath(asset);
    if (string.IsNullOrEmpty(path))
    {
      EditorPrefs.SetString(prefsKey, string.Empty);
      return;
    }

    string guid = AssetDatabase.AssetPathToGUID(path);
    EditorPrefs.SetString(prefsKey, guid ?? string.Empty);
  }

  private void DrawSnapToolbar()
  {
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField("Snap", GUILayout.Width(36));

    foreach (int value in SnapValues)
    {
      bool selected = snap == value;
      GUIStyle style = selected
          ? EditorStyles.miniButtonMid
          : EditorStyles.miniButton;

      Color previousColor = GUI.backgroundColor;
      if (selected)
        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);

      if (GUILayout.Toggle(selected, value.ToString(), style, GUILayout.Width(28))
          && !selected)
      {
        snap = value;
      }

      GUI.backgroundColor = previousColor;
    }

    EditorGUILayout.EndHorizontal();
  }

  private void MaintainOverlayVisual()
  {
    if (!Application.isPlaying)
    {
      DestroyOverlayObject();
      return;
    }

    bool wantReference = showOverlay && referenceTexture != null;
    if (!wantReference)
    {
      DestroyOverlayObject();
      return;
    }

    if (!TryGetViewportRawImage(out RawImage dungeonImage))
    {
      DestroyOverlayObject();
      return;
    }

    if (!dungeonImage.isActiveAndEnabled)
    {
      DestroyOverlayObject();
      return;
    }

    if (!EnsureOverlayObject(dungeonImage))
      return;

    SyncOverlayTransform(dungeonImage);
    ApplyOverlayAppearance(dungeonImage);
  }

  private bool EnsureOverlayObject(RawImage dungeonImage)
  {
    if (overlayObject == null)
    {
      overlayObject = GameObject.Find(OverlayObjectName);
      if (overlayObject != null)
        overlayImage = overlayObject.GetComponent<RawImage>();
    }

    if (overlayObject == null)
    {
      overlayObject = new GameObject(OverlayObjectName);
      overlayObject.hideFlags = HideFlags.DontSave;
      overlayImage = overlayObject.AddComponent<RawImage>();
    }

    if (overlayImage == null)
    {
      DestroyOverlayObject();
      return false;
    }

    overlayImage.raycastTarget = false;

    Transform parent = dungeonImage.transform.parent;
    if (overlayObject.transform.parent != parent)
      overlayObject.transform.SetParent(parent, false);

    overlayObject.transform.SetAsLastSibling();
    return true;
  }

  private void SyncOverlayTransform(RawImage dungeonImage)
  {
    RectTransform source = dungeonImage.rectTransform;
    RectTransform dest = overlayImage.rectTransform;

    dest.localScale = source.localScale;
    dest.anchorMin = source.anchorMin;
    dest.anchorMax = source.anchorMax;
    dest.pivot = source.pivot;
    dest.anchoredPosition = source.anchoredPosition;
    dest.sizeDelta = source.sizeDelta;
    dest.offsetMin = source.offsetMin;
    dest.offsetMax = source.offsetMax;
  }

  private void ApplyOverlayAppearance(RawImage dungeonImage)
  {
    overlayImage.texture = referenceTexture;
    overlayImage.uvRect = dungeonImage.uvRect;
    overlayImage.color = new Color(1f, 1f, 1f, overlayOpacity);
  }

  private void DestroyOverlayObject()
  {
    if (overlayObject != null)
    {
      if (Application.isPlaying)
        Object.Destroy(overlayObject);
      else
        Object.DestroyImmediate(overlayObject);
    }

    overlayObject = null;
    overlayImage = null;
  }

  private bool TryGetViewportRawImage(out RawImage viewportImage)
  {
    if (cachedViewportImage == null)
      cachedViewportImage = FindViewportRawImage();

    viewportImage = cachedViewportImage;
    return viewportImage != null;
  }

  private static RawImage FindViewportRawImage()
  {
    RawImage[] images = Object.FindObjectsByType<RawImage>(
        FindObjectsInactive.Exclude);

    foreach (RawImage image in images)
    {
      if (image == null || image.gameObject.name == OverlayObjectName)
        continue;

      if (image.texture is RenderTexture)
        return image;
    }

    return null;
  }

  private static void RepaintGameViews()
  {
    EditorWindow[] windows =
        Resources.FindObjectsOfTypeAll<EditorWindow>();

    foreach (EditorWindow window in windows)
    {
      if (window != null && window.GetType().Name == "GameView")
        window.Repaint();
    }
  }

  private void SoloPiece(int soloIndex)
  {
    rememberedEnabledStates = new bool[layout.Pieces.Count];

    for (int i = 0; i < layout.Pieces.Count; i++)
      rememberedEnabledStates[i] = layout.Pieces[i].Enabled;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      piece.Enabled =
          i == soloIndex || IsFloorOrCeiling(piece);
    }
  }

  private void RestoreEnabledStates()
  {
    if (rememberedEnabledStates == null)
      return;

    int count = Mathf.Min(
        rememberedEnabledStates.Length,
        layout.Pieces.Count);

    for (int i = 0; i < count; i++)
      layout.Pieces[i].Enabled = rememberedEnabledStates[i];
  }

  private static bool IsFloorOrCeiling(ViewportPiece piece)
  {
    return piece.Graphic == DungeonGraphicType.Floor
        || piece.Graphic == DungeonGraphicType.Ceiling
        || piece.Name == "Floor"
        || piece.Name == "Ceiling";
  }

  private void PersistChanges()
  {
    EditorUtility.SetDirty(layout);
    AssetDatabase.SaveAssets();
    RefreshDungeonRenderer();
  }

  private void SwapPieces(int indexA, int indexB)
  {
    ViewportPiece temp = layout.Pieces[indexA];
    layout.Pieces[indexA] = layout.Pieces[indexB];
    layout.Pieces[indexB] = temp;
  }

  private static void RefreshDungeonRenderer()
  {
    if (!Application.isPlaying)
      return;

    DungeonRenderer renderer =
        Object.FindAnyObjectByType<DungeonRenderer>();

    if (renderer == null)
      return;

    renderer.RequestRedraw();
  }

  private static bool DrawIntStepper(string label, ref int value, int step)
  {
    EditorGUILayout.BeginHorizontal();
    value = EditorGUILayout.IntField(label, value);

    bool changed = false;

    if (GUILayout.Button($"-{step}", GUILayout.Width(36)))
    {
      value -= step;
      changed = true;
    }

    if (GUILayout.Button($"+{step}", GUILayout.Width(36)))
    {
      value += step;
      changed = true;
    }

    EditorGUILayout.EndHorizontal();
    return changed;
  }
}
