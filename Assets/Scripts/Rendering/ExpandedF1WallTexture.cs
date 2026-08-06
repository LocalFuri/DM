using UnityEngine;

namespace DM.Rendering
{
  /// <summary>
  /// Expands D_TILETYPE_WALL_F1 (160×111) to a 224×111 Front Wall F1 texture
  /// by exact horizontal segment copies. No scale, no mirror.
  /// </summary>
  public static class ExpandedF1WallTexture
  {
    public const int SourceWidth = 160;
    public const int SourceHeight = 111;
    public const int ExpandedWidth = 224;
    public const int ExpandedHeight = 111;

    private static Texture2D cachedSource;
    private static Texture2D cachedExpanded;

    /// <summary>
    /// Returns a cached 224×111 expansion of <paramref name="source"/>.
    /// Original texture is never modified.
    /// </summary>
    public static Texture2D BuildExpandedF1Wall(Texture2D source)
    {
      if (source == null)
        return null;

      if (cachedExpanded != null && cachedSource == source)
        return cachedExpanded;

      if (!source.isReadable)
      {
        Debug.LogWarning(
            "ExpandedF1WallTexture: source is not readable; "
                + "enable Read/Write on D_TILETYPE_WALL_F1.");
        return source;
      }

      if (source.width < SourceWidth || source.height < SourceHeight)
      {
        Debug.LogWarning(
            "ExpandedF1WallTexture: expected at least 160x111, got "
                + source.width
                + "x"
                + source.height
                + ".");
        return source;
      }

      Color32[] src = source.GetPixels32();
      int srcW = source.width;
      Color32[] dst = new Color32[ExpandedWidth * ExpandedHeight];

      // Spec uses top-origin Y. GetPixels32 / SetPixels32 are bottom-up.
      for (int topY = 0; topY < ExpandedHeight; topY++)
      {
        int unityY = ExpandedHeight - 1 - topY;
        bool oddBrickRow =
            (topY >= 28 && topY <= 54) || (topY >= 83 && topY <= 110);

        if (oddBrickRow)
        {
          // Rows 2 & 4
          CopySpan(src, srcW, unityY, 0, 32, dst, unityY, 0);
          CopySpan(src, srcW, unityY, 32, 64, dst, unityY, 32);
          CopySpan(src, srcW, unityY, 32, 64, dst, unityY, 96);
          CopySpan(src, srcW, unityY, 96, 64, dst, unityY, 160);
        }
        else
        {
          // Rows 1 & 3
          CopySpan(src, srcW, unityY, 0, 64, dst, unityY, 0);
          CopySpan(src, srcW, unityY, 64, 64, dst, unityY, 64);
          CopySpan(src, srcW, unityY, 64, 64, dst, unityY, 128);
          CopySpan(src, srcW, unityY, 128, 32, dst, unityY, 192);
        }
      }

      Texture2D expanded = new Texture2D(
          ExpandedWidth,
          ExpandedHeight,
          TextureFormat.RGBA32,
          false);
      expanded.name = source.name + "_Expanded224";
      expanded.filterMode = FilterMode.Point;
      expanded.wrapMode = TextureWrapMode.Clamp;
      expanded.SetPixels32(dst);
      expanded.Apply(false, false);

      cachedSource = source;
      cachedExpanded = expanded;
      return cachedExpanded;
    }

    private static void CopySpan(
        Color32[] src,
        int srcWidth,
        int srcUnityY,
        int srcX,
        int width,
        Color32[] dst,
        int dstUnityY,
        int dstX)
    {
      int srcRow = srcUnityY * srcWidth;
      int dstRow = dstUnityY * ExpandedWidth;
      for (int i = 0; i < width; i++)
        dst[dstRow + dstX + i] = src[srcRow + srcX + i];
    }
  }
}
