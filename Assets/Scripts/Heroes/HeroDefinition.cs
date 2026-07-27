using System;
using System.Collections.Generic;

namespace DM.Heroes
{
  [Serializable]
  public sealed class HeroDefinition
  {
    public int Id;

    public string Name = string.Empty;
    public string Title = string.Empty;

    public HeroGender Gender;

    public string PortraitName = string.Empty;

    public HeroResources Resources = new();
    public HeroAttributes Attributes = new();
    public HeroSkills Skills = new();
    public HeroPlacement Placement = new();

    public List<HeroStartingItem> StartingItems = new();
  }

  [Serializable]
  public sealed class HeroResources
  {
    public int Health;

    // DUNGEON.DAT stores stamina in its raw internal units.
    // Example: HALK is extracted as 750.
    public int Stamina;

    public int Mana;
  }

  [Serializable]
  public sealed class HeroAttributes
  {
    public int Luck;
    public int Strength;
    public int Dexterity;
    public int Wisdom;
    public int Vitality;
    public int AntiMagic;
    public int AntiFire;
  }

  [Serializable]
  public sealed class HeroSkills
  {
    // Each main discipline contains four raw subskill values
    // exactly as extracted from DUNGEON.DAT.
    public int[] Fighter = new int[4];
    public int[] Ninja = new int[4];
    public int[] Priest = new int[4];
    public int[] Wizard = new int[4];
  }

  [Serializable]
  public sealed class HeroPlacement
  {
    public int Level;
    public int X;
    public int Y;

    public HeroWallDirection WallDirection;
  }

  [Serializable]
  public sealed class HeroStartingItem
  {
    public string ObjectType = string.Empty;
    public int TypeId;
    public int ChargeCount;
  }

  public enum HeroGender
  {
    Male,
    Female
  }

  public enum HeroWallDirection
  {
    North,
    East,
    South,
    West
  }
}