using DM.Dungeon;
using UnityEngine;

namespace DM.Rendering
{
  public class DungeonRenderer : MonoBehaviour
  {
    public void Render(DungeonMap map)
    {
      Debug.Log("DungeonRenderer started.");

      GameObject cube =
          GameObject.CreatePrimitive(PrimitiveType.Cube);

      cube.transform.position = Vector3.zero;
    }
  }
}