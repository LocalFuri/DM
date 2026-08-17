namespace DM.Dungeon
{
  public class DungeonTile
  {
    public DungeonTileType Type;

    public DungeonSourceTileType SourceType;

    // Original map-file tile value from the JSON "raw" field.
    public int Raw;

    // Display helper only. Must not be used by movement or collision.
    public bool TryGetStairsDirection(out bool isUp)
    {
      isUp = false;

      if (SourceType != DungeonSourceTileType.Stairs)
        return false;

      isUp = (Raw & 0x08) != 0;
      return true;
    }
  }
}
