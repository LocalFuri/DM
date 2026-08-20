using UnityEngine;

namespace DM.Rendering
{
  /// <summary>
  /// Builds the Front Wall F2 131×74 composite from FrontWallF2 + WallF2R
  /// via exact horizontal pixel copies (no mirroring, no scaling).
  /// 106×74 is the raw FrontWallF2 texture.
  /// </summary>
  public static class ExpandedF2WallTexture
  {
    public const int SourceWidth = 106;
    public const int SourceHeight = 74;
    public const int ExpandedWidth = 131;
    public const int ExpandedHeight = 74;

    /// <summary>dest[0..104] = FrontWallF2[:, 1..105]</summary>
    public const int FrontStripWidth = 105;
    public const int FrontSourceStartX = 1;

    /// <summary>dest[105..130] = WallF2R[:, 19..44]</summary>
    public const int RightStripWidth = 26;
    public const int RightDestStartX = 105;
    public const int RightSourceStartX = 19;

    private const int MinFrontWidth = FrontSourceStartX + FrontStripWidth;
    private const int MinRightWidth = RightSourceStartX + RightStripWidth;

    private static Texture2D cachedFront;
    private static Texture2D cachedRight;
    private static Texture2D cachedExpanded;

    /// <summary>
    /// Returns a cached 131×74 Front Wall F2 composite.
    /// Original textures are never modified. No horizontal mirroring.
    ///
    /// Proven mapping (attached 106×74 vs 131×74, palette-exact):
    ///   dest[0..104]   = FrontWallF2[:, 1..105]
    ///   dest[105..130] = WallF2R[:, 19..44]
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
      // dest[105..130] = WallF2R[:, 19..44]
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
      expanded.name = front.name + "_Expanded131";
      expanded.filterMode = FilterMode.Point;
      expanded.wrapMode = TextureWrapMode.Clamp;
      expanded.SetPixels32(dst);
      expanded.Apply(false, false);

      cachedFront = front;
      cachedRight = wallF2R;
      cachedExpanded = expanded;
      return cachedExpanded;
    }

    public const int ExpandedWidth160 = 160;
    /// <summary>dest[0..105] = FrontWallF2[:, 0..105]</summary>
    public const int FrontStripWidth160 = 106;
    public const int FrontSourceStartX160 = 0;
    /// <summary>dest[106..159] = FrontWallF2[:, 1..54]</summary>
    public const int WrapStripWidth160 = 54;
    public const int WrapDestStartX160 = 106;
    public const int WrapSourceStartX160 = 1;
    private const int MinFrontWidth160 = 106;

    private static Texture2D cachedFront160;
    private static Texture2D cachedExpanded160;

    /// <summary>
    /// Returns a cached 160×74 Front Wall F2 composite from FrontWallF2 only.
    /// Original textures are never modified. No horizontal mirroring.
    /// WallF2R is not used.
    ///
    ///   dest[0..105]   = FrontWallF2[:, 0..105]
    ///   dest[106..159] = FrontWallF2[:, 1..54]
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
      // dest[106..159] = FrontWallF2[:, 1..54]
      for (int y = 0; y < ExpandedHeight; y++)
      {
        int frontRow = y * frontW;
        int dstRow = y * ExpandedWidth160;

        for (int i = 0; i < FrontStripWidth160; i++)
        {
          Color32 colour =
              frontPixels[frontRow + FrontSourceStartX160 + i];
          colour.a = 255;
          dst[dstRow + i] = colour;
        }

        for (int i = 0; i < WrapStripWidth160; i++)
        {
          Color32 colour =
              frontPixels[frontRow + WrapSourceStartX160 + i];
          colour.a = 255;
          dst[dstRow + WrapDestStartX160 + i] = colour;
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
