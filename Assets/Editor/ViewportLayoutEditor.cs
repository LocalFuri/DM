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
    ("Black Door Frame Left", false),
    ("Black Door Frame Right", false),
    ("Black Door", false),
  };

  [System.NonSerialized]
  private ViewportLayout layout;
  [System.NonSerialized]
  private DungeonGraphics graphics;
  [System.NonSerialized]
  private ViewportPoseVisibilityStore poseVisibilityStore;
  private Vector2 editorScroll;
  private int pieceSearchFamilyIndex;
  private string pieceSearchText = string.Empty;
  private bool openSearchPiecesPopup;
  private bool focusSearchPieces;
  private GUIStyle searchPiecesLabelStyle;
  private GUIStyle pieceFamilyHeaderStyle;
  private bool[] rememberedEnabledStates;
  private int snap = 1;

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
    RestoreViewportTextureAndDestroyPreview();
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
  /// </summary>
  private static bool IsHiddenFromEditorPieceList(ViewportPiece piece)
  {
    if (piece == null || piece.Name == null)
      return false;

    return piece.Name == "Ceiling Strip 84"
        || piece.Name == "Ceiling Strip 85";
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
    if (pieceSearchFamilyIndex <= 0
        || pieceSearchFamilyIndex >= PieceSearchFamilyOptions.Length)
    {
      return true;
    }

    return PieceMatchesSearchFamily(
        piece,
        PieceSearchFamilyOptions[pieceSearchFamilyIndex]);
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
      params GUILayoutOption[] options)
  {
    Event current = Event.current;
    bool isKeyboard = current != null && current.isKey;
    EventType savedType = EventType.Ignore;
    bool savedChanged = GUI.changed;
    if (isKeyboard)
    {
      savedType = current.type;
      current.type = EventType.Used;
    }

    int keyboardBefore = GUIUtility.keyboardControl;
    bool result = EditorGUILayout.Toggle(label, value, options);
    if (GUIUtility.keyboardControl != keyboardBefore)
      GUIUtility.keyboardControl = 0;

    if (isKeyboard)
    {
      current.type = savedType;
      GUI.changed = savedChanged;
      return value;
    }

    return result;
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
    piece.Name = EditorGUILayout.TextField("Name", piece.Name);

    EditorGUILayout.BeginHorizontal();
    GUILayout.FlexibleSpace();
    float previousLabelWidth = EditorGUIUtility.labelWidth;
    EditorGUIUtility.labelWidth = 55f;
    piece.Enabled = DrawMouseOnlyToggle(
        "Enabled",
        piece.Enabled,
        GUILayout.Width(72));
    bool nameOrEnabledChanged = EditorGUI.EndChangeCheck();

    // Mirror is outside the Name/Enabled/Graphic change-check so SelectPiece →
    // FocusControl(null) cannot swallow the Toggle or skip the live refresh.
    bool mirrorBefore = piece.MirrorHorizontally;
    GUILayout.FlexibleSpace();
    const string MirrorLabel = "Mirror Horizontally";
    float mirrorLabelWidth =
        EditorStyles.label.CalcSize(new GUIContent(MirrorLabel)).x;
    EditorGUIUtility.labelWidth = mirrorLabelWidth;
    piece.MirrorHorizontally = DrawMouseOnlyToggle(
        MirrorLabel,
        piece.MirrorHorizontally,
        GUILayout.Width(mirrorLabelWidth + 18f),
        GUILayout.ExpandWidth(false));
    EditorGUIUtility.labelWidth = previousLabelWidth;
    EditorGUILayout.EndHorizontal();

    EditorGUI.BeginChangeCheck();
    piece.Graphic = (DungeonGraphicType)EditorGUILayout.EnumPopup("Graphic", piece.Graphic);
    if (EditorGUI.EndChangeCheck() || nameOrEnabledChanged)
    {
      SelectPiece(index);
      changed = true;
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
          new[] { "106", "131", "160" },
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

    if (DrawIntStepper("X", ref piece.X, snap))
    {
      SelectPiece(index);
      changed = true;
    }

    if (DrawTopDownYStepper(piece, snap))
    {
      SelectPiece(index);
      changed = true;
    }

    if (IsPoseOffsetCard(piece))
    {
      int offsetXBefore = piece.PoseOffsetX;
      int offsetYBefore = piece.PoseOffsetY;
      DrawIntStepper("Pose Offset X", ref piece.PoseOffsetX, 1);
      DrawIntStepper("Pose Offset Y", ref piece.PoseOffsetY, 1);
      if (piece.PoseOffsetX != offsetXBefore
          || piece.PoseOffsetY != offsetYBefore)
      {
        SelectPiece(index);
        ApplyPoseOffsetChangeForCurrentPose();
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

    EditorGUILayout.EndHorizontal();
    EditorGUILayout.EndVertical();
  }

  /// <summary>
  /// ViewEdit list-label tint for matching wall-piece families. Does not
  /// change piece data, Enabled, or rendering.
  /// </summary>
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

    DrawPreviewMiniMap();
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
      ApplyFloorMirrorFromPose();
      ApplyWallF0LeftFromMapGeometry();
      ApplyWallF0RightFromMapGeometry();
      ApplyWallF1LeftFromMapGeometry();
      return;
    }

    // Unknown pose: kit baseline on the live layout only — do not write store.
    ApplyUnknownPoseDefaultsToLayout();
    ApplyCeilingMirrorFromPose();
    ApplyFloorMirrorFromPose();
    ApplyWallF0LeftFromMapGeometry();
    ApplyWallF0RightFromMapGeometry();
    ApplyWallF1LeftFromMapGeometry();
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
      ApplyFloorMirrorFromPose();
      ApplyWallF0LeftFromMapGeometry();
      ApplyWallF0RightFromMapGeometry();
      ApplyWallF1LeftFromMapGeometry();
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
    ApplyFloorMirrorFromPose();
    ApplyWallF0LeftFromMapGeometry();
    ApplyWallF0RightFromMapGeometry();
    ApplyWallF1LeftFromMapGeometry();
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
  /// Floor mirror from (1,3) North = ON. Same pose formula as Ceiling.
  /// Does not change Ceiling or walls.
  /// </summary>
  private void ApplyFloorMirrorFromPose()
  {
    if (layout == null || layout.Pieces == null)
      return;

    bool mirrorOn =
        ((previewX + previewY + (int)previewFacing) & 1) == 0;

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
  /// Wall F1Left / LeftF1 from the map tile one step forward and one step left.
  /// Solid/out-of-bounds → Enabled. Does not change F0, F1Right, Front F1, or mirrors.
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
    int tileX = previewX + forwardX - rightX;
    int tileY = previewY + forwardY - rightY;
    bool leftF1IsWall = previewMiniMap == null
        || !previewMiniMap.IsInside(tileX, tileY)
        || previewMiniMap.GetTile(tileX, tileY).Type == DungeonTileType.Wall;

    for (int i = 0; i < layout.Pieces.Count; i++)
    {
      ViewportPiece piece = layout.Pieces[i];
      if (piece == null)
        continue;

      if (piece.Name != "Wall F1Left" && piece.Name != "LeftF1")
        continue;

      piece.Enabled = leftF1IsWall;
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

        if (!shouldDraw)
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

        if (F3RightNarrowStripTest.ShouldReplace(piece.Graphic))
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
          D3R2NarrowWidthTest.BlitToBuffer(
              texture,
              pixels,
              PreviewWidth,
              PreviewHeight,
              piece.EffectiveX,
              piece.EffectiveY);
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

        BlitPieceIntoPreview(
            pixels,
            texture,
            piece.EffectiveX,
            piece.EffectiveY,
            mirror);
      }

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
    DungeonBitmapFont[] fonts =
        Object.FindObjectsByType<DungeonBitmapFont>(
            FindObjectsInactive.Include);

    for (int i = 0; i < fonts.Length; i++)
    {
      if (fonts[i] != null && fonts[i].AlphabetGrid != null)
        return fonts[i];
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
