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
          DungeonTile tile = map.GetTile(x, y);

          GameObject quad =
              GameObject.CreatePrimitive(PrimitiveType.Quad);

          quad.name = $"Tile_{x}_{y}";

          quad.transform.position = new Vector3(
              x * 1.2f,
              y * 1.2f,
              0f
          );

          quad.transform.SetParent(transform);

          Renderer quadRenderer =
              quad.GetComponent<Renderer>();

          quadRenderer.material.color =
              tile.Type == DungeonTileType.Wall
                  ? Color.gray
                  : Color.black;
        }
      }
    }
  }
}
