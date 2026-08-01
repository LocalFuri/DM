using System.IO;
using DM.Dungeon;
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
  private const string PrefsGraphicsGuidKey =
      "ViewportLayoutEditor.GraphicsGuid";
  private const string PrefsPreviewXKey = "ViewportLayoutEditor.PreviewX";
  private const string PrefsPreviewYKey = "ViewportLayoutEditor.PreviewY";
  private const string PrefsPreviewFacingKey =
      "ViewportLayoutEditor.PreviewFacing";
  private const string PrefsSelectedPieceIndexKey =
      "ViewportLayoutEditor.SelectedPieceIndex";

  private const string HallOfChampionsMapPath =
      "Assets/Data/Maps/HallOfChampions.json";

  private const int PreviewWidth = 320;
  private const int PreviewHeight = 200;

  [System.NonSerialized]
  private ViewportLayout layout;
  [System.NonSerialized]
  private DungeonGraphics graphics;
  private Vector2 editorScroll;
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

  private Texture2D editModePreviewTexture;
  private Texture savedViewportTexture;
  private bool viewportTextureStolen;

  // Edit Mode map-pose preview (Hall of Champions).
  private int previewX = 1;
  private int previewY = 3;
  private DungeonFacing previewFacing = DungeonFacing.South;
  private DungeonMap previewMiniMap;
  private string previewMiniMapLoadError;
  private Vector2 previewMiniMapScroll;

  // Temporary 320×200 presentation (restored on close / Play Mode).
  private bool presentationOverrideActive;
  private bool canvasScalerStateSaved;
  private CanvasScaler.ScaleMode savedScalerMode;
  private float savedScalerScaleFactor;
  private Vector2 savedScalerReferenceResolution;
  private float savedScalerMatchWidthOrHeight;
  private bool viewportRectSaved;
  private RectTransformSnapshot savedViewportRect;
  private bool gameplayRootRectSaved;
  private RectTransformSnapshot savedGameplayRootRect;
  private RectTransform cachedGameplayRoot;

  private Image cachedMovementArrows;
  private bool movementArrowsStateSaved;
  private bool savedMovementArrowsActive;
  private Transform savedMovementArrowsParent;
  private int savedMovementArrowsSiblingIndex;
  private RectTransformSnapshot savedMovementArrowsRect;
  private bool savedMovementArrowsPreserveAspect;
  private FilterMode savedMovementArrowsFilterMode;
  private bool movementArrowsFilterSaved;
  private Texture savedMovementArrowsFilterTexture;

  private struct RectTransformSnapshot
  {
    public Vector2 AnchorMin;
    public Vector2 AnchorMax;
    public Vector2 Pivot;
    public Vector2 AnchoredPosition;
    public Vector2 SizeDelta;
    public Vector3 LocalScale;
    public Quaternion LocalRotation;
    public Vector2 OffsetMin;
    public Vector2 OffsetMax;

    public static RectTransformSnapshot Capture(RectTransform rect)
    {
      return new RectTransformSnapshot
      {
        AnchorMin = rect.anchorMin,
        AnchorMax = rect.anchorMax,
        Pivot = rect.pivot,
        AnchoredPosition = rect.anchoredPosition,
        SizeDelta = rect.sizeDelta,
        LocalScale = rect.localScale,
        LocalRotation = rect.localRotation,
        OffsetMin = rect.offsetMin,
        OffsetMax = rect.offsetMax
      };
    }

    public void Apply(RectTransform rect)
    {
      rect.localRotation = LocalRotation;
      rect.localScale = LocalScale;
      rect.anchorMin = AnchorMin;
      rect.anchorMax = AnchorMax;
      rect.pivot = Pivot;
      rect.anchoredPosition = AnchoredPosition;
      rect.sizeDelta = SizeDelta;
      // Stretch layouts need offsets restored after sizeDelta.
      if (AnchorMin != AnchorMax)
      {
        rect.offsetMin = OffsetMin;
        rect.offsetMax = OffsetMax;
      }
    }
  }

  [MenuItem("Tools/Viewport Layout Editor")]
  public static void Open()
  {
    GetWindow<ViewportLayoutEditor>("Viewport Layout");
  }

  private void OnEnable()
  {
    wantsMouseMove = true;
    RestorePersistedAssets();
    ReloadLayoutFromDisk();
    RestoreSessionPrefs();
    EditorApplication.update += MaintainOverlayVisual;
    EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    // Force a fresh compose from the loaded asset Enabled flags.
    DestroyEditModePreviewTextureOnly();
    RefreshEditModePreview();
  }

  private void OnDisable()
  {
    SaveAssetGuid(PrefsLayoutGuidKey, layout);
    SaveAssetGuid(PrefsReferenceTextureGuidKey, referenceTexture);
    SaveAssetGuid(PrefsGraphicsGuidKey, graphics);
    SaveSessionPrefs();

    EditorApplication.update -= MaintainOverlayVisual;
    EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
    DestroyOverlayObject();
    RestoreViewportTextureAndDestroyPreview();
    RepaintGameViews();
  }

  private void HandlePlayModeStateChanged(PlayModeStateChange state)
  {
    cachedViewportImage = null;
    DestroyOverlayObject();

    if (state == PlayModeStateChange.ExitingEditMode
        || state == PlayModeStateChange.EnteredPlayMode)
    {
      // Hand the RawImage back to the scene RenderTexture before Play runs.
      RestoreViewportTextureAndDestroyPreview();
    }

    if (state == PlayModeStateChange.EnteredEditMode)
      RefreshEditModePreview();

    if (state == PlayModeStateChange.ExitingPlayMode)
      RepaintGameViews();
  }

  private void OnGUI()
  {
    selectionChangedThisFrame = false;

    editorScroll = EditorGUILayout.BeginScrollView(editorScroll);

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
      SaveSessionPrefs();
      RefreshEditModePreview();
    }

    EditorGUI.BeginChangeCheck();
    DungeonGraphics newGraphics = (DungeonGraphics)EditorGUILayout.ObjectField(
        "Dungeon Graphics",
        graphics,
        typeof(DungeonGraphics),
        false);
    if (EditorGUI.EndChangeCheck())
    {
      graphics = newGraphics;
      SaveAssetGuid(PrefsGraphicsGuidKey, graphics);
      RefreshEditModePreview();
    }

    if (layout == null)
    {
      EditorGUILayout.HelpBox("Select a ViewportLayout asset.", MessageType.Info);
      EditorGUILayout.EndScrollView();
      return;
    }

    if (graphics == null)
    {
      EditorGUILayout.HelpBox(
          "Select a DungeonGraphics asset (textures for the Edit Mode preview).",
          MessageType.Warning);
    }

    ClampSelectedPieceIndex();
    HandlePieceKeyboardNudge();
    HandlePreviewFacingKeyboard();
    DrawSelectedPieceHeader();

    DrawMapPosePreviewControls();

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
    {
      RefreshEditModePreview();
      Repaint();
    }
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
    piece.MirrorHorizontally = EditorGUILayout.Toggle(
        "Mirror Horizontally",
        piece.MirrorHorizontally);

    if (StraightF1WallLogic.IsFloorOrCeilingGraphic(piece.Graphic))
    {
      EditorGUILayout.HelpBox(
          "Refresh From Map sets Ceiling/Floor mirror from the shared "
              + "environment phase (same parity as F1 A/B).",
          MessageType.None);
    }

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
      SaveSessionPrefs();
      GUI.FocusControl(null);
      return;
    }

    int clamped = Mathf.Clamp(index, 0, layout.Pieces.Count - 1);
    if (clamped != selectedPieceIndex)
      selectionChangedThisFrame = true;

    selectedPieceIndex = clamped;
    SaveSessionPrefs();
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

  private void HandlePreviewFacingKeyboard()
  {
    Event current = Event.current;
    if (current.type != EventType.KeyDown)
      return;

    // Only while this editor window has keyboard focus.
    if (focusedWindow != this)
      return;

    if (EditorGUIUtility.editingTextField)
      return;

    DungeonFacing nextFacing;
    switch (current.keyCode)
    {
      case KeyCode.Delete:
        nextFacing = TurnPreviewFacingLeft(previewFacing);
        break;
      case KeyCode.PageDown:
        nextFacing = TurnPreviewFacingRight(previewFacing);
        break;
      default:
        return;
    }

    current.Use();

    if (nextFacing == previewFacing)
      return;

    // Preserve Preview X / Preview Y; only facing changes.
    previewFacing = nextFacing;
    SaveSessionPrefs();
    RefreshEnabledPiecesFromMapPose();
  }

  private static DungeonFacing TurnPreviewFacingLeft(DungeonFacing facing)
  {
    return facing switch
    {
      DungeonFacing.North => DungeonFacing.West,
      DungeonFacing.West => DungeonFacing.South,
      DungeonFacing.South => DungeonFacing.East,
      DungeonFacing.East => DungeonFacing.North,
      _ => facing
    };
  }

  private static DungeonFacing TurnPreviewFacingRight(DungeonFacing facing)
  {
    return facing switch
    {
      DungeonFacing.North => DungeonFacing.East,
      DungeonFacing.East => DungeonFacing.South,
      DungeonFacing.South => DungeonFacing.West,
      DungeonFacing.West => DungeonFacing.North,
      _ => facing
    };
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
      MaintainMovementArrowsPreview();
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

    graphics = LoadDungeonGraphicsByGuid(
        EditorPrefs.GetString(PrefsGraphicsGuidKey, string.Empty));

    if (graphics == null)
      graphics = FindSingleDungeonGraphicsAsset();

    if (graphics != null)
      SaveAssetGuid(PrefsGraphicsGuidKey, graphics);
  }

  /// <summary>
  /// Re-load ViewportLayout from disk so Edit Mode preview matches the .asset
  /// Enabled flags (avoids stale in-memory ScriptableObject state).
  /// </summary>
  private void ReloadLayoutFromDisk()
  {
    if (layout == null)
      return;

    string path = AssetDatabase.GetAssetPath(layout);
    if (string.IsNullOrEmpty(path))
      return;

    AssetDatabase.ImportAsset(
        path,
        ImportAssetOptions.ForceUpdate
            | ImportAssetOptions.ForceSynchronousImport);

    ViewportLayout reloaded =
        AssetDatabase.LoadAssetAtPath<ViewportLayout>(path);
    if (reloaded != null)
      layout = reloaded;
  }

  private void DestroyEditModePreviewTextureOnly()
  {
    if (editModePreviewTexture == null)
      return;

    Object.DestroyImmediate(editModePreviewTexture);
    editModePreviewTexture = null;
  }

  private void RestoreSessionPrefs()
  {
    previewX = EditorPrefs.GetInt(PrefsPreviewXKey, 1);
    previewY = EditorPrefs.GetInt(PrefsPreviewYKey, 3);

    int facingValue = EditorPrefs.GetInt(
        PrefsPreviewFacingKey,
        (int)DungeonFacing.South);
    if (System.Enum.IsDefined(typeof(DungeonFacing), facingValue))
      previewFacing = (DungeonFacing)facingValue;
    else
      previewFacing = DungeonFacing.South;

    selectedPieceIndex = EditorPrefs.GetInt(PrefsSelectedPieceIndexKey, 0);
    ClampSelectedPieceIndex();
  }

  private void SaveSessionPrefs()
  {
    EditorPrefs.SetInt(PrefsPreviewXKey, previewX);
    EditorPrefs.SetInt(PrefsPreviewYKey, previewY);
    EditorPrefs.SetInt(PrefsPreviewFacingKey, (int)previewFacing);
    EditorPrefs.SetInt(PrefsSelectedPieceIndexKey, selectedPieceIndex);
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

  private static DungeonGraphics LoadDungeonGraphicsByGuid(string guid)
  {
    if (string.IsNullOrEmpty(guid))
      return null;

    string path = AssetDatabase.GUIDToAssetPath(guid);
    if (string.IsNullOrEmpty(path))
      return null;

    return AssetDatabase.LoadAssetAtPath<DungeonGraphics>(path);
  }

  private static DungeonGraphics FindSingleDungeonGraphicsAsset()
  {
    string[] guids = AssetDatabase.FindAssets("t:DungeonGraphics");
    if (guids == null || guids.Length != 1)
      return null;

    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
    if (string.IsNullOrEmpty(path))
      return null;

    return AssetDatabase.LoadAssetAtPath<DungeonGraphics>(path);
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
    // Never draw the reference overlay during Play Mode — it is a
    // duplicate RawImage on the Canvas and shows up over gameplay.
    if (Application.isPlaying)
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
    MaintainMovementArrowsPreview();
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

    Transform parent = dungeonImage.transform;
    if (overlayObject.transform.parent != parent)
      overlayObject.transform.SetParent(parent, false);

    overlayObject.transform.SetAsLastSibling();
    return true;
  }

  private void SyncOverlayTransform(RawImage dungeonImage)
  {
    RectTransform dest = overlayImage.rectTransform;

    // Fill the 320×200 preview RawImage so piece UI (arrows) can draw above.
    dest.anchorMin = Vector2.zero;
    dest.anchorMax = Vector2.one;
    dest.pivot = new Vector2(0.5f, 0.5f);
    dest.anchoredPosition = Vector2.zero;
    dest.sizeDelta = Vector2.zero;
    dest.offsetMin = Vector2.zero;
    dest.offsetMax = Vector2.zero;
    dest.localScale = Vector3.one;
    dest.localRotation = Quaternion.identity;
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

      if (image.gameObject.name == "DungeonViewport")
        return image;
    }

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

  private void DrawMapPosePreviewControls()
  {
    EditorGUILayout.Space();
    EditorGUILayout.LabelField(
        "Map Pose Preview",
        EditorStyles.boldLabel);
    EditorGUILayout.HelpBox(
        "Hall of Champions — enables wall pieces for the pose "
            + "(same visibility rules as runtime).",
        MessageType.None);

    EditorGUI.BeginChangeCheck();
    previewX = EditorGUILayout.IntField("Preview X", previewX);
    previewY = EditorGUILayout.IntField("Preview Y", previewY);
    previewFacing = (DungeonFacing)EditorGUILayout.EnumPopup(
        "Preview Facing",
        previewFacing);
    if (EditorGUI.EndChangeCheck())
    {
      SaveSessionPrefs();
      Repaint();
    }

    if (GUILayout.Button("Refresh From Map"))
      RefreshEnabledPiecesFromMapPose();

    DrawEnvironmentPhaseStatus();
    DrawPreviewMiniMap();
  }

  private void DrawEnvironmentPhaseStatus()
  {
    EnsurePreviewMiniMapLoaded();
    if (previewMiniMap == null)
      return;

    bool phaseB = StraightF1WallLogic.IsEnvironmentPhaseB(
        previewFacing,
        previewX,
        previewY);

    string phaseLabel = phaseB
        ? "B (mirrored)"
        : "A (normal)";

    EditorGUILayout.Space();
    EditorGUILayout.LabelField(
        "Environment Phase",
        EditorStyles.boldLabel);
    EditorGUILayout.LabelField("Active phase", phaseLabel);
    EditorGUILayout.LabelField(
        "Ceiling / Floor / F1 share this phase across the 224×136 view.",
        EditorStyles.miniLabel);
    EditorGUILayout.LabelField(
        "Phase mirror (Ceiling & Floor)",
        phaseB ? "ON" : "OFF");

    ViewportPiece ceiling = FindPieceByGraphic(DungeonGraphicType.Ceiling);
    ViewportPiece floor = FindPieceByGraphic(DungeonGraphicType.Floor);
    if (ceiling != null)
    {
      EditorGUILayout.LabelField(
          "Ceiling Mirror Horizontally (stored)",
          ceiling.MirrorHorizontally ? "ON" : "OFF");
    }

    if (floor != null)
    {
      EditorGUILayout.LabelField(
          "Floor Mirror Horizontally (stored)",
          floor.MirrorHorizontally ? "ON" : "OFF");
    }
  }

  private ViewportPiece FindPieceByGraphic(DungeonGraphicType graphic)
  {
    if (layout == null || layout.Pieces == null)
      return null;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece != null && piece.Graphic == graphic)
        return piece;
    }

    return null;
  }

  private void DrawPreviewMiniMap()
  {
    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Mini-map", EditorStyles.boldLabel);

    EnsurePreviewMiniMapLoaded();

    if (previewMiniMap == null)
    {
      EditorGUILayout.HelpBox(
          string.IsNullOrEmpty(previewMiniMapLoadError)
              ? "Could not load Hall of Champions map."
              : previewMiniMapLoadError,
          MessageType.Warning);
      return;
    }

    previewMiniMapScroll = DungeonMiniMapGui.Draw(
        previewMiniMap,
        previewX,
        previewY,
        previewFacing,
        previewMiniMapScroll,
        interactive: true,
        out DungeonMiniMapGui.InteractionResult interaction
    );

    if (interaction.HasHover
        && Event.current.type == EventType.MouseMove)
    {
      Repaint();
    }

    if (interaction.ClickedOpenTile)
      ApplyPreviewPoseFromMiniMapClick(
          interaction.ClickX,
          interaction.ClickY);
  }

  private void ApplyPreviewPoseFromMiniMapClick(int x, int y)
  {
    previewX = x;
    previewY = y;
    // Preview Facing is unchanged.
    SaveSessionPrefs();
    RefreshEnabledPiecesFromMapPose();
    Repaint();
  }

  private void EnsurePreviewMiniMapLoaded()
  {
    if (previewMiniMap != null)
      return;

    if (!File.Exists(HallOfChampionsMapPath))
    {
      previewMiniMapLoadError =
          "Map not found at " + HallOfChampionsMapPath;
      return;
    }

    try
    {
      string json = File.ReadAllText(HallOfChampionsMapPath);
      previewMiniMap = DungeonMap.LoadFromJsonText(json);
      previewMiniMapLoadError = null;
    }
    catch (System.Exception ex)
    {
      previewMiniMap = null;
      previewMiniMapLoadError = ex.Message;
    }
  }

  private void RefreshEnabledPiecesFromMapPose()
  {
    if (layout == null)
      return;

    if (!File.Exists(HallOfChampionsMapPath))
    {
      Debug.LogError(
          "ViewportLayoutEditor: Map not found at "
              + HallOfChampionsMapPath);
      return;
    }

    DungeonMap map;
    try
    {
      string json = File.ReadAllText(HallOfChampionsMapPath);
      map = DungeonMap.LoadFromJsonText(json);
      map.SetPlayerPose(previewX, previewY, previewFacing);
    }
    catch (System.Exception ex)
    {
      Debug.LogError(
          "ViewportLayoutEditor: Failed to load map pose "
              + $"({previewX},{previewY}) facing {previewFacing}: "
              + ex.Message);
      return;
    }

    rememberedEnabledStates = null;

    bool centerF1Visible = IsCenterFrontWallVisible(map, 1);
    bool phaseB = StraightF1WallLogic.IsEnvironmentPhaseB(
        map.PlayerFacing,
        map.PlayerX,
        map.PlayerY);

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];

      if (StraightF1WallLogic.IsFloorOrCeilingGraphic(piece.Graphic))
      {
        piece.Enabled = true;
        piece.MirrorHorizontally = phaseB;
        continue;
      }

      // Keep authored Enabled; do not auto-enable investigation strips.
      if (piece.Graphic == DungeonGraphicType.CeilingStrip84
          || piece.Graphic == DungeonGraphicType.CeilingStrip85)
      {
        continue;
      }

      if (IsAlwaysDrawnBackgroundPiece(piece))
      {
        piece.Enabled = true;
        continue;
      }

      // UI chrome keeps its own Enabled; map pose only drives walls.
      if (piece.Graphic == DungeonGraphicType.MovementArrows)
        continue;

      if (StraightF1WallLogic.IsStraightF1FrontGraphic(piece.Graphic))
      {
        if (!centerF1Visible)
        {
          piece.Enabled = false;
          continue;
        }

        if (piece.Graphic == DungeonGraphicType.FrontWallF1)
        {
          piece.Enabled = true;
          piece.MirrorHorizontally = phaseB;
          continue;
        }

        bool isB = piece.Graphic == DungeonGraphicType.FrontWallF1_B;
        piece.Enabled = isB ? phaseB : !phaseB;
        // A = normal, B = mirrored (also driven by phase at runtime).
        piece.MirrorHorizontally = isB;
        continue;
      }

      // Flat continuous F1: suppress perspective wedges.
      if (centerF1Visible
          && (piece.Graphic == DungeonGraphicType.WallF1L
              || piece.Graphic == DungeonGraphicType.WallF1R))
      {
        piece.Enabled = false;
        continue;
      }

      if (!IsDepthWallGraphic(piece.Graphic))
      {
        // S2/S3 and ornaments have no map visibility rule yet.
        piece.Enabled = false;
        continue;
      }

      piece.Enabled =
          IsDepthWallVisibleAtPose(map, piece.Graphic);
    }

    PersistChanges();
    SaveSessionPrefs();
    Repaint();
  }

  private static bool IsAlwaysDrawnBackgroundPiece(ViewportPiece piece)
  {
    return piece.Graphic == DungeonGraphicType.Floor
        || piece.Graphic == DungeonGraphicType.Ceiling
        || piece.Name == "Floor"
        || piece.Name == "Ceiling";
  }

  // --- Visibility helpers (mirror DungeonRenderer; Edit Mode only) ---

  private static bool IsDepthWallGraphic(DungeonGraphicType graphic)
  {
    switch (graphic)
    {
      case DungeonGraphicType.WallF0L:
      case DungeonGraphicType.WallF0R:
      case DungeonGraphicType.WallF1L:
      case DungeonGraphicType.WallF1R:
      case DungeonGraphicType.WallF2L:
      case DungeonGraphicType.WallF2R:
      case DungeonGraphicType.WallF3L:
      case DungeonGraphicType.WallF3R:
      case DungeonGraphicType.FrontWallF1:
      case DungeonGraphicType.FrontWallF1_A:
      case DungeonGraphicType.FrontWallF1_B:
      case DungeonGraphicType.FrontWallF2:
      case DungeonGraphicType.FrontWallF3:
        return true;
      default:
        return false;
    }
  }

  private static bool TryGetCenterFrontWallDepth(
      DungeonGraphicType graphic,
      out int depth)
  {
    switch (graphic)
    {
      case DungeonGraphicType.FrontWallF1:
      case DungeonGraphicType.FrontWallF1_A:
      case DungeonGraphicType.FrontWallF1_B:
        depth = 1;
        return true;
      case DungeonGraphicType.FrontWallF2:
        depth = 2;
        return true;
      case DungeonGraphicType.FrontWallF3:
        depth = 3;
        return true;
      default:
        depth = 0;
        return false;
    }
  }

  private static bool TryGetSideWallDepthAndSide(
      DungeonGraphicType graphic,
      out int depth,
      out bool isLeft)
  {
    switch (graphic)
    {
      case DungeonGraphicType.WallF0L:
        depth = 0;
        isLeft = true;
        return true;
      case DungeonGraphicType.WallF0R:
        depth = 0;
        isLeft = false;
        return true;
      case DungeonGraphicType.WallF1L:
        depth = 1;
        isLeft = true;
        return true;
      case DungeonGraphicType.WallF1R:
        depth = 1;
        isLeft = false;
        return true;
      case DungeonGraphicType.WallF2L:
        depth = 2;
        isLeft = true;
        return true;
      case DungeonGraphicType.WallF2R:
        depth = 2;
        isLeft = false;
        return true;
      case DungeonGraphicType.WallF3L:
        depth = 3;
        isLeft = true;
        return true;
      case DungeonGraphicType.WallF3R:
        depth = 3;
        isLeft = false;
        return true;
      default:
        depth = 0;
        isLeft = false;
        return false;
    }
  }

  private static bool IsDepthWallVisibleAtPose(
      DungeonMap map,
      DungeonGraphicType graphic)
  {
    if (TryGetCenterFrontWallDepth(graphic, out int centerDepth))
    {
      if (!IsCenterFrontWallVisible(map, centerDepth))
        return false;

      if (!StraightF1WallLogic.IsStraightF1FrontGraphic(graphic)
          || centerDepth != 1)
      {
        return true;
      }

      DungeonMap.GetForwardOffset(
          map.PlayerFacing,
          out int forwardX,
          out int forwardY);

      int wallX = map.PlayerX + forwardX;
      int wallY = map.PlayerY + forwardY;

      return StraightF1WallLogic.IsVariantVisibleForWall(
          graphic,
          map.PlayerFacing,
          wallX,
          wallY,
          centerWallPresent: true
      );
    }

    if (TryGetSideWallDepthAndSide(
            graphic,
            out int sideDepth,
            out bool isLeft))
    {
      return IsSideWallVisible(map, sideDepth, isLeft);
    }

    return false;
  }

  private static bool IsCenterFrontWallVisible(
      DungeonMap map,
      int depth)
  {
    if (depth < 1)
      return false;

    if (IsFrontDepthOccluded(map, depth))
      return false;

    DungeonMap.GetForwardOffset(
        map.PlayerFacing,
        out int forwardX,
        out int forwardY);

    int tileX = map.PlayerX + forwardX * depth;
    int tileY = map.PlayerY + forwardY * depth;

    return IsWallTile(map, tileX, tileY);
  }

  private static bool IsSideWallVisible(
      DungeonMap map,
      int depth,
      bool isLeft)
  {
    // F0 sides stay independent of nearer front occlusion.
    if (depth > 0 && IsFrontDepthOccluded(map, depth))
      return false;

    // F2: centre wall draws FrontWallF2 alone; suppress F2L/R.
    // Straight F1 A/B: centre wrap alone; suppress F1L/R.
    if (depth == 1 && IsCenterFrontWallVisible(map, 1))
      return false;

    if (depth == 2 && IsCenterFrontWallVisible(map, 2))
      return false;

    DungeonMap.GetForwardOffset(
        map.PlayerFacing,
        out int forwardX,
        out int forwardY);

    DungeonMap.GetRightOffset(
        map.PlayerFacing,
        out int rightX,
        out int rightY);

    int sideX = isLeft ? -rightX : rightX;
    int sideY = isLeft ? -rightY : rightY;

    int tileX =
        map.PlayerX + forwardX * depth + sideX;
    int tileY =
        map.PlayerY + forwardY * depth + sideY;

    return IsWallTile(map, tileX, tileY);
  }

  private static bool IsFrontDepthOccluded(DungeonMap map, int depth)
  {
    if (depth <= 0)
      return false;

    DungeonMap.GetForwardOffset(
        map.PlayerFacing,
        out int forwardX,
        out int forwardY);

    for (int nearer = 1; nearer < depth; nearer++)
    {
      int tileX = map.PlayerX + forwardX * nearer;
      int tileY = map.PlayerY + forwardY * nearer;

      if (IsWallTile(map, tileX, tileY))
        return true;
    }

    return false;
  }

  private static bool IsWallTile(DungeonMap map, int x, int y)
  {
    if (!map.IsInside(x, y))
      return true;

    return map.GetTile(x, y).Type == DungeonTileType.Wall;
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
          i == soloIndex || IsFloorOrCeiling(piece)
          || piece.Graphic == DungeonGraphicType.MovementArrows;
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
    RefreshEditModePreview();
  }

  private void SwapPieces(int indexA, int indexB)
  {
    ViewportPiece temp = layout.Pieces[indexA];
    layout.Pieces[indexA] = layout.Pieces[indexB];
    layout.Pieces[indexB] = temp;
  }

  private void RefreshEditModePreview()
  {
    if (Application.isPlaying)
      return;

    if (layout == null || graphics == null)
    {
      RestoreViewportTextureAndDestroyPreview();
      RepaintGameViews();
      return;
    }

    if (!TryGetViewportRawImage(out RawImage dungeonImage))
      return;

    EnsureEditModePreviewTexture();
    ComposeEditModePreview();
    StealViewportTextureIfNeeded(dungeonImage);
    ApplyExact320x200EditModePresentation(dungeonImage);
    dungeonImage.texture = editModePreviewTexture;
    dungeonImage.uvRect = new Rect(0f, 0f, 1f, 1f);
    MaintainOverlayVisual();
    MaintainMovementArrowsPreview();
    RepaintGameViews();
  }

  private void ApplyExact320x200EditModePresentation(RawImage dungeonImage)
  {
    if (dungeonImage == null)
      return;

    ApplyConstantPixelCanvasScaler(dungeonImage);

    RectTransform gameplayRoot = FindGameplayRoot(dungeonImage);
    if (gameplayRoot != null)
    {
      if (!gameplayRootRectSaved)
      {
        savedGameplayRootRect =
            RectTransformSnapshot.Capture(gameplayRoot);
        gameplayRootRectSaved = true;
        cachedGameplayRoot = gameplayRoot;
      }

      ApplyCentered320x200Rect(gameplayRoot);
    }

    RectTransform viewportRect = dungeonImage.rectTransform;
    if (!viewportRectSaved)
    {
      savedViewportRect = RectTransformSnapshot.Capture(viewportRect);
      viewportRectSaved = true;
    }

    ApplyCentered320x200Rect(viewportRect);
    presentationOverrideActive = true;
  }

  private void MaintainMovementArrowsPreview()
  {
    if (Application.isPlaying)
    {
      RestoreMovementArrowsOverride();
      return;
    }

    if (layout == null
        || graphics == null
        || !presentationOverrideActive)
    {
      RestoreMovementArrowsOverride();
      return;
    }

    if (!TryGetViewportRawImage(out RawImage dungeonImage))
    {
      RestoreMovementArrowsOverride();
      return;
    }

    ViewportPiece arrowsPiece = FindMovementArrowsPiece();
    if (arrowsPiece == null)
    {
      RestoreMovementArrowsOverride();
      return;
    }

    Image arrows = FindMovementArrowsImage();
    if (arrows == null)
      return;

    CaptureMovementArrowsStateIfNeeded(arrows);

    RectTransform arrowsRect = arrows.rectTransform;
    if (arrowsRect.parent != dungeonImage.rectTransform)
      arrowsRect.SetParent(dungeonImage.rectTransform, false);

    MovementArrowsLayout.Apply(
        arrows,
        arrowsPiece.X,
        arrowsPiece.Y,
        arrowsPiece.Enabled);

    arrowsRect.SetAsLastSibling();
  }

  private ViewportPiece FindMovementArrowsPiece()
  {
    if (layout == null || layout.Pieces == null)
      return null;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece != null
          && piece.Graphic == DungeonGraphicType.MovementArrows)
      {
        return piece;
      }
    }

    return null;
  }

  private static Image FindMovementArrowsImage()
  {
    Image[] images = Object.FindObjectsByType<Image>(
        FindObjectsInactive.Include);

    for (int i = 0; i < images.Length; i++)
    {
      Image image = images[i];
      if (image != null && image.gameObject.name == "MovementArrows")
        return image;
    }

    return null;
  }

  private void CaptureMovementArrowsStateIfNeeded(Image arrows)
  {
    if (movementArrowsStateSaved || arrows == null)
      return;

    RectTransform rect = arrows.rectTransform;
    savedMovementArrowsActive = arrows.gameObject.activeSelf;
    savedMovementArrowsParent = rect.parent;
    savedMovementArrowsSiblingIndex = rect.GetSiblingIndex();
    savedMovementArrowsRect = RectTransformSnapshot.Capture(rect);
    savedMovementArrowsPreserveAspect = arrows.preserveAspect;

    Texture texture = arrows.mainTexture;
    if (texture != null)
    {
      savedMovementArrowsFilterTexture = texture;
      savedMovementArrowsFilterMode = texture.filterMode;
      movementArrowsFilterSaved = true;
    }

    cachedMovementArrows = arrows;
    movementArrowsStateSaved = true;
  }

  private void RestoreMovementArrowsOverride()
  {
    if (!movementArrowsStateSaved)
      return;

    Image arrows = cachedMovementArrows;
    if (arrows == null)
      arrows = FindMovementArrowsImage();

    if (arrows != null)
    {
      RectTransform arrowsRect = arrows.rectTransform;

      if (savedMovementArrowsParent != null)
        arrowsRect.SetParent(savedMovementArrowsParent, false);
      else if (arrowsRect.parent != null)
        arrowsRect.SetParent(null, false);

      savedMovementArrowsRect.Apply(arrowsRect);

      int siblingCount = arrowsRect.parent != null
          ? arrowsRect.parent.childCount
          : 0;
      if (siblingCount > 0)
      {
        arrowsRect.SetSiblingIndex(
            Mathf.Clamp(
                savedMovementArrowsSiblingIndex,
                0,
                siblingCount - 1));
      }

      arrows.preserveAspect = savedMovementArrowsPreserveAspect;
      arrows.gameObject.SetActive(savedMovementArrowsActive);
    }

    if (movementArrowsFilterSaved
        && savedMovementArrowsFilterTexture != null)
    {
      savedMovementArrowsFilterTexture.filterMode =
          savedMovementArrowsFilterMode;
    }

    movementArrowsStateSaved = false;
    movementArrowsFilterSaved = false;
    savedMovementArrowsFilterTexture = null;
    savedMovementArrowsParent = null;
    cachedMovementArrows = null;
  }

  private void ApplyConstantPixelCanvasScaler(RawImage dungeonImage)
  {
    if (dungeonImage.canvas == null)
      return;

    CanvasScaler scaler =
        dungeonImage.canvas.GetComponent<CanvasScaler>();
    if (scaler == null)
      return;

    if (!canvasScalerStateSaved)
    {
      savedScalerMode = scaler.uiScaleMode;
      savedScalerScaleFactor = scaler.scaleFactor;
      savedScalerReferenceResolution = scaler.referenceResolution;
      savedScalerMatchWidthOrHeight = scaler.matchWidthOrHeight;
      canvasScalerStateSaved = true;
    }

    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
    scaler.scaleFactor = 1f;
  }

  private static void ApplyCentered320x200Rect(RectTransform rect)
  {
    rect.anchorMin = new Vector2(0.5f, 0.5f);
    rect.anchorMax = new Vector2(0.5f, 0.5f);
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = Vector2.zero;
    rect.sizeDelta = new Vector2(PreviewWidth, PreviewHeight);
    rect.localScale = Vector3.one;
    rect.localRotation = Quaternion.identity;
  }

  private static RectTransform FindGameplayRoot(RawImage dungeonImage)
  {
    if (dungeonImage == null)
      return null;

    Transform t = dungeonImage.transform;
    while (t != null)
    {
      if (t.name == "GameplayRoot")
        return t as RectTransform;
      t = t.parent;
    }

    if (dungeonImage.canvas == null)
      return null;

    Transform found =
        dungeonImage.canvas.transform.Find("GameplayRoot");
    return found as RectTransform;
  }

  private void RestoreEditModePresentationOverrides()
  {
    RestoreMovementArrowsOverride();

    if (!presentationOverrideActive
        && !canvasScalerStateSaved
        && !viewportRectSaved
        && !gameplayRootRectSaved)
    {
      return;
    }

    RawImage dungeonImage = null;
    TryGetViewportRawImage(out dungeonImage);

    if (viewportRectSaved && dungeonImage != null)
      savedViewportRect.Apply(dungeonImage.rectTransform);

    if (canvasScalerStateSaved
        && dungeonImage != null
        && dungeonImage.canvas != null)
    {
      CanvasScaler scaler =
          dungeonImage.canvas.GetComponent<CanvasScaler>();
      if (scaler != null)
      {
        scaler.uiScaleMode = savedScalerMode;
        scaler.scaleFactor = savedScalerScaleFactor;
        scaler.referenceResolution =
            savedScalerReferenceResolution;
        scaler.matchWidthOrHeight =
            savedScalerMatchWidthOrHeight;
      }
    }

    if (gameplayRootRectSaved)
    {
      RectTransform gameplayRoot = cachedGameplayRoot;
      if (gameplayRoot == null && dungeonImage != null)
        gameplayRoot = FindGameplayRoot(dungeonImage);

      if (gameplayRoot != null)
        savedGameplayRootRect.Apply(gameplayRoot);
    }

    canvasScalerStateSaved = false;
    viewportRectSaved = false;
    gameplayRootRectSaved = false;
    cachedGameplayRoot = null;
    presentationOverrideActive = false;
  }

  private void EnsureEditModePreviewTexture()
  {
    if (editModePreviewTexture != null
        && editModePreviewTexture.width == PreviewWidth
        && editModePreviewTexture.height == PreviewHeight)
    {
      return;
    }

    if (editModePreviewTexture != null)
      Object.DestroyImmediate(editModePreviewTexture);

    editModePreviewTexture = new Texture2D(
        PreviewWidth,
        PreviewHeight,
        TextureFormat.RGBA32,
        false)
    {
      name = "ViewportLayoutEditModePreview",
      filterMode = FilterMode.Point,
      wrapMode = TextureWrapMode.Clamp,
      hideFlags = HideFlags.HideAndDontSave
    };
  }

  private void ComposeEditModePreview()
  {
    Color32 magenta = new Color32(255, 0, 255, 255);
    Color32[] pixels = new Color32[PreviewWidth * PreviewHeight];
    for (int i = 0; i < pixels.Length; i++)
      pixels[i] = magenta;

    if (layout != null && layout.Pieces != null)
    {
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null || !piece.Enabled)
          continue;

        if (piece.Graphic == DungeonGraphicType.MovementArrows)
          continue;

        Texture2D texture = graphics.GetTexture(piece.Graphic);
        if (texture == null)
          continue;

        if (StraightF1WallLogic.IsStraightF1FrontGraphic(piece.Graphic))
        {
          StraightF1WallLogic.BlitWrapToBuffer(
              texture,
              pixels,
              PreviewWidth,
              PreviewHeight,
              piece.Y,
              piece.MirrorHorizontally);
          continue;
        }

        if (StraightF1WallLogic.IsFloorOrCeilingGraphic(piece.Graphic))
        {
          StraightF1WallLogic.BlitViewportComponentToBuffer(
              texture,
              pixels,
              PreviewWidth,
              PreviewHeight,
              piece.X,
              piece.Y,
              piece.MirrorHorizontally);
          continue;
        }

        Texture2D mask =
            graphics.GetMask(piece.Graphic, out bool flipMaskX);
        BlitPieceIntoPreview(
            pixels,
            texture,
            mask,
            flipMaskX,
            piece.X,
            piece.Y,
            piece.MirrorHorizontally);
      }
    }

    editModePreviewTexture.SetPixels32(pixels);
    editModePreviewTexture.Apply(false);
  }

  private static void BlitPieceIntoPreview(
      Color32[] dest,
      Texture2D source,
      Texture2D mask,
      bool flipMaskHorizontal,
      int destinationX,
      int destinationY,
      bool mirrorHorizontally = false)
  {
    if (!source.isReadable)
      return;

    Color32[] sourcePixels = source.GetPixels32();
    Color32[] maskPixels = null;
    bool useMask = false;

    if (mask != null
        && mask.isReadable
        && mask.width == source.width
        && mask.height == source.height)
    {
      maskPixels = mask.GetPixels32();
      useMask = true;
    }

    for (int sourceY = 0; sourceY < source.height; sourceY++)
    {
      int targetY = destinationY + sourceY;
      if (targetY < 0 || targetY >= PreviewHeight)
        continue;

      for (int column = 0; column < source.width; column++)
      {
        int sourceX = mirrorHorizontally
            ? source.width - 1 - column
            : column;
        int targetX = destinationX + column;
        if (targetX < 0 || targetX >= PreviewWidth)
          continue;

        if (useMask)
        {
          int maskX = flipMaskHorizontal
              ? mask.width - 1 - sourceX
              : sourceX;
          Color32 maskColour =
              maskPixels[sourceY * mask.width + maskX];
          if (maskColour.r < 128
              && maskColour.g < 128
              && maskColour.b < 128)
          {
            continue;
          }
        }

        Color32 sourceColour =
            sourcePixels[sourceY * source.width + sourceX];
        if (sourceColour.a == 0)
          continue;

        dest[targetY * PreviewWidth + targetX] = sourceColour;
      }
    }
  }

  private void StealViewportTextureIfNeeded(RawImage dungeonImage)
  {
    if (viewportTextureStolen)
      return;

    savedViewportTexture = dungeonImage.texture;
    viewportTextureStolen = true;
  }

  private void RestoreViewportTextureAndDestroyPreview()
  {
    RestoreEditModePresentationOverrides();

    if (TryGetViewportRawImage(out RawImage dungeonImage)
        && viewportTextureStolen)
    {
      dungeonImage.texture = savedViewportTexture;
    }

    savedViewportTexture = null;
    viewportTextureStolen = false;
    cachedViewportImage = null;

    if (editModePreviewTexture != null)
    {
      Object.DestroyImmediate(editModePreviewTexture);
      editModePreviewTexture = null;
    }
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
