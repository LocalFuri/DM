namespace DM.Dungeon
{
  public class DungeonMap
  {
    private readonly DungeonTile[,] _tiles;

    public int Width { get; }
    public int Height { get; }

    public DungeonMap(int width, int height)
    {
      Width = width;
      Height = height;

      _tiles = new DungeonTile[width, height];

      CreateTestDungeon();
    }

    public DungeonTile GetTile(int x, int y)
    {
      return _tiles[x, y];
    }

    public string BuildDebugMap()
    {
      string result = "";

      for (int y = Height - 1; y >= 0; y--)
      {
        for (int x = 0; x < Width; x++)
        {
          result += _tiles[x, y].Type == DungeonTileType.Wall
              ? "#"
              : ".";
        }

        result += "\n";
      }

      return result;
    }

    private void CreateTestDungeon()
    {
      for (int y = 0; y < Height; y++)
      {
        for (int x = 0; x < Width; x++)
        {
          bool isBorder =
              x == 0 ||
              y == 0 ||
              x == Width - 1 ||
              y == Height - 1;

          _tiles[x, y] = new DungeonTile
          {
            Type = isBorder
                  ? DungeonTileType.Wall
                  : DungeonTileType.Floor
          };
        }
      }
    }
  }
}