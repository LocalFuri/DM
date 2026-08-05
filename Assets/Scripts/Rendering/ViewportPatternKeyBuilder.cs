using DM.Dungeon;

namespace DM.Rendering
{
  /// <summary>
  /// Builds a facing-normalized <see cref="ViewportPatternKey"/> from map geometry.
  /// </summary>
  public static class ViewportPatternKeyBuilder
  {
    public static ViewportPatternKey Build(
        DungeonMap map,
        int playerX,
        int playerY,
        DungeonFacing facing)
    {
      DungeonMap.GetForwardOffset(facing, out int forwardX, out int forwardY);
      DungeonMap.GetRightOffset(facing, out int rightX, out int rightY);

      ViewportPatternKey key = default;

      key.L0 = Sample(map, playerX, playerY, 0, -1, forwardX, forwardY, rightX, rightY);
      key.R0 = Sample(map, playerX, playerY, 0, 1, forwardX, forwardY, rightX, rightY);

      key.L1 = Sample(map, playerX, playerY, 1, -1, forwardX, forwardY, rightX, rightY);
      key.C1 = Sample(map, playerX, playerY, 1, 0, forwardX, forwardY, rightX, rightY);
      key.R1 = Sample(map, playerX, playerY, 1, 1, forwardX, forwardY, rightX, rightY);

      key.L2 = Sample(map, playerX, playerY, 2, -1, forwardX, forwardY, rightX, rightY);
      key.C2 = Sample(map, playerX, playerY, 2, 0, forwardX, forwardY, rightX, rightY);
      key.R2 = Sample(map, playerX, playerY, 2, 1, forwardX, forwardY, rightX, rightY);

      key.L3 = Sample(map, playerX, playerY, 3, -1, forwardX, forwardY, rightX, rightY);
      key.C3 = Sample(map, playerX, playerY, 3, 0, forwardX, forwardY, rightX, rightY);
      key.R3 = Sample(map, playerX, playerY, 3, 1, forwardX, forwardY, rightX, rightY);

      return key;
    }

    private static ViewportPatternOccupancy Sample(
        DungeonMap map,
        int playerX,
        int playerY,
        int depth,
        int sideSign,
        int forwardX,
        int forwardY,
        int rightX,
        int rightY)
    {
      int tileX =
          playerX + forwardX * depth + rightX * sideSign;
      int tileY =
          playerY + forwardY * depth + rightY * sideSign;

      return Classify(map, tileX, tileY);
    }

    /// <summary>
    /// Wall: Wall tile, FalseWall (stored as Wall), or out-of-bounds.
    /// Open: Floor, Door, Teleporter, Pit, Stairs (all stored as Floor).
    /// </summary>
    private static ViewportPatternOccupancy Classify(
        DungeonMap map,
        int x,
        int y)
    {
      if (map == null || !map.IsInside(x, y))
        return ViewportPatternOccupancy.Wall;

      return map.GetTile(x, y).Type == DungeonTileType.Wall
          ? ViewportPatternOccupancy.Wall
          : ViewportPatternOccupancy.Open;
    }
  }
}
