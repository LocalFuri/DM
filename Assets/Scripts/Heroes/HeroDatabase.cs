using System.Collections.Generic;
using DM.Items;

namespace DM.Heroes
{
  public static class HeroDatabase
  {
    private static readonly Dictionary<int, HeroDefinition> HeroesById = new()
        {
            {
                1,
                new HeroDefinition
                {
                    Id = 1,
                    Name = "Test Hero",
                    Title = "The Adventurer",

                    Health = 100,
                    Stamina = 100,
                    Mana = 50,

                    Strength = 10,
                    Dexterity = 10,
                    Wisdom = 10,
                    Vitality = 10,
                    AntiMagic = 10,
                    AntiFire = 10,

                    StartingItems = new List<ItemInstance>
                    {
                        new ItemInstance(1),
                        new ItemInstance(2)
                    }
                }
            }
        };

    public static HeroDefinition GetById(int heroId)
    {
      HeroesById.TryGetValue(heroId, out HeroDefinition hero);
      return hero;
    }

    public static bool Contains(int heroId)
    {
      return HeroesById.ContainsKey(heroId);
    }

    public static IReadOnlyCollection<HeroDefinition> GetAll()
    {
      return HeroesById.Values;
    }
  }
}