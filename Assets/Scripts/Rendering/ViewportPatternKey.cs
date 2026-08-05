using System;

namespace DM.Rendering
{
  /// <summary>
  /// Facing-normalized corridor geometry. No absolute X/Y/Facing.
  /// Compact form: L0R0|L1C1R1|L2C2R2|L3C3R3 (W/O).
  /// </summary>
  [Serializable]
  public struct ViewportPatternKey : IEquatable<ViewportPatternKey>
  {
    public ViewportPatternOccupancy L0;
    public ViewportPatternOccupancy R0;

    public ViewportPatternOccupancy L1;
    public ViewportPatternOccupancy C1;
    public ViewportPatternOccupancy R1;

    public ViewportPatternOccupancy L2;
    public ViewportPatternOccupancy C2;
    public ViewportPatternOccupancy R2;

    public ViewportPatternOccupancy L3;
    public ViewportPatternOccupancy C3;
    public ViewportPatternOccupancy R3;

    public string ToCompactString()
    {
      return string.Concat(
          OccupancyChar(L0),
          OccupancyChar(R0),
          "|",
          OccupancyChar(L1),
          OccupancyChar(C1),
          OccupancyChar(R1),
          "|",
          OccupancyChar(L2),
          OccupancyChar(C2),
          OccupancyChar(R2),
          "|",
          OccupancyChar(L3),
          OccupancyChar(C3),
          OccupancyChar(R3));
    }

    public static bool TryParse(
        string compact,
        out ViewportPatternKey key)
    {
      key = default;
      if (string.IsNullOrEmpty(compact))
        return false;

      string[] parts = compact.Split('|');
      if (parts.Length != 4)
        return false;

      if (parts[0].Length != 2
          || parts[1].Length != 3
          || parts[2].Length != 3
          || parts[3].Length != 3)
      {
        return false;
      }

      if (!TryRead(parts[0][0], out key.L0)
          || !TryRead(parts[0][1], out key.R0)
          || !TryRead(parts[1][0], out key.L1)
          || !TryRead(parts[1][1], out key.C1)
          || !TryRead(parts[1][2], out key.R1)
          || !TryRead(parts[2][0], out key.L2)
          || !TryRead(parts[2][1], out key.C2)
          || !TryRead(parts[2][2], out key.R2)
          || !TryRead(parts[3][0], out key.L3)
          || !TryRead(parts[3][1], out key.C3)
          || !TryRead(parts[3][2], out key.R3))
      {
        return false;
      }

      return true;
    }

    public bool Equals(ViewportPatternKey other)
    {
      return L0 == other.L0
          && R0 == other.R0
          && L1 == other.L1
          && C1 == other.C1
          && R1 == other.R1
          && L2 == other.L2
          && C2 == other.C2
          && R2 == other.R2
          && L3 == other.L3
          && C3 == other.C3
          && R3 == other.R3;
    }

    public override bool Equals(object obj)
    {
      return obj is ViewportPatternKey other && Equals(other);
    }

    public override int GetHashCode()
    {
      unchecked
      {
        int hash = (int)L0;
        hash = (hash * 397) ^ (int)R0;
        hash = (hash * 397) ^ (int)L1;
        hash = (hash * 397) ^ (int)C1;
        hash = (hash * 397) ^ (int)R1;
        hash = (hash * 397) ^ (int)L2;
        hash = (hash * 397) ^ (int)C2;
        hash = (hash * 397) ^ (int)R2;
        hash = (hash * 397) ^ (int)L3;
        hash = (hash * 397) ^ (int)C3;
        hash = (hash * 397) ^ (int)R3;
        return hash;
      }
    }

    public override string ToString()
    {
      return ToCompactString();
    }

    private static char OccupancyChar(ViewportPatternOccupancy occupancy)
    {
      return occupancy == ViewportPatternOccupancy.Wall ? 'W' : 'O';
    }

    private static bool TryRead(
        char c,
        out ViewportPatternOccupancy occupancy)
    {
      if (c == 'W' || c == 'w')
      {
        occupancy = ViewportPatternOccupancy.Wall;
        return true;
      }

      if (c == 'O' || c == 'o')
      {
        occupancy = ViewportPatternOccupancy.Open;
        return true;
      }

      occupancy = ViewportPatternOccupancy.Open;
      return false;
    }
  }
}
