namespace DM.Dungeon
{
  public class DungeonMap
  {
    // Top row is north (y = Height - 1).
    private static readonly string[] MinimalTestRows =
    {
      "#####",
      "#...#",
      "#.#.#",
      "#...#",
      "#####"
    };

    private readonly DungeonTile[,] _tiles;

    public int Width { get; }
    public int Height { get; }

    public int PlayerX { get; private set; }
    public int PlayerY { get; private set; }
    public DungeonFacing PlayerFacing { get; private set; }

    public DungeonMap(int width, int height)
    {
      Width = width;
      Height = height;

      _tiles = new DungeonTile[width, height];

      CreateTestDungeon();
    }

    public static DungeonMap CreateMinimalTestDungeon()
    {
      return new DungeonMap(5, 5);
    }

    public DungeonTile GetTile(int x, int y)
    {
      return _tiles[x, y];
    }

    public bool IsInside(int x, int y)
    {
      return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    public bool CanEnter(int x, int y)
    {
      if (!IsInside(x, y))
        return false;

      return _tiles[x, y].Type != DungeonTileType.Wall;
    }

    public bool TryMoveBy(int deltaX, int deltaY)
    {
      int nextX = PlayerX + deltaX;
      int nextY = PlayerY + deltaY;

      if (!CanEnter(nextX, nextY))
        return false;

      PlayerX = nextX;
      PlayerY = nextY;
      return true;
    }

    public void TurnLeft()
    {
      PlayerFacing = PlayerFacing switch
      {
        DungeonFacing.North => DungeonFacing.West,
        DungeonFacing.West => DungeonFacing.South,
        DungeonFacing.South => DungeonFacing.East,
        DungeonFacing.East => DungeonFacing.North,
        _ => PlayerFacing
      };
    }

    public void TurnRight()
    {
      PlayerFacing = PlayerFacing switch
      {
        DungeonFacing.North => DungeonFacing.East,
        DungeonFacing.East => DungeonFacing.South,
        DungeonFacing.South => DungeonFacing.West,
        DungeonFacing.West => DungeonFacing.North,
        _ => PlayerFacing
      };
    }

    // localX: -1 strafe left, +1 strafe right
    // localY: +1 forward, -1 backward
    public void GetWorldOffset(
        int localX,
        int localY,
        out int worldDx,
        out int worldDy)
    {
      GetForwardOffset(PlayerFacing, out int forwardX, out int forwardY);
      GetRightOffset(PlayerFacing, out int rightX, out int rightY);

      worldDx = forwardX * localY + rightX * localX;
      worldDy = forwardY * localY + rightY * localX;
    }

    public static void GetForwardOffset(
        DungeonFacing facing,
        out int dx,
        out int dy)
    {
      switch (facing)
      {
        case DungeonFacing.North:
          dx = 0;
          dy = 1;
          break;
        case DungeonFacing.East:
          dx = 1;
          dy = 0;
          break;
        case DungeonFacing.South:
          dx = 0;
          dy = -1;
          break;
        case DungeonFacing.West:
          dx = -1;
          dy = 0;
          break;
        default:
          dx = 0;
          dy = 0;
          break;
      }
    }

    public static void GetRightOffset(
        DungeonFacing facing,
        out int dx,
        out int dy)
    {
      switch (facing)
      {
        case DungeonFacing.North:
          dx = 1;
          dy = 0;
          break;
        case DungeonFacing.East:
          dx = 0;
          dy = -1;
          break;
        case DungeonFacing.South:
          dx = -1;
          dy = 0;
          break;
        case DungeonFacing.West:
          dx = 0;
          dy = 1;
          break;
        default:
          dx = 0;
          dy = 0;
          break;
      }
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
      if (Width == 5 && Height == 5)
      {
        CreateMinimalTestLayout();
        return;
      }

      CreateBorderTestLayout();
    }

    private void CreateMinimalTestLayout()
    {
      for (int y = 0; y < Height; y++)
      {
        string row = MinimalTestRows[Height - 1 - y];

        for (int x = 0; x < Width; x++)
        {
          _tiles[x, y] = new DungeonTile
          {
            Type = row[x] == '#'
                ? DungeonTileType.Wall
                : DungeonTileType.Floor
          };
        }
      }

      PlayerX = 2;
      PlayerY = 3;
      PlayerFacing = DungeonFacing.North;
    }

    private void CreateBorderTestLayout()
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

      PlayerX = 1;
      PlayerY = 1;
      PlayerFacing = DungeonFacing.North;
    }
  }
}
