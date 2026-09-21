using System.IO;
using DM.Dungeon;
using UnityEditor;
using UnityEngine;

public class DungeonFeatureEditor : EditorWindow
{
  private const string HallOfChampionsMapPath =
      "Assets/Data/Maps/HallOfChampions.json";

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

  // Hook wall ornaments: orange full-edge bar + white center dot.
  private static readonly Color HookMarkerColor =
      new Color(1f, 0.35f, 0f, 1f);
  private static readonly Color HookDotColor =
      new Color(1f, 1f, 1f, 1f);


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
  private sealed class HookMarker
  {
    public int X;
    public int Y;
    public WallSide Side;

    public HookMarker(int x, int y, WallSide side)
    {
      X = x;
      Y = y;
      Side = side;
    }
  }

  // Hall of Champions Hook wall faces.
  // Kept explicit in the map editor so they always render and can be
  // visually checked one-by-one against the original.
  private static readonly HookMarker[] HookMarkers =
  {
    new HookMarker(12, 6, WallSide.West),
    new HookMarker(13, 8, WallSide.South),
    new HookMarker(17, 11, WallSide.West),
    new HookMarker(5, 15, WallSide.West),
    new HookMarker(3, 16, WallSide.East),
  };

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
      selectedX = 1;
      selectedY = 2;
    }
    catch (System.Exception ex)
    {
      map = null;
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

    DrawChampionMirrorMarkers(mapRect);
    DrawHookMarkers(mapRect);
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

  private void DrawHookMarkers(Rect mapRect)
  {
    const float lineThickness = 3f;
    const float dotSize = 4f;

    for (int i = 0; i < HookMarkers.Length; i++)
    {
      HookMarker marker = HookMarkers[i];

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
          lineRect = new Rect(
              cellRect.x,
              cellRect.y,
              cellRect.width,
              lineThickness);
          dotRect = new Rect(
              cellRect.center.x - dotSize * 0.5f,
              cellRect.y - (dotSize - lineThickness) * 0.5f,
              dotSize,
              dotSize);
          break;
        }

        case WallSide.East:
        {
          lineRect = new Rect(
              cellRect.xMax - lineThickness,
              cellRect.y,
              lineThickness,
              cellRect.height);
          dotRect = new Rect(
              cellRect.xMax - lineThickness
                  - (dotSize - lineThickness) * 0.5f,
              cellRect.center.y - dotSize * 0.5f,
              dotSize,
              dotSize);
          break;
        }

        case WallSide.South:
        {
          lineRect = new Rect(
              cellRect.x,
              cellRect.yMax - lineThickness,
              cellRect.width,
              lineThickness);
          dotRect = new Rect(
              cellRect.center.x - dotSize * 0.5f,
              cellRect.yMax - lineThickness
                  - (dotSize - lineThickness) * 0.5f,
              dotSize,
              dotSize);
          break;
        }

        default: // West
        {
          lineRect = new Rect(
              cellRect.x,
              cellRect.y,
              lineThickness,
              cellRect.height);
          dotRect = new Rect(
              cellRect.x - (dotSize - lineThickness) * 0.5f,
              cellRect.center.y - dotSize * 0.5f,
              dotSize,
              dotSize);
          break;
        }
      }

      EditorGUI.DrawRect(lineRect, HookMarkerColor);
      EditorGUI.DrawRect(dotRect, HookDotColor);
    }
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

    // Two-way pose sync: a map click requests the same X/Y in ViewEdit.
    // Facing is intentionally left unchanged.
    EditorPrefs.SetInt(ViewEditPreviewXKey, x);
    EditorPrefs.SetInt(ViewEditPreviewYKey, y);

    current.Use();
    GUI.changed = true;
    Repaint();
  }

}
