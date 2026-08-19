using UnityEngine;

namespace DM.Rendering
{
  /// <summary>
  /// TEMP visual test: Wall D3R2 cropped to 32×49. Copies source columns
  /// 7..38 1:1 (no scale, no interpolation). Drops 7 px from the left and
  /// 5 px from the right of the 44 px source. Does not modify the source
  /// texture or D3L2.
  /// </summary>
  public static class D3R2NarrowWidthTest
  {
    public const int SourceMinX = 7;
    public const int SourceMaxX = 38;
    public const int SourceHeight = 49;
    public const int DungeonMinX = 0;
    public const int DungeonMaxX = 223;

    public static bool Enabled = true;

    public static int SourceWidth => SourceMaxX - SourceMinX + 1;

    public static bool ShouldReplace(DungeonGraphicType graphic)
    {
      return Enabled && graphic == DungeonGraphicType.WallD3R2;
    }

    /// <summary>
    /// Copy source X 7..38, Y 0..48 1:1 starting at authored
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

      if (source.height < SourceHeight)
        return;

      Color32[] sourcePixels = source.GetPixels32();
      int sourceWidth = source.width;

      for (int row = 0; row < SourceHeight; row++)
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
