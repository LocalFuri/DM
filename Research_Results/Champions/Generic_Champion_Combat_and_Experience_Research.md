A champion is an 800-byte `CHARDESC`. Combat, protection, skills, and survival all read that record plus the graphic tables. Weapon damage bytes, armor bytes, and the 44 attack-action numbers were not dumped, so those stay as table locations. No DOS routine was opened.

`word64` is a CSBwin change. The original added and subtracted an attack adjustment and could overflow. This port stores one value and clears it (`CSB.h` 2577–2599). The damage filter, `PhysicalAttackFilter`, and `CursorFilter` are also CSBwin. The request stopped at example 7.

## 1. The champion record

`CHARDESC` is 800 bytes (`CSB.h` 2520–2630). Four of them sit in `d.CH16482`. The fields the simulation uses are:

| **Offset** | **Field**               | **Meaning**                                                            |
| ---------: | :---------------------- | :--------------------------------------------------------------------- |
|          0 | `name[8]`               | Text from the mirror, including the terminator.                        |
|          8 | `title[16]`             | Title from the same text.                                              |
|         28 | `facing`                | Absolute facing, not relative to the party.                            |
|         29 | `charPosition`          | Absolute corner in the party square.                                   |
|         32 | `attackType`            | Signed. −1 means no attack is in progress.                             |
|         34 | `incantation[4]`        | Four rune bytes.                                                       |
|         40 | `facing3`               | Direction of the latest hit.                                           |
|         41 | `maxRecentDamage`       | Largest recent hit, used by the portrait.                              |
|         42 | `poisonCount`           | Number of outstanding poison timers.                                   |
|         44 | `busyTimer`             | Signed timer index. −1 means idle.                                     |
|         50 | `ouches`                | Bit mask of injured body parts.                                        |
|         52 | `HP`, `MaxHP`           | Signed words. The setter rejects a value above 999.                    |
|         56 | `Stamina`, `MaxStamina` | Limit 9999.                                                            |
|         60 | `Mana`, `MaxMana`       | Limit 900.                                                             |
|         64 | `word64`                | One temporary defense adjustment.                                      |
|         66 | `food`                  | Signed. The survival tick clamps it at −1024.                          |
|         68 | `water`                 | Same clamp.                                                            |
|         70 | `Attributes[7]`         | Three unsigned bytes each: maximum, current, minimum.                  |
|         92 | `skills92[20]`          | Six bytes each: a signed temporary adjustment and a 32-bit experience. |
|        212 | `possessions[30]`       | Object names.                                                          |
|        272 | `load`                  | Carried weight in tenths of a kilogram.                                |
|        274 | `shieldStrength`        | Added into physical protection.                                        |
|        276 | `talents`               | Cleared to 0 when the mirror creates the champion.                     |
|        280 | `fingerPrint`           | Identity used by bones and the wing file.                              |
|        336 | `portrait`              | The remaining bytes through 799 are the portrait bitmap.               |

The seven attributes, in order, are Luck, Strength, Dexterity, Wisdom, Vitality, Anti-magic, and Anti-fire (`CSB.h` 2602–2609). `Current()` returns 0 when the stored byte has gone negative, which happens to Luck’s minimum after enough cursed objects (`CSB.h` 2445–2461).

## 2. Recruitment

`AddCharacter` (`Character.cpp` 465) does nothing if the cursor holds an object or the party already has four champions. It clears 800 bytes, copies portrait `charID` from graphic 26 when the id is 0 through 23, and otherwise uses an external portrait.

The fingerprint starts as `(level << 10) | (x << 5) | y` and increases until it is unique. Facing and `facing3` copy the party facing. The position is the first free corner in `(index + partyFacing) & 3`. Food and water are each `random(256) + 1500`. `attackType`, `busyTimer`, and `timerIndex` start at −1. Talents start at 0.

The name and the rest of the record come from the first text object on that cell, decoded with type `0x8002`. If that text is missing or illegal, the function substitutes the built-in illegal-hero string (`Character.cpp` 483 and 553–564). The text parser after the name was not followed to the last field, so the initial attribute, skill, and inventory numbers are in that dungeon text, not in a second table.

Removing a champion from a hand clears a torch’s lit bit and subtracts an Illumulet’s brightness. The mirror actuator that calls `AddCharacter` is type 127, which is a CSBwin portrait actuator. The original text-on-the-floor path is the part above.

The Vi altar does not create a new champion. It finds `Bones_a` whose saved fingerprint matches a dead party member, or a dead champion stored in the wing file (`Timer.cpp` 2694–2768). The later health and experience changes in that timer were not read past the bone match.

## 3. A physical hit

`DeterminePhysicalAttackDamage` (`Attack.cpp` 535) returns 0 when the champion is dead. A non-material monster is skipped unless the attack is Vorpal or Disrupt.

The hit test is:

needed = random(32) + monster.dexterity12 + levelDifficulty - 16

hit if quickness > needed

&#x20;  or random(4) == 0

&#x20;  or IsCharacterLucky(champion, 75 - P7)

A miss costs `2 + random(2)` stamina and returns 0. `P7` is supplied by the attack action. `IsCharacterLucky` was not opened.

On a hit, `throwingDistance` comes from the weapon hand. Zero throwing distance leaves the damage accumulator at 0. Otherwise:

damage = throwingDistance + random(throwingDistance/2 + 1)

damage = damage \* P8 / 32

defense = random(32) + monster.defense08 + levelDifficulty

Diamond Edge: defense -= defense/4

Executioner:  defense -= defense/8

damage = damage + random(32) - defense

If the distance was 0 or that result is less than 2, `random(4) == 0` misses. Otherwise the result gains `random(16)`, and sometimes `random(4)` or a further `random(16)`. The trace comments at `Attack.cpp` 765–794 record those branches.

Then:

damage /= 2

damage += random(4) + random(damage)   // the second term only when damage != 0

damage /= 4

damage += random(4) + 1

A Vorpal Blade against a material monster halves that again and misses if the result is 0. If `random(64) < mastery(skill)`, damage gains 10. A negative result becomes 0.

Experience for the swing is `damage * monster.word16 bits 8–11 / 16 + 3` (`Attack.cpp` 882). There is no separate critical-hit type. The extra +10 is the mastery test above. `P8` is the action’s damage multiplier. Its 44 values are in the graphic, not in this report.

## 4. Weapons and actions

Every physical action enters the same function. The weapon changes it through throwing distance, the name tests for Diamond Edge, Executioner, and Vorpal Blade, and the skill number selected for that action.

Graphic `0x230` holds 44 attack actions. From the start of that graphic:

| **Offset** | **Count** | **Contents**                                                        |
| ---------: | --------: | :------------------------------------------------------------------ |
|         64 |        44 | Experience awarded, signed bytes.                                   |
|        108 |        44 | Skill number. The comment says the largest stored value seen is 18. |
|        152 |        44 | `Byte20090`.                                                        |
|        196 |        44 | `Byte20046`.                                                        |
|        240 |        44 | `Byte20002`. The comment says this may be required strength.        |
|        284 |        44 | `Byte19958`.                                                        |
|        328 |        44 | `Byte19914`. The comment lists small integers whose largest is 38.  |
|        372 |       300 | Attack names, packed text.                                          |
|        672 |    44 × 8 | Legal attacks for each weapon class.                                |

Those bytes were not read. The names in source are the `ATTACKTYPE` enumeration (`CSB.h` 589–634): Kick, Punch, Bash, Chop, Swing, Stab, Thrust, Hack, Berzerk, Shoot, Throw, and the spell attacks. Wizard skills 3 and 16–19 spend `7 - min(6, mastery)` mana. Other physical actions spend 0 (`Attack.cpp` 1721–1736).

Weapon records are `WEAPONDESC`, six bytes, 46 of them, at offset 1682 of graphic `0x22f`. Byte 0 is weight. `word4` is little-endian. The damage bytes inside it were not identified beyond the struct (`CSB.h` 1385–1391). Clothing is four bytes, 58 records, at offset 1450 of the same graphic.

Thrown weapons call `AdjustSkills` on skill 10 (`Mouse.cpp` 750) and use the missile path from the earlier projectile work. A thrown weapon’s impact sound is 0. Charges on a weapon or clothing object decrease by one when an attack consumes a charge (`Attack.cpp` 1248–1259). The exact charge test was not re-read.

## 5. Damage taken

`DamageCharacter` (`Character.cpp` 2724) returns immediately for a dead champion, a non-positive damage value, or champion index −1. The pending character is also excluded.

When the damage type `P4` is nonzero, it averages `TAG01680a` over every body part whose bit is set in `mask`. That average is protection.

| **`P4`** | **Effect before the hit is stored**                                              |
| -------: | :------------------------------------------------------------------------------- |
|        1 | Fire. `TAG016426` uses Anti-fire, then `FireShield` is subtracted.               |
|        2 | Protection is halved, then the physical scale is applied.                        |
|        5 | Magic. `TAG016426` uses Anti-magic, then `SpellShield` is subtracted.            |
|        6 | If Wisdom is 115 or more, damage is 0. Otherwise `damage * (115 - Wisdom) / 64`. |
|    other | `damage * (130 - protection) / 64`.                                              |

`ScaledMultiply(damage, 6, factor)` is that division by 64 (`Character.cpp` 2786). A non-positive result stops the function. If damage still exceeds `random(128) + 10` after the Vitality adjustment, injury bits are added to `PendingOuches` until the doubled threshold catches the damage (`Character.cpp` 2822–2836).

`TAG01680a` (`Character.cpp` 2409) adds clothing that has bit 7 of `uByte2` set, the shield in the matching hand, `shieldStrength`, `PartyShield`, and `word64`. It also adds `random(Vitality/8 + 1)`. An injured part subtracts `8 + random(4)`. Sleep halves the total. The returned protection is `(total >> 1)`, limited to 0 through 100.

The calls already traced are:

- A closing door uses `DamageAllCharacters(5, 4, 2)`.
- A pit uses `DamageAllCharacters(20, 48, 2)`.
- A blocked step uses `DamageCharacter(champion, 1, 24, 2)`.
- A monster melee hit ends in `DamageCharacter` with the monster descriptor’s `uByte22[2]` as `P4` (`Monster.cpp` 4579).
- A fireball or lightning cloud uses `DamageAllCharacters(damage, 63, 1)`.

`DamageAllCharacters` itself was not opened. The DSA damage filter can replace the result after the formula (`Character.cpp` 2856).

## 6. What equipment changes

Protection is the sum above, then a random Vitality term, then the 0–100 limit. It is not a fixed armor class. Direction matters because the damage mask selects body parts, and a shield in the hand being struck is shifted by 4 rather than 5 (`Character.cpp` 2440–2448). `Byte1412[bodyPart]` scales the shield term. Those six bytes are in graphic `0x232` at offset 418. Their values were not read.

Equipment bonuses found in `Character.cpp` 1680–1766 are fixed, not rolled:

|    **Slot** | **Object**                               | **Change**                      |
| ----------: | :--------------------------------------- | :------------------------------ |
|     2, head | Crown of Nerra                           | Wisdom +10.                     |
|           2 | Dexhelm                                  | Dexterity +10.                  |
|    3, torso | Flamebain                                | Anti-fire +12.                  |
|           3 | Cloak of Night                           | Dexterity +8.                   |
|     4, legs | Powertowers                              | Strength +10.                   |
| weapon hand | named staves and wands                   | Skill adjustments from 1 to 16. |
| weapon hand | Side Splitter, Dragon Fang, Vorpal Blade | +1, +2, and +4.                 |

Elven Boots, slot 5, add one sixteenth of `MaxLoad` (`Code11f52.cpp` 4364–4367). The generic clothing protection bytes are `ClothingDesc`, not these named bonuses.

## 7. Skills

There are 20 skills (`CSB.h` 2610–2614):

|  **IDs** | **Group** |
| -------: | :-------- |
|   0, 4–7 | Fighter.  |
|  1, 8–11 | Ninja.    |
| 2, 12–15 | Priest.   |
| 3, 16–19 | Wizard.   |

`DetermineMastery` (`Code17818.cpp` 13) returns 1 while the party is sleeping. Otherwise it adds temporary experience unless bit `0x8000` is set. For a skill above 3 it adds the parent skill, whose index is `(skill - 4) / 4`, and then divides by 2. The parent therefore contributes half of the sum, not a separate mastery.

mastery = 1

while experience >= 500:

&#x20;   mastery += 1

&#x20;   experience >>= 1

So mastery 2 starts at 500, mastery 3 at 1000, and mastery `n` at `500 << (n - 2)`. There is no other threshold table. Awarded experience is capped at `0x10000000` (`CSB.h` 2474).

The Firestaff, Pendant Feral, Ekkhard Cross, Gem of Ages, Sceptre of Lyf, and Moonstone add 1 or 2 after that loop (`Code17818.cpp` 73–140). Bit `0x4000` suppresses those possession bonuses.

A successful physical hit queues `damage * monsterBits / 16 + 3` experience. A miss does not. A spell awards the full success value, or that value shifted down on failure, as reported with the spell system. Each ten-second update subtracts 1 from every positive temporary skill adjustment (`Code17818.cpp` 250–256). Permanent experience is not decayed. No function raises Strength or another attribute because mastery increased. Attribute potions and the named equipment do that separately.

## 8. Recovery

The ten-second update runs when `(d.Time & 63) == 0`, or `(d.Time & 15) == 0` while sleeping (`CSBCode.cpp` 682–685).

Mana recovers only when current mana is below the maximum and `random112 < wizardMastery + priestMastery + Wisdom`. The amount is `MaxMana / 40 + 1`, doubled while sleeping. The same tick also spends stamina equal to that amount times `max(7, 16 - magicMastery)` (`Code17818.cpp` 209–238). Mana above the maximum falls by 1.

Stamina recovery depends on how full the bar already is. The loop counts down from 4 by 2 while stamina is below each halved maximum. Food and water then change by that count. If the champion has not moved for more than 80 ticks, recovery gains 1. More than 250 ticks adds another 1. Sleep doubles the base term (`Code17818.cpp` 258–278 and 335).

Health recovers when stamina is at least one quarter of its maximum and `random112 < Vitality + 12`. The gain is `(MaxHP >> 7) + 1`, doubled in sleep. The Ekkhard Cross adds half of that plus 1 (`Code17818.cpp` 344–365).

Food or water below −512 increases the stamina cost. Both are clamped at −1024. Exhaustion damage was not re-read beyond `DamageCharacter` being called with `absStamina / 2` from `Code11f52.cpp` 4493.

## 9. Food and water

Food and water are signed words. New champions start at `1500 + random(256)`. The floor is −1024. The upper values applied while drinking are `min(current + amount, 2048)`.

A waterskin adds 800. The other water branch adds 1600 (`Character.cpp` 4133 and 4288). Eating plays sound 8 once per gulp. `FoodValue[8]` is eight little-endian words at offset 1378 of graphic `0x22f`, immediately after the 27 monster descriptors. Those eight values were not read. The consumption tick does not read Strength, load, or the action being performed. It reads food, water, stamina, sleep, and time since the last party move.

## 10. Load and slots

`MaxLoad` (`Code11f52.cpp` 4341) is:

load = 8 \* Strength + 100

load = StaminaAdjustedAttribute(load)

if any injury:

&#x20;   subtract load/4, or load/8 when injury bit 4 is set

if Elven Boots are in slot 5:

&#x20;   add load/16

add 9

round down to a multiple of 10

`load` on the champion is the current total in tenths of a kilogram. The function that turns this into a party step delay was not read.

The 30 possession slots confirmed by code are 0 and 1 for the hands, 2 head, 3 torso, 4 legs, 5 feet, and 10 neck. `OBJDESC.word4` in graphic `0x22f` at offset 1958 is the placement mask (`CSB.h` 1720–1730): mouth, head, neck, torso, legs, feet, ready quiver, stored quiver, pouch, and chest. There are 180 six-byte object records. A chest’s contents are a separate object list, not extra champion slots. The carrying mask is tested by `CarryLocation[38]`, 38 little-endian words at offset 788 of graphic `0x232`.

## 11. Potions

Potion effects are the class-1 spells already traced. The strength stored in the flask is `(40 * power + random(16)) & 255`. The spell comments name strength, stamina, mana, dexterity, wisdom, vitality, healing, antivenom, and shield potions (`Data.h` 648–660). Drinking one does not cast again. Temporary skill experience falls by 1 on each ten-second tick. Attribute bytes have no separate decay function in the code that was read. Current attributes are the stored bytes. `Current()` only forces a negative Luck minimum to 0.

## 12. Death

`DamageCharacter` ignores a champion whose health is already 0. Champion death plays sound 6 (`Code1f9e6.cpp` 129). `PartyHasDied` is set when the party is lost (`Data.h` 597). The bone-drop and formation-compaction code was not re-read.

Resurrection through the Vi altar requires `Bones_a`, a fingerprint, and a dead champion still in the party or in the wing file. The altar removes the bones. Health, skill, and attribute changes after that match were not read, so they are not stated here.

## 13. Table locations

| **Table**                                             | **Graphic**   |              **Offset** | **Count** |                  **Stride** |
| :---------------------------------------------------- | :------------ | ----------------------: | --------: | --------------------------: |
| Attack experience, skill, and five action-byte tables | `0x230` / 560 | 64, then every 44 bytes |        44 |                           1 |
| Attack names                                          | `0x230`       |                     372 | 300 bytes |                           — |
| Legal attacks                                         | `0x230`       |                     672 |        44 |                           8 |
| Food values                                           | `0x22f` / 559 |                    1378 |         8 |            2, little-endian |
| Clothing                                              | `0x22f`       |                    1450 |        58 |                           4 |
| Weapons                                               | `0x22f`       |                    1682 |        46 |    6, `word4` little-endian |
| Object descriptors                                    | `0x22f`       |                    1958 |       180 | 6, both words little-endian |
| `Byte1412` body-part scale                            | `0x232` / 562 |                     418 |         6 |                           1 |
| `CarryLocation`                                       | `0x232`       |                     788 |        38 |            2, little-endian |

Champion records are not in a graphic. They are the 800-byte party records, four of them, filled from mirror text.

## 14. The same formulas on six cases

A sword against a Screamer uses the physical pipeline. Screamer is monster type 6, so `dexterity12`, `defense08`, and the experience bits come from monster descriptor 6. The sword supplies throwing distance and `P8`. No sword byte was read, so the result cannot be a number.

A punch uses the same function with the punch action’s skill and `P8`. An empty hand has whatever throwing distance `DetermineThrowingDistance` returns for that hand. The miss cost is still `2 + random(2)` stamina.

A thrown dagger does not use this melee division. It becomes a database-14 missile. Skill 10 receives the throwing experience at `Mouse.cpp` 750. Impact damage is the missile’s remaining damage.

A Vorpal Blade against a non-material monster is allowed because Vorpal or Disrupt bypasses the material test. Against a material monster the same blade halves the final damage and can miss when that half is 0.

Physical damage on an armored champion averages `TAG01680a` across the mask, then multiplies by `(130 - average) / 64`. Each part’s protection includes its clothing, a Vitality random term, the shield, and the party shield, then halves and limits the result to 0–100.

Fighter mastery increases only when skill 0, or one of skills 4–7 after adding half of the parent, crosses the next power-of-two boundary at 500. No attribute increases at that moment. Example 7 was cut off in the requ