using DM.Dungeon;
using UnityEditor;
using UnityEngine;

public class DungeonDebugWindow : EditorWindow
{
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

    EditorGUILayout.Space();
    EditorGUILayout.LabelField("Player", EditorStyles.boldLabel);
    EditorGUILayout.LabelField("X", map.PlayerX.ToString());
    EditorGUILayout.LabelField("Y", map.PlayerY.ToString());
    EditorGUILayout.LabelField("Facing", map.PlayerFacing.ToString());
    EditorGUILayout.LabelField("Ahead", GetAheadTileLabel(map));
  }

  private static string GetAheadTileLabel(DungeonMap map)
  {
    GetAheadOffset(map.PlayerFacing, out int dx, out int dy);

    int aheadX = map.PlayerX + dx;
    int aheadY = map.PlayerY + dy;

    if (!map.IsInside(aheadX, aheadY))
      return "Outside Map";

    return map.GetTile(aheadX, aheadY).Type.ToString();
  }

  private static void GetAheadOffset(
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
}
