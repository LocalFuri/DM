using DM.Dungeon;
using UnityEngine;

namespace DM.Rendering
{
  /// <summary>
  /// Straight F1 front wall: 160px wrap → 224px composite, A/B parity, optional mirror.
  /// Shared by runtime DungeonRenderer and Viewport Layout Editor.
  /// </summary>
  public static class StraightF1WallLogic
  {
    public const int CompositeWidth = 224;
    public const int SourceTileWidth = 160;

    public static bool IsStraightF1FrontGraphic(DungeonGraphicType graphic)
    {
      return graphic == DungeonGraphicType.FrontWallF1
          || graphic == DungeonGraphicType.FrontWallF1_A
          || graphic == DungeonGraphicType.FrontWallF1_B;
    }

    public static bool IsF1WallGroupGraphic(DungeonGraphicType graphic)
    {
      return graphic == DungeonGraphicType.WallF1L
          || graphic == DungeonGraphicType.WallF1R
          || IsStraightF1FrontGraphic(graphic);
    }

    /// <summary>
    /// Parity formula (wall cell at depth 1 ahead of the player):
    /// North/South → use wall tile X; East/West → use wall tile Y.
    /// Even → variant A; odd → variant B.
    /// </summary>
    public static bool PreferVariantB(
        DungeonFacing facing,
        int wallTileX,
        int wallTileY)
    {
      bool northSouth =
          facing == DungeonFacing.North
          || facing == DungeonFacing.South;

      int parityAxis = northSouth ? wallTileX : wallTileY;
      return (parityAxis & 1) == 1;
    }

    public static bool IsVariantVisibleForWall(
        DungeonGraphicType graphic,
        DungeonFacing facing,
        int wallTileX,
        int wallTileY,
        bool centerWallPresent)
    {
      if (!centerWallPresent)
        return false;

      // Legacy single FrontWallF1: any straight centre wall.
      if (graphic == DungeonGraphicType.FrontWallF1)
        return true;

      bool wantB = PreferVariantB(facing, wallTileX, wallTileY);

      if (graphic == DungeonGraphicType.FrontWallF1_A)
        return !wantB;

      if (graphic == DungeonGraphicType.FrontWallF1_B)
        return wantB;

      return false;
    }

    // Join-safe wrap (fills dest 0..223, no adjacent src 159|0):
    //   dest 0..30   → src 128..158
    //   dest 31..190 → src 0..159
    //   dest 191..223 → src 1..33
    public static int SourceX(int destX)
    {
      if (destX < 31)
        return 128 + destX;

      if (destX <= 190)
        return destX - 31;

      return destX - 190;
    }

    /// <summary>
    /// Mirror the full 224px composite by reversing write columns
    /// (not by flipping only the 160px source tile).
    /// </summary>
    public static int WriteDestX(int destX, bool mirrorHorizontally)
    {
      if (!mirrorHorizontally)
        return destX;

      return CompositeWidth - 1 - destX;
    }

    public static void BlitWrapToBuffer(
        Texture2D source,
        Color32[] destPixels,
        int bufferWidth,
        int bufferHeight,
        int destinationY,
        bool mirrorHorizontally)
    {
      if (source == null || destPixels == null)
        return;

      if (!source.isReadable)
        return;

      if (source.width < SourceTileWidth || source.height <= 0)
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

        for (int destX = 0; destX < CompositeWidth; destX++)
        {
          int srcX = SourceX(destX);
          int writeX = WriteDestX(destX, mirrorHorizontally);
          if (writeX < 0 || writeX >= bufferWidth)
            continue;

          Color32 colour = sourcePixels[sourceRow + srcX];
          colour.a = 255;
          destPixels[destRow + writeX] = colour;
        }
      }
    }
  }
}
