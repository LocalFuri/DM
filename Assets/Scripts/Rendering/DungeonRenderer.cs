using DM.Dungeon;
using UnityEngine;

namespace DM.Rendering
{
  public class DungeonRenderer : MonoBehaviour
  {
    private const float TileSize = 1f;

    public void Render(DungeonMap map)
    {
      Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

      if (shader == null)
      {
        Debug.LogError("URP Unlit shader was not found.");
        return;
      }

      Material wallMaterial = new Material(shader)
      {
        color = Color.gray
      };

      Material floorMaterial = new Material(shader)
      {
        color = new Color(0.15f, 0.15f, 0.15f)
      };

      for (int y = 0; y < map.Height; y++)
      {
        for (int x = 0; x < map.Width; x++)
        {
          DungeonTile tile = map.GetTile(x, y);

          GameObject tileObject =
              GameObject.CreatePrimitive(PrimitiveType.Quad);

          tileObject.name = $"Tile_{x}_{y}";
          tileObject.transform.SetParent(transform);

          tileObject.transform.position = new Vector3(
              x * TileSize,
              y * TileSize,
              0f
          );

          MeshRenderer tileRenderer =
              tileObject.GetComponent<MeshRenderer>();

          tileRenderer.sharedMaterial =
              tile.Type == DungeonTileType.Wall
                  ? wallMaterial
                  : floorMaterial;

          Collider tileCollider =
              tileObject.GetComponent<Collider>();

          if (tileCollider != null)
          {
            Destroy(tileCollider);
          }
        }
      }
    }
  }
}