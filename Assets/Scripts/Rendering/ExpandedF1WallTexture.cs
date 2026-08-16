using UnityEngine;

namespace DM.Rendering
{
  /// <summary>
  /// Builds Front Wall F1 composites from FrontWallF1 + WallF1L + WallF1R
  /// via exact horizontal pixel copies.
  /// 224×111 includes both side extensions. 191×111 is left extension +
  /// center only (drops WallF1R x 59; no WallF1L).
  /// </summary>
  public static class ExpandedF1WallTexture
  {
    public const int SourceWidth = 160;
    public const int SourceHeight = 111;
    public const int SideWidth = 60;
    public const int ExpandedWidth = 224;
    public const int ExpandedWidth191 = 191;
    public const int ExpandedHeight = 111;

    private static Texture2D cachedFront;
    private static Texture2D cachedLeft;
    private static Texture2D cachedRight;
    private static Texture2D cachedExpanded;

    private static Texture2D cachedFront191;
    private static Texture2D cachedRight191;
    private static Texture2D cachedExpanded191;

    /// <summary>
    /// Returns a cached 224×111 Front Wall F1 composite.
    /// Original textures are never modified.
    /// </summary>
    public static Texture2D BuildExpandedF1Wall(
        Texture2D front,
        Texture2D wallF1L,
        Texture2D wallF1R)
    {
      if (front == null)
        return null;

      if (cachedExpanded != null
          && cachedFront == front
          && cachedLeft == wallF1L
          && cachedRight == wallF1R)
      {
        return cachedExpanded;
      }

      if (!front.isReadable
          || wallF1L == null
          || !wallF1L.isReadable
          || wallF1R == null
          || !wallF1R.isReadable)
      {
        Debug.LogWarning(
            "ExpandedF1WallTexture: FrontWallF1 / WallF1L / WallF1R "
                + "must be readable (Read/Write enabled).");
        return front;
      }

      if (front.width < SourceWidth || front.height < SourceHeight)
      {
        Debug.LogWarning(
            "ExpandedF1WallTexture: FrontWallF1 expected at least 160x111, got "
                + front.width
                + "x"
                + front.height
                + ".");
        return front;
      }

      if (wallF1L.width < SideWidth || wallF1L.height < SourceHeight
          || wallF1R.width < SideWidth || wallF1R.height < SourceHeight)
      {
        Debug.LogWarning(
            "ExpandedF1WallTexture: WallF1L/R expected at least 60x111, got "
                + "L "
                + wallF1L.width
                + "x"
                + wallF1L.height
                + ", R "
                + wallF1R.width
                + "x"
                + wallF1R.height
                + ".");
        return front;
      }

      Color32[] frontPixels = front.GetPixels32();
      Color32[] leftPixels = wallF1L.GetPixels32();
      Color32[] rightPixels = wallF1R.GetPixels32();
      int frontW = front.width;
      int leftW = wallF1L.width;
      int rightW = wallF1R.width;
      Color32[] dst = new Color32[ExpandedWidth * ExpandedHeight];

      // dest[0..31]    = hflip(F1R[:, 28..59])
      // dest[32..191]  = hflip(Front[:, 0..159])
      // dest[192..223] = hflip(F1L[:, 0..31])
      for (int y = 0; y < ExpandedHeight; y++)
      {
        int frontRow = y * frontW;
        int leftRow = y * leftW;
        int rightRow = y * rightW;
        int dstRow = y * ExpandedWidth;

        for (int i = 0; i < 32; i++)
          dst[dstRow + i] = rightPixels[rightRow + (59 - i)];

        for (int i = 0; i < 160; i++)
          dst[dstRow + 32 + i] = frontPixels[frontRow + (159 - i)];

        for (int i = 0; i < 32; i++)
          dst[dstRow + 192 + i] = leftPixels[leftRow + (31 - i)];
      }

      Texture2D expanded = new Texture2D(
          ExpandedWidth,
          ExpandedHeight,
          TextureFormat.RGBA32,
          false);
      expanded.name = front.name + "_Expanded224";
      expanded.filterMode = FilterMode.Point;
      expanded.wrapMode = TextureWrapMode.Clamp;
      expanded.SetPixels32(dst);
      expanded.Apply(false, false);

      cachedFront = front;
      cachedLeft = wallF1L;
      cachedRight = wallF1R;
      cachedExpanded = expanded;
      return cachedExpanded;
    }

    /// <summary>
    /// Returns a cached 191×111 Front Wall F1 composite (left extension +
    /// 160px center). Original textures are never modified.
    /// </summary>
    public static Texture2D BuildExpandedF1Wall191(
        Texture2D front,
        Texture2D wallF1R)
    {
      if (front == null)
        return null;

      if (cachedExpanded191 != null
          && cachedFront191 == front
          && cachedRight191 == wallF1R)
      {
        return cachedExpanded191;
      }

      if (!front.isReadable
          || wallF1R == null
          || !wallF1R.isReadable)
      {
        Debug.LogWarning(
            "ExpandedF1WallTexture: FrontWallF1 / WallF1R "
                + "must be readable (Read/Write enabled).");
        return front;
      }

      if (front.width < SourceWidth || front.height < SourceHeight)
      {
        Debug.LogWarning(
            "ExpandedF1WallTexture: FrontWallF1 expected at least 160x111, got "
                + front.width
                + "x"
                + front.height
                + ".");
        return front;
      }

      if (wallF1R.width < SideWidth || wallF1R.height < SourceHeight)
      {
        Debug.LogWarning(
            "ExpandedF1WallTexture: WallF1R expected at least 60x111, got "
                + wallF1R.width
                + "x"
                + wallF1R.height
                + ".");
        return front;
      }

      Color32[] frontPixels = front.GetPixels32();
      Color32[] rightPixels = wallF1R.GetPixels32();
      int frontW = front.width;
      int rightW = wallF1R.width;
      Color32[] dst = new Color32[ExpandedWidth191 * ExpandedHeight];

      // dest[0..30]    = hflip(F1R[:, 28..58])  // drops WallF1R x 59
      // dest[31..190]  = hflip(Front[:, 0..159])
      for (int y = 0; y < ExpandedHeight; y++)
      {
        int frontRow = y * frontW;
        int rightRow = y * rightW;
        int dstRow = y * ExpandedWidth191;

        for (int i = 0; i < 31; i++)
          dst[dstRow + i] = rightPixels[rightRow + (58 - i)];

        for (int i = 0; i < 160; i++)
          dst[dstRow + 31 + i] = frontPixels[frontRow + (159 - i)];
      }

      Texture2D expanded = new Texture2D(
          ExpandedWidth191,
          ExpandedHeight,
          TextureFormat.RGBA32,
          false);
      expanded.name = front.name + "_Expanded191";
      expanded.filterMode = FilterMode.Point;
      expanded.wrapMode = TextureWrapMode.Clamp;
      expanded.SetPixels32(dst);
      expanded.Apply(false, false);

      cachedFront191 = front;
      cachedRight191 = wallF1R;
      cachedExpanded191 = expanded;
      return cachedExpanded191;
    }
  }
}
