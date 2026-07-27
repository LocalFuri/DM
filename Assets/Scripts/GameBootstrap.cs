using DM.Dungeon;
using DM.Rendering;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
  [SerializeField]
  private DungeonRenderer dungeonRenderer;

  [SerializeField]
  private DungeonKeyboardInput keyboardInput;

  [SerializeField]
  private TextAsset mapJson;

  private void Start()
  {
    Debug.Log("GameBootstrap started.");

    if (mapJson == null)
    {
      Debug.LogError(
          "GameBootstrap: Map JSON TextAsset is not assigned."
      );
      return;
    }

    DungeonMap map = DungeonMap.LoadFromJson(mapJson);

    Debug.Log($"Loaded map: {map.Name}");
    Debug.Log($"Map size: {map.Width} x {map.Height}");
    Debug.Log(
        $"Player ({map.PlayerX},{map.PlayerY}) " +
        $"facing {map.PlayerFacing}."
    );
    Debug.Log(map.BuildDebugMap());

    if (keyboardInput == null)
    {
      keyboardInput = GetComponent<DungeonKeyboardInput>();
    }

    if (keyboardInput != null)
    {
      keyboardInput.Initialize(map, dungeonRenderer);
    }
    else
    {
      Debug.LogWarning(
          "GameBootstrap: DungeonKeyboardInput was not found."
      );
    }

    if (dungeonRenderer == null)
    {
      Debug.LogError(
          "GameBootstrap: DungeonRenderer is not assigned."
      );
      return;
    }

    dungeonRenderer.Render(map);
  }
}