using System.Collections.Generic;

namespace DM.Heroes
{
  public static class HeroDatabase
  {
    public static readonly IReadOnlyList<HeroDefinition> Heroes =
        new List<HeroDefinition>
        {
                new HeroDefinition
                {
                    Id = 1,
                    Name = "DAROOU",
                    Title = "",
                    Gender = HeroGender.Male,
                    PortraitName = "Daroou",

                    Resources = new HeroResources
                    {
                        Health = 100,
                        Stamina = 650,
                        Mana = 6
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 35,
                        Strength = 50,
                        Dexterity = 30,
                        Wisdom = 35,
                        Vitality = 45,
                        AntiMagic = 30,
                        AntiFire = 45
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 3, 0, 3, 0 },
                        Ninja = new[] { 0, 0, 0, 0 },
                        Priest = new[] { 0, 0, 0, 0 },
                        Wizard = new[] { 0, 0, 1, 1 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 6,
                        Y = 13,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 2,
                    Name = "HALK",
                    Title = "THE BARBARIAN",
                    Gender = HeroGender.Male,
                    PortraitName = "Halk",

                    Resources = new HeroResources
                    {
                        Health = 90,
                        Stamina = 750,
                        Mana = 0
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 40,
                        Strength = 55,
                        Dexterity = 43,
                        Wisdom = 30,
                        Vitality = 46,
                        AntiMagic = 38,
                        AntiFire = 48
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 4, 0, 4, 0 },
                        Ninja = new[] { 0, 0, 0, 0 },
                        Priest = new[] { 0, 0, 0, 0 },
                        Wizard = new[] { 0, 0, 0, 0 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 7,
                        Y = 9,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 3,
                    Name = "WU TSE",
                    Title = "SON OF HEAVEN",
                    Gender = HeroGender.Female,
                    PortraitName = "WuTse",

                    Resources = new HeroResources
                    {
                        Health = 45,
                        Stamina = 470,
                        Mana = 20
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 40,
                        Strength = 38,
                        Dexterity = 35,
                        Wisdom = 53,
                        Vitality = 45,
                        AntiMagic = 47,
                        AntiFire = 40
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 0, 0, 0, 0 },
                        Ninja = new[] { 1, 2, 0, 3 },
                        Priest = new[] { 2, 1, 4, 3 },
                        Wizard = new[] { 0, 0, 0, 0 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 7,
                        Y = 13,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 4,
                    Name = "AZIZI",
                    Title = "JOHARI",
                    Gender = HeroGender.Female,
                    PortraitName = "Azizi",

                    Resources = new HeroResources
                    {
                        Health = 61,
                        Stamina = 770,
                        Mana = 7
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 47,
                        Strength = 47,
                        Dexterity = 48,
                        Wisdom = 42,
                        Vitality = 45,
                        AntiMagic = 30,
                        AntiFire = 35
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 2, 1, 3, 0 },
                        Ninja = new[] { 2, 2, 3, 3 },
                        Priest = new[] { 0, 0, 0, 0 },
                        Wizard = new[] { 0, 0, 0, 0 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 7,
                        Y = 16,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 5,
                    Name = "LEIF",
                    Title = "THE VALIANT",
                    Gender = HeroGender.Male,
                    PortraitName = "Leif",

                    Resources = new HeroResources
                    {
                        Health = 75,
                        Stamina = 700,
                        Mana = 7
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 35,
                        Strength = 46,
                        Dexterity = 40,
                        Wisdom = 39,
                        Vitality = 50,
                        AntiMagic = 45,
                        AntiFire = 45
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 3, 2, 2, 0 },
                        Ninja = new[] { 0, 0, 0, 0 },
                        Priest = new[] { 0, 2, 1, 1 },
                        Wizard = new[] { 0, 0, 0, 0 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 8,
                        Y = 15,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 6,
                    Name = "ELIJA",
                    Title = "LION OF YAITOPYA",
                    Gender = HeroGender.Male,
                    PortraitName = "Elija",

                    Resources = new HeroResources
                    {
                        Health = 60,
                        Stamina = 580,
                        Mana = 22
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 50,
                        Strength = 42,
                        Dexterity = 40,
                        Wisdom = 42,
                        Vitality = 36,
                        AntiMagic = 53,
                        AntiFire = 40
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 1, 1, 2, 0 },
                        Ninja = new[] { 0, 0, 0, 0 },
                        Priest = new[] { 2, 1, 4, 2 },
                        Wizard = new[] { 0, 0, 0, 0 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 9,
                        Y = 7,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 7,
                    Name = "SYRA",
                    Title = "CHILD OF NATURE",
                    Gender = HeroGender.Female,
                    PortraitName = "Syra",

                    Resources = new HeroResources
                    {
                        Health = 53,
                        Stamina = 720,
                        Mana = 15
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 55,
                        Strength = 38,
                        Dexterity = 35,
                        Wisdom = 43,
                        Vitality = 45,
                        AntiMagic = 42,
                        AntiFire = 40
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 0, 0, 0, 0 },
                        Ninja = new[] { 0, 0, 0, 0 },
                        Priest = new[] { 0, 3, 1, 1 },
                        Wizard = new[] { 0, 2, 3, 3 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 9,
                        Y = 9,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 8,
                    Name = "TIGGY",
                    Title = "TAMAL",
                    Gender = HeroGender.Female,
                    PortraitName = "Tiggy",

                    Resources = new HeroResources
                    {
                        Health = 25,
                        Stamina = 450,
                        Mana = 35
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 45,
                        Strength = 30,
                        Dexterity = 45,
                        Wisdom = 50,
                        Vitality = 35,
                        AntiMagic = 59,
                        AntiFire = 40
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 0, 0, 0, 0 },
                        Ninja = new[] { 1, 3, 1, 1 },
                        Priest = new[] { 1, 0, 0, 0 },
                        Wizard = new[] { 2, 3, 3, 2 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 9,
                        Y = 13,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 9,
                    Name = "IAIDO",
                    Title = "RUYITO CHIBURI",
                    Gender = HeroGender.Male,
                    PortraitName = "Iaido",

                    Resources = new HeroResources
                    {
                        Health = 48,
                        Stamina = 650,
                        Mana = 11
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 40,
                        Strength = 43,
                        Dexterity = 55,
                        Wisdom = 40,
                        Vitality = 35,
                        AntiMagic = 45,
                        AntiFire = 50
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 2, 3, 0, 2 },
                        Ninja = new[] { 0, 0, 0, 0 },
                        Priest = new[] { 1, 1, 1, 2 },
                        Wizard = new[] { 0, 0, 0, 0 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 10,
                        Y = 4,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 10,
                    Name = "ZED",
                    Title = "DUKE OF BANVILLE",
                    Gender = HeroGender.Male,
                    PortraitName = "Zed",

                    Resources = new HeroResources
                    {
                        Health = 60,
                        Stamina = 600,
                        Mana = 10
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 58,
                        Strength = 40,
                        Dexterity = 40,
                        Wisdom = 40,
                        Vitality = 50,
                        AntiMagic = 40,
                        AntiFire = 40
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 2, 1, 1, 2 },
                        Ninja = new[] { 2, 1, 2, 1 },
                        Priest = new[] { 1, 2, 1, 1 },
                        Wizard = new[] { 1, 2, 1, 1 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 10,
                        Y = 5,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 11,
                    Name = "GANDO",
                    Title = "THURFOOT",
                    Gender = HeroGender.Male,
                    PortraitName = "Gando",

                    Resources = new HeroResources
                    {
                        Health = 39,
                        Stamina = 630,
                        Mana = 26
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 50,
                        Strength = 39,
                        Dexterity = 45,
                        Wisdom = 47,
                        Vitality = 33,
                        AntiMagic = 48,
                        AntiFire = 43
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 0, 0, 0, 0 },
                        Ninja = new[] { 3, 0, 2, 3 },
                        Priest = new[] { 0, 0, 0, 0 },
                        Wizard = new[] { 1, 2, 1, 2 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 11,
                        Y = 10,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 12,
                    Name = "STAMM",
                    Title = "BLADECASTER",
                    Gender = HeroGender.Male,
                    PortraitName = "Stamm",

                    Resources = new HeroResources
                    {
                        Health = 75,
                        Stamina = 800,
                        Mana = 0
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 35,
                        Strength = 52,
                        Dexterity = 43,
                        Wisdom = 35,
                        Vitality = 50,
                        AntiMagic = 35,
                        AntiFire = 55
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 3, 4, 2, 2 },
                        Ninja = new[] { 0, 0, 0, 0 },
                        Priest = new[] { 0, 0, 0, 0 },
                        Wizard = new[] { 0, 0, 0, 0 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 11,
                        Y = 15,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 13,
                    Name = "LINFLAS",
                    Title = "",
                    Gender = HeroGender.Male,
                    PortraitName = "Linflas",

                    Resources = new HeroResources
                    {
                        Health = 65,
                        Stamina = 500,
                        Mana = 12
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 45,
                        Strength = 45,
                        Dexterity = 45,
                        Wisdom = 47,
                        Vitality = 35,
                        AntiMagic = 50,
                        AntiFire = 35
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 0, 1, 2, 4 },
                        Ninja = new[] { 0, 0, 1, 0 },
                        Priest = new[] { 0, 1, 0, 0 },
                        Wizard = new[] { 0, 1, 2, 2 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 12,
                        Y = 9,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 14,
                    Name = "WUUF",
                    Title = "THE BIKA",
                    Gender = HeroGender.Female,
                    PortraitName = "Wuuf",

                    Resources = new HeroResources
                    {
                        Health = 40,
                        Stamina = 500,
                        Mana = 30
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 60,
                        Strength = 33,
                        Dexterity = 57,
                        Wisdom = 45,
                        Vitality = 40,
                        AntiMagic = 35,
                        AntiFire = 40
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 0, 0, 0, 0 },
                        Ninja = new[] { 1, 2, 3, 4 },
                        Priest = new[] { 0, 3, 2, 1 },
                        Wizard = new[] { 0, 0, 0, 0 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 12,
                        Y = 13,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 15,
                    Name = "LEYLA",
                    Title = "SHADOWSEEK",
                    Gender = HeroGender.Female,
                    PortraitName = "Leyla",

                    Resources = new HeroResources
                    {
                        Health = 48,
                        Stamina = 600,
                        Mana = 3
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 50,
                        Strength = 40,
                        Dexterity = 53,
                        Wisdom = 45,
                        Vitality = 47,
                        AntiMagic = 45,
                        AntiFire = 35
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 0, 0, 0, 0 },
                        Ninja = new[] { 3, 3, 3, 4 },
                        Priest = new[] { 0, 0, 0, 0 },
                        Wizard = new[] { 0, 0, 0, 0 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 13,
                        Y = 12,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 16,
                    Name = "MOPHUS",
                    Title = "THE HEALER",
                    Gender = HeroGender.Male,
                    PortraitName = "Mophus",

                    Resources = new HeroResources
                    {
                        Health = 55,
                        Stamina = 550,
                        Mana = 19
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 40,
                        Strength = 42,
                        Dexterity = 35,
                        Wisdom = 40,
                        Vitality = 48,
                        AntiMagic = 40,
                        AntiFire = 45
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 0, 0, 0, 0 },
                        Ninja = new[] { 0, 0, 0, 0 },
                        Priest = new[] { 2, 4, 3, 2 },
                        Wizard = new[] { 0, 0, 0, 0 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 13,
                        Y = 14,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 17,
                    Name = "CHANI",
                    Title = "SAYYADINA SIHAYA",
                    Gender = HeroGender.Female,
                    PortraitName = "Chani",

                    Resources = new HeroResources
                    {
                        Health = 47,
                        Stamina = 670,
                        Mana = 17
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 57,
                        Strength = 37,
                        Dexterity = 47,
                        Wisdom = 57,
                        Vitality = 37,
                        AntiMagic = 47,
                        AntiFire = 37
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 1, 3, 0, 2 },
                        Ninja = new[] { 0, 0, 0, 0 },
                        Priest = new[] { 0, 0, 0, 0 },
                        Wizard = new[] { 3, 2, 3, 1 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 14,
                        Y = 3,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 18,
                    Name = "BORIS",
                    Title = "WIZARD OF BALDOR",
                    Gender = HeroGender.Male,
                    PortraitName = "Boris",

                    Resources = new HeroResources
                    {
                        Health = 35,
                        Stamina = 650,
                        Mana = 28
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 25,
                        Strength = 35,
                        Dexterity = 45,
                        Wisdom = 55,
                        Vitality = 40,
                        AntiMagic = 45,
                        AntiFire = 40
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 0, 0, 0, 0 },
                        Ninja = new[] { 3, 2, 1, 0 },
                        Priest = new[] { 0, 0, 0, 0 },
                        Wizard = new[] { 2, 3, 3, 3 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 14,
                        Y = 6,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 19,
                    Name = "SONJA",
                    Title = "SHE DEVIL",
                    Gender = HeroGender.Female,
                    PortraitName = "Sonja",

                    Resources = new HeroResources
                    {
                        Health = 65,
                        Stamina = 700,
                        Mana = 2
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 40,
                        Strength = 54,
                        Dexterity = 45,
                        Wisdom = 39,
                        Vitality = 49,
                        AntiMagic = 40,
                        AntiFire = 40
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 3, 4, 2, 3 },
                        Ninja = new[] { 0, 0, 0, 0 },
                        Priest = new[] { 0, 0, 0, 0 },
                        Wizard = new[] { 0, 0, 0, 0 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 14,
                        Y = 12,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 20,
                    Name = "HAWK",
                    Title = "THE FEARLESS",
                    Gender = HeroGender.Male,
                    PortraitName = "Hawk",

                    Resources = new HeroResources
                    {
                        Health = 70,
                        Stamina = 850,
                        Mana = 10
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 40,
                        Strength = 45,
                        Dexterity = 35,
                        Wisdom = 38,
                        Vitality = 55,
                        AntiMagic = 35,
                        AntiFire = 35
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 2, 0, 0, 2 },
                        Ninja = new[] { 0, 0, 0, 0 },
                        Priest = new[] { 0, 3, 0, 3 },
                        Wizard = new[] { 0, 0, 0, 0 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 15,
                        Y = 4,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 21,
                    Name = "ALEX",
                    Title = "ANDER",
                    Gender = HeroGender.Male,
                    PortraitName = "Alex",

                    Resources = new HeroResources
                    {
                        Health = 50,
                        Stamina = 570,
                        Mana = 13
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 47,
                        Strength = 44,
                        Dexterity = 55,
                        Wisdom = 45,
                        Vitality = 40,
                        AntiMagic = 35,
                        AntiFire = 40
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 0, 0, 0, 0 },
                        Ninja = new[] { 3, 2, 3, 2 },
                        Priest = new[] { 0, 0, 0, 0 },
                        Wizard = new[] { 2, 2, 1, 2 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 16,
                        Y = 8,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 22,
                    Name = "HISSSSA",
                    Title = "LIZAR OF MAKAN",
                    Gender = HeroGender.Male,
                    PortraitName = "Hissssa",

                    Resources = new HeroResources
                    {
                        Health = 80,
                        Stamina = 610,
                        Mana = 5
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 40,
                        Strength = 58,
                        Dexterity = 48,
                        Wisdom = 35,
                        Vitality = 35,
                        AntiMagic = 43,
                        AntiFire = 55
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 4, 3, 0, 0 },
                        Ninja = new[] { 0, 3, 1, 0 },
                        Priest = new[] { 0, 0, 0, 0 },
                        Wizard = new[] { 0, 0, 0, 0 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 16,
                        Y = 14,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 23,
                    Name = "GOTHMOG",
                    Title = "",
                    Gender = HeroGender.Male,
                    PortraitName = "Gothmog",

                    Resources = new HeroResources
                    {
                        Health = 60,
                        Stamina = 550,
                        Mana = 18
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 30,
                        Strength = 40,
                        Dexterity = 35,
                        Wisdom = 48,
                        Vitality = 34,
                        AntiMagic = 50,
                        AntiFire = 59
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 0, 0, 0, 0 },
                        Ninja = new[] { 0, 0, 0, 0 },
                        Priest = new[] { 0, 0, 0, 0 },
                        Wizard = new[] { 4, 3, 2, 2 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 16,
                        Y = 17,
                        WallDirection = HeroWallDirection.North
                    }
                },

                new HeroDefinition
                {
                    Id = 24,
                    Name = "NABI",
                    Title = "THE PROPHET",
                    Gender = HeroGender.Male,
                    PortraitName = "Nabi",

                    Resources = new HeroResources
                    {
                        Health = 55,
                        Stamina = 650,
                        Mana = 13
                    },

                    Attributes = new HeroAttributes
                    {
                        Luck = 40,
                        Strength = 41,
                        Dexterity = 36,
                        Wisdom = 45,
                        Vitality = 45,
                        AntiMagic = 55,
                        AntiFire = 55
                    },

                    Skills = new HeroSkills
                    {
                        Fighter = new[] { 0, 0, 0, 0 },
                        Ninja = new[] { 0, 0, 0, 0 },
                        Priest = new[] { 1, 1, 4, 2 },
                        Wizard = new[] { 1, 1, 1, 1 }
                    },

                    Placement = new HeroPlacement
                    {
                        Level = 0,
                        X = 17,
                        Y = 9,
                        WallDirection = HeroWallDirection.North
                    }
                },

        };

    public static HeroDefinition GetById(int id)
    {
      foreach (HeroDefinition hero in Heroes)
      {
        if (hero.Id == id)
        {
          return hero;
        }
      }

      return null;
    }

    public static HeroDefinition GetByName(string name)
    {
      foreach (HeroDefinition hero in Heroes)
      {
        if (string.Equals(hero.Name, name, System.StringComparison.OrdinalIgnoreCase))
        {
          return hero;
        }
      }

      return null;
    }

    public static HeroDefinition GetByPlacement(
        int level,
        int x,
        int y,
        HeroWallDirection wallDirection)
    {
      foreach (HeroDefinition hero in Heroes)
      {
        if (hero.Placement.Level == level &&
            hero.Placement.X == x &&
            hero.Placement.Y == y &&
            hero.Placement.WallDirection == wallDirection)
        {
          return hero;
        }
      }

      return null;
    }
  }
}