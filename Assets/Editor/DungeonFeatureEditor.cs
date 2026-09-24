using System.Collections.Generic;
using System.IO;
using DM.Dungeon;
using UnityEditor;
using UnityEngine;

public class DungeonFeatureEditor : EditorWindow
{
  private const string HallOfChampionsMapPath =
      "Assets/Data/Maps/HallOfChampions.json";
  private const string PuddleF1AssetPath =
      "Assets/Art/Ornaments/Puddle_F1.png";

  // Hall of Champions uses three random floor ornaments. The level-local
  // FloorOrnate table is Moss, Puddle, Floor Crack for ordinals 1..3.
  // Puddles therefore resolve from random floor ornament ordinal 2.
  private const int HallOfChampionsRandomFloorOrnamentCount = 3;
  private const int HallOfChampionsPuddleFloorOrnamentOrdinal = 2;
  private const int HallOfChampionsRandomOrnamentModulo = 30;
  private const int HallOfChampionsDungeonSeed = 99;

  // Shared ViewEdit preview pose. Dungeon Features reads these to follow
  // ViewEdit and writes X/Y when the user clicks a map tile.
  private const string ViewEditPreviewXKey =
      "ViewportLayoutEditor.PreviewX";
  private const string ViewEditPreviewYKey =
      "ViewportLayoutEditor.PreviewY";
  private const string ViewEditPreviewFacingKey =
      "ViewportLayoutEditor.PreviewFacing";

  private const float CellSize = 32f;
  private const float LabelLeftMargin = 28f;
  private const float LabelTopMargin = 20f;
  // Extra vertical canvas above/below the map for labels that are drawn
  // outside a wall face. This also keeps y=0 and y=31 labels fully visible
  // on 32-row dungeon levels.
  private const float OutsideFeatureVerticalMargin = 32f;
  // Virtual column just past the Hall of Champions map (x = 0..17).
  private const int OutsideNameColumnX = 18;

  private static readonly Color WallColor = new Color(0.22f, 0.22f, 0.22f);
  private static readonly Color FloorColor = new Color(0.78f, 0.78f, 0.78f);
  private static readonly Color CellBorderColor = new Color(0.08f, 0.08f, 0.08f);
  private static readonly Color SelectionFillColor =
      new Color(0.35f, 0.75f, 1f, 0.35f);
  private static readonly Color SelectionBorderColor =
      new Color(0.2f, 0.85f, 1f, 1f);

  // Champion mirror map marker: full-edge blue wall-face bar with a small yellow center.
  private static readonly Color ChampionMirrorLineColor =
      new Color(0.0f, 0.0f, 1.0f, 1f);
  private static readonly Color ChampionMirrorDotColor =
      new Color(1.0f, 0.9f, 0.0f, 1f);
  private static readonly Color ChampionNameColor =
      new Color(0f, 1f, 0f, 1f);

  // Fallback color used only when an ornament texture cannot be found.
  private static readonly Color OrnamentFallbackColor =
      new Color(1f, 0.55f, 0f, 1f);

  private enum WallSide
  {
    North,
    East,
    South,
    West
  }

  private sealed class ChampionMirrorMarker
  {
    public int X;
    public int Y;
    public WallSide Side;
    public string Champion;

    public ChampionMirrorMarker(
        int x,
        int y,
        WallSide side,
        string champion)
    {
      X = x;
      Y = y;
      Side = side;
      Champion = champion;
    }
  }

  // Hall of Champions mirror positions extracted from the original
  // direction-aware level data. Direction is the wall face on which the
  // champion portrait/mirror appears.
  private sealed class OriginalWallOrnamentMarker
  {
    public string Type;
    public int X;
    public int Y;
    public WallSide Side;
    public bool WallTilePlacement;

    public OriginalWallOrnamentMarker(
        string type,
        int x,
        int y,
        WallSide side,
        bool wallTilePlacement)
    {
      Type = type;
      X = x;
      Y = y;
      Side = side;
      WallTilePlacement = wallTilePlacement;
    }
  }

  // Every Hall of Champions wall ornament is drawn on the impassable grey
  // wall tile, on the face stored in the raw direction bits. Those faces are
  // the directions that are not a possible walk. The sensor wood ring at
  // (6,8) South is not a random face bit, so it is added explicitly.
  private OriginalWallOrnamentMarker[] resolvedWallOrnaments =
      new OriginalWallOrnamentMarker[0];
  private readonly HashSet<int> resolvedPuddleFloors = new HashSet<int>();

  private static readonly ChampionMirrorMarker[] ChampionMirrorMarkers =
  {
    new ChampionMirrorMarker(10, 4, WallSide.North, "Iaido"),
    new ChampionMirrorMarker(10, 5, WallSide.South, "Zed"),
    new ChampionMirrorMarker(14, 3, WallSide.North, "Chani"),
    new ChampionMirrorMarker(15, 4, WallSide.East, "Hawk"),
    new ChampionMirrorMarker(14, 6, WallSide.South, "Boris"),
    new ChampionMirrorMarker(16, 8, WallSide.North, "Alex"),
    new ChampionMirrorMarker(17, 9, WallSide.South, "Nabi"),
    new ChampionMirrorMarker(16, 14, WallSide.South, "Hissssa"),
    new ChampionMirrorMarker(16, 17, WallSide.North, "Gothmog"),
    new ChampionMirrorMarker(14, 12, WallSide.East, "Sonja"),
    new ChampionMirrorMarker(13, 12, WallSide.West, "Leyla"),
    new ChampionMirrorMarker(13, 14, WallSide.East, "Mophus"),
    new ChampionMirrorMarker(12, 13, WallSide.West, "Wuuf"),
    new ChampionMirrorMarker(11, 15, WallSide.South, "Stamm"),
    new ChampionMirrorMarker(7, 16, WallSide.South, "Azizi"),
    new ChampionMirrorMarker(8, 15, WallSide.North, "Leif"),
    new ChampionMirrorMarker(9, 13, WallSide.East, "Tiggy"),
    new ChampionMirrorMarker(7, 13, WallSide.South, "Wu Tse"),
    new ChampionMirrorMarker(6, 13, WallSide.West, "Daroou"),
    new ChampionMirrorMarker(7, 9, WallSide.North, "Halk"),
    new ChampionMirrorMarker(9, 9, WallSide.South, "Syra"),
    new ChampionMirrorMarker(11, 10, WallSide.South, "Gando"),
    new ChampionMirrorMarker(12, 9, WallSide.North, "Linflas"),
    new ChampionMirrorMarker(9, 7, WallSide.West, "Elija")
  };

  private DungeonMap map;
  private string mapLoadError;
  private Vector2 mapScroll;
  private int selectedX = 1;
  private int selectedY = 2;
  private DungeonFacing selectedFacing = DungeonFacing.North;

  private bool ornamentTexturesResolved;
  private Texture2D hookMapIcon;
  private Texture2D woodRingMapIcon;
  private Texture2D slimeMapIcon;
  private Texture2D grateMapIcon;
  private Texture2D puddleMapIcon;

  [MenuItem("Tools/Dungeon Feature Editor &f")]
  public static void Open()
  {
    DungeonFeatureEditor window =
        GetWindow<DungeonFeatureEditor>("Dungeon Features");
    window.minSize = new Vector2(1200f, 800f);
    window.Focus();
  }

  private void OnEnable()
  {
    ornamentTexturesResolved = false;
    hookMapIcon = null;
    woodRingMapIcon = null;
    slimeMapIcon = null;
    grateMapIcon = null;
    puddleMapIcon = null;

    LoadMap();
    SyncSelectionFromViewEdit();
  }

  private void OnInspectorUpdate()
  {
    if (SyncSelectionFromViewEdit())
      Repaint();
  }

  private void LoadMap()
  {
    map = null;
    mapLoadError = null;

    if (!File.Exists(HallOfChampionsMapPath))
    {
      mapLoadError = "Map not found at " + HallOfChampionsMapPath;
      return;
    }

    try
    {
      string json = File.ReadAllText(HallOfChampionsMapPath);
      map = DungeonMap.LoadFromJsonText(json);
      resolvedWallOrnaments = BuildGreyWallOrnaments(map);
      BuildHallOfChampionsPuddleFloors(map, resolvedPuddleFloors);
      selectedX = 1;
      selectedY = 2;
    }
    catch (System.Exception ex)
    {
      map = null;
      resolvedWallOrnaments = new OriginalWallOrnamentMarker[0];
      resolvedPuddleFloors.Clear();
      mapLoadError = ex.Message;
    }
  }

  private bool SyncSelectionFromViewEdit()
  {
    if (map == null
        || !EditorPrefs.HasKey(ViewEditPreviewXKey)
        || !EditorPrefs.HasKey(ViewEditPreviewYKey))
    {
      return false;
    }

    int viewEditX = EditorPrefs.GetInt(ViewEditPreviewXKey, selectedX);
    int viewEditY = EditorPrefs.GetInt(ViewEditPreviewYKey, selectedY);
    DungeonFacing viewEditFacing = (DungeonFacing)EditorPrefs.GetInt(
        ViewEditPreviewFacingKey,
        (int)selectedFacing);

    if (!map.IsInside(viewEditX, viewEditY))
      return false;

    bool changed =
        selectedX != viewEditX
        || selectedY != viewEditY
        || selectedFacing != viewEditFacing;

    if (!changed)
      return false;

    selectedX = viewEditX;
    selectedY = viewEditY;
    selectedFacing = viewEditFacing;
    return true;
  }

  private bool HandleRightMouseWindowSwitch()
  {
    Event current = Event.current;
    if (current == null
        || current.type != EventType.MouseDown
        || current.button != 1)
    {
      return false;
    }

    current.Use();
    EditorApplication.delayCall += ViewportLayoutEditor.Open;
    return true;
  }

  private void OnGUI()
  {
    if (HandleRightMouseWindowSwitch())
      return;

    Event current = Event.current;
    if (current.type == EventType.KeyDown
        && current.keyCode == KeyCode.Escape)
    {
      current.Use();
      Close();
      return;
    }

    if (map == null)
    {
      EditorGUILayout.HelpBox(
          string.IsNullOrEmpty(mapLoadError)
              ? "Could not load Hall of Champions map."
              : mapLoadError,
          MessageType.Error);
      return;
    }

    GUIStyle poseStyle = new GUIStyle(EditorStyles.boldLabel)
    {
      fontSize = 14
    };

    EditorGUILayout.LabelField(
        $"{selectedX} X / Y {selectedY} {selectedFacing}",
        poseStyle,
        GUILayout.Height(20f));

    DrawMapGrid();
  }

  private void DrawMapGrid()
  {
    float mapPixelWidth = map.Width * CellSize;
    float mapPixelHeight = map.Height * CellSize;
    float contentWidth =
        LabelLeftMargin
        + Mathf.Max(map.Width, OutsideNameColumnX + 2) * CellSize;
    float contentHeight =
        LabelTopMargin
        + OutsideFeatureVerticalMargin
        + mapPixelHeight
        + OutsideFeatureVerticalMargin;

    mapScroll = EditorGUILayout.BeginScrollView(mapScroll);

    Rect contentRect = GUILayoutUtility.GetRect(
        contentWidth,
        contentHeight,
        GUILayout.Width(contentWidth),
        GUILayout.Height(contentHeight),
        GUILayout.ExpandWidth(false),
        GUILayout.ExpandHeight(false)
    );

    Rect mapRect = new Rect(
        contentRect.x + LabelLeftMargin,
        contentRect.y + LabelTopMargin + OutsideFeatureVerticalMargin,
        mapPixelWidth,
        mapPixelHeight
    );

    DrawAxisLabels(mapRect);

    for (int y = 0; y < map.Height; y++)
    {
      for (int x = 0; x < map.Width; x++)
      {
        Rect cellRect = new Rect(
            mapRect.x + x * CellSize,
            mapRect.y + y * CellSize,
            CellSize,
            CellSize
        );

        DrawCell(cellRect, map.GetTile(x, y), x == selectedX && y == selectedY);
      }
    }

    DrawPuddleFloorIcons(mapRect);
    DrawChampionMirrorMarkers(mapRect);
    DrawOriginalWallOrnamentIcons(mapRect);
    HandleGridClick(mapRect);

    EditorGUILayout.EndScrollView();
  }

  private void DrawAxisLabels(Rect mapRect)
  {
    GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
    {
      alignment = TextAnchor.MiddleCenter,
      fontSize = 10,
      clipping = TextClipping.Overflow
    };

    GUIStyle yLabelStyle = new GUIStyle(labelStyle)
    {
      alignment = TextAnchor.MiddleRight
    };

    const float xLabelWidth = 20f;

    for (int x = 0; x < map.Width; x++)
    {
      float columnCenterX =
          mapRect.x + x * CellSize + CellSize * 0.5f;

      Rect xLabelRect = new Rect(
          columnCenterX - xLabelWidth * 0.5f,
          mapRect.y - LabelTopMargin,
          xLabelWidth,
          LabelTopMargin
      );

      GUI.Label(xLabelRect, x.ToString(), labelStyle);
    }

    for (int y = 0; y < map.Height; y++)
    {
      Rect yLabelRect = new Rect(
          mapRect.x - LabelLeftMargin,
          mapRect.y + y * CellSize,
          LabelLeftMargin - 2f,
          CellSize
      );

      GUI.Label(yLabelRect, y.ToString(), yLabelStyle);
    }
  }

  private static void DrawCell(
      Rect cellRect,
      DungeonTile tile,
      bool isSelected)
  {
    EditorGUI.DrawRect(cellRect, CellBorderColor);

    Rect fillRect = new Rect(
        cellRect.x + 1f,
        cellRect.y + 1f,
        cellRect.width - 2f,
        cellRect.height - 2f
    );

    Color fillColor = tile.Type == DungeonTileType.Wall
        ? WallColor
        : FloorColor;

    EditorGUI.DrawRect(fillRect, fillColor);

    if (!isSelected)
      return;

    EditorGUI.DrawRect(fillRect, SelectionFillColor);
    EditorGUI.DrawRect(
        new Rect(cellRect.x, cellRect.y, cellRect.width, 2f),
        SelectionBorderColor);
    EditorGUI.DrawRect(
        new Rect(
            cellRect.x,
            cellRect.yMax - 2f,
            cellRect.width,
            2f),
        SelectionBorderColor);
    EditorGUI.DrawRect(
        new Rect(cellRect.x, cellRect.y, 2f, cellRect.height),
        SelectionBorderColor);
    EditorGUI.DrawRect(
        new Rect(
            cellRect.xMax - 2f,
            cellRect.y,
            2f,
            cellRect.height),
        SelectionBorderColor);
  }

  private void DrawChampionMirrorMarkers(Rect mapRect)
  {
    const float lineThickness = 3f;
    const float dotSize = 4f;

    for (int i = 0; i < ChampionMirrorMarkers.Length; i++)
    {
      ChampionMirrorMarker marker = ChampionMirrorMarkers[i];

      Rect cellRect = new Rect(
          mapRect.x + marker.X * CellSize,
          mapRect.y + marker.Y * CellSize,
          CellSize,
          CellSize);

      Rect lineRect;
      Rect dotRect;

      switch (marker.Side)
      {
        case WallSide.North:
        {
          float centerX = cellRect.center.x;
          lineRect = new Rect(
              cellRect.x,
              cellRect.y,
              cellRect.width,
              lineThickness);
          dotRect = new Rect(
              centerX - dotSize * 0.5f,
              cellRect.y - (dotSize - lineThickness) * 0.5f,
              dotSize,
              dotSize);
          break;
        }

        case WallSide.East:
        {
          float centerY = cellRect.center.y;
          lineRect = new Rect(
              cellRect.xMax - lineThickness,
              cellRect.y,
              lineThickness,
              cellRect.height);
          dotRect = new Rect(
              cellRect.xMax - lineThickness
                  - (dotSize - lineThickness) * 0.5f,
              centerY - dotSize * 0.5f,
              dotSize,
              dotSize);
          break;
        }

        case WallSide.South:
        {
          float centerX = cellRect.center.x;
          lineRect = new Rect(
              cellRect.x,
              cellRect.yMax - lineThickness,
              cellRect.width,
              lineThickness);
          dotRect = new Rect(
              centerX - dotSize * 0.5f,
              cellRect.yMax - lineThickness
                  - (dotSize - lineThickness) * 0.5f,
              dotSize,
              dotSize);
          break;
        }

        default: // West
        {
          float centerY = cellRect.center.y;
          lineRect = new Rect(
              cellRect.x,
              cellRect.y,
              lineThickness,
              cellRect.height);
          dotRect = new Rect(
              cellRect.x - (dotSize - lineThickness) * 0.5f,
              centerY - dotSize * 0.5f,
              dotSize,
              dotSize);
          break;
        }
      }

      EditorGUI.DrawRect(lineRect, ChampionMirrorLineColor);
      EditorGUI.DrawRect(dotRect, ChampionMirrorDotColor);

      DrawChampionName(mapRect, cellRect, marker);
    }
  }

  private void DrawPuddleFloorIcons(Rect mapRect)
  {
    if (resolvedPuddleFloors.Count == 0)
      return;

    EnsureOrnamentTexturesResolved();

    const float iconInset = 3f;
    for (int y = 0; y < map.Height; y++)
    {
      for (int x = 0; x < map.Width; x++)
      {
        if (!resolvedPuddleFloors.Contains(PackFloorFeatureTile(x, y)))
          continue;

        Rect cellRect = new Rect(
            mapRect.x + x * CellSize,
            mapRect.y + y * CellSize,
            CellSize,
            CellSize);
        Rect iconRect = new Rect(
            cellRect.x + iconInset,
            cellRect.y + iconInset,
            cellRect.width - iconInset * 2f,
            cellRect.height - iconInset * 2f);

        if (puddleMapIcon != null)
        {
          GUI.DrawTexture(
              iconRect,
              puddleMapIcon,
              ScaleMode.ScaleToFit,
              true);
        }
        else
        {
          DrawMissingFloorOrnamentFallback(iconRect, "P");
        }
      }
    }
  }

  private static void BuildHallOfChampionsPuddleFloors(
      DungeonMap map,
      HashSet<int> destination)
  {
    destination.Clear();
    if (map == null)
      return;

    int value2 = 3000 + map.Width + map.Height;

    for (int y = 0; y < map.Height; y++)
    {
      for (int x = 0; x < map.Width; x++)
      {
        DungeonTile tile = map.GetTile(x, y);
        if (tile == null
            || tile.SourceType != DungeonSourceTileType.Floor
            || (tile.Raw & 0x08) == 0)
        {
          continue;
        }

        int randomIndex = HallOfChampionsRandomOrnamentHash(
            2000 + (x << 5) + y,
            value2,
            HallOfChampionsRandomOrnamentModulo);

        if (randomIndex >= HallOfChampionsRandomFloorOrnamentCount)
          continue;

        int ordinal = randomIndex + 1;
        if (ordinal == HallOfChampionsPuddleFloorOrnamentOrdinal)
          destination.Add(PackFloorFeatureTile(x, y));
      }
    }
  }

  private static int HallOfChampionsRandomOrnamentHash(
      int value1,
      int value2,
      int modulo)
  {
    if (modulo <= 0)
      return 0;

    unchecked
    {
      uint d0Long = (uint)(ushort)value1 * 31417u;
      ushort d0Word = (ushort)d0Long;
      d0Word = (ushort)((d0Word >> 1) & 0x7FFF);

      uint d1Long = (uint)(ushort)value2 * 11u;
      d0Word = (ushort)(d0Word + (ushort)d1Long);
      d0Word = (ushort)(d0Word + HallOfChampionsDungeonSeed);
      d0Word = (ushort)((d0Word >> 2) & 0x3FFF);

      return d0Word % modulo;
    }
  }

  private static int PackFloorFeatureTile(int x, int y)
  {
    return (y << 8) | (x & 0xFF);
  }

  private static void DrawMissingFloorOrnamentFallback(
      Rect iconRect,
      string text)
  {
    EditorGUI.DrawRect(iconRect, OrnamentFallbackColor);

    GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
    {
      alignment = TextAnchor.MiddleCenter,
      clipping = TextClipping.Clip
    };
    style.normal.textColor = Color.black;
    GUI.Label(iconRect, text, style);
  }

  private void DrawOriginalWallOrnamentIcons(Rect mapRect)
  {
    EnsureOrnamentTexturesResolved();
    DrawWallOrnamentIconList(mapRect, resolvedWallOrnaments);
  }

  private static OriginalWallOrnamentMarker[] BuildGreyWallOrnaments(
      DungeonMap map)
  {
    HallWallOrnamentPlacement[] generated =
        HallOfChampionsWallOrnaments.Build(map);
    List<OriginalWallOrnamentMarker> markers =
        new List<OriginalWallOrnamentMarker>();

    if (generated != null)
    {
      for (int i = 0; i < generated.Length; i++)
      {
        HallWallOrnamentPlacement ornament = generated[i];
        if (ornament == null
            || !TryParseWallSide(ornament.Wall, out WallSide side))
        {
          continue;
        }

        markers.Add(
            new OriginalWallOrnamentMarker(
                ornament.Type,
                ornament.X,
                ornament.Y,
                side,
                true));
      }
    }

    bool hasSensorWoodRing = false;
    for (int i = 0; i < markers.Count; i++)
    {
      OriginalWallOrnamentMarker marker = markers[i];
      if (marker.X == 6
          && marker.Y == 8
          && marker.Side == WallSide.South
          && marker.Type == "WoodRing")
      {
        hasSensorWoodRing = true;
        break;
      }
    }

    if (!hasSensorWoodRing)
    {
      markers.Add(
          new OriginalWallOrnamentMarker(
              "WoodRing",
              6,
              8,
              WallSide.South,
              true));
    }

    return markers.ToArray();
  }

  private static bool TryParseWallSide(string wall, out WallSide side)
  {
    if (string.Equals(
            wall,
            "North",
            System.StringComparison.OrdinalIgnoreCase))
    {
      side = WallSide.North;
      return true;
    }

    if (string.Equals(
            wall,
            "East",
            System.StringComparison.OrdinalIgnoreCase))
    {
      side = WallSide.East;
      return true;
    }

    if (string.Equals(
            wall,
            "South",
            System.StringComparison.OrdinalIgnoreCase))
    {
      side = WallSide.South;
      return true;
    }

    if (string.Equals(
            wall,
            "West",
            System.StringComparison.OrdinalIgnoreCase))
    {
      side = WallSide.West;
      return true;
    }

    side = WallSide.North;
    return false;
  }

  private void DrawWallOrnamentIconList(
      Rect mapRect,
      OriginalWallOrnamentMarker[] markers)
  {
    if (markers == null)
      return;

    for (int i = 0; i < markers.Length; i++)
    {
      OriginalWallOrnamentMarker marker = markers[i];

      Rect cellRect = new Rect(
          mapRect.x + marker.X * CellSize,
          mapRect.y + marker.Y * CellSize,
          CellSize,
          CellSize);

      Texture2D texture = GetOrnamentMapIcon(marker.Type);
      Rect iconRect = GetWallFaceIconRect(cellRect, marker.Side, 20f);

      if (texture != null)
      {
        GUI.DrawTexture(
            iconRect,
            texture,
            ScaleMode.ScaleToFit,
            true);
      }
      else
      {
        DrawMissingOrnamentFallback(iconRect, marker.Type);
      }
    }
  }

  private static Rect GetWallFaceIconRect(
      Rect cellRect,
      WallSide side,
      float iconSize)
  {
    float x = cellRect.center.x - iconSize * 0.5f;
    float y = cellRect.center.y - iconSize * 0.5f;
    const float edgeInset = 1f;

    switch (side)
    {
      case WallSide.North:
        y = cellRect.y + edgeInset;
        break;

      case WallSide.East:
        x = cellRect.xMax - iconSize - edgeInset;
        break;

      case WallSide.South:
        y = cellRect.yMax - iconSize - edgeInset;
        break;

      case WallSide.West:
        x = cellRect.x + edgeInset;
        break;
    }

    return new Rect(x, y, iconSize, iconSize);
  }

  private void EnsureOrnamentTexturesResolved()
  {
    if (ornamentTexturesResolved)
      return;

    ornamentTexturesResolved = true;

    hookMapIcon = FindBestOrnamentTexture("Hook");
    woodRingMapIcon = FindBestOrnamentTexture("WoodRing");
    slimeMapIcon = FindBestOrnamentTexture("Slime");
    grateMapIcon = FindBestOrnamentTexture("Grate");
    puddleMapIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
        PuddleF1AssetPath);
  }

  private Texture2D GetOrnamentMapIcon(string type)
  {
    switch (type)
    {
      case "Hook":
        return hookMapIcon;

      case "WoodRing":
        return woodRingMapIcon;

      case "Slime":
        return slimeMapIcon;

      case "Grate":
        return grateMapIcon;

      default:
        return null;
    }
  }

  private static Texture2D FindBestOrnamentTexture(string type)
  {
    string[] folders = { "Assets/Art/Ornaments" };
    string[] guids = AssetDatabase.FindAssets("t:Texture2D", folders);

    Texture2D best = null;
    int bestScore = int.MinValue;

    for (int i = 0; i < guids.Length; i++)
    {
      string path = AssetDatabase.GUIDToAssetPath(guids[i]);
      string fileName =
          Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

      int score = ScoreOrnamentTexture(type, fileName);
      if (score < 0 || score <= bestScore)
        continue;

      Texture2D candidate =
          AssetDatabase.LoadAssetAtPath<Texture2D>(path);
      if (candidate == null)
        continue;

      best = candidate;
      bestScore = score;
    }

    return best;
  }

  private static int ScoreOrnamentTexture(
      string type,
      string lowerFileName)
  {
    if (string.IsNullOrEmpty(lowerFileName))
      return -1;

    string normalized =
        lowerFileName
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

    int score = 0;

    switch (type)
    {
      case "Hook":
        if (!normalized.Contains("hook"))
          return -1;
        score += 100;
        break;

      case "WoodRing":
        if (!(normalized.Contains("woodring")
              || (normalized.Contains("wood")
                  && normalized.Contains("ring"))))
        {
          return -1;
        }

        score += 100;
        break;

      case "Slime":
        if (!normalized.Contains("slime"))
          return -1;
        score += 100;
        break;

      case "Grate":
        if (!normalized.Contains("grate"))
          return -1;
        score += 100;
        if (normalized.Contains("32x28") || normalized.Contains("3228"))
          score += 25;
        break;

      default:
        return -1;
    }

    // Prefer the same frontal artwork used by the first-person renderer.
    if (normalized.Contains("front"))
      score += 50;

    // Prefer a named original-size/front file over side/distance variants.
    if (normalized.Contains("side"))
      score -= 20;
    if (normalized.Contains("f2"))
      score -= 10;
    if (normalized.Contains("f3"))
      score -= 10;

    return score;
  }

  private static void DrawMissingOrnamentFallback(
      Rect iconRect,
      string type)
  {
    EditorGUI.DrawRect(iconRect, OrnamentFallbackColor);

    GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
    {
      alignment = TextAnchor.MiddleCenter,
      clipping = TextClipping.Clip
    };
    style.normal.textColor = Color.black;

    string text =
        type == "WoodRing"
            ? "WR"
            : type == "Hook"
                ? "H"
                : type == "Grate"
                    ? "G"
                    : "S";

    GUI.Label(iconRect, text, style);
  }

  private static GUIStyle CreateChampionNameStyle()
  {
    GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
    {
      alignment = TextAnchor.MiddleCenter,
      fontSize = 12,
      fontStyle = FontStyle.Bold,
      clipping = TextClipping.Overflow,
      wordWrap = false,
      padding = new RectOffset(0, 0, 0, 0),
      margin = new RectOffset(0, 0, 0, 0)
    };

    Color textColor = new Color(0f, 1f, 0f, 1f);
    nameStyle.normal.textColor = textColor;
    nameStyle.hover.textColor = textColor;
    nameStyle.active.textColor = textColor;
    nameStyle.focused.textColor = textColor;
    nameStyle.onNormal.textColor = textColor;
    nameStyle.onHover.textColor = textColor;
    nameStyle.onActive.textColor = textColor;
    nameStyle.onFocused.textColor = textColor;
    return nameStyle;
  }

  private void DrawChampionName(
      Rect mapRect,
      Rect cellRect,
      ChampionMirrorMarker marker)
  {
    if (marker == null || string.IsNullOrEmpty(marker.Champion))
      return;

    GUIStyle nameStyle = CreateChampionNameStyle();

    int nameCellX = marker.X;
    int nameCellY = marker.Y;

    Vector2 labelCenter = cellRect.center;
    switch (marker.Side)
    {
      case WallSide.North:
        nameCellY -= 1;
        labelCenter.y -= CellSize;
        break;

      case WallSide.East:
        nameCellX += 1;
        labelCenter.x += CellSize;
        break;

      case WallSide.South:
        nameCellY += 1;
        labelCenter.y += CellSize;
        break;

      default: // West
        nameCellX -= 1;
        labelCenter.x -= CellSize;
        break;
    }

    bool labelFallsOnWalkableMap =
        map != null
        && map.IsInside(nameCellX, nameCellY)
        && map.GetTile(nameCellX, nameCellY).Type != DungeonTileType.Wall;

    Color previousContentColor = GUI.contentColor;
    GUI.contentColor = ChampionNameColor;

    bool pairTop =
        marker.Champion == "Tiggy" || marker.Champion == "Wu Tse";
    bool pairBottom =
        marker.Champion == "Wuuf" || marker.Champion == "Leif";
    if (pairTop || pairBottom)
    {
      Rect targetTileRect = new Rect(
          labelCenter.x - CellSize * 0.5f,
          labelCenter.y - CellSize * 0.5f,
          CellSize,
          CellSize);

      GUIStyle pairStyle = new GUIStyle(nameStyle)
      {
        alignment = pairTop
            ? TextAnchor.UpperCenter
            : TextAnchor.LowerCenter,
        clipping = TextClipping.Overflow
      };

      GUI.Label(targetTileRect, marker.Champion, pairStyle);
      GUI.contentColor = previousContentColor;
      return;
    }

    // Hissssa / Gothmog: column x=18, Y centered on the blue/yellow wall marker.
    if (marker.Champion == "Hissssa" || marker.Champion == "Gothmog")
    {
      Vector2 outsideSize = nameStyle.CalcSize(new GUIContent(marker.Champion));
      float outsideWidth = Mathf.Max(outsideSize.x, CellSize);
      float outsideHeight = Mathf.Max(outsideSize.y, 18f);
      const float markerLineThickness = 3f;
      float markerCenterY = marker.Side == WallSide.South
          ? cellRect.yMax - markerLineThickness * 0.5f
          : cellRect.y + markerLineThickness * 0.5f;
      Rect outsideNameRect = new Rect(
          mapRect.xMax,
          markerCenterY - outsideHeight * 0.5f,
          outsideWidth,
          outsideHeight);

      GUIStyle outsideStyle = new GUIStyle(nameStyle)
      {
        alignment = TextAnchor.MiddleLeft,
        clipping = TextClipping.Overflow
      };

      GUI.Label(outsideNameRect, marker.Champion, outsideStyle);
      GUI.contentColor = previousContentColor;
      return;
    }

    if (marker.Champion == "Daroou" || marker.Champion == "Mophus")
    {
      float stackedLineHeight = 9f;
      float stackedCharWidth = 20f;
      float stackedTotalHeight = marker.Champion.Length * stackedLineHeight;
      float stackedStartY = marker.Champion == "Mophus"
          ? cellRect.y + 1f
          : labelCenter.y - stackedTotalHeight * 0.5f;

      GUIStyle stackedStyle = new GUIStyle(nameStyle)
      {
        alignment = TextAnchor.MiddleCenter,
        clipping = TextClipping.Overflow
      };

      for (int i = 0; i < marker.Champion.Length; i++)
      {
        string glyph = marker.Champion[i].ToString();
        Vector2 glyphSize = stackedStyle.CalcSize(new GUIContent(glyph));
        float glyphWidth = Mathf.Max(stackedCharWidth, glyphSize.x);
        float glyphHeight = Mathf.Max(stackedLineHeight, glyphSize.y);

        Rect glyphRect = new Rect(
            labelCenter.x - glyphWidth * 0.5f,
            stackedStartY + i * stackedLineHeight
                + (stackedLineHeight - glyphHeight) * 0.5f,
            glyphWidth,
            glyphHeight);

        GUI.Label(glyphRect, glyph, stackedStyle);
      }

      GUI.contentColor = previousContentColor;
      return;
    }

    if (labelFallsOnWalkableMap)
    {
      float walkableLineHeight = 18f;
      float walkableCharWidth = 20f;
      float walkableTotalHeight = marker.Champion.Length * walkableLineHeight;
      float walkableStartY = labelCenter.y - walkableTotalHeight * 0.5f;

      GUIStyle walkableStyle = new GUIStyle(nameStyle)
      {
        alignment = TextAnchor.MiddleCenter,
        clipping = TextClipping.Overflow
      };

      for (int i = 0; i < marker.Champion.Length; i++)
      {
        string glyph = marker.Champion[i].ToString();
        Vector2 glyphSize = walkableStyle.CalcSize(new GUIContent(glyph));
        float glyphWidth = Mathf.Max(walkableCharWidth, glyphSize.x);
        float glyphHeight = Mathf.Max(walkableLineHeight, glyphSize.y);

        Rect glyphRect = new Rect(
            labelCenter.x - glyphWidth * 0.5f,
            walkableStartY + i * walkableLineHeight
                + (walkableLineHeight - glyphHeight) * 0.5f,
            glyphWidth,
            glyphHeight);

        GUI.Label(glyphRect, glyph, walkableStyle);
      }

      GUI.contentColor = previousContentColor;
      return;
    }

    Vector2 nameSize = nameStyle.CalcSize(new GUIContent(marker.Champion));
    float nameWidth = Mathf.Max(128f, nameSize.x + 4f);
    float nameHeight = Mathf.Max(18f, nameSize.y + 2f);
    Rect nameRect = new Rect(
        labelCenter.x - nameWidth * 0.5f,
        labelCenter.y - nameHeight * 0.5f,
        nameWidth,
        nameHeight);

    GUI.Label(nameRect, marker.Champion, nameStyle);
    GUI.contentColor = previousContentColor;
  }

  private bool TryTeleportToWallOrnament(int tileX, int tileY)
  {
    OriginalWallOrnamentMarker marker =
        FindWallOrnamentAtTile(
            resolvedWallOrnaments,
            tileX,
            tileY);

    if (marker == null)
      return false;

    int targetX = marker.X;
    int targetY = marker.Y;
    DungeonFacing targetFacing;

    if (!marker.WallTilePlacement)
    {
      // Mechanism is stored on the walkable cell itself.
      targetFacing = WallSideToFacing(marker.Side);
    }
    else if (!TryWalkableViewPoseForWallTile(
        marker,
        out targetX,
        out targetY,
        out targetFacing))
    {
      return false;
    }

    if (!map.IsInside(targetX, targetY)
        || !map.CanEnter(targetX, targetY))
    {
      return false;
    }

    selectedX = targetX;
    selectedY = targetY;
    selectedFacing = targetFacing;

    EditorPrefs.SetInt(ViewEditPreviewXKey, targetX);
    EditorPrefs.SetInt(ViewEditPreviewYKey, targetY);
    EditorPrefs.SetInt(
        ViewEditPreviewFacingKey,
        (int)targetFacing);

    return true;
  }

  private bool TryWalkableViewPoseForWallTile(
      OriginalWallOrnamentMarker marker,
      out int targetX,
      out int targetY,
      out DungeonFacing targetFacing)
  {
    targetX = marker.X;
    targetY = marker.Y;
    targetFacing = WallSideToFacing(marker.Side);

    int sideX;
    int sideY;
    WallSideOffset(marker.Side, out sideX, out sideY);

    // Physical-face convention: stand on the neighbor in Side and look back.
    int physicalX = marker.X + sideX;
    int physicalY = marker.Y + sideY;
    DungeonFacing lookBack = OppositeFacing(WallSideToFacing(marker.Side));
    if (map.IsInside(physicalX, physicalY) && map.CanEnter(physicalX, physicalY))
    {
      targetX = physicalX;
      targetY = physicalY;
      targetFacing = lookBack;
      return true;
    }

    // JSON/sensor-on-wall convention: Side is the look direction, so the
    // walkable cell is behind the camera. (12,3) North views from (12,4).
    int viewX = marker.X - sideX;
    int viewY = marker.Y - sideY;
    if (map.IsInside(viewX, viewY) && map.CanEnter(viewX, viewY))
    {
      targetX = viewX;
      targetY = viewY;
      targetFacing = WallSideToFacing(marker.Side);
      return true;
    }

    return false;
  }

  private static void WallSideOffset(
      WallSide side,
      out int dx,
      out int dy)
  {
    switch (side)
    {
      case WallSide.North:
        dx = 0;
        dy = -1;
        return;
      case WallSide.East:
        dx = 1;
        dy = 0;
        return;
      case WallSide.South:
        dx = 0;
        dy = 1;
        return;
      default:
        dx = -1;
        dy = 0;
        return;
    }
  }

  private static DungeonFacing OppositeFacing(DungeonFacing facing)
  {
    switch (facing)
    {
      case DungeonFacing.North:
        return DungeonFacing.South;
      case DungeonFacing.East:
        return DungeonFacing.West;
      case DungeonFacing.South:
        return DungeonFacing.North;
      default:
        return DungeonFacing.East;
    }
  }

  private static OriginalWallOrnamentMarker FindWallOrnamentAtTile(
      OriginalWallOrnamentMarker[] markers,
      int x,
      int y)
  {
    if (markers == null)
      return null;

    for (int i = 0; i < markers.Length; i++)
    {
      OriginalWallOrnamentMarker marker = markers[i];
      if (marker != null && marker.X == x && marker.Y == y)
        return marker;
    }

    return null;
  }

  private static DungeonFacing WallSideToFacing(WallSide side)
  {
    switch (side)
    {
      case WallSide.North:
        return DungeonFacing.North;
      case WallSide.East:
        return DungeonFacing.East;
      case WallSide.South:
        return DungeonFacing.South;
      default:
        return DungeonFacing.West;
    }
  }

  private bool TryFacingForEnteredCell(
      int x,
      int y,
      out DungeonFacing facing)
  {
    if (TryChampionMirrorFacing(x, y, out facing))
      return true;

    if (TryAdjacentOrnamentFacing(x, y, out facing))
      return true;

    if (TryAdjacentStairsFacing(x, y, out facing))
      return true;

    facing = selectedFacing;
    return false;
  }

  private bool TryChampionMirrorFacing(
      int x,
      int y,
      out DungeonFacing facing)
  {
    for (int i = 0; i < ChampionMirrorMarkers.Length; i++)
    {
      ChampionMirrorMarker marker = ChampionMirrorMarkers[i];
      if (marker != null && marker.X == x && marker.Y == y)
      {
        facing = WallSideToFacing(marker.Side);
        return true;
      }
    }

    facing = selectedFacing;
    return false;
  }

  // Search North, East, South, then West. The ornament side is the wall
  // face that looks back at the entered cell.
  private bool TryAdjacentOrnamentFacing(
      int x,
      int y,
      out DungeonFacing facing)
  {
    if (HasOrnamentFace(x, y - 1, WallSide.South))
    {
      facing = DungeonFacing.North;
      return true;
    }

    if (HasOrnamentFace(x + 1, y, WallSide.West))
    {
      facing = DungeonFacing.East;
      return true;
    }

    if (HasOrnamentFace(x, y + 1, WallSide.North))
    {
      facing = DungeonFacing.South;
      return true;
    }

    if (HasOrnamentFace(x - 1, y, WallSide.East))
    {
      facing = DungeonFacing.West;
      return true;
    }

    facing = selectedFacing;
    return false;
  }

  private bool TryAdjacentStairsFacing(
      int x,
      int y,
      out DungeonFacing facing)
  {
    if (IsUpOrDownStairs(x, y - 1))
    {
      facing = DungeonFacing.North;
      return true;
    }

    if (IsUpOrDownStairs(x + 1, y))
    {
      facing = DungeonFacing.East;
      return true;
    }

    if (IsUpOrDownStairs(x, y + 1))
    {
      facing = DungeonFacing.South;
      return true;
    }

    if (IsUpOrDownStairs(x - 1, y))
    {
      facing = DungeonFacing.West;
      return true;
    }

    facing = selectedFacing;
    return false;
  }

  private bool HasOrnamentFace(int wallX, int wallY, WallSide side)
  {
    if (resolvedWallOrnaments == null)
      return false;

    for (int i = 0; i < resolvedWallOrnaments.Length; i++)
    {
      OriginalWallOrnamentMarker marker = resolvedWallOrnaments[i];
      if (marker != null
          && marker.X == wallX
          && marker.Y == wallY
          && marker.Side == side)
      {
        return true;
      }
    }

    return false;
  }

  private bool IsUpOrDownStairs(int x, int y)
  {
    if (map == null || !map.IsInside(x, y))
      return false;

    DungeonTile tile = map.GetTile(x, y);
    return tile != null && tile.TryGetStairsDirection(out _);
  }

  private void HandleGridClick(Rect mapRect)
  {
    Event current = Event.current;
    if (current.type != EventType.MouseDown || current.button != 0)
      return;

    Vector2 mouse = current.mousePosition;
    if (!mapRect.Contains(mouse))
      return;

    int x = Mathf.FloorToInt((mouse.x - mapRect.x) / CellSize);
    int y = Mathf.FloorToInt((mouse.y - mapRect.y) / CellSize);

    if (!map.IsInside(x, y))
      return;

    if (TryTeleportToWallOrnament(x, y))
    {
      current.Use();
      GUI.changed = true;
      Repaint();
      return;
    }

    selectedX = x;
    selectedY = y;

    if (map.CanEnter(x, y)
        && TryFacingForEnteredCell(x, y, out DungeonFacing enteredFacing))
    {
      selectedFacing = enteredFacing;
    }

    EditorPrefs.SetInt(ViewEditPreviewXKey, x);
    EditorPrefs.SetInt(ViewEditPreviewYKey, y);
    EditorPrefs.SetInt(
        ViewEditPreviewFacingKey,
        (int)selectedFacing);

    current.Use();
    GUI.changed = true;
    Repaint();
  }

}
