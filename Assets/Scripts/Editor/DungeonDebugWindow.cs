using System.Collections.Generic;
using DM.Dungeon;
using DM.Rendering;
using UnityEditor;
using UnityEngine;

public class DungeonDebugWindow : EditorWindow
{
  private const float CellSize = 12f;
  private const float MiniMapLabelLeftMargin = 20f;
  private const float MiniMapLabelTopMargin = 16f;

  private static readonly Color WallColor = new Color(0.22f, 0.22f, 0.22f);
  private static readonly Color FloorColor = new Color(0.78f, 0.78f, 0.78f);
  private static readonly Color OtherTileColor = new Color(0.5f, 0.5f, 0.5f);
  private static readonly Color CellBorderColor = new Color(0.08f, 0.08f, 0.08f);
  private static readonly Color PlayerColor = new Color(0.95f, 0.7f, 0.1f);
  private static readonly Color FacingMarkerColor = new Color(0.15f, 0.1f, 0.02f);

  private static readonly string[] TrackedWallPieces =
  {
    "WallF0L",
    "WallF0R",
    "WallF1L",
    "WallF1R",
    "WallF2L",
    "WallF2R",
    "WallF3L",
    "WallF3R",
    "FrontWallF1",
    "FrontWallF2",
    "FrontWallF3"
  };

  private Vector2 miniMapScroll;

  [MenuItem("Tools/Dungeon Master/Dungeon Debug")]
  public static void Open()
  {
    GetWindow<DungeonDebugWindow>("Dungeon Debug");
  }

  private void OnEnable()
  {
    EditorApplication.update += OnEditorUpdate;
  }

  private void OnDisable()
  {
    EditorApplication.update -= OnEditorUpdate;
  }

  private void OnEditorUpdate()
  {
    Repaint();
  }

  private void OnGUI()
  {
    EditorGUILayout.LabelField(
        "Dungeon Debug",
        EditorStyles.boldLabel
    );

    EditorGUILayout.Space();
    EditorGUILayout.LabelField(
        "Runtime Status",
        EditorStyles.boldLabel
    );

    bool isPlaying = EditorApplication.isPlaying;
    EditorGUILayout.LabelField(
        "Play Mode",
        isPlaying ? "Playing" : "Stopped"
    );

    if (!isPlaying)
    {
      EditorGUILayout.HelpBox(
          "Enter Play Mode to inspect the dungeon.",
          MessageType.Info
      );
      return;
    }

    DungeonKeyboardInput input =
        Object.FindAnyObjectByType<DungeonKeyboardInput>();

    if (input == null)
    {
      EditorGUILayout.HelpBox(
          "DungeonKeyboardInput not found.",
          MessageType.Warning
      );
      return;
    }

    DungeonMap map = input.Map;
    if (map == null)
    {
      EditorGUILayout.HelpBox(
          "Dungeon map is not loaded.",
          MessageType.Warning
      );
      return;
    }

    DungeonFacing facing = map.PlayerFacing;

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Player", EditorStyles.boldLabel);
    EditorGUILayout.LabelField("X", map.PlayerX.ToString());
    EditorGUILayout.LabelField("Y", map.PlayerY.ToString());
    EditorGUILayout.LabelField("Facing", facing.ToString());
    EditorGUILayout.LabelField(
        "Ahead",
        GetAdjacentTileLabel(map, facing)
    );
    EditorGUILayout.LabelField(
        "Left",
        GetAdjacentTileLabel(map, TurnLeft(facing))
    );
    EditorGUILayout.LabelField(
        "Right",
        GetAdjacentTileLabel(map, TurnRight(facing))
    );
    EditorGUILayout.LabelField(
        "Behind",
        GetAdjacentTileLabel(map, TurnAround(facing))
    );

    DrawRendererSection();
    DrawMiniMap(map);
  }

  private void DrawRendererSection()
  {
    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Renderer", EditorStyles.boldLabel);

    DungeonRenderer renderer =
        Object.FindAnyObjectByType<DungeonRenderer>();

    if (renderer == null)
    {
      EditorGUILayout.HelpBox(
          "DungeonRenderer not found.",
          MessageType.Warning
      );
      return;
    }

    IReadOnlyList<string> visible = renderer.VisibleWallPieces;

    for (int i = 0; i < TrackedWallPieces.Length; i++)
    {
      string pieceName = TrackedWallPieces[i];
      bool isOn = ContainsPiece(visible, pieceName);
      EditorGUILayout.LabelField(
          pieceName,
          isOn ? "ON" : "OFF"
      );
    }
  }

  private static bool ContainsPiece(
      IReadOnlyList<string> visible,
      string pieceName)
  {
    if (visible == null)
      return false;

    for (int i = 0; i < visible.Count; i++)
    {
      if (visible[i] == pieceName)
        return true;
    }

    return false;
  }

  private void DrawMiniMap(DungeonMap map)
  {
    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Mini-map", EditorStyles.boldLabel);

    float mapPixelWidth = map.Width * CellSize;
    float mapPixelHeight = map.Height * CellSize;
    float contentWidth = MiniMapLabelLeftMargin + mapPixelWidth;
    float contentHeight = MiniMapLabelTopMargin + mapPixelHeight;
    float scrollHeight = Mathf.Min(contentHeight + 4f, 340f);

    miniMapScroll = EditorGUILayout.BeginScrollView(
        miniMapScroll,
        GUILayout.Height(scrollHeight)
    );

    Rect contentRect = GUILayoutUtility.GetRect(
        contentWidth,
        contentHeight,
        GUILayout.ExpandWidth(false),
        GUILayout.ExpandHeight(false)
    );

    Rect mapRect = new Rect(
        contentRect.x + MiniMapLabelLeftMargin,
        contentRect.y + MiniMapLabelTopMargin,
        mapPixelWidth,
        mapPixelHeight
    );

    DrawMiniMapAxisLabels(map, mapRect);

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

        bool isPlayer =
            x == map.PlayerX &&
            y == map.PlayerY;

        DrawCell(cellRect, map.GetTile(x, y).Type, isPlayer);

        if (isPlayer)
          DrawFacingMarker(cellRect, map.PlayerFacing);
      }
    }

    EditorGUILayout.EndScrollView();
  }

  private static void DrawMiniMapAxisLabels(
      DungeonMap map,
      Rect mapRect)
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
          mapRect.y - MiniMapLabelTopMargin,
          xLabelWidth,
          MiniMapLabelTopMargin
      );

      GUI.Label(xLabelRect, x.ToString(), labelStyle);
    }

    for (int y = 0; y < map.Height; y++)
    {
      Rect yLabelRect = new Rect(
          mapRect.x - MiniMapLabelLeftMargin,
          mapRect.y + y * CellSize,
          MiniMapLabelLeftMargin - 2f,
          CellSize
      );

      GUI.Label(yLabelRect, y.ToString(), yLabelStyle);
    }
  }

  private static void DrawCell(
      Rect cellRect,
      DungeonTileType type,
      bool isPlayer)
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

  private static string GetAdjacentTileLabel(
      DungeonMap map,
      DungeonFacing direction)
  {
    GetCardinalOffset(direction, out int dx, out int dy);

    int x = map.PlayerX + dx;
    int y = map.PlayerY + dy;

    if (!map.IsInside(x, y))
      return "Outside Map";

    return map.GetTile(x, y).Type.ToString();
  }

  private static void GetCardinalOffset(
      DungeonFacing facing,
      out int dx,
      out int dy)
  {
    switch (facing)
    {
      case DungeonFacing.North:
        dx = 0;
        dy = -1;
        break;
      case DungeonFacing.East:
        dx = 1;
        dy = 0;
        break;
      case DungeonFacing.South:
        dx = 0;
        dy = 1;
        break;
      case DungeonFacing.West:
        dx = -1;
        dy = 0;
        break;
      default:
        dx = 0;
        dy = 0;
        break;
    }
  }

  private static DungeonFacing TurnLeft(DungeonFacing facing)
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

  private static DungeonFacing TurnRight(DungeonFacing facing)
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

  private static DungeonFacing TurnAround(DungeonFacing facing)
  {
    return TurnLeft(TurnLeft(facing));
  }
}
