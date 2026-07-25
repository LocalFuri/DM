using System.Reflection;
using DM.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class ViewportLayoutEditor : EditorWindow
{
  private static readonly int[] SnapValues = { 1, 2, 4, 8 };
  private static readonly BindingFlags InstanceFlags =
      BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

  private const string OverlayObjectName = "___DM_ViewportReferenceOverlay";
  private const string PrefsLayoutGuidKey = "ViewportLayoutEditor.LayoutGuid";
  private const string PrefsReferenceTextureGuidKey =
      "ViewportLayoutEditor.ReferenceTextureGuid";

  private ViewportLayout layout;
  private Vector2 scroll;
  private bool[] rememberedEnabledStates;
  private int snap = 1;

  private Texture2D referenceTexture;
  private bool showOverlay;
  private float overlayOpacity = 0.5f;
  private RawImage cachedViewportImage;

  private GameObject overlayObject;
  private RawImage overlayImage;

  // --- TEMPORARY OVERLAY DIAGNOSTICS (do not ship) ---
  private const float TestOverlayDurationSeconds = 5f;
  private int diagCallbackCount;
  private double diagLastCallbackTime;
  private string diagLastFailureReason = "No overlay update yet.";
  private bool diagGameViewFound;
  private bool diagRawImageFound;
  private bool diagRawImageActiveEnabled;
  private string diagRawImageTextureName = "-";
  private Rect diagGameViewRect;
  private Rect diagOverlayRect;
  private double testOverlayUntil;
  // --- END TEMPORARY OVERLAY DIAGNOSTICS ---

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
    SaveAssetGuid(PrefsReferenceTextureGuidKey, referenceTexture);

    EditorApplication.update -= MaintainOverlayVisual;
    EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
    DestroyOverlayObject();
    RepaintGameViews();
  }

  private void HandlePlayModeStateChanged(PlayModeStateChange state)
  {
    cachedViewportImage = null;
    diagCallbackCount = 0;
    diagLastCallbackTime = 0;
    diagLastFailureReason = "Play mode state changed.";
    testOverlayUntil = 0;
    DestroyOverlayObject();

    if (state == PlayModeStateChange.ExitingPlayMode)
      RepaintGameViews();
  }

  private void OnGUI()
  {
    DrawOverlayControls();
    DrawTemporaryOverlayDiagnostics();

    EditorGUI.BeginChangeCheck();

    ViewportLayout previousLayout = layout;
    layout = (ViewportLayout)EditorGUILayout.ObjectField(
        "Viewport Layout",
        layout,
        typeof(ViewportLayout),
        false);

    if (layout != previousLayout)
    {
      rememberedEnabledStates = null;
      SaveAssetGuid(PrefsLayoutGuidKey, layout);
    }

    if (layout == null)
    {
      EditorGUI.EndChangeCheck();
      EditorGUILayout.HelpBox("Select a ViewportLayout asset.", MessageType.Info);
      return;
    }

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
      EditorGUILayout.BeginVertical(EditorStyles.helpBox);

      piece.Name = EditorGUILayout.TextField("Name", piece.Name);
      piece.Enabled = EditorGUILayout.Toggle("Enabled", piece.Enabled);
      piece.Graphic = (DungeonGraphicType)EditorGUILayout.EnumPopup("Graphic", piece.Graphic);

      if (DrawIntStepper("X", ref piece.X, snap))
        changed = true;

      if (DrawIntStepper("Y", ref piece.Y, snap))
        changed = true;

      EditorGUILayout.BeginHorizontal();

      using (new EditorGUI.DisabledScope(i <= 0))
      {
        if (GUILayout.Button("Move Up"))
        {
          SwapPieces(i, i - 1);
          changed = true;
        }
      }

      using (new EditorGUI.DisabledScope(i >= layout.Pieces.Count - 1))
      {
        if (GUILayout.Button("Move Down"))
        {
          SwapPieces(i, i + 1);
          changed = true;
        }
      }

      if (GUILayout.Button("Solo"))
      {
        SoloPiece(i);
        PersistChanges();
      }

      EditorGUILayout.EndHorizontal();

      EditorGUILayout.EndVertical();
    }

    EditorGUILayout.EndScrollView();

    if (EditorGUI.EndChangeCheck() || changed)
      PersistChanges();
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
    layout = LoadAssetByGuid<ViewportLayout>(
        EditorPrefs.GetString(PrefsLayoutGuidKey, string.Empty));

    referenceTexture = LoadTextureByGuid(
        EditorPrefs.GetString(PrefsReferenceTextureGuidKey, string.Empty));
  }

  private static T LoadAssetByGuid<T>(string guid) where T : Object
  {
    if (string.IsNullOrEmpty(guid))
      return null;

    string path = AssetDatabase.GUIDToAssetPath(guid);
    if (string.IsNullOrEmpty(path))
      return null;

    return AssetDatabase.LoadAssetAtPath<T>(path);
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

  // --- TEMPORARY OVERLAY DIAGNOSTICS (do not ship) ---
  private void DrawTemporaryOverlayDiagnostics()
  {
    EditorGUILayout.Space();
    EditorGUILayout.LabelField(
        "TEMPORARY Overlay Diagnostics",
        EditorStyles.boldLabel);
    EditorGUILayout.HelpBox(
        "Temporary diagnostics only. Remove after the overlay failure is confirmed.\n" +
        "Overlay now uses a temporary editor-only Canvas RawImage sibling " +
        "(not Game View IMGUI).",
        MessageType.Warning);

    RefreshDiagnosticSnapshot();

    EditorGUILayout.LabelField("Is Playing", Application.isPlaying ? "yes" : "no");
    EditorGUILayout.LabelField("Show Overlay", showOverlay ? "yes" : "no");
    EditorGUILayout.LabelField(
        "Reference Texture assigned",
        referenceTexture != null ? "yes" : "no");
    EditorGUILayout.LabelField(
        "Reference Texture name",
        referenceTexture != null ? referenceTexture.name : "-");
    EditorGUILayout.LabelField("Opacity value", overlayOpacity.ToString("0.000"));
    EditorGUILayout.LabelField("Game View found", diagGameViewFound ? "yes" : "no");
    EditorGUILayout.LabelField("Dungeon RawImage found", diagRawImageFound ? "yes" : "no");
    EditorGUILayout.LabelField(
        "RawImage active and enabled",
        diagRawImageActiveEnabled ? "yes" : "no");
    EditorGUILayout.LabelField("RawImage texture name", diagRawImageTextureName);
    EditorGUILayout.LabelField("Calculated Game View rectangle", FormatRect(diagGameViewRect));
    EditorGUILayout.LabelField(
        "Calculated RawImage overlay rectangle",
        FormatRect(diagOverlayRect));
    EditorGUILayout.LabelField(
        "Overlay drawing callback invocation count",
        diagCallbackCount.ToString());
    EditorGUILayout.LabelField(
        "Last callback time",
        diagLastCallbackTime > 0
            ? diagLastCallbackTime.ToString("0.000")
            : "never");
    EditorGUILayout.LabelField("Last failure reason", diagLastFailureReason);
    EditorGUILayout.LabelField(
        "Temp overlay object exists",
        overlayObject != null ? "yes" : "no");

    bool testActive = IsTestOverlayActive();
    EditorGUILayout.LabelField(
        "Test Overlay active",
        testActive
            ? $"yes ({(testOverlayUntil - EditorApplication.timeSinceStartup):0.0}s left)"
            : "no");

    using (new EditorGUI.DisabledScope(!Application.isPlaying))
    {
      if (GUILayout.Button("Test Overlay"))
      {
        testOverlayUntil =
            EditorApplication.timeSinceStartup + TestOverlayDurationSeconds;
        diagLastFailureReason = "Test Overlay started.";
        MaintainOverlayVisual();
        RepaintGameViews();
        Repaint();
      }
    }

    EditorGUILayout.Space();
  }

  private void RefreshDiagnosticSnapshot()
  {
    EditorWindow gameView = FindGameView();
    diagGameViewFound = gameView != null;

    RawImage rawImage = FindViewportRawImage();
    cachedViewportImage = rawImage;
    diagRawImageFound = rawImage != null;
    diagRawImageActiveEnabled =
        rawImage != null && rawImage.isActiveAndEnabled;
    diagRawImageTextureName =
        rawImage != null && rawImage.texture != null
            ? rawImage.texture.name
            : "-";

    diagGameViewRect = default;
    diagOverlayRect = default;

    if (rawImage != null && TryGetRawImageScreenRect(rawImage, out Rect screenRect))
      diagOverlayRect = screenRect;

    if (gameView != null
        && TryGetGameViewImageRects(
            gameView,
            out _,
            out Rect targetInView,
            out _))
    {
      diagGameViewRect = targetInView;
    }
  }

  private static string FormatRect(Rect rect)
  {
    if (rect.width <= 0f || rect.height <= 0f)
      return "invalid / empty";

    return $"x={rect.x:0.##}, y={rect.y:0.##}, w={rect.width:0.##}, h={rect.height:0.##}";
  }

  private bool IsTestOverlayActive()
  {
    return EditorApplication.timeSinceStartup < testOverlayUntil;
  }
  // --- END TEMPORARY OVERLAY DIAGNOSTICS ---

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

    diagCallbackCount++;
    diagLastCallbackTime = EditorApplication.timeSinceStartup;

    bool testActive = IsTestOverlayActive();
    bool wantReference = showOverlay && referenceTexture != null;

    if (!testActive && !wantReference)
    {
      DestroyOverlayObject();
      diagLastFailureReason =
          "Overlay idle (Show Overlay off / no texture, Test Overlay inactive).";
      Repaint();
      return;
    }

    if (!TryGetViewportRawImage(out RawImage dungeonImage))
    {
      DestroyOverlayObject();
      diagLastFailureReason = "Dungeon RawImage not found.";
      Repaint();
      return;
    }

    if (!dungeonImage.isActiveAndEnabled)
    {
      DestroyOverlayObject();
      diagLastFailureReason = "RawImage found but not active/enabled.";
      Repaint();
      return;
    }

    if (!EnsureOverlayObject(dungeonImage))
    {
      diagLastFailureReason = "Failed to create temporary overlay RawImage.";
      Repaint();
      return;
    }

    SyncOverlayTransform(dungeonImage);
    ApplyOverlayAppearance(dungeonImage, testActive, wantReference);

    if (TryGetRawImageScreenRect(dungeonImage, out Rect screenRect))
      diagOverlayRect = screenRect;

    if (testActive)
      diagLastFailureReason =
          "Test Overlay draw issued (temporary Canvas RawImage).";
    else
      diagLastFailureReason =
          "Reference overlay draw issued (temporary Canvas RawImage).";

    Repaint();
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

  private void ApplyOverlayAppearance(
      RawImage dungeonImage,
      bool testActive,
      bool wantReference)
  {
    if (testActive)
    {
      overlayImage.texture = null;
      overlayImage.uvRect = new Rect(0f, 0f, 1f, 1f);
      overlayImage.color = new Color(1f, 0f, 0f, 0.45f);
      return;
    }

    if (wantReference)
    {
      overlayImage.texture = referenceTexture;
      overlayImage.uvRect = dungeonImage.uvRect;
      overlayImage.color = new Color(1f, 1f, 1f, overlayOpacity);
    }
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

  private static bool TryGetGameViewImageRects(
      EditorWindow gameView,
      out Rect groupRect,
      out Rect targetInView,
      out Vector2 renderSize)
  {
    groupRect = default;
    targetInView = default;
    renderSize = default;

    FieldInfo zoomField = gameView.GetType().GetField("m_ZoomArea", InstanceFlags);
    if (zoomField == null)
      return false;

    object zoomArea = zoomField.GetValue(gameView);
    if (zoomArea == null)
      return false;

    PropertyInfo drawRectProperty =
        zoomArea.GetType().GetProperty("drawRect", InstanceFlags);
    if (drawRectProperty == null)
      return false;

    groupRect = (Rect)drawRectProperty.GetValue(zoomArea);

    PropertyInfo targetInViewProperty =
        gameView.GetType().GetProperty("targetInView", InstanceFlags);
    if (targetInViewProperty == null)
      return false;

    targetInView = (Rect)targetInViewProperty.GetValue(gameView);

    PropertyInfo renderSizeProperty =
        gameView.GetType().GetProperty("targetRenderSize", InstanceFlags);
    if (renderSizeProperty == null)
      return false;

    renderSize = (Vector2)renderSizeProperty.GetValue(gameView);
    return groupRect.width > 1f
        && targetInView.width > 1f
        && renderSize.x > 1f
        && renderSize.y > 1f;
  }

  private static EditorWindow FindGameView()
  {
    EditorWindow[] windows =
        Resources.FindObjectsOfTypeAll<EditorWindow>();

    foreach (EditorWindow window in windows)
    {
      if (window != null && window.GetType().Name == "GameView")
        return window;
    }

    return null;
  }

  private bool TryGetViewportRawImage(out RawImage viewportImage)
  {
    if (cachedViewportImage == null)
      cachedViewportImage = FindViewportRawImage();

    viewportImage = cachedViewportImage;
    return viewportImage != null;
  }

  private static bool TryGetRawImageScreenRect(RawImage image, out Rect rect)
  {
    rect = default;

    if (image == null)
      return false;

    RectTransform transform = image.rectTransform;
    Vector3[] corners = new Vector3[4];
    transform.GetWorldCorners(corners);

    float xMin = Mathf.Min(corners[0].x, corners[2].x);
    float xMax = Mathf.Max(corners[0].x, corners[2].x);
    float yMin = Mathf.Min(corners[0].y, corners[2].y);
    float yMax = Mathf.Max(corners[0].y, corners[2].y);

    rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    return rect.width > 1f && rect.height > 1f;
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
