using System.Collections.Generic;

namespace DM.Dungeon
{
  public sealed class HallWallOrnamentPlacement
  {
    public string Type;
    public int X;
    public int Y;
    public string Wall;
    public bool WallTilePlacement;
  }

  // Hall of Champions random wall ornaments from DUNGEON.DAT cell flags.
  // The JSON stores decorations on impassable wall tiles; the party sees
  // them from the adjacent walkable cell looking at that wall.
  public static class HallOfChampionsWallOrnaments
  {
    private const int RandomWallOrnamentCount = 4;
    private const int RandomOrnamentModulo = 30;
    private const int DungeonSeed = 99;

    public static HallWallOrnamentPlacement[] Build(DungeonMap map)
    {
      if (map == null)
        return new HallWallOrnamentPlacement[0];

      List<HallWallOrnamentPlacement> resolved =
          new List<HallWallOrnamentPlacement>();

      for (int y = 0; y < map.Height; y++)
      {
        for (int x = 0; x < map.Width; x++)
        {
          DungeonTile tile = map.GetTile(x, y);
          if (tile == null || tile.Type != DungeonTileType.Wall)
            continue;

          int raw = tile.Raw;
          TryAdd(resolved, x, y, raw, 0x01, "West", 4);
          TryAdd(resolved, x, y, raw, 0x02, "South", 3);
          TryAdd(resolved, x, y, raw, 0x04, "East", 2);
          TryAdd(resolved, x, y, raw, 0x08, "North", 1);
        }
      }

      return resolved.ToArray();
    }

    public static bool MatchesD1Front(
        HallWallOrnamentPlacement ornament,
        int playerX,
        int playerY,
        DungeonFacing playerFacing,
        int wallTileX,
        int wallTileY)
    {
      if (ornament == null)
        return false;

      if (ornament.WallTilePlacement)
      {
        string visiblePhysicalWallFace = OppositeFacingName(playerFacing);
        return ornament.X == wallTileX
            && ornament.Y == wallTileY
            && string.Equals(
                ornament.Wall,
                visiblePhysicalWallFace,
                System.StringComparison.OrdinalIgnoreCase);
      }

      string viewedWallSide = FacingName(playerFacing);
      return ornament.X == playerX
          && ornament.Y == playerY
          && string.Equals(
              ornament.Wall,
              viewedWallSide,
              System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsType(
        HallWallOrnamentPlacement ornament,
        string type)
    {
      return ornament != null
          && string.Equals(
              ornament.Type,
              type,
              System.StringComparison.OrdinalIgnoreCase);
    }

    public static string FacingName(DungeonFacing facing)
    {
      switch (facing)
      {
        case DungeonFacing.North:
          return "North";
        case DungeonFacing.East:
          return "East";
        case DungeonFacing.South:
          return "South";
        case DungeonFacing.West:
          return "West";
        default:
          return string.Empty;
      }
    }

    public static string OppositeFacingName(DungeonFacing facing)
    {
      switch (facing)
      {
        case DungeonFacing.North:
          return "South";
        case DungeonFacing.East:
          return "West";
        case DungeonFacing.South:
          return "North";
        case DungeonFacing.West:
          return "East";
        default:
          return string.Empty;
      }
    }

    private static void TryAdd(
        List<HallWallOrnamentPlacement> resolved,
        int x,
        int y,
        int raw,
        int faceMask,
        string physicalWallFace,
        int faceFactor)
    {
      if ((raw & faceMask) == 0)
        return;

      int ordinal = ResolveOrdinal(x, y, faceFactor);
      if (ordinal <= 0)
        return;

      string type = TypeFromOrdinal(ordinal);
      if (string.IsNullOrEmpty(type))
        return;

      resolved.Add(
          new HallWallOrnamentPlacement
          {
            Type = type,
            X = x,
            Y = y,
            Wall = physicalWallFace,
            WallTilePlacement = true
          });
    }

    private static int ResolveOrdinal(int mapX, int mapY, int faceFactor)
    {
      int value1 = 2000 + (mapX << 5) + ((mapY + 1) * faceFactor);
      int value2 = 3000 + 18 + 19;
      int index = Hash(value1, value2, RandomOrnamentModulo);
      return index < RandomWallOrnamentCount ? index + 1 : 0;
    }

    private static int Hash(int value1, int value2, int modulo)
    {
      if (modulo <= 0)
        return 0;

      unchecked
      {
        uint d0Long = (uint)(ushort)value1 * 31417u;
        ushort d0Word = (ushort)d0Long;
        d0Word = (ushort)((d0Word >> 1) & 0x7FFF);

        uint d1Long = (uint)(ushort)value2 * 11u;
        d0Word = (ushort)(d0Word + (ushort)d1Long);
        d0Word = (ushort)(d0Word + DungeonSeed);
        d0Word = (ushort)((d0Word >> 2) & 0x3FFF);

        return d0Word % modulo;
      }
    }

    private static string TypeFromOrdinal(int ordinal)
    {
      switch (ordinal)
      {
        case 1:
          return "Hook";
        case 2:
          return "Slime";
        case 3:
          return "Grate";
        case 4:
          return "WoodRing";
        default:
          return null;
      }
    }
  }
}
