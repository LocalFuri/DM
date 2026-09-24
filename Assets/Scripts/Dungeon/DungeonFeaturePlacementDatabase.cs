using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DM.Dungeon
{
  [Serializable]
  public sealed class DungeonFeaturePlacementDatabase
  {
    public int schemaVersion;
    public string databaseName;
    public DungeonFeatureLevelInfo[] levels;
    public DungeonOrnamentPlacement[] ornamentPlacements;
    public DungeonFeaturePlacementSummary summary;

    public static DungeonFeaturePlacementDatabase LoadFromJsonText(string json)
    {
      if (string.IsNullOrEmpty(json))
        return null;
      return JsonUtility.FromJson<DungeonFeaturePlacementDatabase>(json);
    }

    public static DungeonFeaturePlacementDatabase LoadFromFile(string path)
    {
      if (string.IsNullOrEmpty(path) || !File.Exists(path))
        return null;
      return LoadFromJsonText(File.ReadAllText(path));
    }

    public List<DungeonOrnamentPlacement> FindResolvedFeature(string type)
    {
      return FindResolvedFeature(-1, type);
    }

    public List<DungeonOrnamentPlacement> FindResolvedFeature(
        int level,
        string type)
    {
      List<DungeonOrnamentPlacement> result =
          new List<DungeonOrnamentPlacement>();
      if (ornamentPlacements == null || string.IsNullOrEmpty(type))
        return result;

      for (int i = 0; i < ornamentPlacements.Length; i++)
      {
        DungeonOrnamentPlacement p = ornamentPlacements[i];
        if (p == null || p.resolved == null)
          continue;
        if (level >= 0 && p.level != level)
          continue;
        if (!string.Equals(
                p.resolved.type,
                type,
                StringComparison.OrdinalIgnoreCase))
          continue;
        result.Add(p);
      }

      return result;
    }
  }

  [Serializable]
  public sealed class DungeonFeatureLevelInfo
  {
    public int level;
    public string file;
    public int offsetX;
    public int offsetY;
    public int width;
    public int height;
    public int difficulty;
    public int randomWallOrnamentCount;
    public int wallOrnamentCount;
    public int randomFloorOrnamentCount;
    public int floorOrnamentCount;
    public int doorOrnamentCount;
    public int locatedThingCount;
  }

  [Serializable]
  public sealed class DungeonOrnamentPlacement
  {
    public string id;
    public string source;
    public string sourcePlacementId;
    public int level;
    public int localX;
    public int localY;
    public int globalX;
    public int globalY;
    public string direction;
    public string recordType;
    public int ornamentOrdinal;
    public string domain;
    public int normalisedX;
    public int normalisedY;
    public int normalisedGlobalX;
    public int normalisedGlobalY;
    public string normalisedFace;
    public string classificationBasis;
    public DungeonResolvedOrnament resolved;
  }

  [Serializable]
  public sealed class DungeonResolvedOrnament
  {
    public int sourceId;
    public string type;
    public string sourceName;
    public string category;
  }

  [Serializable]
  public sealed class DungeonFeaturePlacementSummary
  {
    public int explicitPlacementCount;
    public int ornamentPlacementCount;
    public int resolvedOrnamentPlacementCount;
    public int unresolvedOrnamentPlacementCount;
  }
}
