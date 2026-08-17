using DM.Dungeon;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared Edit Mode mini-map drawing for dungeon editor tools.
/// </summary>
public static class DungeonMiniMapGui
{
  public const float CellSize = 12f;
  public const float LabelLeftMargin = 20f;
  public const float LabelTopMargin = 16f;

  private static readonly Color WallColor = new Color(0.22f, 0.22f, 0.22f);
  private static readonly Color FloorColor = new Color(0.78f, 0.78f, 0.78f);
  private static readonly Color OtherTileColor = new Color(0.5f, 0.5f, 0.5f);
  private static readonly Color CellBorderColor = new Color(0.08f, 0.08f, 0.08f);
  private static readonly Color PlayerColor = new Color(0.95f, 0.7f, 0.1f);
  private static readonly Color FacingMarkerColor = new Color(0.15f, 0.1f, 0.02f);
  private static readonly Color HoverHighlightColor =
      new Color(0.35f, 0.75f, 1f, 0.45f);
  private static readonly Color HoverBorderColor =
      new Color(0.2f, 0.85f, 1f, 1f);
  private static readonly Color CellMarkerColor = new Color(0.08f, 0.08f, 0.08f);

  private static GUIStyle cellMarkerStyle;

  public struct InteractionResult
  {
    public bool HasHover;
    public int HoverX;
    public int HoverY;
    public bool ClickedOpenTile;
    public int ClickX;
    public int ClickY;
  }

  public static Vector2 Draw(
      DungeonMap map,
      int playerX,
      int playerY,
      DungeonFacing facing,
      Vector2 scroll,
      float maxScrollHeight = 340f)
  {
    return Draw(
        map,
        playerX,
        playerY,
        facing,
        scroll,
        interactive: false,
        out _,
        maxScrollHeight
    );
  }

  public static Vector2 Draw(
      DungeonMap map,
      int playerX,
      int playerY,
      DungeonFacing facing,
      Vector2 scroll,
      bool interactive,
      out InteractionResult interaction,
      float maxScrollHeight = 340f)
  {
    interaction = default;

    if (map == null)
    {
      EditorGUILayout.HelpBox("No map loaded.", MessageType.Warning);
      return scroll;
    }

    float mapPixelWidth = map.Width * CellSize;
    float mapPixelHeight = map.Height * CellSize;
    float contentWidth = LabelLeftMargin + mapPixelWidth;
    float contentHeight = LabelTopMargin + mapPixelHeight;
    float scrollHeight = Mathf.Min(contentHeight + 4f, maxScrollHeight);

    scroll = EditorGUILayout.BeginScrollView(
        scroll,
        GUILayout.Height(scrollHeight)
    );

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

    DrawAxisLabels(map, mapRect);

    int hoverX = -1;
    int hoverY = -1;
    bool hasHover = false;

    if (interactive
        && TryGetCellUnderMouse(map, mapRect, out hoverX, out hoverY))
    {
      hasHover = true;
      interaction.HasHover = true;
      interaction.HoverX = hoverX;
      interaction.HoverY = hoverY;
    }

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

        bool isPlayer = x == playerX && y == playerY;
        bool isHovered = hasHover && x == hoverX && y == hoverY;

        DrawCell(
            cellRect,
            map.GetTile(x, y).Type,
            isPlayer,
            isHovered);

        TryDrawCellMarker(cellRect, map, x, y);

        if (isPlayer)
          DrawFacingMarker(cellRect, facing);
      }
    }

    if (interactive)
      HandleClick(map, mapRect, ref interaction);

    EditorGUILayout.EndScrollView();
    return scroll;
  }

  private static bool TryGetCellUnderMouse(
      DungeonMap map,
      Rect mapRect,
      out int cellX,
      out int cellY)
  {
    cellX = -1;
    cellY = -1;

    Vector2 mouse = Event.current.mousePosition;
    if (!mapRect.Contains(mouse))
      return false;

    int x = Mathf.FloorToInt((mouse.x - mapRect.x) / CellSize);
    int y = Mathf.FloorToInt((mouse.y - mapRect.y) / CellSize);

    if (!map.IsInside(x, y))
      return false;

    cellX = x;
    cellY = y;
    return true;
  }

  private static void HandleClick(
      DungeonMap map,
      Rect mapRect,
      ref InteractionResult interaction)
  {
    Event current = Event.current;
    if (current.type != EventType.MouseDown || current.button != 0)
      return;

    if (!TryGetCellUnderMouse(map, mapRect, out int x, out int y))
      return;

    if (!map.CanEnter(x, y))
      return;

    interaction.ClickedOpenTile = true;
    interaction.ClickX = x;
    interaction.ClickY = y;
    current.Use();
    GUI.changed = true;
  }

  private static void DrawAxisLabels(DungeonMap map, Rect mapRect)
  {
    GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
    {
      alignment = TextAnchor.MiddleCenter,
      fontSize = 9,
      clipping = TextClipping.Overflow
    };

    GUIStyle yLabelStyle = new GUIStyle(labelStyle)
    {
      alignment = TextAnchor.MiddleRight
    };

    const float xLabelWidth = 16f;

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

  private static void TryDrawCellMarker(
      Rect cellRect,
      DungeonMap map,
      int x,
      int y)
  {
    if (!TryGetCellMarker(map, x, y, out string marker))
      return;

    GUI.Label(cellRect, marker, GetCellMarkerStyle());
  }

  private static bool TryGetCellMarker(
      DungeonMap map,
      int x,
      int y,
      out string marker)
  {
    if (x == map.StartX && y == map.StartY)
    {
      marker = "E";
      return true;
    }

    DungeonTile tile = map.GetTile(x, y);
    if (tile.SourceType == DungeonSourceTileType.Stairs
        && tile.TryGetStairsDirection(out bool isUp))
    {
      marker = isUp ? "U" : "D";
      return true;
    }

    marker = null;
    return false;
  }

  private static GUIStyle GetCellMarkerStyle()
  {
    if (cellMarkerStyle != null)
      return cellMarkerStyle;

    cellMarkerStyle = new GUIStyle(EditorStyles.miniLabel)
    {
      alignment = TextAnchor.MiddleCenter,
      fontSize = 8,
      fontStyle = FontStyle.Bold,
      clipping = TextClipping.Overflow
    };
    cellMarkerStyle.normal.textColor = CellMarkerColor;

    return cellMarkerStyle;
  }

  private static void DrawCell(
      Rect cellRect,
      DungeonTileType type,
      bool isPlayer,
      bool isHovered)
  {
    EditorGUI.DrawRect(cellRect, CellBorderColor);

    Rect fillRect = new Rect(
        cellRect.x + 1f,
        cellRect.y + 1f,
        cellRect.width - 2f,
        cellRect.height - 2f
    );

    Color fillColor;
    if (isPlayer)
      fillColor = PlayerColor;
    else if (type == DungeonTileType.Wall)
      fillColor = WallColor;
    else if (type == DungeonTileType.Floor)
      fillColor = FloorColor;
    else
      fillColor = OtherTileColor;

    EditorGUI.DrawRect(fillRect, fillColor);

    if (!isHovered)
      return;

    EditorGUI.DrawRect(fillRect, HoverHighlightColor);
    EditorGUI.DrawRect(
        new Rect(cellRect.x, cellRect.y, cellRect.width, 1f),
        HoverBorderColor);
    EditorGUI.DrawRect(
        new Rect(
            cellRect.x,
            cellRect.yMax - 1f,
            cellRect.width,
            1f),
        HoverBorderColor);
    EditorGUI.DrawRect(
        new Rect(cellRect.x, cellRect.y, 1f, cellRect.height),
        HoverBorderColor);
    EditorGUI.DrawRect(
        new Rect(
            cellRect.xMax - 1f,
            cellRect.y,
            1f,
            cellRect.height),
        HoverBorderColor);
  }

  private static void DrawFacingMarker(
      Rect cellRect,
      DungeonFacing facing)
  {
    Vector2 center = cellRect.center;
    float tip = CellSize * 0.38f;
    float baseHalf = CellSize * 0.22f;

    Vector2 tipPoint;
    Vector2 leftPoint;
    Vector2 rightPoint;

    switch (facing)
    {
      case DungeonFacing.North:
        tipPoint = center + new Vector2(0f, -tip);
        leftPoint = center + new Vector2(-baseHalf, tip * 0.35f);
        rightPoint = center + new Vector2(baseHalf, tip * 0.35f);
        break;
      case DungeonFacing.East:
        tipPoint = center + new Vector2(tip, 0f);
        leftPoint = center + new Vector2(-tip * 0.35f, -baseHalf);
        rightPoint = center + new Vector2(-tip * 0.35f, baseHalf);
        break;
      case DungeonFacing.South:
        tipPoint = center + new Vector2(0f, tip);
        leftPoint = center + new Vector2(baseHalf, -tip * 0.35f);
        rightPoint = center + new Vector2(-baseHalf, -tip * 0.35f);
        break;
      case DungeonFacing.West:
        tipPoint = center + new Vector2(-tip, 0f);
        leftPoint = center + new Vector2(tip * 0.35f, baseHalf);
        rightPoint = center + new Vector2(tip * 0.35f, -baseHalf);
        break;
      default:
        return;
    }

    Handles.BeginGUI();
    Handles.color = FacingMarkerColor;
    Handles.DrawAAConvexPolygon(
        tipPoint,
        leftPoint,
        rightPoint
    );
    Handles.EndGUI();
  }
}
