using DM.Dungeon;
using UnityEngine;

namespace DM.Rendering
{
  public class DungeonRenderer : MonoBehaviour
  {
    public void Render(DungeonMap map)
    {
      Debug.Log("DungeonRenderer started.");

      for (int y = 0; y < map.Height; y++)
      {
        for (int x = 0; x < map.Width; x++)
        {
          GameObject cube =
              GameObject.CreatePrimitive(PrimitiveType.Cube);

          cube.transform.position = new Vector3(
              x * 1.2f,
              y * 1.2f,
              0f
          );

          cube.transform.SetParent(transform);
        }
      }
    }
  }
}