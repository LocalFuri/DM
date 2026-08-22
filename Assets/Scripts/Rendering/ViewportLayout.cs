using System;
using System.Collections.Generic;
using UnityEngine;

namespace DM.Rendering
{
  [CreateAssetMenu(
      fileName = "ViewportLayout",
      menuName = "Dungeon Master/Viewport Layout")]
  public class ViewportLayout : ScriptableObject
  {
    [Header("Viewport")]
    public int Width = 320;
    public int Height = 200;

    [Header("Render Order")]
    public List<ViewportPiece> Pieces = new();
  }

  [Serializable]
  public class ViewportPiece
  {
    public string Name;

    public DungeonGraphicType Graphic;

    public int X;
    public int Y;

    /// <summary>
    /// Live per-pose blit correction. Not serialized on ViewportLayout.
    /// Effective draw position is X + PoseOffsetX, Y + PoseOffsetY.
    /// </summary>
    [System.NonSerialized]
    public int PoseOffsetX;

    /// <summary>
    /// Live per-pose blit correction. Not serialized on ViewportLayout.
    /// </summary>
    [System.NonSerialized]
    public int PoseOffsetY;

    public int EffectiveX => X + PoseOffsetX;

    public int EffectiveY => Y + PoseOffsetY;

    public bool Enabled = true;

    /// <summary>
    /// Black Door F2 exception dest (1,4 North only). 0,0 means use 77,46.
    /// Does not affect normal F1 Black Door X/Y.
    /// </summary>
    public int BlackDoorF2X;

    /// <summary>
    /// Black Door F2 exception dest (1,4 North only). 0,0 means use 77,46.
    /// </summary>
    public int BlackDoorF2Y;

    public const int DefaultBlackDoorF2X = 77;
    public const int DefaultBlackDoorF2Y = 46;

    public int ResolvedBlackDoorF2X =>
        BlackDoorF2X == 0 && BlackDoorF2Y == 0
            ? DefaultBlackDoorF2X
            : BlackDoorF2X;

    public int ResolvedBlackDoorF2Y =>
        BlackDoorF2X == 0 && BlackDoorF2Y == 0
            ? DefaultBlackDoorF2Y
            : BlackDoorF2Y;

    [Tooltip(
        "When true, the piece's drawn pixels are mirrored horizontally. "
            + "For straight F1, mirrors the full 224px composite.")]
    public bool MirrorHorizontally;

    [Tooltip("Per-pose Front Wall F1 blit width: 160, 191, or 224.")]
    public int FrontWallF1Width = 191;

    [Tooltip("Per-pose Front Wall F2 blit width: 106, 131, or 160.")]
    public int FrontWallF2Width = 106;
  }

  public static class FrontWallF2Logic
  {
    public const int Width106 = 106;
    public const int Width131 = 131;
    public const int Width160 = 160;
    public const int DefaultWidth = Width106;

    public static int Normalize(int width)
    {
      if (width == Width131 || width == Width160)
        return width;

      return DefaultWidth;
    }

    public static bool IsFrontWallF2Graphic(DungeonGraphicType graphic)
    {
      return graphic == DungeonGraphicType.FrontWallF2;
    }

    /// <summary>
    /// Copy a Front Wall F2 texture 1:1. 106 / 131 / 160 are the only legal
    /// widths. Transparent source pixels are written opaque so a 160-wide
    /// composite cannot collapse to a 131-wide visible hole.
    /// Clips to the destination buffer (320×200), not to 131 or 224.
    /// </summary>
    public static void BlitToBuffer(
        Texture2D source,
        Color32[] destPixels,
        int bufferWidth,
        int bufferHeight,
        int destinationX,
        int destinationY,
        bool mirrorHorizontally)
    {
      BlitColumnsToBuffer(
          source,
          destPixels,
          bufferWidth,
          bufferHeight,
          destinationX,
          destinationY,
          mirrorHorizontally,
          destColumnStart: 0,
          destColumnCount: -1);
    }

    /// <summary>
    /// Re-write dest columns 131..159 of a 160-wide FrontF2 blit.
    /// Used after nearer pieces (RightF0 at X=191) so the extra 29 px stay
    /// visible. 106 / 131 do not call this and are unchanged.
    /// </summary>
    public static void Blit160ExtraStripToBuffer(
        Texture2D source,
        Color32[] destPixels,
        int bufferWidth,
        int bufferHeight,
        int destinationX,
        int destinationY,
        bool mirrorHorizontally)
    {
      if (source == null || source.width != Width160)
        return;

      BlitColumnsToBuffer(
          source,
          destPixels,
          bufferWidth,
          bufferHeight,
          destinationX,
          destinationY,
          mirrorHorizontally,
          destColumnStart: Width131,
          destColumnCount: Width160 - Width131);
    }

    private static void BlitColumnsToBuffer(
        Texture2D source,
        Color32[] destPixels,
        int bufferWidth,
        int bufferHeight,
        int destinationX,
        int destinationY,
        bool mirrorHorizontally,
        int destColumnStart,
        int destColumnCount)
    {
      if (source == null || destPixels == null)
        return;

      if (!source.isReadable)
        return;

      int copyWidth = source.width;
      if (copyWidth != Width106
          && copyWidth != Width131
          && copyWidth != Width160)
      {
        return;
      }

      int columnCount = destColumnCount < 0 ? copyWidth : destColumnCount;
      int columnStart = destColumnStart;
      if (columnStart < 0)
        columnStart = 0;
      if (columnStart + columnCount > copyWidth)
        columnCount = copyWidth - columnStart;
      if (columnCount <= 0)
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

        for (int i = 0; i < columnCount; i++)
        {
          int column = columnStart + i;
          int sourceX = mirrorHorizontally
              ? sourceWidth - 1 - column
              : column;
          int targetX = destinationX + column;
          if (targetX < 0 || targetX >= bufferWidth)
            continue;

          Color32 colour = sourcePixels[sourceRow + sourceX];
          colour.a = 255;
          destPixels[destRow + targetX] = colour;
        }
      }
    }
  }
}