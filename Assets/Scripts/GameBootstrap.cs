using DM.Dungeon;
using DM.Rendering;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
  [SerializeField]
  private DungeonRenderer dungeonRenderer;

  [SerializeField]
  private DungeonKeyboardInput keyboardInput;

  private void Start()
  {
    Debug.Log("GameBootstrap started.");

    DungeonMap map = DungeonMap.CreateMinimalTestDungeon();

    Debug.Log(
        $"Minimal test dungeon {map.Width}x{map.Height}. " +
        $"Player ({map.PlayerX},{map.PlayerY}) facing {map.PlayerFacing}."
    );
    Debug.Log(map.BuildDebugMap());

    if (keyboardInput == null)
      keyboardInput = GetComponent<DungeonKeyboardInput>();

    if (keyboardInput != null)
      keyboardInput.Initialize(map, dungeonRenderer);

    dungeonRenderer.Render(map);
  }
}
