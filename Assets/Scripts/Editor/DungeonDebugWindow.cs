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
  }
}
