using System.Collections.Generic;

namespace DM.Items
{
  public static class ItemDatabase
  {
    private static readonly Dictionary<int, ItemDefinition> ItemsById = new()
        {
            {
                1,
                new ItemDefinition
                {
                    Id = 1,
                    Name = "Torch",
                    Description = "A wooden torch used to light the dungeon.",
                    Weight = 1.1f,
                    Category = ItemCategory.Tool,
                    EquipmentSlot = EquipmentSlot.RightHand,
                    Damage = 0,
                    Armour = 0,
                    Stackable = false,
                    Consumable = false
                }
            },

            {
                2,
                new ItemDefinition
                {
                    Id = 2,
                    Name = "Dagger",
                    Description = "A small one-handed weapon.",
                    Weight = 0.5f,
                    Category = ItemCategory.Weapon,
                    EquipmentSlot = EquipmentSlot.RightHand,
                    Damage = 4,
                    Armour = 0,
                    Stackable = false,
                    Consumable = false
                }
            }
        };

    public static ItemDefinition GetById(int itemId)
    {
      ItemsById.TryGetValue(itemId, out ItemDefinition item);
      return item;
    }

    public static bool Contains(int itemId)
    {
      return ItemsById.ContainsKey(itemId);
    }

    public static IReadOnlyCollection<ItemDefinition> GetAll()
    {
      return ItemsById.Values;
    }
  }
}