using UnityEngine;

namespace DM.Rendering
{
  /// <summary>
  /// TEMP visual test: 31px left-edge slice of Wall F3Right, 1:1 at
  /// the authored piece X. Does not modify the source texture.
  /// Disable or delete after comparison.
  /// </summary>
  public static class F3RightNarrowStripTest
  {
    public const int SourceMinX = 0;
    public const int SourceMaxX = 30;
    public const int DungeonMinX = 0;
    public const int DungeonMaxX = 223;

    public static bool Enabled = true;

    public static int SourceWidth => SourceMaxX - SourceMinX + 1;

    public static bool ShouldReplace(DungeonGraphicType graphic)
    {
      return Enabled && graphic == DungeonGraphicType.WallF3R;
    }

    /// <summary>
    /// Copy source X 0..30 (full source height) 1:1 starting at authored
    /// <paramref name="destinationX"/>. No stretch. Clipped to dungeon
    /// columns 0..223.
    /// </summary>
    public static void BlitToBuffer(
        Texture2D source,
        Color32[] destPixels,
        int bufferWidth,
        int bufferHeight,
        int destinationX,
        int destinationY)
    {
      if (source == null || destPixels == null)
        return;

      if (!source.isReadable)
        return;

      int copyWidth = SourceWidth;
      if (source.width < SourceMinX + copyWidth)
        return;

      Color32[] sourcePixels = source.GetPixels32();
      int sourceWidth = source.width;
      int sourceHeight = source.height;

      for (int row = 0; row < sourceHeight; row++)
      {
        int targetY = destinationY + row;
        if (targetY < 0 || targetY >= bufferHeight)
          continue;

        int sourceRow = row * sourceWidth;
        int destRow = targetY * bufferWidth;

        for (int i = 0; i < copyWidth; i++)
        {
          int destX = destinationX + i;
          if (destX < DungeonMinX || destX > DungeonMaxX)
            continue;

          if (destX < 0 || destX >= bufferWidth)
            continue;

          Color32 colour = sourcePixels[sourceRow + SourceMinX + i];
          if (colour.a == 0)
            continue;

          destPixels[destRow + destX] = colour;
        }
      }
    }
  }
}
