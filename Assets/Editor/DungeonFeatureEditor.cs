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

  private DungeonMap map;
  private string mapLoadError;
  private Vector2 mapScroll;
  private int selectedX = 1;
  private int selectedY = 2;

  [MenuItem("Tools/Dungeon Feature Editor")]
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
    EditorGUILayout.HelpBox("No authored features yet.", MessageType.Info);
  }
}
