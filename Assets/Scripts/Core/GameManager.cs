using DM.Dungeon;
using DM.Rendering;
using UnityEngine;

namespace DM.Core
{
  public class GameManager : MonoBehaviour
  {
    private DungeonMap _dungeonMap;
    private DungeonRenderer _dungeonRenderer;

    private void Awake()
    {
      _dungeonMap = new DungeonMap(10, 6);

      _dungeonRenderer = gameObject.AddComponent<DungeonRenderer>();

      _dungeonRenderer.Render(_dungeonMap);

      Debug.Log($"Dungeon created: {_dungeonMap.Width} x {_dungeonMap.Height}");
      Debug.Log(_dungeonMap.BuildDebugMap());
    }
  }
}