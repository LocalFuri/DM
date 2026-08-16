using System.Collections.Generic;
using DM.Dungeon;
using UnityEngine;

namespace DM.Rendering
{
  /// <summary>
  /// Stage-1 pattern library and shared Edit Mode / runtime lookup.
  /// </summary>
  public static class ViewportPatternCatalog
  {
    public const string Verified12SouthPatternId = "Verified_1_2_South";
    public const string Verified12SouthGeometryKey = "WW|WOW|WOW|WOO";

    private static readonly ViewportPatternDefinition[] Patterns =
    {
      CreateVerified12South()
    };

    private static string lastUnknownKeyWarned;
    private static ViewportPatternDefinition lastMatchedPattern;

    public static IReadOnlyList<ViewportPatternDefinition> AllPatterns
    {
      get { return Patterns; }
    }

    public static ViewportPatternDefinition LastMatchedPattern
    {
      get { return lastMatchedPattern; }
    }

    public static bool TryFindByKey(
        ViewportPatternKey key,
        out ViewportPatternDefinition definition)
    {
      string compact = key.ToCompactString();
      for (int i = 0; i < Patterns.Length; i++)
      {
        ViewportPatternDefinition pattern = Patterns[i];
        if (pattern == null)
          continue;

        if (pattern.GeometryKey == compact)
        {
          definition = pattern;
          lastMatchedPattern = pattern;
          return true;
        }
      }

      definition = null;
      return false;
    }

    /// <summary>
    /// Temporary draw gate shared by Edit Mode and runtime.
    /// Does not modify ViewportLayout / piece.Enabled.
    /// </summary>
    /// <param name="warnUnknownKeyInEditor">
    /// When true, logs an Editor warning once per distinct unknown key.
    /// </param>
    public static bool ShouldDrawPiece(
        ViewportPiece piece,
        DungeonMap map,
        int playerX,
        int playerY,
        DungeonFacing facing,
        bool warnUnknownKeyInEditor)
    {
      if (piece == null)
        return false;

      if (piece.Graphic == DungeonGraphicType.None)
        return false;

      // Permanent kit availability — never overwritten by patterns.
      if (!piece.Enabled)
        return false;

      // Non-wall kit pieces keep authored Enabled composition.
      if (!IsPatternControlledWallGraphic(piece.Graphic))
        return true;

      if (map == null)
        return true;

      ViewportPatternKey key = ViewportPatternKeyBuilder.Build(
          map,
          playerX,
          playerY,
          facing);

      if (TryFindByKey(key, out ViewportPatternDefinition pattern))
        return pattern.ContainsGraphic(piece.Graphic);

      // Unknown key: do not guess; do not use IsDepthWallVisible.
      // Keep authored Enabled composition (already passed the Enabled check).
      if (warnUnknownKeyInEditor)
      {
        WarnUnknownKey(
            key.ToCompactString(),
            playerX,
            playerY,
            facing);
      }

      return true;
    }

    public static bool IsPatternControlledWallGraphic(
        DungeonGraphicType graphic)
    {
      switch (graphic)
      {
        case DungeonGraphicType.WallF0L:
        case DungeonGraphicType.WallF0R:
        case DungeonGraphicType.WallF1L:
        case DungeonGraphicType.WallF1R:
        case DungeonGraphicType.WallF2L:
        case DungeonGraphicType.WallF2R:
        case DungeonGraphicType.WallF3L:
        case DungeonGraphicType.WallF3R:
        case DungeonGraphicType.FrontWallF1:
        case DungeonGraphicType.FrontWallF1_A:
        case DungeonGraphicType.FrontWallF1_B:
        case DungeonGraphicType.FrontWallF2:
        case DungeonGraphicType.FrontWallF3:

        //case DungeonGraphicType.WallS3L:
        //case DungeonGraphicType.WallS3R:
        case DungeonGraphicType.WallD3L2:
        case DungeonGraphicType.WallD3R2:


          return true;
        default:
          return false;
      }
    }

    private static void WarnUnknownKey(
        string compactKey,
        int playerX,
        int playerY,
        DungeonFacing facing)
    {
      string warnId =
          compactKey + "@" + playerX + "," + playerY + "," + facing;
      if (warnId == lastUnknownKeyWarned)
        return;

      lastUnknownKeyWarned = warnId;
      Debug.LogWarning(
          "ViewportPatternCatalog: unknown geometry key \""
              + compactKey
              + "\" at ("
              + playerX
              + ","
              + playerY
              + ") facing "
              + facing
              + ". Only Verified_1_2_South (key "
              + Verified12SouthGeometryKey
              + ") is registered. "
              + "Keeping authored Enabled wall composition (no pattern guess).");
    }

    private static ViewportPatternDefinition CreateVerified12South()
    {
      return new ViewportPatternDefinition
      {
        PatternId = Verified12SouthPatternId,
        GeometryKey = Verified12SouthGeometryKey,
        VisibleGraphics = new List<DungeonGraphicType>
        {
          DungeonGraphicType.WallF0L,
          DungeonGraphicType.WallF0R,
          DungeonGraphicType.WallF1L,
          DungeonGraphicType.WallF1R,
          DungeonGraphicType.WallF2L,
          DungeonGraphicType.WallF3L,
          DungeonGraphicType.WallF3R
        }
      };
    }
  }
}
