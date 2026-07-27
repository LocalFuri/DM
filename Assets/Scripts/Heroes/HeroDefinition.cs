using System;
using System.Collections.Generic;
using DM.Items;

namespace DM.Heroes
{
  [Serializable]
  public class HeroDefinition
  {
    public int Id;

    public string Name;
    public string Title;

    public int Health;
    public int Stamina;
    public int Mana;

    public int Strength;
    public int Dexterity;
    public int Wisdom;
    public int Vitality;
    public int AntiMagic;
    public int AntiFire;

    public List<ItemInstance> StartingItems = new();
  }
}