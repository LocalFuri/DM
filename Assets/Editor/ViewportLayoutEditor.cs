using System.Reflection;
using DM.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class ViewportLayoutEditor : EditorWindow
{
  private static readonly int[] SnapValues = { 1, 2, 4, 8 };
  private static readonly BindingFlags InstanceFlags =
      BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

  private ViewportLayout layout;
  private Vector2 scroll;
  private bool[] rememberedEnabledStates;
  private int snap = 1;

  private Texture2D referenceTexture;
  private bool showOverlay;
  private float overlayOpacity = 0.5f;
  private RawImage cachedViewportImage;

  private EditorWindow hookedGameView;
  private IMGUIContainer hookedContainer;

  // --- TEMPORARY OVERLAY DIAGNOSTICS (do not ship) ---
  private const float TestOverlayDurationSeconds = 5f;
  private int diagCallbackCount;
  private double diagLastCallbackTime;
  private string diagLastFailureReason = "No callback yet.";
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
    EditorApplication.update += MaintainGameViewHook;
    EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
  }

  private void OnDisable()
  {
    EditorApplication.update -= MaintainGameViewHook;
    EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
    UnhookGameView();
    RepaintGameViews();
  }

  private void HandlePlayModeStateChanged(PlayModeStateChange state)
  {
    cachedViewportImage = null;
    diagCallbackCount = 0;
    diagLastCallbackTime = 0;
    diagLastFailureReason = "Play mode state changed.";
    testOverlayUntil = 0;

    if (state == PlayModeStateChange.ExitingPlayMode)
    {
      UnhookGameView();
      RepaintGameViews();
    }
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

    EditorGUI.BeginChangeCheck();

    referenceTexture = (Texture2D)EditorGUILayout.ObjectField(
        "Reference Texture",
        referenceTexture,
        typeof(Texture2D),
        false);

    showOverlay = EditorGUILayout.Toggle("Show Overlay", showOverlay);

    overlayOpacity = EditorGUILayout.Slider(
        "Overlay Opacity",
        overlayOpacity,
        0f,
        1f);

    if (EditorGUI.EndChangeCheck())
      RepaintGameViews();

    EditorGUILayout.Space();
  }

  // --- TEMPORARY OVERLAY DIAGNOSTICS (do not ship) ---
  private void DrawTemporaryOverlayDiagnostics()
  {
    EditorGUILayout.Space();
    EditorGUILayout.LabelField(
        "TEMPORARY Overlay Diagnostics",
        EditorStyles.boldLabel);
    EditorGUILayout.HelpBox(
        "Temporary diagnostics only. Remove after the overlay failure is confirmed.",
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

    bool testActive = EditorApplication.timeSinceStartup < testOverlayUntil;
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
        MaintainGameViewHook();
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

    cachedViewportImage = null;
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

    if (gameView == null)
    {
      if (diagLastFailureReason == "No callback yet."
          || string.IsNullOrEmpty(diagLastFailureReason))
      {
        diagLastFailureReason = "Game View not found.";
      }

      return;
    }

    if (rawImage == null)
      return;

    if (!TryGetRawImageScreenRect(rawImage, out Rect screenRect))
      return;

    if (!TryGetGameViewImageRects(
            gameView,
            out Rect groupRect,
            out Rect targetInView,
            out Vector2 renderSize))
    {
      return;
    }

    diagGameViewRect = targetInView;
    diagOverlayRect = MapGamePixelsToGameViewGui(
        screenRect,
        targetInView,
        Mathf.Max(1, Mathf.RoundToInt(renderSize.x)),
        Mathf.Max(1, Mathf.RoundToInt(renderSize.y)));
    _ = groupRect;
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

  private void MaintainGameViewHook()
  {
    // TEMPORARY: always keep the Game View hook in Play Mode so diagnostics
    // and Test Overlay can observe callback invocations.
    if (!Application.isPlaying)
    {
      if (hookedContainer != null)
        UnhookGameView();
      return;
    }

    EditorWindow gameView = FindGameView();
    diagGameViewFound = gameView != null;
    if (gameView == null)
    {
      UnhookGameView();
      diagLastFailureReason = "Game View not found.";
      Repaint();
      return;
    }

    if (hookedGameView != gameView || hookedContainer == null)
    {
      UnhookGameView();
      if (!HookGameView(gameView))
      {
        diagLastFailureReason =
            "Game View found, but IMGUIContainer hook failed.";
        Repaint();
        return;
      }
    }

    if (IsTestOverlayActive())
    {
      RepaintGameViews();
      Repaint();
    }
    else
    {
      // Keep diagnostics labels live while playing.
      Repaint();
    }
  }

  private bool HookGameView(EditorWindow gameView)
  {
    IMGUIContainer container =
        gameView.rootVisualElement?.Q<IMGUIContainer>();

    if (container == null)
      return false;

    container.onGUIHandler += DrawGameViewOverlay;
    hookedContainer = container;
    hookedGameView = gameView;
    return true;
  }

  private void UnhookGameView()
  {
    if (hookedContainer != null)
      hookedContainer.onGUIHandler -= DrawGameViewOverlay;

    hookedContainer = null;
    hookedGameView = null;
  }

  private void DrawGameViewOverlay()
  {
    if (Event.current.type != EventType.Repaint)
      return;

    diagCallbackCount++;
    diagLastCallbackTime = EditorApplication.timeSinceStartup;

    bool testActive = IsTestOverlayActive();
    bool wantReference =
        showOverlay && referenceTexture != null && Application.isPlaying;

    if (!Application.isPlaying)
    {
      diagLastFailureReason = "Not in Play Mode.";
      return;
    }

    if (!wantReference && !testActive)
    {
      diagLastFailureReason =
          "Callback ran, but Show Overlay is off and Test Overlay is inactive.";
      return;
    }

    EditorWindow gameView = hookedGameView;
    if (gameView == null)
    {
      diagLastFailureReason = "Callback ran, but hooked Game View is null.";
      return;
    }

    if (!TryGetViewportRawImage(out RawImage viewportImage))
    {
      diagLastFailureReason = "Dungeon RawImage not found.";
      return;
    }

    if (!viewportImage.isActiveAndEnabled)
    {
      diagLastFailureReason = "RawImage found but not active/enabled.";
      return;
    }

    if (!TryGetRawImageScreenRect(viewportImage, out Rect screenRect))
    {
      diagLastFailureReason = "RawImage screen rectangle is invalid.";
      return;
    }

    if (!TryGetGameViewImageRects(
            gameView,
            out Rect groupRect,
            out Rect targetInView,
            out Vector2 renderSize))
    {
      diagLastFailureReason =
          "Failed to read Game View zoom/target rectangles via reflection.";
      return;
    }

    Rect drawRect = MapGamePixelsToGameViewGui(
        screenRect,
        targetInView,
        Mathf.Max(1, Mathf.RoundToInt(renderSize.x)),
        Mathf.Max(1, Mathf.RoundToInt(renderSize.y)));

    diagGameViewRect = targetInView;
    diagOverlayRect = drawRect;

    if (drawRect.width < 1f || drawRect.height < 1f)
    {
      diagLastFailureReason =
          $"Overlay rectangle invalid: {FormatRect(drawRect)}";
      return;
    }

    Color previousColor = GUI.color;
    GUI.BeginGroup(groupRect);

    if (testActive)
    {
      // TEMPORARY: unmistakable solid red translucent probe.
      EditorGUI.DrawRect(drawRect, new Color(1f, 0f, 0f, 0.45f));
      diagLastFailureReason = "Test Overlay draw issued.";
    }

    if (wantReference)
    {
      GUI.color = new Color(1f, 1f, 1f, overlayOpacity);
      GUI.DrawTextureWithTexCoords(
          drawRect,
          referenceTexture,
          ToTexCoords(viewportImage.uvRect),
          true);
      GUI.color = previousColor;

      if (!testActive)
        diagLastFailureReason = "Reference overlay draw issued.";
    }
    else
    {
      GUI.color = previousColor;
    }

    GUI.EndGroup();
  }

  private static Rect ToTexCoords(Rect uvRect)
  {
    return new Rect(uvRect.x, uvRect.y, uvRect.width, uvRect.height);
  }

  private static Rect MapGamePixelsToGameViewGui(
      Rect gamePixelRect,
      Rect targetInView,
      int gameWidth,
      int gameHeight)
  {
    if (gameWidth <= 0 || gameHeight <= 0)
      return default;

    float xMin = gamePixelRect.xMin / gameWidth;
    float xMax = gamePixelRect.xMax / gameWidth;
    float yMin = gamePixelRect.yMin / gameHeight;
    float yMax = gamePixelRect.yMax / gameHeight;

    // Game pixels: origin bottom-left. GameView GUI: origin top-left within targetInView.
    return new Rect(
        targetInView.x + xMin * targetInView.width,
        targetInView.y + (1f - yMax) * targetInView.height,
        (xMax - xMin) * targetInView.width,
        (yMax - yMin) * targetInView.height);
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
      if (image != null && image.texture is RenderTexture)
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
