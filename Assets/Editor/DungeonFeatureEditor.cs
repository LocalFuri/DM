using System.IO;
using DM.Dungeon;
using UnityEditor;
using UnityEngine;

public class DungeonFeatureEditor : EditorWindow
{
  private const string HallOfChampionsMapPath =
      "Assets/Data/Maps/HallOfChampions.json";

  private const float CellSize = 32f;
  private const float LabelLeftMargin = 28f;
  private const float LabelTopMargin = 20f;
  private const float InspectorWidth = 320f;

  private static readonly Color WallColor = new Color(0.22f, 0.22f, 0.22f);
  private static readonly Color FloorColor = new Color(0.78f, 0.78f, 0.78f);
  private static readonly Color CellBorderColor = new Color(0.08f, 0.08f, 0.08f);
  private static readonly Color SelectionFillColor =
      new Color(0.35f, 0.75f, 1f, 0.35f);
  private static readonly Color SelectionBorderColor =
      new Color(0.2f, 0.85f, 1f, 1f);

  // Champion mirror map marker: cyan wall-face tick with a small yellow center.
  private static readonly Color ChampionMirrorLineColor =
      new Color(0.0f, 0.85f, 1.0f, 1f);
  private static readonly Color ChampionMirrorDotColor =
      new Color(1.0f, 0.9f, 0.0f, 1f);

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

  [MenuItem("Tools/Dungeon Feature Editor &f")]
  public static void Open()
  {
    DungeonFeatureEditor window =
        GetWindow<DungeonFeatureEditor>("Dungeon Features");
    window.minSize = new Vector2(1200f, 800f);
  }

  private void OnEnable()
  {
    LoadMap();
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
      selectedX = 1;
      selectedY = 2;
    }
    catch (System.Exception ex)
    {
      map = null;
      mapLoadError = ex.Message;
    }
  }

  private void OnGUI()
  {
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

    EditorGUILayout.BeginHorizontal();

    EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
    DrawMapGrid();
    EditorGUILayout.EndVertical();

    EditorGUILayout.BeginVertical(
        GUILayout.Width(InspectorWidth),
        GUILayout.ExpandHeight(true));
    DrawSelectedTilePanel();
    EditorGUILayout.EndVertical();

    EditorGUILayout.EndHorizontal();
  }

  private void DrawMapGrid()
  {
    float mapPixelWidth = map.Width * CellSize;
    float mapPixelHeight = map.Height * CellSize;
    float contentWidth = LabelLeftMargin + mapPixelWidth;
    float contentHeight = LabelTopMargin + mapPixelHeight;

    mapScroll = EditorGUILayout.BeginScrollView(mapScroll);

    Rect contentRect = GUILayoutUtility.GetRect(
        contentWidth,
        contentHeight,
        GUILayout.ExpandWidth(false),
        GUILayout.ExpandHeight(false)
    );

    Rect mapRect = new Rect(
        contentRect.x + LabelLeftMargin,
        contentRect.y + LabelTopMargin,
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

    DrawChampionMirrorMarkers(mapRect);
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

  private static void DrawChampionMirrorMarkers(Rect mapRect)
  {
    const float lineLength = 12f;
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
              centerX - lineLength * 0.5f,
              cellRect.y,
              lineLength,
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
              centerY - lineLength * 0.5f,
              lineThickness,
              lineLength);
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
              centerX - lineLength * 0.5f,
              cellRect.yMax - lineThickness,
              lineLength,
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
              centerY - lineLength * 0.5f,
              lineThickness,
              lineLength);
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
    }
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

    selectedX = x;
    selectedY = y;
    current.Use();
    GUI.changed = true;
    Repaint();
  }

  private void DrawSelectedTilePanel()
  {
    EditorGUILayout.LabelField("Selected Tile", EditorStyles.boldLabel);
    EditorGUILayout.Space();

    EditorGUILayout.LabelField("Level", "0");
    EditorGUILayout.LabelField("X", selectedX.ToString());
    EditorGUILayout.LabelField("Y", selectedY.ToString());

    if (!map.IsInside(selectedX, selectedY))
    {
      EditorGUILayout.HelpBox(
          "Selected coordinate is outside the map.",
          MessageType.Warning);
      return;
    }

    DungeonTile tile = map.GetTile(selectedX, selectedY);

    EditorGUILayout.LabelField("Gameplay Type", tile.Type.ToString());
    EditorGUILayout.LabelField("Source Type", tile.SourceType.ToString());
    EditorGUILayout.LabelField("Raw", tile.Raw.ToString());
    EditorGUILayout.LabelField("Hex", tile.Raw.ToString("X2"));

    if (tile.SourceType == DungeonSourceTileType.Stairs
        && tile.TryGetStairsDirection(out bool isUp))
    {
      EditorGUILayout.LabelField(
          "Stairs Direction",
          isUp ? "Up" : "Down");
    }

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Features on this tile", EditorStyles.boldLabel);

    bool foundChampionMirror = false;
    for (int i = 0; i < ChampionMirrorMarkers.Length; i++)
    {
      ChampionMirrorMarker marker = ChampionMirrorMarkers[i];
      if (marker.X != selectedX || marker.Y != selectedY)
        continue;

      foundChampionMirror = true;
      EditorGUILayout.LabelField(
          "Champion Mirror",
          marker.Champion + " [" + marker.Side + "]");
    }

    if (!foundChampionMirror)
      EditorGUILayout.HelpBox("No authored features yet.", MessageType.Info);
  }
}
