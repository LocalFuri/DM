# 07 — Combat mechanics

**CONFIRMED** in this CSBwin tree unless a paragraph says otherwise. Arithmetic is signed 16-bit (`sw`) with integer division. `STRandom(n)` is `(STRandom() & 0xffff) % n`, so 0 through n−1 (`CSBCode.cpp` 1232–1249).

Champion swings enter `_Attack` (`Attack.cpp` 1608). The physical cases call `AttackWithPhysicalForce` (`Attack.cpp` 1038), which calls `DeterminePhysicalAttackDamage` (`Attack.cpp` 535). Monster swings enter `MonsterDamagesCharacter` (`Monster.cpp` 4509). Neither path has a critical-hit multiplier.

This note was completed on 3 November 2026. Graphic `0x230` was decompressed with the CSBwin LZW path and the seven attack tables were read from the packed struct layout. No CSBwin gameplay source was changed.

## Unity design decision

**Approved.** Dungeon Master Unity will use CSBwin’s corrected attribute-based Anti-Fire and Anti-Magic damage calculations.

`TAG016426` (`Character.cpp` 2059) uses the full attribute:

```
factor = 170 - currentAttribute
if factor < 16: result = damage / 8
else:           result = damage * factor / 128
```

Attribute 6 is Anti-Fire, used when `DamageCharacter` class `P4 == 1`. Attribute 5 is Anti-Magic, used when `P4 == 5` (`CSB.h` 640–648, `Character.cpp` 2791–2801). `ScaledMultiply(damage, 7, factor)` is `(damage * factor) >> 7` (`Character.cpp` 22–24).

The comment in `TAG016426` quotes Christophe Fontanel: the Atari ST compiler computed `170 - 0`, so those attributes did nothing in the Atari ST binaries. The Amiga binary used the attribute. This tree follows the C source. Unity will follow this tree. The Atari ST compiler bug will not be reproduced.

No other Atari-versus-CSBwin difference is decided. That includes `word64` being assigned and cleared here instead of added and subtracted, and the shoot assignment that discards `2 * (word4 + shoot mastery)`.

## Graphic 0x230

`graphics.dat` in this tree is 271911 bytes. SHA-256 `196cb349827e8926e2763fa61ecbe8e10810003045ef72a435b7da4dded56d8e`. The file is big-endian (first word is not `0x8001`). `ReadGraphicsIndex` (`Graphics.cpp` 2068) reports 563 graphics. On the little-endian build, `LE16` (`CSBTypes.h` 139) byte-swaps the index.

Graphic `0x230` is compressed: 1055 bytes in the file, 1256 bytes (`0x4E8`) after `LZWExpand` (`Graphics.cpp` 2778) and the `0x90` repeat step in `TAG022eec` (`Graphics.cpp` 2963). `ReadAndExpandGraphic` skips LZW only when the compressed and decompressed sizes are equal (`Graphics.cpp` 2711). They are not equal for `0x230`. The decompressed length matches the assert in `ReadTablesFromGraphicsFile` (`CSBCode.cpp` 10560).

The same decoder reproduced the other asserts used by that function:

| Graphic | Decompressed | Assert |
|---|---:|---|
| `0x22F` | 3086 | `0xC0E` (`CSBCode.cpp` 10569) |
| `0x230` | 1256 | `0x4E8` (`CSBCode.cpp` 10560) |
| `0x232` | 1826 | `0x722` (`CSBCode.cpp` 10606) |

The loader copies the graphic onto `(ui8 *)&d.Byte20244 + 2` (`CSBCode.cpp` 10561). `Data.h` and `CSB.h` are `#pragma pack(1)`. From that address the bytes are:

| Bytes | Field |
|---:|---|
| 8 | `Word20242[4]` |
| 40 | five `RectPos` (8 bytes each) |
| 16 | `Byte20194[16]` |
| 44 | `experienceForAttacking` |
| 44 | `SkillNumber` |
| 44 | `Byte20090` |
| 44 | `Byte20046` |
| 44 | `Byte20002` |
| 44 | `Byte19958` |
| 44 | `Byte19914` |
| 300 | `AttackNames` |

The seven tables therefore start at decompressed offset 64. Parsing `AttackNames` the way `GetAttackTypeName` does (`CSBCode.cpp` 8969) produced 44 C strings in `ATTACKTYPE` order (`CSB.h` 589–635). That is the layout check. The column titles in `AsciiDump.cpp` 1823–1836 were not used. Those titles call `experienceForAttacking` “Time” and `Byte19914` “Probability”. The functions below are what the game runs.

`_Attack` reads experience, stamina, `P7`, `P8`, and cooldown through a `(UI8)` cast (`Attack.cpp` 1697–1711 and 1122–1128). None of those five tables contains a byte above 127, so the signed `i8` and the unsigned cast are the same number. `word64 = Byte20090[attack]` (`Character.cpp` 4984) assigns an `i8` to an `i16`, so bytes 236–252 are negative. Those signs are what armor uses.

## Attack table

308 values: 44 attacks times the seven arrays. `exp` is `experienceForAttacking`. `skill` is `SkillNumber`. `armor` is signed `Byte20090`, copied into `CHARDESC::word64`. `stam` is `Byte20046` before the extra `STRandomBool()`. `P7` is `Byte20002`. `P8` is `Byte19958`. `cool` is `Byte19914`, in game ticks.

| # | Name in graphic | exp | skill | armor | stam | P7 | P8 | cool |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 0 | N | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| 1 | BLOCK | 8 | 7 | +36 | 4 | 22 | 15 | 6 |
| 2 | CHOP | 10 | 6 | 0 | 8 | 48 | 48 | 8 |
| 3 | X | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| 4 | BLOW HORN | 0 | 14 | −4 | 1 | 0 | 0 | 6 |
| 5 | FLIP | 0 | 12 | −10 | 0 | 0 | 0 | 3 |
| 6 | PUNCH | 8 | 9 | −10 | 1 | 38 | 32 | 1 |
| 7 | KICK | 13 | 9 | −5 | 3 | 28 | 48 | 5 |
| 8 | WAR CRY | 7 | 14 | +4 | 1 | 0 | 0 | 3 |
| 9 | STAB | 15 | 9 | −20 | 3 | 30 | 48 | 5 |
| 10 | CLIMB DOWN | 15 | 8 | −15 | 40 | 0 | 0 | 35 |
| 11 | FREEZE LIFE | 22 | 14 | −10 | 3 | 0 | 0 | 20 |
| 12 | HIT | 10 | 9 | +16 | 3 | 20 | 20 | 4 |
| 13 | SWING | 6 | 4 | +5 | 2 | 32 | 16 | 6 |
| 14 | STAB | 12 | 5 | −15 | 4 | 42 | 60 | 10 |
| 15 | THRUST | 19 | 5 | −17 | 13 | 57 | 66 | 16 |
| 16 | JAB | 11 | 5 | −5 | 3 | 70 | 8 | 2 |
| 17 | PARRY | 17 | 7 | +29 | 1 | 18 | 8 | 18 |
| 18 | HACK | 9 | 4 | +10 | 6 | 27 | 25 | 8 |
| 19 | BERZERK | 40 | 4 | −10 | 26 | 46 | 96 | 30 |
| 20 | FIREBALL | 35 | 16 | −7 | 5 | 0 | 0 | 42 |
| 21 | DISPELL | 25 | 17 | −7 | 2 | 0 | 0 | 31 |
| 22 | CONFUSE | 0 | 14 | −7 | 2 | 0 | 0 | 10 |
| 23 | LIGHTNING | 30 | 17 | −7 | 4 | 0 | 0 | 38 |
| 24 | DISRUPT | 10 | 17 | −7 | 5 | 46 | 55 | 9 |
| 25 | MELEE | 24 | 6 | −5 | 17 | 64 | 60 | 20 |
| 26 | X | 0 | 8 | −15 | 1 | 0 | 0 | 10 |
| 27 | INVOKE | 25 | 3 | −9 | 2 | 0 | 0 | 16 |
| 28 | SLASH | 9 | 4 | +4 | 2 | 26 | 16 | 4 |
| 29 | CLEAVE | 12 | 4 | 0 | 10 | 40 | 48 | 12 |
| 30 | BASH | 11 | 6 | 0 | 9 | 32 | 50 | 20 |
| 31 | STUN | 10 | 6 | +5 | 2 | 50 | 16 | 7 |
| 32 | SHOOT | 9 | 11 | −15 | 3 | 0 | 0 | 14 |
| 33 | SPELLSHIELD | 20 | 15 | −7 | 1 | 0 | 0 | 30 |
| 34 | FIRESHIELD | 20 | 15 | −7 | 2 | 0 | 0 | 35 |
| 35 | FLUXCAGE | 12 | 3 | +8 | 6 | 0 | 0 | 2 |
| 36 | HEAL | 0 | 13 | −20 | 1 | 0 | 0 | 19 |
| 37 | CALM | 0 | 14 | −5 | 1 | 0 | 0 | 9 |
| 38 | LIGHT | 20 | 17 | 0 | 3 | 0 | 0 | 10 |
| 39 | WINDOW | 30 | 18 | −15 | 2 | 0 | 0 | 15 |
| 40 | SPIT | 25 | 16 | −7 | 3 | 0 | 0 | 22 |
| 41 | BRANDISH | 0 | 14 | −4 | 2 | 0 | 0 | 10 |
| 42 | THROW | 5 | 10 | 0 | 0 | 0 | 0 | 0 |
| 43 | FUSE | 1 | 3 | +8 | 2 | 0 | 0 | 2 |

Raw `Byte20090` bytes, in attack order, for the values that are negative as `i8`: 252, 246, 246, 251, 236, 241, 246, 241, 239, 251, 246, 249, 249, 249, 249, 249, 251, 241, 247, 241, 249, 249, 236, 251, 241, 249, 252. Those are attacks 4, 5, 6, 7, 9, 10, 11, 14, 15, 16, 19, 20, 21, 22, 23, 24, 25, 26, 27, 32, 33, 34, 36, 37, 39, 40, and 41. Every other `Byte20090` byte is the non-negative armor number in the table.

Enum cross-check (`CSB.h` 589–635): 0 Juggle is stored as `N`, 3 Speed as `X`, and 26 Pray as `X`. The source comments already say those three are not used in CSB. Rows 9 and 14 are both named `STAB` (`atk_STAB1` and `atk_STAB2`). Chop’s `P7` and `P8` are both 48, which matches the published CSBwin attack trace for Chop.

### The seven arrays alone

Each list is attack 0 through attack 43.

`experienceForAttacking`: 0, 8, 10, 0, 0, 0, 8, 13, 7, 15, 15, 22, 10, 6, 12, 19, 11, 17, 9, 40, 35, 25, 0, 30, 10, 24, 0, 25, 9, 12, 11, 10, 9, 20, 20, 12, 0, 0, 20, 30, 25, 0, 5, 1.

`SkillNumber`: 0, 7, 6, 0, 14, 12, 9, 9, 14, 9, 8, 14, 9, 4, 5, 5, 5, 7, 4, 4, 16, 17, 14, 17, 17, 6, 8, 3, 4, 4, 6, 6, 11, 15, 15, 3, 13, 14, 17, 18, 16, 14, 10, 3.

`Byte20090` as signed armor: 0, +36, 0, 0, −4, −10, −10, −5, +4, −20, −15, −10, +16, +5, −15, −17, −5, +29, +10, −10, −7, −7, −7, −7, −7, −5, −15, −9, +4, 0, 0, +5, −15, −7, −7, +8, −20, −5, 0, −15, −7, −4, 0, +8.

`Byte20046`: 0, 4, 8, 0, 1, 0, 1, 3, 1, 3, 40, 3, 3, 2, 4, 13, 3, 1, 6, 26, 5, 2, 2, 4, 5, 17, 1, 2, 2, 10, 9, 2, 3, 1, 2, 6, 1, 1, 3, 2, 3, 2, 0, 2.

`Byte20002`: 0, 22, 48, 0, 0, 0, 38, 28, 0, 30, 0, 0, 20, 32, 42, 57, 70, 18, 27, 46, 0, 0, 0, 0, 46, 64, 0, 0, 26, 40, 32, 50, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.

`Byte19958`: 0, 15, 48, 0, 0, 0, 32, 48, 0, 48, 0, 0, 20, 16, 60, 66, 8, 8, 25, 96, 0, 0, 0, 0, 55, 60, 0, 0, 16, 48, 50, 16, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.

`Byte19914`: 0, 6, 8, 0, 6, 3, 1, 5, 3, 5, 35, 20, 4, 6, 10, 16, 2, 18, 8, 30, 42, 31, 10, 38, 9, 20, 10, 16, 4, 12, 20, 7, 14, 30, 35, 2, 19, 9, 10, 15, 22, 10, 0, 2.

This source only names four skills (`CSBCode.cpp` 353–356): 0 Fighter, 1 Ninja, 2 Priest, 3 Wizard. `CHARDESC` groups the hidden subskills (`CSB.h` 2611–2614): 4–7 Fighter, 8–11 Ninja, 12–15 Priest, 16–19 Wizard. The damage code does not print subskill names. Swing, Hack, Berzerk, Slash, and Cleave use skill 4. The second Stab, Thrust, and Jab use skill 5. Chop and Melee use skill 6. Block and Parry use skill 7.

## How each column is used

| Array | Consumer |
|---|---|
| `Byte19914` | `disableTime` in `_Attack` (`Attack.cpp` 1697). Comment in `Data.h` 630: small integers, largest 38. The extracted maximum is Fireball’s 42. |
| `Byte20046` | Stamina base. `_Attack` adds `STRandomBool()` (`Attack.cpp` 1704). |
| `experienceForAttacking` | Flat skill experience after the action (`Attack.cpp` 1711), then `AdjustSkills`. |
| `SkillNumber` | Which of the 20 skills receives that flat grant (`Attack.cpp` 1698). |
| `Byte20002` | Passed as `P7` (`Attack.cpp` 1122). Used only as `IsCharacterLucky(champion, 75 - P7)`. Not a Strength check. |
| `Byte19958` | Passed as `P8` (`Attack.cpp` 1128). Melee power: the throwing-distance seed is multiplied by `P8 / 32`. |
| `Byte20090` | Copied into `word64` for the cooldown (`Character.cpp` 4984). Added into armor by `TAG01680a`. |

`P8 / 32` for the physical rows: Jab is 8/32. Block is 15/32. Swing, Slash, and Stun are 16/32. Hit is 20/32. Hack is 25/32. Punch is 32/32. Chop, Kick, the first Stab, and Cleave are 48/32. Bash is 50/32. Disrupt is 55/32. The second Stab and Melee are 60/32. Thrust is 66/32. Berzerk is 96/32. Parry is 8/32.

For a physical swing the luck target is `75 - P7`. Jab’s 70 leaves 5. Thrust’s 57 leaves 18. Melee’s 64 leaves 11. Chop’s 48 leaves 27. Swing’s 32 leaves 43. A zero `P7` leaves 75.

## 1. Weapon damage

Melee damage is not `weapon.word4`. The seed is `DetermineThrowingDistance(champion, hand 1)` (`Character.cpp` 2282). A seed of 0 takes the weak-hit branch.

```
base     = current Strength + (STRandom() & 15)          // 0..15
weight   = object weight in the weapon hand
lightMax = MaxLoad / 16
if weight <= lightMax:
    base = base + weight - 12
else if weight <= (lightMax - 12) / 2 + lightMax:
    base = base + (weight - lightMax) / 2
else:
    base = base - 2 * (weight - that midpoint)
if the object is a weapon:
    base += WEAPONDESC.uByte2
    masteryBonus = 0
    class = WEAPONDESC.uByte1
    if class is 0 or 2:  masteryBonus += DetermineMastery(skill 4)
    if class is 1..15:   masteryBonus += DetermineMastery(skill 10)
    if class is 16..111: masteryBonus += DetermineMastery(skill 11)
    base += 2 * masteryBonus
base = StaminaAdjustedAttribute(base)
if that hand's injury bit is set: base /= 2
seed = clamp(0, base / 2, 100)
```

On a non-zero seed (`Attack.cpp` 663–706):

```
power = seed + (random & 0xffff) % (seed / 2 + 1)
power = power * Byte19958[attack] / 32
armor = STRandom(32) + monster.defense08 + levelDifficulty
if weapon is Diamond Edge (objNI_DiamondEdge): armor -= armor / 4
if weapon is Executioner (objNI_Executioner):  armor -= armor / 8
score = power + STRandom(32) - armor
```

`levelDifficulty` is `LEVELDESC::experienceMultiplier()`, bits 12–15 of the level descriptor (`CSB.h` 2159). Diamond Edge and Executioner reduce that armor term only. They do not multiply final hit points. They are skipped when the seed is 0, because that block never runs.

If the seed is 0 or `score <= 1` (`Attack.cpp` 714–796): one chance in four the swing stops (stamina 2 or 3, damage 0). Otherwise a small positive `D7` is built from `STRandom0_3`, sometimes `STRandom(16)`, and sometimes `Larger(0, STRandom(16) + score)`. The trace string on line 793 says `max(score, random(16))`. The code adds `Larger(0, random(16) + score)`.

Then every swing that was not aborted (`Attack.cpp` 798–874):

```
D7 /= 2
D7 += STRandom0_3() + (D7 == 0 ? 0 : (random & 0xffff) % D7)
if D7 != 0: D7 += (random & 0xffff) % D7
D7 /= 4
D7 += STRandom0_3() + 1
if weapon is Vorpal Blade and the monster is material:
    D7 /= 2
    if D7 == 0: abort (stamina 2 or 3, damage 0)
if STRandom(64) < DetermineMastery(attack skill): D7 += 10
if D7 < 0: D7 = 0
hit points = D7
```

`DamageMonster` (`Code11f52.cpp` 1067) subtracts that value from one monster’s hit points. There is no second armor roll. Invincible monsters ignore it. A non-material monster takes 0 from a normal physical swing. Vorpal Blade or `atk_DISRUPT` is allowed into the formula. `AttackWithPhysicalForce` returns 0 immediately if Disrupt is used on a material monster (`Attack.cpp` 1108–1119). Against a non-material monster the Vorpal halving does not run, because that halving requires a material target. Disrupt’s `P8` is 55, so a legal Disrupt swing scales the seed by 55/32.

`FlashAttackDamage` (`Attack.cpp` 939) only picks a larger graphic when the displayed number is above 15 or above 40.

**Shoot.** `Attack.cpp` 1878–1889 computes `2 * (launcher.word4 + DetermineMastery(skill 11))` and then overwrites it:

```
pParam->attdep.shoot.damage = 2 * (pWeapon_18->word4 + w_42);
pParam->attdep.shoot.damage = D4W;
```

`D4W` is the launcher’s `uByte1`, minus 16 for classes 16–31 and minus 32 for classes 32–47. `LaunchMissile` stores that byte as `damageRemaining` (`Magic.cpp` 39). Hit points are later `DetermineMagicDamage` (`Code11f52.cpp` 1581), which uses range left, weapon `uByte3`, and half the object weight. `damageRemaining` only changes a subtraction of `32 - damageRemaining / 8`. The `word4 + mastery` product never reaches the missile. Whether the Atari binary did the same overwrite was not checked. Shoot’s table row is experience 9, skill 11, armor −15, stamina 3, cooldown 14. Its `P7` and `P8` are 0 because Shoot does not call `DeterminePhysicalAttackDamage`.

**Throw.** `ThrowByCharacter` (`Mouse.cpp` 683) spends stamina from object weight, waits 4 ticks if no other delay was supplied, and grants skill 10 some experience from inside that function. Missile `damageRemaining` is `clamp(40, 8 * throw mastery + (STRandom() & 31), 200)`. Range is throwing distance plus weapon `uByte3`, `STRandom(16)`, half the throwing distance, and throw mastery. Impact hit points again come from `DetermineMagicDamage`. The table row is experience 5, skill 10, armor 0, stamina 0, cooldown 0. `_Attack` forces `disableTime` to −1 before the throw and then sets it back to 0 after `ThrowByCharacter` has posted its own timer (`Attack.cpp` 2075–2113), so the table cooldown is not the timer that runs. Table stamina 0 means `_Attack` does not call `AdjustStamina` again.

**Missiles against monsters.** Hit points are `(64 * DetermineMagicDamage) / defense08`, with defense forced to at least 1, plus `DeterminePoisonDamage` (`Code11f52.cpp` 2029–2047). That is division by defense. Melee subtracts defense inside the roll. Non-material monsters ignore missiles other than Dispel.

**Spells.** `CharacterThrowsSpell` (`Magic.cpp` 453) launches with damage byte 90. Mana required for wizard skills 16, 17, 18, 19, and major skill 3 is `7 - min(6, mastery)` (`Attack.cpp` 1720–1726). A spell that fails the mana test inside `AttackWithSpell` halves `experienceGained`. Fireball, Dispell, Lightning, and Spit use that path. Their table experience values are 35, 25, 30, and 25. Their cooldowns are 42, 31, 38, and 22.

## 2. Hit probability and Dexterity

Dexterity enters only through `Quickness` (`Character.cpp` 2229). The function comment matches the arithmetic:

```
q = current Dexterity + STRandom(8)
q = q - ((q / 2) * load) / MaxLoad
if PartySleeping: q /= 2
q /= 2
q = clamp(STRandom(8) + 1, q, 100 - STRandom(8))
```

`MaxLoad` (`Code11f52.cpp` 4341) is `StaminaAdjustedAttribute(8 * current Strength + 100)`, then a wound cut, then +1/16 if Elven Boots are on the feet, then +9, then rounded down to a multiple of 10. Strength therefore changes to-hit only by changing encumbrance.

A physical swing hits (`Attack.cpp` 607–619) unless all three of these are true:

- `Quickness <= STRandom(32) + monster.dexterity12 + levelDifficulty - 16`
- `STRandom0_3() != 0` (three failures in four)
- `IsCharacterLucky(champion, 75 - Byte20002[attack])` is false

`dexterity12` is monster byte `uByte8[4]` (`AsciiDump.cpp` 1802). `defense08` is a different byte and is not in this compare. A forum trace of this function calls `uByte8[4]` “defence”. This source names it dexterity and uses it as the to-hit target. `defense08` is the armor term in the damage roll.

`IsCharacterLucky` (`Character.cpp` 2185): immediate true if a coin flip succeeds and `STRandom(100) > luckNeeded`, without changing Luck. Otherwise compare `STRandom(current Luck)` (0 if Luck is 0) with `luckNeeded`. Success lowers current Luck by 2. Failure raises it by 2. Both are clamped to the attribute’s minimum and maximum.

A miss here still returns 1 from `AttackWithPhysicalForce` (`Attack.cpp` 1172). The flat attack experience is not halved. Only “no monster”, “can’t reach”, and illegal Disrupt return 0, and those do halve experience and cooldown (`Attack.cpp` 1812–1816).

Monster swings (`Monster.cpp` 4524–4541) use the same shape with two differences. The level term is `2 * experienceMultiplier`. The champion dodges when quickness is at least the target and `STRandom0_3() != 0`, or when `IsCharacterLucky(champion, 60)` is true. `WakeUp()` runs first and sets `PartySleeping = 0` (`CSBCode.cpp` 7418–7422), so this dodge does not see the sleep penalty inside `Quickness`.

## 3. Armor and damage reduction

Monster defense against a champion’s melee swing is the subtraction in section 1: `STRandom(32) + defense08 + levelDifficulty`, then Diamond Edge or Executioner. Missiles divide by `defense08` instead.

Champion armor is `TAG01680a` (`Character.cpp` 2409), averaged over the body slots in the hit mask, then applied in `DamageCharacter` (`Character.cpp` 2724).

Per slot, the defense total adds:

- a random `0 .. vitality/8` term, halved when the caller sets the high bit of the slot index
- `word64` (the signed attack-table value stored for the current cooldown)
- `shieldStrength` (the character’s shield potion)
- `PartyShield`
- a contribution from a shield held in either hand, if clothing `uByte2` has bit `0x80`
- worn clothing on that slot: `TAG009470` returns `uByte1`, or `uByte1 * ((uByte2 & 7) + 4) / 8` when scaled (`Character.cpp` 167–174)
- an injury on that slot subtracts `STRandom0_3() + 8`
- if `PartySleeping` is still set, the total is halved

The result is clamped to 0..100 after a final divide by 2.

`DamageCharacter` then depends on `P4`, the damage class. Monster melee passes `MONSTERDESC.uByte22[2]` (`Monster.cpp` 4579).

| `P4` | Effect |
|---|---|
| 0 | No armor. Raw damage is queued. Stamina collapse uses this. |
| 6 | Wisdom: `damage * (115 - current Wisdom) / 64`. The armor average is computed and then not used. |
| 5 | `TAG016426` with Anti-Magic (attribute 5), then subtract `SpellShield`. |
| 1 | `TAG016426` with Anti-Fire (attribute 6), then subtract `FireShield`. |
| 2 | Armor average is halved, then the physical formula. |
| other, including 3 | Physical formula. |

Physical formula, after a non-positive result has already returned 0:

```
damage = damage * (130 - averageArmor) / 64
```

`ScaledMultiply(a, 6, b)` is `(a * b) >> 6` (`Character.cpp` 22–24).

If the remaining damage exceeds a Vitality roll, injury bits are set. `PendingDamage` is what actually changes hit points later.

`word64` in this port is assigned, not accumulated (`Character.cpp` 4984, `Timer.cpp` 2636). Paul’s comment on `CHARDESC::word64` (`CSB.h` 2577–2598) says the original added `Byte20090` at the start of an attack and subtracted it when the cooldown ended. Attacks with no cooldown never subtracted, so the word could overflow. This tree sets the word to the table byte and clears it to 0 when any attack cooldown ends.

The extracted signs matter. Block is +36 and Parry is +29, so those recoveries raise armor. The first Stab is −20, Heal is −20, Thrust is −17, the second Stab, Shoot, Climb Down, Window, and the unused Pray row are −15, and Berzerk, Punch, Flip, and Freeze Life are −10. Most spells are −7. Chop, Bash, Cleave, Throw, and Light are 0. While `word64` is active, `TAG01680a` adds it before `damage * (130 - armor) / 64`.

## 4. Strength

Current Strength is not compared with `Byte20002`. That byte only changes the luck threshold `75 - P7`.

Strength is an input to `DetermineThrowingDistance`, so it scales the melee damage seed, throw range, and the force passed to `HitDoor` for Bash, Hack, Berzerk, Kick, Swing, and Chop on a breakable door (`Attack.cpp` 1779). One point of Strength adds one point before the final `/ 2` and the cap of 100, before weight, skill, stamina, and injury. Paul Stevens’ throwing-distance note says the same: about half a point of seed per point of Strength, with no further gain once the seed is 100.

Strength also sets `MaxLoad`, which changes the Dexterity load penalty. `StaminaAdjustedAttribute` (`Code11f52.cpp` 4314) is applied to both the damage seed and max load. At or above half of maximum stamina the value is unchanged. Below half:

```
result = base/2 + (base/2) * stamina / (MaxStamina/2)
```

At 0 stamina the result is half the base.

An injured weapon hand halves the throwing seed (`Character.cpp` 2380–2389). Hand bit 1 is the shield hand. Hand bit 2 is the weapon hand.

## 5. Critical hits

No function, constant, or damage branch is a critical hit.

The nearest special cases, all already in the formulas:

- One chance in four to hit anyway when Quickness fails (`STRandom0_3() == 0`).
- `IsCharacterLucky` as a third to-hit chance, and as a monster-attack dodge at threshold 60.
- `STRandom(64) < mastery` adds a flat 10 hit points. It does not double damage.
- Vorpal Blade halves damage against material monsters and can turn that into a miss.
- Diamond Edge removes one quarter of the monster armor term. Executioner removes one eighth.
- The on-screen damage flash changes art above 15 and above 40. Those thresholds are not used in the hit-point math.

## 6. Attack cooldowns

`disableTime` starts as `Byte19914[attack]` (`Attack.cpp` 1697). A physical attempt that returns 0 halves it. Spell Shield or Fire Shield failure also halves it. Climb Down’s table value is 35, and the case sets it to 0 when the move is not an empty pit (`Attack.cpp` 2013–2015). Throw forces it to 0 after `ThrowByCharacter` has already scheduled its own timer.

`DisableCharacterAction` (`Magic.cpp` 485) posts timer `TT_11` at `d.Time + delay`. If a busy timer already exists and the new end is later, it adds half the remaining old delay. If the old timer ends later, the new end is the old end plus half the new delay. The old timer is deleted. The character gets `CHARFLAG_attackDisable`, which shades the attack button (`Character.cpp` 4889).

`EnableCharacterAction` (`Timer.cpp` 2621) runs when that timer expires. It clears the disable flag and sets `word64` to 0.

One game tick is one `_MainLoop` pass. `ProcessTimers` calls that pass “1/6 second” (`CSBCode.cpp` 6756). Report 01 is the clock reference. A table value of 6 is six ticks. The longest extracted cooldowns are Fireball 42, Lightning 38, Fireshield 35, Climb Down 35, Dispell 31, Berzerk 30, and Spellshield 30. Punch is 1. Fluxcage and Fuse are 2. Throw’s table value is 0.

## 7. Stamina

A physical swing pays stamina twice.

Inside `DeterminePhysicalAttackDamage`, before `DamageMonster`:

| Result | `staminaAdjust` |
|---|---|
| Quickness miss, weak-hit miss, or Vorpal reduced to 0 | `2 + STRandomBool()` → 2 or 3 |
| Damage computed | `4 + STRandom0_3()` → 4..7 |
| Non-material whiff that dealt 0 | `2 + STRandomBool()` → 2 or 3 |

`PhysicalAttackFilter` calls `AdjustStamina` with that value (`Attack.cpp` 497–500).

After the attack switch, `_Attack` always calls `AdjustStamina(champion, Byte20046[type] + STRandomBool())` when that sum is non-zero (`Attack.cpp` 2126–2128). The two costs add.

Extracted bases, before the extra 0 or 1: Climb Down 40, Berzerk 26, Melee 17, Thrust 13, Cleave 10, Bash 9, Chop 8, Hack and Fluxcage 6, Fireball and Disrupt 5, Block and the second Stab 4. Flip and Throw are 0, so `_Attack` adds no table stamina for them. Flip still pays the physical-path stamina if it is not in the physical switch. Flip is not a physical case. It only pays `Byte20046`, which is 0.

`AdjustStamina` (`Code11f52.cpp` 4477) subtracts the cost. If the result is negative, stamina becomes 0 and `DamageCharacter` is called with half the deficit, mask 0, class 0. That damage ignores armor. Report 05 covers regeneration. This report only covers the attack spend.

Throwing uses a different cost, `TAG016382` (`Mouse.cpp` 663): half the weight, limited to 1..10, plus half of each further 10 weight units.

## 8. Experience

There is no kill bonus in `DamageMonster`. Experience is skill experience.

`DetermineMastery` (`Code17818.cpp` 13), matching Paul Stevens’ mastery table:

- Sleeping party: mastery 1.
- Subskill: average the subskill experience (plus temp adjust) with its major skill, using `>> 1`.
- Start at 1. While experience is at least 500, add 1 and shift experience right by 1. So 0–499 is 1, 500–999 is 2, 1000–1999 is 3, and so on.
- Firestaff and a few worn items add 1 or 2 unless the caller sets the ignore-possessions flag.

On a physical swing that produces a damage number, including 0 for a non-material whiff (`Attack.cpp` 882–888):

```
skillAdjust = hitPoints * monster.word16_8_11() / 16 + 3
```

`word16_8_11` is bits 8–11 of monster `word16` (`CSB.h` 2391). `AdjustSkills` applies it with reason `ASW_PhysicalAttack`. This is in addition to the flat `experienceForAttacking` value.

`_Attack` then adds `experienceForAttacking[type]` to `SkillNumber[type]`, unless that byte is 0. A 0 return from the physical, spell, or shield path reduces that flat grant (half, or one quarter for a failed shield).

War Cry, Calm, Brandish, Blow Horn, and Confuse also add their own amounts inside `WarCryEtc` (`Attack.cpp` 1432–1529). The internal grants are War Cry 12, Calm 35, Brandish 30, Blow Horn 20, and Confuse 45, halved when the fear check fails. The flat table grant is added afterward when it is non-zero. From the extracted table, only War Cry has a non-zero flat grant among those five: 7, on skill 14. Calm, Brandish, Blow Horn, and Confuse have table experience 0, so their only grant is the `WarCryEtc` amount. War Cry receives both.

Heal’s table experience is 0. The Heal case replaces it when hit points are missing: it sets the grant to 2, then adds 2 for every healed chunk (`Attack.cpp` 1959–1967). If the champion is already at maximum hit points, the grant stays 0.

`AdjustSkills` (`Magic.cpp` 189) then:

- For skills 4 through 11, if `LastMonsterAttackTime < d.Time - 150`, halve the grant. `MonsterAttacks` stores that timestamp (`Monster.cpp` 1005).
- If the level multiplier is non-zero, multiply by it. A multiplier of 0 leaves the grant unchanged (`CSB.h` 1071).
- For skills 4 and above, if the monster timestamp is still within 25 ticks, double the grant. That test and the 150-tick test cannot both succeed.
- Add the result to the named skill, and also to the major skill when the named skill is a subskill.
- Add `clamp(1, experience / 8, 100)` to that skill’s `tempAdjust`, until the temp word would pass 32000.
- If major-skill mastery increased, `LevelUp` raises maximums. Fighter levels add `MaxStamina / 16` and raise maximum Strength (`Magic.cpp` 102–106).

Being struck grants skill 7 the monster’s `word16_8_11` value before the damage roll (`Monster.cpp` 4529). Skill 7 is the last Fighter subskill. The same function subtracts twice the mastery of skill 7 from the monster’s damage before the random expansion.

The largest flat grants in the table are Berzerk 40, Fireball 35, Lightning and Window 30, Dispell, Invoke, and Spit 25, and Melee 24. Juggle, Speed, Blow Horn, Flip, Confuse, Pray, Heal, Calm, and Brandish are 0 before any special-case replacement.

A Party Attack Filter DSA can overwrite damage, stamina, and experience before they are applied. The formulas above are the engine with no filter.

## Unresolved

- Weapon `uByte1`, `uByte2`, `uByte3`, and `word4`, and monster `defense08`, `dexterity12`, `attack10`, and `word16` bits 8–11, are in other graphics and were not dumped.
- The monster hit-location mask (`Monster.cpp` 4543–4556, `d.uByte590`) was not mapped to head, torso, and legs.
- The shoot assignment that discards `2 * (word4 + shoot mastery)` is confirmed in CSBwin. The Atari instruction stream was not compared.
- `word64` add/subtract is the original behavior described in the `CHARDESC` comment. This tree sets and clears. Saves already written by the old overflow are not part of this formula.
- Unity will use the attribute-based `TAG016426`. That is the only approved Atari-versus-CSBwin choice. The shoot overwrite and the `word64` set/clear change remain undecided for Unity.
