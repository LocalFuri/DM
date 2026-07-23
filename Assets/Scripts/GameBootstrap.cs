using DM.Dungeon;
using DM.Rendering;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
  [SerializeField]
  private DungeonRenderer dungeonRenderer;

  private void Start()
  {
    Debug.Log("GameBootstrap started.");

    DungeonMap map = new DungeonMap(8, 8);

    dungeonRenderer.Render(map);
  }
}