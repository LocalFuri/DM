using DM.Dungeon;
using UnityEngine;

namespace DM.Rendering
{
  /// <summary>
  /// Straight F1 wrap + shared dungeon environment A/B phase (floor, ceiling, F1).
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

    public static bool IsEnvironmentPhaseGraphic(DungeonGraphicType graphic)
    {
      return graphic == DungeonGraphicType.Floor
          || graphic == DungeonGraphicType.Ceiling
          || IsStraightF1FrontGraphic(graphic);
    }

    public static bool IsFloorOrCeilingGraphic(DungeonGraphicType graphic)
    {
      return graphic == DungeonGraphicType.Floor
          || graphic == DungeonGraphicType.Ceiling;
    }

    /// <summary>
    /// Parity formula (cell at depth 1 ahead of the player):
    /// North/South → use cell X; East/West → use cell Y.
    /// Even → Phase A (normal); odd → Phase B (mirrored).
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

    /// <summary>
    /// Environment phase for Floor, Ceiling, and F1 together.
    /// Uses the depth-1 cell ahead of the player (same axis as PreferVariantB).
    /// </summary>
    public static bool IsEnvironmentPhaseB(
        DungeonFacing facing,
        int playerX,
        int playerY)
    {
      DungeonMap.GetForwardOffset(facing, out int forwardX, out int forwardY);
      return PreferVariantB(
          facing,
          playerX + forwardX,
          playerY + forwardY);
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
    /// Mirror across the 224px dungeon viewport by reversing write columns.
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

    /// <summary>
    /// Blit a floor/ceiling sprite into the buffer, optionally mirroring
    /// across the full 224px dungeon viewport (columns 0..223).
    /// </summary>
    public static void BlitViewportComponentToBuffer(
        Texture2D source,
        Color32[] destPixels,
        int bufferWidth,
        int bufferHeight,
        int destinationX,
        int destinationY,
        bool mirrorHorizontally)
    {
      if (source == null || destPixels == null)
        return;

      if (!source.isReadable)
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

        for (int column = 0; column < sourceWidth; column++)
        {
          int destX = destinationX + column;
          if (destX < 0 || destX >= CompositeWidth)
            continue;

          int writeX = WriteDestX(destX, mirrorHorizontally);
          if (writeX < 0 || writeX >= bufferWidth)
            continue;

          Color32 colour = sourcePixels[sourceRow + column];
          if (colour.a == 0)
            continue;

          destPixels[destRow + writeX] = colour;
        }
      }
    }
  }
}
