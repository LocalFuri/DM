using DM.Dungeon;
using UnityEngine;

namespace DM.Rendering
{
  public class DungeonRenderer : MonoBehaviour
  {
    private const int ViewWidth = 320;
    private const int ViewHeight = 200;

    [Header("Rendering")]
    [SerializeField] private Camera dungeonCamera;
    [SerializeField] private RenderTexture targetTexture;

    [Header("Dungeon Master Graphics")]
    [SerializeField] private Texture2D ceilingTexture;
    [SerializeField] private Texture2D floorTexture;
    [SerializeField] private Texture2D frontWallF0;

    private Texture2D frameBuffer;
    private Color32[] framePixels;

    private DungeonMap currentMap;
    private bool frameDirty = true;

    private void Start()
    {
      CreateFrameBuffer();

      Render(null);
    }

    private void OnEnable()
    {
      Camera.onPostRender += HandleCameraPostRender;
    }

    private void OnDisable()
    {
      Camera.onPostRender -= HandleCameraPostRender;
    }

    private void OnDestroy()
    {
      if (frameBuffer != null)
      {
        Destroy(frameBuffer);
      }
    }

    /// <summary>
    /// Stores the dungeon map and requests a new rendered frame.
    /// </summary>
    public void Render(DungeonMap map)
    {
      currentMap = map;
      frameDirty = true;
    }

    private void HandleCameraPostRender(Camera renderedCamera)
    {
      if (renderedCamera != dungeonCamera)
      {
        return;
      }

      if (targetTexture == null)
      {
        return;
      }

      if (frameDirty)
      {
        DrawDungeonFrame();
        frameDirty = false;
      }

      Graphics.Blit(frameBuffer, targetTexture);
    }

    private void CreateFrameBuffer()
    {
      frameBuffer = new Texture2D(
          ViewWidth,
          ViewHeight,
          TextureFormat.RGBA32,
          false
      );

      frameBuffer.name = "Dungeon Frame Buffer";
      frameBuffer.filterMode = FilterMode.Point;
      frameBuffer.wrapMode = TextureWrapMode.Clamp;

      framePixels = new Color32[ViewWidth * ViewHeight];
    }

    private void DrawDungeonFrame()
    {
      Clear(new Color32(0, 0, 0, 255));

      // Top half of the 320×200 viewport.
      if (ceilingTexture != null)
      {
        BlitScaled(
            ceilingTexture,
            0,
            ViewHeight / 2,
            ViewWidth,
            ViewHeight / 2
        );
      }

      // Bottom half of the 320×200 viewport.
      if (floorTexture != null)
      {
        BlitScaled(
            floorTexture,
            0,
            0,
            ViewWidth,
            ViewHeight / 2
        );
      }

      // First test wall, centred in the viewport.
      if (frontWallF0 != null)
      {
        int wallX = (ViewWidth - frontWallF0.width) / 2;
        int wallY = (ViewHeight - frontWallF0.height) / 2;

        Blit(frontWallF0, wallX, wallY);
      }

      frameBuffer.SetPixels32(framePixels);
      frameBuffer.Apply(false);
    }

    private void Clear(Color32 colour)
    {
      for (int i = 0; i < framePixels.Length; i++)
      {
        framePixels[i] = colour;
      }
    }

    private void Blit(Texture2D source, int destinationX, int destinationY)
    {
      Color32[] sourcePixels = source.GetPixels32();

      for (int sourceY = 0; sourceY < source.height; sourceY++)
      {
        int targetY = destinationY + sourceY;

        if (targetY < 0 || targetY >= ViewHeight)
        {
          continue;
        }

        for (int sourceX = 0; sourceX < source.width; sourceX++)
        {
          int targetX = destinationX + sourceX;

          if (targetX < 0 || targetX >= ViewWidth)
          {
            continue;
          }

          Color32 sourceColour =
              sourcePixels[sourceY * source.width + sourceX];

          if (sourceColour.a == 0)
          {
            continue;
          }

          framePixels[targetY * ViewWidth + targetX] =
              sourceColour;
        }
      }
    }

    private void BlitScaled(
        Texture2D source,
        int destinationX,
        int destinationY,
        int destinationWidth,
        int destinationHeight)
    {
      Color32[] sourcePixels = source.GetPixels32();

      for (int targetY = 0; targetY < destinationHeight; targetY++)
      {
        int screenY = destinationY + targetY;

        if (screenY < 0 || screenY >= ViewHeight)
        {
          continue;
        }

        int sourceY =
            targetY * source.height / destinationHeight;

        for (int targetX = 0; targetX < destinationWidth; targetX++)
        {
          int screenX = destinationX + targetX;

          if (screenX < 0 || screenX >= ViewWidth)
          {
            continue;
          }

          int sourceX =
              targetX * source.width / destinationWidth;

          framePixels[screenY * ViewWidth + screenX] =
              sourcePixels[sourceY * source.width + sourceX];
        }
      }
    }
  }
}