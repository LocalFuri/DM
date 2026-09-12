using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DM.Dungeon;
using DM.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// CHATGPT_BUILD_F1_MINIMAP_ALGORITHM_STAGE1_20260830_AA
public class ViewportLayoutEditor : EditorWindow
{
  private static readonly int[] SnapValues = { 1, 2, 4, 8 };

  private const string PrefsLayoutGuidKey = "ViewportLayoutEditor.LayoutGuid";
  private const string PrefsGraphicsGuidKey =
      "ViewportLayoutEditor.GraphicsGuid";
  private const string PrefsPreviewXKey = "ViewportLayoutEditor.PreviewX";
  private const string PrefsPreviewYKey = "ViewportLayoutEditor.PreviewY";
  private const string PrefsPreviewFacingKey =
      "ViewportLayoutEditor.PreviewFacing";
  private const string PrefsSelectedPieceIndexKey =
      "ViewportLayoutEditor.SelectedPieceIndex";
  private const string SearchPiecesControlName =
      "ViewportLayoutEditor.SearchPieces";

  private const string HallOfChampionsMapPath =
      "Assets/Data/Maps/HallOfChampions.json";

  private const string DefaultViewportLayoutPath =
      "Assets/Dungeon Master/ViewportLayout.asset";

  private const int PreviewWidth = 320;
  private const int PreviewHeight = 200;

  // NORMAL-WALL REBOOT:
  // Until the new geometry renderer is built, old normal-wall rendering is
  // completely disconnected. Only the relative geometry snapshot is active.
  private const bool GeometryFoundationOnly = false;
  private static readonly string[] PieceSearchFamilyOptions =
  {
    "",
    "Front",
    "Left",
    "Right",
    "LeftD3",
    "RightD3",
    "Black Door",
    "Active",
  };

  /// <summary>
  /// Locked (1,2) South kit Enabled baseline for ViewportLayout.asset disk writes.
  /// Source: LockedBackup/ViewportLayout_1_2_South.asset + walls.txt.
  /// </summary>
  private static readonly (string Name, bool Enabled)[] KitBaselineEnabled =
  {
    ("Ceiling", true),
    ("Ceiling Strip 84", false),
    ("Ceiling Strip 85", false),
    ("Floor", true),
    ("Front Wall F3", false),
    ("Wall F3Left", true),
    ("Wall F3Right", true),
    ("Wall D3L2", false),
    ("Wall D3R2", false),
    ("Front Wall F2", false),
    ("Wall F2Left", true),
    ("Wall F2Right", false),
    ("Front Wall F1", false),
    ("Wall F1Left", true),
    ("Wall F1Right", true),
    ("Wall F0Left", true),
    ("Wall F0Right", true),
    ("Movement Arrows", true),
    ("Champion Status Slot 1", true),
    ("Champion Status Slot 2", true),
    ("Champion Status Slot 3", true),
    ("Champion Status Slot 4", true),
    // Door Enabled is pose-authored; kit default is off.
    ("Black Door Frame Left F1", false),
    ("Black Door Frame Left F2", false),
    ("Black Door Frame Right F1", false),
    ("Black Door Frame Right F2", false),
    ("BlackDoorF1", false),
  };

  [System.NonSerialized]
  private ViewportLayout layout;
  [System.NonSerialized]
  private DungeonGraphics graphics;
  private Vector2 editorScroll;
  private bool scrollToBottomOnNextRepaint;
  private int lastPlayModeScrollX = int.MinValue;
  private int lastPlayModeScrollY = int.MinValue;
  private DungeonFacing lastPlayModeScrollFacing = (DungeonFacing)(-1);
  private int pieceSearchFamilyIndex;
  private bool showWallsActivFilter;
  private bool showOnlyWallsNeededForCurrentPose;
  private bool previewDisableAllWalls;
  private bool showGeometryDiagnostics;
  private string pieceSearchText = string.Empty;
  private bool openSearchPiecesPopup;
  private bool focusSearchPieces;
  private GUIStyle searchPiecesLabelStyle;
  private GUIStyle pieceFamilyHeaderStyle;
  private int snap = 1;

  private bool hookedViewEditGlobalNavigation;

  private static int s_viewEditGlobalNavOwners;
  private static bool s_viewEditGlobalNavCallbackAdded;
  private static bool s_viewEditGlobalNavDispatch;
  private static readonly EditorApplication.CallbackFunction
      ViewEditGlobalNavHandler = HandleViewEditGlobalNavigationEvent;
  private static System.Delegate s_viewEditBeforeEventProcessedHandler;

  // BlackDoorF1 layout Enabled is initialized once for the verified 1,3 North pose.
  // After initialization, the visible BlackDoorF1 checkbox remains authoritative.
  private bool blackDoorF1EnabledInitialized;
  private bool blackDoorFrameLeftF1EnabledInitialized;
  private bool blackDoorFrameRightF1EnabledInitialized;

  // ViewEdit-only BlackDoorF2 card controls. Do not write layout/pose and
  // do not drive rendering; F2 still uses the existing pose exception.
  private bool blackDoorF2CardInitialized;
  private bool blackDoorF2CardEnabled;
  private bool blackDoorF2CardMirror;
  private DungeonGraphicType blackDoorF2CardGraphic =
      DungeonGraphicType.BlackDoor;

  private bool blackDoorF3CardInitialized;
  private bool blackDoorF3CardEnabled;
  private bool blackDoorF3CardMirror;
  private DungeonGraphicType blackDoorF3CardGraphic =
      DungeonGraphicType.BlackDoor;
  private int blackDoorF3CardX;
  private int blackDoorF3CardY;

  private bool blackDoorFrameLeftF3EnabledInitialized;
  private bool blackDoorFrameRightF3EnabledInitialized;
  private bool blackDoorFrameLeftF3CardEnabled;
  private bool blackDoorFrameLeftF3CardMirror;
  private int blackDoorFrameLeftF3CardX;
  private int blackDoorFrameLeftF3CardY;

  private bool blackDoorFrameRightF3CardEnabled;
  private int blackDoorFrameRightF3CardX;
  private int blackDoorFrameRightF3CardY;

  private Texture2D blackDoorFrameF3SourceTexture;
  private Texture2D blackDoorFrameLeftF1SourceTexture;
  private Texture2D blackDoorF1SourceTexture;
  private Texture2D blackDoorF3SourceTexture;
  private Texture2D blackDoorF2SourceTexture;
  private Texture2D frontWallF2_224ReferenceTexture;
  private Texture2D right2SSourceTexture;

  // Single source of truth for selection.
  private int selectedPieceIndex;
  private bool selectionChangedThisFrame;

  private RawImage cachedViewportImage;

  private Texture2D editModePreviewTexture;
  private Texture savedViewportTexture;
  private bool viewportTextureStolen;

  // Edit Mode map-pose preview (Hall of Champions).
  // previewFacing is the single source of truth for viewport compose, minimap
  // arrow, and Console — keep all three on this field only.
  // RestoreSessionPrefs restores last EditorPrefs pose, or map start.
  private int previewX;
  private int previewY;
  private DungeonFacing previewFacing = DungeonFacing.South;
  private DungeonMap previewMiniMap;
  private string previewMiniMapLoadError;
  private Vector2 previewMiniMapScroll;
  private string deterministicWallDiagnosticText;
  private Rect geometryDiagnosticRect;

  // New normal-wall pipeline foundation:
  // pose-store data may still exist, but normal-wall placement is restored from
  // one clean baseline before the relative-geometry resolver runs.
  private readonly Dictionary<string, NormalWallBaseline> normalWallBaselineByName =
      new Dictionary<string, NormalWallBaseline>();

  private struct NormalWallBaseline
  {
    public DungeonGraphicType Graphic;
    public int X;
    public int Y;
    public bool Mirror;
    public int FrontF1Width;
    public int FrontF2Width;
  }

  // Per-pose, transient normal-wall result. Never written to the layout asset
  // or pose store. This is the render-time assembly produced from map geometry.
  private readonly Dictionary<ViewportPiece, ResolvedNormalWallState>
      resolvedNormalWallByPiece =
          new Dictionary<ViewportPiece, ResolvedNormalWallState>();

  // ViewEdit-only normal-wall Mirror overrides. These exist only while the
  // current preview pose is stationary. They are never written to the layout
  // asset or pose store and are cleared on every pose/facing change.
  private readonly Dictionary<ViewportPiece, bool> previewMirrorOverrideByPiece =
      new Dictionary<ViewportPiece, bool>();

  // ViewEdit-only FrontF1 width overrides. Like the Mirror test, these exist
  // only while the current X/Y/Facing is unchanged and are never persisted.
  private readonly Dictionary<ViewportPiece, int> previewFrontF1WidthOverrideByPiece =
      new Dictionary<ViewportPiece, int>();

  // ViewEdit FrontF1 crop control for the current pose (live cache).
  // Canonical storage is previewFrontF1CropByPose, keyed by X/Y/Facing.
  private bool frontF1CropPreview;
  private int frontF1CropStartXPreview;
  private const bool FrontF1ReferenceCrop = false;

  private struct FrontF1CropPreviewState
  {
    public bool Enabled;
    public int CropX;
  }

  private readonly Dictionary<string, FrontF1CropPreviewState>
      previewFrontF1CropByPose =
          new Dictionary<string, FrontF1CropPreviewState>();

  private struct FrontF1GeometryOverride
  {
    public int X;
    public int Y;
    public int Width;
  }

  // Verified FrontF1 values keyed by relative minimap geometry, not absolute map pose.
  // These survive movement/turning within the current editor session.
  private static readonly Dictionary<string, FrontF1GeometryOverride>
      frontF1GeometryOverrides =
          new Dictionary<string, FrontF1GeometryOverride>();

  // Verified Enabled state for any normal wall, keyed by relative minimap
  // geometry + piece identity. This is independent of absolute map X/Y/Facing.
  private static readonly Dictionary<string, bool> normalWallEnabledGeometryOverrides =
      new Dictionary<string, bool>();

  // Verified X/Y and Mirror for any normal wall, keyed by relative minimap
  // geometry + piece identity. These are independent of absolute map position.
  private static readonly Dictionary<string, Vector2Int> normalWallPositionGeometryOverrides =
      new Dictionary<string, Vector2Int>();
  private static readonly Dictionary<string, bool> normalWallMirrorGeometryOverrides =
      new Dictionary<string, bool>();

  // ViewEdit-only normal-wall X/Y overrides. Like the temporary Mirror test,
  // these affect only the stationary preview pose and are discarded whenever
  // X/Y/Facing changes. They are never written to the layout asset/pose store.
  private readonly Dictionary<ViewportPiece, Vector2Int> previewPositionOverrideByPiece =
      new Dictionary<ViewportPiece, Vector2Int>();

  // ViewEdit-only Enabled override for exception pieces. This lets the user hide
  // an automatically active exception without fighting the exception rule.
  private readonly Dictionary<ViewportPiece, bool> previewEnabledOverrideByPiece =
      new Dictionary<ViewportPiece, bool>();
  private readonly Dictionary<ViewportPiece, DungeonGraphicType>
      previewGraphicOverrideByPiece =
          new Dictionary<ViewportPiece, DungeonGraphicType>();
  private bool previewMirrorChangedThisFrame;
  private bool previewFrontF1WidthChangedThisFrame;
  private bool previewPositionChangedThisFrame;
  private bool previewEnabledChangedThisFrame;
  private bool previewGraphicChangedThisFrame;

  private struct ResolvedNormalWallState
  {
    public bool Enabled;
    public DungeonGraphicType Graphic;
    public int X;
    public int Y;
    public bool Mirror;
    public int FrontF1Width;
    public int FrontF2Width;
  }

  private static string lastLoggedF0DrawDiagnosticKey;

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

  [MenuItem("Tools/ViewEdit &v")]
  public static void Open()
  {
    GetWindow<ViewportLayoutEditor>("ViewEdit");
  }

  [InitializeOnLoadMethod]
  private static void RegisterInspectorRightClickOpen()
  {
    EditorApplication.CallbackFunction handler = HandleInspectorRightClickOpen;
    RemoveViewEditGlobalEventHandler(handler);
    AddViewEditGlobalEventHandler(handler);
  }

  private static void HandleInspectorRightClickOpen()
  {
    if (HasOpenInstances<ViewportLayoutEditor>())
      return;

    Event current = Event.current;
    if (current == null
        || current.type != EventType.MouseDown
        || current.button != 1)
    {
      return;
    }

    EditorWindow hovered = mouseOverWindow;
    if (hovered == null
        || hovered.GetType().FullName != "UnityEditor.InspectorWindow")
    {
      return;
    }

    current.Use();
    Open();
  }

  private void OnEnable()
  {
    titleContent = new GUIContent("ViewEdit");
    wantsMouseMove = true;
    RestorePersistedAssets();
    ReloadLayoutFromDisk();
    RestoreSessionPrefs();
    showOnlyWallsNeededForCurrentPose = true;
    showWallsActivFilter = true;
    StripObsoleteFrontWallF1ABPieces();
    EnsureLeft2SAndRight2SPieces();
    CaptureNormalWallBaselinesFromLayout();
    ApplyCurrentPoseVisibilityToLayout();
    EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    // Force a fresh compose from the current pose's Enabled flags.
    DestroyEditModePreviewTextureOnly();
    RefreshEditModePreview();
    if (!hookedViewEditGlobalNavigation)
    {
      RegisterViewEditGlobalNavigation();
      hookedViewEditGlobalNavigation = true;
    }
  }

  private void OnDisable()
  {
    if (hookedViewEditGlobalNavigation)
    {
      UnregisterViewEditGlobalNavigation();
      hookedViewEditGlobalNavigation = false;
    }
    StripObsoleteFrontWallF1ABPieces();
    SaveAssetGuid(PrefsLayoutGuidKey, layout);
    SaveAssetGuid(PrefsGraphicsGuidKey, graphics);
    SaveSessionPrefs();

    EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
    RepaintGameViews();
  }

  private void HandlePlayModeStateChanged(PlayModeStateChange state)
  {
    cachedViewportImage = null;

    if (state == PlayModeStateChange.ExitingEditMode
        || state == PlayModeStateChange.EnteredPlayMode)
    {
      // Hand the RawImage back to the scene RenderTexture before Play runs.
      RestoreViewportTextureAndDestroyPreview();
    }

    if (state == PlayModeStateChange.EnteredPlayMode)
    {
      // In Play Mode, ViewEdit becomes a live "what is actually active" wall view.
      // Clear list filters and show only wall pieces whose runtime Enabled flag is on.
      showOnlyWallsNeededForCurrentPose = true;
      pieceSearchFamilyIndex = 0;
      pieceSearchText = string.Empty;

      // Request a one-shot scroll-to-bottom after the Play Mode UI has
      // completed a real repaint. Setting the scroll position only here is too
      // early because Unity has not calculated the final content height yet.
      scrollToBottomOnNextRepaint = true;
      editorScroll = new Vector2(0f, float.MaxValue);

      Repaint();
    }

    if (state == PlayModeStateChange.EnteredEditMode)
    {
      // Re-apply preview pose after Play mutated live layout Enabled flags.
      StripObsoleteFrontWallF1ABPieces();
      ApplyCurrentPoseVisibilityToLayout();
      RefreshEditModePreview();
    }

    if (state == PlayModeStateChange.ExitingPlayMode)
      RepaintGameViews();
  }

  private void OnInspectorUpdate()
  {
    if (Application.isPlaying)
      Repaint();
  }

  private void StoreAllNormalWallOverridesForCurrentGeometry()
  {
    if (layout == null || layout.Pieces == null)
      return;

    EnsurePreviewMiniMapLoaded();
    if (previewMiniMap == null)
      return;

    RelativeViewportGeometry geometry =
        RelativeViewportGeometry.Calculate(
            previewMiniMap,
            previewX,
            previewY,
            previewFacing);

    string frontF1GeometryKey = BuildFrontF1GeometryKey(geometry);

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null || !IsNormalWallPiece(piece))
        continue;

      ResolvedNormalWallState state;
      bool hasResolved =
          resolvedNormalWallByPiece.TryGetValue(piece, out state);

      bool enabled = hasResolved ? state.Enabled : piece.Enabled;
      if (previewEnabledOverrideByPiece.TryGetValue(
              piece, out bool previewEnabled))
      {
        enabled = previewEnabled;
      }
      else if (IsFrontWallF1Card(piece)
          && TryGetFrontF1PreviewEnabledOverride(out previewEnabled))
      {
        enabled = previewEnabled;
      }

      int x = hasResolved ? state.X : piece.EffectiveX;
      int y = hasResolved ? state.Y : piece.EffectiveY;
      if (IsFrontWallF1Card(piece)
          && TryGetFrontF1PreviewPositionOverride(
              piece, out Vector2Int previewPosition))
      {
        x = previewPosition.x;
        y = previewPosition.y;
      }
      else if (previewPositionOverrideByPiece.TryGetValue(
              piece, out previewPosition))
      {
        x = previewPosition.x;
        y = previewPosition.y;
      }

      bool mirror = hasResolved ? state.Mirror : piece.MirrorHorizontally;
      if (IsFrontWallF1Card(piece)
          && TryGetFrontF1PreviewMirrorOverride(
              piece, out bool previewMirror))
      {
        mirror = previewMirror;
      }
      else if (previewMirrorOverrideByPiece.TryGetValue(
              piece, out previewMirror))
      {
        mirror = previewMirror;
      }

      DungeonGraphicType graphic =
          hasResolved ? state.Graphic : piece.Graphic;

      int frontWallF1Width = 0;
      if (IsFrontWallF1Card(piece))
      {
        frontWallF1Width = hasResolved
            ? state.FrontF1Width
            : piece.FrontWallF1Width;
        if (TryGetFrontF1PreviewWidthOverride(
                piece, out int previewWidth))
        {
          frontWallF1Width = previewWidth;
        }

        frontWallF1Width =
            StraightF1WallLogic.NormalizeFrontWallF1Width(frontWallF1Width);
      }

      int frontWallF2Width = 0;
      if (IsFrontWallF2Card(piece))
      {
        frontWallF2Width = hasResolved
            ? state.FrontF2Width
            : piece.FrontWallF2Width;
      }

      string geometryPieceKey =
          BuildNormalWallEnabledGeometryKey(geometry, piece);

      normalWallEnabledGeometryOverrides[geometryPieceKey] = enabled;
      normalWallPositionGeometryOverrides[geometryPieceKey] =
          new Vector2Int(x, y);
      normalWallMirrorGeometryOverrides[geometryPieceKey] = mirror;

      if (IsFrontWallF1Card(piece))
      {
        frontF1GeometryOverrides[frontF1GeometryKey] =
            new FrontF1GeometryOverride
            {
              X = x,
              Y = y,
              Width = frontWallF1Width
            };
      }

    }

    // The current geometry is now committed. Discard only the temporary
    // stationary-pose tests; the geometry overrides above become authoritative.
    previewEnabledOverrideByPiece.Clear();
    previewPositionOverrideByPiece.Clear();
    previewMirrorOverrideByPiece.Clear();
    previewFrontF1WidthOverrideByPiece.Clear();
    previewGraphicOverrideByPiece.Clear();

    // Keep the visually verified Black Door F1 layout authoritative.
    // These are the accepted working reference values for 1,3 North.
    if (IsVerifiedBlackDoorF1Pose())
    {
      ViewportPiece leftFrame =
          FindLayoutPieceByName("Black Door Frame Left F1");
      if (leftFrame != null)
      {
        leftFrame.X = 44;
        leftFrame.Y = DisplayYToUnityY(46, 94);
      }

      ViewportPiece rightFrame =
          FindLayoutPieceByName("Black Door Frame Right F1");
      if (rightFrame != null)
      {
        rightFrame.X = 154;
        rightFrame.Y = DisplayYToUnityY(46, 94);
      }

      ViewportPiece frontDoor =
          FindLayoutPieceByName("BlackDoorF1");
      if (frontDoor != null)
      {
        frontDoor.X = 63;
        frontDoor.Y = DisplayYToUnityY(47, 88);
      }

      ViewportPiece rightF1 =
          FindLayoutPieceByName("RightF1");
      if (rightF1 == null)
        rightF1 = FindLayoutPieceByName("Wall F1Right");
      if (rightF1 != null)
      {
        rightF1.X = 165;
        rightF1.Y = DisplayYToUnityY(
            42,
            GetPieceHeightForEditorY(rightF1));
      }
    }

    ApplyCurrentPoseVisibilityToLayout();
    ResetEditModeViewportLogCache();
    DestroyEditModePreviewTextureOnly();
    RefreshEditModePreview();
    RepaintGameViews();
    Repaint();
  }

  private void OnGUI()
  {
    selectionChangedThisFrame = false;

    if (Application.isPlaying
        && (previewX != lastPlayModeScrollX
            || previewY != lastPlayModeScrollY
            || previewFacing != lastPlayModeScrollFacing))
    {
      lastPlayModeScrollX = previewX;
      lastPlayModeScrollY = previewY;
      lastPlayModeScrollFacing = previewFacing;
      scrollToBottomOnNextRepaint = true;
    }

    // Claim EditorWindow focus on click so existing keyboard nav can run.
    // Do not clear GUI.FocusControl here — a TextField/IntField on this
    // same MouseDown still needs to receive it.
    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
      Focus();

    if (!HandleGeometryDiagnosticsRightClickClose())
      HandleViewEditRightClickHome();

    // Arrow Up/Down must be handled before BeginScrollView — otherwise the
    // scroll view consumes them for scrolling and HandlePreviewMoveKeyboard
    // never sees a usable KeyDown (Left/Right strafe is unaffected).
    // Facing keys run here too so EnumPopup cannot override the same KeyDown.
    if (layout != null && !Application.isPlaying)
    {
      HandlePreviewMoveKeyboard();
      HandlePreviewFacingKeyboard();
      HandlePreviewStrafeKeyboard();
    }

    editorScroll = EditorGUILayout.BeginScrollView(editorScroll);

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

    bool layoutEditable = !Application.isPlaying;
    if (!layoutEditable)
    {
      EditorGUILayout.HelpBox(
          "ViewportLayout.asset is read-only in Play Mode. "
              + "Stop Play to edit and save layout coordinates/visibility.",
          MessageType.Warning);
    }

    ClampSelectedPieceIndex();

    DrawMapPosePreviewControls();

    using (new EditorGUI.DisabledScope(!layoutEditable))
    {
      EditorGUI.BeginChangeCheck();

      if (layoutEditable)
        Undo.RecordObject(layout, "Viewport Layout Change");

      DrawSnapToolbar();

      EditorGUILayout.BeginHorizontal();

      if (GUILayout.Button(
              showOnlyWallsNeededForCurrentPose
                  ? "Show All Walls"
                  : "Show all Walls we Need",
              GUILayout.Width(130f)))
      {
        showOnlyWallsNeededForCurrentPose =
            !showOnlyWallsNeededForCurrentPose;

        if (!showOnlyWallsNeededForCurrentPose)
          showWallsActivFilter = false;

        pieceSearchFamilyIndex = 0;
        pieceSearchText = string.Empty;
        editorScroll = Vector2.zero;
        GUI.FocusControl(null);
        Repaint();
      }

      if (GUILayout.Button(
              "Override Current Walls",
              GUILayout.Width(170f)))
      {
        StoreAllNormalWallOverridesForCurrentGeometry();
        GUI.FocusControl(null);
      }

      bool diagnosticsPressed = GUILayout.Toggle(
          showGeometryDiagnostics,
          "Diagnostics",
          EditorStyles.miniButton,
          GUILayout.Width(90f));
      if (diagnosticsPressed != showGeometryDiagnostics)
      {
        showGeometryDiagnostics = diagnosticsPressed;
        Repaint();
      }

      EditorGUILayout.EndHorizontal();

      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.PrefixLabel(
          "Search Pieces",
          EditorStyles.popup,
          GetSearchPiecesLabelStyle());

      GUIStyle searchPoseStyle = new GUIStyle(EditorStyles.boldLabel);
      Color brightBlue = new Color(0.2f, 0.7f, 1.0f);
      searchPoseStyle.normal.textColor = brightBlue;
      searchPoseStyle.hover.textColor = brightBlue;
      searchPoseStyle.focused.textColor = brightBlue;
      searchPoseStyle.active.textColor = brightBlue;
      GUILayout.Label(
          previewX + "/" + previewY + " " + previewFacing,
          searchPoseStyle,
          GUILayout.Width(78f));

      bool guiChangedBeforeSearch = GUI.changed;
      Rect searchPiecesPopupRect = EditorGUILayout.GetControlRect();
      Event searchEvent = Event.current;
      if (searchEvent.type == EventType.MouseDown
          && searchEvent.button == 0
          && searchPiecesPopupRect.Contains(searchEvent.mousePosition))
      {
        openSearchPiecesPopup = true;
        focusSearchPieces = true;
      }
      GUI.SetNextControlName(SearchPiecesControlName);
      pieceSearchText = EditorGUI.TextField(
          searchPiecesPopupRect,
          pieceSearchText ?? string.Empty);
      if (focusSearchPieces && Event.current.type == EventType.Repaint)
      {
        GUI.FocusControl(SearchPiecesControlName);
        EditorGUI.FocusTextInControl(SearchPiecesControlName);
        focusSearchPieces = false;
      }
      // Search is a list filter only — never treat it as a layout persist.
      GUI.changed = guiChangedBeforeSearch;
      if (openSearchPiecesPopup && Event.current.type != EventType.Layout)
      {
        openSearchPiecesPopup = false;
        bool previousEnabled = GUI.enabled;
        GUI.enabled = true;
        ShowSearchPiecesFamilyMenu(searchPiecesPopupRect);
        GUI.enabled = previousEnabled;
      }

      EditorGUILayout.EndHorizontal();
      ClampSearchFamilyToEnabledPieces();
      HandlePieceSearchKeyboard();

      if (layoutEditable)
      {
        StripObsoleteFrontWallF1ABPieces();
        EnsureLeft2SAndRight2SPieces();
      }

      bool changed = false;

      List<int> displayIndices = new List<int>();
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (IsHiddenFromEditorPieceList(piece))
          continue;

        if (!PieceMatchesSearchFilter(piece))
          continue;

        displayIndices.Add(i);
      }

      displayIndices.Sort(CompareViewEditPieceDisplayOrder);

      for (int n = 0; n < displayIndices.Count; n++)
      {
        int i = displayIndices[n];
        ViewportPiece piece = layout.Pieces[i];
        bool isSelected = i == selectedPieceIndex;
        DrawPieceCard(i, piece, isSelected, ref changed);
      }

      bool editorChanged = EditorGUI.EndChangeCheck();
      if ((editorChanged || changed)
          && !previewMirrorChangedThisFrame
          && !previewFrontF1WidthChangedThisFrame
          && !previewPositionChangedThisFrame
          && !previewEnabledChangedThisFrame
          && !previewGraphicChangedThisFrame)
        PersistChanges();
      previewMirrorChangedThisFrame = false;
      previewFrontF1WidthChangedThisFrame = false;
      previewPositionChangedThisFrame = false;
      previewEnabledChangedThisFrame = false;
      previewGraphicChangedThisFrame = false;
    }

    EditorGUILayout.EndScrollView();

    if (scrollToBottomOnNextRepaint
        && Event.current.type == EventType.Repaint)
    {
      editorScroll = new Vector2(0f, float.MaxValue);
      scrollToBottomOnNextRepaint = false;
      Repaint();
    }

    // Click on empty / non-control area: release leftover text-field focus
    // so keyboard nav works. Controls that consume MouseDown (TextField,
    // IntField, buttons) are left alone here.
    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
    {
      GUI.FocusControl(null);
      Focus();
    }

    if (selectionChangedThisFrame)
    {
      RefreshEditModePreview();
      Repaint();
    }
  }

  /// <summary>
  /// Muted investigation pieces stay in the layout asset but are not listed.
  /// Disabled pieces for the current pose are also omitted from ViewEdit.
  /// </summary>
  private bool IsHiddenFromEditorPieceList(ViewportPiece piece)
  {
    if (piece == null || piece.Name == null)
      return false;

    // Permanent ViewEdit exclusions.
    if (piece.Name == "Front Wall F1"
        || piece.Name == "Ceiling"
        || piece.Name == "Floor"
        || piece.Graphic == DungeonGraphicType.Ceiling
        || piece.Graphic == DungeonGraphicType.Floor
        || piece.Name == "Movement Arrows"
        || piece.Graphic == DungeonGraphicType.MovementArrows
        || piece.Name == "Champion Status Slot 1"
        || piece.Name == "Champion Status Slot 2"
        || piece.Name == "Champion Status Slot 3"
        || piece.Name == "Champion Status Slot 4"
        || piece.Name == "Ceiling Strip 84"
        || piece.Name == "Ceiling Strip 85")
    {
      return true;
    }

    // Black Door cards are pose-area list items only. Show All Walls still
    // hides them outside the existing Black Door views. Stored values and
    // render logic are not changed.
    if (IsBlackDoorEditorPiece(piece)
        && !IsWallNeededForCurrentPose(piece))
    {
      return true;
    }

    if (showOnlyWallsNeededForCurrentPose
        && (IsWallEditorPiece(piece) || IsBlackDoorEditorPiece(piece)))
    {
      // Edit Mode: show walls required by the preview geometry.
      // Play Mode: show the walls the runtime renderer has actually enabled.
      bool wallIsActive =
          Application.isPlaying
          ? piece.Enabled
          : IsWallNeededForCurrentPose(piece);

      if (!wallIsActive)
        return true;
    }

    return false;
  }

  private void EnableAllWallsNeededForCurrentPose()
  {
    if (layout == null || layout.Pieces == null)
      return;

    Undo.RecordObject(layout, "Enable Walls Needed For Current Pose");

    bool changed = false;
    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null
          || (!IsWallEditorPiece(piece) && !IsBlackDoorEditorPiece(piece)))
        continue;

      bool needed = IsWallNeededForCurrentPose(piece);
      if (needed && !piece.Enabled)
      {
        piece.Enabled = true;
        changed = true;
      }
    }

    if (!changed)
      return;

    EditorUtility.SetDirty(layout);

    RefreshEditModePreview();
  }

  private static bool IsWallEditorPiece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (IsWallF0LeftPiece(piece)
        || IsWallF0RightPiece(piece)
        || IsWallF1LeftPiece(piece)
        || IsWallF1RightPiece(piece)
        || IsWallF2LeftPiece(piece)
        || IsWallF2RightPiece(piece)
        || IsWallF3LeftPiece(piece)
        || IsWallF3RightPiece(piece)
        || IsFrontWallF1Card(piece)
        || IsFrontWallF2Card(piece)
        || IsFrontWallF3Card(piece))
    {
      return true;
    }

    string name = piece.Name ?? string.Empty;
    return name == "LeftD3"
        || name == "RightD3"
        || name == "LeftS2"
        || name == "Right2S"
        || name == "Wall D3L2"
        || name == "Wall D3R2"
        || name == "Black Door Frame Left F1"
        || name == "Black Door Frame Right F1"
        || name == "Black Door Frame Left F2"
        || name == "Black Door Frame Right F2"
        || name == "BlackDoorF1";
  }

  private static bool IsBlackDoorEditorPiece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    string name = piece.Name ?? string.Empty;
    return name == "BlackDoorF1"
        || name == "BlackDoorF2"
        || name == "BlackDoorF3"
        || name == "Black Door Frame Left F1"
        || name == "Black Door Frame Right F1"
        || name == "Black Door Frame Left F2"
        || name == "Black Door Frame Right F2"
        || name == "Black Door Frame Left F3"
        || name == "Black Door Frame Right F3";
  }

  private bool IsWallNeededForCurrentPose(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    EnsurePreviewMiniMapLoaded();

    // If the minimap cannot be read, do not hide wall cards.
    if (previewMiniMap == null)
      return true;

    string name = piece.Name ?? string.Empty;

    // At the verified Black Door F1 pose, the door occupies the front opening.
    // The normal FrontF2 wall is therefore not a needed piece for this view.
    if (previewX == 1
        && previewY == 3
        && previewFacing == DungeonFacing.North
        && (IsFrontWallF2Card(piece)
            || FrontWallF2Logic.IsFrontWallF2Graphic(piece.Graphic)))
    {
      return false;
    }

    // Black Door editor cards must be decided BEFORE the generic normal-wall
    // resolver. Some Black Door pieces are classified as normal wall pieces,
    // so the old ordering returned their resolved Enabled=false before these
    // dedicated door-pose rules were ever reached.
    if (IsBlackDoorEditorPiece(piece))
    {
      if (previewX == 1 && previewFacing == DungeonFacing.North)
      {
        if (previewY == 3)
        {
          return name == "Black Door Frame Left F1"
              || name == "Black Door Frame Right F1"
              || name == "BlackDoorF1";
        }

        if (previewY == 4)
        {
          return name == "Black Door Frame Left F2"
              || name == "Black Door Frame Right F2"
              || name == "BlackDoorF2";
        }

        if (previewY == 5)
        {
          return name == "Black Door Frame Left F3"
              || name == "Black Door Frame Right F3"
              || name == "BlackDoorF3";
        }
      }

      return false;
    }

    if (IsNormalWallPiece(piece)
        && TryGetResolvedNormalWallState(
            piece, out ResolvedNormalWallState resolvedNeededState))
    {
      return resolvedNeededState.Enabled;
    }

    DungeonMap.GetForwardOffset(
        previewFacing,
        out int forwardX,
        out int forwardY);
    DungeonMap.GetRightOffset(
        previewFacing,
        out int rightX,
        out int rightY);

    bool TileIsWallOrOutside(int x, int y)
    {
      return !previewMiniMap.IsInside(x, y)
          || previewMiniMap.GetTile(x, y).Type == DungeonTileType.Wall;
    }

    int f1X = previewX + forwardX;
    int f1Y = previewY + forwardY;
    int f2X = previewX + forwardX * 2;
    int f2Y = previewY + forwardY * 2;
    int f3X = previewX + forwardX * 3;
    int f3Y = previewY + forwardY * 3;

    bool f1CenterWall = TileIsWallOrOutside(f1X, f1Y);
    bool f2CenterWall = TileIsWallOrOutside(f2X, f2Y);
    bool f3CenterWall = TileIsWallOrOutside(f3X, f3Y);

    if (IsWallF0LeftPiece(piece))
      return TileIsWallOrOutside(previewX - rightX, previewY - rightY);

    if (IsWallF0RightPiece(piece))
      return TileIsWallOrOutside(previewX + rightX, previewY + rightY);

    if (IsWallF1LeftPiece(piece))
      return !f1CenterWall
          && TileIsWallOrOutside(f1X - rightX, f1Y - rightY);

    if (IsWallF1RightPiece(piece))
      return !f1CenterWall
          && TileIsWallOrOutside(f1X + rightX, f1Y + rightY);

    if (IsWallF2LeftPiece(piece))
      return !f1CenterWall
          && !f2CenterWall
          && TileIsWallOrOutside(f2X - rightX, f2Y - rightY);

    if (IsWallF2RightPiece(piece))
      return !f1CenterWall
          && !f2CenterWall
          && TileIsWallOrOutside(f2X + rightX, f2Y + rightY);

    if (IsWallF3LeftPiece(piece))
      return !f1CenterWall
          && !f2CenterWall
          && !f3CenterWall
          && TileIsWallOrOutside(f3X - rightX, f3Y - rightY);

    if (IsWallF3RightPiece(piece))
      return !f1CenterWall
          && !f2CenterWall
          && !f3CenterWall
          && TileIsWallOrOutside(f3X + rightX, f3Y + rightY);

    if (IsFrontWallF1Card(piece))
      return f1CenterWall;

    if (IsFrontWallF2Card(piece))
      return false;

    if (IsFrontWallF3Card(piece))
    {
      if (previewX == 1
          && previewY == 4
          && previewFacing == DungeonFacing.North)
      {
        return false;
      }

      return !f1CenterWall && !f2CenterWall && f3CenterWall;
    }

    if (name == "LeftD3" || name == "Wall D3L2")
    {
      return !f1CenterWall
          && !f2CenterWall
          && !f3CenterWall
          && TileIsWallOrOutside(f3X - rightX, f3Y - rightY);
    }

    if (name == "RightD3" || name == "Wall D3R2")
    {
      // Normal RightD3 visibility comes from the minimap resolver. Keep the
      // existing Black Door oblique exception as a separate exception layer.
      if (TryGetResolvedNormalWallState(
              piece, out ResolvedNormalWallState rightD3State))
      {
        return rightD3State.Enabled;
      }

      bool blackDoorOblique =
          previewX == 0
          && previewY == 5
          && previewFacing == DungeonFacing.North;

      return blackDoorOblique;
    }

    // Other Black Door cards are hidden unless one of the exact views above needs them.
    if (name == "Black Door Frame Left F1"
        || name == "Black Door Frame Right F1"
        || name == "Black Door Frame Left F2"
        || name == "Black Door Frame Right F2"
        || name == "BlackDoorF1")
    {
      return false;
    }

    return true;
  }

  /// <summary>
  /// Live editor cards come from layout.Pieces. Obsolete A/B pieces must be
  /// removed here so Unity in-memory state cannot write them back to disk.
  /// </summary>
  private void StripObsoleteFrontWallF1ABPieces()
  {
    if (layout == null || layout.Pieces == null)
      return;

    bool hasFrontWallF1 = false;
    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name == "Front Wall F1"
          || piece.Graphic == DungeonGraphicType.FrontWallF1)
      {
        hasFrontWallF1 = true;
        break;
      }
    }

    int insertAt = -1;
    int keepX = 40;
    int keepY = 47;
    bool removed = false;

    for (int i = layout.Pieces.Count - 1; i >= 0; i--)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "Front Wall F1 A"
          && piece.Name != "Front Wall F1 B")
      {
        continue;
      }

      keepX = piece.X;
      keepY = piece.Y;
      insertAt = i;
      layout.Pieces.RemoveAt(i);
      removed = true;
    }

    if (!hasFrontWallF1)
    {
      if (insertAt < 0)
        insertAt = layout.Pieces.Count;

      layout.Pieces.Insert(
          insertAt,
          new ViewportPiece
          {
            Name = "Front Wall F1",
            Graphic = DungeonGraphicType.FrontWallF1,
            X = keepX,
            Y = keepY,
            Enabled = false,
            MirrorHorizontally = false,
            FrontWallF1Width = StraightF1WallLogic.DefaultFrontWallF1Width
          });
      removed = true;
    }

    if (!removed)
      return;

    EditorUtility.SetDirty(layout);
    ClampSelectedPieceIndex();
  }


  private bool PieceMatchesSearchFilter(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (showWallsActivFilter)
    {
      if (MatchesShowWallsActivFilter(piece))
        return true;

      // Pose changes force Activ on. Activ only matches enabled normal walls,
      // so needed Black Door cards would be dropped after the Needed check.
      return showOnlyWallsNeededForCurrentPose
          && IsBlackDoorEditorPiece(piece)
          && IsWallNeededForCurrentPose(piece);
    }

    string search = (pieceSearchText ?? string.Empty).Trim();
    if (search.Length > 0)
    {
      string name = piece != null ? piece.Name ?? string.Empty : string.Empty;
      return name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    if (pieceSearchFamilyIndex <= 0
        || pieceSearchFamilyIndex >= PieceSearchFamilyOptions.Length)
    {
      return !IsOnDemandSearchPiece(piece);
    }

    string selectedFamily =
        PieceSearchFamilyOptions[pieceSearchFamilyIndex];
    return PieceMatchesSearchFamily(piece, selectedFamily);
  }

  /// <summary>
  /// Movement Arrows, Champion Status Slots, Ceiling, and Floor stay off the
  /// unfiltered ViewEdit list; Search Pieces text/family can still reveal them.
  /// </summary>
  private static bool IsOnDemandSearchPiece(ViewportPiece piece)
  {
    return PieceMatchesSearchFamily(piece, "Arrows")
        || PieceMatchesSearchFamily(piece, "Champion Slot 1")
        || PieceMatchesSearchFamily(piece, "Champion Slot 2")
        || PieceMatchesSearchFamily(piece, "Champion Slot 3")
        || PieceMatchesSearchFamily(piece, "Champion Slot 4")
        || PieceMatchesSearchFamily(piece, "Ceiling")
        || PieceMatchesSearchFamily(piece, "Floor");
  }

  /// <summary>
  /// Show Walls Activ: every currently enabled wall piece, including FrontF1/F2/F3
  /// by name or graphic. Ceiling and Floor are never included. Family and text
  /// search are not applied.
  /// </summary>
  private static bool MatchesShowWallsActivFilter(ViewportPiece piece)
  {
    if (piece == null || !piece.Enabled || IsFloorOrCeiling(piece))
      return false;

    DungeonGraphicType graphic = piece.Graphic;
    if (graphic == DungeonGraphicType.FrontWallF1
        || graphic == DungeonGraphicType.FrontWallF1_A
        || graphic == DungeonGraphicType.FrontWallF1_B
        || graphic == DungeonGraphicType.FrontWallF2
        || graphic == DungeonGraphicType.FrontWallF3
        || graphic == DungeonGraphicType.WallF0L
        || graphic == DungeonGraphicType.WallF0R
        || graphic == DungeonGraphicType.WallF1L
        || graphic == DungeonGraphicType.WallF1R
        || graphic == DungeonGraphicType.WallF2L
        || graphic == DungeonGraphicType.WallF2R
        || graphic == DungeonGraphicType.WallF3L
        || graphic == DungeonGraphicType.WallF3R)
    {
      return true;
    }

    string name = piece.Name ?? string.Empty;
    return name.StartsWith("FrontF", System.StringComparison.Ordinal)
        || name.StartsWith("Front Wall F", System.StringComparison.Ordinal)
        || name.StartsWith("LeftF", System.StringComparison.Ordinal)
        || name.StartsWith("RightF", System.StringComparison.Ordinal)
        || name.StartsWith("Wall F", System.StringComparison.Ordinal)
        || name == "LeftD3"
        || name == "RightD3"
        || name == "LeftS2"
        || name == "Right2S";
  }

  /// <summary>
  /// Exact family filters for the Search Pieces dropdown. Prefixes use FrontF /
  /// LeftF / RightF so LeftD3 and RightD3 are not included in Left / Right.
  /// </summary>
  private static bool PieceMatchesSearchFamily(
      ViewportPiece piece,
      string family)
  {
    if (piece == null)
      return false;

    string name = piece.Name ?? string.Empty;
    switch (family)
    {
      case "Front":
        return name.StartsWith("FrontF", System.StringComparison.Ordinal)
            || name.StartsWith("Front Wall F", System.StringComparison.Ordinal);
      case "Left":
        return name.StartsWith("LeftF", System.StringComparison.Ordinal);
      case "Right":
        return name.StartsWith("RightF", System.StringComparison.Ordinal);
      case "LeftD3":
        return name == "LeftD3";
      case "RightD3":
        return name == "RightD3";
      case "Black Door":
        return name.IndexOf("Black Door", System.StringComparison.OrdinalIgnoreCase) >= 0
            || string.Equals(
                name,
                "BlackDoorF1",
                System.StringComparison.OrdinalIgnoreCase);
      case "Active":
        return piece.Enabled;
      case "Arrows":
        return name == "Movement Arrows"
            || piece.Graphic == DungeonGraphicType.MovementArrows;
      case "Champion Slot 1":
        return name == "Champion Status Slot 1";
      case "Champion Slot 2":
        return name == "Champion Status Slot 2";
      case "Champion Slot 3":
        return name == "Champion Status Slot 3";
      case "Champion Slot 4":
        return name == "Champion Status Slot 4";
      case "Ceiling":
        return name == "Ceiling"
            || piece.Graphic == DungeonGraphicType.Ceiling;
      case "Floor":
        return name == "Floor"
            || piece.Graphic == DungeonGraphicType.Floor;
      default:
        return true;
    }
  }

  private GUIStyle GetSearchPiecesLabelStyle()
  {
    if (searchPiecesLabelStyle == null)
    {
      searchPiecesLabelStyle = new GUIStyle(EditorStyles.label)
      {
        fontStyle = FontStyle.Bold
      };
      Color brightGreen = new Color(0.2f, 1.0f, 0.2f);
      searchPiecesLabelStyle.normal.textColor = brightGreen;
      searchPiecesLabelStyle.hover.textColor = brightGreen;
      searchPiecesLabelStyle.focused.textColor = brightGreen;
      searchPiecesLabelStyle.active.textColor = brightGreen;
    }

    return searchPiecesLabelStyle;
  }

  private GUIStyle GetPieceFamilyHeaderStyle(Color color)
  {
    if (pieceFamilyHeaderStyle == null)
    {
      pieceFamilyHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
      {
        fontStyle = FontStyle.Bold
      };
    }

    pieceFamilyHeaderStyle.normal.textColor = color;
    pieceFamilyHeaderStyle.hover.textColor = color;
    pieceFamilyHeaderStyle.focused.textColor = color;
    pieceFamilyHeaderStyle.active.textColor = color;
    return pieceFamilyHeaderStyle;
  }

  private bool HandleGeometryDiagnosticsRightClickClose()
  {
    if (!showGeometryDiagnostics)
      return false;

    Event current = Event.current;
    if (current == null)
      return false;

    bool rightPressed =
        current.type == EventType.MouseDown && current.button == 1;
    if (!rightPressed && current.type != EventType.ContextClick)
      return false;

    if (!geometryDiagnosticRect.Contains(current.mousePosition))
      return false;

    showGeometryDiagnostics = false;
    current.Use();
    Repaint();
    return true;
  }

  /// <summary>
  /// Right-click anywhere in ViewEdit scrolls to the top and focuses the
  /// Search Pieces text field. Does not open a popup or change Enabled flags.
  /// </summary>
  private void HandleViewEditRightClickHome()
  {
    Event current = Event.current;
    if (current == null)
      return;

    bool rightPressed =
        current.type == EventType.MouseDown && current.button == 1;
    if (!rightPressed && current.type != EventType.ContextClick)
      return;

    editorScroll = Vector2.zero;
    pieceSearchFamilyIndex = 0;
    focusSearchPieces = true;
    current.Use();
    Repaint();
  }

  private void ShowSearchPiecesFamilyMenu(Rect popupRect)
  {
    GenericMenu menu = new GenericMenu();
    for (int i = 0; i < PieceSearchFamilyOptions.Length; i++)
    {
      string label = PieceSearchFamilyOptions[i];
      if (string.IsNullOrEmpty(label))
        continue;

      int index = i;
      menu.AddItem(
          new GUIContent(label),
          false,
          () =>
          {
            pieceSearchFamilyIndex = index;
            showWallsActivFilter = false;
            pieceSearchText = string.Empty;
            editorScroll = Vector2.zero;
            GUI.FocusControl(null);
            Repaint();
          });
    }

    menu.DropDown(popupRect);
  }

  private void ClampSearchFamilyToEnabledPieces()
  {
    if (pieceSearchFamilyIndex <= 0)
      return;

    if (pieceSearchFamilyIndex >= PieceSearchFamilyOptions.Length)
    {
      pieceSearchFamilyIndex = 0;
      return;
    }

    string family = PieceSearchFamilyOptions[pieceSearchFamilyIndex];
    if (string.IsNullOrEmpty(family))
    {
      pieceSearchFamilyIndex = 0;
      return;
    }

    // Keep the selected family even when all of its pieces are disabled.
    // This lets ViewEdit show those disabled pieces so they can be enabled again.
  }

  private static bool IsPermanentSearchFamily(string family)
  {
    return family == "Arrows"
        || family == "Champion Slot 1"
        || family == "Champion Slot 2"
        || family == "Champion Slot 3"
        || family == "Champion Slot 4"
        || family == "Ceiling"
        || family == "Floor";
  }

  private bool FamilyHasEnabledPiece(string family)
  {
    if (layout == null || layout.Pieces == null || string.IsNullOrEmpty(family))
      return false;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null || !piece.Enabled)
        continue;

      if (PieceMatchesSearchFamily(piece, family))
        return true;
    }

    return false;
  }

  private void HandlePieceSearchKeyboard()
  {
    Event current = Event.current;
    if (current.type != EventType.KeyDown)
      return;

    if (focusedWindow != this)
      return;

    if (current.keyCode != KeyCode.Escape)
      return;

    if (pieceSearchFamilyIndex == 0)
      return;

    pieceSearchFamilyIndex = 0;
    current.Use();
    GUI.FocusControl(null);
    Repaint();
  }

  /// <summary>
  /// Piece-card checkbox that toggles only from mouse clicks. Keyboard
  /// events are ignored and the control is not allowed to keep focus, so
  /// Delete/Page Down/arrows/Space cannot change Enabled or Mirror.
  /// </summary>
  private static bool DrawMouseOnlyToggle(
      string label,
      bool value,
      bool pieceEnabled,
      params GUILayoutOption[] options)
  {
    Rect rowRect = EditorGUILayout.GetControlRect(
        true,
        EditorGUIUtility.singleLineHeight,
        options);
    Rect toggleRect = EditorGUI.PrefixLabel(rowRect, new GUIContent(label));
    toggleRect.width = 16f;

    Event current = Event.current;
    bool isMirror = label == "Mirror" || label == "Mirror Horizontally";
    if (current != null
        && current.type == EventType.MouseDown
        && current.button == 0
        && toggleRect.Contains(current.mousePosition))
    {
      if (!isMirror || pieceEnabled)
      {
        value = !value;
        GUI.changed = true;
        current.Use();
      }
    }

    // Shared Unity checkbox chrome. Colored fills are
    // inset so they cannot cover the PrefixLabel or change hit-testing.
    GUI.Toggle(toggleRect, false, GUIContent.none, EditorStyles.toggle);

    Color checkColor = new Color(0.2f, 1.0f, 0.2f);
    bool showChecked = value;
    if (isMirror && !pieceEnabled)
      showChecked = false;
    if (pieceEnabled && showChecked && isMirror)
    {
      EditorGUI.DrawRect(InsetToggleFillRect(toggleRect), new Color(1f, 140f / 255f, 0f));
      checkColor = Color.black;
    }

    if (showChecked)
    {
      GUIStyle checkStyle = new GUIStyle(EditorStyles.label)
      {
        alignment = TextAnchor.MiddleCenter,
        fontStyle = FontStyle.Bold,
        fontSize = 13,
        padding = new RectOffset(0, 0, 0, 1)
      };
      checkStyle.normal.textColor = checkColor;
      GUI.Label(toggleRect, "✓", checkStyle);
    }

    // This custom toggle never takes keyboard focus, preserving ViewEdit's
    // Delete/Page Down/arrows/Space navigation behavior.
    return value;
  }

  private static Rect InsetToggleFillRect(Rect toggleRect)
  {
    const float inset = 2f;
    return new Rect(
        toggleRect.x + inset,
        toggleRect.y + inset,
        toggleRect.width - inset * 2f,
        toggleRect.height - inset * 2f);
  }

  /// <summary>
  /// ViewEdit card list only. Does not change layout.Pieces or draw order.
  /// Front, then Left, then Right, then all remaining pieces in original order.
  /// </summary>
  private int CompareViewEditPieceDisplayOrder(int indexA, int indexB)
  {
    ViewportPiece pieceA = layout.Pieces[indexA];
    ViewportPiece pieceB = layout.Pieces[indexB];
    if (previewX == 5
        && previewY == 2
        && previewFacing == DungeonFacing.South)
    {
      int orderA = Get52SouthLeftToRightOrder(pieceA);
      int orderB = Get52SouthLeftToRightOrder(pieceB);
      if (orderA != orderB)
        return orderA.CompareTo(orderB);
      return indexA.CompareTo(indexB);
    }

    int groupA = GetViewEditDisplayFamilyGroup(pieceA);
    int groupB = GetViewEditDisplayFamilyGroup(pieceB);
    if (groupA != groupB)
      return groupA.CompareTo(groupB);

    int distanceA = GetViewEditDisplayDistanceOrder(pieceA);
    int distanceB = GetViewEditDisplayDistanceOrder(pieceB);
    if (distanceA != distanceB)
      return distanceA.CompareTo(distanceB);

    return indexA.CompareTo(indexB);
  }

  private static int GetViewEditDisplayFamilyGroup(ViewportPiece piece)
  {
    if (piece == null)
      return 3;

    if (IsFrontWallF1Card(piece)
        || IsFrontWallF2Card(piece)
        || IsFrontWallF3Card(piece))
    {
      return 0;
    }

    if (IsWallF0LeftPiece(piece)
        || IsWallF1LeftPiece(piece)
        || IsWallF2LeftPiece(piece)
        || IsWallF3LeftPiece(piece)
        || piece.Name == "LeftD3"
        || piece.Name == "Wall D3L2"
        || piece.Name == "LeftS2")
    {
      return 1;
    }

    if (IsWallF0RightPiece(piece)
        || IsWallF1RightPiece(piece)
        || IsWallF2RightPiece(piece)
        || IsWallF3RightPiece(piece)
        || piece.Name == "RightD3"
        || piece.Name == "Wall D3R2"
        || piece.Name == "Right2S")
    {
      return 2;
    }

    return 3;
  }

  private static int GetViewEditDisplayDistanceOrder(ViewportPiece piece)
  {
    if (piece == null)
      return 0;

    if (IsFrontWallF1Card(piece)
        || IsWallF0LeftPiece(piece)
        || IsWallF0RightPiece(piece))
    {
      return 0;
    }

    if (IsFrontWallF2Card(piece)
        || IsWallF1LeftPiece(piece)
        || IsWallF1RightPiece(piece))
    {
      return 1;
    }

    if (IsFrontWallF3Card(piece)
        || IsWallF2LeftPiece(piece)
        || IsWallF2RightPiece(piece))
    {
      return 2;
    }

    if (IsWallF3LeftPiece(piece) || IsWallF3RightPiece(piece))
      return 3;

    if (piece.Name == "LeftD3"
        || piece.Name == "Wall D3L2"
        || piece.Name == "RightD3"
        || piece.Name == "Wall D3R2")
    {
      return 4;
    }

    return 0;
  }

  /// <summary>
  /// Read-only canonical ViewEdit X/Y by piece name. Display only.
  /// One entry per wall piece. Null X/Y means not yet defined (Ref X/Y - / -).
  /// </summary>
  private static readonly (string Name, int? X, int? Y)[] CanonicalReferenceXYByName =
  {
    // Front
    ("FrontF0", null, null),
    ("FrontF1", 0, 42),
    ("FrontF2", 0, 125),
    ("FrontF3", 7, 58),
    ("Front Wall F1", null, null),
    ("Front Wall F2", null, null),
    ("Front Wall F3", null, null),

    // Left
    ("LeftF0", 0, 33),
    ("LeftF1", 0, 42),
    ("LeftF2", 0, 52),
    ("LeftF3", 5, 60),
    ("Wall F0Left", null, null),
    ("Wall F1Left", null, null),
    ("Wall F2Left", null, null),
    ("Wall F3Left", null, null),

    // Right
    ("RightF0", 192, 33),
    ("RightF1", 165, 42),
    ("RightF2", 147, 51),
    ("RightF3", 136, 60),
    ("Wall F0Right", null, null),
    ("Wall F1Right", null, null),
    ("Wall F2Right", null, null),
    ("Wall F3Right", null, null),

    // LeftD3
    ("LeftD3", null, null),
    ("Wall D3L2", null, null),

    // RightD3
    ("RightD3", 190, 58),
    ("Wall D3R2", null, null),

    // 2S (ViewEdit list only; no geometry yet)
    ("LeftS2", null, null),
    ("Right2S", null, null),

    // Black Door
    ("BlackDoorF1", 63, 47),
    ("BlackDoorF2", 77, 46),
    ("BlackDoorF3", 90, 101),
    ("Black Door Frame Left F1", 44, 46),
    ("Black Door Frame Left F2", 64, 54),
    ("Black Door Frame Left F3", 81, 102),
    ("Black Door Frame Right F1", 154, 46),
    ("Black Door Frame Right F2", 140, 54),
    ("Black Door Frame Right F3", 134, 102),
  };

  /// <summary>
  /// Runtime Adjust Ref overrides. Checked before the static table.
  /// Stores non-mirrored Ref X and Ref Y.
  /// </summary>
  private static readonly Dictionary<string, Vector2Int> CanonicalReferenceXYOverrides =
      new Dictionary<string, Vector2Int>();

  /// <summary>
  /// Runtime mirrored Ref X only. Non-mirrored Ref X and Ref Y stay in
  /// CanonicalReferenceXYOverrides.
  /// </summary>
  private static readonly Dictionary<string, int> CanonicalMirroredReferenceXOverrides =
      new Dictionary<string, int>();

  private static readonly (string Name, int X)[] CanonicalMirroredReferenceXDefaults =
  {
    ("LeftF0", 192),
    ("LeftF1", 0),
    ("LeftF2", 0),
    ("LeftF3", 136),
    ("RightF0", 0),
    ("RightF1", 0),
    ("RightF2", 147),
    ("RightF3", 136),
  };

  /// <summary>
  /// Read-only canonical ViewEdit X/Y by piece name. Display only.
  /// </summary>
  private static bool TryGetCanonicalReferenceXY(
      string pieceName,
      out int x,
      out int y)
  {
    x = 0;
    y = 0;
    if (string.IsNullOrEmpty(pieceName))
      return false;

    if (CanonicalReferenceXYOverrides.TryGetValue(
            pieceName, out Vector2Int overridden))
    {
      x = overridden.x;
      y = overridden.y;
      return true;
    }

    for (int i = 0; i < CanonicalReferenceXYByName.Length; i++)
    {
      (string Name, int? X, int? Y) entry = CanonicalReferenceXYByName[i];
      if (entry.Name != pieceName)
        continue;

      if (!entry.X.HasValue || !entry.Y.HasValue)
        return false;

      x = entry.X.Value;
      y = entry.Y.Value;
      return true;
    }

    return false;
  }

  private static void SetCanonicalReferenceXY(string pieceName, int x, int y)
  {
    if (string.IsNullOrEmpty(pieceName))
      return;

    CanonicalReferenceXYOverrides[pieceName] = new Vector2Int(x, y);
  }

  private static bool TryGetSideWallCanonicalName(
      ViewportPiece piece,
      out string name)
  {
    name = null;
    if (piece == null)
      return false;

    if (IsWallF0LeftPiece(piece))
    {
      name = "LeftF0";
      return true;
    }
    if (IsWallF1LeftPiece(piece))
    {
      name = "LeftF1";
      return true;
    }
    if (IsWallF2LeftPiece(piece))
    {
      name = "LeftF2";
      return true;
    }
    if (IsWallF3LeftPiece(piece))
    {
      name = "LeftF3";
      return true;
    }
    if (IsWallF0RightPiece(piece))
    {
      name = "RightF0";
      return true;
    }
    if (IsWallF1RightPiece(piece))
    {
      name = "RightF1";
      return true;
    }
    if (IsWallF2RightPiece(piece))
    {
      name = "RightF2";
      return true;
    }
    if (IsWallF3RightPiece(piece))
    {
      name = "RightF3";
      return true;
    }

    return false;
  }

  private static bool TryGetMirroredReferenceXDefault(string pieceName, out int x)
  {
    x = 0;
    if (string.IsNullOrEmpty(pieceName))
      return false;

    for (int i = 0; i < CanonicalMirroredReferenceXDefaults.Length; i++)
    {
      if (CanonicalMirroredReferenceXDefaults[i].Name != pieceName)
        continue;

      x = CanonicalMirroredReferenceXDefaults[i].X;
      return true;
    }

    return false;
  }

  private static bool TryGetMirroredReferenceX(ViewportPiece piece, out int x)
  {
    x = 0;
    if (piece == null)
      return false;

    if (!string.IsNullOrEmpty(piece.Name)
        && CanonicalMirroredReferenceXOverrides.TryGetValue(piece.Name, out x))
      return true;

    if (TryGetSideWallCanonicalName(piece, out string canonicalName)
        && CanonicalMirroredReferenceXOverrides.TryGetValue(canonicalName, out x))
      return true;

    if (!string.IsNullOrEmpty(piece.Name)
        && TryGetMirroredReferenceXDefault(piece.Name, out x))
      return true;

    return TryGetSideWallCanonicalName(piece, out canonicalName)
        && TryGetMirroredReferenceXDefault(canonicalName, out x);
  }

  private static bool TryGetActiveCanonicalReferenceXY(
      ViewportPiece piece,
      bool mirror,
      out int x,
      out int y)
  {
    x = 0;
    y = 0;
    if (piece == null || string.IsNullOrEmpty(piece.Name))
      return false;

    string referenceName = piece.Name;
    if (TryGetSideWallCanonicalName(piece, out string canonicalName))
      referenceName = canonicalName;

    if (!TryGetCanonicalReferenceXY(referenceName, out x, out y))
      return false;

    // For side walls, Mirror changes only the active Ref X.
    // Ref Y always stays at the normal canonical Y.
    if (mirror && TryGetMirroredReferenceX(piece, out int mirroredX))
      x = mirroredX;

    return true;
  }

  private static void SetActiveCanonicalReferenceXY(
      ViewportPiece piece,
      bool mirror,
      int x,
      int y)
  {
    if (piece == null || string.IsNullOrEmpty(piece.Name))
      return;

    SetCanonicalReferenceXY(piece.Name, x, y);
  }

  private bool IsShowAllWallsPreview()
  {
    return !Application.isPlaying && !showOnlyWallsNeededForCurrentPose;
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

    DungeonGraphicType graphicBeforePopup = piece.Graphic;
    if (previewGraphicOverrideByPiece.TryGetValue(
            piece, out DungeonGraphicType graphicOverride))
    {
      graphicBeforePopup = graphicOverride;
      piece.Graphic = graphicOverride;
    }

    bool compactFrontWallHeader =
        IsFrontWallF1Card(piece)
        || IsFrontWallF2Card(piece)
        || IsFrontWallF3Card(piece);
    bool compactBlackDoorFrontHeader =
        piece.Name == "BlackDoorF1"
        || piece.Name == "BlackDoorF2"
        || piece.Name == "BlackDoorF3";
    bool compactBlackDoorF1FrameHeader =
        piece.Name == "Black Door Frame Left F1"
        || piece.Name == "Black Door Frame Right F1"
        || piece.Name == "Black Door Frame Left F2"
        || piece.Name == "Black Door Frame Right F2"
        || piece.Name == "Black Door Frame Left F3"
        || piece.Name == "Black Door Frame Right F3";
    bool isLeftD3Card =
        piece.Name == "LeftD3"
        || piece.Name == "Wall D3L2"
        || piece.Name == "WallD3L2";
    bool compactD3Header =
        !isLeftD3Card
        && (piece.Name == "RightD3"
            || piece.Name == "Wall D3R2"
            || piece.Graphic == DungeonGraphicType.WallD3L2
            || piece.Graphic == DungeonGraphicType.WallD3R2);
    bool compactSideWallHeader =
        IsWallF0LeftPiece(piece)
        || IsWallF0RightPiece(piece)
        || IsWallF1LeftPiece(piece)
        || IsWallF1RightPiece(piece)
        || IsWallF2LeftPiece(piece)
        || IsWallF2RightPiece(piece)
        || IsWallF3LeftPiece(piece)
        || IsWallF3RightPiece(piece)
        || isLeftD3Card
        || piece.Name == "LeftS2"
        || piece.Name == "Right2S";
    bool hideNameForWall = IsWallEditorPiece(piece);

    if (!compactFrontWallHeader
        && !compactBlackDoorFrontHeader
        && !compactBlackDoorF1FrameHeader
        && !compactD3Header
        && !compactSideWallHeader)
    {
      EditorGUILayout.BeginHorizontal();
      string headerText = isSelected ? $"▶ {piece.Name}" : piece.Name;
      if (TryGetPieceFamilyLabelColor(piece, out Color familyColor))
      {
        EditorGUILayout.LabelField(
            headerText,
            GetPieceFamilyHeaderStyle(familyColor));
      }
      else
      {
        EditorGUILayout.LabelField(headerText, EditorStyles.label);
      }
      EditorGUILayout.EndHorizontal();
    }

    EditorGUI.BeginChangeCheck();
    EditorGUILayout.BeginHorizontal();
    float savedNameLabelWidth = EditorGUIUtility.labelWidth;
    if (compactFrontWallHeader || compactBlackDoorFrontHeader)
    {
      string headerText = isSelected ? $"▶ {piece.Name}" : piece.Name;
      if (TryGetPieceFamilyLabelColor(piece, out Color familyColor))
      {
        EditorGUILayout.LabelField(
            headerText,
            GetPieceFamilyHeaderStyle(familyColor),
            GUILayout.Width(110f));
      }
      else
      {
        EditorGUILayout.LabelField(
            headerText,
            EditorStyles.label,
            GUILayout.Width(110f));
      }

      EditorGUIUtility.labelWidth = 0f;
      if (IsFrontWallF1Card(piece))
      {
        // FrontF1 has exactly one valid graphic. Do not expose the full
        // DungeonGraphicType enum (doors, ornaments, obsolete F1_A/F1_B, etc.).
        if (piece.Graphic != DungeonGraphicType.FrontWallF1)
        {
          piece.Graphic = DungeonGraphicType.FrontWallF1;
          GUI.changed = true;
        }

        EditorGUILayout.Popup(
            0,
            new[] { "Front Wall F1" },
            GUILayout.Width(135f));
      }
      else
      {
        piece.Graphic = (DungeonGraphicType)EditorGUILayout.EnumPopup(
            GUIContent.none, piece.Graphic, GUILayout.Width(135f));
      }

      // Front pieces use two compact rows:
      // row 1 = name + Graphic + Enabled + Mirror
      // row 2 = Width (when applicable) + X + Y + Ref.
    }
    else if (compactBlackDoorF1FrameHeader)
    {
      string headerText = isSelected ? $"▶ {piece.Name}" : piece.Name;
      if (TryGetPieceFamilyLabelColor(piece, out Color familyColor))
      {
        EditorGUILayout.LabelField(
            headerText,
            GetPieceFamilyHeaderStyle(familyColor),
            GUILayout.Width(180f));
      }
      else
      {
        EditorGUILayout.LabelField(
            headerText,
            EditorStyles.label,
            GUILayout.Width(180f));
      }

      EditorGUIUtility.labelWidth = 0f;
      piece.Graphic = (DungeonGraphicType)EditorGUILayout.EnumPopup(
          GUIContent.none, piece.Graphic, GUILayout.Width(135f));

      // Black Door elements use the same compact arrangement as the normal
      // Left/Right wall cards: name + Graphic + Enabled + Mirror on row 1,
      // then X / Y on row 2.
    }
    else if (compactD3Header)
    {
      string headerText = isSelected ? $"▶ {piece.Name}" : piece.Name;
      if (TryGetPieceFamilyLabelColor(piece, out Color familyColor))
      {
        EditorGUILayout.LabelField(
            headerText,
            GetPieceFamilyHeaderStyle(familyColor),
            GUILayout.Width(110f));
      }
      else
      {
        EditorGUILayout.LabelField(
            headerText,
            EditorStyles.label,
            GUILayout.Width(110f));
      }

      EditorGUIUtility.labelWidth = 0f;
      piece.Graphic = (DungeonGraphicType)EditorGUILayout.EnumPopup(
          GUIContent.none, piece.Graphic, GUILayout.Width(135f));

      // Match FrontF1's compact card layout: title + Graphic on row 1,
      // Enabled / Mirror / Ref on row 2, then X / Y on row 3.
      EditorGUILayout.EndHorizontal();
      EditorGUILayout.BeginHorizontal();
    }
    else if (compactSideWallHeader)
    {
      string headerText = isSelected ? $"▶ {piece.Name}" : piece.Name;
      if (TryGetPieceFamilyLabelColor(piece, out Color familyColor))
      {
        EditorGUILayout.LabelField(
            headerText,
            GetPieceFamilyHeaderStyle(familyColor),
            GUILayout.Width(110f));
      }
      else
      {
        EditorGUILayout.LabelField(
            headerText,
            EditorStyles.label,
            GUILayout.Width(110f));
      }

      EditorGUIUtility.labelWidth = 0f;
      piece.Graphic = (DungeonGraphicType)EditorGUILayout.EnumPopup(
          GUIContent.none, piece.Graphic, GUILayout.Width(135f));

      // Keep side-wall Enabled and Mirror on the same first row.
    }
    else if (!hideNameForWall)
    {
      float nameLabelWidth =
          EditorStyles.label.CalcSize(new GUIContent("Name")).x;
      EditorGUIUtility.labelWidth = nameLabelWidth;
      piece.Name = EditorGUILayout.TextField("Name", piece.Name);
      EditorGUILayout.EndHorizontal();
      EditorGUILayout.BeginHorizontal();
    }
    EditorGUIUtility.labelWidth = savedNameLabelWidth;

    float previousLabelWidth = EditorGUIUtility.labelWidth;
    const float ToggleBoxWidth = 18f;
    const float ToggleGroupGap = 10f;

    const string EnabledLabel = "Enabled";
    float enabledLabelWidth =
        EditorStyles.label.CalcSize(new GUIContent(EnabledLabel)).x;
    EditorGUIUtility.labelWidth = enabledLabelWidth;

    bool normalWallEnabledPreview = IsNormalWallPiece(piece);
    bool isBlackDoorRightD3Exception =
        IsBlackDoorObliqueRightD3PoseException(piece);

    bool enabledBefore = piece.Enabled;
    if (normalWallEnabledPreview
        && TryGetResolvedNormalWallState(
            piece, out ResolvedNormalWallState enabledPreviewState))
    {
      enabledBefore = enabledPreviewState.Enabled;
    }
    if ((normalWallEnabledPreview || isBlackDoorRightD3Exception)
        && previewEnabledOverrideByPiece.TryGetValue(
            piece, out bool manualPreviewEnabled))
    {
      enabledBefore = manualPreviewEnabled;
    }

    // (5,2) South only forces pieces outside the allowed set OFF.
    // Show All Walls keeps the ViewEdit Enabled checkbox as the preview authority.
    if (!IsShowAllWallsPreview()
        && TryGet52SouthForcedWallEnabled(piece, out bool forced52UiEnabled)
        && !forced52UiEnabled)
      enabledBefore = false;

    bool enabledAfter = DrawMouseOnlyToggle(
        EnabledLabel,
        enabledBefore,
        enabledBefore,
        GUILayout.Width(enabledLabelWidth + ToggleBoxWidth),
        GUILayout.ExpandWidth(false));
    bool nameOrEnabledChanged = EditorGUI.EndChangeCheck();

    if ((normalWallEnabledPreview || isBlackDoorRightD3Exception)
        && enabledAfter != enabledBefore)
    {
      bool is52SouthForcedOff =
          !IsShowAllWallsPreview()
          && TryGet52SouthForcedWallEnabled(piece, out bool forced52UiChangeEnabled)
          && !forced52UiChangeEnabled;
      if (!is52SouthForcedOff)
      {
        // ViewEdit-only stationary-pose test, identical in lifetime to X/Y/Mirror.
        // Geometry remains authoritative after X/Y/Facing changes.
        previewEnabledOverrideByPiece[piece] = enabledAfter;
        previewEnabledChangedThisFrame = true;
        RefreshTemporaryNormalWallPreview();
      }
    }
    else if (!normalWallEnabledPreview && !isBlackDoorRightD3Exception)
    {
      piece.Enabled = enabledAfter;
    }

    bool effectiveEnabled =
        (normalWallEnabledPreview || isBlackDoorRightD3Exception)
        ? enabledAfter
        : piece.Enabled;

    // Normal-wall Mirror is a temporary ViewEdit override only. Geometry remains
    // authoritative and the override is discarded as soon as the preview pose
    // changes. Non-normal pieces retain their existing authored behavior.
    bool normalWallMirrorPreview = IsNormalWallPiece(piece);
    bool mirrorBefore = piece.MirrorHorizontally;
    if (normalWallMirrorPreview
        && previewMirrorOverrideByPiece.TryGetValue(piece, out bool previewMirror))
      mirrorBefore = previewMirror;

    GUILayout.Space(ToggleGroupGap);
    const string MirrorLabel = "Mirror";
    float mirrorLabelWidth =
        EditorStyles.label.CalcSize(new GUIContent(MirrorLabel)).x;
    EditorGUIUtility.labelWidth = mirrorLabelWidth;
    bool mirrorAfter = DrawMouseOnlyToggle(
        MirrorLabel,
        mirrorBefore,
        effectiveEnabled,
        GUILayout.Width(mirrorLabelWidth + ToggleBoxWidth),
        GUILayout.ExpandWidth(false));
    EditorGUIUtility.labelWidth = previousLabelWidth;
    GUILayout.Space(ToggleGroupGap);

    bool hasPieceCardReference = TryGetPieceCardReferenceXY(
        piece, mirrorAfter, out int canonicalRefX, out int canonicalRefY);

    if (compactD3Header)
    {
      string refLabel = hasPieceCardReference
          ? $"Ref X {canonicalRefX} / Y {canonicalRefY}"
          : "Ref X - / Y -";
      EditorGUILayout.LabelField(
          refLabel,
          GUILayout.ExpandWidth(false));
    }

    EditorGUILayout.EndHorizontal();

    EditorGUI.BeginChangeCheck();
    if (!compactFrontWallHeader
        && !compactBlackDoorFrontHeader
        && !compactBlackDoorF1FrameHeader
        && !compactD3Header
        && !compactSideWallHeader)
    {
      EditorGUILayout.BeginHorizontal();

      piece.Graphic = (DungeonGraphicType)EditorGUILayout.EnumPopup(
          "Graphic",
          piece.Graphic);

      string refLabel = hasPieceCardReference
          ? $"Ref X {canonicalRefX} / Y {canonicalRefY}"
          : "Ref X - / Y -";
      EditorGUILayout.LabelField(
          refLabel,
          GUILayout.ExpandWidth(false));
      EditorGUILayout.EndHorizontal();
    }
    if (EditorGUI.EndChangeCheck() || nameOrEnabledChanged)
    {
      SelectPiece(index);
      changed = true;
    }

    if (piece.Graphic != graphicBeforePopup)
    {
      previewGraphicOverrideByPiece[piece] = piece.Graphic;
      previewGraphicChangedThisFrame = true;
      RefreshTemporaryNormalWallPreview();
    }

    if (piece.Name == "BlackDoorF1")
    {
      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.PrefixLabel("Size");
      using (new EditorGUI.DisabledScope(true))
      {
        EditorGUILayout.IntField(96, GUILayout.Width(50));
        EditorGUILayout.LabelField("x", GUILayout.Width(12));
        EditorGUILayout.IntField(88, GUILayout.Width(50));
      }
      EditorGUILayout.EndHorizontal();
    }

    if (StraightF1WallLogic.IsFloorOrCeilingGraphic(piece.Graphic))
    {
      EditorGUILayout.HelpBox(
          "Ceiling/Floor Mirror is the per-pose Mirror Horizontally flag "
              + "(Edit Mode and Play/Build).",
          MessageType.None);
    }

    if (mirrorAfter != mirrorBefore)
    {
      if (normalWallMirrorPreview)
      {
        previewMirrorOverrideByPiece[piece] = mirrorAfter;
        previewMirrorChangedThisFrame = true;
        RefreshTemporaryNormalWallPreview();
      }
      else
      {
        piece.MirrorHorizontally = mirrorAfter;
        changed = true;
        ApplyMirrorHorizontallyChangeForCurrentPose();
      }
    }

    bool hasFrontF1Width = IsFrontWallF1Card(piece);
    int frontF1WidthBefore = 0;
    if (hasFrontF1Width)
    {
      frontF1WidthBefore =
          StraightF1WallLogic.NormalizeFrontWallF1Width(
              piece.FrontWallF1Width);

      if (TryGetResolvedNormalWallState(
              piece, out ResolvedNormalWallState resolvedF1WidthState)
          && resolvedF1WidthState.FrontF1Width > 0)
      {
        frontF1WidthBefore =
            StraightF1WallLogic.NormalizeFrontWallF1Width(
                resolvedF1WidthState.FrontF1Width);
      }

      if (previewFrontF1WidthOverrideByPiece.TryGetValue(
              piece, out int previewF1Width))
      {
        frontF1WidthBefore =
            StraightF1WallLogic.NormalizeFrontWallF1Width(previewF1Width);
      }
    }

    EditorGUILayout.BeginHorizontal();

    if (hasFrontF1Width)
    {
      EditorGUILayout.LabelField("Width", GUILayout.Width(38f));
      int widthSelected = EditorGUILayout.IntPopup(
          frontF1WidthBefore,
          new[] { "160", "192", "224" },
          new[]
          {
            StraightF1WallLogic.CompositeWidth160,
            StraightF1WallLogic.CompositeWidth191,
            StraightF1WallLogic.CompositeWidth
          },
          GUILayout.Width(52f));

      widthSelected =
          StraightF1WallLogic.NormalizeFrontWallF1Width(widthSelected);

      if (widthSelected != frontF1WidthBefore)
      {
        previewFrontF1WidthOverrideByPiece[piece] = widthSelected;
        previewFrontF1WidthChangedThisFrame = true;
        RefreshTemporaryNormalWallPreview();
      }

      GUILayout.Space(4f);
    }
    float savedXyLabelWidth = EditorGUIUtility.labelWidth;
    EditorGUIUtility.labelWidth =
        EditorStyles.label.CalcSize(new GUIContent("X")).x;

    bool normalWallPositionPreview = IsNormalWallPiece(piece);
    int editX = piece.X;
    int editUnityY = piece.Y;
    if (normalWallPositionPreview
        && TryGetResolvedNormalWallState(piece, out ResolvedNormalWallState xyState))
    {
      editX = xyState.X;
      editUnityY = xyState.Y;
    }
    if (normalWallPositionPreview
        && previewPositionOverrideByPiece.TryGetValue(piece, out Vector2Int previewPosition))
    {
      editX = previewPosition.x;
      editUnityY = previewPosition.y;
    }

    int xBefore = editX;
    bool hasCanonicalRef = TryGetPieceCardReferenceXY(
        piece, mirrorAfter, out canonicalRefX, out canonicalRefY);
    int pieceHeightForY = GetPieceHeightForEditorY(piece);
    int displayYForRef = UnityYToDisplayY(editUnityY, pieceHeightForY);
    bool xChanged = DrawIntStepperInline(
        "X",
        ref editX,
        snap,
        hasCanonicalRef && editX != canonicalRefX,
        IsShowAllWallsPreview());
    if (xChanged && editX != xBefore)
    {
      SelectPiece(index);
      if (normalWallPositionPreview)
      {
        previewPositionOverrideByPiece[piece] = new Vector2Int(editX, editUnityY);

        previewPositionChangedThisFrame = true;
        RefreshTemporaryNormalWallPreview();
      }
      else
      {
        piece.X = editX;
        changed = true;
      }
    }

    EditorGUIUtility.labelWidth =
        EditorStyles.label.CalcSize(new GUIContent("Y")).x;
    int yBefore = editUnityY;
    bool yChanged = DrawTopDownYStepperInline(
        ref editUnityY,
        pieceHeightForY,
        snap,
        hasCanonicalRef && displayYForRef != canonicalRefY,
        IsShowAllWallsPreview());
    if (yChanged && editUnityY != yBefore)
    {
      SelectPiece(index);
      if (normalWallPositionPreview)
      {
        previewPositionOverrideByPiece[piece] = new Vector2Int(editX, editUnityY);
        previewPositionChangedThisFrame = true;
        RefreshTemporaryNormalWallPreview();
      }
      else
      {
        piece.Y = editUnityY;
        changed = true;
      }
    }
    EditorGUIUtility.labelWidth = savedXyLabelWidth;

    if (compactSideWallHeader
        || compactFrontWallHeader
        || compactBlackDoorFrontHeader
        || compactBlackDoorF1FrameHeader)
    {
      string refLabel = hasCanonicalRef
          ? $"Ref X {canonicalRefX} / Y {canonicalRefY}"
          : "Ref X - / Y -";
      GUILayout.Space(10f);
      EditorGUILayout.LabelField(
          refLabel,
          GUILayout.Width(125f),
          GUILayout.ExpandWidth(false));
    }

    EditorGUILayout.EndHorizontal();

    if (piece.Name == "BlackDoorF1")
    {
      bool showBlackDoorF2EditorCards =
          previewX == 1 && previewY == 4 && previewFacing == DungeonFacing.North;
      bool showBlackDoorF3EditorCards =
          previewX == 1 && previewY == 5 && previewFacing == DungeonFacing.North;

      if (showBlackDoorF2EditorCards)
      {
        if (!blackDoorF2CardInitialized)
        {
          blackDoorF2CardEnabled = false;
          blackDoorF2CardMirror = piece.MirrorHorizontally;
          blackDoorF2CardGraphic = DungeonGraphicType.BlackDoor;
          blackDoorF2CardInitialized = true;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("BlackDoorF2", EditorStyles.label);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.TextField("Name", "BlackDoorF2");

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        float f2PreviousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 55f;
        blackDoorF2CardEnabled = DrawMouseOnlyToggle(
            "Enabled",
            blackDoorF2CardEnabled,
            blackDoorF2CardEnabled,
            GUILayout.Width(72));
        GUILayout.FlexibleSpace();
        const string F2MirrorLabel = "Mirror Horizontally";
        float f2MirrorLabelWidth =
            EditorStyles.label.CalcSize(new GUIContent(F2MirrorLabel)).x;
        EditorGUIUtility.labelWidth = f2MirrorLabelWidth;
        blackDoorF2CardMirror = DrawMouseOnlyToggle(
            F2MirrorLabel,
            blackDoorF2CardMirror,
            blackDoorF2CardEnabled,
            GUILayout.Width(f2MirrorLabelWidth + 18f),
            GUILayout.ExpandWidth(false));
        EditorGUIUtility.labelWidth = f2PreviousLabelWidth;
        EditorGUILayout.EndHorizontal();

        blackDoorF2CardGraphic =
            (DungeonGraphicType)EditorGUILayout.EnumPopup(
                "Graphic",
                blackDoorF2CardGraphic);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Size");
        using (new EditorGUI.DisabledScope(true))
        {
          EditorGUILayout.IntField(63, GUILayout.Width(50));
          EditorGUILayout.LabelField("x", GUILayout.Width(12));
          EditorGUILayout.IntField(60, GUILayout.Width(50));
        }
        EditorGUILayout.EndHorizontal();

        int f2X = piece.ResolvedBlackDoorF2X;
        int f2Y = piece.ResolvedBlackDoorF2Y;
        int f2XBefore = f2X;
        int f2YBefore = f2Y;
        DrawIntStepper("X", ref f2X, snap);
        DrawIntStepper("Y", ref f2Y, snap);
        if (f2X != f2XBefore || f2Y != f2YBefore)
        {
          piece.BlackDoorF2X = f2X;
          piece.BlackDoorF2Y = f2Y;
          changed = true;
        }

        EditorGUILayout.EndVertical();
      }

      if (showBlackDoorF3EditorCards)
      {
        if (!blackDoorF3CardInitialized)
        {
          blackDoorF3CardEnabled = false;
          blackDoorF3CardMirror = piece.MirrorHorizontally;
          blackDoorF3CardGraphic = DungeonGraphicType.BlackDoor;
          blackDoorF3CardInitialized = true;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("BlackDoorF3", EditorStyles.label);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.TextField("Name", "BlackDoorF3");

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        float f3PreviousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 55f;
        blackDoorF3CardEnabled = DrawMouseOnlyToggle(
            "Enabled",
            blackDoorF3CardEnabled,
            blackDoorF3CardEnabled,
            GUILayout.Width(72));
        GUILayout.FlexibleSpace();
        const string F3MirrorLabel = "Mirror Horizontally";
        float f3MirrorLabelWidth =
            EditorStyles.label.CalcSize(new GUIContent(F3MirrorLabel)).x;
        EditorGUIUtility.labelWidth = f3MirrorLabelWidth;
        blackDoorF3CardMirror = DrawMouseOnlyToggle(
            F3MirrorLabel,
            blackDoorF3CardMirror,
            blackDoorF3CardEnabled,
            GUILayout.Width(f3MirrorLabelWidth + 18f),
            GUILayout.ExpandWidth(false));
        EditorGUIUtility.labelWidth = f3PreviousLabelWidth;
        EditorGUILayout.EndHorizontal();

        blackDoorF3CardGraphic =
            (DungeonGraphicType)EditorGUILayout.EnumPopup(
                "Graphic",
                blackDoorF3CardGraphic);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Size");
        using (new EditorGUI.DisabledScope(true))
        {
          EditorGUILayout.IntField(45, GUILayout.Width(50));
          EditorGUILayout.LabelField("x", GUILayout.Width(12));
          EditorGUILayout.IntField(39, GUILayout.Width(50));
        }
        EditorGUILayout.EndHorizontal();

        ViewportPiece doorF3 = FindLayoutPieceByName("BlackDoorF3");
        if (doorF3 == null)
        {
          doorF3 = EnsureBlackDoorF3Piece();
          changed = true;
        }
        int f3X = doorF3 != null ? doorF3.X : blackDoorF3CardX;
        int f3Y = doorF3 != null ? doorF3.Y : blackDoorF3CardY;
        int f3XBefore = f3X;
        int f3YBefore = f3Y;
        DrawIntStepper("X", ref f3X, snap);
        DrawIntStepper("Y", ref f3Y, snap);
        blackDoorF3CardX = f3X;
        blackDoorF3CardY = f3Y;
        if (doorF3 != null && (f3X != f3XBefore || f3Y != f3YBefore))
        {
          doorF3.X = f3X;
          doorF3.Y = f3Y;
          changed = true;
        }

        EditorGUILayout.EndVertical();
      }
    }
    EditorGUILayout.EndVertical();
  }

  private void DrawBlackDoorFrameF3EditorCard(
      string name,
      ref bool initialized,
      ref bool enabled,
      ref bool mirror,
      ref int x,
      ref int y,
      bool defaultMirror)
  {
    if (!initialized)
    {
      enabled = false;
      mirror = defaultMirror;
      initialized = true;
    }

    EditorGUILayout.Space(4f);
    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField(name, EditorStyles.label);
    EditorGUILayout.EndHorizontal();

    EditorGUILayout.TextField("Name", name);

    EditorGUILayout.BeginHorizontal();
    GUILayout.FlexibleSpace();
    float previousLabelWidth = EditorGUIUtility.labelWidth;
    EditorGUIUtility.labelWidth = 55f;
    enabled = DrawMouseOnlyToggle(
        "Enabled",
        enabled,
        enabled,
        GUILayout.Width(72));
    GUILayout.FlexibleSpace();
    const string MirrorLabel = "Mirror Horizontally";
    float mirrorLabelWidth =
        EditorStyles.label.CalcSize(new GUIContent(MirrorLabel)).x;
    EditorGUIUtility.labelWidth = mirrorLabelWidth;
    mirror = DrawMouseOnlyToggle(
        MirrorLabel,
        mirror,
        enabled,
        GUILayout.Width(mirrorLabelWidth + 18f),
        GUILayout.ExpandWidth(false));
    EditorGUIUtility.labelWidth = previousLabelWidth;
    EditorGUILayout.EndHorizontal();

    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.PrefixLabel("Size");
    using (new EditorGUI.DisabledScope(true))
    {
      EditorGUILayout.IntField(10, GUILayout.Width(50));
      EditorGUILayout.LabelField("x", GUILayout.Width(12));
      EditorGUILayout.IntField(42, GUILayout.Width(50));
    }
    EditorGUILayout.EndHorizontal();

    DrawIntStepper("X", ref x, snap);
    DrawIntStepper("Y", ref y, snap);

    EditorGUILayout.EndVertical();
  }

  private ViewportPiece FindLayoutPieceByName(string name)
  {
    if (layout == null || layout.Pieces == null || string.IsNullOrEmpty(name))
      return null;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece != null && piece.Name == name)
        return piece;
    }

    return null;
  }

  /// <summary>
  /// ViewEdit list entries only. Separate from LeftF2 / RightF2 / LeftD3 / RightD3.
  /// No geometry or render rules yet.
  /// </summary>
  private void EnsureLeft2SAndRight2SPieces()
  {
    EnsureNamedWallListPiece("LeftS2", "LeftF2");
    EnsureNamedWallListPiece("Right2S", "RightF2");
  }

  private ViewportPiece EnsureNamedWallListPiece(
      string name,
      string insertAfterName)
  {
    ViewportPiece existing = FindLayoutPieceByName(name);
    if (existing != null)
    {
      if (name == "LeftS2"
          && existing.Graphic == DungeonGraphicType.None)
      {
        existing.Graphic = DungeonGraphicType.Left2S;
      }

      return existing;
    }

    if (layout == null || layout.Pieces == null)
      return null;

    int insertAt = layout.Pieces.Count;
    if (!string.IsNullOrEmpty(insertAfterName))
    {
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece != null && piece.Name == insertAfterName)
        {
          insertAt = i + 1;
          break;
        }
      }
    }

    ViewportPiece created = new ViewportPiece
    {
      Name = name,
      Graphic = name == "LeftS2"
          ? DungeonGraphicType.Left2S
          : DungeonGraphicType.None,
      X = 0,
      Y = 0,
      Enabled = false,
      MirrorHorizontally = false
    };
    layout.Pieces.Insert(insertAt, created);
    return created;
  }

  /// <summary>
  /// Persistent Left F3 frame X/Y live on this layout piece, same as Left F2.
  /// Hidden from the piece list; nested card edits piece.X / piece.Y.
  /// </summary>
  private ViewportPiece EnsureBlackDoorFrameLeftF3Piece()
  {
    ViewportPiece existing = FindLayoutPieceByName("Black Door Frame Left F3");
    if (existing != null)
      return existing;

    if (layout == null || layout.Pieces == null)
      return null;

    int insertAt = layout.Pieces.Count;
    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece != null && piece.Name == "Black Door Frame Left F2")
      {
        insertAt = i + 1;
        break;
      }
    }

    ViewportPiece created = new ViewportPiece
    {
      Name = "Black Door Frame Left F3",
      Graphic = DungeonGraphicType.None,
      X = blackDoorFrameLeftF3CardX,
      Y = blackDoorFrameLeftF3CardY,
      Enabled = false,
      MirrorHorizontally = false
    };
    layout.Pieces.Insert(insertAt, created);
    return created;
  }

  /// <summary>
  /// Persistent Right F3 frame X/Y live on this layout piece, same as Left F3.
  /// Hidden from the piece list; nested card edits piece.X / piece.Y.
  /// </summary>
  private ViewportPiece EnsureBlackDoorFrameRightF3Piece()
  {
    ViewportPiece existing = FindLayoutPieceByName("Black Door Frame Right F3");
    if (existing != null)
      return existing;

    if (layout == null || layout.Pieces == null)
      return null;

    int insertAt = layout.Pieces.Count;
    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece != null && piece.Name == "Black Door Frame Right F2")
      {
        insertAt = i + 1;
        break;
      }
    }

    ViewportPiece created = new ViewportPiece
    {
      Name = "Black Door Frame Right F3",
      Graphic = DungeonGraphicType.None,
      X = blackDoorFrameRightF3CardX,
      Y = blackDoorFrameRightF3CardY,
      Enabled = false,
      MirrorHorizontally = false
    };
    layout.Pieces.Insert(insertAt, created);
    return created;
  }

  /// <summary>
  /// Persistent BlackDoorF3 X/Y live on this layout piece, same as F3 frames.
  /// Hidden from the piece list; nested card edits piece.X / piece.Y.
  /// </summary>
  private ViewportPiece EnsureBlackDoorF3Piece()
  {
    ViewportPiece existing = FindLayoutPieceByName("BlackDoorF3");
    if (existing != null)
      return existing;

    if (layout == null || layout.Pieces == null)
      return null;

    int insertAt = layout.Pieces.Count;
    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece != null && piece.Name == "BlackDoorF1")
      {
        insertAt = i + 1;
        break;
      }
    }

    ViewportPiece created = new ViewportPiece
    {
      Name = "BlackDoorF3",
      Graphic = DungeonGraphicType.None,
      X = blackDoorF3CardX,
      Y = blackDoorF3CardY,
      Enabled = false,
      MirrorHorizontally = false
    };
    layout.Pieces.Insert(insertAt, created);
    return created;
  }
  private static bool TryGetPieceFamilyLabelColor(
      ViewportPiece piece,
      out Color color)
  {
    color = default;
    if (piece == null || string.IsNullOrEmpty(piece.Name))
      return false;

    switch (piece.Name)
    {
      case "Left0":
      case "LeftF0":
      case "Wall F0Left":
        color = new Color32(0x9D, 0xCA, 0xFF, 0xFF);
        return true;
      case "Left1":
      case "LeftF1":
      case "Wall F1Left":
        color = new Color32(0x7F, 0xD3, 0xFF, 0xFF);
        return true;
      case "Left2":
      case "LeftF2":
      case "Wall F2Left":
        color = new Color32(0x2F, 0xA8, 0xFF, 0xFF);
        return true;
      case "Left3":
      case "LeftF3":
      case "Wall F3Left":
        color = new Color32(0x17, 0x6A, 0xA5, 0xFF);
        return true;
      case "Front1":
      case "FrontF1":
      case "Front Wall F1":
        color = new Color32(0x79, 0xD9, 0x96, 0xFF);
        return true;
      case "Front2":
      case "FrontF2":
      case "Front Wall F2":
        color = new Color32(0x4F, 0xB8, 0x74, 0xFF);
        return true;
      case "Front3":
      case "FrontF3":
      case "Front Wall F3":
        color = new Color32(0x33, 0x89, 0x5A, 0xFF);
        return true;
      case "Right0":
      case "RightF0":
      case "Wall F0Right":
        color = new Color32(0xFF, 0xD1, 0xA1, 0xFF);
        return true;
      case "Right1":
      case "RightF1":
      case "Wall F1Right":
        color = new Color32(0xFF, 0xB8, 0x70, 0xFF);
        return true;
      case "Right2":
      case "RightF2":
      case "Wall F2Right":
        color = new Color32(0xE8, 0x95, 0x45, 0xFF);
        return true;
      case "Right3":
      case "RightF3":
      case "Wall F3Right":
        color = new Color32(0xB9, 0x6A, 0x22, 0xFF);
        return true;
      case "LeftD3":
      case "Wall D3L2":
      case "RightD3":
      case "Wall D3R2":
        color = new Color32(0x9B, 0x6F, 0xD1, 0xFF);
        return true;
      case "LeftS2":
      case "Right2S":
        color = new Color32(0x9B, 0x6F, 0xD1, 0xFF);
        return true;
      case "BlackDoorF1":
      case "Black Door Frame Left F1":
      case "Black Door Frame Right F1":
        color = new Color32(0xFF, 0x66, 0xFF, 0xFF);
        return true;
      case "BlackDoorF2":
      case "Black Door Frame Left F2":
      case "Black Door Frame Right F2":
        color = new Color32(0xD8, 0x3F, 0xD8, 0xFF);
        return true;
      case "BlackDoorF3":
      case "Black Door Frame Left F3":
      case "Black Door Frame Right F3":
        color = new Color32(0x9C, 0x2B, 0x9C, 0xFF);
        return true;
      default:
        return false;
    }
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

  private void HandlePreviewFacingKeyboard()
  {
    Event current = Event.current;
    if (current.type != EventType.KeyDown)
      return;

    if (!s_viewEditGlobalNavDispatch && focusedWindow != this)
      return;

    // Delete/PageDown are reserved ViewEdit turn keys. Do not let a stale
    // TextField/DelayedIntField focus block direction changes.
    DungeonFacing nextFacing;
    KeyCode key = current.keyCode;
    switch (key)
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

    if (nextFacing != previewFacing)
    {
      // Preserve Preview X / Preview Y; only facing changes.
      SwitchPreviewPose(previewX, previewY, nextFacing);
      if (!s_viewEditGlobalNavDispatch)
        TryRefocusPreviewWindow();
    }

    current.Use();
    if (key == KeyCode.Delete)
      GUI.FocusControl(null);
  }

  private void HandlePreviewStrafeKeyboard()
  {
    Event current = Event.current;
    if (current.type != EventType.KeyDown)
      return;

    if (!s_viewEditGlobalNavDispatch && focusedWindow != this)
      return;

    if (EditorGUIUtility.editingTextField)
      return;

    int strafeSign;
    switch (current.keyCode)
    {
      case KeyCode.LeftArrow:
        strafeSign = -1; // left relative to facing
        break;
      case KeyCode.RightArrow:
        strafeSign = 1; // right relative to facing
        break;
      default:
        return;
    }

    current.Use();

    EnsurePreviewMiniMapLoaded();
    if (previewMiniMap == null)
      return;

    DungeonMap.GetRightOffset(
        previewFacing,
        out int rightX,
        out int rightY);

    int nextX = previewX + rightX * strafeSign;
    int nextY = previewY + rightY * strafeSign;

    if (!previewMiniMap.CanEnter(nextX, nextY))
    {
      PlayerWallBumpFeedback.ReportIfBlockedMove(strafeSign, 0);
      return;
    }

    // Keep Preview Facing unchanged.
    SwitchPreviewPose(nextX, nextY, previewFacing);
    if (!s_viewEditGlobalNavDispatch)
      TryRefocusPreviewWindow();
  }

  private void HandlePreviewMoveKeyboard()
  {
    Event current = Event.current;
    if (current.type != EventType.KeyDown)
      return;

    if (!s_viewEditGlobalNavDispatch && focusedWindow != this)
      return;

    if (EditorGUIUtility.editingTextField)
      return;

    int moveSign;
    switch (current.keyCode)
    {
      case KeyCode.UpArrow:
        moveSign = 1; // forward relative to facing
        break;
      case KeyCode.DownArrow:
        moveSign = -1; // backward relative to facing
        break;
      default:
        return;
    }

    current.Use();

    EnsurePreviewMiniMapLoaded();
    if (previewMiniMap == null)
      return;

    DungeonMap.GetForwardOffset(
        previewFacing,
        out int forwardX,
        out int forwardY);

    int nextX = previewX + forwardX * moveSign;
    int nextY = previewY + forwardY * moveSign;

    if (!previewMiniMap.CanEnter(nextX, nextY))
    {
      PlayerWallBumpFeedback.ReportIfBlockedMove(0, moveSign);
      return;
    }

    SwitchPreviewPose(nextX, nextY, previewFacing);
    if (!s_viewEditGlobalNavDispatch)
      TryRefocusPreviewWindow();
  }

  private void TryRefocusPreviewWindow()
  {
    GUI.FocusControl(null);
    Focus();
    EditorApplication.delayCall += RestoreViewEditKeyboardFocus;
  }

  /// <summary>
  /// One-shot: Game View repaint after preview refresh can steal EditorWindow
  /// focus. Restore ViewEdit unless a text/numeric field is being edited.
  /// </summary>
  private void RestoreViewEditKeyboardFocus()
  {
    if (this == null)
      return;

    if (EditorGUIUtility.editingTextField)
      return;

    Focus();
  }

  private static void RegisterViewEditGlobalNavigation()
  {
    s_viewEditGlobalNavOwners++;
    if (s_viewEditGlobalNavCallbackAdded)
      return;

    AddViewEditGlobalEventHandler(ViewEditGlobalNavHandler);
    AddViewEditBeforeEventProcessedHandler();
    s_viewEditGlobalNavCallbackAdded = true;
  }

  private static void UnregisterViewEditGlobalNavigation()
  {
    s_viewEditGlobalNavOwners--;
    if (s_viewEditGlobalNavOwners > 0)
      return;

    s_viewEditGlobalNavOwners = 0;
    if (!s_viewEditGlobalNavCallbackAdded)
      return;

    RemoveViewEditGlobalEventHandler(ViewEditGlobalNavHandler);
    RemoveViewEditBeforeEventProcessedHandler();
    s_viewEditGlobalNavCallbackAdded = false;
  }

  private static void AddViewEditGlobalEventHandler(
      EditorApplication.CallbackFunction handler)
  {
    EventInfo evt = typeof(EditorApplication).GetEvent(
        "globalEventHandler",
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    if (evt != null)
    {
      evt.GetAddMethod(true)?.Invoke(null, new object[] { handler });
      return;
    }

    FieldInfo field = typeof(EditorApplication).GetField(
        "globalEventHandler",
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    if (field == null)
      return;

    var value = (EditorApplication.CallbackFunction)field.GetValue(null);
    value -= handler;
    value += handler;
    field.SetValue(null, value);
  }

  private static void RemoveViewEditGlobalEventHandler(
      EditorApplication.CallbackFunction handler)
  {
    EventInfo evt = typeof(EditorApplication).GetEvent(
        "globalEventHandler",
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    if (evt != null)
    {
      evt.GetRemoveMethod(true)?.Invoke(null, new object[] { handler });
      return;
    }

    FieldInfo field = typeof(EditorApplication).GetField(
        "globalEventHandler",
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    if (field == null)
      return;

    var value = (EditorApplication.CallbackFunction)field.GetValue(null);
    value -= handler;
    field.SetValue(null, value);
  }

  private static EventInfo GetGuiViewBeforeEventProcessedEvent()
  {
    System.Type guiViewType = typeof(EditorWindow).Assembly.GetType(
        "UnityEditor.GUIView");
    if (guiViewType == null)
      return null;

    return guiViewType.GetEvent(
        "beforeEventProcessed",
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
  }

  private static void AddViewEditBeforeEventProcessedHandler()
  {
    EventInfo evt = GetGuiViewBeforeEventProcessedEvent();
    if (evt == null)
      return;

    MethodInfo method = typeof(ViewportLayoutEditor).GetMethod(
        nameof(HandleViewEditBeforeEventProcessed),
        BindingFlags.Static | BindingFlags.NonPublic);
    if (method == null)
      return;

    s_viewEditBeforeEventProcessedHandler = System.Delegate.CreateDelegate(
        evt.EventHandlerType,
        method,
        false);
    if (s_viewEditBeforeEventProcessedHandler == null)
      return;

    evt.GetAddMethod(true)?.Invoke(
        null,
        new object[] { s_viewEditBeforeEventProcessedHandler });
  }

  private static void RemoveViewEditBeforeEventProcessedHandler()
  {
    EventInfo evt = GetGuiViewBeforeEventProcessedEvent();
    if (evt == null || s_viewEditBeforeEventProcessedHandler == null)
      return;

    evt.GetRemoveMethod(true)?.Invoke(
        null,
        new object[] { s_viewEditBeforeEventProcessedHandler });
    s_viewEditBeforeEventProcessedHandler = null;
  }

  private static void HandleViewEditBeforeEventProcessed(
      EventType type,
      KeyCode keyCode,
      EventModifiers modifiers)
  {
    if (type != EventType.KeyDown)
      return;

    if (!IsViewEditNavigationKey(keyCode))
      return;

    TryDispatchViewEditGlobalNavigation();
  }

  private static void HandleViewEditGlobalNavigationEvent()
  {
    TryDispatchViewEditGlobalNavigation();
  }

  private static bool IsViewEditNavigationKey(KeyCode keyCode)
  {
    switch (keyCode)
    {
      case KeyCode.UpArrow:
      case KeyCode.DownArrow:
      case KeyCode.LeftArrow:
      case KeyCode.RightArrow:
      case KeyCode.Delete:
      case KeyCode.PageDown:
        return true;
      default:
        return false;
    }
  }

  private static bool IsEditorTextOrNumericInputActive()
  {
    if (EditorGUIUtility.editingTextField)
      return true;

    EditorWindow focused = focusedWindow;
    if (focused == null)
      return false;

    UnityEngine.UIElements.VisualElement root = focused.rootVisualElement;
    if (root == null)
      return false;

    UnityEngine.UIElements.Focusable focusedElement =
        root.focusController != null
            ? root.focusController.focusedElement
            : null;
    return IsUiToolkitTextOrNumericInput(focusedElement);
  }

  private static bool IsUiToolkitTextOrNumericInput(
      UnityEngine.UIElements.Focusable focused)
  {
    if (focused == null)
      return false;

    for (System.Type type = focused.GetType(); type != null; type = type.BaseType)
    {
      System.Type check = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
      string name = check.Name;
      if (name == "TextInputBaseField`1"
          || name == "TextField"
          || name == "SearchFieldBase`1"
          || name == "ToolbarSearchField")
      {
        return true;
      }
    }

    return false;
  }

  private static void TryDispatchViewEditGlobalNavigation()
  {
    Event current = Event.current;
    if (current == null || current.type != EventType.KeyDown)
      return;

    if (!IsViewEditNavigationKey(current.keyCode))
      return;

    if (Application.isPlaying)
      return;

    // Arrow movement should still respect active text/numeric input, but
    // Delete/PageDown are reserved for ViewEdit turning and must always pass.
    bool isFacingKey =
        current.keyCode == KeyCode.Delete
        || current.keyCode == KeyCode.PageDown;
    if (!isFacingKey && IsEditorTextOrNumericInputActive())
      return;

    ViewportLayoutEditor window = FindOpenViewEditWindow();
    if (window == null || window.layout == null)
      return;

    s_viewEditGlobalNavDispatch = true;
    try
    {
      window.HandlePreviewMoveKeyboard();
      window.HandlePreviewFacingKeyboard();
      window.HandlePreviewStrafeKeyboard();
    }
    finally
    {
      s_viewEditGlobalNavDispatch = false;
    }
  }

  private static ViewportLayoutEditor FindOpenViewEditWindow()
  {
    ViewportLayoutEditor[] windows =
        Resources.FindObjectsOfTypeAll<ViewportLayoutEditor>();
    if (windows == null)
      return null;

    for (int i = 0; i < windows.Length; i++)
    {
      ViewportLayoutEditor window = windows[i];
      if (window != null)
        return window;
    }

    return null;
  }

  /// <summary>
  /// Recompose for the current pose and push that Texture2D onto the live
  /// Game View dungeon RawImage. Always re-finds the RawImage (no cache).
  /// </summary>
  private void PresentEditModePreviewToGameView()
  {
    if (Application.isPlaying || layout == null || graphics == null)
      return;

    RawImage dungeonImage = FindLiveDungeonViewportRawImage();
    if (dungeonImage == null)
    {
      Debug.LogWarning(
          "PresentEditModePreviewToGameView: DungeonViewport RawImage not found.");
      return;
    }

    cachedViewportImage = dungeonImage;

    EnsureEditModePreviewTexture();
    if (editModePreviewTexture == null)
      return;

    ComposeEditModePreview();
    if (editModePreviewTexture == null)
      return;

    editModePreviewTexture.Apply(false);

    StealViewportTextureIfNeeded(dungeonImage);
    ApplyExact320x200EditModePresentation(dungeonImage);

    // Presentation may rebuild hierarchy — resolve the live RawImage again.
    dungeonImage = FindLiveDungeonViewportRawImage();
    if (dungeonImage == null)
      return;

    cachedViewportImage = dungeonImage;

    // Force a reference change so RawImage/Canvas pick up in-place pixel updates.
    // Avoid uvRect / SetAllDirty here — those hit NRE when canvas is unset.
    dungeonImage.texture = Texture2D.whiteTexture;
    dungeonImage.texture = editModePreviewTexture;

    if (dungeonImage.canvas != null)
      Canvas.ForceUpdateCanvases();

    MaintainMovementArrowsPreview();
    RepaintGameViews();
  }

  /// <summary>
  /// Fresh lookup of the RawImage actually shown in Game View (by name).
  /// Does not use cachedViewportImage.
  /// </summary>
  private RawImage FindLiveDungeonViewportRawImage()
  {
    RawImage[] images = Object.FindObjectsByType<RawImage>(
        FindObjectsInactive.Exclude);

    foreach (RawImage image in images)
    {
      if (image == null)
        continue;

      if (image.gameObject.name == "DungeonViewport")
        return image;
    }

    // Prefer the RawImage currently displaying our preview texture.
    foreach (RawImage image in images)
    {
      if (image == null)
        continue;

      if (editModePreviewTexture != null
          && image.texture == editModePreviewTexture)
      {
        return image;
      }
    }

    foreach (RawImage image in images)
    {
      if (image == null)
        continue;

      if (image.texture is RenderTexture)
        return image;
    }

    return null;
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

  private void RestorePersistedAssets()
  {
    string savedLayoutGuid =
        EditorPrefs.GetString(PrefsLayoutGuidKey, string.Empty);

    layout = LoadViewportLayoutByGuid(savedLayoutGuid);

    if (layout == null)
    {
      layout = AssetDatabase.LoadAssetAtPath<ViewportLayout>(
          DefaultViewportLayoutPath);
    }

    if (layout == null)
      layout = FindSingleViewportLayoutAsset();

    if (layout != null)
      SaveAssetGuid(PrefsLayoutGuidKey, layout);

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
    EnsurePreviewMiniMapLoaded();

    bool hasSavedPose =
        EditorPrefs.HasKey(PrefsPreviewXKey)
        && EditorPrefs.HasKey(PrefsPreviewYKey)
        && EditorPrefs.HasKey(PrefsPreviewFacingKey);

    if (hasSavedPose)
    {
      previewX = EditorPrefs.GetInt(PrefsPreviewXKey, 0);
      previewY = EditorPrefs.GetInt(PrefsPreviewYKey, 0);
      int facingInt = EditorPrefs.GetInt(
          PrefsPreviewFacingKey,
          (int)DungeonFacing.South);
      previewFacing = facingInt >= (int)DungeonFacing.North
          && facingInt <= (int)DungeonFacing.West
          ? (DungeonFacing)facingInt
          : DungeonFacing.South;

      if (previewMiniMap != null && !previewMiniMap.CanEnter(previewX, previewY))
      {
        previewX = previewMiniMap.StartX;
        previewY = previewMiniMap.StartY;
        previewFacing = previewMiniMap.StartFacing;
      }
    }
    else if (previewMiniMap != null)
    {
      previewX = previewMiniMap.StartX;
      previewY = previewMiniMap.StartY;
      previewFacing = previewMiniMap.StartFacing;
    }

    selectedPieceIndex = EditorPrefs.GetInt(PrefsSelectedPieceIndexKey, 0);
    ClampSelectedPieceIndex();
    LoadFrontF1CropPreviewForCurrentPose();
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

    GUILayout.Space(8f);
    if (GUILayout.Button("Show Walls Activ", GUILayout.Width(120f)))
      showWallsActivFilter = !showWallsActivFilter;
    if (GUILayout.Button("Disable Walls", GUILayout.Width(100f)))
    {
      DisableWallsKeepChrome();
      RefreshEditModePreview();
      RepaintGameViews();
      Repaint();
    }

    EditorGUILayout.EndHorizontal();
  }

  private bool TryGetViewportRawImage(out RawImage viewportImage)
  {
    if (cachedViewportImage == null)
      cachedViewportImage = FindViewportRawImage();

    // Unity fake-null: drop a destroyed cached reference.
    if (cachedViewportImage == null)
    {
      viewportImage = null;
      return false;
    }

    viewportImage = cachedViewportImage;
    return true;
  }

  private RawImage FindViewportRawImage()
  {
    RawImage[] images = Object.FindObjectsByType<RawImage>(
        FindObjectsInactive.Exclude);

    foreach (RawImage image in images)
    {
      if (image == null)
        continue;

      if (image.gameObject.name == "DungeonViewport")
        return image;
    }

    foreach (RawImage image in images)
    {
      if (image == null)
        continue;

      if (image.texture is RenderTexture)
        return image;

      // After the first steal, the dungeon RawImage holds our preview Texture2D
      // (not a RenderTexture) — still treat it as the viewport target.
      if (editModePreviewTexture != null
          && image.texture == editModePreviewTexture)
      {
        return image;
      }
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
    if (showGeometryDiagnostics)
      DrawRelativeViewportGeometryDebug();

    DrawPreviewMiniMap();
  }

  private void DrawRelativeViewportGeometryDebug()
  {
    EnsurePreviewMiniMapLoaded();
    if (previewMiniMap == null)
      return;

    RelativeViewportGeometry geometry =
        RelativeViewportGeometry.Calculate(
            previewMiniMap,
            previewX,
            previewY,
            previewFacing);

    HashSet<string> drawPieces = new HashSet<string>();
    if (layout != null && layout.Pieces != null)
    {
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null)
          continue;

        // BlackDoorF1 is a special editor piece and does not use the normal-wall
        // resolver. Include it in Diagnostics when its dedicated needed-pose
        // rule says it belongs to the current view.
        if (piece.Name == "BlackDoorF1" && IsWallNeededForCurrentPose(piece))
        {
          drawPieces.Add("BlackDoorF1");
          continue;
        }

        if (!TryGetResolvedNormalWallState(
                piece, out ResolvedNormalWallState state)
            || !state.Enabled)
        {
          continue;
        }

        string name = null;
        if (!TryGetSideWallCanonicalName(piece, out name))
        {
          if (IsFrontWallF1Card(piece)) name = "FrontF1";
          else if (IsFrontWallF2Card(piece)) name = "FrontF2";
          else if (IsFrontWallF3Card(piece)) name = "FrontF3";
          else name = piece.Name ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(name))
          drawPieces.Add(name);
      }
    }

    string text =
        "GEOMETRY DIAGNOSTIC  "
        + previewX + "," + previewY + " " + previewFacing + "\n"
        + "F0: L=" + FormatRelativeViewportCellShort(geometry.F0Left)
        + "  R=" + FormatRelativeViewportCellShort(geometry.F0Right)
        + "\nF1: L=" + FormatRelativeViewportCellShort(geometry.F1Left)
        + "  C=" + FormatRelativeViewportCellShort(geometry.F1Center)
        + "  R=" + FormatRelativeViewportCellShort(geometry.F1Right)
        + "\nF2: L=" + FormatRelativeViewportCellShort(geometry.F2Left)
        + "  C=" + FormatRelativeViewportCellShort(geometry.F2Center)
        + "  R=" + FormatRelativeViewportCellShort(geometry.F2Right)
        + "\nF3: L=" + FormatRelativeViewportCellShort(geometry.F3Left)
        + "  C=" + FormatRelativeViewportCellShort(geometry.F3Center)
        + "  R=" + FormatRelativeViewportCellShort(geometry.F3Right)
        + "\n\nDRAW: "
        + (drawPieces.Count > 0
            ? string.Join(", ", drawPieces)
            : "none");

    EditorGUILayout.HelpBox(text, MessageType.None);
    if (Event.current.type == EventType.Repaint)
      geometryDiagnosticRect = GUILayoutUtility.GetLastRect();
  }

  private static string FormatRelativeViewportCell(RelativeViewportCell cell)
  {
    return cell.IsInside
        ? cell.Type + " (" + cell.X + "," + cell.Y + ")"
        : "OUT (" + cell.X + "," + cell.Y + ")";
  }

  private static string FormatRelativeViewportCellShort(RelativeViewportCell cell)
  {
    if (!cell.IsInside)
      return "X";

    if (IsViewEditGeometryWall(cell))
      return "W";

    string type = cell.Type.ToString();
    if (type.IndexOf("STONE", System.StringComparison.OrdinalIgnoreCase) >= 0
        || type.IndexOf("WALL", System.StringComparison.OrdinalIgnoreCase) >= 0)
      return "W";

    if (type.IndexOf("DOOR", System.StringComparison.OrdinalIgnoreCase) >= 0)
      return "D";

    if (type.IndexOf("PIT", System.StringComparison.OrdinalIgnoreCase) >= 0)
      return "P";

    if (type.IndexOf("STAIR", System.StringComparison.OrdinalIgnoreCase) >= 0)
      return "S";

    if (type.IndexOf("TELE", System.StringComparison.OrdinalIgnoreCase) >= 0)
      return "T";

    if (type.IndexOf("FALSE", System.StringComparison.OrdinalIgnoreCase) >= 0)
      return "F";

    return "O";
  }

  private string BuildViewportGeometryOverlayText()
  {
    EnsurePreviewMiniMapLoaded();
    if (previewMiniMap == null)
      return "GEOM: NO MAP";

    RelativeViewportGeometry g =
        RelativeViewportGeometry.Calculate(
            previewMiniMap,
            previewX,
            previewY,
            previewFacing);

    return "F0 L" + FormatRelativeViewportCellShort(g.F0Left)
        + " R" + FormatRelativeViewportCellShort(g.F0Right)
        + "\nF1 L" + FormatRelativeViewportCellShort(g.F1Left)
        + " C" + FormatRelativeViewportCellShort(g.F1Center)
        + " R" + FormatRelativeViewportCellShort(g.F1Right)
        + "\nF2 L" + FormatRelativeViewportCellShort(g.F2Left)
        + " C" + FormatRelativeViewportCellShort(g.F2Center)
        + " R" + FormatRelativeViewportCellShort(g.F2Right)
        + "\nF3 L" + FormatRelativeViewportCellShort(g.F3Left)
        + " C" + FormatRelativeViewportCellShort(g.F3Center)
        + " R" + FormatRelativeViewportCellShort(g.F3Right);
  }

  private string BuildDeterministicWallDiagnostic()
  {
    EnsurePreviewMiniMapLoaded();
    if (previewMiniMap == null)
      return string.IsNullOrEmpty(previewMiniMapLoadError)
          ? "Could not load Hall of Champions map."
          : previewMiniMapLoadError;

    RelativeViewportGeometry geometry =
        RelativeViewportGeometry.Calculate(
            previewMiniMap,
            previewX,
            previewY,
            previewFacing);

    System.Text.StringBuilder report = new System.Text.StringBuilder();
    report.Append("POSE ")
        .Append(previewX)
        .Append(",")
        .Append(previewY)
        .Append(" ")
        .Append(previewFacing)
        .Append("\nSOURCE ")
        .Append(HallOfChampionsMapPath)
        .Append("\n\nMAP GEOMETRY\n");

    report.Append("F0L=").Append(FormatRelativeViewportCell(geometry.F0Left))
        .Append("   F0R=").Append(FormatRelativeViewportCell(geometry.F0Right))
        .Append("\nF1L=").Append(FormatRelativeViewportCell(geometry.F1Left))
        .Append("   F1C=").Append(FormatRelativeViewportCell(geometry.F1Center))
        .Append("   F1R=").Append(FormatRelativeViewportCell(geometry.F1Right))
        .Append("\nF2L=").Append(FormatRelativeViewportCell(geometry.F2Left))
        .Append("   F2C=").Append(FormatRelativeViewportCell(geometry.F2Center))
        .Append("   F2R=").Append(FormatRelativeViewportCell(geometry.F2Right))
        .Append("\nF3L=").Append(FormatRelativeViewportCell(geometry.F3Left))
        .Append("   F3C=").Append(FormatRelativeViewportCell(geometry.F3Center))
        .Append("   F3R=").Append(FormatRelativeViewportCell(geometry.F3Right));

    report.Append("\n\nFRONT F2 RULE\n");

    DungeonMap.GetForwardOffset(
        previewFacing,
        out int forwardX,
        out int forwardY);
    DungeonMap.GetRightOffset(
        previewFacing,
        out int rightX,
        out int rightY);

    int front1X = previewX + forwardX;
    int front1Y = previewY + forwardY;
    bool front1Blocked = previewMiniMap == null
        || !previewMiniMap.IsInside(front1X, front1Y)
        || previewMiniMap.GetTile(front1X, front1Y).Type == DungeonTileType.Wall;

    bool centerF2Wall = false;
    if (!front1Blocked)
    {
      int centerF2X = previewX + forwardX * 2;
      int centerF2Y = previewY + forwardY * 2;
      centerF2Wall = previewMiniMap == null
          || !previewMiniMap.IsInside(centerF2X, centerF2Y)
          || previewMiniMap.GetTile(centerF2X, centerF2Y).Type == DungeonTileType.Wall;
    }

    int left0X = previewX - rightX;
    int left0Y = previewY - rightY;
    int left1X = previewX + forwardX - rightX;
    int left1Y = previewY + forwardY - rightY;
    int left2X = previewX + forwardX * 2 - rightX;
    int left2Y = previewY + forwardY * 2 - rightY;

    bool left0Open = previewMiniMap != null
        && previewMiniMap.IsInside(left0X, left0Y)
        && previewMiniMap.GetTile(left0X, left0Y).Type != DungeonTileType.Wall;
    bool left1Open = previewMiniMap != null
        && previewMiniMap.IsInside(left1X, left1Y)
        && previewMiniMap.GetTile(left1X, left1Y).Type != DungeonTileType.Wall;
    bool left2IsWall = previewMiniMap == null
        || !previewMiniMap.IsInside(left2X, left2Y)
        || previewMiniMap.GetTile(left2X, left2Y).Type == DungeonTileType.Wall;

    bool centerF2 =
        !front1Blocked && centerF2Wall;
    bool leftExposedF2 =
        front1Blocked && left1Open && left2IsWall;
    bool frontF2Enabled = centerF2 || leftExposedF2;

    string frontF2Class = centerF2
        ? "Center"
        : leftExposedF2
            ? "LeftExposed"
            : "None";

    report.Append("front1Blocked=").Append(front1Blocked)
        .Append("\ncenterF2Wall=").Append(centerF2Wall)
        .Append("\nleft0Open=").Append(left0Open)
        .Append("  left1Open=").Append(left1Open)
        .Append("  left2IsWall=").Append(left2IsWall)
        .Append("\ncenterF2=").Append(centerF2)
        .Append("  leftExposedF2=").Append(leftExposedF2)
        .Append("\n=> FrontF2 Class=").Append(frontF2Class)
        .Append("\n=> FrontF2 Enabled=").Append(frontF2Enabled);

    ViewportPiece frontF2 = FindLayoutPieceByName("FrontF2");
    if (frontF2 == null)
      frontF2 = FindLayoutPieceByName("Front Wall F2");

    report.Append("\n\nCURRENT FRONT F2 PIECE\n");
    if (frontF2 == null)
    {
      report.Append("not found");
    }
    else
    {
      report.Append("Enabled=").Append(frontF2.Enabled)
          .Append("  Width=")
          .Append(FrontWallF2Logic.Normalize(frontF2.FrontWallF2Width))
          .Append("  X=").Append(frontF2.X)
          .Append("  Y=").Append(frontF2.Y)
          .Append("  Mirror=").Append(frontF2.MirrorHorizontally);
    }

    return report.ToString();
  }

  private void DrawPreviewMiniMap()
  {
    EditorGUILayout.Space();

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

    // Keep minimap arrow on the same previewFacing as viewport + Console.
    previewMiniMap.SetPlayerPose(previewX, previewY, previewFacing);

    EditorGUILayout.BeginHorizontal();

    EditorGUILayout.BeginVertical(GUILayout.Width(300f));
    previewMiniMapScroll = DungeonMiniMapGui.Draw(
        previewMiniMap,
        previewX,
        previewY,
        previewFacing,
        previewMiniMapScroll,
        interactive: true,
        out DungeonMiniMapGui.InteractionResult interaction
    );
    EditorGUILayout.EndVertical();


    // Keep the compact 3×2 movement pad directly beside the minimap.
    EditorGUILayout.BeginVertical(GUILayout.Width(96f));
    DrawPreviewNavigationPad();
    EditorGUILayout.EndVertical();

    EditorGUILayout.EndHorizontal();

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

  /// <summary>
  /// Compact 3×2 navigation pad (editor-only). Uses NavigatePreviewPoseOnly.
  /// </summary>
  private void DrawPreviewNavigationPad()
  {
    using (new EditorGUI.DisabledScope(Application.isPlaying || layout == null))
    {
      const float buttonWidth = 28f;
      const float buttonHeight = 22f;

      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button(
              "↶",
              GUILayout.Width(buttonWidth),
              GUILayout.Height(buttonHeight)))
      {
        PreviewNavigateTurnLeft();
        TryRefocusPreviewWindow();
      }

      if (GUILayout.Button(
              "↑",
              GUILayout.Width(buttonWidth),
              GUILayout.Height(buttonHeight)))
      {
        PreviewNavigateMoveForward();
        TryRefocusPreviewWindow();
      }

      if (GUILayout.Button(
              "↷",
              GUILayout.Width(buttonWidth),
              GUILayout.Height(buttonHeight)))
      {
        PreviewNavigateTurnRight();
        TryRefocusPreviewWindow();
      }

      EditorGUILayout.EndHorizontal();

      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button(
              "←",
              GUILayout.Width(buttonWidth),
              GUILayout.Height(buttonHeight)))
      {
        PreviewNavigateStrafeLeft();
        TryRefocusPreviewWindow();
      }

      if (GUILayout.Button(
              "↓",
              GUILayout.Width(buttonWidth),
              GUILayout.Height(buttonHeight)))
      {
        PreviewNavigateMoveBackward();
        TryRefocusPreviewWindow();
      }

      if (GUILayout.Button(
              "→",
              GUILayout.Width(buttonWidth),
              GUILayout.Height(buttonHeight)))
      {
        PreviewNavigateStrafeRight();
        TryRefocusPreviewWindow();
      }

      EditorGUILayout.EndHorizontal();
    }
  }

  private void ApplyPreviewPoseFromMiniMapClick(int x, int y)
  {
    // Preview Facing is unchanged.
    SwitchPreviewPose(x, y, previewFacing);
    TryRefocusPreviewWindow();
  }

  private void SwitchPreviewPose(int newX, int newY, DungeonFacing newFacing)
  {
    // During Play the Game View is owned by DungeonRenderer — do not let the
    // editor previewFacing / minimap drift away from a skipped Refresh/log.
    if (Application.isPlaying)
      return;

    if (newX == previewX && newY == previewY && newFacing == previewFacing)
      return;

    EnsurePreviewMiniMapLoaded();
    if (previewMiniMap != null && !previewMiniMap.CanEnter(newX, newY))
      return;


    // Any manual Mirror test belongs only to the pose we are leaving.
    // Restore the checkbox itself to the geometry result before changing pose,
    // then discard the temporary preview override.
    foreach (ViewportPiece overriddenPiece in previewMirrorOverrideByPiece.Keys)
    {
      if (overriddenPiece != null
          && resolvedNormalWallByPiece.TryGetValue(
              overriddenPiece, out ResolvedNormalWallState resolvedState))
        overriddenPiece.MirrorHorizontally = resolvedState.Mirror;
    }
    previewMirrorOverrideByPiece.Clear();
    previewFrontF1WidthOverrideByPiece.Clear();
    previewPositionOverrideByPiece.Clear();
    previewEnabledOverrideByPiece.Clear();
    previewGraphicOverrideByPiece.Clear();
    previewDisableAllWalls = false;

    previewX = newX;
    previewY = newY;
    previewFacing = newFacing;
    LoadFrontF1CropPreviewForCurrentPose();
    showWallsActivFilter = true;
    SaveSessionPrefs();
    PlayerWallBumpFeedback.ResetWallHitLog();

    if (previewMiniMap != null)
      previewMiniMap.SetPlayerPose(previewX, previewY, previewFacing);

    ApplyCurrentPoseVisibilityToLayout();

    // Full cache reset so Console must emit the new previewFacing.
    ResetEditModeViewportLogCache();
    RefreshEditModePreview();
    GUI.changed = true;
    Repaint();
  }

  /// <summary>
  /// Preview navigation only (editor 3×2 pad). Updates X/Y/Facing, applies the
  /// destination pose for display, refreshes preview/minimap. Does not capture
  /// or save pose visibility / layout assets.
  /// </summary>
  private void NavigatePreviewPoseOnly(int newX, int newY, DungeonFacing newFacing)
  {
    if (Application.isPlaying)
      return;

    if (newX == previewX && newY == previewY && newFacing == previewFacing)
      return;

    // Any manual Mirror test belongs only to the pose we are leaving.
    // Restore the checkbox itself to the geometry result before changing pose,
    // then discard the temporary preview override.
    foreach (ViewportPiece overriddenPiece in previewMirrorOverrideByPiece.Keys)
    {
      if (overriddenPiece != null
          && resolvedNormalWallByPiece.TryGetValue(
              overriddenPiece, out ResolvedNormalWallState resolvedState))
        overriddenPiece.MirrorHorizontally = resolvedState.Mirror;
    }
    previewMirrorOverrideByPiece.Clear();
    previewFrontF1WidthOverrideByPiece.Clear();
    previewPositionOverrideByPiece.Clear();
    previewEnabledOverrideByPiece.Clear();
    previewGraphicOverrideByPiece.Clear();
    previewDisableAllWalls = false;

    previewX = newX;
    previewY = newY;
    previewFacing = newFacing;
    LoadFrontF1CropPreviewForCurrentPose();
    showWallsActivFilter = true;
    SaveSessionPrefs();
    PlayerWallBumpFeedback.ResetWallHitLog();

    if (previewMiniMap != null)
      previewMiniMap.SetPlayerPose(previewX, previewY, previewFacing);

    ApplyPoseVisibilityForNavigationOnly();

    ResetEditModeViewportLogCache();
    RefreshEditModePreview();
    GUI.changed = true;
    Repaint();
  }

  /// <summary>
  /// Navigation preview keeps map/minimap geometry active, but deliberately
  /// renders no normal walls or Black Door pieces.
  /// </summary>
  private void ApplyPoseVisibilityForNavigationOnly()
  {
    if (layout == null)
      return;

    // No per-view/per-pose storage exists anymore.
    // Every preview pose starts from the same non-pose defaults, then applies
    // only deterministic runtime/editor rules.
    ApplyUnknownPoseDefaultsToLayout();
    EnsureChampionStatusSlotsEnabled();
    ApplyCeilingMirrorFromPose();
    ApplyFloorMirrorReferenceOverride();

    // CHATGPT_BUILD_F1_MINIMAP_ALGORITHM_STAGE1_20260830_AA
    // Stage 1: automatic F0/F1 wall assembly from minimap only.
    // F2/F3/D3/Black Door remain disabled until F1 is verified.
    ApplyF1MinimapWallRecipe();
  }

  private void PreviewNavigateTurnLeft()
  {
    NavigatePreviewPoseOnly(
        previewX,
        previewY,
        TurnPreviewFacingLeft(previewFacing));
  }

  private void PreviewNavigateTurnRight()
  {
    NavigatePreviewPoseOnly(
        previewX,
        previewY,
        TurnPreviewFacingRight(previewFacing));
  }

  private void PreviewNavigateMoveForward()
  {
    TryPreviewNavigateRelative(0, 1);
  }

  private void PreviewNavigateMoveBackward()
  {
    TryPreviewNavigateRelative(0, -1);
  }

  private void PreviewNavigateStrafeLeft()
  {
    TryPreviewNavigateRelative(-1, 0);
  }

  private void PreviewNavigateStrafeRight()
  {
    TryPreviewNavigateRelative(1, 0);
  }

  /// <summary>
  /// Facing-local move: +Y forward, -Y back, -X strafe left, +X strafe right.
  /// </summary>
  private void TryPreviewNavigateRelative(int localX, int localY)
  {
    EnsurePreviewMiniMapLoaded();
    if (previewMiniMap == null)
      return;

    DungeonMap.GetForwardOffset(
        previewFacing,
        out int forwardX,
        out int forwardY);
    DungeonMap.GetRightOffset(
        previewFacing,
        out int rightX,
        out int rightY);

    int nextX = previewX + forwardX * localY + rightX * localX;
    int nextY = previewY + forwardY * localY + rightY * localX;

    if (!previewMiniMap.CanEnter(nextX, nextY))
    {
      PlayerWallBumpFeedback.ReportIfBlockedMove(localX, localY);
      return;
    }

    NavigatePreviewPoseOnly(nextX, nextY, previewFacing);
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

  /// <summary>
  /// Disables every piece except Floor, Ceiling, Movement Arrows, and
  /// Champion Status Slot 1–4 (matched by ViewportPiece.Name).
  /// </summary>
  /// <summary>
  /// ViewEdit preview only. Floor, ceiling, arrows, and champion slots stay.
  /// Stored piece Enabled / X / Y / Graphic / Mirror are not changed.
  /// </summary>
  private void DisableWallsKeepChrome()
  {
    if (Application.isPlaying)
      return;

    previewDisableAllWalls = true;
  }

  private static bool IsDisableWallsKeeper(ViewportPiece piece)
  {
    if (piece == null || piece.Name == null)
      return false;

    return piece.Name == "Floor"
        || piece.Name == "Ceiling"
        || piece.Name == "Movement Arrows"
        || piece.Name == "Champion Status Slot 1"
        || piece.Name == "Champion Status Slot 2"
        || piece.Name == "Champion Status Slot 3"
        || piece.Name == "Champion Status Slot 4";
  }

  private static bool IsFloorOrCeiling(ViewportPiece piece)
  {
    return piece.Graphic == DungeonGraphicType.Floor
        || piece.Graphic == DungeonGraphicType.Ceiling
        || piece.Name == "Floor"
        || piece.Name == "Ceiling";
  }

  private static bool IsChampionStatusSlotPiece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (piece.Graphic == DungeonGraphicType.ChampionStatusBackground)
      return true;

    return piece.Name == "Champion Status Slot 1"
        || piece.Name == "Champion Status Slot 2"
        || piece.Name == "Champion Status Slot 3"
        || piece.Name == "Champion Status Slot 4";
  }

  private void PersistChanges()
  {
    if (Application.isPlaying || layout == null)
      return;

    StripObsoleteFrontWallF1ABPieces();
    EditorUtility.SetDirty(layout);
    AssetDatabase.SaveAssets();

    RefreshDungeonRenderer();
    RefreshEditModePreview();
  }


  /// <summary>
  /// Mirror Horizontally toggled: capture per-pose flag, then force an immediate
  /// Edit Mode viewport + Console refresh (including (M) markers).
  /// </summary>
  private void ApplyMirrorHorizontallyChangeForCurrentPose()
  {
    if (Application.isPlaying || layout == null)
      return;

    EditorUtility.SetDirty(layout);

    // Same pose X/Y/Facing — clear dedupe so Console re-emits with new (M).
    ResetEditModeViewportLogCache();
    DestroyEditModePreviewTextureOnly();
    RefreshEditModePreview();
    RepaintGameViews();
    Repaint();
  }

  private static bool IsFrontWallF1Card(ViewportPiece piece)
  {
    if (piece == null || piece.Name == null)
      return false;

    return piece.Name == "FrontF1"
        || piece.Name == "Front Wall F1";
  }

  private bool TryGetFrontF1PreviewEnabledOverride(out bool value)
  {
    value = false;
    foreach (KeyValuePair<ViewportPiece, bool> entry in previewEnabledOverrideByPiece)
    {
      if (entry.Key == null || !IsFrontWallF1Card(entry.Key))
        continue;

      value = entry.Value;
      return true;
    }

    return false;
  }

  private bool TryGetFrontF1PreviewPositionOverride(
      ViewportPiece piece,
      out Vector2Int position)
  {
    if (piece != null
        && previewPositionOverrideByPiece.TryGetValue(piece, out position))
    {
      return true;
    }

    foreach (KeyValuePair<ViewportPiece, Vector2Int> entry in previewPositionOverrideByPiece)
    {
      if (entry.Key == null || !IsFrontWallF1Card(entry.Key))
        continue;
      if (piece != null && ReferenceEquals(entry.Key, piece))
        continue;

      position = entry.Value;
      return true;
    }

    position = default;
    return false;
  }

  private bool TryGetFrontF1PreviewMirrorOverride(
      ViewportPiece piece,
      out bool mirror)
  {
    if (piece != null
        && previewMirrorOverrideByPiece.TryGetValue(piece, out mirror))
    {
      return true;
    }

    foreach (KeyValuePair<ViewportPiece, bool> entry in previewMirrorOverrideByPiece)
    {
      if (entry.Key == null || !IsFrontWallF1Card(entry.Key))
        continue;
      if (piece != null && ReferenceEquals(entry.Key, piece))
        continue;

      mirror = entry.Value;
      return true;
    }

    mirror = false;
    return false;
  }

  private bool TryGetFrontF1PreviewWidthOverride(
      ViewportPiece piece,
      out int width)
  {
    if (piece != null
        && previewFrontF1WidthOverrideByPiece.TryGetValue(piece, out width))
    {
      width = StraightF1WallLogic.NormalizeFrontWallF1Width(width);
      return true;
    }

    foreach (KeyValuePair<ViewportPiece, int> entry in previewFrontF1WidthOverrideByPiece)
    {
      if (entry.Key == null || !IsFrontWallF1Card(entry.Key))
        continue;
      if (piece != null && ReferenceEquals(entry.Key, piece))
        continue;

      width = StraightF1WallLogic.NormalizeFrontWallF1Width(entry.Value);
      return true;
    }

    width = 0;
    return false;
  }

  private string CurrentPreviewPoseKey()
  {
    return previewX + "," + previewY + "," + previewFacing;
  }

  private void EnsureFrontF1Crop05SouthDefault()
  {
    string key05South = "0,5," + DungeonFacing.South;
    if (previewFrontF1CropByPose.ContainsKey(key05South))
      return;

    previewFrontF1CropByPose[key05South] = new FrontF1CropPreviewState
    {
      Enabled = true,
      CropX = 32
    };
  }

  private void SaveCurrentFrontF1CropPreview()
  {
    previewFrontF1CropByPose[CurrentPreviewPoseKey()] =
        new FrontF1CropPreviewState
        {
          Enabled = frontF1CropPreview,
          CropX = Mathf.Clamp(
              frontF1CropStartXPreview,
              0,
              StraightF1WallLogic.CompositeWidth - 1)
        };
  }

  private void LoadFrontF1CropPreviewForCurrentPose()
  {
    EnsureFrontF1Crop05SouthDefault();
    if (previewFrontF1CropByPose.TryGetValue(
            CurrentPreviewPoseKey(), out FrontF1CropPreviewState state))
    {
      frontF1CropPreview = state.Enabled;
      frontF1CropStartXPreview = Mathf.Clamp(
          state.CropX,
          0,
          StraightF1WallLogic.CompositeWidth - 1);
      return;
    }

    frontF1CropPreview = false;
    frontF1CropStartXPreview = 0;
  }

  private static bool IsFrontWallF2Card(ViewportPiece piece)
  {
    if (piece == null || piece.Name == null)
      return false;

    return piece.Name == "FrontF2"
        || piece.Name == "Front Wall F2";
  }

  private static bool IsFrontWallF3Card(ViewportPiece piece)
  {
    if (piece == null || piece.Name == null)
      return false;

    return piece.Name == "FrontF3"
        || piece.Name == "Front Wall F3";
  }

  private static bool Is52SouthLeftD3Piece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    return piece.Name == "LeftD3"
        || piece.Name == "Wall D3L2"
        || piece.Name == "WallD3L2"
        || piece.Graphic == DungeonGraphicType.WallD3L2;
  }

  /// <summary>
  /// (5,2) South left-to-right blit / ViewEdit list order:
  /// LeftS2, FrontF3, FrontF1, RightD3. LeftD3 shares the leftmost
  /// slot when manually enabled in Show All Walls.
  /// </summary>
  private static int Get52SouthLeftToRightOrder(ViewportPiece piece)
  {
    if (piece != null && piece.Name == "LeftS2")
      return 0;
    if (Is52SouthLeftD3Piece(piece))
      return 0;
    if (IsFrontWallF3Card(piece))
      return 1;
    if (IsFrontWallF1Card(piece))
      return 2;
    if (piece != null
        && (piece.Name == "RightD3"
            || piece.Name == "Wall D3R2"
            || piece.Graphic == DungeonGraphicType.WallD3R2))
    {
      return 3;
    }

    return 4;
  }

  private bool TryGet52SouthForcedWallEnabled(
      ViewportPiece piece,
      out bool enabled)
  {
    enabled = false;

    if (piece == null
        || previewX != 5
        || previewY != 2
        || previewFacing != DungeonFacing.South
        || !IsNormalWallPiece(piece)
        || piece.Name == "LeftS2")
    {
      return false;
    }

    // LeftD3 remains available only through Show All Walls for manual testing.
    enabled = IsFrontWallF3Card(piece)
        || IsFrontWallF1Card(piece)
        || piece.Name == "RightD3"
        || piece.Name == "Wall D3R2"
        || piece.Graphic == DungeonGraphicType.WallD3R2;
    return true;
  }

  private static bool IsPoseOffsetCard(ViewportPiece piece)
  {
    if (piece == null || piece.Name == null)
      return false;

    switch (piece.Name)
    {
      case "FrontF3":
      case "LeftF3":
      case "RightF3":
      case "LeftD3":
      case "RightD3":
      case "FrontF2":
      case "LeftF2":
      case "RightF2":
      case "FrontF1":
      case "LeftF1":
      case "RightF1":
      case "LeftF0":
      case "RightF0":
        return true;
      default:
        return false;
    }
  }

  /// <summary>
  /// Pose Offset X/Y toggled: capture per-pose offsets without mutating
  /// layout piece.X / piece.Y, then refresh Edit Mode preview.
  /// </summary>
  private void ApplyPoseOffsetChangeForCurrentPose()
  {
    if (Application.isPlaying || layout == null)
      return;


    ResetEditModeViewportLogCache();
    DestroyEditModePreviewTextureOnly();
    RefreshEditModePreview();
    RepaintGameViews();
    Repaint();
  }

  /// <summary>
  /// F1 Width toggled: capture per-pose width, then refresh Edit Mode preview.
  /// </summary>
  private void ApplyFrontWallF1WidthChangeForCurrentPose()
  {
    if (Application.isPlaying || layout == null)
      return;

    EditorUtility.SetDirty(layout);

    ResetEditModeViewportLogCache();
    DestroyEditModePreviewTextureOnly();
    RefreshEditModePreview();
    RepaintGameViews();
    Repaint();
  }

  /// <summary>
  /// F2 Width toggled: capture per-pose width, then refresh Edit Mode preview.
  /// </summary>
  private void ApplyFrontWallF2WidthChangeForCurrentPose()
  {
    if (Application.isPlaying || layout == null)
      return;

    EditorUtility.SetDirty(layout);

    ResetEditModeViewportLogCache();
    DestroyEditModePreviewTextureOnly();
    RefreshEditModePreview();
    RepaintGameViews();
    Repaint();
  }

  /// <summary>
  /// TEMP diagnostic: force live wall Enabled to Front Wall F1 only at
  /// (1,2) West, capture into that pose entry, persist, refresh preview.
  /// </summary>
  private void SetWestF1BOnlyDiagnostic()
  {
    if (Application.isPlaying || layout == null || layout.Pieces == null)
      return;

    if (!IsPreviewPose12West())
      return;

    Undo.RecordObject(layout, "Set West F1 Only");

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null || string.IsNullOrEmpty(piece.Name))
        continue;

      switch (piece.Name)
      {
        case "Front Wall F1":
          piece.Enabled = true;
          break;
        case "Front Wall F2":
        case "Front Wall F3":
        case "Wall F0Left":
        case "Wall F0Right":
        case "Wall F1Left":
        case "Wall F1Right":
        case "Wall F2Left":
        case "Wall F2Right":
        case "Wall F3Left":
        case "Wall F3Right":
          piece.Enabled = false;
          break;
      }
    }

    // Capture + persist current pose (1,2 West) via existing workflow.
    PersistChanges();
  }

  private bool IsPreviewPose12West()
  {
    return previewX == 1
        && previewY == 2
        && previewFacing == DungeonFacing.West;
  }



  private void EnsureChampionStatusSlotsEnabled()
  {
    if (layout == null || layout.Pieces == null)
      return;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name == "Champion Status Slot 1"
          || piece.Name == "Champion Status Slot 2"
          || piece.Name == "Champion Status Slot 3"
          || piece.Name == "Champion Status Slot 4")
      {
        piece.Enabled = true;
      }
    }
  }

  private void ApplyCurrentPoseVisibilityToLayout()
  {
    if (layout == null)
      return;

    // No stored view is loaded or created here.
    ApplyUnknownPoseDefaultsToLayout();
    EnsureChampionStatusSlotsEnabled();
    ApplyCeilingMirrorFromPose();
    ApplyFloorMirrorReferenceOverride();

    // CHATGPT_BUILD_F1_MINIMAP_ALGORITHM_STAGE1_20260830_AB
    // Stage 1: automatic F0/F1 wall assembly from minimap only.
    // F2/F3/D3/Black Door remain disabled until F1 is verified.
    ApplyF1MinimapWallRecipe();

    // Apply Black Door F1/F2/F3 pose-specific Enabled states after the normal
    // wall recipe. This routine previously existed but was never called.
    ApplyBlackDoorEnabledFromPoseException();

    // Exception layer: the Hall of Champions oblique RightD3 starts enabled so
    // ViewEdit reflects what is actually rendered. The user may temporarily
    // disable it with the Enabled checkbox for visual testing.
    EnableBlackDoorObliqueRightD3ForCurrentPose();
  }

  private void EnableBlackDoorObliqueRightD3ForCurrentPose()
  {
    if (layout == null || layout.Pieces == null)
      return;

    if (previewX != 0
        || previewY != 5
        || previewFacing != DungeonFacing.North)
      return;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "Wall D3R2" && piece.Name != "RightD3")
        continue;

      piece.Enabled = true;
      return;
    }
  }

  /// <summary>
  /// Stage-1 automatic wall resolver.
  /// Reads only the player-relative minimap cells and resolves F0/F1.
  /// No pose storage and no absolute map-coordinate exceptions.
  ///
  /// FrontF1:
  ///   F0L wall + F0R wall -> 160px centered at X=32
  ///   F0L wall + F0R open -> 192px mirrored, covering X=32..223
  ///   F0L open + F0R wall -> 192px normal, covering X=0..191
  ///   F0L open + F0R open -> 224px at X=0
  ///
  /// F1 side walls are visible only when F1 center is open.
  /// </summary>
  private static bool GetFrontF2LateralMirrorPhase(
      int playerX,
      int playerY,
      DungeonFacing facing)
  {
    return StraightF1WallLogic.GetFrontF2LateralMirrorPhase(
        playerX,
        playerY,
        facing);
  }

  /// <summary>
  /// Deterministic F0 side-wall mirror phase.
  /// Reference pose (1,3) South is Mirror OFF.
  /// Moving one map tile OR turning 90 degrees flips the phase.
  /// </summary>
  private bool GetSideWallMirrorFromPose()
  {
    int referenceParity =
        (1 + 3 + (int)DungeonFacing.South) & 1;
    int currentParity =
        (previewX + previewY + (int)previewFacing) & 1;

    return currentParity != referenceParity;
  }

  private bool GetF0MirrorFromPose()
  {
    return GetSideWallMirrorFromPose();
  }


  // TEMP MAP-CHECK SIMULATION:
  // Treat map cell (0,5) as a wall in ViewEdit geometry only.
  // Remove/disable this after the map check; HallOfChampions.json is untouched.
  private const bool TemporaryDisableMapCell0_5 = false;

  private static bool IsViewEditGeometryWall(RelativeViewportCell cell)
  {
    if (TemporaryDisableMapCell0_5 && cell.X == 0 && cell.Y == 5)
      return false;

    return cell.IsWall;
  }

  private static bool IsVerifiedFrontF1X0Geometry(
      RelativeViewportGeometry g)
  {
    // Verified original-DM references: 4,7 East and 4,17 East.
    // Keyed only by normalized diagnostic geometry, never absolute map pose.
    //
    // Shared decisive geometry:
    // F0:       R=W
    // F1: L=W C=W R=W
    // F2:     C=W R=W
    // F3:     C=W R=W
    //
    // F0 Left, F2 Left and F3 Left are deliberately ignored because the
    // verified views differ there but both require FrontF1 X=0.
    return IsViewEditGeometryWall(g.F0Right)
        && IsViewEditGeometryWall(g.F1Left)
        && IsViewEditGeometryWall(g.F1Center)
        && IsViewEditGeometryWall(g.F1Right)
        && IsViewEditGeometryWall(g.F2Center)
        && IsViewEditGeometryWall(g.F2Right)
        && IsViewEditGeometryWall(g.F3Center)
        && IsViewEditGeometryWall(g.F3Right);
  }

  private static bool IsVerifiedRightF3MirrorOnGeometry(
      RelativeViewportGeometry g)
  {
    // Verified original-DM reference first observed at 1,7 East.
    // Keyed only by normalized diagnostic geometry, not map coordinates/facing.
    //
    // F0: L=O R=W
    // F1: L=W C=O R=W
    // F2: L=W C=O R=W
    // F3: L=O C=O R=W
    return !IsViewEditGeometryWall(g.F0Left)
        && IsViewEditGeometryWall(g.F0Right)
        && IsViewEditGeometryWall(g.F1Left)
        && !IsViewEditGeometryWall(g.F1Center)
        && IsViewEditGeometryWall(g.F1Right)
        && IsViewEditGeometryWall(g.F2Left)
        && !IsViewEditGeometryWall(g.F2Center)
        && IsViewEditGeometryWall(g.F2Right)
        && !IsViewEditGeometryWall(g.F3Left)
        && !IsViewEditGeometryWall(g.F3Center)
        && IsViewEditGeometryWall(g.F3Right);
  }

  private static bool IsVerifiedRightF2MirrorOnGeometry(
      RelativeViewportGeometry g)
  {
    // Verified original-DM reference first observed at 1,7 East.
    // Keyed only by normalized diagnostic geometry, not map coordinates/facing.
    //
    // F0: L=O R=W
    // F1: L=W C=O R=W
    // F2: L=W C=O R=W
    // F3: L=O C=O R=W
    return !IsViewEditGeometryWall(g.F0Left)
        && IsViewEditGeometryWall(g.F0Right)
        && IsViewEditGeometryWall(g.F1Left)
        && !IsViewEditGeometryWall(g.F1Center)
        && IsViewEditGeometryWall(g.F1Right)
        && IsViewEditGeometryWall(g.F2Left)
        && !IsViewEditGeometryWall(g.F2Center)
        && IsViewEditGeometryWall(g.F2Right)
        && !IsViewEditGeometryWall(g.F3Left)
        && !IsViewEditGeometryWall(g.F3Center)
        && IsViewEditGeometryWall(g.F3Right);
  }

  private static bool IsVerifiedRightF1MirrorOnGeometry(
      RelativeViewportGeometry g)
  {
    // Verified original-DM reference first observed at 1,7 East.
    // Keyed only by normalized diagnostic geometry, not map coordinates/facing.
    //
    // F0: L=O R=W
    // F1: L=W C=O R=W
    // F2: L=W C=O R=W
    // F3: L=O C=O R=W
    return !IsViewEditGeometryWall(g.F0Left)
        && IsViewEditGeometryWall(g.F0Right)
        && IsViewEditGeometryWall(g.F1Left)
        && !IsViewEditGeometryWall(g.F1Center)
        && IsViewEditGeometryWall(g.F1Right)
        && IsViewEditGeometryWall(g.F2Left)
        && !IsViewEditGeometryWall(g.F2Center)
        && IsViewEditGeometryWall(g.F2Right)
        && !IsViewEditGeometryWall(g.F3Left)
        && !IsViewEditGeometryWall(g.F3Center)
        && IsViewEditGeometryWall(g.F3Right);
  }

  private static bool IsVerifiedRightF0MirrorOnGeometry(
      RelativeViewportGeometry g)
  {
    // Verified original-DM reference first observed at 1,7 East.
    // IMPORTANT: this is keyed only by the normalized diagnostic geometry,
    // not by absolute map X/Y or facing.
    //
    // F0: L=O R=W
    // F1: L=W C=O R=W
    // F2: L=W C=O R=W
    // F3: L=O C=O R=W
    return !IsViewEditGeometryWall(g.F0Left)
        && IsViewEditGeometryWall(g.F0Right)
        && IsViewEditGeometryWall(g.F1Left)
        && !IsViewEditGeometryWall(g.F1Center)
        && IsViewEditGeometryWall(g.F1Right)
        && IsViewEditGeometryWall(g.F2Left)
        && !IsViewEditGeometryWall(g.F2Center)
        && IsViewEditGeometryWall(g.F2Right)
        && !IsViewEditGeometryWall(g.F3Left)
        && !IsViewEditGeometryWall(g.F3Center)
        && IsViewEditGeometryWall(g.F3Right);
  }

  private static bool IsLeftD3ObliqueOpening(RelativeViewportGeometry g)
  {
    return !IsViewEditGeometryWall(g.F0Left)
        && !IsViewEditGeometryWall(g.F0Right)
        && IsViewEditGeometryWall(g.F1Center)
        && !IsViewEditGeometryWall(g.F1Left);
  }

  private bool HasLeftD3LeadingStripActiveTile()
  {
    EnsurePreviewMiniMapLoaded();
    if (previewMiniMap == null)
      return false;

    DungeonMap.GetForwardOffset(
        previewFacing,
        out int forwardX,
        out int forwardY);
    DungeonMap.GetRightOffset(
        previewFacing,
        out int rightX,
        out int rightY);

    int sampleX = previewX + forwardX * 2 - rightX * 2;
    int sampleY = previewY + forwardY * 2 - rightY * 2;

    // Temporary comparison switch: "disabled" means black wall / non-existing.
    if (TemporaryDisableMapCell0_5 && sampleX == 0 && sampleY == 5)
      return false;

    if (!previewMiniMap.IsInside(sampleX, sampleY))
      return false;

    // White/active map tile -> the tiny 8 px leading strip is visible.
    // Black wall/non-existing tile -> the strip is hidden.
    return previewMiniMap.GetTile(sampleX, sampleY).Type != DungeonTileType.Wall;
  }

  /// <summary>
  /// Deterministic FrontF1 mirror phase.
  /// Reference pose (1,3) South is Mirror OFF.
  /// Moving one map tile OR turning 90 degrees flips the phase.
  /// </summary>
  private bool GetFrontF1MirrorFromPose()
  {
    int referenceParity =
        (1 + 3 + (int)DungeonFacing.South) & 1;
    int currentParity =
        (previewX + previewY + (int)previewFacing) & 1;

    return currentParity != referenceParity;
  }

  private static string BuildFrontF1GeometryKey(RelativeViewportGeometry g)
  {
    return (IsViewEditGeometryWall(g.F0Left) ? "W" : "O")
        + (IsViewEditGeometryWall(g.F0Right) ? "W" : "O")
        + "|"
        + (IsViewEditGeometryWall(g.F1Left) ? "W" : "O")
        + (IsViewEditGeometryWall(g.F1Center) ? "W" : "O")
        + (IsViewEditGeometryWall(g.F1Right) ? "W" : "O")
        + "|"
        + (IsViewEditGeometryWall(g.F2Left) ? "W" : "O")
        + (IsViewEditGeometryWall(g.F2Center) ? "W" : "O")
        + (IsViewEditGeometryWall(g.F2Right) ? "W" : "O")
        + "|"
        + (IsViewEditGeometryWall(g.F3Left) ? "W" : "O")
        + (IsViewEditGeometryWall(g.F3Center) ? "W" : "O")
        + (IsViewEditGeometryWall(g.F3Right) ? "W" : "O");
  }

  private static string BuildNormalWallEnabledGeometryKey(
      RelativeViewportGeometry g,
      ViewportPiece piece)
  {
    string pieceId = piece != null
        ? (!string.IsNullOrEmpty(piece.Name)
            ? piece.Name
            : piece.Graphic.ToString())
        : "<null>";
    return BuildFrontF1GeometryKey(g) + "|" + pieceId;
  }

  private bool TryGetCurrentRelativeViewportGeometry(out RelativeViewportGeometry geometry)
  {
    geometry = default;
    EnsurePreviewMiniMapLoaded();
    if (previewMiniMap == null)
      return false;

    geometry = RelativeViewportGeometry.Calculate(
        previewMiniMap,
        previewX,
        previewY,
        previewFacing);
    return true;
  }

  private bool TryGetPieceCardReferenceXY(
      ViewportPiece piece,
      bool mirror,
      out int x,
      out int y)
  {
    bool hasCurrentGeometry =
        TryGetCurrentRelativeViewportGeometry(
            out RelativeViewportGeometry currentGeometry);

    bool leftD3FrontF1Reference =
        IsFrontWallF1Card(piece)
        && hasCurrentGeometry
        && IsLeftD3ObliqueOpening(currentGeometry);

    bool verifiedFrontF1X0Reference =
        IsFrontWallF1Card(piece)
        && hasCurrentGeometry
        && IsVerifiedFrontF1X0Geometry(currentGeometry);

    // Pose-specific canonical reference verified in ViewEdit:
    // 1,5 North LeftF1 = X 0 / display Y 42 / Mirror OFF.
    if (IsWallF1LeftPiece(piece)
        && previewX == 1
        && previewY == 5
        && previewFacing == DungeonFacing.North)
    {
      x = 0;
      y = 42;
      return true;
    }

    if (TryGetSideWallCanonicalName(piece, out _)
        && TryGetActiveCanonicalReferenceXY(piece, mirror, out x, out y))
    {
      return true;
    }

    if (!TryGetActiveCanonicalReferenceXY(piece, mirror, out x, out y))
      return false;

    if (verifiedFrontF1X0Reference)
      x = 0;
    else if (leftD3FrontF1Reference)
      x = 32;

    return true;
  }

  private void ApplyF1MinimapWallRecipe()
  {
    DisableAllWallRenderingPieces();

    if (layout == null || layout.Pieces == null)
      return;

    EnsurePreviewMiniMapLoaded();
    if (previewMiniMap == null)
      return;

    RelativeViewportGeometry g =
        RelativeViewportGeometry.Calculate(
            previewMiniMap,
            previewX,
            previewY,
            previewFacing);

    string frontF1GeometryKey = BuildFrontF1GeometryKey(g);

    bool frontF1 =
        IsViewEditGeometryWall(g.F1Center);
    // Exposed left depth lane: F1/F2 centers open, F1-left open, F2-left open,
    // F3-left wall. This requires the side LeftF3 piece only. FrontF3 remains
    // the normal straight-ahead center-wall case. Keep it player-relative so
    // it works after moving or turning.
    bool leftLaneF3 =
        !IsViewEditGeometryWall(g.F1Center)
        && !IsViewEditGeometryWall(g.F2Center)
        && !IsViewEditGeometryWall(g.F1Left)
        && !IsViewEditGeometryWall(g.F2Left)
        && IsViewEditGeometryWall(g.F3Left);

    bool leftS2 =
        IsViewEditGeometryWall(g.F1Center)
        && !IsViewEditGeometryWall(g.F1Left)
        && !IsViewEditGeometryWall(g.F2Left)
        && IsViewEditGeometryWall(g.F3Left);

    bool frontF3 =
        (!IsViewEditGeometryWall(g.F1Center) &&
         !IsViewEditGeometryWall(g.F2Center) &&
         IsViewEditGeometryWall(g.F3Center))
        || leftS2;

    bool leftF0 = IsViewEditGeometryWall(g.F0Left);
    bool rightF0 = IsViewEditGeometryWall(g.F0Right);
    bool f0Mirror = GetF0MirrorFromPose();
    bool frontF1Mirror =
        leftS2 || GetFrontF1MirrorFromPose();

    bool leftF1 =
        !IsViewEditGeometryWall(g.F1Center) &&
        IsViewEditGeometryWall(g.F1Left);
    bool rightF1 =
        !IsViewEditGeometryWall(g.F1Center) &&
        IsViewEditGeometryWall(g.F1Right);

    bool leftF2 =
        !IsViewEditGeometryWall(g.F1Center) &&
        !IsViewEditGeometryWall(g.F2Center) &&
        IsViewEditGeometryWall(g.F2Left);
    bool rightF2 =
        !IsViewEditGeometryWall(g.F1Center) &&
        !IsViewEditGeometryWall(g.F2Center) &&
        IsViewEditGeometryWall(g.F2Right);

    bool leftF3 = leftLaneF3;
    bool rightF3 =
        !IsViewEditGeometryWall(g.F1Center) &&
        !IsViewEditGeometryWall(g.F2Center) &&
        !IsViewEditGeometryWall(g.F3Center) &&
        IsViewEditGeometryWall(g.F3Right);

    // RightD3 oblique-right opening derived only from player-relative near geometry,
    // never from absolute map coordinates. Verified examples include 1,6 East,
    // 1,16 East, 6,2 East, 6,14 East, and 15,7 East. Deeper F2/F3 cells
    // vary across those views, so they are deliberately not part of the rule.
    bool rightD3ObliqueOpening =
        !IsViewEditGeometryWall(g.F0Left)
        && !IsViewEditGeometryWall(g.F0Right)
        && IsViewEditGeometryWall(g.F1Center)
        && !IsViewEditGeometryWall(g.F1Right);

    // LeftD3 oblique-left opening is the independent mirror-side decision.
    // Verified at 2,17 North and 7,15 North; 16,17 North confirms that
    // LeftD3 and RightD3 can both be active at the same time.
    bool leftD3ObliqueOpening =
        IsLeftD3ObliqueOpening(g)
        && !leftS2;

    // Minimap occupancy signature, not a map pose.
    bool frontMirror =
        GetFrontF2LateralMirrorPhase(
            previewX,
            previewY,
            previewFacing);

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      bool enabled;
      int x;
      int y;
      bool mirror = false;
      int frontF1Width = 0;

      if (IsFrontWallF1Card(piece))
      {
        enabled = frontF1;
        // Display Y 42 -> Unity Y 47 (FrontF1 height 111).
        y = 47;
        mirror = frontF1Mirror;
        if (leftD3ObliqueOpening && rightD3ObliqueOpening)
            frontF1Width = StraightF1WallLogic.CompositeWidth160;
        else if (leftD3ObliqueOpening || rightD3ObliqueOpening)
            frontF1Width = StraightF1WallLogic.CompositeWidth191;
        else if (leftF0 && rightF0)
            frontF1Width = StraightF1WallLogic.CompositeWidth191;
        else if (leftF0 || rightF0)
            frontF1Width = StraightF1WallLogic.CompositeWidth191;
        else
            frontF1Width = StraightF1WallLogic.CompositeWidth;
        x = (leftF0 && rightF0)
            ? 0
            : frontF1Width == StraightF1WallLogic.CompositeWidth ? 0 : 32;

        // The current canonical recipe owns FrontF1 at a solid F0-left/F0-right view.
        // Do not let the legacy FrontF1 geometry-position store restore X=32 here.
        if (!(leftF0 && rightF0)
            && frontF1GeometryOverrides.TryGetValue(
                frontF1GeometryKey, out FrontF1GeometryOverride verifiedF1))
        {
          x = verifiedF1.X;
          y = verifiedF1.Y;
          frontF1Width =
              StraightF1WallLogic.NormalizeFrontWallF1Width(
                  verifiedF1.Width);
        }

        // D3 oblique views keep their geometry-specific FrontF1 anchor
        // authoritative even if an older verified geometry override disagrees.
        // LeftD3 needs the first 32 screen pixels free, so FrontF1 starts at X=32.
        // RightD3-only views anchor FrontF1 at X=0. If both are active, LeftD3
        // wins because X=0 would cover the left oblique strip.
        if (leftD3ObliqueOpening)
          x = 32;
        else if (rightD3ObliqueOpening)
          x = 0;

        // Solid-front/right-wall reference: moving FrontF1 to X=0 removes
        // the verified left-edge gap. Geometry signature only.
        if (IsVerifiedFrontF1X0Geometry(g))
          x = 0;
      }
      else if (IsFrontWallF2Card(piece))
      {
        // FrontF2 Enabled is owned by ViewEdit / saved pose visibility only.
        enabled = piece.Enabled;
        x = 0;
        y = DisplayYToUnityY(125, GetPieceHeightForEditorY(piece));
        mirror = frontMirror;
      }
      else if (IsFrontWallF3Card(piece))
      {
        enabled = frontF3;
        x = 7;
        y = DisplayYToUnityY(58, GetPieceHeightForEditorY(piece));
        mirror = false;
      }
      else if (IsWallF0LeftPiece(piece))
      {
        enabled = leftF0;
        mirror = GetSideWallMirrorFromPose();
        if (TryGetActiveCanonicalReferenceXY(
                piece, mirror, out int leftF0RefX, out int leftF0RefY))
        {
          x = leftF0RefX;
          y = DisplayYToUnityY(leftF0RefY, GetPieceHeightForEditorY(piece));
        }
        else
        {
          x = 0;
          y = DisplayYToUnityY(33, GetPieceHeightForEditorY(piece));
        }
      }
      else if (IsWallF0RightPiece(piece))
      {
        enabled = rightF0;
        mirror = IsVerifiedRightF0MirrorOnGeometry(g)
            ? true
            : GetSideWallMirrorFromPose();
        if (TryGetActiveCanonicalReferenceXY(
                piece, mirror, out int rightF0RefX, out int rightF0RefY))
        {
          x = rightF0RefX;
          y = DisplayYToUnityY(rightF0RefY, GetPieceHeightForEditorY(piece));
        }
        else
        {
          x = 192;
          y = DisplayYToUnityY(33, GetPieceHeightForEditorY(piece));
        }
      }
      else if (IsWallF1LeftPiece(piece))
      {
        enabled = leftF1;
        mirror = GetSideWallMirrorFromPose();

        if (TryGetActiveCanonicalReferenceXY(piece, mirror, out int leftF1RefX, out int leftF1RefY))
        {
          x = leftF1RefX;
          y = DisplayYToUnityY(leftF1RefY, GetPieceHeightForEditorY(piece));
        }
        else
        {
          x = 0;
          y = DisplayYToUnityY(42, GetPieceHeightForEditorY(piece));
        }

        // Verified canonical Black Door F3 view at 1,5 North.
        // LeftF1 is visible at X=0, display Y=42, Mirror OFF.
        if (previewX == 1
            && previewY == 5
            && previewFacing == DungeonFacing.North)
        {
          enabled = true;
          x = 0;
          y = DisplayYToUnityY(42, GetPieceHeightForEditorY(piece));
          mirror = false;
        }
      }
      else if (IsWallF1RightPiece(piece))
      {
        enabled = rightF1;
        mirror = IsVerifiedRightF1MirrorOnGeometry(g)
            ? true
            : GetSideWallMirrorFromPose();

        if (TryGetActiveCanonicalReferenceXY(piece, mirror, out int rightF1RefX, out int rightF1RefY))
        {
          x = rightF1RefX;
          y = DisplayYToUnityY(rightF1RefY, GetPieceHeightForEditorY(piece));
        }
        else
        {
          x = 165;
          y = DisplayYToUnityY(41, GetPieceHeightForEditorY(piece));
        }

        // Verified Black Door F1 side-wall alignment at 1,3 North.
        if (previewX == 1
            && previewY == 3
            && previewFacing == DungeonFacing.North)
        {
          x = 165;
          y = DisplayYToUnityY(42, GetPieceHeightForEditorY(piece));
        }
      }
      else if (IsWallF2LeftPiece(piece))
      {
        enabled = leftF2;
        mirror = GetSideWallMirrorFromPose();
        if (TryGetActiveCanonicalReferenceXY(
                piece, mirror, out int leftF2RefX, out int leftF2RefY))
        {
          x = leftF2RefX;
          y = DisplayYToUnityY(leftF2RefY, GetPieceHeightForEditorY(piece));
        }
        else
        {
          x = 0;
          y = DisplayYToUnityY(52, GetPieceHeightForEditorY(piece));
        }
      }
      else if (IsWallF2RightPiece(piece))
      {
        enabled = rightF2;
        mirror = IsVerifiedRightF2MirrorOnGeometry(g)
            ? true
            : GetSideWallMirrorFromPose();
        if (TryGetActiveCanonicalReferenceXY(
                piece, mirror, out int rightF2RefX, out int rightF2RefY))
        {
          x = rightF2RefX;
          y = DisplayYToUnityY(rightF2RefY, GetPieceHeightForEditorY(piece));
        }
        else
        {
          x = 147;
          y = DisplayYToUnityY(51, GetPieceHeightForEditorY(piece));
        }
      }
      else if (IsWallF3LeftPiece(piece))
      {
        enabled = leftF3;
        mirror = GetSideWallMirrorFromPose();
        if (TryGetActiveCanonicalReferenceXY(
                piece, mirror, out int leftF3RefX, out int leftF3RefY))
        {
          x = leftF3RefX;
          y = DisplayYToUnityY(leftF3RefY, GetPieceHeightForEditorY(piece));
        }
        else
        {
          x = 5;
          y = DisplayYToUnityY(60, GetPieceHeightForEditorY(piece));
        }
      }
      else if (IsWallF3RightPiece(piece))
      {
        enabled = rightF3;
        mirror = IsVerifiedRightF3MirrorOnGeometry(g)
            ? true
            : GetSideWallMirrorFromPose();
        if (TryGetActiveCanonicalReferenceXY(
                piece, mirror, out int rightF3RefX, out int rightF3RefY))
        {
          x = rightF3RefX;
          y = DisplayYToUnityY(rightF3RefY, GetPieceHeightForEditorY(piece));
        }
        else
        {
          x = 136;
          y = DisplayYToUnityY(60, GetPieceHeightForEditorY(piece));
        }
      }
      else if (piece.Name == "LeftS2")
      {
        enabled = leftS2;
        x = 0;
        y = DisplayYToUnityY(
            57,
            GetPieceHeightForEditorY(piece));
        mirror = GetSideWallMirrorFromPose();
      }
      else if (piece.Name == "Right2S")
      {
        enabled = false;
        x = piece.EffectiveX;
        y = piece.EffectiveY;
        mirror = piece.MirrorHorizontally;
      }
      else if (piece.Name == "LeftD3"
          || piece.Name == "Wall D3L2"
          || piece.Graphic == DungeonGraphicType.WallD3L2)
      {
        // Geometry-driven oblique left-side depth piece.
        // The first tiny leading strip is visible when the outer-left map tile
        // at 2-forward / 2-left is active/white. A black wall/non-existing
        // tile shifts the piece 8 px left so the strip disappears.
        enabled = leftD3ObliqueOpening;
        x = HasLeftD3LeadingStripActiveTile() ? 0 : -8;
        y = piece.EffectiveY;
        mirror = piece.MirrorHorizontally;
      }
      else if (piece.Name == "RightD3"
          || piece.Name == "Wall D3R2"
          || piece.Graphic == DungeonGraphicType.WallD3R2)
      {
        // Geometry-driven oblique right-side depth piece. Position/blit stay on
        // the existing RightD3 path; this rule decides only whether it is needed.
        enabled = rightD3ObliqueOpening;
        x = piece.EffectiveX;
        y = piece.EffectiveY;
        mirror = piece.MirrorHorizontally;
      }
      else
      {
        continue;
      }

      string normalWallGeometryKey =
          BuildNormalWallEnabledGeometryKey(g, piece);

      if (!IsFrontWallF2Card(piece)
          && normalWallEnabledGeometryOverrides.TryGetValue(
              normalWallGeometryKey,
              out bool verifiedEnabled))
      {
        enabled = verifiedEnabled;
      }

      // Exposed-left LeftF3 visibility is geometry authority. Older saved
      // Enabled overrides must not suppress the required side F3 piece.
      if (leftLaneF3 && IsWallF3LeftPiece(piece))
      {
        enabled = true;
      }

      // FrontF1 at a solid F0-left/F0-right view is now canonical recipe-owned.
      // The legacy normal-wall position store contains X=32 for this geometry;
      // bypass it so it cannot override the verified X=0 reference.
      if (!(IsFrontWallF1Card(piece) && leftF0 && rightF0)
          && normalWallPositionGeometryOverrides.TryGetValue(
              normalWallGeometryKey,
              out Vector2Int verifiedPosition))
      {
        x = verifiedPosition.x;
        y = verifiedPosition.y;
      }

      // FrontF3 canonical X is authoritative over any older saved
      // geometry-position override. The verified wall starts at screen X=7.
      if (IsFrontWallF3Card(piece))
      {
        x = 7;
      }

      // RightD3 canonical position is authoritative over any older saved
      // geometry-position override. Ref Y is top-down display space.
      if ((piece.Name == "RightD3"
              || piece.Name == "Wall D3R2"
              || piece.Graphic == DungeonGraphicType.WallD3R2)
          && TryGetActiveCanonicalReferenceXY(
              piece, mirror, out int rightD3RefX, out int rightD3RefY))
      {
        x = rightD3RefX;
        y = DisplayYToUnityY(
            rightD3RefY, GetPieceHeightForEditorY(piece));
      }

      // LeftD3 X is geometry-driven from the outer-left active/black map tile and
      // stays authoritative even if an older saved position disagrees.
      if ((piece.Name == "LeftD3"
              || piece.Name == "Wall D3L2"
              || piece.Graphic == DungeonGraphicType.WallD3L2)
          && enabled)
      {
        x = HasLeftD3LeadingStripActiveTile() ? 0 : -8;
      }

      if (normalWallMirrorGeometryOverrides.TryGetValue(
              normalWallGeometryKey,
              out bool verifiedMirror))
      {
        mirror = verifiedMirror;
      }

      // FrontF1 mirror is pose-parity driven and must flip when moving one
      // tile or turning 90 degrees. Keep it authoritative over any older
      // geometry-level mirror override.
      if (IsFrontWallF1Card(piece))
      {
        mirror = frontF1Mirror;
      }

      // ViewEdit visibility for (5,2) South.
      // LeftD3 stays manual-only in Show All Walls.
      // LeftS2 Enabled comes from leftS2 geometry, not this pose list.
      if (previewX == 5
          && previewY == 2
          && previewFacing == DungeonFacing.South
          && piece.Name != "LeftS2")
      {
        enabled = IsFrontWallF3Card(piece)
            || IsFrontWallF1Card(piece)
            || piece.Name == "RightD3"
            || piece.Name == "Wall D3R2"
            || piece.Graphic == DungeonGraphicType.WallD3R2;
      }

      ResolvedNormalWallState state = new ResolvedNormalWallState
      {
        Enabled = enabled,
        Graphic = piece.Graphic,
        X = x,
        Y = y,
        Mirror = mirror,
        FrontF1Width = frontF1Width,
        FrontF2Width = 0
      };

      resolvedNormalWallByPiece[piece] = state;
      if (!IsFrontWallF2Card(piece)
          || (previewX == 5
              && previewY == 2
              && previewFacing == DungeonFacing.South))
        piece.Enabled = state.Enabled;
      piece.MirrorHorizontally = state.Mirror;
      piece.PoseOffsetX = 0;
      piece.PoseOffsetY = 0;
    }

    bool sideWallMirror = GetSideWallMirrorFromPose();
    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null
          || (!IsWallF0LeftPiece(piece) && !IsWallF0RightPiece(piece)
              && !IsWallF1LeftPiece(piece) && !IsWallF1RightPiece(piece)
              && !IsWallF2LeftPiece(piece) && !IsWallF2RightPiece(piece)
              && !IsWallF3LeftPiece(piece) && !IsWallF3RightPiece(piece)))
        continue;

      bool resolvedSideMirror =
          (IsWallF0RightPiece(piece)
              && IsVerifiedRightF0MirrorOnGeometry(g))
          || (IsWallF1RightPiece(piece)
              && IsVerifiedRightF1MirrorOnGeometry(g))
          || (IsWallF2RightPiece(piece)
              && IsVerifiedRightF2MirrorOnGeometry(g))
          || (IsWallF3RightPiece(piece)
              && IsVerifiedRightF3MirrorOnGeometry(g))
              ? true
              : sideWallMirror;

      if (resolvedNormalWallByPiece.TryGetValue(
              piece, out ResolvedNormalWallState sideWallState))
      {
        sideWallState.Mirror = resolvedSideMirror;
        resolvedNormalWallByPiece[piece] = sideWallState;
      }

      piece.MirrorHorizontally = resolvedSideMirror;
    }

    // FrontF1 mirror is deterministic from the current map pose.
    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null || !IsFrontWallF1Card(piece))
        continue;

      if (resolvedNormalWallByPiece.TryGetValue(
              piece, out ResolvedNormalWallState frontF1State))
      {
        frontF1State.Mirror = frontF1Mirror;
        resolvedNormalWallByPiece[piece] = frontF1State;
      }

      piece.MirrorHorizontally = frontF1Mirror;
    }

    // legacy store application disabled: canonical/recipe logic is now authoritative.

    // ApplyPersistedlegacy storeWallRows(frontF1GeometryKey);
    // LeftF0 mirror is deterministic from the current pose. older stored geometry can contain
    // the mirror value from a previously verified geometry, so restore the
    // current pose value after geometry resolution. A temporary manual ViewEdit mirror
    // override is applied later and can still win for testing.
    bool leftF0PoseMirror = GetSideWallMirrorFromPose();
    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null || !IsWallF0LeftPiece(piece))
        continue;

      if (resolvedNormalWallByPiece.TryGetValue(
              piece, out ResolvedNormalWallState leftF0StateAfterResolve))
      {
        leftF0StateAfterResolve.Mirror = leftF0PoseMirror;
        resolvedNormalWallByPiece[piece] = leftF0StateAfterResolve;
      }

      piece.MirrorHorizontally = leftF0PoseMirror;
    }

    // FrontF1 live X follows the canonical card reference for this geometry, so
    // the resolved value can never disagree with the Ref shown on the card.
    // Temporary ViewEdit X/Y overrides are applied afterward and still win.
    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null || !IsFrontWallF1Card(piece))
        continue;

      if (!TryGetPieceCardReferenceXY(
              piece,
              GetFrontF1MirrorFromPose(),
              out int frontF1RefX,
              out _))
      {
        continue;
      }

      if (resolvedNormalWallByPiece.TryGetValue(
              piece, out ResolvedNormalWallState frontF1StateAfterResolve))
      {
        frontF1StateAfterResolve.X = frontF1RefX;
        resolvedNormalWallByPiece[piece] = frontF1StateAfterResolve;
      }

      // Remove any older temporary ViewEdit X/Y edit for FrontF1 in the
      // solid-front geometries that previously owned this rule. Otherwise
      // ApplyTemporaryNormalWallPreviewOverrides() would put a stale X back.
      if ((leftF0 && rightF0) || IsVerifiedFrontF1X0Geometry(g))
        previewPositionOverrideByPiece.Remove(piece);
    }

    // Verified RightF0 geometry mirror authority.
    // Older stored geometry may contain an mirror value, so re-apply the normalized
    // geometry result after geometry resolution. A manual ViewEdit mirror override still
    // applies afterward and can be used for testing.
    if (IsVerifiedRightF0MirrorOnGeometry(g))
    {
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null || !IsWallF0RightPiece(piece))
          continue;

        if (resolvedNormalWallByPiece.TryGetValue(
                piece, out ResolvedNormalWallState rightF0StateAfterResolve))
        {
          rightF0StateAfterResolve.Mirror = true;
          resolvedNormalWallByPiece[piece] = rightF0StateAfterResolve;
        }

        piece.MirrorHorizontally = true;
      }
    }

    // Verified RightF3 geometry mirror authority.
    // Re-apply after geometry resolution so an older stored mirror value cannot overwrite
    // the normalized geometry result. Manual ViewEdit mirror overrides still
    // apply afterward for testing.
    if (IsVerifiedRightF3MirrorOnGeometry(g))
    {
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null || !IsWallF3RightPiece(piece))
          continue;

        if (resolvedNormalWallByPiece.TryGetValue(
                piece, out ResolvedNormalWallState rightF3StateAfterResolve))
        {
          rightF3StateAfterResolve.Mirror = true;
          resolvedNormalWallByPiece[piece] = rightF3StateAfterResolve;
        }

        piece.MirrorHorizontally = true;
      }
    }

    // Verified RightF2 geometry mirror authority.
    // Re-apply after geometry resolution so an older stored mirror value cannot overwrite
    // the normalized geometry result. Manual ViewEdit mirror overrides still
    // apply afterward for testing.
    if (IsVerifiedRightF2MirrorOnGeometry(g))
    {
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null || !IsWallF2RightPiece(piece))
          continue;

        if (resolvedNormalWallByPiece.TryGetValue(
                piece, out ResolvedNormalWallState rightF2StateAfterResolve))
        {
          rightF2StateAfterResolve.Mirror = true;
          resolvedNormalWallByPiece[piece] = rightF2StateAfterResolve;
        }

        piece.MirrorHorizontally = true;
      }
    }

    // Verified RightF1 geometry mirror authority.
    // Older stored geometry may contain an mirror value, so re-apply the normalized
    // geometry result after geometry resolution. Manual ViewEdit mirror overrides still
    // apply afterward for testing.
    if (IsVerifiedRightF1MirrorOnGeometry(g))
    {
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null || !IsWallF1RightPiece(piece))
          continue;

        if (resolvedNormalWallByPiece.TryGetValue(
                piece, out ResolvedNormalWallState rightF1StateAfterResolve))
        {
          rightF1StateAfterResolve.Mirror = true;
          resolvedNormalWallByPiece[piece] = rightF1StateAfterResolve;
        }

        piece.MirrorHorizontally = true;
      }
    }

    // Older stored geometry may contain an FrontF1 Mirror value. FrontF1 mirror is
    // pose-parity driven, so restore the pose value after geometry resolution is applied.
    // Temporary ViewEdit mirror overrides are applied afterward and still win.
    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null || !IsFrontWallF1Card(piece))
        continue;

      bool poseMirror = frontF1Mirror;

      if (resolvedNormalWallByPiece.TryGetValue(
              piece, out ResolvedNormalWallState frontF1StateAfterResolve))
      {
        frontF1StateAfterResolve.Mirror = poseMirror;
        resolvedNormalWallByPiece[piece] = frontF1StateAfterResolve;
      }

      piece.MirrorHorizontally = poseMirror;
    }

    ApplyTemporaryNormalWallPreviewOverrides();
  }

  /// <summary>
  /// Stationary-pose ViewEdit tests overlay geometry in resolved state
  /// only. They are never written to ViewportLayout.asset.
  /// </summary>
  private void ApplyTemporaryNormalWallPreviewOverrides()
  {
    if (layout == null || layout.Pieces == null)
      return;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null || !IsNormalWallPiece(piece))
        continue;

      bool hasEnabledOverride = previewEnabledOverrideByPiece.TryGetValue(
          piece, out bool previewEnabled);
      bool hasPositionOverride = previewPositionOverrideByPiece.TryGetValue(
          piece, out Vector2Int previewPosition);
      bool hasMirrorOverride = previewMirrorOverrideByPiece.TryGetValue(
          piece, out bool previewMirror);
      bool hasGraphicOverride = previewGraphicOverrideByPiece.TryGetValue(
          piece, out DungeonGraphicType previewGraphic);
      bool hasWidthOverride = false;
      int previewWidth = 0;
      if (IsFrontWallF1Card(piece)
          && previewFrontF1WidthOverrideByPiece.TryGetValue(
              piece, out previewWidth))
      {
        hasWidthOverride = true;
      }

      if (!hasEnabledOverride
          && !hasPositionOverride
          && !hasMirrorOverride
          && !hasWidthOverride
          && !hasGraphicOverride)
      {
        continue;
      }

      if (!resolvedNormalWallByPiece.TryGetValue(
              piece, out ResolvedNormalWallState state))
      {
        state = new ResolvedNormalWallState
        {
          Enabled = piece.Enabled,
          Graphic = piece.Graphic,
          X = piece.EffectiveX,
          Y = piece.EffectiveY,
          Mirror = piece.MirrorHorizontally,
          FrontF1Width = piece.FrontWallF1Width,
          FrontF2Width = piece.FrontWallF2Width
        };
      }

      if (hasEnabledOverride)
        state.Enabled = previewEnabled;
      if (hasGraphicOverride)
        state.Graphic = previewGraphic;
      if (hasPositionOverride)
      {
        state.X = previewPosition.x;
        state.Y = previewPosition.y;
      }
      if (hasMirrorOverride)
        state.Mirror = previewMirror;
      if (hasWidthOverride)
      {
        state.FrontF1Width =
            StraightF1WallLogic.NormalizeFrontWallF1Width(previewWidth);
      }

      resolvedNormalWallByPiece[piece] = state;
    }
  }

  /// <summary>
  /// Original-Dungeon-Master-style normal-wall authority:
  /// map + facing -> fixed relative cells -> fixed wall pieces.
  ///
  /// No saved views, no absolute map-coordinate cases, no perspective math.
  /// This stage changes only Enabled. Existing authored X/Y/width/source/mirror
  /// remain the fixed drawing recipe for each wall piece.
  /// </summary>
  private void ApplyFixedDungeonWallRecipe()
  {
    resolvedNormalWallByPiece.Clear();

    if (layout == null || layout.Pieces == null)
      return;

    EnsurePreviewMiniMapLoaded();
    if (previewMiniMap == null)
      return;

    // Always return geometry/source properties to the one shared authored
    // baseline before deciding which relative slots are occupied.
    RestoreNormalWallBaselineProperties();

    RelativeViewportGeometry g =
        RelativeViewportGeometry.Calculate(
            previewMiniMap,
            previewX,
            previewY,
            previewFacing);

    // CHATGPT_BUILD_SCUMMVM_RELATIVE_CELLS_STAGE1_20260829_AG
    // Source-backed Dungeon Master occupancy: each relative wall cell is
    // enabled directly from its own map cell. Do not manually suppress farther
    // cells; the original renderer gets occlusion from fixed draw order/shapes.
    bool leftF0   = IsViewEditGeometryWall(g.F0Left);
    bool rightF0  = IsViewEditGeometryWall(g.F0Right);

    bool leftF1   = IsViewEditGeometryWall(g.F1Left);
    bool frontF1  = IsViewEditGeometryWall(g.F1Center);
    bool rightF1  = IsViewEditGeometryWall(g.F1Right);

    bool leftF2   = IsViewEditGeometryWall(g.F2Left);
    bool rightF2  = IsViewEditGeometryWall(g.F2Right);

    bool leftF3   = IsViewEditGeometryWall(g.F3Left);
    bool frontF3  = IsViewEditGeometryWall(g.F3Center);
    bool rightF3  = IsViewEditGeometryWall(g.F3Right);

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (!IsNormalWallPiece(piece))
        continue;

      bool enabled = false;

      if (IsWallF0LeftPiece(piece))
        enabled = leftF0;
      else if (IsWallF0RightPiece(piece))
        enabled = rightF0;
      else if (IsWallF1LeftPiece(piece))
        enabled = leftF1;
      else if (IsFrontWallF1Card(piece))
        enabled = frontF1;
      else if (IsWallF1RightPiece(piece))
        enabled = rightF1;
      else if (IsWallF2LeftPiece(piece))
        enabled = leftF2;
      else if (IsFrontWallF2Card(piece))
        enabled = piece.Enabled;
      else if (IsWallF2RightPiece(piece))
        enabled = rightF2;
      else if (IsWallF3LeftPiece(piece))
        enabled = leftF3;
      else if (IsFrontWallF3Card(piece))
        enabled = frontF3;
      else if (IsWallF3RightPiece(piece))
        enabled = rightF3;
      else
      {
        // Legacy D3 helper pieces are not part of the first fixed-slot table.
        enabled = false;
      }

      if (!IsFrontWallF2Card(piece))
        piece.Enabled = enabled;
      piece.PoseOffsetX = 0;
      piece.PoseOffsetY = 0;

      // CHATGPT_BUILD_SCUMMVM_D1_Y_FIX_STAGE3_20260829_AI
      // ScummVM/DM destination frames for depth D1 only.
      // D1L: 0..63, D1C: 32..191, D1R: 160..223; ScummVM top-down y 9..119 converts to Unity bottom-up Y=16.
      // FrontF1 width 160 is centered by StraightF1WallLogic, so X=0
      // produces the source-backed D1C destination x=32.
      if (IsWallF1LeftPiece(piece))
      {
        piece.X = 0;
        piece.Y = 16;
      }
      else if (IsFrontWallF1Card(piece))
      {
        piece.X = 0;
        piece.Y = 16;
        piece.FrontWallF1Width = StraightF1WallLogic.CompositeWidth160;
      }
      else if (IsWallF1RightPiece(piece))
      {
        piece.X = 160;
        piece.Y = 16;
      }
    }
  }

  private static bool IsNormalWallPiece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (IsWallF0LeftPiece(piece)
        || IsWallF0RightPiece(piece)
        || IsWallF1LeftPiece(piece)
        || IsWallF1RightPiece(piece)
        || IsWallF2LeftPiece(piece)
        || IsWallF2RightPiece(piece)
        || IsWallF3LeftPiece(piece)
        || IsWallF3RightPiece(piece)
        || IsFrontWallF1Card(piece)
        || IsFrontWallF2Card(piece)
        || IsFrontWallF3Card(piece))
    {
      return true;
    }

    return piece.Graphic == DungeonGraphicType.WallD3L2
        || piece.Graphic == DungeonGraphicType.WallD3R2
        || piece.Name == "LeftS2"
        || piece.Name == "Right2S"
        || piece.Name == "LeftD3"
        || piece.Name == "RightD3"
        || piece.Name == "Wall D3L2"
        || piece.Name == "Wall D3R2";
  }

  private void CaptureNormalWallBaselinesFromLayout()
  {
    normalWallBaselineByName.Clear();

    if (layout == null || layout.Pieces == null)
      return;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (!IsNormalWallPiece(piece))
        continue;

      string key = piece.Name ?? string.Empty;
      if (key.Length == 0)
        continue;

      normalWallBaselineByName[key] = new NormalWallBaseline
      {
        Graphic = piece.Graphic,
        X = piece.X,
        Y = piece.Y,
        Mirror = piece.MirrorHorizontally,
        FrontF1Width = piece.FrontWallF1Width,
        FrontF2Width = piece.FrontWallF2Width
      };
    }
  }

  private void RestoreNormalWallBaselineProperties()
  {
    if (layout == null || layout.Pieces == null)
      return;

    if (normalWallBaselineByName.Count == 0)
      CaptureNormalWallBaselinesFromLayout();

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (!IsNormalWallPiece(piece))
        continue;

      string key = piece.Name ?? string.Empty;
      if (!normalWallBaselineByName.TryGetValue(key, out NormalWallBaseline baseline))
        continue;

      piece.Graphic = baseline.Graphic;
      piece.X = baseline.X;
      piece.Y = baseline.Y;
      piece.MirrorHorizontally = baseline.Mirror;
      piece.PoseOffsetX = 0;
      piece.PoseOffsetY = 0;
      piece.FrontWallF1Width = baseline.FrontF1Width;
      piece.FrontWallF2Width = baseline.FrontF2Width;
    }
  }


  private bool TryGetResolvedNormalWallState(
      ViewportPiece piece,
      out ResolvedNormalWallState state)
  {
    state = default;
    return piece != null
        && resolvedNormalWallByPiece.TryGetValue(piece, out state);
  }


  /// <summary>
  /// Ceiling mirror from (1,3) North = ON. Toggles once per tile step
  /// (forward/back/strafe) and once per 90° turn. Floor and walls unchanged.
  /// </summary>
  private void ApplyCeilingMirrorFromPose()
  {
    if (layout == null || layout.Pieces == null)
      return;

    bool mirrorOn =
        ((previewX + previewY + (int)previewFacing) & 1) == 0;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null || piece.Name != "Ceiling")
        continue;

      piece.MirrorHorizontally = mirrorOn;
      return;
    }
  }

  /// <summary>
  /// Floor mirror from (1,3) North = OFF. Toggles once per tile step
  /// (forward/back/strafe) and once per 90° turn, opposite to the ceiling phase.
  /// No other piece is changed.
  /// </summary>
  private void ApplyFloorMirrorReferenceOverride()
  {
    if (layout == null || layout.Pieces == null)
      return;

    bool mirrorOn =
        ((previewX + previewY + (int)previewFacing) & 1) != 0;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null || piece.Name != "Floor")
        continue;

      piece.MirrorHorizontally = mirrorOn;
      return;
    }
  }

  /// <summary>
  /// Wall F0Left / LeftF0 from the map tile immediately to the player's left.
  /// Solid/out-of-bounds → Enabled. Does not change Ceiling, Floor, or other walls.
  /// </summary>
  // LEGACY NORMAL-WALL PATH: retained temporarily for reference; not called by cutover pipeline.
  private void ApplyWallF0LeftFromMapGeometry()
  {
    if (layout == null || layout.Pieces == null)
      return;

    EnsurePreviewMiniMapLoaded();
    DungeonMap.GetRightOffset(
        previewFacing,
        out int rightX,
        out int rightY);
    int leftX = previewX - rightX;
    int leftY = previewY - rightY;
    bool leftIsWall = previewMiniMap == null
        || !previewMiniMap.IsInside(leftX, leftY)
        || previewMiniMap.GetTile(leftX, leftY).Type == DungeonTileType.Wall;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "Wall F0Left" && piece.Name != "LeftF0")
        continue;

      piece.Enabled = leftIsWall;
      piece.MirrorHorizontally = false;
      return;
    }
  }

  private static bool IsWallF0LeftPiece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (piece.Name == "Wall F0Left" || piece.Name == "LeftF0")
      return true;

    return piece.Graphic == DungeonGraphicType.WallF0L;
  }

  /// <summary>
  /// Wall F0Right / RightF0 from the map tile immediately to the player's right.
  /// Solid/out-of-bounds → Enabled. Does not change F0Left, Ceiling, Floor, or other walls.
  /// </summary>
  private void ApplyWallF0RightFromMapGeometry()
  {
    if (layout == null || layout.Pieces == null)
      return;

    EnsurePreviewMiniMapLoaded();
    DungeonMap.GetRightOffset(
        previewFacing,
        out int rightX,
        out int rightY);
    int neighborX = previewX + rightX;
    int neighborY = previewY + rightY;
    bool rightIsWall = previewMiniMap == null
        || !previewMiniMap.IsInside(neighborX, neighborY)
        || previewMiniMap.GetTile(neighborX, neighborY).Type
            == DungeonTileType.Wall;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "Wall F0Right" && piece.Name != "RightF0")
        continue;

      piece.Enabled = rightIsWall;
      piece.MirrorHorizontally = false;
      return;
    }
  }

  private static bool IsWallF0RightPiece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (piece.Name == "Wall F0Right" || piece.Name == "RightF0")
      return true;

    return piece.Graphic == DungeonGraphicType.WallF0R;
  }

  /// <summary>
  /// Wall F1Left / LeftF1 from the map tile one step forward and one step left,
  /// but only if the tile directly forward is open. A solid forward wall hides F1Left.
  /// </summary>
  private void ApplyWallF1LeftFromMapGeometry()
  {
    if (layout == null || layout.Pieces == null)
      return;

    EnsurePreviewMiniMapLoaded();
    DungeonMap.GetForwardOffset(
        previewFacing,
        out int forwardX,
        out int forwardY);
    DungeonMap.GetRightOffset(
        previewFacing,
        out int rightX,
        out int rightY);

    int frontX = previewX + forwardX;
    int frontY = previewY + forwardY;
    bool frontIsWall = previewMiniMap == null
        || !previewMiniMap.IsInside(frontX, frontY)
        || previewMiniMap.GetTile(frontX, frontY).Type == DungeonTileType.Wall;

    bool leftF1IsWall = false;
    if (!frontIsWall)
    {
      int tileX = previewX + forwardX - rightX;
      int tileY = previewY + forwardY - rightY;
      leftF1IsWall = previewMiniMap == null
          || !previewMiniMap.IsInside(tileX, tileY)
          || previewMiniMap.GetTile(tileX, tileY).Type == DungeonTileType.Wall;
    }

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "Wall F1Left" && piece.Name != "LeftF1")
        continue;

      piece.Enabled = leftF1IsWall;
      piece.MirrorHorizontally = false;
      return;
    }
  }

  private static bool IsWallF1LeftPiece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (piece.Name == "Wall F1Left" || piece.Name == "LeftF1")
      return true;

    return piece.Graphic == DungeonGraphicType.WallF1L;
  }

  /// <summary>
  /// Wall F1Right / RightF1 from the map tile one step forward and one step right,
  /// but only if the tile directly forward is open. A solid forward wall hides F1Right.
  /// </summary>
  private void ApplyWallF1RightFromMapGeometry()
  {
    if (layout == null || layout.Pieces == null)
      return;

    EnsurePreviewMiniMapLoaded();
    DungeonMap.GetForwardOffset(
        previewFacing,
        out int forwardX,
        out int forwardY);
    DungeonMap.GetRightOffset(
        previewFacing,
        out int rightX,
        out int rightY);

    int frontX = previewX + forwardX;
    int frontY = previewY + forwardY;
    bool frontIsWall = previewMiniMap == null
        || !previewMiniMap.IsInside(frontX, frontY)
        || previewMiniMap.GetTile(frontX, frontY).Type == DungeonTileType.Wall;

    bool rightF1IsWall = false;
    if (!frontIsWall)
    {
      int tileX = previewX + forwardX + rightX;
      int tileY = previewY + forwardY + rightY;
      rightF1IsWall = previewMiniMap == null
          || !previewMiniMap.IsInside(tileX, tileY)
          || previewMiniMap.GetTile(tileX, tileY).Type == DungeonTileType.Wall;
    }

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "Wall F1Right" && piece.Name != "RightF1")
        continue;

      piece.Enabled = rightF1IsWall;
      piece.MirrorHorizontally = false;
      return;
    }
  }

  private static bool IsWallF1RightPiece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (piece.Name == "Wall F1Right" || piece.Name == "RightF1")
      return true;

    return piece.Graphic == DungeonGraphicType.WallF1R;
  }

  /// <summary>
  /// Wall F2Left / LeftF2 from the map tile two steps forward and one step left,
  /// but only if both tiles directly forward are open. A solid nearer wall hides F2Left.
  /// </summary>
  private void ApplyWallF2LeftFromMapGeometry()
  {
    if (layout == null || layout.Pieces == null)
      return;

    EnsurePreviewMiniMapLoaded();
    DungeonMap.GetForwardOffset(
        previewFacing,
        out int forwardX,
        out int forwardY);
    DungeonMap.GetRightOffset(
        previewFacing,
        out int rightX,
        out int rightY);

    int front1X = previewX + forwardX;
    int front1Y = previewY + forwardY;
    int front2X = previewX + forwardX * 2;
    int front2Y = previewY + forwardY * 2;
    bool frontBlocked = previewMiniMap == null
        || !previewMiniMap.IsInside(front1X, front1Y)
        || previewMiniMap.GetTile(front1X, front1Y).Type == DungeonTileType.Wall
        || !previewMiniMap.IsInside(front2X, front2Y)
        || previewMiniMap.GetTile(front2X, front2Y).Type == DungeonTileType.Wall;

    bool leftF2IsWall = false;
    if (!frontBlocked)
    {
      int tileX = previewX + forwardX * 2 - rightX;
      int tileY = previewY + forwardY * 2 - rightY;
      leftF2IsWall = previewMiniMap == null
          || !previewMiniMap.IsInside(tileX, tileY)
          || previewMiniMap.GetTile(tileX, tileY).Type == DungeonTileType.Wall;
    }

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "Wall F2Left" && piece.Name != "LeftF2")
        continue;

      piece.Enabled = leftF2IsWall;
      piece.MirrorHorizontally = false;
      return;
    }
  }

  private static bool IsWallF2LeftPiece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (piece.Name == "Wall F2Left" || piece.Name == "LeftF2")
      return true;

    return piece.Graphic == DungeonGraphicType.WallF2L;
  }

  /// <summary>
  /// Wall F2Right / RightF2 from the map tile two steps forward and one step right,
  /// but only if both tiles directly forward are open. A solid nearer wall hides F2Right.
  /// </summary>
  private void ApplyWallF2RightFromMapGeometry()
  {
    if (layout == null || layout.Pieces == null)
      return;

    EnsurePreviewMiniMapLoaded();
    DungeonMap.GetForwardOffset(
        previewFacing,
        out int forwardX,
        out int forwardY);
    DungeonMap.GetRightOffset(
        previewFacing,
        out int rightX,
        out int rightY);

    int front1X = previewX + forwardX;
    int front1Y = previewY + forwardY;
    int front2X = previewX + forwardX * 2;
    int front2Y = previewY + forwardY * 2;
    bool frontBlocked = previewMiniMap == null
        || !previewMiniMap.IsInside(front1X, front1Y)
        || previewMiniMap.GetTile(front1X, front1Y).Type == DungeonTileType.Wall
        || !previewMiniMap.IsInside(front2X, front2Y)
        || previewMiniMap.GetTile(front2X, front2Y).Type == DungeonTileType.Wall;

    bool rightF2IsWall = false;
    if (!frontBlocked)
    {
      int tileX = previewX + forwardX * 2 + rightX;
      int tileY = previewY + forwardY * 2 + rightY;
      rightF2IsWall = previewMiniMap == null
          || !previewMiniMap.IsInside(tileX, tileY)
          || previewMiniMap.GetTile(tileX, tileY).Type == DungeonTileType.Wall;
    }

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "Wall F2Right" && piece.Name != "RightF2")
        continue;

      piece.Enabled = rightF2IsWall;
      piece.MirrorHorizontally = false;
      return;
    }
  }

  private static bool IsWallF2RightPiece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (piece.Name == "Wall F2Right" || piece.Name == "RightF2")
      return true;

    return piece.Graphic == DungeonGraphicType.WallF2R;
  }

  /// <summary>
  /// Wall F3Left / LeftF3 from the map tile three steps forward and one step left,
  /// but only if all three tiles directly forward are open. A nearer solid wall hides F3Left.
  /// </summary>
  private void ApplyWallF3LeftFromMapGeometry()
  {
    if (layout == null || layout.Pieces == null)
      return;

    EnsurePreviewMiniMapLoaded();
    DungeonMap.GetForwardOffset(
        previewFacing,
        out int forwardX,
        out int forwardY);
    DungeonMap.GetRightOffset(
        previewFacing,
        out int rightX,
        out int rightY);

    int front1X = previewX + forwardX;
    int front1Y = previewY + forwardY;
    int front2X = previewX + forwardX * 2;
    int front2Y = previewY + forwardY * 2;
    int front3X = previewX + forwardX * 3;
    int front3Y = previewY + forwardY * 3;
    bool frontBlocked = previewMiniMap == null
        || !previewMiniMap.IsInside(front1X, front1Y)
        || previewMiniMap.GetTile(front1X, front1Y).Type == DungeonTileType.Wall
        || !previewMiniMap.IsInside(front2X, front2Y)
        || previewMiniMap.GetTile(front2X, front2Y).Type == DungeonTileType.Wall
        || !previewMiniMap.IsInside(front3X, front3Y)
        || previewMiniMap.GetTile(front3X, front3Y).Type == DungeonTileType.Wall;

    bool leftF3IsWall = false;
    if (!frontBlocked)
    {
      int tileX = previewX + forwardX * 3 - rightX;
      int tileY = previewY + forwardY * 3 - rightY;
      leftF3IsWall = previewMiniMap == null
          || !previewMiniMap.IsInside(tileX, tileY)
          || previewMiniMap.GetTile(tileX, tileY).Type == DungeonTileType.Wall;
    }

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "Wall F3Left" && piece.Name != "LeftF3")
        continue;

      piece.Enabled = leftF3IsWall;
      piece.MirrorHorizontally = false;
      return;
    }
  }

  private static bool IsWallF3LeftPiece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (piece.Name == "Wall F3Left" || piece.Name == "LeftF3")
      return true;

    return piece.Graphic == DungeonGraphicType.WallF3L;
  }

  /// <summary>
  /// Wall F3Right / RightF3 from the map tile three steps forward and one step right,
  /// but only if all three tiles directly forward are open. A nearer solid wall hides F3Right.
  /// </summary>
  private void ApplyWallF3RightFromMapGeometry()
  {
    if (layout == null || layout.Pieces == null)
      return;

    EnsurePreviewMiniMapLoaded();
    DungeonMap.GetForwardOffset(
        previewFacing,
        out int forwardX,
        out int forwardY);
    DungeonMap.GetRightOffset(
        previewFacing,
        out int rightX,
        out int rightY);

    int front1X = previewX + forwardX;
    int front1Y = previewY + forwardY;
    int front2X = previewX + forwardX * 2;
    int front2Y = previewY + forwardY * 2;
    int front3X = previewX + forwardX * 3;
    int front3Y = previewY + forwardY * 3;
    bool frontBlocked = previewMiniMap == null
        || !previewMiniMap.IsInside(front1X, front1Y)
        || previewMiniMap.GetTile(front1X, front1Y).Type == DungeonTileType.Wall
        || !previewMiniMap.IsInside(front2X, front2Y)
        || previewMiniMap.GetTile(front2X, front2Y).Type == DungeonTileType.Wall
        || !previewMiniMap.IsInside(front3X, front3Y)
        || previewMiniMap.GetTile(front3X, front3Y).Type == DungeonTileType.Wall;

    bool rightF3IsWall = false;
    if (!frontBlocked)
    {
      int tileX = previewX + forwardX * 3 + rightX;
      int tileY = previewY + forwardY * 3 + rightY;
      rightF3IsWall = previewMiniMap == null
          || !previewMiniMap.IsInside(tileX, tileY)
          || previewMiniMap.GetTile(tileX, tileY).Type == DungeonTileType.Wall;
    }

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "Wall F3Right" && piece.Name != "RightF3")
        continue;

      piece.Enabled = rightF3IsWall;
      piece.MirrorHorizontally = false;
      return;
    }
  }

  private static bool IsWallF3RightPiece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (piece.Name == "Wall F3Right" || piece.Name == "RightF3")
      return true;

    return piece.Graphic == DungeonGraphicType.WallF3R;
  }

  /// <summary>
  /// RightD3 / Wall D3R2 visibility from map geometry.
  /// Normal rule: all three forward tiles must be open and the depth-3/right
  /// tile must be solid. The existing (1,5) North Black Door F3 view is locked
  /// and deliberately left untouched by this rule.
  /// </summary>
  private void ApplyRightD3FromMapGeometry()
  {
    if (layout == null || layout.Pieces == null)
      return;

    // Locked existing Black Door F3 view: do not alter its RightD3 state.
    if (previewX == 1
        && previewY == 5
        && previewFacing == DungeonFacing.North)
    {
      return;
    }

    EnsurePreviewMiniMapLoaded();
    DungeonMap.GetForwardOffset(
        previewFacing,
        out int forwardX,
        out int forwardY);
    DungeonMap.GetRightOffset(
        previewFacing,
        out int rightX,
        out int rightY);

    int front1X = previewX + forwardX;
    int front1Y = previewY + forwardY;
    int front2X = previewX + forwardX * 2;
    int front2Y = previewY + forwardY * 2;
    int front3X = previewX + forwardX * 3;
    int front3Y = previewY + forwardY * 3;

    bool frontBlocked = previewMiniMap == null
        || !previewMiniMap.IsInside(front1X, front1Y)
        || previewMiniMap.GetTile(front1X, front1Y).Type == DungeonTileType.Wall
        || !previewMiniMap.IsInside(front2X, front2Y)
        || previewMiniMap.GetTile(front2X, front2Y).Type == DungeonTileType.Wall
        || !previewMiniMap.IsInside(front3X, front3Y)
        || previewMiniMap.GetTile(front3X, front3Y).Type == DungeonTileType.Wall;

    bool rightD3IsWall = false;
    if (!frontBlocked)
    {
      int tileX = previewX + forwardX * 3 + rightX;
      int tileY = previewY + forwardY * 3 + rightY;
      rightD3IsWall = previewMiniMap == null
          || !previewMiniMap.IsInside(tileX, tileY)
          || previewMiniMap.GetTile(tileX, tileY).Type == DungeonTileType.Wall;
    }

    // Unique Hall of Champions Black Door oblique view.
    // Visibility is forced here, but position is preview-only below.
    bool blackDoorObliqueException =
        previewX == 0
        && previewY == 5
        && previewFacing == DungeonFacing.North;

    bool enabled = rightD3IsWall || blackDoorObliqueException;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "Wall D3R2" && piece.Name != "RightD3")
        continue;

      piece.Enabled = enabled;
      return;
    }
  }

  /// <summary>
  /// FrontF1 / Front Wall F1 from the map tile one step directly forward.
  /// Solid/out-of-bounds → Enabled. Does not change side walls, FrontF2/F3, width, or mirrors.
  /// </summary>
  // LEGACY FRONT-WALL PATH: retained temporarily for reference; not called by cutover pipeline.
  private void ApplyFrontF1FromMapGeometry()
  {
    if (layout == null || layout.Pieces == null)
      return;

    EnsurePreviewMiniMapLoaded();

    bool frontF1IsWall = true;
    if (previewMiniMap != null)
    {
      RelativeViewportGeometry geometry =
          RelativeViewportGeometry.Calculate(
              previewMiniMap,
              previewX,
              previewY,
              previewFacing);

      frontF1IsWall = geometry.F1Center.IsWall;
    }

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "FrontF1" && piece.Name != "Front Wall F1")
        continue;

      piece.Enabled = frontF1IsWall;
      return;
    }
  }

  /// <summary>
  /// FrontF3 / Front Wall F3 from the map tile three steps forward, only if
  /// both nearer forward tiles are open. A solid F1 or F2 wall hides FrontF3.
  /// </summary>
  private void ApplyFrontF3FromMapGeometry()
  {
    if (layout == null || layout.Pieces == null)
      return;

    // (1,4) North is a Black Door F2 front view and (1,5) North is a
    // Black Door F3 front view. Force the normal FrontF3 wall off so
    // ViewEdit matches the dedicated Black Door front view.
    if (previewX == 1
        && (previewY == 4 || previewY == 5)
        && previewFacing == DungeonFacing.North)
    {
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null)
          continue;

        if (piece.Name != "FrontF3" && piece.Name != "Front Wall F3")
          continue;

        piece.Enabled = false;
        return;
      }

      return;
    }

    EnsurePreviewMiniMapLoaded();
    DungeonMap.GetForwardOffset(
        previewFacing,
        out int forwardX,
        out int forwardY);

    int front1X = previewX + forwardX;
    int front1Y = previewY + forwardY;
    int front2X = previewX + forwardX * 2;
    int front2Y = previewY + forwardY * 2;
    bool nearerBlocked = previewMiniMap == null
        || !previewMiniMap.IsInside(front1X, front1Y)
        || previewMiniMap.GetTile(front1X, front1Y).Type == DungeonTileType.Wall
        || !previewMiniMap.IsInside(front2X, front2Y)
        || previewMiniMap.GetTile(front2X, front2Y).Type == DungeonTileType.Wall;

    bool frontF3IsWall = false;
    if (!nearerBlocked)
    {
      int tileX = previewX + forwardX * 3;
      int tileY = previewY + forwardY * 3;
      frontF3IsWall = previewMiniMap == null
          || !previewMiniMap.IsInside(tileX, tileY)
          || previewMiniMap.GetTile(tileX, tileY).Type == DungeonTileType.Wall;
    }

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "FrontF3" && piece.Name != "Front Wall F3")
        continue;

      piece.Enabled = frontF3IsWall;
      return;
    }
  }

  /// <summary>
  /// FrontF1/F2 mirror phase from the visible normal-wall configuration only.
  /// Absolute map X/Y, facing parity, and hidden map cells do not participate.
  /// Identical visible normal-wall configurations therefore get the same phase.
  /// </summary>
  private void ApplyFrontF1F2MirrorFromVisibleWallConfiguration()
  {
    if (layout == null || layout.Pieces == null)
      return;

    int signature = 17;
    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      bool isNormalWall =
          IsWallF0LeftPiece(piece)
          || IsWallF0RightPiece(piece)
          || IsWallF1LeftPiece(piece)
          || IsWallF1RightPiece(piece)
          || IsWallF2LeftPiece(piece)
          || IsWallF2RightPiece(piece)
          || IsWallF3LeftPiece(piece)
          || IsWallF3RightPiece(piece)
          || IsFrontWallF1Card(piece)
          || IsFrontWallF2Card(piece)
          || IsFrontWallF3Card(piece);

      if (!isNormalWall)
        continue;

      signature = unchecked(signature * 31 + (piece.Enabled ? 1 : 0));
    }

    bool mirror = (signature & 1) != 0;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (IsFrontWallF1Card(piece) || IsFrontWallF2Card(piece))
        piece.MirrorHorizontally = mirror;
    }
  }

  /// <summary>
  /// Black Door ViewEdit Enabled exceptions. Does not change blit or other poses.
  /// (1,4) North: F2 view — F1 off, F2 on.
  /// (1,3) North: F1 view — F1 on, F2 off, F3 off.
  /// (1,5) North: F3 view — F1 off, F2 off, F3 on.
  /// </summary>
  private void ApplyBlackDoorEnabledFromPoseException()
  {
    if (layout == null || layout.Pieces == null)
      return;

    if (previewFacing != DungeonFacing.North || previewX != 1)
      return;

    if (previewY == 4)
    {
      // Initialize the ViewEdit F2 card once for this editor session.
      // Do not force it back on every preview compose, otherwise the
      // user's Enabled toggle is immediately overwritten.
      if (!blackDoorF2CardInitialized)
      {
        blackDoorF2CardEnabled = true;
        blackDoorF2CardInitialized = true;
      }
      blackDoorFrameLeftF3CardEnabled = false;
      blackDoorFrameRightF3CardEnabled = false;

      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null)
          continue;

        if (piece.Name == "Black Door Frame Left F2"
            || piece.Name == "Black Door Frame Right F2")
        {
          piece.Enabled = true;
        }

        // Black Door F2 occupies the front view at (1,4) North. Any normal
        // FrontF3 card/graphic must stay disabled so it cannot appear behind
        // the dedicated door view.
        if (IsFrontWallF3Card(piece)
            || piece.Graphic == DungeonGraphicType.FrontWallF3)
        {
          piece.Enabled = false;
        }
      }

      return;
    }

    if (previewY == 3)
    {
      blackDoorF2CardEnabled = false;
      blackDoorF2CardInitialized = true;
      blackDoorF3CardEnabled = false;
      blackDoorF3CardInitialized = true;
      blackDoorFrameLeftF3CardEnabled = false;
      blackDoorFrameRightF3CardEnabled = false;

      // Verified Black Door F1 layout for the 1,3 North front-door view.
      // Display coordinates:
      //   Left frame  X=44  Y=46
      //   Right frame X=154 Y=46
      //   Door        X=63  Y=47
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null)
          continue;

        if (piece.Name == "Black Door Frame Left F1")
        {
          if (!blackDoorFrameLeftF1EnabledInitialized)
          {
            piece.Enabled = true;
            blackDoorFrameLeftF1EnabledInitialized = true;
          }

          piece.X = 44;
          piece.Y = DisplayYToUnityY(46, 94);
        }
        else if (piece.Name == "Black Door Frame Right F1")
        {
          if (!blackDoorFrameRightF1EnabledInitialized)
          {
            piece.Enabled = true;
            blackDoorFrameRightF1EnabledInitialized = true;
          }

          piece.X = 154;
          piece.Y = DisplayYToUnityY(46, 94);
        }
        else if (piece.Name == "BlackDoorF1")
        {
          if (!blackDoorF1EnabledInitialized)
          {
            piece.Enabled = true;
            blackDoorF1EnabledInitialized = true;
          }

          piece.X = 63;
          piece.Y = DisplayYToUnityY(47, 88);
        }

        // Black Door F1 occupies the front opening at (1,3) North.
        // Keep the normal FrontF2 wall disabled for this dedicated door view.
        if (IsFrontWallF2Card(piece)
            || FrontWallF2Logic.IsFrontWallF2Graphic(piece.Graphic))
        {
          piece.Enabled = false;
        }
      }

      return;
    }

    if (previewY != 5)
      return;

    blackDoorF2CardEnabled = false;
    blackDoorF2CardInitialized = true;
    blackDoorF3CardEnabled = true;
    blackDoorF3CardInitialized = true;
    blackDoorFrameLeftF3CardEnabled = true;
    blackDoorFrameRightF3CardEnabled = true;

    // Initialize the visible Left F3 frame layout card once. After this,
    // its Enabled checkbox is authoritative and pose refreshes preserve it.
    if (!blackDoorFrameLeftF3EnabledInitialized)
    {
      ViewportPiece leftF3Frame = FindLayoutPieceByName("Black Door Frame Left F3");
      if (leftF3Frame != null)
        leftF3Frame.Enabled = true;

      blackDoorFrameLeftF3EnabledInitialized = true;
    }

    // Initialize the visible Right F3 frame layout card once. After this,
    // its Enabled checkbox is authoritative and pose refreshes preserve it.
    if (!blackDoorFrameRightF3EnabledInitialized)
    {
      ViewportPiece rightF3Frame = FindLayoutPieceByName("Black Door Frame Right F3");
      if (rightF3Frame != null)
        rightF3Frame.Enabled = true;

      blackDoorFrameRightF3EnabledInitialized = true;
    }

    // Black Door F3 occupies the front view at (1,5) North. Keep any
    // normal FrontF3 card/graphic disabled so it cannot render behind it.
    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (IsFrontWallF3Card(piece)
          || piece.Graphic == DungeonGraphicType.FrontWallF3)
      {
        piece.Enabled = false;
      }
    }

  }


  private bool[] CaptureWorkingEnabledFlags()
  {
    if (layout == null || layout.Pieces == null)
      return System.Array.Empty<bool>();

    bool[] flags = new bool[layout.Pieces.Count];
    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      flags[i] = piece != null && piece.Enabled;
    }

    return flags;
  }

  private void ApplyWorkingEnabledFlags(bool[] flags)
  {
    if (layout == null || layout.Pieces == null || flags == null)
      return;

    int count = Mathf.Min(flags.Length, layout.Pieces.Count);
    for (int i = 0; i < count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece != null)
        piece.Enabled = flags[i];
    }
  }

  private void ApplyKitBaselineEnabledToLayout()
  {
    if (layout == null || layout.Pieces == null)
      return;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name == "BlackDoorF1"
          || piece.Name == "BlackDoorF2"
          || piece.Name == "BlackDoorF3"
          || piece.Name == "Black Door Frame Left F1"
          || piece.Name == "Black Door Frame Right F1"
          || piece.Name == "Black Door Frame Left F3"
          || piece.Name == "Black Door Frame Right F3")
        continue;

      bool found = false;
      for (int b = 0; b < KitBaselineEnabled.Length; b++)
      {
        if (KitBaselineEnabled[b].Name == piece.Name)
        {
          piece.Enabled = KitBaselineEnabled[b].Enabled;
          found = true;
          break;
        }
      }

      // Unknown kit pieces must not keep a prior pose's Enabled.
      if (!found)
        piece.Enabled = false;
    }
  }

  /// <summary>
  /// Safe live defaults when a pose has no store entry: kit Enabled (or false)
  /// and MirrorHorizontally=false so the previous pose cannot leak.
  /// Does not write the store.
  /// </summary>
  private void ApplyUnknownPoseDefaultsToLayout()
  {
    ApplyKitBaselineEnabledToLayout();
    if (layout == null || layout.Pieces == null)
      return;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      piece.MirrorHorizontally = false;
      piece.PoseOffsetX = 0;
      piece.PoseOffsetY = 0;
      if (StraightF1WallLogic.IsStraightF1FrontGraphic(piece.Graphic))
      {
        piece.FrontWallF1Width =
            StraightF1WallLogic.DefaultFrontWallF1Width;
      }

      if (FrontWallF2Logic.IsFrontWallF2Graphic(piece.Graphic))
      {
        piece.FrontWallF2Width = FrontWallF2Logic.DefaultWidth;
      }
    }
  }

  private void SwapPieces(int indexA, int indexB)
  {
    Undo.RecordObject(layout, "Viewport Layout Reorder");

    ViewportPiece temp = layout.Pieces[indexA];
    layout.Pieces[indexA] = layout.Pieces[indexB];
    layout.Pieces[indexB] = temp;
  }

  private void RefreshEditModePreview()
  {
    if (Application.isPlaying)
      return;

    if (layout == null)
    {
      RestoreViewportTextureAndDestroyPreview();
      RepaintGameViews();
      return;
    }

    if (graphics == null)
    {
      RestoreViewportTextureAndDestroyPreview();
      RepaintGameViews();
      // Still emit Console from previewFacing so it cannot lag the minimap.
      LogCurrentViewportStateToConsole();
      return;
    }

    // Drop any stale RawImage cache — hierarchy/presentation may have changed.
    cachedViewportImage = null;

    RawImage dungeonImage = FindLiveDungeonViewportRawImage();
    if (dungeonImage == null)
    {
      LogCurrentViewportStateToConsole();
      return;
    }

    cachedViewportImage = dungeonImage;

    // Recreate the preview Texture2D every refresh so RawImage/Canvas cannot
    // keep showing a stale GPU copy of an in-place updated texture.
    DestroyEditModePreviewTextureOnly();
    EnsureEditModePreviewTexture();
    ComposeEditModePreview();
    if (editModePreviewTexture == null)
    {
      LogCurrentViewportStateToConsole();
      return;
    }

    editModePreviewTexture.Apply(false);

    StealViewportTextureIfNeeded(dungeonImage);
    ApplyExact320x200EditModePresentation(dungeonImage);

    // Presentation may rebuild hierarchy — resolve the live RawImage again.
    dungeonImage = FindLiveDungeonViewportRawImage();
    if (dungeonImage == null)
    {
      LogCurrentViewportStateToConsole();
      return;
    }

    cachedViewportImage = dungeonImage;

    // Force a reference change so RawImage/Canvas pick up the new Texture2D.
    dungeonImage.texture = Texture2D.whiteTexture;
    dungeonImage.texture = editModePreviewTexture;
    if (dungeonImage.canvas != null)
      Canvas.ForceUpdateCanvases();

    MaintainMovementArrowsPreview();
    RepaintGameViews();

    // After pose visibility/mirror apply (+ compose when Game View is available).
    LogCurrentViewportStateToConsole();
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

  private static int GetNormalWallRenderDepth(ViewportPiece piece)
  {
    if (piece == null)
      return -1;

    // Higher number = farther from the player. Compose draws far -> near.
    if (IsWallF3LeftPiece(piece)
        || IsWallF3RightPiece(piece)
        || IsFrontWallF3Card(piece)
        || piece.Graphic == DungeonGraphicType.WallD3L2
        || piece.Graphic == DungeonGraphicType.WallD3R2
        || piece.Name == "LeftD3"
        || piece.Name == "RightD3"
        || piece.Name == "Wall D3L2"
        || piece.Name == "Wall D3R2")
    {
      return 3;
    }

    if (IsWallF2LeftPiece(piece)
        || IsWallF2RightPiece(piece)
        || IsFrontWallF2Card(piece))
    {
      return 2;
    }

    if (IsWallF1LeftPiece(piece)
        || IsWallF1RightPiece(piece)
        || IsFrontWallF1Card(piece))
    {
      return 1;
    }

    if (IsWallF0LeftPiece(piece) || IsWallF0RightPiece(piece))
      return 0;

    return -1;
  }

  private void ComposeEditModePreview()
  {
    EnsureEditModePreviewTexture();
    if (editModePreviewTexture == null)
      return;

    Color32 magenta = new Color32(255, 0, 255, 255);
    Color32[] pixels = new Color32[PreviewWidth * PreviewHeight];
    for (int i = 0; i < pixels.Length; i++)
      pixels[i] = magenta;

    // Temporary pose for visibility/mirror only — never write the layout asset.
    DungeonMap poseMap = TryGetPreviewPoseMap();

    if (layout != null && layout.Pieces != null)
    {
      bool is14South =
          previewX == 1
          && previewY == 4
          && previewFacing == DungeonFacing.South;
      bool leftF0OverlapArmed = false;
      int leftF0OverlapX = 0;
      int leftF0OverlapY = 0;
      int leftF0OverlapW = 0;
      int leftF0OverlapH = 0;

      void LogIfOverlapsLeftF0(
          ViewportPiece laterPiece,
          DungeonGraphicType laterGraphic,
          int laterX,
          int laterY,
          int laterW,
          int laterH)
      {
        if (!leftF0OverlapArmed
            || laterPiece == null
            || IsWallF0LeftPiece(laterPiece)
            || laterW <= 0
            || laterH <= 0)
          return;

        bool overlaps =
            laterX < leftF0OverlapX + leftF0OverlapW
            && laterX + laterW > leftF0OverlapX
            && laterY < leftF0OverlapY + leftF0OverlapH
            && laterY + laterH > leftF0OverlapY;
        if (!overlaps)
          return;

        Debug.Log(
            "LEFTF0 OVERLAP | "
            + laterPiece.Name
            + " | "
            + laterGraphic
            + " | X=" + laterX
            + " Y=" + laterY
            + " | "
            + laterW + "x" + laterH);
      }

      // Draw normal walls by physical depth (far -> near) without changing
      // layout.Pieces itself. Non-wall pieces keep their original slots/order.
      List<ViewportPiece> orderedNormalWalls = new List<ViewportPiece>();
      bool hasLiveFrontF3Card = false;
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece candidate = layout.Pieces[i];
        if (candidate != null && candidate.Name == "FrontF3")
          hasLiveFrontF3Card = true;
        if (IsNormalWallPiece(candidate))
          orderedNormalWalls.Add(candidate);
      }

      if (previewX == 5
          && previewY == 2
          && previewFacing == DungeonFacing.South)
      {
        orderedNormalWalls.Sort(
            (a, b) => Get52SouthLeftToRightOrder(a).CompareTo(
                Get52SouthLeftToRightOrder(b)));
      }
      else
      {
        orderedNormalWalls.Sort(
            (a, b) => GetNormalWallRenderDepth(b).CompareTo(
                GetNormalWallRenderDepth(a)));
      }

      int nextNormalWall = 0;
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (IsNormalWallPiece(piece))
          piece = orderedNormalWalls[nextNormalWall++];

        if (previewDisableAllWalls
            && piece != null
            && !IsDisableWallsKeeper(piece))
        {
          continue;
        }

        if (hasLiveFrontF3Card
            && piece != null
            && piece.Name == "Front Wall F3")
        {
          continue;
        }

        // Final early draw gate for verified ViewEdit pose (5,2) South.
        // Reject disabled normal-wall/D3 pieces before any special blit path
        // (especially the FrontF2 224-reference blit) can run.
        if (piece != null
            && previewX == 5
            && previewY == 2
            && previewFacing == DungeonFacing.South
            && !IsShowAllWallsPreview()
            && IsNormalWallPiece(piece)
            && TryGet52SouthForcedWallEnabled(piece, out bool forced52EarlyEnabled)
            && !forced52EarlyEnabled)
        {
          continue;
        }

        // (1,4) North is the Black Door F2 front view and (1,5) North is
        // the Black Door F3 front view. The normal FrontF3 wall must never
        // render behind/through either dedicated door view.
        if (previewX == 1
            && (previewY == 4 || previewY == 5)
            && previewFacing == DungeonFacing.North
            && piece != null
            && (IsFrontWallF3Card(piece)
                || piece.Graphic == DungeonGraphicType.FrontWallF3))
        {
          continue;
        }

        // TEMP isolation test for exactly (0,5) South:
        // Ceiling + Floor + FrontF1 + FrontF3 + RightF0 only.
        bool isolateFrontF1At05South =
            previewX == 0
            && previewY == 5
            && previewFacing == DungeonFacing.South;

        if (isolateFrontF1At05South
            && (piece == null
                || (!IsFloorOrCeiling(piece)
                    && !IsChampionStatusSlotPiece(piece)
                    && !IsFrontWallF1Card(piece)
                    && !IsFrontWallF3Card(piece)
                    && !IsWallF0RightPiece(piece)
                    && piece.Name != "LeftS2")))
        {
          continue;
        }

        bool isLeftF0Diag = is14South && IsWallF0LeftPiece(piece);
        bool shouldDraw = ShouldDrawPieceAtPreviewPose(piece);
        bool blackDoorF1Exception = IsBlackDoorF1PoseException(piece);
        bool blackDoorF2Exception = IsBlackDoorF2PoseException(piece);
        bool blackDoorF3Exception = IsBlackDoorF3PoseException(piece);
        bool blackDoorObliqueRightD3Exception =
            IsBlackDoorObliqueRightD3PoseException(piece)
            && (!previewEnabledOverrideByPiece.TryGetValue(
                    piece, out bool manualExceptionEnabled)
                || manualExceptionEnabled);

        bool manualNormalWallEnabledForDraw =
            IsNormalWallPiece(piece)
            && previewEnabledOverrideByPiece.TryGetValue(
                piece, out bool manualWallEnabledForDraw)
            && manualWallEnabledForDraw;

        if (!shouldDraw
            && !manualNormalWallEnabledForDraw
            && !blackDoorF1Exception
            && !blackDoorF2Exception
            && !blackDoorF3Exception
            && !blackDoorObliqueRightD3Exception
            && !(isolateFrontF1At05South
                && (IsFrontWallF3Card(piece) || IsWallF0RightPiece(piece))))
        {
          continue;
        }

        if ((piece.Name == "Black Door Frame Left F2"
                || piece.Name == "Black Door Frame Right F2")
            && (previewX != 1
                || previewY != 4
                || previewFacing != DungeonFacing.North))
          continue;

        if (piece.Graphic == DungeonGraphicType.MovementArrows)
          continue;

        // Black Door F1 frame pieces are explicitly drawn immediately after
        // the door below, so skip their normal list-order draw at this pose.
        if (previewX == 1
            && previewY == 3
            && previewFacing == DungeonFacing.North
            && (piece.Name == "Black Door Frame Left F1"
                || piece.Name == "Black Door Frame Right F1"))
        {
          continue;
        }

        // (1,3) North Black Door F1 occupies the center opening.
        // Do not draw the normal FrontF2 wall through the door.
        if (previewX == 1
            && previewY == 3
            && previewFacing == DungeonFacing.North
            && (IsFrontWallF2Card(piece)
                || FrontWallF2Logic.IsFrontWallF2Graphic(piece.Graphic)))
        {
          continue;
        }

        // CUTOVER: render transient geometry assembly for normal walls.
        bool mirror = GetPreviewMirror(piece, poseMap);
        // ViewEdit Mirror remains a temporary manual test for every normal wall,
        // including F2/F3 pieces that are not in the current cutover resolver.
        if (previewMirrorOverrideByPiece.TryGetValue(piece, out bool manualPreviewMirror))
          mirror = manualPreviewMirror;
        DungeonGraphicType drawGraphic = piece.Graphic;
        int resolvedX = piece.EffectiveX;
        int resolvedY = piece.EffectiveY;
        int resolvedF1Width =
            StraightF1WallLogic.NormalizeFrontWallF1Width(
                piece.FrontWallF1Width);

        if (TryGetResolvedNormalWallState(
                piece,
                out ResolvedNormalWallState resolvedWall))
        {
          if (isLeftF0Diag)
            Debug.Log(
                "LEFTF0 DIAG | "
                + piece.Name
                + " | resolvedWall.Enabled="
                + resolvedWall.Enabled);

          bool resolvedEnabled = resolvedWall.Enabled;
          if (previewEnabledOverrideByPiece.TryGetValue(
                  piece, out bool manualNormalWallEnabled))
          {
            resolvedEnabled = manualNormalWallEnabled;
          }
          else if (IsFrontWallF1Card(piece)
              && TryGetFrontF1PreviewEnabledOverride(
                  out bool frontF1EnabledOverride))
          {
            resolvedEnabled = frontF1EnabledOverride;
          }

          // For (5,2) South, only disallowed pieces are forced OFF.
          // Allowed pieces keep their ViewEdit Enabled override.
          if (!IsShowAllWallsPreview()
              && TryGet52SouthForcedWallEnabled(piece, out bool forced52DrawEnabled)
              && !forced52DrawEnabled)
            resolvedEnabled = false;

          if (!resolvedEnabled
              && !(isolateFrontF1At05South && IsFrontWallF3Card(piece)))
            continue;

          // Geometry supplies the normal orientation. A ViewEdit-only checkbox
          // override may temporarily replace it until the preview pose changes.
          mirror = resolvedWall.Mirror;
          if (previewMirrorOverrideByPiece.TryGetValue(piece, out bool previewMirror))
            mirror = previewMirror;
          drawGraphic = resolvedWall.Graphic;
          if (previewGraphicOverrideByPiece.TryGetValue(
                  piece, out DungeonGraphicType previewGraphic))
            drawGraphic = previewGraphic;
          if (isLeftF0Diag)
            Debug.Log("LEFTF0 DIAG | drawGraphic=" + drawGraphic);
          if (!IsWallF0LeftPiece(piece) && !IsWallF0RightPiece(piece)
              && !IsWallF1LeftPiece(piece) && !IsWallF1RightPiece(piece)
              && !IsWallF2LeftPiece(piece) && !IsWallF2RightPiece(piece)
              && !IsWallF3LeftPiece(piece) && !IsWallF3RightPiece(piece))
          {
            resolvedX = resolvedWall.X;
            resolvedY = resolvedWall.Y;
            if (previewPositionOverrideByPiece.TryGetValue(piece, out Vector2Int previewPosition))
            {
              resolvedX = previewPosition.x;
              resolvedY = previewPosition.y;
            }
          }
          resolvedF1Width = resolvedWall.FrontF1Width;
        }

        if (IsFrontWallF1Card(piece)
            && previewFrontF1WidthOverrideByPiece.TryGetValue(
                piece, out int manualFrontF1Width))
        {
          resolvedF1Width =
              StraightF1WallLogic.NormalizeFrontWallF1Width(
                  manualFrontF1Width);
        }

        // Side-wall dest is override else that piece's canonical Ref.
        // Mirror flips source pixels only; it never changes X/Y.
        if (IsWallF0LeftPiece(piece) || IsWallF0RightPiece(piece)
            || IsWallF1LeftPiece(piece) || IsWallF1RightPiece(piece)
            || IsWallF2LeftPiece(piece) || IsWallF2RightPiece(piece)
            || IsWallF3LeftPiece(piece) || IsWallF3RightPiece(piece))
        {
          if (previewPositionOverrideByPiece.TryGetValue(
                  piece, out Vector2Int sideWallOverride))
          {
            resolvedX = sideWallOverride.x;
            resolvedY = sideWallOverride.y;
          }
          else if (TryGetCanonicalReferenceXY(
                       piece.Name, out int sideRefX, out int sideRefY)
                   || (TryGetSideWallCanonicalName(piece, out string sideCanonicalName)
                       && TryGetCanonicalReferenceXY(
                           sideCanonicalName, out sideRefX, out sideRefY)))
          {
            resolvedX = sideRefX;
            resolvedY = DisplayYToUnityY(
                sideRefY, GetPieceHeightForEditorY(piece));
          }

          if (!previewMirrorOverrideByPiece.ContainsKey(piece))
            mirror = GetSideWallMirrorFromPose();
        }

        // Temporary ViewEdit tests always win over canonical / pose
        // mirror for this stationary preview. Override Current Walls commits.
        bool hasForced52LiveEnabled =
            TryGet52SouthForcedWallEnabled(piece, out bool forced52LiveEnabled);
        if (previewEnabledOverrideByPiece.TryGetValue(
                piece, out bool livePreviewEnabled)
            && IsNormalWallPiece(piece)
            && !livePreviewEnabled
            && (!hasForced52LiveEnabled || forced52LiveEnabled))
        {
          continue;
        }
        if (IsFrontWallF1Card(piece)
            && TryGetFrontF1PreviewMirrorOverride(
                piece, out bool frontF1PreviewMirror))
        {
          mirror = frontF1PreviewMirror;
        }
        else if (previewMirrorOverrideByPiece.TryGetValue(
                piece, out bool livePreviewMirror))
        {
          mirror = livePreviewMirror;
        }
        if (IsFrontWallF1Card(piece)
            && TryGetFrontF1PreviewPositionOverride(
                piece, out Vector2Int frontF1PreviewPosition))
        {
          resolvedX = frontF1PreviewPosition.x;
          resolvedY = frontF1PreviewPosition.y;
        }
        else if (previewPositionOverrideByPiece.TryGetValue(
                piece, out Vector2Int livePreviewPosition))
        {
          resolvedX = livePreviewPosition.x;
          resolvedY = livePreviewPosition.y;
        }
        if (IsFrontWallF1Card(piece)
            && TryGetFrontF1PreviewWidthOverride(
                piece, out int livePreviewWidth))
        {
          resolvedF1Width = livePreviewWidth;
        }

        // Final canonical authority for the verified 1,5 North LeftF1 view.
        // This is deliberately after geometry resolution/canonical/temporary resolution so
        // no older stored or preview value can move the final blit.
        if (IsWallF1LeftPiece(piece)
            && previewX == 1
            && previewY == 5
            && previewFacing == DungeonFacing.North)
        {
          resolvedX = 0;
          resolvedY = DisplayYToUnityY(
              42, GetPieceHeightForEditorY(piece));
          mirror = false;
          drawGraphic = DungeonGraphicType.WallF1L;
        }

        if (IsWallF0LeftPiece(piece) || IsWallF0RightPiece(piece))
        {
          string f0DrawDiagnosticKey =
              previewX + "," + previewY + "," + previewFacing
              + "|" + piece.Name
              + "|" + resolvedX
              + "|" + resolvedY
              + "|" + mirror;
          if (lastLoggedF0DrawDiagnosticKey != f0DrawDiagnosticKey)
          {
            lastLoggedF0DrawDiagnosticKey = f0DrawDiagnosticKey;
            Debug.Log(
                "F0 DRAW | "
                + previewX + "," + previewY + " " + previewFacing.ToString().ToUpperInvariant()
                + " | " + piece.Name
                + " | X=" + resolvedX
                + " | Y=" + resolvedY
                + " | mirror=" + (mirror ? "ON" : "OFF"));
          }
        }

        // LeftS2 / Right2S must be handled before any generic front/side wall
        // graphic path.  The opposite 2S artwork is a source-image substitute
        // only: X/Y always stay with the card being drawn, and Mirror=true
        // must still flip the substituted source pixels.
        if (piece.Name == "LeftS2" || piece.Name == "Right2S")
        {
          Texture2D wall2STexture = piece.Name == "LeftS2"
              ? (mirror
                  ? GetRight2STexture()
                  : graphics.GetTexture(DungeonGraphicType.Left2S))
              : (mirror
                  ? graphics.GetTexture(DungeonGraphicType.Left2S)
                  : GetRight2STexture());

          if (wall2STexture == null)
            continue;

          BlitPieceIntoPreview(
              pixels,
              wall2STexture,
              resolvedX,
              resolvedY,
              mirror);
          LogIfOverlapsLeftF0(
              piece,
              piece.Graphic,
              resolvedX,
              resolvedY,
              wall2STexture.width,
              wall2STexture.height);
          continue;
        }

        if (StraightF1WallLogic.IsStraightF1FrontGraphic(piece.Graphic))
        {
          int width = resolvedF1Width;
          int frontF1TextureHeight = StraightF1WallLogic.CompositeHeight;

          if (previewX == 0
              && previewY == 5
              && previewFacing == DungeonFacing.South)
          {
            width = StraightF1WallLogic.CompositeWidth;
            Texture2D f1Texture = graphics.GetFrontWallF1Texture(width);
            if (f1Texture == null)
            {
              Debug.LogError(
                  "FrontF1 0,5 South: required 224x111 source texture is missing.");
              continue;
            }

            frontF1TextureHeight = f1Texture.height;

            if (frontF1CropPreview)
            {
              // GameView dest X is independent of the mirrored-image start X.
              int destinationStartX = 32;
              int sourceStartX = 32;
              int copyWidth =
                  StraightF1WallLogic.CompositeWidth - sourceStartX;

              BlitFrontF1MirroredImageFromX(
                  pixels,
                  f1Texture,
                  sourceStartX,
                  destinationStartX,
                  resolvedY);

              LogIfOverlapsLeftF0(
                  piece,
                  piece.Graphic,
                  destinationStartX,
                  resolvedY,
                  copyWidth,
                  f1Texture.height);
            }
            else
            {
              int destinationStartX = 0;
              bool f1Mirror = true;

              StraightF1WallLogic.BlitCompositeToBuffer(
                  f1Texture,
                  pixels,
                  PreviewWidth,
                  PreviewHeight,
                  destinationStartX,
                  resolvedY,
                  f1Mirror,
                  width);

              LogIfOverlapsLeftF0(
                  piece,
                  piece.Graphic,
                  destinationStartX,
                  resolvedY,
                  width,
                  f1Texture.height);
            }
          }
          else if (TryGetCurrentRelativeViewportGeometry(out RelativeViewportGeometry currentGeometry)
              && IsLeftD3ObliqueOpening(currentGeometry))
          {
            // LeftD3 composition: keep the left 32 screen pixels free for LeftD3.
            // FrontF1 starts at source X=32 and is drawn at destination X=32.
            Texture2D fullF1Texture =
                graphics.GetFrontWallF1Texture(StraightF1WallLogic.CompositeWidth);
            if (fullF1Texture == null)
              continue;

            frontF1TextureHeight = fullF1Texture.height;
            const int leftD3FrontF1StartX = 32;

            bool rightD3AlsoActive =
                !currentGeometry.F0Left.IsWall
                && !currentGeometry.F0Right.IsWall
                && currentGeometry.F1Center.IsWall
                && !currentGeometry.F1Right.IsWall;

            int leftD3FrontF1CopyWidth =
                rightD3AlsoActive
                    ? StraightF1WallLogic.CompositeWidth160
                    : StraightF1WallLogic.CompositeWidth191;

            if (mirror)
            {
              BlitFrontF1MirroredImageFromX(
                  pixels,
                  fullF1Texture,
                  leftD3FrontF1StartX,
                  leftD3FrontF1StartX,
                  resolvedY,
                  leftD3FrontF1CopyWidth);
            }
            else
            {
              BlitFrontF1CroppedPreview(
                  pixels,
                  fullF1Texture,
                  leftD3FrontF1StartX,
                  leftD3FrontF1StartX,
                  resolvedY,
                  false,
                  leftD3FrontF1CopyWidth);
            }

            LogIfOverlapsLeftF0(
                piece,
                piece.Graphic,
                leftD3FrontF1StartX,
                resolvedY,
                leftD3FrontF1CopyWidth,
                fullF1Texture.height);
          }
          else if (frontF1CropPreview)
          {
            Texture2D fullF1Texture =
                graphics.GetFrontWallF1Texture(StraightF1WallLogic.CompositeWidth);
            if (fullF1Texture == null)
              continue;

            frontF1TextureHeight = fullF1Texture.height;

            // Crop X defines both the first source column and first screen column.
            // Example: Crop X=32 -> source 32..223 -> screen 32..223.
            // In Crop ON mode, normal X is not used; Y still uses resolvedY.
            int cropStartX = Mathf.Clamp(
                frontF1CropStartXPreview,
                0,
                StraightF1WallLogic.CompositeWidth - 1);
            int cropDestinationX = cropStartX;
            if (previewX == 1
                && previewY == 7
                && previewFacing == DungeonFacing.South)
            {
              cropDestinationX = 0;
            }
            bool cropMirror = mirror;

            int cropWidth = StraightF1WallLogic.CompositeWidth - cropStartX;

            BlitFrontF1CroppedPreview(
                pixels,
                fullF1Texture,
                cropStartX,
                cropDestinationX,
                resolvedY,
                cropMirror);

            LogIfOverlapsLeftF0(
                piece,
                piece.Graphic,
                cropDestinationX,
                resolvedY,
                cropWidth,
                fullF1Texture.height);
          }
          else
          {
            Texture2D f1Texture = graphics.GetFrontWallF1Texture(width);
            if (f1Texture == null)
              continue;

            frontF1TextureHeight = f1Texture.height;

            // Normal FrontF1 mode: use the resolved left-edge X exactly.
            int f1DestX = resolvedX;
            if (previewX == 1
                && previewY == 7
                && previewFacing == DungeonFacing.South)
            {
              f1DestX = 0;
            }

            if (mirror && width < StraightF1WallLogic.CompositeWidth)
            {
              // A narrow mirrored FrontF1 must reflect the same columns it
              // would draw unmirrored. Mirroring the full composite first and
              // then copying keeps the selected window; passing the narrow
              // width straight to the composite blit would instead sample the
              // opposite end of the 224px source.
              int mirroredSourceStartX =
                  StraightF1WallLogic.CompositeWidth - width;

              BlitFrontF1MirroredImageFromX(
                  pixels,
                  f1Texture,
                  mirroredSourceStartX,
                  f1DestX,
                  resolvedY,
                  width);
            }
            else
            {
              StraightF1WallLogic.BlitCompositeToBuffer(
                  f1Texture,
                  pixels,
                  PreviewWidth,
                  PreviewHeight,
                  f1DestX,
                  resolvedY,
                  mirror,
                  width);
            }

            LogIfOverlapsLeftF0(
                piece,
                piece.Graphic,
                f1DestX,
                resolvedY,
                f1Texture.width,
                f1Texture.height);
          }
          ClearFrontWallOverflowIntoUi(
              pixels,
              resolvedY,
              frontF1TextureHeight);
          continue;
        }

        if (FrontWallF2Logic.IsFrontWallF2Graphic(piece.Graphic))
        {
          Texture2D f2Texture = GetFrontWallF2_224ReferenceTexture();
          if (f2Texture == null)
            continue;

          BlitPieceIntoPreview(
              pixels,
              f2Texture,
              resolvedX,
              resolvedY,
              mirror);
          LogIfOverlapsLeftF0(
              piece,
              piece.Graphic,
              resolvedX,
              resolvedY,
              f2Texture.width,
              f2Texture.height);

          ClearFrontWallOverflowIntoUi(
              pixels,
              resolvedY,
              f2Texture.height);
          continue;
        }

        if (IsWallF0LeftPiece(piece) && mirror)
          drawGraphic = DungeonGraphicType.WallF0R;
        else if (IsWallF0RightPiece(piece) && mirror)
          drawGraphic = DungeonGraphicType.WallF0L;
        else if (IsWallF1LeftPiece(piece) && mirror)
          drawGraphic = DungeonGraphicType.WallF1R;
        else if (IsWallF1RightPiece(piece) && mirror)
          drawGraphic = DungeonGraphicType.WallF1L;
        else if (IsWallF2LeftPiece(piece) && mirror)
          drawGraphic = DungeonGraphicType.WallF2R;
        else if (IsWallF2RightPiece(piece) && mirror)
          drawGraphic = DungeonGraphicType.WallF2L;
        else if (IsWallF3LeftPiece(piece) && mirror)
          drawGraphic = DungeonGraphicType.WallF3R;
        else if (IsWallF3RightPiece(piece) && mirror)
          drawGraphic = DungeonGraphicType.WallF3L;

        Texture2D texture = graphics.GetTexture(drawGraphic);

                if (isLeftF0Diag)
        {
          if (texture == null)
          {
            Debug.Log("LEFTF0 DIAG | texture=null");
          }
          else
          {
            Debug.Log(
                "LEFTF0 DIAG | texture="
                + texture.width + "x" + texture.height
                + " | isReadable=" + texture.isReadable);
          }
        }
        if (texture == null)
          continue;

        if (D3R2NarrowWidthTest.ShouldReplace(piece.Graphic))
        {
          bool hasManualRightD3Position =
              previewPositionOverrideByPiece.ContainsKey(piece);
          bool hasManualRightD3Mirror =
              previewMirrorOverrideByPiece.ContainsKey(piece);

          // When RightD3 is being manually tested in ViewEdit, use the normal
          // preview blit so the live X/Y and Mirror controls actually affect
          // what is drawn. With no manual test active, preserve the existing
          // D3R2 narrow-strip rendering exactly as before.
          if (hasManualRightD3Position || hasManualRightD3Mirror)
          {
            BlitPieceIntoPreview(
                pixels,
                texture,
                resolvedX,
                resolvedY,
                mirror);

            LogIfOverlapsLeftF0(
                piece,
                drawGraphic,
                resolvedX,
                resolvedY,
                texture.width,
                texture.height);
          }
          else if (blackDoorObliqueRightD3Exception)
          {
            // ViewEdit display position was measured as X=196, Y=58.
            // D3R2 is 49 px high, so bottom-up framebuffer Y is 200-58-49=93.
            D3R2NarrowWidthTest.BlitToBuffer(
                texture,
                pixels,
                PreviewWidth,
                PreviewHeight,
                196,
                93);
            LogIfOverlapsLeftF0(
                piece,
                drawGraphic,
                196,
                93,
                texture.width,
                texture.height);
          }
          else
          {
            D3R2NarrowWidthTest.BlitToBuffer(
                texture,
                pixels,
                PreviewWidth,
                PreviewHeight,
                piece.EffectiveX,
                piece.EffectiveY);
            LogIfOverlapsLeftF0(
                piece,
                drawGraphic,
                piece.EffectiveX,
                piece.EffectiveY,
                texture.width,
                texture.height);
          }
          continue;
        }

        if (StraightF1WallLogic.IsFloorOrCeilingGraphic(piece.Graphic))
        {
          StraightF1WallLogic.BlitViewportComponentToBuffer(
              texture,
              pixels,
              PreviewWidth,
              PreviewHeight,
              piece.EffectiveX,
              piece.EffectiveY,
              mirror);
          LogIfOverlapsLeftF0(
              piece,
              drawGraphic,
              piece.EffectiveX,
              piece.EffectiveY,
              texture.width,
              texture.height);
          continue;
        }

        // (1,3) North: dedicated Black Door F1 96x88 source.
        // Keep X/Y from the existing BlackDoorF1 ViewEdit/layout piece so we
        // can visually tune placement next without changing the asset.
        if (blackDoorF1Exception)
        {
          // Original layering: draw the left/right F1 frame pieces first,
          // then draw the front Black Door over them.
          // The game only has one F1 frame graphic, so the right frame uses
          // the same left-frame texture mirrored horizontally.
          Texture2D leftFrameSource = GetBlackDoorFrameLeftF1SourceTexture();

          ViewportPiece leftFramePiece =
              FindLayoutPieceByName("Black Door Frame Left F1");
          if (leftFrameSource != null && leftFramePiece != null && leftFramePiece.Enabled)
          {
            BlitPieceIntoPreview(
                pixels,
                leftFrameSource,
                leftFramePiece.EffectiveX,
                leftFramePiece.EffectiveY,
                leftFramePiece.MirrorHorizontally);
          }

          ViewportPiece rightFramePiece =
              FindLayoutPieceByName("Black Door Frame Right F1");
          if (leftFrameSource != null && rightFramePiece != null && rightFramePiece.Enabled)
          {
            BlitPieceIntoPreview(
                pixels,
                leftFrameSource,
                rightFramePiece.EffectiveX,
                rightFramePiece.EffectiveY,
                true);
          }

          if (piece.Enabled)
          {
            Texture2D f1DoorSource = GetBlackDoorF1SourceTexture();
            if (f1DoorSource != null)
            {
              BlitPieceIntoPreview(
                  pixels,
                  f1DoorSource,
                  piece.EffectiveX,
                  piece.EffectiveY,
                  mirror);
              LogIfOverlapsLeftF0(
                  piece,
                  drawGraphic,
                  piece.EffectiveX,
                  piece.EffectiveY,
                  f1DoorSource.width,
                  f1DoorSource.height);
            }
          }

          continue;
        }

        // (1,4) North: F2 frames already blitted in kit order; BlackDoorF2
        // 66×64 1:1 last so it covers overlapping inner frame pixels.
        if (blackDoorF2Exception)
        {
          // 1,4 North: draw the special F2 left/right frame parts first,
          // then draw the dedicated front F2 door over them.
          ViewportPiece leftF2Frame =
              FindLayoutPieceByName("Black Door Frame Left F2");
          if (leftF2Frame != null && leftF2Frame.Enabled)
          {
            Texture2D leftF2FrameSource =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Art/Walls/Black Door Frame_Left_18x65.png");
            if (leftF2FrameSource != null)
            {
              BlitPieceIntoPreview(
                  pixels,
                  leftF2FrameSource,
                  leftF2Frame.EffectiveX,
                  leftF2Frame.EffectiveY,
                  leftF2Frame.MirrorHorizontally);
            }
          }

          ViewportPiece rightF2Frame =
              FindLayoutPieceByName("Black Door Frame Right F2");
          if (rightF2Frame != null && rightF2Frame.Enabled)
          {
            Texture2D rightF2FrameSource =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Art/Walls/Black Door Frame_Left_18x65.png");
            if (rightF2FrameSource != null)
            {
              BlitPieceIntoPreview(
                  pixels,
                  rightF2FrameSource,
                  rightF2Frame.EffectiveX,
                  rightF2Frame.EffectiveY,
                  true);
            }
          }

          // Dedicated front F2 door.
          ViewportPiece doorF2 = FindLayoutPieceByName("BlackDoorF2");
          bool f2DoorEnabled =
              doorF2 != null
                  ? doorF2.Enabled
                  : blackDoorF2CardEnabled;
          if (f2DoorEnabled)
          {
            Texture2D f2Source = GetBlackDoorF2SourceTexture();
            if (f2Source != null)
            {
              int f2DoorX = doorF2 != null
                  ? doorF2.X
                  : piece.ResolvedBlackDoorF2X;
              int f2DoorY = doorF2 != null
                  ? doorF2.Y
                  : piece.ResolvedBlackDoorF2Y;
              bool f2DoorMirror = doorF2 != null
                  ? doorF2.MirrorHorizontally
                  : blackDoorF2CardMirror;

              BlitPieceIntoPreview(
                  pixels,
                  f2Source,
                  f2DoorX,
                  f2DoorY,
                  f2DoorMirror);
              LogIfOverlapsLeftF0(
                  doorF2 ?? piece,
                  drawGraphic,
                  f2DoorX,
                  f2DoorY,
                  f2Source.width,
                  f2Source.height);
            }
          }
          continue;
        }

        // (1,5) North: F3 frames first, then BlackDoorF3 45×39 1:1 on top.
        if (blackDoorF3Exception)
        {
          BlitBlackDoorF3FramesIntoPreview(pixels);
          ViewportPiece doorF3 = FindLayoutPieceByName("BlackDoorF3");
          bool f3DoorEnabled =
              doorF3 != null
                  ? doorF3.Enabled
                  : blackDoorF3CardEnabled;
          int f3X = doorF3 != null ? doorF3.X : blackDoorF3CardX;
          int f3Y = doorF3 != null ? doorF3.Y : blackDoorF3CardY;
          if (f3DoorEnabled)
          {
            Texture2D f3Source = GetBlackDoorF3SourceTexture();
            if (f3Source != null)
            {
              BlitPieceIntoPreview(
                  pixels,
                  f3Source,
                  f3X,
                  f3Y,
                  mirror);
              LogIfOverlapsLeftF0(
                  piece,
                  drawGraphic,
                  f3X,
                  f3Y,
                  f3Source.width,
                  f3Source.height);
            }
          }
          continue;
        }

        string leftF0UnreadableLog = null;
        if (isLeftF0Diag)
        {
          Debug.Log(
              "LEFTF0 BLIT CALLED | X="
              + resolvedX
              + " Y="
              + resolvedY
              + " | mirror="
              + (mirror ? "ON" : "OFF"));
          leftF0UnreadableLog = "LEFTF0 BLIT EXIT isReadable=false";
          leftF0OverlapArmed = true;
          leftF0OverlapX = resolvedX;
          leftF0OverlapY = resolvedY;
          leftF0OverlapW = texture.width;
          leftF0OverlapH = texture.height;
        }

        BlitPieceIntoPreview(
            pixels,
            texture,
            resolvedX,
            resolvedY,
            mirror,
            leftF0UnreadableLog);

        if (!isLeftF0Diag)
        {
          LogIfOverlapsLeftF0(
              piece,
              drawGraphic,
              resolvedX,
              resolvedY,
              texture.width,
              texture.height);
        }

        if (piece.Graphic == DungeonGraphicType.FrontWallF3)
        {
          ClearFrontWallOverflowIntoUi(
              pixels,
              piece.EffectiveY,
              texture.height);
        }
      }

      // Wall rendering is intentionally disabled. No special wall/door blits.
    }

    DungeonBitmapFont bitmapFont = FindEditModeBitmapFont();
    if (bitmapFont != null)
    {
      // First hero-name frame: 43×7 inside Champion Status Slot 1
      // (layout X=0,Y=171). Lighter band is texture top → FB Y 193..199.
      const int frameX = 0;
      const int frameY = 193;
      const int frameWidth = 43;
      const int frameHeight = 7;
      const int localX = -1;
      const int localY = 0;
      const int championNameAdvance = 6;

      Color32 halkGold = new Color32(255, 182, 0, 255);

      bitmapFont.DrawText(
          pixels,
          PreviewWidth,
          PreviewHeight,
          "HALK",
          frameX + localX,
          frameY + localY,
          halkGold,
          frameX,
          frameY,
          frameWidth,
          frameHeight,
          championNameAdvance
      );

      // Same DrawPoseDebugText path as Play/Build comparison mode.
      bitmapFont.DrawPoseDebugText(
          pixels,
          PreviewWidth,
          PreviewHeight,
          previewX,
          previewY,
          previewFacing
      );
    }

    editModePreviewTexture.SetPixels32(pixels);
    editModePreviewTexture.Apply(false);
  }

  private static string lastEditModeViewportLogMessage;
  private static int lastEditLoggedPoseX = int.MinValue;
  private static int lastEditLoggedPoseY = int.MinValue;
  private static DungeonFacing lastEditLoggedPoseFacing =
      (DungeonFacing)(-1);

  /// <summary>
  /// Allow the current Edit Mode POS/wall line to log again after an
  /// external Console clear (e.g. returning from Play Mode).
  /// </summary>
  public static void ResetEditModeViewportLogCache()
  {
    lastEditModeViewportLogMessage = null;
    lastEditLoggedPoseX = int.MinValue;
    lastEditLoggedPoseY = int.MinValue;
    lastEditLoggedPoseFacing = (DungeonFacing)(-1);
  }

  /// <summary>
  /// Console POS/wall line for Edit Mode (same FormatConsoleLine as Play).
  /// Uses live preview pose and Enabled layout pieces. Deduped until cache reset.
  /// Pose key (X/Y/Facing) always refreshes even when wall lists match.
  /// </summary>
  internal void LogCurrentViewportStateToConsole()
  {
    if (layout == null)
      return;

    List<ViewportWallDebugEntry> walls =
        new List<ViewportWallDebugEntry>(12);
    ViewportWallDebugText.CollectEnabledFromLayout(layout, walls);

    string message = ViewportWallDebugText.FormatConsoleLine(
        previewX,
        previewY,
        previewFacing,
        walls
    );

    bool poseChanged =
        previewX != lastEditLoggedPoseX
        || previewY != lastEditLoggedPoseY
        || previewFacing != lastEditLoggedPoseFacing;

    if (!poseChanged && message == lastEditModeViewportLogMessage)
      return;

    lastEditLoggedPoseX = previewX;
    lastEditLoggedPoseY = previewY;
    lastEditLoggedPoseFacing = previewFacing;
    lastEditModeViewportLogMessage = message;
  }

  private static DungeonBitmapFont FindEditModeBitmapFont()
  {
    DungeonBitmapFont[] fonts =
        Resources.FindObjectsOfTypeAll<DungeonBitmapFont>();

    for (int i = 0; i < fonts.Length; i++)
    {
      DungeonBitmapFont font = fonts[i];
      if (font == null)
        continue;

      if (font.AlphabetGrid == null)
        continue;

      if (EditorUtility.IsPersistent(font))
        continue;

      if (!font.gameObject.scene.IsValid() || !font.gameObject.scene.isLoaded)
        continue;

      return font;
    }

    return null;
  }

  private void BlitFrontWallF2_160ExtraStripIntoPreview(
      Color32[] pixels,
      DungeonMap poseMap)
  {
    if (layout == null || layout.Pieces == null || graphics == null)
      return;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (!ShouldDrawPieceAtPreviewPose(piece))
        continue;

      if (!FrontWallF2Logic.IsFrontWallF2Graphic(piece.Graphic))
        continue;

      if (FrontWallF2Logic.Normalize(piece.FrontWallF2Width)
          != FrontWallF2Logic.Width160)
      {
        continue;
      }

      Texture2D f2Texture =
          graphics.GetFrontWallF2Texture(FrontWallF2Logic.Width160);
      if (f2Texture == null || f2Texture.width != FrontWallF2Logic.Width160)
        return;

      FrontWallF2Logic.Blit160ExtraStripToBuffer(
          f2Texture,
          pixels,
          PreviewWidth,
          PreviewHeight,
          piece.EffectiveX,
          piece.EffectiveY,
          GetPreviewMirror(piece, poseMap));
      return;
    }
  }

  /// <summary>
  /// Edit Mode draw gate.
  /// There is no per-view/per-pose visibility store anymore.
  /// </summary>
  private static bool IsWallRenderingPiece(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (IsNormalWallPiece(piece))
      return true;

    if (piece.Graphic == DungeonGraphicType.BlackDoor)
      return true;

    string name = piece.Name ?? string.Empty;
    return name.StartsWith("BlackDoor", System.StringComparison.Ordinal)
        || name.StartsWith("Black Door", System.StringComparison.Ordinal)
        || name.Contains("Wall");
  }

  private void DisableAllWallRenderingPieces()
  {
    resolvedNormalWallByPiece.Clear();

    if (layout == null || layout.Pieces == null)
      return;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (!IsWallRenderingPiece(piece))
        continue;
      if (IsFrontWallF2Card(piece))
      {
        piece.PoseOffsetX = 0;
        piece.PoseOffsetY = 0;
        continue;
      }
      if (piece.Name == "BlackDoorF1"
          || piece.Name == "BlackDoorF2"
          || piece.Name == "BlackDoorF3"
          || piece.Name == "Black Door Frame Left F1"
          || piece.Name == "Black Door Frame Right F1"
          || piece.Name == "Black Door Frame Left F3"
          || piece.Name == "Black Door Frame Right F3")
        continue;

      piece.Enabled = false;
      piece.PoseOffsetX = 0;
      piece.PoseOffsetY = 0;
    }
  }

  private bool ShouldDrawPieceAtPreviewPose(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (previewDisableAllWalls && !IsDisableWallsKeeper(piece))
      return false;

    // Normal walls draw only when the current minimap resolver explicitly
    // produced an enabled transient state. Black Door and all unresolved wall
    // pieces remain off.
    if (IsWallRenderingPiece(piece))
    {
      if (previewEnabledOverrideByPiece.TryGetValue(
              piece, out bool previewEnabled))
      {
        return previewEnabled;
      }

      return piece.Enabled
          && TryGetResolvedNormalWallState(
              piece,
              out ResolvedNormalWallState wallState)
          && wallState.Enabled;
    }

    if (piece.Graphic == DungeonGraphicType.None)
      return false;

    return piece.Enabled;
  }

  /// <summary>
  /// (1,3) North Black Door F1 front view.
  /// Uses the dedicated 96x88 source texture and does not write pose data.
  /// </summary>
  private bool IsVerifiedBlackDoorF1Pose()
  {
    return previewX == 1
        && previewY == 3
        && previewFacing == DungeonFacing.North;
  }

  private bool IsBlackDoorF1PoseException(ViewportPiece piece)
  {
    if (piece == null || piece.Name != "BlackDoorF1")
      return false;

    return previewX == 1
        && previewY == 3
        && previewFacing == DungeonFacing.North;
  }

  /// <summary>
  /// (1,4) North Black Door F2 size exception. Draw may run even when the
  /// normal Black Door Enabled flag is off. Does not write pose data.
  /// </summary>
  private bool IsBlackDoorF2PoseException(ViewportPiece piece)
  {
    // F2 is rendered only through the existing BlackDoorF1 carrier piece.
    // Do not match every piece that happens to use the BlackDoor graphic.
    if (piece == null || piece.Name != "BlackDoorF1")
      return false;

    return previewX == 1
        && previewY == 4
        && previewFacing == DungeonFacing.North;
  }

  /// <summary>
  /// (1,5) North Black Door F3 size exception. Draw may run even when the
  /// normal Black Door Enabled flag is off. Does not write pose data.
  /// </summary>
  private bool IsBlackDoorF3PoseException(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (piece.Graphic != DungeonGraphicType.BlackDoor)
      return false;

    return previewX == 1
        && previewY == 5
        && previewFacing == DungeonFacing.North;
  }

  /// <summary>
  /// Unique Hall of Champions oblique Black Door side view.
  /// Preview-only: never changes layout X/Y, pose offsets, mirror flags, or the
  /// existing (1,5) North Black Door F3 setup.
  /// </summary>
  private bool IsBlackDoorObliqueRightD3PoseException(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (piece.Name != "Wall D3R2" && piece.Name != "RightD3")
      return false;

    return previewX == 0
        && previewY == 5
        && previewFacing == DungeonFacing.North;
  }

  /// <summary>
  /// (0,5) North only: draw the Black Door right F3 frame at its measured
  /// oblique position. This is completely separate from the locked (1,5) North
  /// F3 frame path and does not mutate the hidden frame piece.
  /// </summary>
  private void BlitBlackDoorObliqueRightF3FrameIntoPreview(Color32[] pixels)
  {
    if (previewX != 0
        || previewY != 5
        || previewFacing != DungeonFacing.North)
    {
      return;
    }

    Texture2D source = GetBlackDoorFrameF3SourceTexture();
    if (source == null)
      return;

    BlitPieceIntoPreview(
        pixels,
        source,
        195,
        98,
        true);
  }

  private Texture2D GetFrontWallF2_224ReferenceTexture()
  {
    if (frontWallF2_224ReferenceTexture == null)
    {
      frontWallF2_224ReferenceTexture =
          AssetDatabase.LoadAssetAtPath<Texture2D>(
              "Assets/Art/Walls/Front Wall F2_224x74.png");
    }

    return frontWallF2_224ReferenceTexture;
  }

  private Texture2D GetRight2STexture()
  {
    if (right2SSourceTexture == null)
    {
      right2SSourceTexture =
          AssetDatabase.LoadAssetAtPath<Texture2D>(
              "Assets/Art/Walls/Right2S.png");
    }

    return right2SSourceTexture;
  }

  private Texture2D GetBlackDoorFrameF3SourceTexture()
  {
    if (blackDoorFrameF3SourceTexture == null)
    {
      blackDoorFrameF3SourceTexture =
          AssetDatabase.LoadAssetAtPath<Texture2D>(
              "Assets/Art/Walls/Black Door Frame_Left_10x42.png");
    }

    return blackDoorFrameF3SourceTexture;
  }

  private Texture2D GetBlackDoorFrameLeftF1SourceTexture()
  {
    if (blackDoorFrameLeftF1SourceTexture == null)
    {
      blackDoorFrameLeftF1SourceTexture =
          AssetDatabase.LoadAssetAtPath<Texture2D>(
              "Assets/Art/Walls/Black Door Frame_Left_25x94.png");
    }

    return blackDoorFrameLeftF1SourceTexture;
  }

  private Texture2D GetBlackDoorF1SourceTexture()
  {
    if (blackDoorF1SourceTexture == null)
    {
      blackDoorF1SourceTexture =
          AssetDatabase.LoadAssetAtPath<Texture2D>(
              "Assets/Art/Walls/Black Door 96x88.png");
    }

    return blackDoorF1SourceTexture;
  }

  private Texture2D GetBlackDoorF3SourceTexture()
  {
    if (blackDoorF3SourceTexture == null)
    {
      blackDoorF3SourceTexture =
          AssetDatabase.LoadAssetAtPath<Texture2D>(
              "Assets/Art/Walls/BlackDoorF3_45x39.png");
    }

    return blackDoorF3SourceTexture;
  }

  private Texture2D GetBlackDoorF2SourceTexture()
  {
    if (blackDoorF2SourceTexture == null)
    {
      blackDoorF2SourceTexture =
          AssetDatabase.LoadAssetAtPath<Texture2D>(
              "Assets/Art/Walls/BlackDoorF2_66x64.png");
    }

    return blackDoorF2SourceTexture;
  }

  /// <summary>
  /// (1,5) North Black Door F3 frames. Same 10×42 left source; right is
  /// mirrored. Not wall-geometry pieces. Does not write pose data.
  /// </summary>
  private void BlitBlackDoorF3FramesIntoPreview(Color32[] pixels)
  {
    if (previewX != 1
        || previewY != 5
        || previewFacing != DungeonFacing.North)
    {
      return;
    }

    Texture2D source = GetBlackDoorFrameF3SourceTexture();
    if (source == null)
      return;

    ViewportPiece leftF3 = FindLayoutPieceByName("Black Door Frame Left F3");
    bool leftF3FrameEnabled =
        leftF3 != null
            ? leftF3.Enabled
            : blackDoorFrameLeftF3CardEnabled;

    if (leftF3FrameEnabled)
    {
      int leftX = leftF3 != null ? leftF3.X : blackDoorFrameLeftF3CardX;
      int leftY = leftF3 != null ? leftF3.Y : blackDoorFrameLeftF3CardY;
      BlitPieceIntoPreview(
          pixels,
          source,
          leftX,
          leftY,
          blackDoorFrameLeftF3CardMirror);
    }

    ViewportPiece rightF3 = FindLayoutPieceByName("Black Door Frame Right F3");
    bool rightF3FrameEnabled =
        rightF3 != null
            ? rightF3.Enabled
            : blackDoorFrameRightF3CardEnabled;

    if (rightF3FrameEnabled)
    {
      int rightX = rightF3 != null ? rightF3.X : blackDoorFrameRightF3CardX;
      int rightY = rightF3 != null ? rightF3.Y : blackDoorFrameRightF3CardY;
      BlitPieceIntoPreview(
          pixels,
          source,
          rightX,
          rightY,
          true);
    }
  }

  /// <summary>
  /// Preview-only mirror from the piece's authored MirrorHorizontally flag.
  /// Does not write the layout asset or apply pose phase overrides.
  /// </summary>
  private bool GetPreviewMirror(ViewportPiece piece, DungeonMap poseMap)
  {
    if (piece == null)
      return false;

    // F0 side walls use the deterministic pose phase. F1/F2/F3 keep their
    // current imported orientation until their mirror rules are verified.
    if (IsWallF0LeftPiece(piece) || IsWallF0RightPiece(piece))
      return GetF0MirrorFromPose();

    if (IsWallF1LeftPiece(piece)
        || IsWallF1RightPiece(piece)
        || IsWallF2LeftPiece(piece)
        || IsWallF2RightPiece(piece)
        || IsWallF3LeftPiece(piece)
        || IsWallF3RightPiece(piece))
      return false;

    return piece.MirrorHorizontally;
  }

  private DungeonMap TryGetPreviewPoseMap()
  {
    EnsurePreviewMiniMapLoaded();
    if (previewMiniMap == null)
      return null;

    previewMiniMap.SetPlayerPose(previewX, previewY, previewFacing);
    return previewMiniMap;
  }

  private static void ClearFrontWallOverflowIntoUi(
      Color32[] pixels,
      int destinationY,
      int height)
  {
    if (pixels == null || height <= 0)
      return;

    Color32 magenta = new Color32(255, 0, 255, 255);
    int startY = Mathf.Max(0, destinationY);
    int endY = Mathf.Min(PreviewHeight, destinationY + height);

    for (int y = startY; y < endY; y++)
    {
      int row = y * PreviewWidth;
      for (int x = 224; x < PreviewWidth; x++)
        pixels[row + x] = magenta;
    }
  }

  // Mirror the complete 224x111 FrontF1 first, then copy from mirrored
  // image X = mirroredSourceStartX through the last column.
  // Destination start is independent and is not derived from source start.
  private static void BlitFrontF1MirroredImageFromX(
      Color32[] dest,
      Texture2D source,
      int mirroredSourceStartX,
      int destinationStartX,
      int destinationY,
      int maxCopyWidth = int.MaxValue)
  {
    if (dest == null || source == null || !source.isReadable)
      return;

    int imageWidth = Mathf.Min(
        source.width,
        StraightF1WallLogic.CompositeWidth);
    if (imageWidth <= 0)
      return;

    int lastImageX = imageWidth - 1;
    mirroredSourceStartX = Mathf.Clamp(mirroredSourceStartX, 0, lastImageX);
    int copyWidth = lastImageX - mirroredSourceStartX + 1;
    copyWidth = Mathf.Min(copyWidth, maxCopyWidth);
    if (copyWidth <= 0)
      return;

    Color32[] sourcePixels = source.GetPixels32();

    for (int sourceY = 0; sourceY < source.height; sourceY++)
    {
      int targetY = destinationY + sourceY;
      if (targetY < 0 || targetY >= PreviewHeight)
        continue;

      int sourceRow = sourceY * source.width;
      int destRow = targetY * PreviewWidth;

      for (int i = 0; i < copyWidth; i++)
      {
        int targetX = destinationStartX + i;
        if (targetX < 0 || targetX >= StraightF1WallLogic.CompositeWidth)
          continue;

        int mirroredX = mirroredSourceStartX + i;
        int originalX = lastImageX - mirroredX;
        Color32 colour = sourcePixels[sourceRow + originalX];
        colour.a = 255;
        dest[destRow + targetX] = colour;
      }
    }
  }

  private static void BlitFrontF1CroppedPreview(
      Color32[] dest,
      Texture2D source,
      int sourceStartX,
      int destinationStartX,
      int destinationY,
      bool mirrorHorizontally,
      int maxCopyWidth = int.MaxValue)
  {
    if (dest == null || source == null || !source.isReadable)
      return;

    sourceStartX = Mathf.Clamp(
        sourceStartX,
        0,
        Mathf.Min(source.width, StraightF1WallLogic.CompositeWidth) - 1);
    int sourceEndX = Mathf.Min(
        source.width,
        StraightF1WallLogic.CompositeWidth) - 1;
    int copyWidth = sourceEndX - sourceStartX + 1;
    copyWidth = Mathf.Min(copyWidth, maxCopyWidth);
    if (copyWidth <= 0)
      return;

    Color32[] sourcePixels = source.GetPixels32();

    for (int sourceY = 0; sourceY < source.height; sourceY++)
    {
      int targetY = destinationY + sourceY;
      if (targetY < 0 || targetY >= PreviewHeight)
        continue;

      int sourceRow = sourceY * source.width;
      int destRow = targetY * PreviewWidth;

      for (int i = 0; i < copyWidth; i++)
      {
        int targetX = destinationStartX + i;
        if (targetX < 0 || targetX >= StraightF1WallLogic.CompositeWidth)
          continue;

        // Crop controls the source/destination start.
        // Mirror remains independent and reverses only the selected source strip.
        int sourceX = mirrorHorizontally
            ? sourceEndX - i
            : sourceStartX + i;

        Color32 colour = sourcePixels[sourceRow + sourceX];
        colour.a = 255;
        dest[destRow + targetX] = colour;
      }
    }
  }

  private static void BlitPieceIntoPreview(
      Color32[] dest,
      Texture2D source,
      int destinationX,
      int destinationY,
      bool mirrorHorizontally = false,
      string unreadableDiagnostic = null)
  {
    if (!source.isReadable)
    {
      if (!string.IsNullOrEmpty(unreadableDiagnostic))
        Debug.Log(unreadableDiagnostic);
      return;
    }

    Color32[] sourcePixels = source.GetPixels32();

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

        Color32 sourceColour =
            sourcePixels[sourceY * source.width + sourceX];
        if (sourceColour.a == 0)
          continue;

        dest[targetY * PreviewWidth + targetX] = sourceColour;
      }
    }
  }

  private static void BlitPieceScaledIntoPreview(
      Color32[] dest,
      Texture2D source,
      int destinationX,
      int destinationY,
      int destWidth,
      int destHeight,
      bool mirrorHorizontally = false)
  {
    if (!source.isReadable || destWidth <= 0 || destHeight <= 0)
      return;

    Color32[] sourcePixels = source.GetPixels32();

    for (int destRow = 0; destRow < destHeight; destRow++)
    {
      int targetY = destinationY + destRow;
      if (targetY < 0 || targetY >= PreviewHeight)
        continue;

      int sourceY = destRow * source.height / destHeight;

      for (int destCol = 0; destCol < destWidth; destCol++)
      {
        int sampleX = destCol * source.width / destWidth;
        if (mirrorHorizontally)
          sampleX = source.width - 1 - sampleX;

        int targetX = destinationX + destCol;
        if (targetX < 0 || targetX >= PreviewWidth)
          continue;

        Color32 sourceColour =
            sourcePixels[sourceY * source.width + sampleX];
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

  private static int DrawDelayedIntFieldMaybeRed(
      string label,
      int value,
      bool valueDiffersFromRef,
      bool immediate = false)
  {
    Color previousGuiColor = GUI.color;
    Color previousContentColor = GUI.contentColor;
    GUIStyle fieldStyle = EditorStyles.numberField;
    if (valueDiffersFromRef)
    {
      fieldStyle = new GUIStyle(EditorStyles.numberField);
      Color red = Color.red;
      fieldStyle.normal.textColor = red;
      fieldStyle.hover.textColor = red;
      fieldStyle.focused.textColor = red;
      fieldStyle.active.textColor = red;
    }

    EditorGUILayout.LabelField(label, GUILayout.Width(12f));
    int result = immediate
        ? EditorGUILayout.IntField(value, fieldStyle, GUILayout.Width(36f))
        : EditorGUILayout.DelayedIntField(
            value, fieldStyle, GUILayout.Width(36f));
    GUI.color = previousGuiColor;
    GUI.contentColor = previousContentColor;
    return result;
  }

  private static bool DrawIntStepperInline(
      string label,
      ref int value,
      int step,
      bool valueDiffersFromRef = false,
      bool immediate = false)
  {
    EditorGUI.BeginChangeCheck();
    value = DrawDelayedIntFieldMaybeRed(
        label, value, valueDiffersFromRef, immediate);

    bool changed = EditorGUI.EndChangeCheck();

    if (GUILayout.Button($"-{step}", GUILayout.Width(36f)))
    {
      value -= step;
      changed = true;
    }

    if (GUILayout.Button($"+{step}", GUILayout.Width(36f)))
    {
      value += step;
      changed = true;
    }

    return changed;
  }

  private static bool DrawTopDownYStepperInline(
      ref int unityY,
      int pieceHeight,
      int step,
      bool valueDiffersFromRef = false,
      bool immediate = false)
  {
    int oldUnityY = unityY;
    int displayY = UnityYToDisplayY(unityY, pieceHeight);

    EditorGUI.BeginChangeCheck();
    displayY = DrawDelayedIntFieldMaybeRed(
        "Y", displayY, valueDiffersFromRef, immediate);

    if (GUILayout.Button($"-{step}", GUILayout.Width(36f)))
      displayY -= step;

    if (GUILayout.Button($"+{step}", GUILayout.Width(36f)))
      displayY += step;

    int maxDisplayY = Mathf.Max(0, PreviewHeight - Mathf.Max(1, pieceHeight));
    displayY = Mathf.Clamp(displayY, 0, maxDisplayY);
    unityY = DisplayYToUnityY(displayY, pieceHeight);

    return EditorGUI.EndChangeCheck() || unityY != oldUnityY;
  }

  private static bool DrawIntStepper(
      string label,
      ref int value,
      int step,
      bool valueDiffersFromRef = false)
  {
    EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
    EditorGUI.BeginChangeCheck();
    value = DrawDelayedIntFieldMaybeRed(
        label, value, valueDiffersFromRef);

    bool changed = EditorGUI.EndChangeCheck();

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

  private void RefreshTemporaryNormalWallPreview()
  {
    if (Application.isPlaying)
      return;

    ApplyCurrentPoseVisibilityToLayout();
    ResetEditModeViewportLogCache();
    DestroyEditModePreviewTextureOnly();
    RefreshEditModePreview();
    RepaintGameViews();
    Repaint();
  }

  /// <summary>
  /// Temporary normal-wall Y editor. The value is Unity bottom-origin, while
  /// ViewEdit displays GIMP/top-origin Y. No layout field is mutated.
  /// </summary>
  private static bool DrawTopDownYStepper(
      ref int unityY,
      int pieceHeight,
      int step,
      bool valueDiffersFromRef = false)
  {
    int oldUnityY = unityY;
    int displayY = UnityYToDisplayY(unityY, pieceHeight);

    EditorGUI.BeginChangeCheck();
    EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
    displayY = DrawDelayedIntFieldMaybeRed(
        "Y", displayY, valueDiffersFromRef);

    if (GUILayout.Button($"-{step}", GUILayout.Width(36)))
      displayY -= step;

    if (GUILayout.Button($"+{step}", GUILayout.Width(36)))
      displayY += step;

    EditorGUILayout.EndHorizontal();

    int maxDisplayY = Mathf.Max(0, PreviewHeight - Mathf.Max(1, pieceHeight));
    displayY = Mathf.Clamp(displayY, 0, maxDisplayY);
    unityY = DisplayYToUnityY(displayY, pieceHeight);

    return EditorGUI.EndChangeCheck() || unityY != oldUnityY;
  }

  /// <summary>
  /// Editor-only Y: show/edit top-down (GIMP) coords. Storage stays Unity
  /// bottom-up framebuffer Y used by blit. displayY = 200 - unityY - h.
  /// </summary>
  private bool DrawTopDownYStepper(ViewportPiece piece, int step)
  {
    if (piece == null)
      return false;

    int pieceHeight = GetPieceHeightForEditorY(piece);
    int oldUnityY = piece.Y;
    int displayY = UnityYToDisplayY(oldUnityY, pieceHeight);

    EditorGUI.BeginChangeCheck();
    EditorGUILayout.BeginHorizontal();
    displayY = EditorGUILayout.IntField("Y", displayY);

    if (GUILayout.Button($"-{step}", GUILayout.Width(36)))
      displayY -= step;

    if (GUILayout.Button($"+{step}", GUILayout.Width(36)))
      displayY += step;

    EditorGUILayout.EndHorizontal();

    int maxDisplayY = Mathf.Max(0, PreviewHeight - pieceHeight);
    displayY = Mathf.Clamp(displayY, 0, maxDisplayY);
    piece.Y = DisplayYToUnityY(displayY, pieceHeight);

    return EditorGUI.EndChangeCheck() || piece.Y != oldUnityY;
  }

  private int GetPieceHeightForEditorY(ViewportPiece piece)
  {
    if (piece == null || graphics == null)
      return 1;

    if (IsFrontWallF1Card(piece))
    {
      int width = StraightF1WallLogic.NormalizeFrontWallF1Width(
          piece.FrontWallF1Width);
      if (TryGetResolvedNormalWallState(
              piece,
              out ResolvedNormalWallState resolvedWall))
      {
        width = resolvedWall.FrontF1Width;
      }

      Texture2D f1Texture = graphics.GetFrontWallF1Texture(width);
      if (f1Texture != null && f1Texture.height > 0)
        return f1Texture.height;
    }

    Texture2D texture = graphics.GetTexture(piece.Graphic);
    if (texture == null || texture.height <= 0)
      return 1;

    return texture.height;
  }

  private static int UnityYToDisplayY(int storedUnityY, int pieceHeight)
  {
    int height = Mathf.Max(1, pieceHeight);
    return PreviewHeight - storedUnityY - height;
  }

  private static int DisplayYToUnityY(int displayY, int pieceHeight)
  {
    int height = Mathf.Max(1, pieceHeight);
    return PreviewHeight - displayY - height;
  }
}
