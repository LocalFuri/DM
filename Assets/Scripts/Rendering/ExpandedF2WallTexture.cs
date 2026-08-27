using UnityEngine;

namespace DM.Rendering
{
  /// <summary>
  /// Builds the Front Wall F2 132×74 composite from FrontWallF2 + WallF2R
  /// via exact horizontal pixel copies (no mirroring, no scaling).
  /// 106×74 is the raw FrontWallF2 texture.
  /// </summary>
  public static class ExpandedF2WallTexture
  {
    public const int SourceWidth = 106;
    public const int SourceHeight = 74;
    public const int ExpandedWidth = 132;
    public const int ExpandedHeight = 74;

    /// <summary>dest[0..104] = FrontWallF2[:, 1..105]</summary>
    public const int FrontStripWidth = 105;
    public const int FrontSourceStartX = 1;

    /// <summary>dest[105..131] = WallF2R[:, 19..45]</summary>
    public const int RightStripWidth = 27;
    public const int RightDestStartX = 105;
    public const int RightSourceStartX = 19;

    private const int MinFrontWidth = FrontSourceStartX + FrontStripWidth;
    private const int MinRightWidth = RightSourceStartX + RightStripWidth;

    private static Texture2D cachedFront;
    private static Texture2D cachedRight;
    private static Texture2D cachedExpanded;

    /// <summary>
    /// Returns a cached 132×74 Front Wall F2 composite.
    /// Original textures are never modified. No horizontal mirroring.
    ///
    /// Test mapping for 132×74:
    ///   dest[0..104]   = FrontWallF2[:, 1..105]
    ///   dest[105..131] = WallF2R[:, 19..45]
    /// </summary>
    public static Texture2D BuildExpandedF2Wall(
        Texture2D front,
        Texture2D wallF2R)
    {
      if (front == null)
        return null;

      if (cachedExpanded != null
          && cachedFront == front
          && cachedRight == wallF2R)
      {
        return cachedExpanded;
      }

      if (!front.isReadable
          || wallF2R == null
          || !wallF2R.isReadable)
      {
        Debug.LogWarning(
            "ExpandedF2WallTexture: FrontWallF2 / WallF2R "
                + "must be readable (Read/Write enabled).");
        return front;
      }

      if (front.width < MinFrontWidth || front.height < SourceHeight)
      {
        Debug.LogWarning(
            "ExpandedF2WallTexture: FrontWallF2 expected at least 106x74, got "
                + front.width
                + "x"
                + front.height
                + ".");
        return front;
      }

      if (wallF2R.width < MinRightWidth || wallF2R.height < SourceHeight)
      {
        Debug.LogWarning(
            "ExpandedF2WallTexture: WallF2R expected at least "
                + MinRightWidth
                + "x"
                + SourceHeight
                + ", got "
                + wallF2R.width
                + "x"
                + wallF2R.height
                + ".");
        return front;
      }

      Color32[] frontPixels = front.GetPixels32();
      Color32[] rightPixels = wallF2R.GetPixels32();
      int frontW = front.width;
      int rightW = wallF2R.width;
      Color32[] dst = new Color32[ExpandedWidth * ExpandedHeight];

      // dest[0..104]   = FrontWallF2[:, 1..105]
      // dest[105..131] = WallF2R[:, 19..45]
      for (int y = 0; y < ExpandedHeight; y++)
      {
        int frontRow = y * frontW;
        int rightRow = y * rightW;
        int dstRow = y * ExpandedWidth;

        for (int i = 0; i < FrontStripWidth; i++)
        {
          dst[dstRow + i] =
              frontPixels[frontRow + FrontSourceStartX + i];
        }

        for (int i = 0; i < RightStripWidth; i++)
        {
          dst[dstRow + RightDestStartX + i] =
              rightPixels[rightRow + RightSourceStartX + i];
        }
      }

      Texture2D expanded = new Texture2D(
          ExpandedWidth,
          ExpandedHeight,
          TextureFormat.RGBA32,
          false);
      expanded.name = front.name + "_Expanded132";
      expanded.filterMode = FilterMode.Point;
      expanded.wrapMode = TextureWrapMode.Clamp;
      expanded.SetPixels32(dst);
      expanded.Apply(false, false);

      cachedFront = front;
      cachedRight = wallF2R;
      cachedExpanded = expanded;
      return cachedExpanded;
    }

    public const int ExpandedWidth160 = 162;
    /// <summary>dest[0..105] = FrontWallF2[:, 0..105]</summary>
    public const int CenterStripWidth160 = 106;
    public const int CenterDestStartX160 = 0;
    public const int CenterSourceStartX160 = 0;
    /// <summary>dest[106..127] = FrontWallF2[:, 1..22]</summary>
    public const int RightAStripWidth160 = 22;
    public const int RightADestStartX160 = 106;
    public const int RightASourceStartX160 = 1;
    /// <summary>dest[128..161] = FrontWallF2[:, 24..57]</summary>
    public const int RightBStripWidth160 = 34;
    public const int RightBDestStartX160 = 128;
    public const int RightBSourceStartX160 = 24;
    private const int MinFrontWidth160 = 106;

    private static Texture2D cachedFront160;
    private static Texture2D cachedExpanded160;

    /// <summary>
    /// Returns a cached 162×74 Front Wall F2 composite from FrontWallF2 only.
    /// Original textures are never modified. No horizontal mirroring.
    /// WallF2R is not used.
    ///
    ///   dest[0..105]   = FrontWallF2[:, 0..105]
    ///   dest[106..127] = FrontWallF2[:, 1..22]
    ///   dest[128..161] = FrontWallF2[:, 24..57]
    /// </summary>
    public static Texture2D BuildExpandedF2Wall160(Texture2D front)
    {
      if (front == null)
        return null;

      if (cachedExpanded160 != null
          && cachedExpanded160.width != ExpandedWidth160)
      {
        cachedExpanded160 = null;
      }

      if (cachedExpanded160 != null && cachedFront160 == front)
        return cachedExpanded160;

      if (!front.isReadable)
      {
        Debug.LogWarning(
            "ExpandedF2WallTexture: FrontWallF2 "
                + "must be readable (Read/Write enabled).");
        return front;
      }

      if (front.width < MinFrontWidth160 || front.height < SourceHeight)
      {
        Debug.LogWarning(
            "ExpandedF2WallTexture: FrontWallF2 expected at least 106x74, got "
                + front.width
                + "x"
                + front.height
                + ".");
        return front;
      }

      Color32[] frontPixels = front.GetPixels32();
      int frontW = front.width;
      Color32[] dst = new Color32[ExpandedWidth160 * ExpandedHeight];

      // dest[0..105]   = FrontWallF2[:, 0..105]
      // dest[106..127] = FrontWallF2[:, 1..22]
      // dest[128..161] = FrontWallF2[:, 24..57]
      for (int y = 0; y < ExpandedHeight; y++)
      {
        int frontRow = y * frontW;
        int dstRow = y * ExpandedWidth160;

        for (int i = 0; i < CenterStripWidth160; i++)
        {
          dst[dstRow + CenterDestStartX160 + i] =
              frontPixels[frontRow + CenterSourceStartX160 + i];
        }

        for (int i = 0; i < RightAStripWidth160; i++)
        {
          dst[dstRow + RightADestStartX160 + i] =
              frontPixels[frontRow + RightASourceStartX160 + i];
        }

        for (int i = 0; i < RightBStripWidth160; i++)
        {
          dst[dstRow + RightBDestStartX160 + i] =
              frontPixels[frontRow + RightBSourceStartX160 + i];
        }
      }

      Texture2D expanded = new Texture2D(
          ExpandedWidth160,
          ExpandedHeight,
          TextureFormat.RGBA32,
          false);
      expanded.name = front.name + "_Expanded160";
      expanded.filterMode = FilterMode.Point;
      expanded.wrapMode = TextureWrapMode.Clamp;
      expanded.SetPixels32(dst);
      expanded.Apply(false, false);

      if (expanded.width != ExpandedWidth160)
        return front;

      cachedFront160 = front;
      cachedExpanded160 = expanded;
      return cachedExpanded160;
    }
  }
}
