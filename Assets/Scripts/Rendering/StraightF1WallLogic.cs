using DM.Dungeon;
using UnityEngine;

namespace DM.Rendering
{
  /// <summary>
  /// Front Wall F1 1:1 blit helpers + Floor/Ceiling environment phase.
  /// Front F1 texture is the cached 224×111 composite from ExpandedF1WallTexture.
  /// </summary>
  public static class StraightF1WallLogic
  {
    public const int CompositeWidth = 224;
    public const int CompositeWidth191 = 191;
    public const int CompositeWidth160 = 160;
    public const int CompositeDestX191 = 0;
    public const int DefaultFrontWallF1Width = 191;
    public const int CompositeHeight = 111;

    public static int NormalizeFrontWallF1Width(int width)
    {
      if (width == CompositeWidth160 || width == CompositeWidth)
        return width;

      return DefaultFrontWallF1Width;
    }

    public static int FrontWallF1DestX(int width, int authoredX)
    {
      int normalized = NormalizeFrontWallF1Width(width);
      if (normalized == CompositeWidth160)
        return authoredX;

      if (normalized == CompositeWidth191)
        return CompositeDestX191;

      return 0;
    }

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
    /// F0/F2/F3 Left/Right are separate authored images and must never be
    /// mirrored. F1 Left/Right keep authored MirrorHorizontally.
    /// </summary>
    public static bool IsAuthoredSideWallGraphic(DungeonGraphicType graphic)
    {
      switch (graphic)
      {
        case DungeonGraphicType.WallF0L:
        case DungeonGraphicType.WallF0R:
        case DungeonGraphicType.WallF2L:
        case DungeonGraphicType.WallF2R:
        case DungeonGraphicType.WallF3L:
        case DungeonGraphicType.WallF3R:
          return true;
        default:
          return false;
      }
    }

    /// <summary>
    /// Floor/Ceiling only — Front F1 uses authored MirrorHorizontally.
    /// </summary>
    public static bool IsEnvironmentPhaseGraphic(DungeonGraphicType graphic)
    {
      return graphic == DungeonGraphicType.Floor
          || graphic == DungeonGraphicType.Ceiling;
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
    /// Environment phase for Floor and Ceiling.
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

      if (graphic == DungeonGraphicType.FrontWallF1)
        return true;

      bool wantB = PreferVariantB(facing, wallTileX, wallTileY);

      if (graphic == DungeonGraphicType.FrontWallF1_A)
        return !wantB;

      if (graphic == DungeonGraphicType.FrontWallF1_B)
        return wantB;

      return false;
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

    /// <summary>
    /// Copy a Front Wall F1 texture into the buffer.
    /// 224-wide: 1:1 into columns 0..223.
    /// 191-wide: 191 source columns at CompositeDestX191.
    /// 160-wide: 160 source columns at destinationX (authored piece X).
    /// Optional horizontal mirror is authored only (layout MirrorHorizontally).
    /// </summary>
    public static void BlitCompositeToBuffer(
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

      if (source.height <= 0)
        return;

      int copyWidth = source.width;
      if (copyWidth != CompositeWidth160
          && copyWidth != CompositeWidth191
          && copyWidth != CompositeWidth)
      {
        return;
      }

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
          int writeX = WriteDestX(destX, mirrorHorizontally);
          if (writeX < 0 || writeX >= bufferWidth)
            continue;

          Color32 colour = sourcePixels[sourceRow + i];
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
