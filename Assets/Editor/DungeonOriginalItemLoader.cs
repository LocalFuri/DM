using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Read-only original floor-item records. Inventory/world rendering remains in its owning editor.
internal static class DungeonOriginalItemLoader
{
  private const string SourcePath = "Assets/Data/Features/DungeonFeaturePlacements.json";
  [Serializable] internal sealed class Record
  {
    public string id;
    public int level;
    public int localX;
    public int localY;
    public string direction;
    public string recordType;
    public string tileClass;
    public Thing thing;
  }
  [Serializable] internal sealed class Thing { public int Type; }
  [Serializable] private sealed class Database { public Record[] placements; }
  private static Record[] cached;
  private static DateTime lastWriteUtc;
  internal static Record[] Load()
  {
    try
    {
      if (!File.Exists(SourcePath)) throw new FileNotFoundException("Required original item placements missing", SourcePath);
      DateTime stamp = File.GetLastWriteTimeUtc(SourcePath);
      if (cached != null && stamp == lastWriteUtc) return cached;
      Database db = JsonUtility.FromJson<Database>(File.ReadAllText(SourcePath));
      if (db == null || db.placements == null) throw new InvalidDataException("Missing placements array");
      var result = new List<Record>();
      var ids = new HashSet<string>(StringComparer.Ordinal);
      foreach (Record r in db.placements)
      {
        if (r == null || r.tileClass != "Floor" || !IsPickable(r.recordType)) continue;
        if (r.thing == null || string.IsNullOrEmpty(r.id) || !ids.Add(r.id))
          throw new InvalidDataException("Missing thing/ID or duplicated floor record ID: " + (r == null ? "null" : r.id));
        result.Add(r);
      }
      cached = result.ToArray(); lastWriteUtc = stamp;
      return cached;
    }
    catch (Exception e)
    {
      cached = null;
      Debug.LogError("ORIGINAL FLOOR ITEMS: Cannot load authoritative database: " + e);
      return new Record[0]; // An explicit failure, never synthetic Apple/Bread items.
    }
  }
  internal static bool IsPickable(string type)
  {
    return type == "Junk" || type == "Weapon" || type == "Armour" ||
           type == "Potion" || type == "Scroll" || type == "Container";
  }
  internal static string DecodeFloorItemName(string recordType, int type)
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

}
