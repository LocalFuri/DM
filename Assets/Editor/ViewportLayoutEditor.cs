using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DM.Dungeon;
using DM.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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
  [System.NonSerialized]
  private ViewportPoseVisibilityStore poseVisibilityStore;
  private Vector2 editorScroll;
  private int pieceSearchFamilyIndex;
  private bool showWallsActivFilter;
  private string pieceSearchText = string.Empty;
  private bool openSearchPiecesPopup;
  private bool focusSearchPieces;
  private GUIStyle searchPiecesLabelStyle;
  private GUIStyle pieceFamilyHeaderStyle;
  private bool[] rememberedEnabledStates;
  private int snap = 1;
  private readonly Dictionary<string, bool> extraPieceDeterministicByName =
      new Dictionary<string, bool>();

  private bool hookedViewEditGlobalNavigation;

  private static int s_viewEditGlobalNavOwners;
  private static bool s_viewEditGlobalNavCallbackAdded;
  private static bool s_viewEditGlobalNavDispatch;
  private static readonly EditorApplication.CallbackFunction
      ViewEditGlobalNavHandler = HandleViewEditGlobalNavigationEvent;
  private static System.Delegate s_viewEditBeforeEventProcessedHandler;

  // Blocks pose capture while PersistChanges temporarily writes kit baseline
  // Enabled onto the live layout (SaveAssets can re-enter editor code).
  private bool suppressPoseCaptureFromLayout;

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

  private bool blackDoorFrameLeftF3CardInitialized;
  private bool blackDoorFrameLeftF3CardEnabled;
  private bool blackDoorFrameLeftF3CardMirror;
  private int blackDoorFrameLeftF3CardX;
  private int blackDoorFrameLeftF3CardY;

  private bool blackDoorFrameRightF3CardInitialized;
  private bool blackDoorFrameRightF3CardEnabled;
  private bool blackDoorFrameRightF3CardMirror;
  private int blackDoorFrameRightF3CardX;
  private int blackDoorFrameRightF3CardY;

  private Texture2D blackDoorFrameF3SourceTexture;
  private Texture2D blackDoorF3SourceTexture;
  private Texture2D blackDoorF2SourceTexture;

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

  // TEMP F3 diagnostics — remove after verification.
  private static string lastLoggedFrontWallF3EditDrawKey;

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
    EnsurePoseVisibilityStore();
    StripObsoleteFrontWallF1ABPieces();
    MigrateObsoleteFrontWallF1ABPoseStore();
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
    CaptureCurrentPoseVisibilityToStore();
    PersistPoseVisibilityStore();
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

  private void OnGUI()
  {
    selectionChangedThisFrame = false;

    // Claim EditorWindow focus on click so existing keyboard nav can run.
    // Do not clear GUI.FocusControl here — a TextField/IntField on this
    // same MouseDown still needs to receive it.
    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
      Focus();

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
      EditorGUILayout.PrefixLabel(
          "Search Pieces",
          EditorStyles.popup,
          GetSearchPiecesLabelStyle());
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
        StripObsoleteFrontWallF1ABPieces();

      bool changed = false;

      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (IsHiddenFromEditorPieceList(piece))
          continue;

        if (!PieceMatchesSearchFilter(piece))
          continue;

        bool isSelected = i == selectedPieceIndex;
        DrawPieceCard(i, piece, isSelected, ref changed);
      }

      if (EditorGUI.EndChangeCheck() || changed)
        PersistChanges();
    }

    EditorGUILayout.EndScrollView();

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
  private static bool IsHiddenFromEditorPieceList(ViewportPiece piece)
  {
    if (piece == null || piece.Name == null)
      return false;

    return piece.Name == "Ceiling Strip 84"
        || piece.Name == "Ceiling Strip 85"
        || piece.Name == "Black Door Frame Left F3"
        || piece.Name == "Black Door Frame Right F3"
        || piece.Name == "BlackDoorF3";
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

  private void MigrateObsoleteFrontWallF1ABPoseStore()
  {
    EnsurePoseVisibilityStore();
    if (poseVisibilityStore == null)
      return;

    if (poseVisibilityStore.MigrateObsoleteFrontWallF1ABEntries())
      EditorUtility.SetDirty(poseVisibilityStore);
  }

  private bool PieceMatchesSearchFilter(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (showWallsActivFilter)
      return MatchesShowWallsActivFilter(piece);

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

    return PieceMatchesSearchFamily(
        piece,
        PieceSearchFamilyOptions[pieceSearchFamilyIndex]);
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

    if (ShouldDrawNonDTermStatus(piece))
      return true;

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
        || name == "RightD3";
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
        return name.StartsWith("FrontF", System.StringComparison.Ordinal);
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

      if (!IsPermanentSearchFamily(label) && !FamilyHasEnabledPiece(label))
        continue;

      int index = i;
      menu.AddItem(
          new GUIContent(label),
          false,
          () =>
          {
            pieceSearchFamilyIndex = index;
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

    if (IsPermanentSearchFamily(family))
      return;

    if (!FamilyHasEnabledPiece(family))
      pieceSearchFamilyIndex = 0;
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
    bool isStatusOnly = label == "Non-DTerm";
    if (current != null
        && current.type == EventType.MouseDown
        && current.button == 0
        && toggleRect.Contains(current.mousePosition))
    {
      if (isStatusOnly)
      {
        current.Use();
      }
      else if (!isMirror || pieceEnabled)
      {
        value = !value;
        GUI.changed = true;
        current.Use();
      }
    }

    // Shared Unity checkbox chrome for all three types. Colored fills are
    // inset so they cannot cover the PrefixLabel or change hit-testing.
    GUI.Toggle(toggleRect, false, GUIContent.none, EditorStyles.toggle);

    Color checkColor = new Color(0.2f, 1.0f, 0.2f);
    bool showChecked = value;
    if (isMirror && !pieceEnabled)
      showChecked = false;
    if (pieceEnabled && showChecked && label == "DTerm")
    {
      EditorGUI.DrawRect(InsetToggleFillRect(toggleRect), new Color(1f, 210f / 255f, 0f));
      checkColor = Color.black;
    }
    else if (pieceEnabled && showChecked && isMirror)
    {
      EditorGUI.DrawRect(InsetToggleFillRect(toggleRect), new Color(1f, 140f / 255f, 0f));
      checkColor = Color.black;
    }
    else if (showChecked && isStatusOnly)
    {
      EditorGUI.DrawRect(InsetToggleFillRect(toggleRect), Color.red);
      checkColor = Color.yellow;
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

    EditorGUI.BeginChangeCheck();
    EditorGUILayout.BeginHorizontal();
    float nameLabelWidth =
        EditorStyles.label.CalcSize(new GUIContent("Name")).x;
    float savedNameLabelWidth = EditorGUIUtility.labelWidth;
    EditorGUIUtility.labelWidth = nameLabelWidth;
    piece.Name = EditorGUILayout.TextField("Name", piece.Name);
    EditorGUIUtility.labelWidth = savedNameLabelWidth;
    EditorGUILayout.EndHorizontal();

    EditorGUILayout.BeginHorizontal();
    float previousLabelWidth = EditorGUIUtility.labelWidth;
    const float ToggleBoxWidth = 18f;
    const float ToggleGroupGap = 10f;

    const string EnabledLabel = "Enabled";
    float enabledLabelWidth =
        EditorStyles.label.CalcSize(new GUIContent(EnabledLabel)).x;
    EditorGUIUtility.labelWidth = enabledLabelWidth;
    piece.Enabled = DrawMouseOnlyToggle(
        EnabledLabel,
        piece.Enabled,
        piece.Enabled,
        GUILayout.Width(enabledLabelWidth + ToggleBoxWidth),
        GUILayout.ExpandWidth(false));
    bool nameOrEnabledChanged = EditorGUI.EndChangeCheck();

    GUILayout.Space(ToggleGroupGap);
    const string DeterministicLabel = "DTerm";
    float deterministicLabelWidth =
        EditorStyles.label.CalcSize(new GUIContent(DeterministicLabel)).x;
    EditorGUIUtility.labelWidth = deterministicLabelWidth;
    bool deterministicBefore = GetPieceDeterministic(piece);
    bool deterministic = DrawMouseOnlyToggle(
        DeterministicLabel,
        deterministicBefore,
        piece.Enabled,
        GUILayout.Width(deterministicLabelWidth + ToggleBoxWidth),
        GUILayout.ExpandWidth(false));
    if (piece.Enabled
        && (IsWallF0LeftPiece(piece)
            || IsWallF0RightPiece(piece)
            || IsWallF1LeftPiece(piece)
            || IsWallF1RightPiece(piece)
            || IsWallF2LeftPiece(piece)
            || IsWallF2RightPiece(piece)
            || IsWallF3LeftPiece(piece)
            || IsWallF3RightPiece(piece)
            || IsFrontWallF1Card(piece)
            || IsFrontWallF3Card(piece)))
      deterministic = true;
    if (!piece.Enabled)
      deterministic = false;
    if (deterministic != deterministicBefore)
    {
      SetPieceDeterministic(piece, deterministic);
      changed = true;
    }

    // Mirror is outside the Name/Enabled/Graphic change-check so SelectPiece →
    // FocusControl(null) cannot swallow the Toggle or skip the live refresh.
    bool mirrorBefore = piece.MirrorHorizontally;
    GUILayout.Space(ToggleGroupGap);
    const string MirrorLabel = "Mirror";
    float mirrorLabelWidth =
        EditorStyles.label.CalcSize(new GUIContent(MirrorLabel)).x;
    EditorGUIUtility.labelWidth = mirrorLabelWidth;
    piece.MirrorHorizontally = DrawMouseOnlyToggle(
        MirrorLabel,
        piece.MirrorHorizontally,
        piece.Enabled,
        GUILayout.Width(mirrorLabelWidth + ToggleBoxWidth),
        GUILayout.ExpandWidth(false));
    if (ShouldDrawNonDTermStatus(piece))
    {
      GUILayout.Space(ToggleGroupGap);
      const string NonDTermLabel = "Non-DTerm";
      float nonDTermLabelWidth =
          EditorStyles.label.CalcSize(new GUIContent(NonDTermLabel)).x;
      EditorGUIUtility.labelWidth = nonDTermLabelWidth;
      DrawMouseOnlyToggle(
          NonDTermLabel,
          piece.Enabled && !deterministic,
          piece.Enabled,
          GUILayout.Width(nonDTermLabelWidth + ToggleBoxWidth),
          GUILayout.ExpandWidth(false));
    }
    EditorGUIUtility.labelWidth = previousLabelWidth;
    EditorGUILayout.EndHorizontal();

    EditorGUI.BeginChangeCheck();
    piece.Graphic = (DungeonGraphicType)EditorGUILayout.EnumPopup("Graphic", piece.Graphic);
    if (EditorGUI.EndChangeCheck() || nameOrEnabledChanged)
    {
      SelectPiece(index);
      changed = true;
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

    if (piece.MirrorHorizontally != mirrorBefore)
    {
      changed = true;
      ApplyMirrorHorizontallyChangeForCurrentPose();
    }

    if (IsFrontWallF1Card(piece))
    {
      int widthBefore =
          StraightF1WallLogic.NormalizeFrontWallF1Width(
              piece.FrontWallF1Width);
      int widthSelected = EditorGUILayout.IntPopup(
          "F1 Width",
          widthBefore,
          new[] { "160", "191", "224" },
          new[]
          {
            StraightF1WallLogic.CompositeWidth160,
            StraightF1WallLogic.CompositeWidth191,
            StraightF1WallLogic.CompositeWidth
          });
      piece.FrontWallF1Width =
          StraightF1WallLogic.NormalizeFrontWallF1Width(widthSelected);

      if (piece.FrontWallF1Width != widthBefore)
      {
        changed = true;
        ApplyFrontWallF1WidthChangeForCurrentPose();
      }
    }

    if (IsFrontWallF2Card(piece))
    {
      int widthBefore = FrontWallF2Logic.Normalize(piece.FrontWallF2Width);
      int widthSelected = EditorGUILayout.IntPopup(
          "F2 Width",
          widthBefore,
          new[] { "106", "131", "162" },
          new[]
          {
            FrontWallF2Logic.Width106,
            FrontWallF2Logic.Width131,
            FrontWallF2Logic.Width160
          });
      piece.FrontWallF2Width = FrontWallF2Logic.Normalize(widthSelected);

      if (piece.FrontWallF2Width != widthBefore)
      {
        changed = true;
        ApplyFrontWallF2WidthChangeForCurrentPose();
      }
    }

    EditorGUILayout.BeginHorizontal();
    float savedXyLabelWidth = EditorGUIUtility.labelWidth;
    EditorGUIUtility.labelWidth =
        EditorStyles.label.CalcSize(new GUIContent("X")).x;
    EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
    if (DrawIntStepper("X", ref piece.X, snap))
    {
      SelectPiece(index);
      changed = true;
    }
    EditorGUILayout.EndHorizontal();

    GUILayout.Space(10f);

    EditorGUIUtility.labelWidth =
        EditorStyles.label.CalcSize(new GUIContent("Y")).x;
    EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
    if (DrawTopDownYStepper(piece, snap))
    {
      SelectPiece(index);
      changed = true;
    }
    EditorGUILayout.EndHorizontal();
    EditorGUIUtility.labelWidth = savedXyLabelWidth;
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

    using (new EditorGUI.DisabledScope(rememberedEnabledStates == null))
    {
      if (GUILayout.Button("Restore"))
      {
        RestoreEnabledStates();
        changed = true;
      }
    }

    EditorGUILayout.EndHorizontal();
    EditorGUILayout.EndVertical();

    if (piece.Name == "Black Door Frame Left F2")
    {
      ViewportPiece leftF3 = FindLayoutPieceByName("Black Door Frame Left F3");
      if (leftF3 == null)
      {
        leftF3 = EnsureBlackDoorFrameLeftF3Piece();
        changed = true;
      }
      int x = leftF3 != null ? leftF3.X : blackDoorFrameLeftF3CardX;
      int y = leftF3 != null ? leftF3.Y : blackDoorFrameLeftF3CardY;
      int xBefore = x;
      int yBefore = y;
      DrawBlackDoorFrameF3EditorCard(
          "Black Door Frame Left F3",
          ref blackDoorFrameLeftF3CardInitialized,
          ref blackDoorFrameLeftF3CardEnabled,
          ref blackDoorFrameLeftF3CardMirror,
          ref x,
          ref y,
          false);
      blackDoorFrameLeftF3CardX = x;
      blackDoorFrameLeftF3CardY = y;
      if (leftF3 != null && (x != xBefore || y != yBefore))
      {
        leftF3.X = x;
        leftF3.Y = y;
        changed = true;
      }
    }
    else if (piece.Name == "Black Door Frame Right F2")
    {
      ViewportPiece rightF3 = FindLayoutPieceByName("Black Door Frame Right F3");
      if (rightF3 == null)
      {
        rightF3 = EnsureBlackDoorFrameRightF3Piece();
        changed = true;
      }
      int x = rightF3 != null ? rightF3.X : blackDoorFrameRightF3CardX;
      int y = rightF3 != null ? rightF3.Y : blackDoorFrameRightF3CardY;
      int xBefore = x;
      int yBefore = y;
      DrawBlackDoorFrameF3EditorCard(
          "Black Door Frame Right F3",
          ref blackDoorFrameRightF3CardInitialized,
          ref blackDoorFrameRightF3CardEnabled,
          ref blackDoorFrameRightF3CardMirror,
          ref x,
          ref y,
          true);
      blackDoorFrameRightF3CardX = x;
      blackDoorFrameRightF3CardY = y;
      if (rightF3 != null && (x != xBefore || y != yBefore))
      {
        rightF3.X = x;
        rightF3.Y = y;
        changed = true;
      }
    }
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

    if (EditorGUIUtility.editingTextField)
      return;

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

    if (IsEditorTextOrNumericInputActive())
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

    // Allow F3 diagnostics to re-emit after a forced preview rebuild.
    lastLoggedFrontWallF3EditDrawKey = null;
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

    GUILayout.FlexibleSpace();
    if (GUILayout.Button("Show Walls Activ", GUILayout.ExpandWidth(false)))
      showWallsActivFilter = !showWallsActivFilter;
    if (GUILayout.Button("Disable Walls", GUILayout.ExpandWidth(false)))
    {
      DisableWallsKeepChrome();
      PersistChanges();
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
    EditorGUILayout.Space();
    EditorGUILayout.LabelField(
        previewX + " X /" + previewY + " Y - " + previewFacing);

    DrawRelativeViewportGeometryDebug();

    if (GUILayout.Button("Explain Walls", GUILayout.Width(120f)))
      deterministicWallDiagnosticText = BuildDeterministicWallDiagnostic();

    if (!string.IsNullOrEmpty(deterministicWallDiagnosticText))
      EditorGUILayout.HelpBox(deterministicWallDiagnosticText, MessageType.None);

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

    string text =
        "F0L=" + FormatRelativeViewportCell(geometry.F0Left)
        + "   F0R=" + FormatRelativeViewportCell(geometry.F0Right)
        + "\nF1L=" + FormatRelativeViewportCell(geometry.F1Left)
        + "   F1C=" + FormatRelativeViewportCell(geometry.F1Center)
        + "   F1R=" + FormatRelativeViewportCell(geometry.F1Right)
        + "\nF2L=" + FormatRelativeViewportCell(geometry.F2Left)
        + "   F2C=" + FormatRelativeViewportCell(geometry.F2Center)
        + "   F2R=" + FormatRelativeViewportCell(geometry.F2Right)
        + "\nF3L=" + FormatRelativeViewportCell(geometry.F3Left)
        + "   F3C=" + FormatRelativeViewportCell(geometry.F3Center)
        + "   F3R=" + FormatRelativeViewportCell(geometry.F3Right);

    EditorGUILayout.HelpBox(text, MessageType.None);
  }

  private static string FormatRelativeViewportCell(RelativeViewportCell cell)
  {
    return cell.IsInside
        ? cell.Type + " (" + cell.X + "," + cell.Y + ")"
        : "OUT (" + cell.X + "," + cell.Y + ")";
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

    EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(false));
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

    GUILayout.Space(6f);

    // Compact 3×2 pad immediately to the right, top-aligned with the map.
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

    CaptureCurrentPoseVisibilityToStore();
    PersistPoseVisibilityStore();

    previewX = newX;
    previewY = newY;
    previewFacing = newFacing;
    SaveSessionPrefs();
    PlayerWallBumpFeedback.ResetWallHitLog();

    if (previewMiniMap != null)
      previewMiniMap.SetPlayerPose(previewX, previewY, previewFacing);

    ApplyCurrentPoseVisibilityToLayout();
    PersistPoseVisibilityStore();

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

    previewX = newX;
    previewY = newY;
    previewFacing = newFacing;
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
  /// Load saved Enabled/Mirror for the current preview pose without creating
  /// or capturing entries (no SetDirty / SaveAssets).
  /// </summary>
  private void ApplyPoseVisibilityForNavigationOnly()
  {
    if (layout == null)
      return;

    EnsurePoseVisibilityStore();
    if (poseVisibilityStore == null)
      return;

    if (poseVisibilityStore.TryFindEntry(
            previewX,
            previewY,
            previewFacing,
            out ViewportPoseVisibilityEntry entry))
    {
      poseVisibilityStore.ApplyToLayout(entry, layout);
      ApplyCeilingMirrorFromPose();
      ApplyFloorMirrorReferenceOverride();
      ApplyWallF0LeftFromMapGeometry();
      ApplyWallF0RightFromMapGeometry();
      ApplyWallF1LeftFromMapGeometry();
      ApplyWallF1RightFromMapGeometry();
      ApplyWallF2LeftFromMapGeometry();
      ApplyWallF2RightFromMapGeometry();
      ApplyWallF3LeftFromMapGeometry();
      ApplyWallF3RightFromMapGeometry();
    ApplyRightD3FromMapGeometry();
      ApplyFrontF1FromMapGeometry();
      ApplyFrontF2FromMapGeometry();
      ApplyFrontF3FromMapGeometry();
      ApplyBlackDoorEnabledFromPoseException();
      return;
    }

    // Unknown pose: kit baseline on the live layout only — do not write store.
    ApplyUnknownPoseDefaultsToLayout();
    ApplyCeilingMirrorFromPose();
    ApplyFloorMirrorReferenceOverride();
    ApplyWallF0LeftFromMapGeometry();
    ApplyWallF0RightFromMapGeometry();
    ApplyWallF1LeftFromMapGeometry();
    ApplyWallF1RightFromMapGeometry();
    ApplyWallF2LeftFromMapGeometry();
    ApplyWallF2RightFromMapGeometry();
    ApplyWallF3LeftFromMapGeometry();
    ApplyWallF3RightFromMapGeometry();
    ApplyRightD3FromMapGeometry();
    ApplyFrontF1FromMapGeometry();
    ApplyFrontF2FromMapGeometry();
    ApplyFrontF3FromMapGeometry();
    ApplyBlackDoorEnabledFromPoseException();
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

  private void SoloPiece(int soloIndex)
  {
    Undo.RecordObject(layout, "Viewport Layout Solo Piece");

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

  /// <summary>
  /// Snapshots Enabled into rememberedEnabledStates (Solo/Restore), then
  /// disables every piece except Floor, Ceiling, Movement Arrows, and
  /// Champion Status Slot 1–4 (matched by ViewportPiece.Name).
  /// </summary>
  private void DisableWallsKeepChrome()
  {
    if (layout == null || Application.isPlaying)
      return;

    Undo.RecordObject(layout, "Viewport Layout Disable Walls");

    rememberedEnabledStates = new bool[layout.Pieces.Count];

    for (int i = 0; i < layout.Pieces.Count; i++)
      rememberedEnabledStates[i] = layout.Pieces[i].Enabled;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      piece.Enabled = IsDisableWallsKeeper(piece);
    }
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

  private void RestoreEnabledStates()
  {
    if (rememberedEnabledStates == null)
      return;

    Undo.RecordObject(layout, "Viewport Layout Restore Enabled");

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

  private static bool ShouldDrawNonDTermStatus(ViewportPiece piece)
  {
    if (piece == null || IsFloorOrCeiling(piece))
      return false;

    return IsWallF0LeftPiece(piece)
        || IsWallF0RightPiece(piece)
        || IsWallF1LeftPiece(piece)
        || IsWallF1RightPiece(piece)
        || IsWallF2LeftPiece(piece)
        || IsWallF2RightPiece(piece)
        || IsWallF3LeftPiece(piece)
        || IsWallF3RightPiece(piece)
        || IsFrontWallF1Card(piece)
        || IsFrontWallF2Card(piece)
        || IsFrontWallF3Card(piece)
        || piece.Graphic == DungeonGraphicType.WallD3L2
        || piece.Graphic == DungeonGraphicType.WallD3R2
        || piece.Name == "LeftD3"
        || piece.Name == "RightD3";
  }

  private void PersistChanges()
  {
    if (Application.isPlaying || layout == null)
      return;

    StripObsoleteFrontWallF1ABPieces();

    // Capture pose from the real live Enabled flags first.
    CaptureCurrentPoseVisibilityToStore();

    // Keep ViewportLayout.asset Enabled as the locked kit baseline so pose
    // visibility never pollutes permanent layout properties on disk.
    // SaveAssets may re-enter editor UI; suppress pose capture while the live
    // layout briefly holds kit Enabled (doors/etc. forced off).
    bool[] workingEnabled = CaptureWorkingEnabledFlags();
    suppressPoseCaptureFromLayout = true;
    try
    {
      ApplyKitBaselineEnabledToLayout();
      EditorUtility.SetDirty(layout);
      AssetDatabase.SaveAssets();
    }
    finally
    {
      ApplyWorkingEnabledFlags(workingEnabled);
      suppressPoseCaptureFromLayout = false;
    }

    // Authoritative pose write after live Enabled is restored.
    CaptureCurrentPoseVisibilityToStore();
    PersistPoseVisibilityStore();

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

    CaptureCurrentPoseVisibilityToStore();
    PersistPoseVisibilityStore();
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

  private bool GetPieceDeterministic(ViewportPiece piece)
  {
    if (piece == null || !piece.Enabled)
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
        || IsFrontWallF3Card(piece))
      return true;

    if (layout != null)
    {
      if (IsFrontWallF1Card(piece))
        return layout.FrontF1Deterministic;
      if (IsFrontWallF2Card(piece))
        return layout.FrontF2Deterministic;
      if (IsFrontWallF3Card(piece))
        return layout.FrontF3Deterministic;
    }

    string key = piece != null && piece.Name != null ? piece.Name : string.Empty;
    if (extraPieceDeterministicByName.TryGetValue(key, out bool value))
      return value;
    return false;
  }

  private void SetPieceDeterministic(ViewportPiece piece, bool value)
  {
    if (layout != null)
    {
      if (IsFrontWallF1Card(piece))
      {
        layout.FrontF1Deterministic = value;
        return;
      }

      if (IsFrontWallF2Card(piece))
      {
        layout.FrontF2Deterministic = value;
        return;
      }

      if (IsFrontWallF3Card(piece))
      {
        layout.FrontF3Deterministic = value;
        return;
      }
    }

    string key = piece != null && piece.Name != null ? piece.Name : string.Empty;
    extraPieceDeterministicByName[key] = value;
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

    CaptureCurrentPoseVisibilityToStore();
    PersistPoseVisibilityStore();

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

    CaptureCurrentPoseVisibilityToStore();
    PersistPoseVisibilityStore();
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

    CaptureCurrentPoseVisibilityToStore();
    PersistPoseVisibilityStore();
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

  private void EnsurePoseVisibilityStore()
  {
    if (poseVisibilityStore != null)
      return;

    poseVisibilityStore =
        AssetDatabase.LoadAssetAtPath<ViewportPoseVisibilityStore>(
            ViewportPoseVisibilityStore.DefaultAssetPath);

    if (poseVisibilityStore == null)
    {
      poseVisibilityStore =
          ScriptableObject.CreateInstance<ViewportPoseVisibilityStore>();
      poseVisibilityStore.MapId = "HallOfChampions";

      string folder = Path.GetDirectoryName(
          ViewportPoseVisibilityStore.DefaultAssetPath);
      if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
      {
        // Assets/EditorData already exists in this project.
        Directory.CreateDirectory(folder.Replace('\\', '/'));
        AssetDatabase.Refresh();
      }

      AssetDatabase.CreateAsset(
          poseVisibilityStore,
          ViewportPoseVisibilityStore.DefaultAssetPath);
    }

    SeedLocked12SouthPoseIfMissing();
  }

  /// <summary>
  /// Seeds (1,2) South from LockedBackup when the store has no entry yet.
  /// </summary>
  private void SeedLocked12SouthPoseIfMissing()
  {
    if (poseVisibilityStore == null || layout == null)
      return;

    if (poseVisibilityStore.TryFindEntry(
            1,
            2,
            DungeonFacing.South,
            out _))
    {
      return;
    }

    ViewportPoseVisibilityEntry south =
        poseVisibilityStore.GetOrCreateEntry(1, 2, DungeonFacing.South);

    // Full piece capture (Enabled + Mirror) from kit/safe defaults.
    ApplyUnknownPoseDefaultsToLayout();
    poseVisibilityStore.CaptureFromLayout(south, layout);

    EditorUtility.SetDirty(poseVisibilityStore);
    AssetDatabase.SaveAssets();
  }

  private void CaptureCurrentPoseVisibilityToStore()
  {
    if (Application.isPlaying || layout == null)
      return;

    // Never capture while PersistChanges has kit-baseline Enabled on layout.
    if (suppressPoseCaptureFromLayout)
      return;

    EnsurePoseVisibilityStore();
    if (poseVisibilityStore == null)
      return;

    ApplyFloorMirrorReferenceOverride();

    ViewportPoseVisibilityEntry entry =
        poseVisibilityStore.GetOrCreateEntry(
            previewX,
            previewY,
            previewFacing);
    poseVisibilityStore.CaptureFromLayout(entry, layout);
    EditorUtility.SetDirty(poseVisibilityStore);
  }

  private void ApplyCurrentPoseVisibilityToLayout()
  {
    if (layout == null)
      return;

    EnsurePoseVisibilityStore();
    if (poseVisibilityStore == null)
      return;

    if (!poseVisibilityStore.TryFindEntry(
            previewX,
            previewY,
            previewFacing,
            out ViewportPoseVisibilityEntry entry))
    {
      // First visit to this pose: start from safe defaults, then capture.
      ApplyUnknownPoseDefaultsToLayout();
      ApplyCeilingMirrorFromPose();
      ApplyFloorMirrorReferenceOverride();
      ApplyWallF0LeftFromMapGeometry();
      ApplyWallF0RightFromMapGeometry();
      ApplyWallF1LeftFromMapGeometry();
      ApplyWallF1RightFromMapGeometry();
      ApplyWallF2LeftFromMapGeometry();
      ApplyWallF2RightFromMapGeometry();
      ApplyWallF3LeftFromMapGeometry();
      ApplyWallF3RightFromMapGeometry();
    ApplyRightD3FromMapGeometry();
      ApplyFrontF1FromMapGeometry();
      ApplyFrontF2FromMapGeometry();
      ApplyFrontF3FromMapGeometry();
      ApplyBlackDoorEnabledFromPoseException();
      entry = poseVisibilityStore.GetOrCreateEntry(
          previewX,
          previewY,
          previewFacing);
      poseVisibilityStore.CaptureFromLayout(entry, layout);
      EditorUtility.SetDirty(poseVisibilityStore);
      return;
    }

    poseVisibilityStore.ApplyToLayout(entry, layout);
    ApplyCeilingMirrorFromPose();
    ApplyFloorMirrorReferenceOverride();
    ApplyWallF0LeftFromMapGeometry();
    ApplyWallF0RightFromMapGeometry();
    ApplyWallF1LeftFromMapGeometry();
    ApplyWallF1RightFromMapGeometry();
    ApplyWallF2LeftFromMapGeometry();
    ApplyWallF2RightFromMapGeometry();
    ApplyWallF3LeftFromMapGeometry();
    ApplyWallF3RightFromMapGeometry();
    ApplyRightD3FromMapGeometry();
    ApplyFrontF1FromMapGeometry();
    ApplyFrontF2FromMapGeometry();
    ApplyFrontF3FromMapGeometry();
    ApplyBlackDoorEnabledFromPoseException();
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
      piece.MirrorHorizontally =
          ((previewX + previewY + (int)previewFacing) & 1) == 0;
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
      piece.MirrorHorizontally =
          ((previewX + previewY + (int)previewFacing) & 1) != 0;
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
      piece.MirrorHorizontally =
          ((previewX + previewY + (int)previewFacing) & 1) == 0;
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
      piece.MirrorHorizontally =
          ((previewX + previewY + (int)previewFacing) & 1) != 0;
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
      piece.MirrorHorizontally =
          ((previewX + previewY + (int)previewFacing) & 1) == 0;
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
      piece.MirrorHorizontally =
          ((previewX + previewY + (int)previewFacing) & 1) == 0;
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
      piece.MirrorHorizontally =
          ((previewX + previewY + (int)previewFacing) & 1) != 0;
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
  /// FrontF2 / Front Wall F2 from the map tile two steps forward, only if
  /// the nearer forward tile is open. A solid F1 wall hides center FrontF2.
  /// Also on if the left column is open through F1 and the left F2 tile is solid.
  /// </summary>
  private void ApplyFrontF2FromMapGeometry()
  {
    if (layout == null || layout.Pieces == null)
      return;

    // (1,3) North is a Black Door front view. Force FrontF2 off so ViewEdit
    // matches the exception (not drawn) and the saved pose stays disabled.
    if (previewX == 1
        && previewY == 3
        && previewFacing == DungeonFacing.North)
    {
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null)
          continue;

        if (piece.Name != "FrontF2" && piece.Name != "Front Wall F2")
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
      int tileX = previewX + forwardX * 2;
      int tileY = previewY + forwardY * 2;
      centerF2Wall = previewMiniMap == null
          || !previewMiniMap.IsInside(tileX, tileY)
          || previewMiniMap.GetTile(tileX, tileY).Type == DungeonTileType.Wall;
    }

    int left1X = previewX + forwardX - rightX;
    int left1Y = previewY + forwardY - rightY;
    int left2X = previewX + forwardX * 2 - rightX;
    int left2Y = previewY + forwardY * 2 - rightY;

    bool left1Open = previewMiniMap != null
        && previewMiniMap.IsInside(left1X, left1Y)
        && previewMiniMap.GetTile(left1X, left1Y).Type != DungeonTileType.Wall;
    bool left2IsWall = previewMiniMap == null
        || !previewMiniMap.IsInside(left2X, left2Y)
        || previewMiniMap.GetTile(left2X, left2Y).Type == DungeonTileType.Wall;

    // Classify FrontF2 only from relative map geometry.
    // Center: F1 center open and F2 center solid/out.
    // LeftExposed: F1 center blocked, F1 left open, F2 left solid/out.
    bool centerF2 =
        !front1Blocked && centerF2Wall;
    bool leftExposedF2 =
        front1Blocked && left1Open && left2IsWall;

    bool frontF2IsWall = centerF2 || leftExposedF2;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "FrontF2" && piece.Name != "Front Wall F2")
        continue;

      piece.Enabled = frontF2IsWall;

      // DTerm FrontF2: identical center-F2 map geometry uses the authored
      // (1,5) West FrontF2 Width/X/Y as the canonical reference.
      // This makes geometry twins such as (1,5) West and (1,16) West render
      // identically while leaving Non-DTerm poses fully pose-authored.
      if (frontF2IsWall
          && centerF2
          && layout.FrontF2Deterministic
          && TryGetCenterFrontF2DeterministicReference(
              out int deterministicWidth,
              out int deterministicX,
              out int deterministicY))
      {
        piece.FrontWallF2Width = deterministicWidth;
        piece.X = deterministicX;
        piece.Y = deterministicY;
      }

      return;
    }
  }

  private bool TryGetCenterFrontF2DeterministicReference(
      out int width,
      out int x,
      out int y)
  {
    width = FrontWallF2Logic.DefaultWidth;
    x = 0;
    y = 0;

    EnsurePoseVisibilityStore();
    if (poseVisibilityStore == null || layout == null)
      return false;

    if (!poseVisibilityStore.TryFindEntry(
            1,
            5,
            DungeonFacing.West,
            out ViewportPoseVisibilityEntry referenceEntry))
    {
      return false;
    }

    ViewportLayout referenceLayout = UnityEngine.Object.Instantiate(layout);
    try
    {
      poseVisibilityStore.ApplyToLayout(referenceEntry, referenceLayout);

      if (referenceLayout.Pieces == null)
        return false;

      for (int i = 0; i < referenceLayout.Pieces.Count; i++)
      {
        ViewportPiece referencePiece = referenceLayout.Pieces[i];
        if (!IsFrontWallF2Card(referencePiece))
          continue;

        width = FrontWallF2Logic.Normalize(referencePiece.FrontWallF2Width);
        x = referencePiece.X;
        y = referencePiece.Y;
        return true;
      }
    }
    finally
    {
      UnityEngine.Object.DestroyImmediate(referenceLayout);
    }

    return false;
  }

  /// <summary>
  /// FrontF3 / Front Wall F3 from the map tile three steps forward, only if
  /// both nearer forward tiles are open. A solid F1 or F2 wall hides FrontF3.
  /// </summary>
  private void ApplyFrontF3FromMapGeometry()
  {
    if (layout == null || layout.Pieces == null)
      return;

    // (1,4) North is a Black Door F2 front view. Force FrontF3 off so ViewEdit
    // matches the exception and the saved pose stays disabled.
    if (previewX == 1
        && previewY == 4
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
      blackDoorF2CardEnabled = true;
      blackDoorF2CardInitialized = true;
      blackDoorFrameLeftF3CardEnabled = false;
      blackDoorFrameLeftF3CardInitialized = true;
      blackDoorFrameRightF3CardEnabled = false;
      blackDoorFrameRightF3CardInitialized = true;

      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null)
          continue;

        if (piece.Name != "Black Door Frame Right F2")
          continue;

        piece.Enabled = true;
        break;
      }

      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null)
          continue;

        if (piece.Name != "BlackDoorF1")
          continue;

        piece.Enabled = false;
        return;
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
      blackDoorFrameLeftF3CardInitialized = true;
      blackDoorFrameRightF3CardEnabled = false;
      blackDoorFrameRightF3CardInitialized = true;

      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null)
          continue;

        if (piece.Name != "Black Door Frame Left F1")
          continue;

        piece.Enabled = true;
        break;
      }

      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        if (piece == null)
          continue;

        if (piece.Name != "BlackDoorF1")
          continue;

        piece.Enabled = true;
        return;
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
    blackDoorFrameLeftF3CardInitialized = true;
    blackDoorFrameRightF3CardEnabled = true;
    blackDoorFrameRightF3CardInitialized = true;
    blackDoorFrameRightF3CardMirror = true;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "BlackDoorF1")
        continue;

      piece.Enabled = false;
      return;
    }
  }

  private void PersistPoseVisibilityStore()
  {
    if (Application.isPlaying || poseVisibilityStore == null)
      return;

    EditorUtility.SetDirty(poseVisibilityStore);
    AssetDatabase.SaveAssets();
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
      for (int i = 0; i < layout.Pieces.Count; i++)
      {
        ViewportPiece piece = layout.Pieces[i];
        bool shouldDraw = ShouldDrawPieceAtPreviewPose(piece);
        bool blackDoorF2Exception = IsBlackDoorF2PoseException(piece);
        bool blackDoorF3Exception = IsBlackDoorF3PoseException(piece);
        bool blackDoorObliqueRightD3Exception =
            IsBlackDoorObliqueRightD3PoseException(piece);

        if (!shouldDraw
            && !blackDoorF2Exception
            && !blackDoorF3Exception
            && !blackDoorObliqueRightD3Exception)
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

        bool mirror = GetPreviewMirror(piece, poseMap);
        DungeonGraphicType drawGraphic = piece.Graphic;
        if (IsWallF0LeftPiece(piece))
        {
          bool phaseOn =
              ((previewX + previewY + (int)previewFacing) & 1) == 0;
          drawGraphic = phaseOn
              ? DungeonGraphicType.WallF0R
              : DungeonGraphicType.WallF0L;
          mirror = phaseOn;
        }
        else if (IsWallF0RightPiece(piece))
        {
          bool phaseOn =
              ((previewX + previewY + (int)previewFacing) & 1) == 0;
          drawGraphic = phaseOn
              ? DungeonGraphicType.WallF0R
              : DungeonGraphicType.WallF0L;
          mirror = !phaseOn;
        }
        else if (IsWallF1LeftPiece(piece))
        {
          bool phaseOn =
              ((previewX + previewY + (int)previewFacing) & 1) == 0;
          drawGraphic = phaseOn
              ? DungeonGraphicType.WallF1R
              : DungeonGraphicType.WallF1L;
          mirror = phaseOn;
        }
        else if (IsWallF1RightPiece(piece))
        {
          bool phaseOn =
              ((previewX + previewY + (int)previewFacing) & 1) == 0;
          drawGraphic = phaseOn
              ? DungeonGraphicType.WallF1R
              : DungeonGraphicType.WallF1L;
          mirror = !phaseOn;
        }
        else if (IsWallF2LeftPiece(piece))
        {
          bool phaseOn =
              ((previewX + previewY + (int)previewFacing) & 1) == 0;
          drawGraphic = phaseOn
              ? DungeonGraphicType.WallF2R
              : DungeonGraphicType.WallF2L;
          mirror = phaseOn;
        }
        else if (IsWallF3LeftPiece(piece))
        {
          bool phaseOn =
              ((previewX + previewY + (int)previewFacing) & 1) == 0;
          drawGraphic = phaseOn
              ? DungeonGraphicType.WallF3R
              : DungeonGraphicType.WallF3L;
          mirror = phaseOn;
        }
        else if (IsWallF3RightPiece(piece))
        {
          bool phaseOn =
              ((previewX + previewY + (int)previewFacing) & 1) == 0;
          drawGraphic = phaseOn
              ? DungeonGraphicType.WallF3R
              : DungeonGraphicType.WallF3L;
          mirror = !phaseOn;
        }

        if (StraightF1WallLogic.IsStraightF1FrontGraphic(piece.Graphic))
        {
          int width = StraightF1WallLogic.NormalizeFrontWallF1Width(
              piece.FrontWallF1Width);
          Texture2D f1Texture = graphics.GetFrontWallF1Texture(width);
          if (f1Texture == null)
            continue;

          StraightF1WallLogic.BlitCompositeToBuffer(
              f1Texture,
              pixels,
              PreviewWidth,
              PreviewHeight,
              StraightF1WallLogic.FrontWallF1DestX(width, piece.EffectiveX),
              piece.EffectiveY,
              mirror);
          continue;
        }

        if (FrontWallF2Logic.IsFrontWallF2Graphic(piece.Graphic))
        {
          int width = FrontWallF2Logic.Normalize(piece.FrontWallF2Width);
          Texture2D f2Texture = graphics.GetFrontWallF2Texture(width);
          if (f2Texture == null)
            continue;

          if (width == FrontWallF2Logic.Width160
              && f2Texture.width == FrontWallF2Logic.Width160)
          {
            FrontWallF2Logic.BlitToBuffer(
                f2Texture,
                pixels,
                PreviewWidth,
                PreviewHeight,
                piece.EffectiveX,
                piece.EffectiveY,
                mirror);
          }
          else
          {
            BlitPieceIntoPreview(
                pixels,
                f2Texture,
                piece.EffectiveX,
                piece.EffectiveY,
                mirror);
          }

          continue;
        }

        Texture2D texture = graphics.GetTexture(drawGraphic);
        if (texture == null)
          continue;

        if (piece.Graphic == DungeonGraphicType.FrontWallF3)
        {
          Texture2D built = ExpandedF3WallTexture.BuildExpandedF3Wall(
              graphics.FrontWallF3,
              graphics.WallF3L,
              graphics.WallF3R);
          bool sameAsHelper = ReferenceEquals(texture, built)
              || ReferenceEquals(
                  texture,
                  ExpandedF3WallTexture.LastReturnedTexture);
          string key =
              (piece.Name ?? "")
              + "|"
              + texture.name
              + "|"
              + texture.width
              + "x"
              + texture.height
              + "|"
              + piece.EffectiveX
              + ","
              + piece.EffectiveY
              + "|"
              + sameAsHelper
              + "|Edit";
          if (key != lastLoggedFrontWallF3EditDrawKey)
          {
            lastLoggedFrontWallF3EditDrawKey = key;
            Debug.Log(
                "F3 DRAW: "
                    + piece.Name
                    + " / Graphic="
                    + (int)piece.Graphic
                    + " ("
                    + piece.Graphic
                    + ") / Texture="
                    + texture.name
                    + " / Size="
                    + texture.width
                    + "x"
                    + texture.height
                    + " / X="
                    + piece.EffectiveX
                    + " / Y="
                    + piece.EffectiveY
                    + " / GetTexture==BuildExpandedF3Wall="
                    + sameAsHelper
                    + " (Edit Mode)");
          }
        }

        if (F3RightNarrowStripTest.ShouldReplace(piece.Graphic)
            && drawGraphic == DungeonGraphicType.WallF3R)
        {
          F3RightNarrowStripTest.BlitToBuffer(
              texture,
              pixels,
              PreviewWidth,
              PreviewHeight,
              piece.EffectiveX,
              piece.EffectiveY);
          continue;
        }

        if (D3R2NarrowWidthTest.ShouldReplace(piece.Graphic))
        {
          if (blackDoorObliqueRightD3Exception)
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
          continue;
        }

        // (1,4) North: F2 frames already blitted in kit order; BlackDoorF2
        // 66×64 1:1 last so it covers overlapping inner frame pixels.
        if (blackDoorF2Exception)
        {
          Texture2D f2Source = GetBlackDoorF2SourceTexture();
          if (f2Source != null)
          {
            BlitPieceIntoPreview(
                pixels,
                f2Source,
                piece.ResolvedBlackDoorF2X,
                piece.ResolvedBlackDoorF2Y,
                mirror);
          }
          continue;
        }

        // (1,5) North: F3 frames first, then BlackDoorF3 45×39 1:1 on top.
        if (blackDoorF3Exception)
        {
          BlitBlackDoorF3FramesIntoPreview(pixels);
          ViewportPiece doorF3 = FindLayoutPieceByName("BlackDoorF3");
          int f3X = doorF3 != null ? doorF3.X : blackDoorF3CardX;
          int f3Y = doorF3 != null ? doorF3.Y : blackDoorF3CardY;
          Texture2D f3Source = GetBlackDoorF3SourceTexture();
          if (f3Source != null)
          {
            BlitPieceIntoPreview(
                pixels,
                f3Source,
                f3X,
                f3Y,
                mirror);
          }
          continue;
        }

        BlitPieceIntoPreview(
            pixels,
            texture,
            piece.EffectiveX,
            piece.EffectiveY,
            mirror);
      }

      BlitBlackDoorObliqueRightF3FrameIntoPreview(pixels);
      BlitFrontWallF2_160ExtraStripIntoPreview(pixels, poseMap);
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
    Debug.Log(message);
  }

  private static DungeonBitmapFont FindEditModeBitmapFont()
  {
    Debug.Log("FindEditModeBitmapFont CALLED");

    DungeonBitmapFont[] fonts =
        Resources.FindObjectsOfTypeAll<DungeonBitmapFont>();

    for (int i = 0; i < fonts.Length; i++)
    {
      DungeonBitmapFont font = fonts[i];
      if (font == null)
        continue;

      Debug.Log(
          "FindEditModeBitmapFont candidate: "
              + font.name
              + " AlphabetGridNull="
              + (font.AlphabetGrid == null)
              + " IsPersistent="
              + EditorUtility.IsPersistent(font)
              + " SceneValid="
              + font.gameObject.scene.IsValid()
              + " SceneLoaded="
              + font.gameObject.scene.isLoaded);

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
  /// Edit Mode draw gate: authored Enabled for the current X/Y/Facing pose only.
  /// Does not derive visibility from map geometry or the pattern catalog.
  /// Enabled comes from ViewportPoseVisibility (same store as Play/Build).
  /// </summary>
  private static bool ShouldDrawPieceAtPreviewPose(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (piece.Graphic == DungeonGraphicType.None)
      return false;

    return piece.Enabled;
  }

  /// <summary>
  /// (1,4) North Black Door F2 size exception. Draw may run even when the
  /// normal Black Door Enabled flag is off. Does not write pose data.
  /// </summary>
  private bool IsBlackDoorF2PoseException(ViewportPiece piece)
  {
    if (piece == null)
      return false;

    if (piece.Graphic != DungeonGraphicType.BlackDoor)
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

    if (blackDoorFrameLeftF3CardEnabled)
    {
      ViewportPiece leftF3 = FindLayoutPieceByName("Black Door Frame Left F3");
      int leftX = leftF3 != null ? leftF3.X : blackDoorFrameLeftF3CardX;
      int leftY = leftF3 != null ? leftF3.Y : blackDoorFrameLeftF3CardY;
      BlitPieceIntoPreview(
          pixels,
          source,
          leftX,
          leftY,
          blackDoorFrameLeftF3CardMirror);
    }

    if (blackDoorFrameRightF3CardEnabled)
    {
      ViewportPiece rightF3 = FindLayoutPieceByName("Black Door Frame Right F3");
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
  private static bool GetPreviewMirror(ViewportPiece piece, DungeonMap poseMap)
  {
    if (piece == null)
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

  private static void BlitPieceIntoPreview(
      Color32[] dest,
      Texture2D source,
      int destinationX,
      int destinationY,
      bool mirrorHorizontally = false)
  {
    if (!source.isReadable)
      return;

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
