using System;

namespace DM.Items
{
  [Serializable]
  public class ItemDefinition
  {
    public int Id;
    public string Name;
    public string Description;

    public float Weight;

    public ItemCategory Category;
    public EquipmentSlot EquipmentSlot;

    public int Damage;
    public int Armour;

    public bool Stackable;
    public bool Consumable;
  }
}