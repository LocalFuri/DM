using DM.Dungeon;
using UnityEngine;

namespace DM.Core
{
  public class GameManager : MonoBehaviour
  {
    private DungeonMap _dungeonMap;

    private void Awake()
    {
      _dungeonMap = new DungeonMap(10, 6);

      Debug.Log($"Dungeon created: {_dungeonMap.Width} x {_dungeonMap.Height}");
      
    }
  }
}