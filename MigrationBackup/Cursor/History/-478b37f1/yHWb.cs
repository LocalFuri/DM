using UnityEngine;

namespace DM.Rendering
{
  /// <summary>
  /// Builds Wall F2Left as a non-rectangular partial-region composite.
  /// Starts from full F2L; replaces only the per-row front-facing X range
  /// with a horizontal mirror of the matching F2R front-facing columns.
  /// Receding / transition pixels of F2L are left untouched.
  /// </summary>
  public static class F2LeftWallTexture
  {
    public const int Width = 78;
    public const int Height = 74;

    /// <summary>
    /// Per-row first destination X of the front face (inclusive through 77).
    /// Measured from original DM 224×111 crops: corner seam is the dark
    /// vertical mortar between dithered receding wall and flat front face;
    /// left seam ↔ right seam are exact mirrors (e.g. 49 ↔ 174).
    ///
    /// Alternating brick courses:
    ///   wide  (start 50): y 0..17, 40..58
    ///   narrow (start 59): y 18..37, 65..73
    ///   tooth transitions / floor steps: remaining rows
    ///
    /// Mapping: dst[x,y] = F2R[77 - x, y] for x in [start[y], 77].
    /// </summary>
    private static readonly int[] FrontFaceStartX =
    {
      // y 0..17 — wide course (seam 49)
      50, 50, 50, 50, 50, 50, 50, 50,
      50, 50, 50, 50, 50, 50, 50, 50,
      50, 50,
      // y 18..37 — narrow course (no extension past front-wall edge)
      59, 59, 59, 59, 59, 59, 59, 59,
      59, 59, 59, 59, 59, 59, 59, 59,
      59, 59, 59, 59,
      // y 38..39 — tooth step into next wide course
      55, 53,
      // y 40..58 — wide course (seam 49)
      50, 50, 50, 50, 50, 50, 50, 50,
      50, 50, 50, 50, 50, 50, 50, 50,
      50, 50, 50,
      // y 59..64 — floor-corner step (widening then)
      54, 52, 51, 49, 48, 46,
      // y 65..73 — narrow (floor silhouette; no side extension)
      59, 59, 59, 59, 59, 59, 59, 59,
      59
    };

    private static Texture2D cachedLeft;
    private static Texture2D cachedRight;
    private static Texture2D cachedComposite;

    /// <summary>
    /// Returns a cached 78×74 Wall F2Left with only the confirmed
    /// front-facing pixels replaced by a mirrored copy from Wall F2Right.
    /// Original textures are never modified.
    /// </summary>
    public static Texture2D BuildF2Left(
        Texture2D wallF2L,
        Texture2D wallF2R)
    {
      if (wallF2L == null)
        return null;

      if (cachedComposite != null
          && cachedLeft == wallF2L
          && cachedRight == wallF2R)
      {
        return cachedComposite;
      }

      if (!wallF2L.isReadable
          || wallF2R == null
          || !wallF2R.isReadable)
      {
        Debug.LogWarning(
            "F2LeftWallTexture: WallF2L / WallF2R must be readable "
                + "(Read/Write enabled).");
        return wallF2L;
      }

      if (wallF2L.width < Width || wallF2L.height < Height
          || wallF2R.width < Width || wallF2R.height < Height)
      {
        Debug.LogWarning(
            "F2LeftWallTexture: WallF2L/R expected at least 78x74, got "
                + "L "
                + wallF2L.width
                + "x"
                + wallF2L.height
                + ", R "
                + wallF2R.width
                + "x"
                + wallF2R.height
                + ".");
        return wallF2L;
      }

      Color32[] leftPixels = wallF2L.GetPixels32();
      Color32[] rightPixels = wallF2R.GetPixels32();
      int leftW = wallF2L.width;
      int rightW = wallF2R.width;
      Color32[] dst = new Color32[Width * Height];

      // Full F2L copy, then overwrite only the per-row front-face span.
      for (int y = 0; y < Height; y++)
      {
        int leftRow = y * leftW;
        int rightRow = y * rightW;
        int dstRow = y * Width;

        for (int x = 0; x < Width; x++)
          dst[dstRow + x] = leftPixels[leftRow + x];

        int frontStart = FrontFaceStartX[y];
        for (int x = frontStart; x < Width; x++)
          dst[dstRow + x] = rightPixels[rightRow + (Width - 1 - x)];
      }

      Texture2D composite = new Texture2D(
          Width,
          Height,
          TextureFormat.RGBA32,
          false);
      composite.name = wallF2L.name + "_F2LeftComposite";
      composite.filterMode = FilterMode.Point;
      composite.wrapMode = TextureWrapMode.Clamp;
      composite.SetPixels32(dst);
      composite.Apply(false, false);

      cachedLeft = wallF2L;
      cachedRight = wallF2R;
      cachedComposite = composite;
      return cachedComposite;
    }
  }
}
