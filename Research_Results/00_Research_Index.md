# CSBwin research index

Research-only notes for the Dungeon Master / Chaos Strikes Back mechanics implemented by this CSBwin tree. No gameplay code was changed.

## Identification

| Item | Value |
|---|---|
| Source tree | `C:\!!!Stick\Coder\DM\CSBWin-master\` |
| Version string | `CSB for Windows/Linux Version 15.9` (`CSBwin.rc`, string `IDS_VERSION`) |
| `graphics.dat` size | 271911 bytes |
| `graphics.dat` SHA-256 | `196cb349827e8926e2763fa61ecbe8e10810003045ef72a435b7da4dded56d8e` |
| Latest verification | 3 November 2026 |

`graphics.dat` in this tree is big-endian (first word is not `0x8001`). It contains 563 graphics. Graphic `0x22F` decompresses to 3086 bytes (`0xC0E`). Graphic `0x230` decompresses to 1256 bytes (`0x4E8`). Graphic `0x232` decompresses to 1826 bytes (`0x722`). All three sizes match the asserts in `ReadTablesFromGraphicsFile` (`CSBCode.cpp` 10569, 10560, and 10606). The 3 November check recomputed the SHA-256 above and decoded graphic `0x230`.

## Topics

| Status | Report |
|---|---|
| Complete | [01_Game_Time_and_Timers.md](01_Game_Time_and_Timers.md) |
| Complete | [02_Lighting_and_Darkness.md](02_Lighting_and_Darkness.md) |
| Complete | [03_Food_and_Water.md](03_Food_and_Water.md) |
| Complete | [04_Mana_Regeneration.md](04_Mana_Regeneration.md) |
| Complete | [05_Health_and_Stamina.md](05_Health_and_Stamina.md) |
| Complete | [06_Other_Time_Dependent_Mechanics.md](06_Other_Time_Dependent_Mechanics.md) |
| Complete | [07_Combat_Mechanics.md](07_Combat_Mechanics.md) |
| Complete | [08_Monster_AI_and_Behavior.md](08_Monster_AI_and_Behavior.md) |

## Key findings

- Simulation time is an integer tick, `d.Time`. One pass of `_MainLoop` processes due timers, then adds 1 to `d.Time`.
- The comments and the "1/6 second" timer-overflow message describe **10 vertical blanks per tick**. At 60 Hz that is 6 ticks per second. This port's Normal speed is **15 blanks × 17 ms = 255 ms** per tick.
- Sleep sets `d.Speed = 0`, so each emulated blank is a tick. The champion update then runs every 16 ticks instead of every 64.
- Pause sets `d.ClockRunning = 0`. `d.Time` does not advance.
- Viewport darkness is six palettes. Hand torches and `d.Brightness` are summed, then compared with `PaletteBrightness`.
- `Word1074` and `PaletteBrightness` were read from this `graphics.dat`. They match the tables published with CSBwin.
- Food and water change in `TenSecondUpdate`, on the same 64-tick (awake) or 16-tick (sleep) schedule as mana, stamina, and health. The eight food values were read from this `graphics.dat`.
- Mana gain requires `random112 < Wizard mastery + Priest mastery + current Wisdom`. `random112` is three bits of `d.Time`, not a random call. Wisdom changes how often regen succeeds. It does not change the amount.
- A legal step spends `3 * load / MaxLoad + 1` stamina and then blocks further steps for `TimeToMove` ticks.
- Melee damage is built from `DetermineThrowingDistance`, then scaled by `Byte19958 / 32`, then reduced by monster `defense08`. Dexterity enters only through `Quickness`. There is no critical-hit multiplier.
- Graphic `0x230` holds 44 attack rows. The seven combat arrays are 308 bytes. `Byte20090` is signed when stored in `word64`. Block is +36 and Parry is +29. The first Stab and Heal are −20.
- Unity will use CSBwin’s attribute-based Anti-Fire and Anti-Magic formula in `TAG016426`. The Atari ST compiler bug that ignored those attributes will not be reproduced. Other Atari/CSBwin differences are not decided.
- Graphic `0x22F` holds 27 `MONSTERDESC` records of 26 bytes, starting at decompressed offset 676. Hit points are `baseHealth * generator multiplier + STRandom() % (baseHealth/4 + 1)`, not a fixed stat.
- Pursuit is sight, smell, footprints, then a random legal step. There is no path search. `timePerMove` 255 never steps. Freeze Life postpones non-invincible groups by 4 ticks. Party sleep does not pause monster timers.

## Entry points

| System | Function | File |
|---|---|---|
| Main loop | `_MainLoop` | `CSBCode.cpp` |
| Virtual blank | `vblInterrupt`, `checkVBL` | `CSBCode.cpp`, `system.cpp` |
| Timer queue | `ProcessTimers`, `GameTimers::CheckForTimerEvent` | `CSBCode.cpp`, `Timer.cpp` |
| Champion clock | `TenSecondUpdate` | `Code17818.cpp` |
| Torch burn | `NinetySecondUpdate` | `CSBCode.cpp` |
| Palette | `SelectPaletteForLightLevel` | `CSBCode.cpp` |
| Light decay | `ProcessLightLevelTimer` | `Timer.cpp` |
| Movement | `MoveParty`, `TimeToMove` | `CSBCode.cpp` |
| Missiles | `LaunchMissile`, `MissileTimer` | `Magic.cpp`, `Timer.cpp` |
| Monsters | `Increment_Time_By_W52_Plus_Random` | `Monster.cpp` |
| Monster scheduler | `ProcessTimers29to41`, `InitialChecks` | `Monster.cpp` |
| Monster step and sight | `PossibleMove`, `Blocked` | `Monster.cpp` |
| Monster attack choice | `MonsterAttacks` | `Monster.cpp` |
| Monster graphic bits | `NextMonsterUpdateTime` | `CSBCode.cpp` |
| Physical attack | `_Attack`, `DeterminePhysicalAttackDamage` | `Attack.cpp` |
| Quickness and throw seed | `Quickness`, `DetermineThrowingDistance` | `Character.cpp` |
| Champion damage | `DamageCharacter`, `TAG01680a`, `TAG016426` | `Character.cpp` |
| Monster damage | `MonsterDamagesCharacter`, `DamageMonster` | `Monster.cpp`, `Code11f52.cpp` |
| Skill experience | `AdjustSkills`, `DetermineMastery` | `Magic.cpp`, `Code17818.cpp` |

## Outstanding questions

- The source comments match a 60 Hz machine with 10 blanks per tick. They do not say what a 50 Hz Atari did with the same 10-blank delay.
- `TenSecondUpdate` is commented "2.5 seconds while sleeping", which is 16 ticks at 1/6 second. This port also sets `d.Speed = 0`, so those 16 ticks last 16 blanks.
- Eating clamps food and water to −1023. The periodic update clamps them to −1024. Both are in the source.
- `SelectPaletteForLightLevel` weights five hand-torch slots. After four selection-sort passes, the fifth slot is not guaranteed to be the fifth-brightest torch.
- Monster `timePerMove`, attack delays, and the other descriptor fields for all 27 types are in report 08. Types 5 and 25 have no printed name in `MonsterName`.
- `FoodValue` covers eight items, `objNI_Apple` through `objNI_DragonSteak`. Other edible objects, if any, do not use this array.
- Weapon `uByte1`/`uByte2`/`word4` were not dumped. The 27 monster defense, dexterity, and attack bytes are in report 08.
- Position nibbles 0–3 are compass slots. Their screen names (top left, top right, bottom left, bottom right) depend on party facing and graphic `0x22E`, which was not decoded.
- Fire and poison resistance nibbles are stored. Every damage call site was not traced.
- `word2` bit 3 and the fear 0/3 test versus the Atari binary remain open. Timer 29 calls `Try_Directions_D5_To_D4` with a random direction. See report 08.
- The shoot assignment that discards `2 * (word4 + shoot mastery)` is confirmed in CSBwin. It was not compared with an Atari binary.
- `word64` is assigned and cleared in this port. The `CHARDESC` comment says the original added and subtracted `Byte20090`. Unity has not chosen between those two.
