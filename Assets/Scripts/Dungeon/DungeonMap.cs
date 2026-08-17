using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DM.Dungeon
{
  public class DungeonMap
  {
    // Fallback start when playerStart is missing or invalid in map JSON.
    private const int HallOfChampionsStartX = 4;
    private const int HallOfChampionsStartY = 3;
    private static readonly DungeonFacing HallOfChampionsStartFacing =
        DungeonFacing.South;

    private static readonly Regex TileRegex = new Regex(
        "\\{\\s*\"x\"\\s*:\\s*(?<x>\\d+)\\s*,\\s*\"y\"\\s*:\\s*(?<y>\\d+)\\s*," +
        "\\s*\"raw\"\\s*:\\s*(?<raw>\\d+)\\s*,\\s*\"hex\"\\s*:\\s*\"[^\"]*\"\\s*," +
        "\\s*\"type\"\\s*:\\s*\"(?<type>[^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly DungeonTile[,] _tiles;

    public string Name { get; private set; }

    public int Width { get; }
    public int Height { get; }

    public int PlayerX { get; private set; }
    public int PlayerY { get; private set; }
    public DungeonFacing PlayerFacing { get; private set; }

    public int StartX { get; private set; }
    public int StartY { get; private set; }
    public DungeonFacing StartFacing { get; private set; }

    private DungeonMap(string name, int width, int height)
    {
      Name = name;
      Width = width;
      Height = height;
      _tiles = new DungeonTile[width, height];
    }

    public static DungeonMap LoadFromJson(TextAsset mapJson)
    {
      if (mapJson == null)
      {
        throw new ArgumentNullException(nameof(mapJson));
      }

      return LoadFromJsonText(mapJson.text);
    }

    public static DungeonMap LoadFromJsonText(string json)
    {
      if (string.IsNullOrEmpty(json))
      {
        throw new ArgumentException(
            "Map JSON text is empty.",
            nameof(json)
        );
      }

      MapHeader header = JsonUtility.FromJson<MapHeader>(json);

      if (header == null
          || header.width <= 0
          || header.height <= 0)
      {
        throw new InvalidOperationException(
            "Map JSON is missing a valid width/height."
        );
      }

      string mapName = string.IsNullOrEmpty(header.name)
          ? "(unnamed)"
          : header.name;

      DungeonMap map = new DungeonMap(
          mapName,
          header.width,
          header.height
      );

      map.LoadTiles(json);
      map.ApplyPlayerStartFromHeader(header);

      return map;
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

    // JSON maps use top-left origin with Y increasing downward.
    public static void GetForwardOffset(
        DungeonFacing facing,
        out int dx,
        out int dy)
    {
      switch (facing)
      {
        case DungeonFacing.North:
          dx = 0;
          dy = -1;
          break;
        case DungeonFacing.East:
          dx = 1;
          dy = 0;
          break;
        case DungeonFacing.South:
          dx = 0;
          dy = 1;
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
          dy = 1;
          break;
        case DungeonFacing.South:
          dx = -1;
          dy = 0;
          break;
        case DungeonFacing.West:
          dx = 0;
          dy = -1;
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

      // Print top row (y = 0) first to match JSON top-left origin.
      for (int y = 0; y < Height; y++)
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

    private void LoadTiles(string json)
    {
      for (int y = 0; y < Height; y++)
      {
        for (int x = 0; x < Width; x++)
        {
          _tiles[x, y] = new DungeonTile
          {
            Type = DungeonTileType.Wall,
            SourceType = DungeonSourceTileType.Wall,
            Raw = 0
          };
        }
      }

      MatchCollection matches = TileRegex.Matches(json);

      foreach (Match match in matches)
      {
        int x = int.Parse(match.Groups["x"].Value);
        int y = int.Parse(match.Groups["y"].Value);
        int raw = int.Parse(match.Groups["raw"].Value);
        string typeName = match.Groups["type"].Value;

        if (!IsInside(x, y))
        {
          Debug.LogWarning(
              $"DungeonMap: Tile ({x},{y}) is outside " +
              $"{Width}x{Height}; skipped."
          );
          continue;
        }

        _tiles[x, y] = new DungeonTile
        {
          Type = ConvertTileType(typeName),
          SourceType = ParseSourceType(typeName),
          Raw = raw
        };
      }
    }

    private void ApplyPlayerStartFromHeader(MapHeader header)
    {
      if (TryParsePlayerStart(
              header,
              out int startX,
              out int startY,
              out DungeonFacing startFacing))
      {
        SetPlayerStart(startX, startY, startFacing);
        StartX = startX;
        StartY = startY;
        StartFacing = startFacing;
        return;
      }

      Debug.LogWarning(
          "DungeonMap: playerStart missing or invalid in map JSON; " +
          $"falling back to ({HallOfChampionsStartX}," +
          $"{HallOfChampionsStartY}) facing " +
          $"{HallOfChampionsStartFacing}."
      );

      SetPlayerStart(
          HallOfChampionsStartX,
          HallOfChampionsStartY,
          HallOfChampionsStartFacing
      );

      StartX = HallOfChampionsStartX;
      StartY = HallOfChampionsStartY;
      StartFacing = HallOfChampionsStartFacing;
    }

    private bool TryParsePlayerStart(
        MapHeader header,
        out int startX,
        out int startY,
        out DungeonFacing startFacing)
    {
      startX = 0;
      startY = 0;
      startFacing = HallOfChampionsStartFacing;

      if (header == null || header.playerStart == null)
        return false;

      PlayerStartData start = header.playerStart;
      if (!TryParseFacing(start.facing, out startFacing))
        return false;

      startX = start.x;
      startY = start.y;

      if (!CanEnter(startX, startY))
        return false;

      return true;
    }

    private static bool TryParseFacing(
        string facingName,
        out DungeonFacing facing)
    {
      facing = HallOfChampionsStartFacing;

      if (string.IsNullOrEmpty(facingName))
        return false;

      switch (facingName)
      {
        case "North":
          facing = DungeonFacing.North;
          return true;
        case "East":
          facing = DungeonFacing.East;
          return true;
        case "South":
          facing = DungeonFacing.South;
          return true;
        case "West":
          facing = DungeonFacing.West;
          return true;
        default:
          return false;
      }
    }

    private void SetPlayerStart(
        int x,
        int y,
        DungeonFacing facing)
    {
      if (!CanEnter(x, y))
      {
        throw new InvalidOperationException(
            $"DungeonMap: Start tile ({x},{y}) is not enterable."
        );
      }

      PlayerX = x;
      PlayerY = y;
      PlayerFacing = facing;
    }

    public void SetPlayerPose(
        int x,
        int y,
        DungeonFacing facing)
    {
      SetPlayerStart(x, y, facing);
    }

    private static DungeonTileType ConvertTileType(string typeName)
    {
      switch (typeName)
      {
        case "Wall":
        case "FalseWall":
        case "Special":
        case "Unknown":
          return DungeonTileType.Wall;

        case "Floor":
        case "Door":
        case "Teleporter":
        case "Stairs":
        case "Pit":
          return DungeonTileType.Floor;

        default:
          return DungeonTileType.Wall;
      }
    }

    private static DungeonSourceTileType ParseSourceType(string typeName)
    {
      switch (typeName)
      {
        case "Wall":
          return DungeonSourceTileType.Wall;
        case "Floor":
          return DungeonSourceTileType.Floor;
        case "Door":
          return DungeonSourceTileType.Door;
        case "Teleporter":
          return DungeonSourceTileType.Teleporter;
        case "Stairs":
          return DungeonSourceTileType.Stairs;
        case "Pit":
          return DungeonSourceTileType.Pit;
        case "FalseWall":
          return DungeonSourceTileType.FalseWall;
        case "Special":
          return DungeonSourceTileType.Special;
        case "Unknown":
          return DungeonSourceTileType.Unknown;
        default:
          return DungeonSourceTileType.Unknown;
      }
    }

    [Serializable]
    private class MapHeader
    {
      public string name;
      public int width;
      public int height;
      public PlayerStartData playerStart;
    }

    [Serializable]
    private class PlayerStartData
    {
      public int x;
      public int y;
      public string facing;
    }
  }
}
