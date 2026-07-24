using DM.Dungeon;
using UnityEngine;

namespace DM.Rendering
{
  public class DungeonRenderer : MonoBehaviour
  {
    private const int DefaultViewWidth = 320;
    private const int DefaultViewHeight = 200;

    [Header("Rendering")]
    [SerializeField] private Camera dungeonCamera;
    [SerializeField] private RenderTexture targetTexture;

    [Header("Viewport Layout")]
    [SerializeField] private ViewportLayout layout;

    [Header("Graphics Database")]
    [SerializeField] private DungeonGraphics graphics;

    private Texture2D frameBuffer;
    private Color32[] framePixels;

    private int viewWidth;
    private int viewHeight;

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
      viewWidth =
          layout != null && layout.Width > 0
              ? layout.Width
              : DefaultViewWidth;

      viewHeight =
          layout != null && layout.Height > 0
              ? layout.Height
              : DefaultViewHeight;

      frameBuffer = new Texture2D(
          viewWidth,
          viewHeight,
          TextureFormat.RGBA32,
          false
      );

      frameBuffer.name = "Dungeon Frame Buffer";
      frameBuffer.filterMode = FilterMode.Point;
      frameBuffer.wrapMode = TextureWrapMode.Clamp;

      framePixels = new Color32[viewWidth * viewHeight];
    }

    private void DrawDungeonFrame()
    {
      Clear(new Color32(0, 0, 0, 255));

      if (layout == null || graphics == null)
      {
        ApplyFrameBuffer();
        return;
      }

      foreach (ViewportPiece piece in layout.Pieces)
      {
        DrawPiece(piece);
      }

      ApplyFrameBuffer();
    }

    private void DrawPiece(ViewportPiece piece)
    {
      if (piece == null)
      {
        return;
      }

      if (!piece.Enabled)
      {
        return;
      }

      Texture2D texture = graphics.GetTexture(piece.Graphic);

      if (texture == null)
      {
        return;
      }

      Blit(
          texture,
          piece.X,
          piece.Y
      );
    }

    private void ApplyFrameBuffer()
    {
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
            targetY >= viewHeight)
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
              targetX >= viewWidth)
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
              targetY * viewWidth +
              targetX
          ] = sourceColour;
        }
      }
    }
  }
}