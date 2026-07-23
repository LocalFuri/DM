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

    [Header("Graphics Database")]
    [SerializeField] private DungeonGraphics graphics;

    private Texture2D frameBuffer;
    private Color32[] framePixels;

    private DungeonMap currentMap;
    private bool frameDirty = true;

    private void Start()
    {
      CreateFrameBuffer();

      // Temporary first render until the map and player system
      // calls Render(...) itself.
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

      if (targetTexture == null || frameBuffer == null)
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

      if (graphics != null)
      {
        DrawCeiling();
        DrawFloor();
        DrawFrontWall();
      }

      frameBuffer.SetPixels32(framePixels);
      frameBuffer.Apply(false);
    }

    private void DrawCeiling()
    {
      Texture2D texture = graphics.Ceiling;

      if (texture == null)
      {
        return;
      }

      int ceilingX =
          (ViewWidth - texture.width) / 2;

      int ceilingY = 140;

      Blit(
          texture,
          ceilingX,
          ceilingY
      );
    }

    private void DrawFloor()
    {
      Texture2D texture = graphics.Floor;

      if (texture == null)
      {
        return;
      }

      int floorX =
          (ViewWidth - texture.width) / 2;

      int floorY = 0;

      Blit(
          texture,
          floorX,
          floorY
      );
    }

    private void DrawFrontWall()
    {
      Texture2D texture = graphics.FrontWallF0;

      if (texture == null)
      {
        return;
      }

      int wallX =
          (ViewWidth - texture.width) / 2;

      int wallY = 45;

      Blit(
          texture,
          wallX,
          wallY
      );
    }

    private void Clear(Color32 colour)
    {
      for (int i = 0; i < framePixels.Length; i++)
      {
        framePixels[i] = colour;
      }
    }

    private void Blit(
        Texture2D source,
        int destinationX,
        int destinationY)
    {
      Color32[] sourcePixels =
          source.GetPixels32();

      for (
          int sourceY = 0;
          sourceY < source.height;
          sourceY++)
      {
        int targetY =
            destinationY + sourceY;

        if (
            targetY < 0 ||
            targetY >= ViewHeight)
        {
          continue;
        }

        for (
            int sourceX = 0;
            sourceX < source.width;
            sourceX++)
        {
          int targetX =
              destinationX + sourceX;

          if (
              targetX < 0 ||
              targetX >= ViewWidth)
          {
            continue;
          }

          Color32 sourceColour =
              sourcePixels[
                  sourceY * source.width +
                  sourceX
              ];

          if (sourceColour.a == 0)
          {
            continue;
          }

          framePixels[
              targetY * ViewWidth +
              targetX
          ] = sourceColour;
        }
      }
    }
  }
}