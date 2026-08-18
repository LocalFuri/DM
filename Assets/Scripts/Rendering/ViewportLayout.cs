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

    public bool Enabled = true;

    [Tooltip(
        "When true, the piece's drawn pixels are mirrored horizontally. "
            + "For straight F1, mirrors the full 224px composite.")]
    public bool MirrorHorizontally;

    [Tooltip("Per-pose Front Wall F1 blit width: 160, 191, or 224.")]
    public int FrontWallF1Width = 191;

    [Tooltip("Per-pose Front Wall F2 blit width: 106 or 131.")]
    public int FrontWallF2Width = 106;
  }

  public static class FrontWallF2Logic
  {
    public const int Width106 = 106;
    public const int Width131 = 131;
    public const int DefaultWidth = Width106;

    public static int Normalize(int width)
    {
      if (width == Width131)
        return width;

      return DefaultWidth;
    }

    public static bool IsFrontWallF2Graphic(DungeonGraphicType graphic)
    {
      return graphic == DungeonGraphicType.FrontWallF2;
    }
  }
}