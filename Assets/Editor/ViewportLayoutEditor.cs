using DM.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class ViewportLayoutEditor : EditorWindow
{
  private static readonly int[] SnapValues = { 1, 2, 4, 8 };

  private ViewportLayout layout;
  private Vector2 scroll;
  private bool[] rememberedEnabledStates;
  private int snap = 1;

  private Texture2D referenceTexture;
  private bool showOverlay;
  private float overlayOpacity = 0.5f;
  private RawImage cachedViewportImage;

  [MenuItem("Tools/Dungeon Master/Viewport Layout Editor")]
  public static void Open()
  {
    GetWindow<ViewportLayoutEditor>("Viewport Layout");
  }

  private void OnEnable()
  {
    RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
    EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
  }

  private void OnDisable()
  {
    RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
    EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
    RepaintGameViews();
  }

  private void HandlePlayModeStateChanged(PlayModeStateChange state)
  {
    cachedViewportImage = null;

    if (state == PlayModeStateChange.ExitingPlayMode)
      RepaintGameViews();
  }

  private void OnGUI()
  {
    DrawOverlayControls();

    EditorGUI.BeginChangeCheck();

    ViewportLayout previousLayout = layout;
    layout = (ViewportLayout)EditorGUILayout.ObjectField(
        "Viewport Layout",
        layout,
        typeof(ViewportLayout),
        false);

    if (layout != previousLayout)
      rememberedEnabledStates = null;

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

    referenceTexture = (Texture2D)EditorGUILayout.ObjectField(
        "Reference Texture",
        referenceTexture,
        typeof(Texture2D),
        false);

    EditorGUI.BeginChangeCheck();
    showOverlay = EditorGUILayout.Toggle("Show Overlay", showOverlay);
    bool overlayToggled = EditorGUI.EndChangeCheck();

    overlayOpacity = EditorGUILayout.Slider(
        "Overlay Opacity",
        overlayOpacity,
        0f,
        1f);

    if (overlayToggled)
      RepaintGameViews();

    EditorGUILayout.Space();
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

  private void HandleEndCameraRendering(
      ScriptableRenderContext context,
      Camera camera)
  {
    if (!showOverlay || referenceTexture == null || !Application.isPlaying)
      return;

    if (camera == null || camera.cameraType != CameraType.Game)
      return;

    if (camera.targetTexture != null)
      return;

    if (!TryGetViewportScreenRect(out Rect viewportRect))
      return;

    float aspect =
        (float)referenceTexture.width / referenceTexture.height;
    Rect drawRect = FitAspect(viewportRect, aspect);

    DrawOverlayTexture(camera, drawRect, referenceTexture, overlayOpacity);
  }

  private bool TryGetViewportScreenRect(out Rect rect)
  {
    rect = default;

    if (cachedViewportImage == null)
      cachedViewportImage = FindViewportRawImage();

    if (cachedViewportImage == null)
      return false;

    RectTransform transform = cachedViewportImage.rectTransform;
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
      if (image != null && image.texture is RenderTexture)
        return image;
    }

    return null;
  }

  private static Rect FitAspect(Rect container, float aspect)
  {
    float containerAspect = container.width / container.height;

    if (containerAspect > aspect)
    {
      float width = container.height * aspect;
      return new Rect(
          container.x + (container.width - width) * 0.5f,
          container.y,
          width,
          container.height);
    }

    float height = container.width / aspect;
    return new Rect(
        container.x,
        container.y + (container.height - height) * 0.5f,
        container.width,
        height);
  }

  private static void DrawOverlayTexture(
      Camera camera,
      Rect screenRect,
      Texture texture,
      float opacity)
  {
    GL.PushMatrix();
    GL.LoadPixelMatrix(
        0f,
        camera.pixelWidth,
        0f,
        camera.pixelHeight);

    Graphics.DrawTexture(
        screenRect,
        texture,
        new Rect(0f, 0f, 1f, 1f),
        0,
        0,
        0,
        0,
        new Color(1f, 1f, 1f, opacity));

    GL.PopMatrix();
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
