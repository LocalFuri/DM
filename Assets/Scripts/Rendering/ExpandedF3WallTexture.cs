using UnityEngine;

namespace DM.Rendering
{
  /// <summary>
  /// Builds the Front Wall F3 composite (90×49) from WallF3L + FrontWallF3
  /// via exact horizontal pixel copies (no mirroring, no scaling).
  /// Shared by Edit Mode, Play Mode, and Build via GetTexture.
  /// </summary>
  public static class ExpandedF3WallTexture
  {
    /// <summary>Front source width historically authored (70); copy uses X 0..68.</summary>
    public const int SourceWidth = 70;
    public const int SourceHeight = 49;

    /// <summary>Proven left strip: dest X 0..16 from WallF3L source X 53..69.</summary>
    public const int LeftStripWidth = 17;
    public const int LeftSourceStartX = 53;

    /// <summary>Proven front strip: dest X 17..85 from Front source X 0..68.</summary>
    public const int FrontStripWidth = 69;
    public const int FrontDestStartX = 17;

    /// <summary>
    /// TEMPORARY final-edge placeholder: dest X 86..89 filled by repeating
    /// Front source column X=68 (last proven mapped front column). Not a real
    /// source strip — replace when the true right-edge mapping is known.
    /// </summary>
    public const int PlaceholderDestStartX = 86;
    public const int PlaceholderWidth = 4;
    public const int PlaceholderSourceX = 68;

    public const int ExpandedWidth = 90;
    public const int ExpandedHeight = 49;

    // Minimum source extents for the proven mapping (left needs X 53..69).
    private const int MinLeftWidth = LeftSourceStartX + LeftStripWidth; // 70
    private const int MinFrontWidth = FrontStripWidth; // 69 (X 0..68)

    private static Texture2D cachedFront;
    private static Texture2D cachedLeft;
    private static Texture2D cachedRight;
    private static Texture2D cachedExpanded;

    /// <summary>
    /// Last texture returned by <see cref="BuildExpandedF3Wall"/> (for draw diagnostics).
    /// </summary>
    public static Texture2D LastReturnedTexture { get; private set; }

    /// <summary>
    /// Returns a cached 90×49 Front Wall F3 composite.
    /// Original textures are never modified. No horizontal mirroring.
    ///
    /// Proven mapping (first 86 px):
    ///   dest[0..16]  = WallF3L[:, 53..69]   (direct copy)
    ///   dest[17..85] = Front[:, 0..68]      (direct copy)
    ///
    /// Temporary placeholder (last 4 px):
    ///   dest[86..89] = Front[:, 68]         (repeat last proven front column)
    /// </summary>
    public static Texture2D BuildExpandedF3Wall(
        Texture2D front,
        Texture2D wallF3L,
        Texture2D wallF3R)
    {
      if (front == null)
      {
        LastReturnedTexture = null;
        return null;
      }

      if (cachedExpanded != null
          && cachedFront == front
          && cachedLeft == wallF3L
          && cachedRight == wallF3R)
      {
        LastReturnedTexture = cachedExpanded;
        return cachedExpanded;
      }

      if (!front.isReadable)
      {
        LastReturnedTexture = front;
        return front;
      }

      if (wallF3L == null)
      {
        LastReturnedTexture = front;
        return front;
      }

      if (!wallF3L.isReadable)
      {
        LastReturnedTexture = front;
        return front;
      }

      if (front.width < MinFrontWidth || front.height < SourceHeight)
      {
        LastReturnedTexture = front;
        return front;
      }

      if (wallF3L.width < MinLeftWidth || wallF3L.height < SourceHeight)
      {
        LastReturnedTexture = front;
        return front;
      }

      Color32[] frontPixels = front.GetPixels32();
      Color32[] leftPixels = wallF3L.GetPixels32();
      int frontW = front.width;
      int leftW = wallF3L.width;
      Color32[] dst = new Color32[ExpandedWidth * ExpandedHeight];

      // Proven: dest[0..16] = WallF3L[:, 53..69] (direct, no mirror)
      // Proven: dest[17..85] = Front[:, 0..68] (direct, no mirror)
      // TEMP placeholder: dest[86..89] = Front[:, 68] repeated
      for (int y = 0; y < ExpandedHeight; y++)
      {
        int frontRow = y * frontW;
        int leftRow = y * leftW;
        int dstRow = y * ExpandedWidth;

        for (int i = 0; i < LeftStripWidth; i++)
        {
          dst[dstRow + i] =
              leftPixels[leftRow + LeftSourceStartX + i];
        }

        for (int i = 0; i < FrontStripWidth; i++)
        {
          dst[dstRow + FrontDestStartX + i] =
              frontPixels[frontRow + i];
        }

        // TEMPORARY final-edge placeholder — not a real source strip.
        // Extends the last proven Front column (source X=68) into dest X 86..89
        // so the texture stays 90×49 until the true right-edge mapping is known.
        Color32 edge = frontPixels[frontRow + PlaceholderSourceX];
        for (int i = 0; i < PlaceholderWidth; i++)
        {
          dst[dstRow + PlaceholderDestStartX + i] = edge;
        }
      }

      Texture2D expanded = new Texture2D(
          ExpandedWidth,
          ExpandedHeight,
          TextureFormat.RGBA32,
          false);
      expanded.name = front.name + "_Expanded90";
      expanded.filterMode = FilterMode.Point;
      expanded.wrapMode = TextureWrapMode.Clamp;
      expanded.SetPixels32(dst);
      expanded.Apply(false, false);

      cachedFront = front;
      cachedLeft = wallF3L;
      cachedRight = wallF3R;
      cachedExpanded = expanded;
      LastReturnedTexture = expanded;

      return cachedExpanded;
    }
  }
}
