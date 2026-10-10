using System;
using System.Collections.Generic;
using System.Globalization;
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

    public int EntranceX { get; private set; }
    public int EntranceY { get; private set; }

    public bool ExitRevealed { get; private set; }
    public int ExitX { get; private set; }
    public int ExitY { get; private set; }

    private DungeonMap(string name, int width, int height)
    {
      Name = name;
      Width = width;
      Height = height;
      _tiles = new DungeonTile[width, height];
      EntranceX = 1;
      EntranceY = 2;
      ExitX = 1;
      ExitY = 3;
      ExitRevealed = false;
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
        DungeonMap rawGridMap = TryLoadRawTileGridMap(json);
        if (rawGridMap != null)
          return rawGridMap;

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

      if (x == EntranceX && y == EntranceY)
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

    public bool TryGetStairsAtPlayer(out bool isUp)
    {
      isUp = false;

      if (!IsInside(PlayerX, PlayerY))
        return false;

      DungeonTile tile = GetTile(PlayerX, PlayerY);
      return tile != null && tile.TryGetStairsDirection(out isUp);
    }

    public bool TryFindStairs(
        bool isUp,
        int preferredX,
        int preferredY,
        out int stairsX,
        out int stairsY)
    {
      stairsX = -1;
      stairsY = -1;
      int bestDistance = int.MaxValue;

      for (int y = 0; y < Height; y++)
      {
        for (int x = 0; x < Width; x++)
        {
          DungeonTile tile = GetTile(x, y);
          if (tile == null
              || !tile.TryGetStairsDirection(out bool tileIsUp)
              || tileIsUp != isUp)
          {
            continue;
          }

          int distance =
              Mathf.Abs(x - preferredX) + Mathf.Abs(y - preferredY);

          if (distance >= bestDistance)
            continue;

          bestDistance = distance;
          stairsX = x;
          stairsY = y;
        }
      }

      return stairsX >= 0 && stairsY >= 0;
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

    // DUNGEON.DAT exports store an integer tile_grid instead of the
    // Hall width/height/tiles objects. Each cell is the original raw byte.
    // Bits 7-5 are the tile type, matching the Hall JSON "raw" field.
    private static DungeonMap TryLoadRawTileGridMap(string json)
    {
      List<int[]> rows = TryParseTileGrid(json);
      if (rows == null || rows.Count == 0 || rows[0].Length == 0)
        return null;

      int height = rows.Count;
      int width = 0;
      for (int y = 0; y < rows.Count; y++)
      {
        if (rows[y].Length > width)
          width = rows[y].Length;
      }

      int level = TryReadTopLevelInt(json, "level");
      string mapName = level > 0 ? "Level " + level : "Dungeon Level";
      DungeonMap map = new DungeonMap(mapName, width, height);

      for (int y = 0; y < height; y++)
      {
        int[] row = rows[y];
        for (int x = 0; x < width; x++)
        {
          int raw = x < row.Length ? row[x] : 0;
          string typeName = RawTileTypeName(raw);
          map._tiles[x, y] = new DungeonTile
          {
            Type = ConvertTileType(typeName),
            SourceType = ParseSourceType(typeName),
            Raw = raw
          };
        }
      }

      // Level 0 retains the verified Hall of Champions party entry point.
      // The original DUNGEON.DAT tile grid has no playerStart object.
      if (level == 0)
      {
        const int hallStartX = 1;
        const int hallStartY = 3;
        if (!map.CanEnter(hallStartX, hallStartY))
          throw new InvalidOperationException(
              "DungeonMap: original Level 0 start (1,3) is not enterable.");
        map.SetPlayerStart(hallStartX, hallStartY, DungeonFacing.South);
        map.StartX = hallStartX;
        map.StartY = hallStartY;
        map.StartFacing = DungeonFacing.South;
        return map;
      }

      // Preserve the existing start selection for Levels 1-13 for now.
      // Stair transitions set their own validated destination pose.
      if (!TryFindEnterableStart(map, out int startX, out int startY))
      {
        throw new InvalidOperationException(
            "DungeonMap: raw tile grid has no enterable tile.");
      }

      map.SetPlayerStart(startX, startY, DungeonFacing.North);
      map.StartX = startX;
      map.StartY = startY;
      map.StartFacing = DungeonFacing.North;
      return map;
    }

    private static bool TryFindEnterableStart(
        DungeonMap map,
        out int startX,
        out int startY)
    {
      startX = 0;
      startY = 0;
      int fallbackX = -1;
      int fallbackY = -1;

      for (int y = 0; y < map.Height; y++)
      {
        for (int x = 0; x < map.Width; x++)
        {
          if (!map.CanEnter(x, y))
            continue;

          if (fallbackX < 0)
          {
            fallbackX = x;
            fallbackY = y;
          }

          DungeonTile tile = map._tiles[x, y];
          if (tile != null && tile.TryGetStairsDirection(out _))
          {
            startX = x;
            startY = y;
            return true;
          }
        }
      }

      if (fallbackX < 0)
        return false;

      startX = fallbackX;
      startY = fallbackY;
      return true;
    }

    private static string RawTileTypeName(int raw)
    {
      switch ((raw >> 5) & 7)
      {
        case 0: return "Wall";
        case 1: return "Floor";
        case 2: return "Pit";
        case 3: return "Stairs";
        case 4: return "Door";
        case 5: return "Teleporter";
        case 6: return "FalseWall";
        default: return "Unknown";
      }
    }

    private static int TryReadTopLevelInt(string json, string fieldName)
    {
      string token = "\"" + fieldName + "\"";
      int key = json.IndexOf(token, StringComparison.Ordinal);
      if (key < 0)
        return 0;

      int colon = json.IndexOf(':', key + token.Length);
      if (colon < 0)
        return 0;

      int i = colon + 1;
      while (i < json.Length && char.IsWhiteSpace(json[i]))
        i++;

      int start = i;
      if (i < json.Length && json[i] == '-')
        i++;
      while (i < json.Length && char.IsDigit(json[i]))
        i++;
      if (i == start || (json[start] == '-' && i == start + 1))
        return 0;

      return int.Parse(
          json.Substring(start, i - start),
          CultureInfo.InvariantCulture);
    }

    private static List<int[]> TryParseTileGrid(string json)
    {
      int key = json.IndexOf("\"tile_grid\"", StringComparison.Ordinal);
      if (key < 0)
        return null;

      int start = json.IndexOf('[', key);
      if (start < 0)
        return null;

      List<int[]> rows = new List<int[]>();
      int i = start + 1;
      int length = json.Length;
      while (i < length)
      {
        while (i < length && char.IsWhiteSpace(json[i]))
          i++;
        if (i >= length)
          return null;
        if (json[i] == ']')
          return rows;
        if (json[i] == ',')
        {
          i++;
          continue;
        }
        if (json[i] != '[')
          return null;

        i++;
        List<int> row = new List<int>();
        while (i < length)
        {
          while (i < length && char.IsWhiteSpace(json[i]))
            i++;
          if (i >= length)
            return null;
          if (json[i] == ']')
          {
            i++;
            break;
          }
          if (json[i] == ',')
          {
            i++;
            continue;
          }

          int numStart = i;
          if (json[i] == '-')
            i++;
          while (i < length && char.IsDigit(json[i]))
            i++;
          if (i == numStart || (json[numStart] == '-' && i == numStart + 1))
            return null;

          row.Add(int.Parse(
              json.Substring(numStart, i - numStart),
              CultureInfo.InvariantCulture));
        }

        rows.Add(row.ToArray());
      }

      return null;
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

    public void SetExitRevealed(bool revealed)
    {
      ExitRevealed = revealed;
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
