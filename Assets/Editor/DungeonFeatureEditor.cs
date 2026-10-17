using System.Collections.Generic;
using System.IO;
using DM.Dungeon;
using UnityEditor;
using UnityEngine;

public class DungeonFeatureEditor : EditorWindow
{
  private const string HallOfChampionsMapPath =
      "Assets/Data/Maps/DungeonMaster_Level00.json";
  private const string DungeonFeaturePlacementsPath =
      "Assets/Data/Features/DungeonFeaturePlacements.json";
  private const int HallOfChampionsLevel = 0;
  private const int MaxDungeonLevel = 13;
  private const string MossF1AssetName = "Moss_F1_56x19";
  private const string PuddleF1AssetPath =
      "Assets/Art/Ornaments/Puddle_F1.png";

  // Hall of Champions uses three random floor ornaments. The level-local
  // FloorOrnate table is Moss, Puddle, Floor Crack for ordinals 1..3.
  // Puddles therefore resolve from random floor ornament ordinal 2.
  private const int HallOfChampionsRandomFloorOrnamentCount = 3;
  private const int HallOfChampionsPuddleFloorOrnamentOrdinal = 2;
  private const int HallOfChampionsRandomOrnamentModulo = 30;
  private const int HallOfChampionsDungeonSeed = 99;

  // Shared ViewEdit preview pose. Dungeon Features reads these to follow
  // ViewEdit and writes X/Y when the user clicks a map tile.
  private const string ViewEditPreviewXKey =
      "ViewportLayoutEditor.PreviewX";
  private const string ViewEditPreviewYKey =
      "ViewportLayoutEditor.PreviewY";
  private const string ViewEditPreviewFacingKey =
      "ViewportLayoutEditor.PreviewFacing";
  private const string ViewEditPreviewLevelKey =
      "ViewportLayoutEditor.PreviewLevel";

  private const float BaseCellSize = 32f;
  private const int MaxMapZoomStep = 1;
  private const int MaximumDungeonTilesPerAxis = 32;
  private const string MapZoomPreferenceKey = "DungeonFeatureEditor.MapZoomOutStep";
  // Two wheel positions: normal 1x and fit a full 32x32 map in the viewport.
  private float mapZoom => GetZoomForStep(mapZoomStep);
  private float GetFitMapZoom()
  {
    // Reserve space for map coordinates, wall labels, scrollbars and header.
    float availableWidth = Mathf.Max(1f, position.width - 24f);
    float availableHeight = Mathf.Max(1f, position.height - 110f);
    float requiredWidth = LabelLeftMargin
        + Mathf.Max(MaximumDungeonTilesPerAxis, OutsideNameColumnX + 2) * BaseCellSize;
    float requiredHeight = LabelTopMargin + 2f * OutsideFeatureVerticalMargin
        + MaximumDungeonTilesPerAxis * BaseCellSize;
    return Mathf.Clamp(Mathf.Min(availableWidth / requiredWidth,
        availableHeight / requiredHeight), 0.01f, 1f);
  }

  private float GetZoomForStep(int step)
  {
    return step >= MaxMapZoomStep ? GetFitMapZoom() : 1f;
  }
  private float CellSize => BaseCellSize * mapZoom;
  private const float LabelLeftMargin = 28f;
  private const float LabelTopMargin = 20f;
  // Extra vertical canvas above/below the map for labels that are drawn
  // outside a wall face. This also keeps y=0 and y=31 labels fully visible
  // on 32-row dungeon levels.
  private const float OutsideFeatureVerticalMargin = 32f;
  // Virtual column just past the Hall of Champions map (x = 0..17).
  private const int OutsideNameColumnX = 18;

  private static readonly Color WallColor = new Color(0.22f, 0.22f, 0.22f);
  private static readonly Color FloorColor = new Color(0.78f, 0.78f, 0.78f);
  private static readonly Color CellBorderColor = new Color(0.08f, 0.08f, 0.08f);
  private static readonly Color SelectionFillColor =
      new Color(0.35f, 0.75f, 1f, 0.35f);
  private static readonly Color SelectionBorderColor =
      new Color(0.2f, 0.85f, 1f, 1f);
  private static readonly Color StairDownTileColor =
      new Color(0f, 1f, 0f, 1f);
  private static readonly Color ManaclesHighlightBackgroundColor =
      new Color(1f, 165f / 255f, 0f, 1f);
  private static readonly Color LordOrderOutsideMarkerColor =
      new Color(0.6f, 0f, 0.8f, 1f);

  // Champion mirror map marker: full-edge blue wall-face bar with a small yellow center.
  private static readonly Color ChampionMirrorLineColor =
      new Color(0.0f, 0.0f, 1.0f, 1f);
  private static readonly Color ChampionMirrorDotColor =
      new Color(1.0f, 0.9f, 0.0f, 1f);
  private static readonly Color ChampionNameColor =
      new Color(0f, 1f, 0f, 1f);
  private static readonly Color VerifiedChampionTileColor =
      new Color(0f, 1f, 0f, 1f);

  private enum WallSide
  {
    North,
    East,
    South,
    West
  }

  private sealed class ChampionMirrorMarker
  {
    public int X;
    public int Y;
    public WallSide Side;
    public string Champion;

    public ChampionMirrorMarker(
        int x,
        int y,
        WallSide side,
        string champion)
    {
      X = x;
      Y = y;
      Side = side;
      Champion = champion;
    }
  }

  // Hall of Champions mirror positions extracted from the original
  // direction-aware level data. Direction is the wall face on which the
  // champion portrait/mirror appears.
  private sealed class OriginalWallOrnamentMarker
  {
    public string Type;
    public int X;
    public int Y;
    public WallSide Side;
    public bool WallTilePlacement;

    public OriginalWallOrnamentMarker(
        string type,
        int x,
        int y,
        WallSide side,
        bool wallTilePlacement)
    {
      Type = type;
      X = x;
      Y = y;
      Side = side;
      WallTilePlacement = wallTilePlacement;
    }
  }

  // Every Hall of Champions wall ornament is drawn on the impassable grey
  // wall tile, on the face stored in the raw direction bits. Those faces are
  // the directions that are not a possible walk. The sensor wood ring at
  // (6,8) South is not a random face bit, so it is added explicitly.
  private OriginalWallOrnamentMarker[] resolvedWallOrnaments =
      new OriginalWallOrnamentMarker[0];
  private readonly HashSet<int> resolvedPuddleFloors = new HashSet<int>();
  private readonly HashSet<int> resolvedMossFloors = new HashSet<int>();

  // Read-only floor-item map layer sourced from all 14 levels of the
  // placement database. Never turn wall-linked inventory into floor loot.
  [System.Serializable]
  private sealed class FloorItemPlacementRecord
  {
    public string id;
    public int level;
    public int localX;
    public int localY;
    public string recordType;
    public string tileClass;
    public FloorThingRecord thing;
  }

  [System.Serializable]
  private sealed class FloorThingRecord
  {
    public int Type;
    public int Next;
  }

  [System.Serializable]
  private sealed class FloorItemPlacementDatabase
  {
    public FloorItemPlacementRecord[] placements;
  }

  private readonly Dictionary<int, List<FloorItemPlacementRecord>>
      resolvedFloorItems = new Dictionary<int, List<FloorItemPlacementRecord>>();
  private static readonly Color FloorItemMapColor = new Color(0.05f, 0.35f, 0.85f, 1f);
  private const string ShowFloorItemsPreferenceKey = "DungeonFeatureEditor.ShowFloorItems";
  private bool showFloorItems = true;
  private int inspectedFloorItemX = -1;
  private int inspectedFloorItemY = -1;

  private static bool IsFloorItemRecordType(string recordType)
  {
    switch (recordType)
    {
      case "Junk":
      case "Weapon":
      case "Armour":
      case "Potion":
      case "Scroll":
      case "Container":
        return true;
      default:
        return false;
    }
  }

  private void LoadFloorItemMapLayer()
  {
    resolvedFloorItems.Clear();
    if (!File.Exists(DungeonFeaturePlacementsPath))
    {
      Debug.LogError("Dungeon Features: Missing floor-item placements: "
          + DungeonFeaturePlacementsPath);
      return;
    }

    try
    {
      FloorItemPlacementDatabase database = JsonUtility.FromJson<FloorItemPlacementDatabase>(
          File.ReadAllText(DungeonFeaturePlacementsPath));
      if (database == null || database.placements == null)
      {
        Debug.LogError("Dungeon Features: Floor-item placements array is missing.");
        return;
      }

      foreach (FloorItemPlacementRecord entry in database.placements)
      {
        if (entry == null || entry.level != currentDungeonLevel
            || entry.tileClass != "Floor" || !IsFloorItemRecordType(entry.recordType))
          continue;
        if (!map.IsInside(entry.localX, entry.localY))
        {
          Debug.LogError("Dungeon Features: Floor item " + entry.id
              + " lies outside Level " + entry.level + " map at ("
              + entry.localX + "," + entry.localY + ").");
          continue;
        }
        int tile = PackFloorFeatureTile(entry.localX, entry.localY);
        if (!resolvedFloorItems.TryGetValue(tile, out List<FloorItemPlacementRecord> list))
        {
          list = new List<FloorItemPlacementRecord>();
          resolvedFloorItems.Add(tile, list);
        }
        list.Add(entry);
      }
    }
    catch (System.Exception ex)
    {
      resolvedFloorItems.Clear();
      Debug.LogError("Dungeon Features: Could not read floor-item database: " + ex);
    }
  }

  private void DrawFloorItemMapIcons(Rect mapRect)
  {
    if (!showFloorItems)
      return;
    foreach (var pair in resolvedFloorItems)
    {
      List<FloorItemPlacementRecord> list = pair.Value;
      if (list == null || list.Count == 0)
        continue;
      int x = list[0].localX;
      int y = list[0].localY;

      if (list.Count == 1)
      {
        Texture2D itemIcon = GetFloorItemInventoryMapIcon(list[0]);
        if (itemIcon != null)
        {
          float iconMargin = 1f * mapZoom;
          float iconSize = Mathf.Max(1f, CellSize - iconMargin * 2f);
          Rect iconRect = new Rect(
              mapRect.x + x * CellSize + iconMargin,
              mapRect.y + y * CellSize + iconMargin,
              iconSize,
              iconSize);
          GUI.DrawTexture(iconRect, itemIcon, ScaleMode.StretchToFill, true);
          continue;
        }
      }

      Rect marker = new Rect(mapRect.x + x * CellSize + CellSize * 0.56f,
          mapRect.y + y * CellSize + CellSize * 0.02f,
          CellSize * 0.42f, CellSize * 0.42f);
      EditorGUI.DrawRect(marker, FloorItemMapColor);
      GUIStyle style = new GUIStyle(EditorStyles.whiteMiniLabel)
      {
        alignment = TextAnchor.MiddleCenter,
        fontStyle = FontStyle.Bold,
        fontSize = Mathf.Max(7, Mathf.RoundToInt(10 * mapZoom))
      };
      GUI.Label(marker, list.Count.ToString(), style);
    }
  }

  private Texture2D GetFloorItemInventoryMapIcon(FloorItemPlacementRecord entry)
  {
    if (entry == null)
      return null;

    int type = entry.thing != null ? entry.thing.Type : -1;
    string cacheKey = entry.recordType + ":" + type.ToString();
    if (floorItemInventoryIconCache.TryGetValue(cacheKey, out Texture2D cached))
      return cached;

    string decodedName = DecodeFloorItemName(entry.recordType, type);
    Texture2D resolved = ResolveInventoryItemIcon(decodedName, entry.recordType);
    if (resolved == null)
      Debug.LogError("Dungeon Features: Missing 16x16 inventory map icon for '"
          + decodedName + "' (" + entry.recordType + " Type " + type
          + ", record " + entry.id + ", level " + entry.level
          + ", tile (" + entry.localX + "," + entry.localY
          + ")). Check the exact asset filename in Assets; no replacement art is used.");
    else if (resolved.width != 16 || resolved.height != 16)
    {
      Debug.LogError("Dungeon Features: Inventory map icon for '" + decodedName
          + "' must be 16x16, got " + resolved.width + "x" + resolved.height
          + " (asset " + resolved.name + ").");
      resolved = null;
    }
    floorItemInventoryIconCache[cacheKey] = resolved;
    return resolved;
  }

  private Texture2D ResolveInventoryItemIcon(string decodedName, string recordType)
  {
    if (string.IsNullOrEmpty(decodedName))
      return null;

    // Verified art filename for Junk Type 1. The displayed item name remains
    // Waterskin; the inventory sprite asset is named Waterskin_empty_16x16.
    // Strict lookup: do not substitute any other sprite when it is absent.
    if (string.Equals(recordType, "Junk", System.StringComparison.OrdinalIgnoreCase)
        && string.Equals(decodedName, "Waterskin", System.StringComparison.OrdinalIgnoreCase))
      return FindExactTextureByName("Waterskin_empty_16x16");

    // Verified art filename in Assets/MISC.
    if (string.Equals(recordType, "Scroll", System.StringComparison.OrdinalIgnoreCase)
        && string.Equals(decodedName, "Scroll", System.StringComparison.OrdinalIgnoreCase))
      return FindExactTextureByName("Scroll_16x16");

    string sanitized = SanitizeItemNameForAsset(decodedName);
    string underscored = decodedName.Replace(" ", "_");
    string sanitizedUnderscored = SanitizeItemNameForAsset(underscored);

    List<string> candidates = new List<string>();
    AddUniqueCandidate(candidates, decodedName + "_16x16");
    AddUniqueCandidate(candidates, underscored + "_16x16");
    AddUniqueCandidate(candidates, sanitized + "_16x16");
    AddUniqueCandidate(candidates, sanitizedUnderscored + "_16x16");
    AddUniqueCandidate(candidates, decodedName);
    AddUniqueCandidate(candidates, underscored);
    AddUniqueCandidate(candidates, sanitized);
    AddUniqueCandidate(candidates, sanitizedUnderscored);

    if (string.Equals(recordType, "Container", System.StringComparison.OrdinalIgnoreCase))
    {
      AddUniqueCandidate(candidates, "Chest_16x16");
      AddUniqueCandidate(candidates, "Chest");
    }
    else if (string.Equals(recordType, "Scroll", System.StringComparison.OrdinalIgnoreCase))
    {
      AddUniqueCandidate(candidates, "Scroll_16x16");
      AddUniqueCandidate(candidates, "Scroll");
    }

    for (int i = 0; i < candidates.Count; i++)
    {
      Texture2D texture = FindExactTextureByName(candidates[i]);
      if (texture != null)
        return texture;
    }

    return null;
  }

  private static void AddUniqueCandidate(List<string> candidates, string candidate)
  {
    if (string.IsNullOrEmpty(candidate))
      return;
    for (int i = 0; i < candidates.Count; i++)
    {
      if (string.Equals(candidates[i], candidate, System.StringComparison.OrdinalIgnoreCase))
        return;
    }
    candidates.Add(candidate);
  }

  private static string SanitizeItemNameForAsset(string value)
  {
    if (string.IsNullOrEmpty(value))
      return string.Empty;

    System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
    bool previousUnderscore = false;
    for (int i = 0; i < value.Length; i++)
    {
      char c = value[i];
      if (char.IsLetterOrDigit(c))
      {
        builder.Append(c);
        previousUnderscore = false;
      }
      else if (char.IsWhiteSpace(c) || c == '-' || c == (char)39 || c == '(' || c == ')' || c == '/')
      {
        if (!previousUnderscore)
        {
          builder.Append('_');
          previousUnderscore = true;
        }
      }
    }

    return builder.ToString().Trim('_');
  }


  // Exact numeric item enums from LocalFuri CSBwin Objects.h.
  // Never infer names from a numeric type without its record category.
  private static string DecodeFloorItemName(string recordType, int type)
  {
    switch (recordType)
    {
      case "Weapon":
        switch (type)
        {
          case 0: return "Eye Of Time";
          case 1: return "Storm Ring";
          case 2: return "Torch";
          case 3: return "Flamitt";
          case 4: return "Staff Of Claws";
          case 5: return "Storm";
          case 6: return "Ra Blade";
          case 7: return "The Firestaff A";
          case 8: return "Dagger";
          case 9: return "Falchion";
          case 10: return "Sword";
          case 11: return "Rapier";
          case 12: return "Biter";
          case 13: return "Samurai Sword";
          case 14: return "Side Splitter";
          case 15: return "Diamond Edge";
          case 16: return "Vorpal Blade";
          case 17: return "Dragon Fang";
          case 18: return "Axe";
          case 19: return "Executioner";
          case 20: return "Mace";
          case 21: return "Mace Of Order";
          case 22: return "Morningstar";
          case 23: return "Club";
          case 24: return "Stone Club";
          case 25: return "Claw Bow";
          case 26: return "Crossbow";
          case 27: return "Arrow";
          case 28: return "Slayer";
          case 29: return "Sling";
          case 30: return "Rock";
          case 31: return "Poison Dart";
          case 32: return "Throwing Star";
          case 33: return "Stick";
          case 34: return "Staff";
          case 35: return "Wand";
          case 36: return "Teo Wand";
          case 37: return "Yew Staff";
          case 38: return "Staff Of Irra";
          case 39: return "Cross Of Neta";
          case 40: return "Serpent Staff";
          case 41: return "Dragon Spit";
          case 42: return "Sceptre Of Lyf";
          case 43: return "Horn Of Fear";
          case 44: return "Speedbow";
          case 45: return "The Firestaff B";
        }
        break;
      case "Armour":
        switch (type)
        {
          case 0: return "Cape";
          case 1: return "Cloak Of Night";
          case 2: return "Tattered Pants";
          case 3: return "Sandals";
          case 4: return "Leather Boots";
          case 5: return "Tattered Shirt";
          case 6: return "Robe";
          case 7: return "Fine Robe A";
          case 8: return "Fine Robe B";
          case 9: return "Kirtle";
          case 10: return "Silk Shirt";
          case 11: return "Tabard";
          case 12: return "Gunna";
          case 13: return "Elven Doublet";
          case 14: return "Elven Huke";
          case 15: return "Elven Boots";
          case 16: return "Leather Jerkin";
          case 17: return "Leather Pants";
          case 18: return "Suede Boots";
          case 19: return "Blue Pants";
          case 20: return "Tunic";
          case 21: return "Ghi";
          case 22: return "Ghi Trousers";
          case 23: return "Calista";
          case 24: return "Crown Of Nerra";
          case 25: return "Berserker Helm";
          case 26: return "Helmet";
          case 27: return "Basinet";
          case 28: return "Neta Shield";
          case 29: return "Crystal Shield";
          case 30: return "Wooden Shield";
          case 31: return "Small Shield";
          case 32: return "Mail Aketon";
          case 33: return "Leg Mail";
          case 34: return "Mithral Aketon";
          case 35: return "Mithral Mail";
          case 36: return "Casque NCoif";
          case 37: return "Hosen";
          case 38: return "Armet";
          case 39: return "Torso Plate";
          case 40: return "Leg Plate";
          case 41: return "Foot Plate";
          case 42: return "Sar Shield";
          case 43: return "Helm Of Ra";
          case 44: return "Plate Of Ra";
          case 45: return "Poleyn Of Ra";
          case 46: return "Greave Of Ra";
          case 47: return "Shield Of Ra";
          case 48: return "Dragon Helm";
          case 49: return "Dragon Plate";
          case 50: return "Dragon Poleyn";
          case 51: return "Dragon Greave";
          case 52: return "Dragon Shield";
          case 53: return "Dexhelm";
          case 54: return "Flamebain";
          case 55: return "Power Towers";
          case 56: return "Boots Of Speed";
          case 57: return "Halter";
        }
        break;
      case "Junk":
        switch (type)
        {
          case 0: return "Compass";
          case 1: return "Waterskin";
          case 2: return "Jewel Symal";
          case 3: return "Illumulet";
          case 4: return "Ashes";
          case 5: return "Bones Of Hero";
          case 6: return "Sar Coin";
          case 7: return "Silver Coin";
          case 8: return "Gor Coin";
          case 9: return "Iron Key";
          case 10: return "Key Of B";
          case 11: return "Solid Key";
          case 12: return "Square Key";
          case 13: return "Tourquoise Key";
          case 14: return "Cross Key";
          case 15: return "Onyx Key";
          case 16: return "Skeleton Key";
          case 17: return "Gold Key";
          case 18: return "Winged Key";
          case 19: return "Topaz Key";
          case 20: return "Sapphire Key";
          case 21: return "Emerald Key";
          case 22: return "Ruby Key";
          case 23: return "Ra Key";
          case 24: return "Master Key";
          case 25: return "Boulder";
          case 26: return "Blue Gem";
          case 27: return "Orange Gem";
          case 28: return "Green Gem";
          case 29: return "Apple";
          case 30: return "Corn";
          case 31: return "Bread";
          case 32: return "Cheese";
          case 33: return "Screamer Slice";
          case 34: return "Worm Round";
          case 35: return "Shank";
          case 36: return "Dragon Steak";
          case 37: return "Gem Of Ages";
          case 38: return "Ekkhard Cross";
          case 39: return "Moonstone";
          case 40: return "The Hellion";
          case 41: return "Pendant Feral";
          case 42: return "Magical Box A";
          case 43: return "Magical Box B";
          case 44: return "Mirror Of Dawn";
          case 45: return "Rope";
          case 46: return "Rabbits Foot";
          case 47: return "Corbum";
          case 48: return "Choker";
          case 49: return "Lock Picks";
          case 50: return "Magnifier";
          case 51: return "Zokathra Spell";
          case 52: return "Bones";
        }
        break;
      case "Potion":
        switch (type)
        {
          case 0: return "Mon Potion A";
          case 1: return "Um Potion";
          case 2: return "Des Potion";
          case 3: return "Ven Potion";
          case 4: return "Sar Potion";
          case 5: return "Zo Potion";
          case 6: return "Ros Potion";
          case 7: return "Ku Potion";
          case 8: return "Dane Potion";
          case 9: return "Neta Potion";
          case 10: return "Anti Venin";
          case 11: return "Mon Potion B";
          case 12: return "Ya Potion";
          case 13: return "EEPotion";
          case 14: return "Vi Potion";
          case 15: return "Water Flask";
          case 16: return "Kath Bomb";
          case 17: return "Pew Bomb";
          case 18: return "Ra Bomb";
          case 19: return "Ful Bomb";
          case 20: return "Empty Flask";
        }
        break;
      case "Scroll":
        switch (type)
        {
          case 0: return "Scroll";
        }
        break;
      case "Container":
        switch (type)
        {
          case 0: return "Chest";
        }
        break;
    }
    return null;
  }

  private List<FloorItemPlacementRecord> GetInspectedFloorItems()
  {
    if (inspectedFloorItemX < 0 || inspectedFloorItemY < 0)
      return null;

    resolvedFloorItems.TryGetValue(
        PackFloorFeatureTile(inspectedFloorItemX, inspectedFloorItemY),
        out List<FloorItemPlacementRecord> records);
    return records;
  }

  private static string GetFloorItemDisplayName(FloorItemPlacementRecord entry)
  {
    if (entry == null)
      return string.Empty;

    bool singleSubtype = entry.recordType == "Scroll"
        || entry.recordType == "Container";
    string label = (entry.thing != null || singleSubtype)
        ? DecodeFloorItemName(entry.recordType,
            singleSubtype ? 0 : entry.thing.Type)
        : null;
    if (!string.IsNullOrEmpty(label))
      return label;

    string type = entry.thing != null
        ? entry.thing.Type.ToString() : "missing";
    return entry.recordType + " (Type " + type + ")";
  }


  // Compact one-line tile summary. The tooltip retains the complete list
  // without pushing the map down or crowding its cells.
  private GUIContent GetSelectedFloorItemsSummary()
  {
    List<FloorItemPlacementRecord> records = GetInspectedFloorItems();
    if (records == null || records.Count == 0)
      return GUIContent.none;

    var brief = new System.Text.StringBuilder();
    brief.Append(" | <color=#00FF00>(").Append(inspectedFloorItemX).Append(",")
        .Append(inspectedFloorItemY).Append(")</color>: ");
    var tooltip = new System.Text.StringBuilder();
    tooltip.Append("Floor items at (").Append(inspectedFloorItemX)
        .Append(",").Append(inspectedFloorItemY).Append(") — ")
        .Append(records.Count).Append(" record(s)");
    for (int i = 0; i < records.Count; i++)
    {
      FloorItemPlacementRecord entry = records[i];
      string label = GetFloorItemDisplayName(entry);
      string description = label + " [" + entry.id + "]";
      tooltip.Append("\n").Append(i + 1).Append(". ").Append(description);
      if (i < 2)
      {
        if (i > 0)
          brief.Append(", ");
        brief.Append("<color=#00FF00>").Append(label)
            .Append("</color> [").Append(entry.id).Append("]");
      }
    }
    if (records.Count > 2)
      brief.Append(" +").Append(records.Count - 2).Append(" more");
    return new GUIContent(brief.ToString(), tooltip.ToString());
  }


  // Verification restarts for each champion: visual equipment, pickup,
  // floor drop, and throwing must all be tested before marking green.
  private static readonly string[] VerifiedChampionEquipmentNames =
  {
  };

  private static readonly ChampionMirrorMarker[] ChampionMirrorMarkers =
  {
    new ChampionMirrorMarker(10, 4, WallSide.North, "Iaido"),
    new ChampionMirrorMarker(10, 5, WallSide.South, "Zed"),
    new ChampionMirrorMarker(14, 3, WallSide.North, "Chani"),
    new ChampionMirrorMarker(15, 4, WallSide.East, "Hawk"),
    new ChampionMirrorMarker(14, 6, WallSide.South, "Boris"),
    new ChampionMirrorMarker(16, 8, WallSide.North, "Alex"),
    new ChampionMirrorMarker(17, 9, WallSide.South, "Nabi"),
    new ChampionMirrorMarker(16, 14, WallSide.South, "Hissssa"),
    new ChampionMirrorMarker(16, 17, WallSide.North, "Gothmog"),
    new ChampionMirrorMarker(14, 12, WallSide.East, "Sonja"),
    new ChampionMirrorMarker(13, 12, WallSide.North, "Leyla"),
    new ChampionMirrorMarker(13, 14, WallSide.East, "Mophus"),
    new ChampionMirrorMarker(12, 13, WallSide.West, "Wuuf"),
    new ChampionMirrorMarker(11, 15, WallSide.South, "Stamm"),
    new ChampionMirrorMarker(7, 16, WallSide.South, "Azizi"),
    new ChampionMirrorMarker(8, 15, WallSide.North, "Leif"),
    new ChampionMirrorMarker(9, 13, WallSide.East, "Tiggy"),
    new ChampionMirrorMarker(7, 13, WallSide.South, "Wu Tse"),
    new ChampionMirrorMarker(6, 13, WallSide.West, "Daroou"),
    new ChampionMirrorMarker(7, 9, WallSide.North, "Halk"),
    new ChampionMirrorMarker(9, 9, WallSide.South, "Syra"),
    new ChampionMirrorMarker(11, 10, WallSide.South, "Gando"),
    new ChampionMirrorMarker(12, 9, WallSide.North, "Linflas"),
    new ChampionMirrorMarker(9, 7, WallSide.West, "Elija")
  };

  private DungeonMap map;
  private string mapLoadError;
  private Vector2 mapScroll;
  private int mapZoomStep = 0;
  private int selectedX = 1;
  private int selectedY = 2;
  private DungeonFacing selectedFacing = DungeonFacing.North;
  private int currentDungeonLevel = HallOfChampionsLevel;

  private bool ornamentTexturesResolved;
  private readonly Dictionary<string, Texture2D> floorItemInventoryIconCache =
      new Dictionary<string, Texture2D>();
  private Texture2D viAltarMapIcon;
  private Texture2D hookMapIcon;
  private Texture2D woodRingMapIcon;
  private Texture2D slimeMapIcon;
  private Texture2D grateMapIcon;
  private Texture2D manaclesMapIcon;
  private Texture2D fullTorchHolderMapIcon;
  private Texture2D puddleMapIcon;
  private Texture2D mossMapIcon;
  private Texture2D stairDownMapIcon;
  // Report missing icons once per editor-window lifetime, not every repaint.
  private readonly HashSet<string> missingMapIconErrors =
      new HashSet<string>();

  [MenuItem("Tools/Dungeon Feature Editor &f")]
  public static void Open()
  {
    DungeonFeatureEditor window =
        GetWindow<DungeonFeatureEditor>("Dungeon Features");
    window.minSize = new Vector2(1200f, 800f);
    window.Focus();
  }

  private void OnEnable()
  {
    showFloorItems = true; // Floor-item badges are always visible.
    mapZoomStep = Mathf.Clamp(EditorPrefs.GetInt(MapZoomPreferenceKey, 0), 0, MaxMapZoomStep);
    ornamentTexturesResolved = false;
    floorItemInventoryIconCache.Clear();
    missingMapIconErrors.Clear();
    viAltarMapIcon = null;
    hookMapIcon = null;
    woodRingMapIcon = null;
    slimeMapIcon = null;
    grateMapIcon = null;
    manaclesMapIcon = null;
    fullTorchHolderMapIcon = null;
    puddleMapIcon = null;
    mossMapIcon = null;
    stairDownMapIcon = null;

    currentDungeonLevel = Mathf.Clamp(
        EditorPrefs.GetInt(
            ViewEditPreviewLevelKey,
            HallOfChampionsLevel),
        HallOfChampionsLevel,
        MaxDungeonLevel);

    LoadMapForLevel(currentDungeonLevel);
    SyncSelectionFromViewEdit();
  }

  private void OnInspectorUpdate()
  {
    bool changed = SyncLevelFromViewEdit();
    changed |= SyncSelectionFromViewEdit();
    if (changed)
      Repaint();
  }

  private static string GetMapPathForLevel(int level)
  {
    if (level == HallOfChampionsLevel)
      return HallOfChampionsMapPath;

    return $"Assets/Data/Maps/DungeonMaster_Level{level:00}.json";
  }

  private bool SyncLevelFromViewEdit()
  {
    int requestedLevel = Mathf.Clamp(
        EditorPrefs.GetInt(
            ViewEditPreviewLevelKey,
            currentDungeonLevel),
        HallOfChampionsLevel,
        MaxDungeonLevel);

    if (requestedLevel == currentDungeonLevel)
      return false;

    if (!LoadMapForLevel(requestedLevel))
      return false;

    return true;
  }

  private bool LoadMapForLevel(int level)
  {
    string mapPath = GetMapPathForLevel(level);
    if (!File.Exists(mapPath))
    {
      mapLoadError = "Map not found at " + mapPath;
      return false;
    }

    DungeonMap previousMap = map;
    int previousLevel = currentDungeonLevel;

    try
    {
      string json = File.ReadAllText(mapPath);
      DungeonMap loadedMap = DungeonMap.LoadFromJsonText(json);

      map = loadedMap;
      currentDungeonLevel = level;
      mapLoadError = null;

      resolvedWallOrnaments = new OriginalWallOrnamentMarker[0];
      resolvedPuddleFloors.Clear();
      resolvedMossFloors.Clear();
      LoadFloorItemMapLayer();
      inspectedFloorItemX = -1;
      inspectedFloorItemY = -1;

      if (!LoadResolvedFeaturesFromDatabase())
      {
        if (level == HallOfChampionsLevel)
        {
          resolvedWallOrnaments = BuildGreyWallOrnaments(map);
          BuildHallOfChampionsPuddleFloors(map, resolvedPuddleFloors);
        }
      }

      // The shared pose is applied immediately after this load. Use the map's
      // own start only as a safe temporary value while the level changes.
      selectedX = map.StartX;
      selectedY = map.StartY;
      selectedFacing = map.StartFacing;
      return true;
    }
    catch (System.Exception ex)
    {
      map = previousMap;
      currentDungeonLevel = previousLevel;
      resolvedWallOrnaments = new OriginalWallOrnamentMarker[0];
      resolvedPuddleFloors.Clear();
      resolvedMossFloors.Clear();
      resolvedFloorItems.Clear();
      mapLoadError = ex.Message;
      Debug.LogWarning(
          "Dungeon Features: Could not load level " + level
          + " from " + mapPath + ": " + ex.Message);
      return false;
    }
  }

  private bool SyncSelectionFromViewEdit()
  {
    if (map == null
        || !EditorPrefs.HasKey(ViewEditPreviewXKey)
        || !EditorPrefs.HasKey(ViewEditPreviewYKey))
    {
      return false;
    }

    int viewEditX = EditorPrefs.GetInt(ViewEditPreviewXKey, selectedX);
    int viewEditY = EditorPrefs.GetInt(ViewEditPreviewYKey, selectedY);
    DungeonFacing viewEditFacing = (DungeonFacing)EditorPrefs.GetInt(
        ViewEditPreviewFacingKey,
        (int)selectedFacing);

    if (!map.IsInside(viewEditX, viewEditY))
      return false;

    bool changed =
        selectedX != viewEditX
        || selectedY != viewEditY
        || selectedFacing != viewEditFacing;

    if (!changed)
      return false;

    selectedX = viewEditX;
    selectedY = viewEditY;
    selectedFacing = viewEditFacing;
    return true;
  }

  private bool HandleRightMouseWindowSwitch()
  {
    Event current = Event.current;
    if (current == null
        || current.type != EventType.MouseDown
        || current.button != 1)
    {
      return false;
    }

    current.Use();
    EditorApplication.delayCall += ViewportLayoutEditor.Open;
    return true;
  }

  private void OnGUI()
  {
    if (HandleRightMouseWindowSwitch())
      return;

    Event current = Event.current;
    if (current.type == EventType.KeyDown
        && current.keyCode == KeyCode.Escape)
    {
      current.Use();
      Close();
      return;
    }

    if (map == null)
    {
      EditorGUILayout.HelpBox(
          string.IsNullOrEmpty(mapLoadError)
              ? "Could not load Hall of Champions map."
              : mapLoadError,
          MessageType.Error);
      return;
    }

    GUIStyle poseStyle = new GUIStyle(EditorStyles.boldLabel)
    {
      fontSize = 14
    };

    EditorGUILayout.BeginHorizontal();
    GUILayout.Label("Map", GUILayout.Width(28f));

    if (GUILayout.Button("<", GUILayout.Width(24f)))
      SetDungeonLevelFromUi(currentDungeonLevel - 1);

    EditorGUI.BeginChangeCheck();
    int requestedLevel = EditorGUILayout.DelayedIntField(
        currentDungeonLevel,
        GUILayout.Width(34f));
    if (EditorGUI.EndChangeCheck())
    {
      SetDungeonLevelFromUi(requestedLevel);
      GUI.FocusControl(null);
    }

    if (GUILayout.Button(">", GUILayout.Width(24f)))
      SetDungeonLevelFromUi(currentDungeonLevel + 1);

    GUILayout.Space(10f);
    GUILayout.Label(
        $"{selectedX} X / Y {selectedY} {selectedFacing}",
        poseStyle,
        GUILayout.ExpandWidth(false),
        GUILayout.Height(20f));
    GUIContent floorSummary = GetSelectedFloorItemsSummary();
    if (!string.IsNullOrEmpty(floorSummary.text))
    {
      GUIStyle coloredItemSummaryStyle = new GUIStyle(EditorStyles.label)
      {
        richText = true
      };
      GUILayout.Label(floorSummary, coloredItemSummaryStyle,
          GUILayout.ExpandWidth(false), GUILayout.Height(20f));
    }
    GUILayout.FlexibleSpace();
    EditorGUILayout.EndHorizontal();

    DrawMapGrid();
  }

  private void SetDungeonLevelFromUi(int requestedLevel)
  {
    requestedLevel = Mathf.Clamp(
        requestedLevel,
        HallOfChampionsLevel,
        MaxDungeonLevel);

    if (requestedLevel == currentDungeonLevel)
      return;

    if (!LoadMapForLevel(requestedLevel) || map == null)
      return;

    // LoadMapForLevel seeds the selection from the new map's authored start.
    // Publish the whole pose so ViewEdit can switch level and land on the same
    // tile/facing instead of retaining coordinates from the previous map.
    EditorPrefs.SetInt(ViewEditPreviewLevelKey, currentDungeonLevel);
    EditorPrefs.SetInt(ViewEditPreviewXKey, selectedX);
    EditorPrefs.SetInt(ViewEditPreviewYKey, selectedY);
    EditorPrefs.SetInt(
        ViewEditPreviewFacingKey,
        (int)selectedFacing);

    Repaint();
  }

  private void DrawMapGrid()
  {
    float mapPixelWidth = map.Width * CellSize;
    float mapPixelHeight = map.Height * CellSize;
    List<FloorItemPlacementRecord> previewRecords = GetInspectedFloorItems();
    bool showMultiItemPreview = previewRecords != null && previewRecords.Count > 1;
    float contentWidth =
        LabelLeftMargin * mapZoom
        + Mathf.Max(map.Width, OutsideNameColumnX + 2) * CellSize;
    float contentHeight =
        LabelTopMargin * mapZoom
        + OutsideFeatureVerticalMargin * mapZoom
        + mapPixelHeight
        + OutsideFeatureVerticalMargin * mapZoom;

    mapScroll = EditorGUILayout.BeginScrollView(mapScroll);

    Rect contentRect = GUILayoutUtility.GetRect(
        contentWidth,
        contentHeight,
        GUILayout.Width(contentWidth),
        GUILayout.Height(contentHeight),
        GUILayout.ExpandWidth(false),
        GUILayout.ExpandHeight(false)
    );

    Rect mapRect = new Rect(
        contentRect.x + LabelLeftMargin * mapZoom,
        contentRect.y + (LabelTopMargin + OutsideFeatureVerticalMargin) * mapZoom,
        mapPixelWidth,
        mapPixelHeight
    );

    // Consume wheel events only inside the map content; elsewhere the window
    // keeps its normal scrolling behavior.
    HandleMapZoom(contentRect);
    DrawAxisLabels(mapRect);

    for (int y = 0; y < map.Height; y++)
    {
      for (int x = 0; x < map.Width; x++)
      {
        Rect cellRect = new Rect(
            mapRect.x + x * CellSize,
            mapRect.y + y * CellSize,
            CellSize,
            CellSize
        );

        DrawCell(
            cellRect,
            map.GetTile(x, y),
            x == selectedX && y == selectedY);
      }
    }

    DrawStairDownIcons(mapRect);
    DrawPuddleFloorIcons(mapRect);
    DrawMossFloorIcons(mapRect);
    DrawFloorItemMapIcons(mapRect);
    DrawOriginalWallOrnamentIcons(mapRect);
    DrawVerifiedChampionTileHighlights(mapRect);
    DrawChampionMirrorMarkers(mapRect);
    HandleGridClick(mapRect);
    // Draw last so the item popup remains above other map features.
    if (showMultiItemPreview)
      DrawSelectedFloorItemsPopup(mapRect, previewRecords);

    EditorGUILayout.EndScrollView();
  }

  // Read-only popup anchored to the selected multi-item map tile.
  // Inventory art stays 16x16; the popup displays it at 2x (32x32).
  private Rect GetSelectedFloorItemPopupRect(Rect mapRect, int itemCount)
  {
    const int columns = 4;
    float unit = mapZoom;
    float iconSize = 32f * unit;
    float gap = 4f * unit;
    float padding = 7f * unit;
    float headingHeight = 15f * unit;
    int usedColumns = Mathf.Min(columns, itemCount);
    int rows = (itemCount + columns - 1) / columns;
    float popupWidth = padding * 2f + usedColumns * iconSize
        + Mathf.Max(0, usedColumns - 1) * gap;
    float popupHeight = padding * 2f + headingHeight
        + rows * iconSize + Mathf.Max(0, rows - 1) * gap;
    float centerX = mapRect.x + (inspectedFloorItemX + 0.5f) * CellSize;
    float centerY = mapRect.y + (inspectedFloorItemY + 0.5f) * CellSize;
    float x = centerX + CellSize * 0.6f;
    if (x + popupWidth > mapRect.xMax)
      x = centerX - CellSize * 0.6f - popupWidth;
    x = Mathf.Clamp(x, mapRect.x, Mathf.Max(mapRect.x, mapRect.xMax - popupWidth));
    float y = Mathf.Clamp(centerY - popupHeight * 0.5f,
        mapRect.y, Mathf.Max(mapRect.y, mapRect.yMax - popupHeight));
    return new Rect(x, y, popupWidth, popupHeight);
  }

  private void DrawSelectedFloorItemsPopup(Rect mapRect,
      List<FloorItemPlacementRecord> records)
  {
    if (records == null || records.Count <= 1)
      return;

    Rect popup = GetSelectedFloorItemPopupRect(mapRect, records.Count);
    Vector3 tileCenter = new Vector3(
        mapRect.x + (inspectedFloorItemX + 0.5f) * CellSize,
        mapRect.y + (inspectedFloorItemY + 0.5f) * CellSize, 0f);
    Vector3 popupEnd = new Vector3(
        tileCenter.x < popup.center.x ? popup.xMin : popup.xMax,
        popup.center.y, 0f);
    Handles.BeginGUI();
    Color previous = Handles.color;
    Handles.color = Color.cyan;
    Handles.DrawAAPolyLine(1f, tileCenter, popupEnd);
    Handles.color = previous;
    Handles.EndGUI();

    GUI.Box(popup, GUIContent.none);
    float unit = mapZoom;
    float padding = 7f * unit;
    float headingHeight = 15f * unit;
    float iconSize = 32f * unit;
    float gap = 4f * unit;
    GUIStyle heading = new GUIStyle(EditorStyles.miniBoldLabel)
    {
      alignment = TextAnchor.MiddleLeft
    };
    GUI.Label(new Rect(popup.x + padding, popup.y + padding,
        popup.width - 2f * padding, headingHeight),
        "(" + inspectedFloorItemX + "," + inspectedFloorItemY
        + ")  " + records.Count + " items", heading);
    for (int i = 0; i < records.Count; i++)
    {
      FloorItemPlacementRecord entry = records[i];
      int column = i % 4;
      int row = i / 4;
      Rect rect = new Rect(
          popup.x + padding + column * (iconSize + gap),
          popup.y + padding + headingHeight + row * (iconSize + gap),
          iconSize, iconSize);
      Texture2D texture = GetFloorItemInventoryMapIcon(entry);
      if (texture != null)
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
      // A missing asset remains an error in GetFloorItemInventoryMapIcon;
      // do not substitute another image.
      GUI.Label(rect, new GUIContent(string.Empty,
          GetFloorItemDisplayName(entry) + " [" + entry.id + "]"));
    }
  }

  private void HandleMapZoom(Rect contentRect)
  {
    Event e = Event.current;
    if (e.type != EventType.ScrollWheel || !contentRect.Contains(e.mousePosition))
      return;

    // Wheel down zooms out; wheel up zooms back toward normal 1x size.
    int direction = e.delta.y > 0f ? 1 : -1;
    int nextZoomStep = mapZoomStep;
    float currentZoom = mapZoom;
    // Only two possible zoom positions: 1x and fit-to-window.
    nextZoomStep = Mathf.Clamp(nextZoomStep + direction, 0, MaxMapZoomStep);
    float nextZoom = GetZoomForStep(nextZoomStep);
    if (Mathf.Abs(nextZoom - currentZoom) <= 0.0001f)
    {
      e.Use();
      return;
    }

    float scale = nextZoom / currentZoom;
    // Preserve the point under the pointer in the scroll view while zooming.
    Vector2 anchor = e.mousePosition - contentRect.position;
    mapScroll += anchor * (scale - 1f);
    mapScroll.x = Mathf.Max(0f, mapScroll.x);
    mapScroll.y = Mathf.Max(0f, mapScroll.y);
    // Returning from Fit 32x32 to 100% starts at the first map row,
    // like returning to the top of a worksheet in Excel.
    if (mapZoomStep == MaxMapZoomStep && nextZoomStep == 0)
      mapScroll = Vector2.zero;
    mapZoomStep = nextZoomStep;
    EditorPrefs.SetInt(MapZoomPreferenceKey, mapZoomStep);
    e.Use();
    GUI.changed = true;
    Repaint();
  }

  private void DrawAxisLabels(Rect mapRect)
  {
    GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
    {
      alignment = TextAnchor.MiddleCenter,
      fontSize = Mathf.Max(1, Mathf.RoundToInt(10f * mapZoom)),
      clipping = TextClipping.Overflow
    };

    GUIStyle yLabelStyle = new GUIStyle(labelStyle)
    {
      alignment = TextAnchor.MiddleRight
    };

    float xLabelWidth = 20f * mapZoom;

    for (int x = 0; x < map.Width; x++)
    {
      float columnCenterX =
          mapRect.x + x * CellSize + CellSize * 0.5f;

      Rect xLabelRect = new Rect(
          columnCenterX - xLabelWidth * 0.5f,
          mapRect.y - LabelTopMargin * mapZoom,
          xLabelWidth,
          LabelTopMargin * mapZoom
      );

      GUI.Label(xLabelRect, x.ToString(), labelStyle);
    }

    for (int y = 0; y < map.Height; y++)
    {
      Rect yLabelRect = new Rect(
          mapRect.x - LabelLeftMargin * mapZoom,
          mapRect.y + y * CellSize,
          (LabelLeftMargin - 2f) * mapZoom,
          CellSize
      );

      GUI.Label(yLabelRect, y.ToString(), yLabelStyle);
    }
  }

  private static void DrawCell(
      Rect cellRect,
      DungeonTile tile,
      bool isSelected)
  {
    EditorGUI.DrawRect(cellRect, CellBorderColor);

    Rect fillRect = new Rect(
        cellRect.x + 1f,
        cellRect.y + 1f,
        cellRect.width - 2f,
        cellRect.height - 2f
    );

    Color fillColor = tile.Type == DungeonTileType.Wall
        ? WallColor
        : FloorColor;

    EditorGUI.DrawRect(fillRect, fillColor);

    if (!isSelected)
      return;

    EditorGUI.DrawRect(fillRect, SelectionFillColor);
    EditorGUI.DrawRect(
        new Rect(cellRect.x, cellRect.y, cellRect.width, 2f),
        SelectionBorderColor);
    EditorGUI.DrawRect(
        new Rect(
            cellRect.x,
            cellRect.yMax - 2f,
            cellRect.width,
            2f),
        SelectionBorderColor);
    EditorGUI.DrawRect(
        new Rect(cellRect.x, cellRect.y, 2f, cellRect.height),
        SelectionBorderColor);
    EditorGUI.DrawRect(
        new Rect(
            cellRect.xMax - 2f,
            cellRect.y,
            2f,
            cellRect.height),
        SelectionBorderColor);
  }

  private static bool IsVerifiedChampionTile(int dungeonLevel, int tileX, int tileY)
  {
    if (dungeonLevel != HallOfChampionsLevel)
      return false;

    for (int i = 0; i < ChampionMirrorMarkers.Length; i++)
    {
      ChampionMirrorMarker marker = ChampionMirrorMarkers[i];
      if (marker.X != tileX || marker.Y != tileY)
        continue;

      for (int nameIndex = 0; nameIndex < VerifiedChampionEquipmentNames.Length; nameIndex++)
      {
        if (string.Equals(
                marker.Champion,
                VerifiedChampionEquipmentNames[nameIndex],
                System.StringComparison.OrdinalIgnoreCase))
          return true;
      }

      return false;
    }

    return false;
  }

  // Final tile highlight pass, drawn above ornaments and beneath champion names/markers.
  // This deliberately uses mirror coordinates (the champion's wall tile), not player coordinates.
  private void DrawVerifiedChampionTileHighlights(Rect mapRect)
  {
    if (currentDungeonLevel != HallOfChampionsLevel)
      return;

    for (int i = 0; i < ChampionMirrorMarkers.Length; i++)
    {
      ChampionMirrorMarker marker = ChampionMirrorMarkers[i];
      if (!IsVerifiedChampionTile(currentDungeonLevel, marker.X, marker.Y))
        continue;

      Rect tileRect = new Rect(
          mapRect.x + marker.X * CellSize + 1f,
          mapRect.y + marker.Y * CellSize + 1f,
          CellSize - 2f,
          CellSize - 2f);
      EditorGUI.DrawRect(tileRect, VerifiedChampionTileColor);
    }
  }

  private void DrawChampionMirrorMarkers(Rect mapRect)
  {
    // Champion mirrors/names are Hall-of-Champions-only. Other dungeon levels
    // use the all-level feature placement database without this overlay.
    if (currentDungeonLevel != HallOfChampionsLevel)
      return;

    for (int i = 0; i < ChampionMirrorMarkers.Length; i++)
    {
      ChampionMirrorMarker marker = ChampionMirrorMarkers[i];

      Rect cellRect = new Rect(
          mapRect.x + marker.X * CellSize,
          mapRect.y + marker.Y * CellSize,
          CellSize,
          CellSize);

      DrawWallFaceDirectionMarker(cellRect, marker.Side);
      DrawChampionName(mapRect, cellRect, marker);
    }
  }

  // Shared full-edge direction marker for champion mirrors and wall ornaments.
  // Wall face direction comes from each map placement, never from player facing.
  private void DrawWallFaceDirectionMarker(Rect cellRect, WallSide side)
  {
    float lineThickness = 3f * mapZoom;
    float dotSize = 4f * mapZoom;
    Rect lineRect;
    Rect dotRect;

    switch (side)
    {
      case WallSide.North:
      {
        float centerX = cellRect.center.x;
        lineRect = new Rect(
            cellRect.x,
            cellRect.y,
            cellRect.width,
            lineThickness);
        dotRect = new Rect(
            centerX - dotSize * 0.5f,
            cellRect.y - (dotSize - lineThickness) * 0.5f,
            dotSize,
            dotSize);
        break;
      }

      case WallSide.East:
      {
        float centerY = cellRect.center.y;
        lineRect = new Rect(
            cellRect.xMax - lineThickness,
            cellRect.y,
            lineThickness,
            cellRect.height);
        dotRect = new Rect(
            cellRect.xMax - lineThickness
                - (dotSize - lineThickness) * 0.5f,
            centerY - dotSize * 0.5f,
            dotSize,
            dotSize);
        break;
      }

      case WallSide.South:
      {
        float centerX = cellRect.center.x;
        lineRect = new Rect(
            cellRect.x,
            cellRect.yMax - lineThickness,
            cellRect.width,
            lineThickness);
        dotRect = new Rect(
            centerX - dotSize * 0.5f,
            cellRect.yMax - lineThickness
                - (dotSize - lineThickness) * 0.5f,
            dotSize,
            dotSize);
        break;
      }

      default: // West
      {
        float centerY = cellRect.center.y;
        lineRect = new Rect(
            cellRect.x,
            cellRect.y,
            lineThickness,
            cellRect.height);
        dotRect = new Rect(
            cellRect.x - (dotSize - lineThickness) * 0.5f,
            centerY - dotSize * 0.5f,
            dotSize,
            dotSize);
        break;
      }
    }

    EditorGUI.DrawRect(lineRect, ChampionMirrorLineColor);
    EditorGUI.DrawRect(dotRect, ChampionMirrorDotColor);
  }

  private void DrawStairDownIcons(Rect mapRect)
  {
    if (map == null)
      return;

    EnsureOrnamentTexturesResolved();

    float iconInset = 3f * mapZoom;

    for (int y = 0; y < map.Height; y++)
    {
      for (int x = 0; x < map.Width; x++)
      {
        DungeonTile tile = map.GetTile(x, y);
        if (tile == null
            || !tile.TryGetStairsDirection(out bool stairsUp)
            || stairsUp)
        {
          continue;
        }

        Rect cellRect = new Rect(
            mapRect.x + x * CellSize,
            mapRect.y + y * CellSize,
            CellSize,
            CellSize);

        Rect stairFillRect = new Rect(
            cellRect.x + 1f,
            cellRect.y + 1f,
            cellRect.width - 2f,
            cellRect.height - 2f);
        EditorGUI.DrawRect(stairFillRect, StairDownTileColor);

        Rect iconRect = new Rect(
            cellRect.x + iconInset,
            cellRect.y + iconInset,
            cellRect.width - iconInset * 2f,
            cellRect.height - iconInset * 2f);

        if (stairDownMapIcon != null)
        {
          GUI.DrawTexture(
              iconRect,
              stairDownMapIcon,
              ScaleMode.ScaleToFit,
              true);
        }
        else
        {
          ReportMissingMapIcon("Stairs_Down_Icon_20x22", "stairs-down map icon");
        }
      }
    }
  }

  private void DrawPuddleFloorIcons(Rect mapRect)
  {
    if (resolvedPuddleFloors.Count == 0)
      return;

    EnsureOrnamentTexturesResolved();

    float iconInset = 3f * mapZoom;
    for (int y = 0; y < map.Height; y++)
    {
      for (int x = 0; x < map.Width; x++)
      {
        if (!resolvedPuddleFloors.Contains(PackFloorFeatureTile(x, y)))
          continue;

        Rect cellRect = new Rect(
            mapRect.x + x * CellSize,
            mapRect.y + y * CellSize,
            CellSize,
            CellSize);
        Rect iconRect = new Rect(
            cellRect.x + iconInset,
            cellRect.y + iconInset,
            cellRect.width - iconInset * 2f,
            cellRect.height - iconInset * 2f);

        if (puddleMapIcon != null)
        {
          GUI.DrawTexture(
              iconRect,
              puddleMapIcon,
              ScaleMode.ScaleToFit,
              true);
        }
        else
        {
          ReportMissingMapIcon(PuddleF1AssetPath, "puddle map icon");
        }
      }
    }
  }

  // Floor moss has no wall-facing direction. Display it on the exact tile
  // from DungeonFeaturePlacements.json, including the player's own tile.
  private void DrawMossFloorIcons(Rect mapRect)
  {
    if (resolvedMossFloors.Count == 0)
      return;

    EnsureOrnamentTexturesResolved();
    if (mossMapIcon == null)
    {
      ReportMissingMapIcon(MossF1AssetName + ".png", "moss map icon");
      return;
    }

    float iconInset = 3f * mapZoom;
    for (int y = 0; y < map.Height; y++)
    {
      for (int x = 0; x < map.Width; x++)
      {
        if (!resolvedMossFloors.Contains(PackFloorFeatureTile(x, y)))
          continue;

        Rect cellRect = new Rect(
            mapRect.x + x * CellSize,
            mapRect.y + y * CellSize,
            CellSize,
            CellSize);
        Rect iconRect = new Rect(
            cellRect.x + iconInset,
            cellRect.y + iconInset,
            cellRect.width - iconInset * 2f,
            cellRect.height - iconInset * 2f);
        GUI.DrawTexture(iconRect, mossMapIcon, ScaleMode.ScaleToFit, true);
      }
    }
  }

  /// <summary>
  /// Populates Dungeon Features from the generated all-level placement
  /// database for the same active level that ViewEdit is displaying.
  /// Champion mirrors remain a dedicated Hall-of-Champions overlay.
  /// </summary>
  private bool LoadResolvedFeaturesFromDatabase()
  {
    if (!File.Exists(DungeonFeaturePlacementsPath))
      return false;

    try
    {
      DungeonFeaturePlacementDatabase database =
          DungeonFeaturePlacementDatabase.LoadFromFile(
              DungeonFeaturePlacementsPath);
      if (database == null || database.ornamentPlacements == null)
        return false;

      List<OriginalWallOrnamentMarker> wallMarkers =
          new List<OriginalWallOrnamentMarker>();
      resolvedPuddleFloors.Clear();
      resolvedMossFloors.Clear();

      for (int i = 0; i < database.ornamentPlacements.Length; i++)
      {
        DungeonOrnamentPlacement placement =
            database.ornamentPlacements[i];
        if (placement == null
            || placement.level != currentDungeonLevel
            || placement.resolved == null
            || string.IsNullOrEmpty(placement.resolved.type))
        {
          continue;
        }

        if (string.Equals(
                placement.domain,
                "Floor",
                System.StringComparison.OrdinalIgnoreCase))
        {
          if (string.Equals(
                  placement.resolved.type,
                  "Puddle",
                  System.StringComparison.OrdinalIgnoreCase))
          {
            resolvedPuddleFloors.Add(
                PackFloorFeatureTile(
                    placement.normalisedX,
                    placement.normalisedY));
          }
          if (string.Equals(
                  placement.resolved.type,
                  "Moss",
                  System.StringComparison.OrdinalIgnoreCase))
          {
            resolvedMossFloors.Add(
                PackFloorFeatureTile(
                    placement.normalisedX,
                    placement.normalisedY));
          }
          continue;
        }

        if (!string.Equals(
                placement.domain,
                "Wall",
                System.StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }

        // Champion mirrors already have their own blue/yellow wall marker and
        // green champion-name labels in this editor.
        if (string.Equals(
                placement.resolved.type,
                "ChampionMirror",
                System.StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }

        if (!TryParseWallSide(
                placement.normalisedFace,
                out WallSide side))
        {
          continue;
        }

        wallMarkers.Add(
            new OriginalWallOrnamentMarker(
                NormaliseDatabaseFeatureType(placement.resolved.type),
                placement.normalisedX,
                placement.normalisedY,
                side,
                true));
      }

      resolvedWallOrnaments = wallMarkers.ToArray();
      return true;
    }
    catch (System.Exception ex)
    {
      Debug.LogWarning(
          "Dungeon Features database load failed; using Hall fallback: "
          + ex.Message);
      return false;
    }
  }

  private static string NormaliseDatabaseFeatureType(string type)
  {
    if (string.Equals(
            type,
            "WallGrate",
            System.StringComparison.OrdinalIgnoreCase))
      return "Grate";

    return type;
  }

  private static void BuildHallOfChampionsPuddleFloors(
      DungeonMap map,
      HashSet<int> destination)
  {
    destination.Clear();
    if (map == null)
      return;

    int value2 = 3000 + map.Width + map.Height;

    for (int y = 0; y < map.Height; y++)
    {
      for (int x = 0; x < map.Width; x++)
      {
        DungeonTile tile = map.GetTile(x, y);
        if (tile == null
            || tile.SourceType != DungeonSourceTileType.Floor
            || (tile.Raw & 0x08) == 0)
        {
          continue;
        }

        int randomIndex = HallOfChampionsRandomOrnamentHash(
            2000 + (x << 5) + y,
            value2,
            HallOfChampionsRandomOrnamentModulo);

        if (randomIndex >= HallOfChampionsRandomFloorOrnamentCount)
          continue;

        int ordinal = randomIndex + 1;
        if (ordinal == HallOfChampionsPuddleFloorOrnamentOrdinal)
          destination.Add(PackFloorFeatureTile(x, y));
      }
    }
  }

  private static int HallOfChampionsRandomOrnamentHash(
      int value1,
      int value2,
      int modulo)
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
      d0Word = (ushort)(d0Word + HallOfChampionsDungeonSeed);
      d0Word = (ushort)((d0Word >> 2) & 0x3FFF);

      return d0Word % modulo;
    }
  }

  private static int PackFloorFeatureTile(int x, int y)
  {
    return (y << 8) | (x & 0xFF);
  }

  private void ReportMissingMapIcon(string asset, string description)
  {
    string key = description + ": " + (asset ?? "<null>");
    if (missingMapIconErrors.Add(key))
      Debug.LogError("<color=cyan>Dungeon Features: Missing " + description
          + " (" + (asset ?? "<null>") + "). No substitute graphic was drawn.</color>");
  }

  private void DrawOriginalWallOrnamentIcons(Rect mapRect)
  {
    EnsureOrnamentTexturesResolved();
    DrawWallOrnamentIconList(mapRect, resolvedWallOrnaments);
  }

  private static OriginalWallOrnamentMarker[] BuildGreyWallOrnaments(
      DungeonMap map)
  {
    HallWallOrnamentPlacement[] generated =
        HallOfChampionsWallOrnaments.Build(map);
    List<OriginalWallOrnamentMarker> markers =
        new List<OriginalWallOrnamentMarker>();

    if (generated != null)
    {
      for (int i = 0; i < generated.Length; i++)
      {
        HallWallOrnamentPlacement ornament = generated[i];
        if (ornament == null
            || !TryParseWallSide(ornament.Wall, out WallSide side))
        {
          continue;
        }

        markers.Add(
            new OriginalWallOrnamentMarker(
                ornament.Type,
                ornament.X,
                ornament.Y,
                side,
                true));
      }
    }

    return markers.ToArray();
  }

  private static bool TryParseWallSide(string wall, out WallSide side)
  {
    if (!string.IsNullOrEmpty(wall) && wall.Length == 1)
    {
      switch (char.ToUpperInvariant(wall[0]))
      {
        case 'N': side = WallSide.North; return true;
        case 'E': side = WallSide.East; return true;
        case 'S': side = WallSide.South; return true;
        case 'W': side = WallSide.West; return true;
      }
    }

    if (string.Equals(
            wall,
            "North",
            System.StringComparison.OrdinalIgnoreCase))
    {
      side = WallSide.North;
      return true;
    }

    if (string.Equals(
            wall,
            "East",
            System.StringComparison.OrdinalIgnoreCase))
    {
      side = WallSide.East;
      return true;
    }

    if (string.Equals(
            wall,
            "South",
            System.StringComparison.OrdinalIgnoreCase))
    {
      side = WallSide.South;
      return true;
    }

    if (string.Equals(
            wall,
            "West",
            System.StringComparison.OrdinalIgnoreCase))
    {
      side = WallSide.West;
      return true;
    }

    side = WallSide.North;
    return false;
  }

  private void DrawWallOrnamentIconList(
      Rect mapRect,
      OriginalWallOrnamentMarker[] markers)
  {
    if (markers == null)
      return;

    // Large red VI overlay fills the Altar of Vi tile; original art stays beneath.
    GUIStyle viLabelStyle = new GUIStyle(GUI.skin.label)
    {
      alignment = TextAnchor.MiddleCenter,
      fontStyle = FontStyle.Bold,
      fontSize = Mathf.Max(1, Mathf.RoundToInt(20f * mapZoom))
    };
    viLabelStyle.normal.textColor = Color.red;

    GUIStyle lordOrderOutsideLabelStyle = new GUIStyle(GUI.skin.label)
    {
      alignment = TextAnchor.MiddleCenter,
      fontStyle = FontStyle.Bold,
      fontSize = Mathf.Max(1, Mathf.RoundToInt(10f * mapZoom))
    };
    lordOrderOutsideLabelStyle.normal.textColor = Color.white;

    for (int i = 0; i < markers.Length; i++)
    {
      OriginalWallOrnamentMarker marker = markers[i];

      Rect cellRect = new Rect(
          mapRect.x + marker.X * CellSize,
          mapRect.y + marker.Y * CellSize,
          CellSize,
          CellSize);

      // Only wall-attached ornaments have a facing direction. Floor features
      // are drawn separately and intentionally receive no blue marker.
      DrawWallFaceDirectionMarker(cellRect, marker.Side);

      Texture2D texture = GetOrnamentMapIcon(marker.Type);
      // All wall ornaments are centered on their 32x32 map tile.
      // The lit torch retains its native 16x16 display size; other wall
      // ornaments retain their existing 20x20 drawing area.
      float iconSize = (IsTorchHolderOrnamentType(marker.Type) ? 16f : 20f) * mapZoom;
      Rect iconRect = new Rect(
          cellRect.center.x - iconSize * 0.5f,
          cellRect.center.y - iconSize * 0.5f,
          iconSize,
          iconSize);

      if (IsLordOrderOutsideOrnamentType(marker.Type))
      {
        EditorGUI.DrawRect(iconRect, LordOrderOutsideMarkerColor);
        GUI.Label(cellRect, "LO", lordOrderOutsideLabelStyle);
        continue;
      }

      if (texture != null)
      {
        if (IsManaclesOrnamentType(marker.Type))
          EditorGUI.DrawRect(iconRect, ManaclesHighlightBackgroundColor);

        GUI.DrawTexture(
            iconRect,
            texture,
            ScaleMode.ScaleToFit,
            true);

        if (string.Equals(marker.Type, "ViAltar",
                System.StringComparison.OrdinalIgnoreCase))
          GUI.Label(cellRect, "VI", viLabelStyle);
      }
      else
      {
        ReportMissingMapIcon(marker.Type, "wall ornament map icon");
      }
    }
  }

  private static bool IsTorchHolderOrnamentType(string type)
  {
    return string.Equals(
               type,
               "FullTorchHolder",
               System.StringComparison.OrdinalIgnoreCase)
           || string.Equals(
               type,
               "EmptyTorchHolder",
               System.StringComparison.OrdinalIgnoreCase);
  }

  private static bool IsManaclesOrnamentType(string type)
  {
    return string.Equals(
               type,
               "Manacles",
               System.StringComparison.OrdinalIgnoreCase)
           || string.Equals(
               type,
               "WallManacles",
               System.StringComparison.OrdinalIgnoreCase);
  }

  private static bool IsLordOrderOutsideOrnamentType(string type)
  {
    return string.Equals(
               type,
               "LordOrderOutside",
               System.StringComparison.OrdinalIgnoreCase);
  }

  private void EnsureOrnamentTexturesResolved()
  {
    if (ornamentTexturesResolved)
      return;

    ornamentTexturesResolved = true;

    viAltarMapIcon = FindBestOrnamentTexture("ViAltar");
    hookMapIcon = FindBestOrnamentTexture("Hook");
    woodRingMapIcon = FindBestOrnamentTexture("WoodRing");
    slimeMapIcon = FindBestOrnamentTexture("Slime");
    grateMapIcon = FindBestOrnamentTexture("Grate");
    manaclesMapIcon = FindExactTextureByName("Manacles_F1");
    fullTorchHolderMapIcon = FindExactTextureByName("Torch_Lit_16x16");
    puddleMapIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
        PuddleF1AssetPath);
    mossMapIcon = FindExactTextureByName(MossF1AssetName);
    stairDownMapIcon = FindExactTextureByName("Stairs_Down_Icon_20x22");
  }

  private Texture2D GetOrnamentMapIcon(string type)
  {
    switch (type)
    {
      case "ViAltar":
        return viAltarMapIcon;

      case "Hook":
        return hookMapIcon;

      case "WoodRing":
        return woodRingMapIcon;

      case "Slime":
        return slimeMapIcon;

      case "Grate":
        return grateMapIcon;

      case "Manacles":
      case "WallManacles":
        return manaclesMapIcon;

      case "FullTorchHolder":
      case "EmptyTorchHolder":
        return fullTorchHolderMapIcon;

      default:
        return null;
    }
  }


  private static Texture2D FindExactTextureByName(string assetName)
  {
    if (string.IsNullOrEmpty(assetName))
      return null;

    string[] guids = AssetDatabase.FindAssets(
        assetName + " t:Texture2D",
        new[] { "Assets" });

    for (int i = 0; i < guids.Length; i++)
    {
      string path = AssetDatabase.GUIDToAssetPath(guids[i]);
      if (!string.Equals(
              Path.GetFileNameWithoutExtension(path),
              assetName,
              System.StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
      if (texture != null)
        return texture;
    }

    return null;
  }

  private static Texture2D FindBestOrnamentTexture(string type)
  {
    string[] folders = type == "ViAltar"
        ? new[] { "Assets" }
        : new[] { "Assets/Art/Ornaments" };
    string[] guids = AssetDatabase.FindAssets("t:Texture2D", folders);

    Texture2D best = null;
    int bestScore = int.MinValue;

    for (int i = 0; i < guids.Length; i++)
    {
      string path = AssetDatabase.GUIDToAssetPath(guids[i]);
      string fileName =
          Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

      int score = ScoreOrnamentTexture(type, fileName);
      if (score < 0 || score <= bestScore)
        continue;

      Texture2D candidate =
          AssetDatabase.LoadAssetAtPath<Texture2D>(path);
      if (candidate == null)
        continue;

      best = candidate;
      bestScore = score;
    }

    return best;
  }

  private static int ScoreOrnamentTexture(
      string type,
      string lowerFileName)
  {
    if (string.IsNullOrEmpty(lowerFileName))
      return -1;

    string normalized =
        lowerFileName
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

    int score = 0;

    switch (type)
    {
      case "ViAltar":
        if (!(normalized.Contains("altar") && normalized.Contains("vi")))
          return -1;
        score += 100;
        if (normalized.Contains("96x56") || normalized.Contains("9656"))
          score += 25;
        break;

      case "Hook":
        if (!normalized.Contains("hook"))
          return -1;
        score += 100;
        break;

      case "WoodRing":
        if (!(normalized.Contains("woodring")
              || (normalized.Contains("wood")
                  && normalized.Contains("ring"))))
        {
          return -1;
        }

        score += 100;
        break;

      case "Slime":
        if (!normalized.Contains("slime"))
          return -1;
        score += 100;
        break;

      case "Grate":
        if (!normalized.Contains("grate"))
          return -1;
        score += 100;
        if (normalized.Contains("32x28") || normalized.Contains("3228"))
          score += 25;
        break;

      default:
        return -1;
    }

    // Prefer the same frontal artwork used by the first-person renderer.
    if (normalized.Contains("front"))
      score += 50;

    // Prefer a named original-size/front file over side/distance variants.
    if (normalized.Contains("side"))
      score -= 20;
    if (normalized.Contains("f2"))
      score -= 10;
    if (normalized.Contains("f3"))
      score -= 10;

    return score;
  }

  private GUIStyle CreateChampionNameStyle()
  {
    GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
    {
      alignment = TextAnchor.MiddleCenter,
      fontSize = Mathf.Max(1, Mathf.RoundToInt(12f * mapZoom)),
      fontStyle = FontStyle.Bold,
      clipping = TextClipping.Overflow,
      wordWrap = false,
      padding = new RectOffset(0, 0, 0, 0),
      margin = new RectOffset(0, 0, 0, 0)
    };

    Color textColor = new Color(0f, 1f, 0f, 1f);
    nameStyle.normal.textColor = textColor;
    nameStyle.hover.textColor = textColor;
    nameStyle.active.textColor = textColor;
    nameStyle.focused.textColor = textColor;
    nameStyle.onNormal.textColor = textColor;
    nameStyle.onHover.textColor = textColor;
    nameStyle.onActive.textColor = textColor;
    nameStyle.onFocused.textColor = textColor;
    return nameStyle;
  }

  private void DrawChampionName(
      Rect mapRect,
      Rect cellRect,
      ChampionMirrorMarker marker)
  {
    if (marker == null || string.IsNullOrEmpty(marker.Champion))
      return;

    GUIStyle nameStyle = CreateChampionNameStyle();

    int nameCellX = marker.X;
    int nameCellY = marker.Y;

    Vector2 labelCenter = cellRect.center;
    switch (marker.Side)
    {
      case WallSide.North:
        nameCellY -= 1;
        labelCenter.y -= CellSize;
        break;

      case WallSide.East:
        nameCellX += 1;
        labelCenter.x += CellSize;
        break;

      case WallSide.South:
        nameCellY += 1;
        labelCenter.y += CellSize;
        break;

      default: // West
        nameCellX -= 1;
        labelCenter.x -= CellSize;
        break;
    }

    bool labelFallsOnWalkableMap =
        map != null
        && map.IsInside(nameCellX, nameCellY)
        && map.GetTile(nameCellX, nameCellY).Type != DungeonTileType.Wall;

    Color previousContentColor = GUI.contentColor;
    GUI.contentColor = ChampionNameColor;

    bool pairTop =
        marker.Champion == "Tiggy" || marker.Champion == "Wu Tse";
    bool pairBottom =
        marker.Champion == "Wuuf" || marker.Champion == "Leif";
    if (pairTop || pairBottom)
    {
      Rect targetTileRect = new Rect(
          labelCenter.x - CellSize * 0.5f,
          labelCenter.y - CellSize * 0.5f,
          CellSize,
          CellSize);

      GUIStyle pairStyle = new GUIStyle(nameStyle)
      {
        alignment = pairTop
            ? TextAnchor.UpperCenter
            : TextAnchor.LowerCenter,
        clipping = TextClipping.Overflow
      };

      GUI.Label(targetTileRect, marker.Champion, pairStyle);
      GUI.contentColor = previousContentColor;
      return;
    }

    // Hissssa / Gothmog: column x=18, Y centered on the blue/yellow wall marker.
    if (marker.Champion == "Hissssa" || marker.Champion == "Gothmog")
    {
      Vector2 outsideSize = nameStyle.CalcSize(new GUIContent(marker.Champion));
      float outsideWidth = Mathf.Max(outsideSize.x, CellSize);
      float outsideHeight = Mathf.Max(outsideSize.y, 18f);
      const float markerLineThickness = 3f;
      float markerCenterY = marker.Side == WallSide.South
          ? cellRect.yMax - markerLineThickness * 0.5f
          : cellRect.y + markerLineThickness * 0.5f;
      Rect outsideNameRect = new Rect(
          mapRect.xMax,
          markerCenterY - outsideHeight * 0.5f,
          outsideWidth,
          outsideHeight);

      GUIStyle outsideStyle = new GUIStyle(nameStyle)
      {
        alignment = TextAnchor.MiddleLeft,
        clipping = TextClipping.Overflow
      };

      GUI.Label(outsideNameRect, marker.Champion, outsideStyle);
      GUI.contentColor = previousContentColor;
      return;
    }

    if (marker.Champion == "Daroou" || marker.Champion == "Mophus")
    {
      float stackedLineHeight = 9f;
      float stackedCharWidth = 20f;
      float stackedTotalHeight = marker.Champion.Length * stackedLineHeight;
      float stackedStartY = marker.Champion == "Mophus"
          ? cellRect.y + 1f
          : labelCenter.y - stackedTotalHeight * 0.5f;

      GUIStyle stackedStyle = new GUIStyle(nameStyle)
      {
        alignment = TextAnchor.MiddleCenter,
        clipping = TextClipping.Overflow
      };

      for (int i = 0; i < marker.Champion.Length; i++)
      {
        string glyph = marker.Champion[i].ToString();
        Vector2 glyphSize = stackedStyle.CalcSize(new GUIContent(glyph));
        float glyphWidth = Mathf.Max(stackedCharWidth, glyphSize.x);
        float glyphHeight = Mathf.Max(stackedLineHeight, glyphSize.y);

        Rect glyphRect = new Rect(
            labelCenter.x - glyphWidth * 0.5f,
            stackedStartY + i * stackedLineHeight
                + (stackedLineHeight - glyphHeight) * 0.5f,
            glyphWidth,
            glyphHeight);

        GUI.Label(glyphRect, glyph, stackedStyle);
      }

      GUI.contentColor = previousContentColor;
      return;
    }

    if (labelFallsOnWalkableMap)
    {
      float walkableLineHeight = 18f;
      float walkableCharWidth = 20f;
      float walkableTotalHeight = marker.Champion.Length * walkableLineHeight;
      float walkableStartY = labelCenter.y - walkableTotalHeight * 0.5f;

      GUIStyle walkableStyle = new GUIStyle(nameStyle)
      {
        alignment = TextAnchor.MiddleCenter,
        clipping = TextClipping.Overflow
      };

      for (int i = 0; i < marker.Champion.Length; i++)
      {
        string glyph = marker.Champion[i].ToString();
        Vector2 glyphSize = walkableStyle.CalcSize(new GUIContent(glyph));
        float glyphWidth = Mathf.Max(walkableCharWidth, glyphSize.x);
        float glyphHeight = Mathf.Max(walkableLineHeight, glyphSize.y);

        Rect glyphRect = new Rect(
            labelCenter.x - glyphWidth * 0.5f,
            walkableStartY + i * walkableLineHeight
                + (walkableLineHeight - glyphHeight) * 0.5f,
            glyphWidth,
            glyphHeight);

        GUI.Label(glyphRect, glyph, walkableStyle);
      }

      GUI.contentColor = previousContentColor;
      return;
    }

    Vector2 nameSize = nameStyle.CalcSize(new GUIContent(marker.Champion));
    float nameWidth = Mathf.Max(128f, nameSize.x + 4f);
    float nameHeight = Mathf.Max(18f, nameSize.y + 2f);
    Rect nameRect = new Rect(
        labelCenter.x - nameWidth * 0.5f,
        labelCenter.y - nameHeight * 0.5f,
        nameWidth,
        nameHeight);

    GUI.Label(nameRect, marker.Champion, nameStyle);
    GUI.contentColor = previousContentColor;
  }

  private bool TryTeleportToWallOrnament(int tileX, int tileY)
  {
    OriginalWallOrnamentMarker marker =
        FindWallOrnamentAtTile(
            resolvedWallOrnaments,
            tileX,
            tileY);

    if (marker == null)
      return false;

    int targetX = marker.X;
    int targetY = marker.Y;
    DungeonFacing targetFacing;

    if (!marker.WallTilePlacement)
    {
      // Mechanism is stored on the walkable cell itself.
      targetFacing = WallSideToFacing(marker.Side);
    }
    else if (!TryWalkableViewPoseForWallTile(
        marker,
        out targetX,
        out targetY,
        out targetFacing))
    {
      return false;
    }

    if (!map.IsInside(targetX, targetY)
        || !map.CanEnter(targetX, targetY))
    {
      return false;
    }

    selectedX = targetX;
    selectedY = targetY;
    selectedFacing = targetFacing;

    EditorPrefs.SetInt(ViewEditPreviewLevelKey, currentDungeonLevel);
    EditorPrefs.SetInt(ViewEditPreviewXKey, targetX);
    EditorPrefs.SetInt(ViewEditPreviewYKey, targetY);
    EditorPrefs.SetInt(
        ViewEditPreviewFacingKey,
        (int)targetFacing);

    return true;
  }

  private bool TryWalkableViewPoseForWallTile(
      OriginalWallOrnamentMarker marker,
      out int targetX,
      out int targetY,
      out DungeonFacing targetFacing)
  {
    targetX = marker.X;
    targetY = marker.Y;
    targetFacing = WallSideToFacing(marker.Side);

    int sideX;
    int sideY;
    WallSideOffset(marker.Side, out sideX, out sideY);

    // Physical-face convention: stand on the neighbor in Side and look back.
    int physicalX = marker.X + sideX;
    int physicalY = marker.Y + sideY;
    DungeonFacing lookBack = OppositeFacing(WallSideToFacing(marker.Side));
    if (map.IsInside(physicalX, physicalY) && map.CanEnter(physicalX, physicalY))
    {
      targetX = physicalX;
      targetY = physicalY;
      targetFacing = lookBack;
      return true;
    }

    // JSON/sensor-on-wall convention: Side is the look direction, so the
    // walkable cell is behind the camera. (12,3) North views from (12,4).
    int viewX = marker.X - sideX;
    int viewY = marker.Y - sideY;
    if (map.IsInside(viewX, viewY) && map.CanEnter(viewX, viewY))
    {
      targetX = viewX;
      targetY = viewY;
      targetFacing = WallSideToFacing(marker.Side);
      return true;
    }

    return false;
  }

  private static void WallSideOffset(
      WallSide side,
      out int dx,
      out int dy)
  {
    switch (side)
    {
      case WallSide.North:
        dx = 0;
        dy = -1;
        return;
      case WallSide.East:
        dx = 1;
        dy = 0;
        return;
      case WallSide.South:
        dx = 0;
        dy = 1;
        return;
      default:
        dx = -1;
        dy = 0;
        return;
    }
  }

  private static DungeonFacing OppositeFacing(DungeonFacing facing)
  {
    switch (facing)
    {
      case DungeonFacing.North:
        return DungeonFacing.South;
      case DungeonFacing.East:
        return DungeonFacing.West;
      case DungeonFacing.South:
        return DungeonFacing.North;
      default:
        return DungeonFacing.East;
    }
  }

  private static OriginalWallOrnamentMarker FindWallOrnamentAtTile(
      OriginalWallOrnamentMarker[] markers,
      int x,
      int y)
  {
    if (markers == null)
      return null;

    for (int i = 0; i < markers.Length; i++)
    {
      OriginalWallOrnamentMarker marker = markers[i];
      if (marker != null && marker.X == x && marker.Y == y)
        return marker;
    }

    return null;
  }

  private static DungeonFacing WallSideToFacing(WallSide side)
  {
    switch (side)
    {
      case WallSide.North:
        return DungeonFacing.North;
      case WallSide.East:
        return DungeonFacing.East;
      case WallSide.South:
        return DungeonFacing.South;
      default:
        return DungeonFacing.West;
    }
  }

  private bool TryFacingForEnteredCell(
      int x,
      int y,
      out DungeonFacing facing)
  {
    if (TryChampionMirrorFacing(x, y, out facing))
      return true;

    if (TryAdjacentOrnamentFacing(x, y, out facing))
      return true;

    if (TryAdjacentStairsFacing(x, y, out facing))
      return true;

    facing = selectedFacing;
    return false;
  }

  private bool TryChampionMirrorFacing(
      int x,
      int y,
      out DungeonFacing facing)
  {
    for (int i = 0; i < ChampionMirrorMarkers.Length; i++)
    {
      ChampionMirrorMarker marker = ChampionMirrorMarkers[i];
      if (marker != null && marker.X == x && marker.Y == y)
      {
        facing = WallSideToFacing(marker.Side);
        return true;
      }
    }

    facing = selectedFacing;
    return false;
  }

  // Search North, East, South, then West. The ornament side is the wall
  // face that looks back at the entered cell.
  private bool TryAdjacentOrnamentFacing(
      int x,
      int y,
      out DungeonFacing facing)
  {
    if (HasOrnamentFace(x, y - 1, WallSide.South))
    {
      facing = DungeonFacing.North;
      return true;
    }

    if (HasOrnamentFace(x + 1, y, WallSide.West))
    {
      facing = DungeonFacing.East;
      return true;
    }

    if (HasOrnamentFace(x, y + 1, WallSide.North))
    {
      facing = DungeonFacing.South;
      return true;
    }

    if (HasOrnamentFace(x - 1, y, WallSide.East))
    {
      facing = DungeonFacing.West;
      return true;
    }

    facing = selectedFacing;
    return false;
  }

  private bool TryAdjacentStairsFacing(
      int x,
      int y,
      out DungeonFacing facing)
  {
    if (IsUpOrDownStairs(x, y - 1))
    {
      facing = DungeonFacing.North;
      return true;
    }

    if (IsUpOrDownStairs(x + 1, y))
    {
      facing = DungeonFacing.East;
      return true;
    }

    if (IsUpOrDownStairs(x, y + 1))
    {
      facing = DungeonFacing.South;
      return true;
    }

    if (IsUpOrDownStairs(x - 1, y))
    {
      facing = DungeonFacing.West;
      return true;
    }

    facing = selectedFacing;
    return false;
  }

  private bool HasOrnamentFace(int wallX, int wallY, WallSide side)
  {
    if (resolvedWallOrnaments == null)
      return false;

    for (int i = 0; i < resolvedWallOrnaments.Length; i++)
    {
      OriginalWallOrnamentMarker marker = resolvedWallOrnaments[i];
      if (marker != null
          && marker.X == wallX
          && marker.Y == wallY
          && marker.Side == side)
      {
        return true;
      }
    }

    return false;
  }

  private bool IsUpOrDownStairs(int x, int y)
  {
    if (map == null || !map.IsInside(x, y))
      return false;

    DungeonTile tile = map.GetTile(x, y);
    return tile != null && tile.TryGetStairsDirection(out _);
  }

  private void HandleGridClick(Rect mapRect)
  {
    Event current = Event.current;
    if (current.type != EventType.MouseDown || current.button != 0)
      return;

    Vector2 mouse = current.mousePosition;
    List<FloorItemPlacementRecord> popupRecords = GetInspectedFloorItems();
    if (popupRecords != null && popupRecords.Count > 1
        && GetSelectedFloorItemPopupRect(mapRect, popupRecords.Count).Contains(mouse))
    {
      current.Use();
      return;
    }
    if (!mapRect.Contains(mouse))
      return;

    int x = Mathf.FloorToInt((mouse.x - mapRect.x) / CellSize);
    int y = Mathf.FloorToInt((mouse.y - mapRect.y) / CellSize);

    if (!map.IsInside(x, y))
      return;

    bool doubleClick = current.clickCount == 2;
    // An inspection belongs only to its selected tile. Clicking any other
    // tile (including one without items) clears stale details immediately.
    // Preserve the current details when double-clicking the same tile.
    if (inspectedFloorItemX != x || inspectedFloorItemY != y)
    {
      inspectedFloorItemX = -1;
      inspectedFloorItemY = -1;
    }
    // Inspect item records on a SINGLE click only. A second click must
    // remain available for the existing double-click tile navigation.
    if (!doubleClick && showFloorItems
        && resolvedFloorItems.TryGetValue(PackFloorFeatureTile(x, y),
               out List<FloorItemPlacementRecord> clickedItems)
        && clickedItems != null && clickedItems.Count > 0)
    {
      inspectedFloorItemX = x;
      inspectedFloorItemY = y;
      current.Use();
      GUI.changed = true;
      Repaint();
      return;
    }

    // Double-click navigation only accepts a walkable destination. A single
    // click still permits selecting map walls and inspecting their ornaments.
    if (doubleClick && !map.CanEnter(x, y))
    {
      current.Use();
      return;
    }

    if (TryTeleportToWallOrnament(x, y))
    {
      current.Use();
      GUI.changed = true;
      Repaint();
      if (doubleClick)
        CloseAfterGridTeleport();
      return;
    }

    DungeonFacing previousFacing = selectedFacing;
    selectedX = x;
    selectedY = y;

    if (map.CanEnter(x, y)
        && TryFacingForEnteredCell(x, y, out DungeonFacing enteredFacing))
    {
      selectedFacing = enteredFacing;
    }

    if (doubleClick)
      selectedFacing = previousFacing;

    EditorPrefs.SetInt(ViewEditPreviewLevelKey, currentDungeonLevel);
    EditorPrefs.SetInt(ViewEditPreviewXKey, x);
    EditorPrefs.SetInt(ViewEditPreviewYKey, y);
    EditorPrefs.SetInt(
        ViewEditPreviewFacingKey,
        (int)selectedFacing);

    current.Use();
    GUI.changed = true;
    Repaint();
    if (doubleClick)
      CloseAfterGridTeleport();
  }

  // The selected map pose has already been published to the shared ViewEdit
  // preferences. Close like Escape, then focus the existing ViewEdit window;
  // its normal inspector update applies the requested pose and redraws GameView.
  private void CloseAfterGridTeleport()
  {
    EditorApplication.delayCall += () =>
    {
      if (this != null)
        Close();
      foreach (ViewportLayoutEditor viewEdit in
          Resources.FindObjectsOfTypeAll<ViewportLayoutEditor>())
      {
        if (viewEdit == null)
          continue;
        viewEdit.Focus();
        viewEdit.Repaint();
        break;
      }
    };
  }

}
