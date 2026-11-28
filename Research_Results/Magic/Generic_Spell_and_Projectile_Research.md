Rune spells, thrown objects, and monster missiles all become database-14 projectiles and share `MissileTimer`. Clouds are a second object, database 15, drawn after the projectiles. The spell table is 25 records. Its numeric bytes were not read from the file in this session. The names below are the comments in `Data.h`.

This is the Atari translation. `CallSpellFilter` and `DSACastSpell` are CSBwin additions. No DOS spell or projectile routine was opened.

## 1. Casting

`CastMagic` (`Magic.cpp` 899) fills a parameter block, calls the CSBwin spell filter, then `CastSpell` (`Magic.cpp` 1062).

The champion’s incantation bytes are packed into one word, starting at bit 24 (`Incantation2Spell`, `Magic.cpp` 935). That word is compared with `Spells[0..24].spellID`. A match on the low 24 bits is enough unless the stored id has a nonzero high byte. The first byte must not be the only byte.

Power is `incantation[0] - 95` and must be 1 through 6 (`Magic.cpp` 1171).

Each `SPELL` (`CSB.h` 1765) is 8 bytes:

| **Field**          | **Use**                                    |
| :----------------- | :----------------------------------------- |
| `spellID`          | Little-endian rune id.                     |
| `skillRequired`    | Added to power to form the difficulty.     |
| `byte5`            | Skill number passed to `DetermineMastery`. |
| `word6` bits 0–3   | Class: 1 potion, 2 missile, 3 other.       |
| `word6` bits 4–9   | Missile or potion type.                    |
| `word6` bits 10–15 | Recovery time.                             |

Difficulty is `D4 = skillRequired + power`. The experience value is:

random(0..7) + 16\*D4 + 8\*(power-1)\*skillRequired + D4\*D4

(`Magic.cpp` 1185–1191). If mastery is below `D4`, each missing point rolls `random(128)`. The roll fails when it is greater than `min(wisdom + 15, 115)`. Failure grants the experience shifted right by the number of missing points and prints an error (`Magic.cpp` 1195–1210). Success grants the full experience (`Magic.cpp` 1375). Recovery is bits 10–15 of `word6`, applied by `DisableCharacterAction` (`Magic.cpp` 1379).

A rune missile does not spend mana. `CharacterThrowsSpell` is called with `neededMana` 0 (`Magic.cpp` 1258–1263). Mana is spent by the attack-menu wizard skills: `7 - min(6, mastery)` (`Attack.cpp` 1721–1730). If the champion has less, the range is scaled by `mana / needed` and all remaining mana is spent (`Attack.cpp` 1303–1309).

Class 2 range is clamped from 21 to 255:

(2\*mastery + 4) \* (power + 2)

If the missile type is 4, mastery is doubled first (`Magic.cpp` 1251–1262). The projectile damage is the constant 90. Decay is `10 - min(8, maxMana/8)`. If the range is less than four times that decay, the range gains 3 and the decay loses 1 (`Magic.cpp` 467–473).

The 25 entries are graphic `0x230`, which is 1256 bytes. `Spells` starts at offset 1026: 25 records, stride 8, little-endian `spellID` and `word6`. The comments at `Data.h` 637–662 say:

| **Index** | **Runes** | **Commented id** | **Commented class** | **Commented effect**           |
| --------: | :-------- | :--------------- | ------------------: | :----------------------------- |
|         0 | 1-4       | `0x666f00`       |                   3 | Party shield                   |
|         1 | 1-5-2     | `0x667073`       |                   3 | Footprints                     |
|         2 | 3-2-6     | `0x686d77`       |                   3 | Invisibility                   |
|         3 | 3-1       | `0x686c00`       |                   2 | Poison cloud `0xff87`          |
|         4 | 3-2-5     | `0x686d76`       |                   3 | See-through walls              |
|         5 | 3-3-5     | `0x686e76`       |                   2 | Missile `0xff82`               |
|         6 | 3-4-5     | `0x686f76`       |                   3 | Light                          |
|         7 | 4         | `0x690000`       |                   3 | Torch                          |
|         8 | 4-4       | `0x696f00`       |                   2 | Fireball `0xff80`              |
|         9 | 4-5-1     | `0x697072`       |                   1 | Strength potion                |
|        10 | 4-5-4     | `0x697075`       |                   3 | Fire shield                    |
|        11 | 5-2       | `0x6a6d00`       |                   2 | Missile `0xff83`, non-material |
|        12 | 5-1       | `0x6a6c00`       |                   3 | Poison bolt                    |
|        13 | 5-4-6     | `0x6a6f77`       |                   3 | Darkness                       |
|        14 | 6         | `0x6b0000`       |                   2 | Missile `0xff84`, open door    |
|        15 | 1-5       | `0x667000`       |                   1 | Magic-shield potion            |
|        16 | 1         | `0x660000`       |                   1 | Stamina potion                 |
|        17 | 1-5-3     | `0x667074`       |                   1 | Wisdom potion                  |
|        18 | 1-5-4     | `0x667075`       |                   1 | Vitality potion                |
|        19 | 2         | `0x670000`       |                   1 | Cure wounds                    |
|        20 | 2-5       | `0x677000`       |                   1 | Antivenom                      |
|        21 | 3-5-2     | `0x687073`       |                   1 | Dexterity potion               |
|        22 | 6-5-5     | `0x6b7076`       |                   1 | Mana potion                    |
|        23 | 6-1       | `0x6b6c76`       |                   1 | Poison potion                  |
|        24 | 6-3-5     | `0x6b6e76`       |                   3 | Zo Kath Ra                     |

Those ids and classes are the source comments, not a fresh dump of the graphic.

## 2. How a projectile is drawn

Thrown weapons and spell missiles use the same draw loop, after the monster at that corner (`Code51a4.cpp` 2336–2599). `GetGraphicClass` (`Code51a4.cpp` 2875) returns a negative class for a spell or for a weapon whose descriptor bits 8–12 are nonzero. The absolute value selects `STRUCT5688`:

graphic = uByte0 + 316 + frame

`word4` bits 0–1 are the frame count. Zero means one frame. Otherwise the loader also keeps `+1`, and if the count is not 2 it keeps `+2` (`Code390e.cpp` 149–166). The frame itself is chosen from the missile’s direction and the party’s facing (`Code51a4.cpp` 2400–2499).

`width` and `height` are the native byte-width and pixel height. Source pixel width is `width << 1`. At relative cell 9, or when the remaining range is 255 and bit `0x100` of `word4` is clear, the graphic is drawn at that native size (`Code51a4.cpp` 2368–2375). Otherwise:

distance = 2 \* (cell / 3) + corner / 2

factor   = Byte5752[distance]

if bit 0x100 is clear:

&#x20;   factor = factor \* max(96, rangeRemaining + 1) >> 8

byteWidth = (width  \* factor + factor/2) >> 5

height    = (height \* factor + factor/2) >> 5

(`Code51a4.cpp` 2377–2392). This is not the monster 20/32 or 16/32 pair. The factor comes from `Byte5752`, then from the remaining range. The color map is `Pointer1864[distance >> 1]`, which startup sets to `Byte5728` (`CSBCode.cpp` 10787). Transparent color is 10.

The anchor is `viewportPosition[0][cell][corner]`. Its X is the center. Y is fixed at 47, then the bitmap is centered vertically around that line (`Code51a4.cpp` 2340 and 2570–2576). X is clamped to 0 and 223. A mirror is applied after scaling when the side test says so. A vertical flip uses `TAG008840` when the other flag is set. The derived slot, when bit `0x100` is clear, is `282 + uByte1 + 6*frame + distance` (`Code51a4.cpp` 2512–2518).

A physical object whose class is not negative uses the ordinary floor-item path instead, with the missile position as its corner (`Code51a4.cpp` 2601–2629).

## 3. Flight

`LaunchMissile` (`Magic.cpp` 16) creates a database-14 object. Range and damage are stored, each limited to 255. The timer is function 48 for the first step and 49 after that. Word 8 holds X in bits 0–4, Y in bits 5–9, direction in bits 10–11, and decay in bits 12–15. The first timer is `time + 1`.

`MissileTimer` (`Timer.cpp` 185) is the same function for a fireball and for a thrown Screamer Slice. The comment says so. On function 49, before moving:

1. If the missile is on the party’s tile, test the party.
2. Test a monster on that tile.
3. If `rangeRemaining <= decay`, remove the missile and, for a real object, drop it.
4. Subtract decay from range. Subtract decay from damage, not below 0.

The first step, function 48, skips that decay (`Timer.cpp` 232–235).

Inside the tile, if direction and corner have the same parity, the corner decreases by 1. Otherwise it increases by 1 (`Timer.cpp` 361–367). The corner matches the direction, or the direction plus one, when the missile is about to leave the tile. The next tile is rejected when it is outside the map, stone, a solid false wall, or stairs following stairs (`Timer.cpp` 334–339). `MoveObject` may change the destination for a teleporter. A pit is not given its own test in this function. A door is tested when the missile changes corner without leaving the cell (`Timer.cpp` 391–409). Each successful step reschedules the timer at `time + 1`.

each tick after the first:

&#x20;   if party or monster at this corner: impact and stop

&#x20;   if range <= decay: stop

&#x20;   range -= decay

&#x20;   damage = max(0, damage - decay)

&#x20;   corner += 1 or corner -= 1

&#x20;   if the corner leaves the tile:

&#x20;       step one tile in the flight direction

&#x20;       stop if the next tile is stone, solid false wall, or stairs-to-stairs

&#x20;   else if the tile is a door:

&#x20;       test the door

&#x20;   timer += 1

## 4. Clouds

An impact calls `CreateCloud` (`Code11f52.cpp` 1397). The cloud’s type is the spell type and its value is the size argument. Fireball, poison, and lightning play sound 5 when the size is greater than 80, otherwise sound 20. Other clouds except monster death play sound 13. The timer is function 25, one tick later, or five ticks later for record `0xffe4` (`Code11f52.cpp` 1471–1478).

`ProcessTT_25` (`Timer.cpp` 426) computes damage and then either reschedules or deletes the cloud:

| **Cloud**         | **Damage base**                             | **What keeps it alive**                          |
| :---------------- | :------------------------------------------ | :----------------------------------------------- |
| `0xff87` poison   | `max(1, min(value/32, 4) + random(0 or 1))` | `value >= 6`; then `value -= 3`                  |
| `0xff80` fireball | `value/2 + 1 + random(that) + 1`            | One immediate hit, then the cloud is removed     |
| `0xff82`          | Half of the fireball base                   | Same removal. Also damages non-material monsters |
| `0xffe4`          | None in this case                           | Type increases by 1, sound 5, one more tick      |
| `0xffa8`          | None here                                   | If `value > 55`, `value -= 40` and one more tick |

Poison on the party uses `DamageAllCharacters`. Poison on a monster is `8 * (damage + random(0..3)) / (poisonResistance + 1)`, and resistance 15 means zero (`Code11f52.cpp` 1377–1390).

`DrawClouds` (`Code51a4.cpp` 77) maps the cloud type onto graphic group 0, 1, 2, or 3. Types 0, 2, and 101 use group 0. Types 6 and 7 use group 2. Type 40 uses group 3. Anything else uses group 1. Type 100 is lightning and does not use that group: it draws `STRUCT5688` graphic `uByte0 + 317`, scaled by `Word7246` and `IdentityColorMap` (`Code51a4.cpp` 134–161).

`TAG005088` (`Code51a4.cpp` 21) scales a group with `Byte5696`. The size argument is capped at 32. Destination size is `(source * size + size/2) / 32`. Size 32 uses graphic `348 + group` unscaled, except group 3. Smaller sizes use `ShrinkBLT` from graphic `348`, `349`, or `350`. Group 3 uses `Byte5712`. The others use the identity map. The derived slot is `14*group + size/2 + 436`.

The drawn size follows `value >> 5` (`Code51a4.cpp` 185). Poison and `0xffa8` reduce `value` on a timer, so their picture shrinks. A fireball’s explosion damage is computed once from the original size in `CreateCloud` and is not redrawn from the remaining damage. Monster death uses `RNMonsterDeath` with size 110, 190, or 255 for horizontal sizes 0, 1, and 2 (`Code11f52.cpp` 1261–1288).

## 5. Damage

Fireball and lightning, at the moment the cloud is created (`Code11f52.cpp` 1495–1533):

damage = size/2 + 1 + random(that amount)

lightning: damage /= 2

if the party is there: DamageAllCharacters(damage, 63, 1)

else if a monster is there:

&#x20;   stop if fire resistance is 15

&#x20;   non-material: damage >>= 2

&#x20;   damage -= random(0 .. 2\*fireResistance)

Dispel (`0xff82` and the dispel case in `ProcessTT_25`) damages non-material monsters. A Zytaz that is currently attacking takes `random(value/8*2+1) + random(0..3) + reduced damage`. Other non-material monsters take the group damage. Material monsters are not hit by that case (`Timer.cpp` 497–541).

A physical projectile’s damage is the database-14 `damageRemaining`, reduced by decay each tick. Champion poison from a projectile is a separate field and is applied on a coin toss after `DamageCharacter` (`Code11f52.cpp` 2072–2081\`).

A fireball or `0xff82` cloud on a door calls `HitDoor` with the explosion flag (`Timer.cpp` 490–494). That is the same bash path as an axe: the door must allow a fireball and the force must meet `DoorTOC`’s low byte. Only a closed door changes, and it becomes state 5.

## 6. Spells that do not fly

Class 3 uses bits 4–9 as the effect (`Magic.cpp` 1266–1372). `D6` starts as `4 * (power + 1)`.

| **Type** | **Effect**                                                                         | **Removal**                               |
| -------: | :--------------------------------------------------------------------------------- | :---------------------------------------- |
|        0 | Light. Brightness uses `Word1074`. Duration starts at 10000 and adds `(D6-8)*512`. | Timer 70 decrements brightness.           |
|        1 | Darkness. Duration 98.                                                             | Same light timer, in the other direction. |
|        2 | `SeeThruWalls` increases. Duration is `D6*D6` after `D6` is halved.                | Timer 73 decrements the count.            |
|        3 | `Invisible` increases. Duration is `D6`.                                           | Timer 71 decrements the count.            |
|        4 | Party shield adds `D6`, or `D6/4` if the shield is already above 50.               | Timer 74 subtracts the stored amount.     |
|        5 | Another brightness change. Duration `4000 + 128*(D6-3)`.                           | Timer 70.                                 |
|        6 | Footprints. The active count increases and the list index is recorded.             | Timer 79 decrements the count.            |
|        7 | Creates the Zo spell object in a hand, or on the floor if both hands are full.     | It is an object, not a timer.             |
|        8 | Fire shield. Strength is `D6*D6 + 100`.                                            | Timer 78 subtracts `strength/32`.         |

Class 1 fills an empty flask. Strength is `(40*power + random(16)) & 255` (`Magic.cpp` 1220–1231). Casting fails with result 3 when no flask is held, and the incantation is kept.

Party shield and fire shield add to a global value. Casting again adds more. A value already above 50 quarters the new contribution. The timer later subtracts exactly the amount that was added. Invisibility, see-through walls, and footprints are counters, so overlapping casts accumulate and each timer removes one.

## 7. Drawing order

On an open cell the order is floor ornament, ceiling pit on F2 and F1, then objects. Inside the object call the order is floor items, the monster, then missiles at that corner. Clouds are drawn after all four corners (`Code51a4.cpp` 2802–2809).

On a facing door the floor ornament and the far-side objects, including their missiles, are drawn before the frame and the door. Near-side objects and their missiles are drawn after the door. A cloud from that call still comes after the corners of that call, so a near-side cloud is after the door and a far-side cloud is before it.

There is no stencil. A later blit covers an earlier one except where the new pixel is color 10.