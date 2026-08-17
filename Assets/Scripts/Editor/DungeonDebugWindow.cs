using System.Collections.Generic;
using DM.Dungeon;
using DM.Rendering;
using UnityEditor;
using UnityEngine;

public class DungeonDebugWindow : EditorWindow
{
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

    miniMapScroll = DungeonMiniMapGui.Draw(
        map,
        map.PlayerX,
        map.PlayerY,
        map.PlayerFacing,
        miniMapScroll
    );
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
