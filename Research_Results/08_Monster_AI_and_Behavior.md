# 08 — Monster AI and behavior

**VERIFIED** in this CSBwin tree unless a paragraph says **PARTIALLY VERIFIED** or **UNRESOLVED**. Integer arithmetic is the same 16-bit style as report 07. `STRandom(n)` is `(STRandom() & 0xffff) % n`.

No CSBwin gameplay source was changed. Champion damage, armor, and the Anti-Fire / Anti-Magic decision stay in report 07. This note does not repeat those formulas.

The engine does not use A* or any stored path. A monster group is one database record. It looks, smells, follows footprints, or picks a random open direction.

## 1. Executive summary

One monster group is a `DB4` record. It has a type, a fear nibble, up to four hit-point words, and a position/facing byte. While the group is on the party’s level it also has an `ITEM16` with per-member facing, position, and attack/mirror bits.

Behavior is timer-driven. `ProcessTimers29to41` (`Monster.cpp` 3278) is the scheduler. Negative timers `TT_M1`, `TT_M2`, and `TT_M3` are one-shot reactions. Timers 29–31 and 32–41 are the repeating AI. Movement period is `timePerMove + STRandom0_3() - 1`, at least 1 tick (`Increment_Time_By_W52_Plus_Random`, `Monster.cpp` 1413).

Seeing the party requires facing (unless the type sees all around), range, and a straight-ish open path (`Blocked`, `Monster.cpp` 513). Smell and old party footprints are the only memory (`GetBestMonsterFacingP1`, `Monster.cpp` 595). There is no grid search.

Attack damage from a monster in melee is `MonsterDamagesCharacter` (`Monster.cpp` 4509). Report 07 has the hit roll, the random damage expansion, armor, and shields. This note covers when that function is reached.

Graphic `0x22F` holds all 27 `MONSTERDESC` records. They were decompressed with the same LZW path as report 07. Decompressed size 3086 matches `CSBCode.cpp` 10569.

## 2. Monster data structures

**VERIFIED.** `MONSTERDESC` is 26 bytes (`CSB.h` 2291–2400). `ReadTablesFromGraphicsFile` loads graphic `0x22F` at `d.Byte10340` (`CSBCode.cpp` 10569–10570) and calls `fixItem26` (`CSBCode.cpp` 10452), which byte-swaps `word2`, `word4`, `word14`, `word16`, `word18`, and `word20`.

The descriptor starts 676 bytes into the decompressed graphic: 4 + 256 + 64 + 256 + 8 + 80 + 8 (`Data.h` 1052–1059). `uByte0` is the type number and matches the array index for all 27 records.

| Field | Meaning in this source |
|---|---|
| `attackSound` | Sound ordinal in `MonsterAttacks` (`Monster.cpp` 1126). |
| `word2` bits 0–1 | `horizontalSize`. 0 = up to four monsters. 1 = up to two, on opposite corners. 2 = one monster fills the cell (`Monster.cpp` 144–165). |
| `word2` bits 7–8 | `verticalSize`, compared with door opening (`Monster.cpp` 768). |
| `word2` bit 2 | `See360`. Sight ignores facing. |
| `word2` bit 3 | `word2Bit3`. No AI use found in this pass. **UNRESOLVED** effect. |
| `word2` bit 4 | `word2Bit4`. Attack picks a random living champion instead of the champion in front (`Monster.cpp` 1104). |
| `word2` bit 5 | `levitating`. May cross an open pit. |
| `word2` bit 6 | `nonMaterial`. Doors and physical immunity. See report 07. |
| `word2` bit 9 | `leavesDroppings`. |
| `word2` bit 10 | `absorbsDaggersEtc`. |
| `word2` bit 11 | `seesInvisible`. |
| `word2` bit 12 | `canSeeInDark`. |
| `word2` bit 13 | `invincible`. |
| `word4` | Graphic flags: side, back, attack, and mirror (`CSB.h` 2266–2288). |
| `movementTicks06` | `timePerMove`. 255 means `PossibleMove` always fails (`Monster.cpp` 664). |
| `attackTicks07` | Added into the attack timer (`Monster.cpp` 3836). |
| `defense08` | Melee armor term and missile divisor. Report 07. |
| `baseHealth09` | Base of the hit-point roll. Not the current hit points. |
| `attack10` | Melee damage seed and missile range seed (`Monster.cpp` 1129, 4558). |
| `poisonAttack11` | Poison chance input after a melee hit (`Monster.cpp` 4583). |
| `dexterity12` | To-hit target for the champion, and also the missile damage byte (`Monster.cpp` 1134). |
| `unused13` | Not read by the functions in this pass. |
| `word14` bits 0–3 | `sightDistance`. |
| `word14` bits 8–11 | `smellingDistance`. |
| `word14` bits 12–15 | `word14_12_15`. Greater than 1 means the monster may shoot (`Monster.cpp` 1053). |
| `word16` bits 4–7 | `bravery`. 15 never flees from War Cry (`Attack.cpp` 1496). |
| `word16` bits 8–11 | `word16_8_11`. Skill experience factor. Report 07. |
| `word16` bits 12–15 | `word16_12_15`. Must be at least 10 to take a monster teleporter (`Monster.cpp` 742). |
| `word18` bits 4–7 | `FireResistance`. |
| `word18` bits 8–11 | `PoisonResistance`. |
| `word20` bits 4–7 | Extra delay from move to attack. |
| `word20` bits 8–11 | Extra delay from attack to move. |
| `uByte22[2]` | Damage class `P4` passed to `DamageCharacter` (`Monster.cpp` 4579). |

Runtime group state is `DB4` (`CSB.h` fear, `numMonM1`, `hitPoints6[4]`, facing, `groupIndex`) plus `ITEM16` while the group is active on the party level. `ITEM16` holds the position byte, the facing byte, `uByte5` (a wander countdown), `uByte6`/`uByte7` (last seen party X/Y), and four `singleMonsterStatus` bytes (attacking and mirror).

Type names that this source prints are in `MonsterName` (`AsciiDump.cpp` 307). Types 5 and 25 fall through to `"Unrecognized_Type"`. The enums are `mon_5` and `mon_25` (`Objects.h` 160 and 180). Their display names are **UNRESOLVED**. Types 25 and 26 match Lord Chaos on every combat and AI byte extracted here. Their `word4` graphic words are 0, while Lord Chaos is `0x0038`.

Directions 0, 1, 2, 3 are north, east, south, west (`data.cpp` 1756, `Code11f52.cpp` 271). Position nibbles use that same 0–3 numbering. They are not screen corners by themselves.

## 3. Monster statistics

**VERIFIED** from graphic `0x22F` after the same byte swap `fixItem26` performs. Health is not a fixed value.

```
bonus = STRandom() % (baseHealth / 4 + 1)
hitPoints = baseHealth * hitPointMultiplier + bonus
```

`hitPointMultiplier` is bits 4–7 of the monster generator actuator (`CSB.h` 1070), passed into creature creation (`Monster.cpp` 191–196). A multiplier of 1 gives `baseHealth` through `baseHealth + baseHealth/4`.

`mv` is `timePerMove`. 255 never steps. `at` is `attackTicks07`. `see`, `sm`, and `r` are sight, smell, and `word14` bits 12–15. `br` is bravery. `xp` is `word16` bits 8–11. `tel` is `word16` bits 12–15 (teleporter gate and not the shoot flag). `fi` and `po` are fire and poison resistance nibbles. `dly` is move-to-attack, then attack-to-move. `P4` is `uByte22[2]`.

| ID | Name | Size | HP base | Def | Dex | Atk | Poi | Move | Atk ticks | See | Smell | Shoot if >1 | Brave | XP | Tel | Fire | Poison | Delays | P4 | Flags |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---|
| 0 | Scorpion | 2 | 150 | 55 | 55 | 150 | 240 | 8 | 20 | 3 | 1 | 1 | 9 | 9 | 2 | 7 | 8 | 5, 2 | 4 | absorb |
| 1 | Slime Devil | 0 | 110 | 20 | 20 | 80 | 15 | 15 | 32 | 2 | 1 | 3 | 10 | 3 | 3 | 4 | 14 | 8, 3 | 3 | absorb |
| 2 | Giggler | 0 | 10 | 50 | 110 | 10 | 0 | 3 | 5 | 6 | 3 | 1 | 0 | 1 | 7 | 3 | 2 | 2, 2 | 0 | absorb, random target |
| 3 | Flying Eye | 0 | 40 | 30 | 80 | 58 | 0 | 10 | 21 | 10 | 2 | 3 | 10 | 6 | 9 | 3 | 11 | 1, 1 | 5 | 360, levitate, absorb, random target |
| 4 | Hellhound | 1 | 101 | 45 | 65 | 90 | 0 | 9 | 8 | 4 | 5 | 1 | 15 | 8 | 5 | 3 | 10 | 4, 1 | 4 | absorb, drops |
| 5 | mon_5 | 1 | 60 | 100 | 30 | 30 | 0 | 20 | 18 | 2 | 2 | 1 | 3 | 3 | 4 | 8 | 5 | 6, 2 | 3 | absorb |
| 6 | Screamer | 0 | 165 | 5 | 5 | 5 | 0 | 120 | 10 | 1 | 1 | 1 | 15 | 0 | 1 | 6 | 7 | 15, 2 | 6 | 360, absorb, drops, bit 3 |
| 7 | Rock Pile | 0 | 50 | 170 | 10 | 40 | 5 | 185 | 15 | 3 | 4 | 1 | 12 | 5 | 2 | 14 | 6 | 15, 1 | 4 | drops |
| 8 | Rive | 0 | 30 | 15 | 80 | 55 | 0 | 11 | 16 | 3 | 4 | 1 | 6 | 6 | 4 | 12 | 15 | 1, 1 | 6 | 360, levitate, non-material, sees invisible, sees in dark |
| 9 | Stone Golem | 2 | 120 | 240 | 35 | 219 | 0 | 21 | 14 | 3 | 0 | 1 | 15 | 11 | 3 | 15 | 15 | 15, 4 | 3 | drops |
| 10 | Mummy | 0 | 33 | 25 | 40 | 20 | 0 | 17 | 12 | 4 | 2 | 1 | 9 | 4 | 5 | 1 | 15 | 8, 4 | 3 | absorb, sees in dark |
| 11 | Black Flame | 2 | 80 | 45 | 60 | 105 | 0 | 255 | 8 | 4 | 3 | 1 | 10 | 5 | 5 | 15 | 15 | 1, 1 | 1 | 360, non-material, sees invisible, sees in dark, immobile |
| 12 | Skeleton | 0 | 20 | 22 | 80 | 22 | 0 | 7 | 7 | 3 | 0 | 1 | 9 | 5 | 6 | 6 | 15 | 3, 1 | 4 | sees in dark, drops |
| 13 | Couatl | 2 | 39 | 42 | 88 | 90 | 100 | 5 | 10 | 3 | 3 | 1 | 3 | 7 | 5 | 3 | 6 | 1, 1 | 4 | levitate, absorb, sees in dark |
| 14 | Vexirk | 0 | 44 | 47 | 90 | 75 | 0 | 10 | 20 | 5 | 3 | 4 | 5 | 9 | 13 | 5 | 3 | 6, 6 | 5 | levitate, absorb, bit 3, random target |
| 15 | Worm | 1 | 70 | 72 | 35 | 45 | 35 | 18 | 19 | 1 | 10 | 1 | 10 | 5 | 1 | 9 | 11 | 5, 2 | 4 | drops |
| 16 | Ant Man | 0 | 20 | 28 | 41 | 25 | 0 | 13 | 8 | 3 | 3 | 1 | 4 | 1 | 2 | 2 | 3 | 3, 3 | 3 | absorb, drops |
| 17 | Muncher | 0 | 8 | 180 | 150 | 28 | 20 | 1 | 16 | 2 | 4 | 1 | 15 | 9 | 1 | 0 | 0 | 1, 1 | 4 | levitate, absorb |
| 18 | Deth Knight | 0 | 60 | 140 | 70 | 105 | 0 | 14 | 6 | 5 | 0 | 1 | 15 | 10 | 7 | 15 | 15 | 4, 1 | 4 | drops |
| 19 | Zytaz | 0 | 33 | 15 | 65 | 61 | 0 | 5 | 18 | 8 | 2 | 3 | 7 | 12 | 10 | 5 | 15 | 1, 1 | 5 | levitate, non-material |
| 20 | Water Elemental | 2 | 144 | 75 | 50 | 66 | 0 | 25 | 25 | 1 | 3 | 1 | 7 | 6 | 7 | 10 | 14 | 4, 3 | 3 | 360, non-material, sees in dark, bit 3, random target |
| 21 | Oitu | 2 | 77 | 33 | 60 | 130 | 0 | 7 | 15 | 2 | 5 | 1 | 6 | 9 | 6 | 5 | 8 | 2, 2 | 4 | |
| 22 | Demon | 0 | 100 | 68 | 75 | 100 | 0 | 10 | 14 | 4 | 3 | 4 | 15 | 13 | 11 | 5 | 10 | 2, 1 | 3 | absorb, sees in dark |
| 23 | Lord Chaos | 2 | 180 | 255 | 130 | 210 | 0 | 12 | 22 | 9 | 3 | 6 | 3 | 15 | 15 | 11 | 15 | 6, 5 | 5 | levitate, sees invisible, sees in dark, invincible, bit 3 |
| 24 | Dragon | 2 | 255 | 110 | 70 | 255 | 0 | 13 | 28 | 5 | 6 | 2 | 7 | 15 | 11 | 12 | 6 | 4, 4 | 4 | absorb, drops, bit 3 |
| 25 | mon_25 | 2 | 180 | 255 | 130 | 210 | 0 | 12 | 22 | 9 | 3 | 6 | 3 | 15 | 15 | 11 | 15 | 6, 5 | 5 | same AI bytes as Lord Chaos, `word4` = 0 |
| 26 | Grey Lord | 2 | 180 | 255 | 130 | 210 | 0 | 12 | 22 | 9 | 3 | 6 | 3 | 15 | 15 | 11 | 15 | 6, 5 | 5 | same AI bytes as Lord Chaos, `word4` = 0 |

Size 0 can be four monsters. Size 1 can be two. Size 2 is one. Vertical size is 0, 1, 2, or 3 as `vsz` from `word2` bits 7–8. It was extracted and is omitted from the table except where the door test needs it. The full vertical sizes are in the decode: Scorpion 1, Slime Devil 1, Giggler 2, Flying Eye 1, Hellhound 2, mon_5 3, Screamer 2, Rock Pile 2, Rive 0, Stone Golem 1, Mummy 1, Black Flame 1, Skeleton 1, Couatl 1, Vexirk 3, Worm 3, Ant Man 1, Muncher 1, Deth Knight 1, Zytaz 0, Water Elemental 1, Oitu 1, Demon 1, Lord Chaos 1, Dragon 1, mon_25 1, Grey Lord 1.

Where fire or poison resistance is applied inside damage was not re-traced in this pass. The nibbles are real. Their exact consumption is **PARTIALLY VERIFIED** (fields exist and are named; the call sites were not all opened).

## 4. Movement

**VERIFIED.** `PossibleMove` (`Monster.cpp` 635) tests one step in a compass direction.

It fails when:

- `timePerMove` is 255. `InitialChecks` still copies that 255 into a local and replaces it with 100, so reaction delays for an immobile monster use 100 rather than 255 (`Monster.cpp` 3031–3034). The step itself remains illegal.
- The destination is outside the map.
- A monster-move filter set `monsterMoveInhibit[direction]`.
- The cell is stone or stairs.
- The cell is an open pit, the monster does not levitate, and the fake-pit test fails.
- The cell is a closed false wall and the fake-wall test fails.
- The destination is the party’s cell (`moveOntoPartyPosition`).
- A real door is too closed for the monster’s vertical size and the monster is material. Door state 5 is allowed through. Non-material monsters pass.
- An active teleporter is not a monster teleporter, `word16_12_15` is below 10, or `MonsterAllowedOnLevel` fails.
- Another monster already occupies the cell.

A flux cage on an invincible monster sets `FluxCages[direction]` and also fails the step (`Monster.cpp` 715–729).

`PossibleDoubleMove` (`Monster.cpp` 818) checks the same direction one cell farther, unless that direction is a flux cage.

`TurnMonster` (`Monster.cpp` 840) does not turn through 180 degrees in one update. If the monster faces directly away from the preferred direction (difference 2), it turns 90 degrees left or right at random. Otherwise it snaps to the preferred direction. Worms (`P4` set, size 1) turn as a pair and will not turn twice in the same tick.

`Try_To_Move` (`Monster.cpp` 2177) is the flee/wander entry used when fear is 5:

1. `Blocked` looks for the party.
2. If the party is seen, the preferred direction is the primary direction plus 2 (directly away), and `timePerMove` is reduced by one quarter.
3. If not seen, `uByte5` counts down. While it is non-zero, half the time the code looks for any open direction. If the party is orthogonally adjacent and no move exists, fear becomes 0 and the group turns.
4. When `uByte5` hits 0, fear becomes 0.

`Try_Preferred_Direction_Then_Others` and `Try_Directions_D5_To_D4` try the preferred compass direction, then the others. This is not a path. A blocked preferred step becomes a different one-step direction or a turn.

Off the party level, `InitialChecks` does not run the full AI. For the group leader timers it tries one random direction and may `MoveObject` (`Monster.cpp` 2923–2944). Other member timers are skipped (`Monster.cpp` 2785–2804).

Movement frequency on the party level is `Larger(1, timePerMove + STRandom0_3() - 1)` ticks (`Monster.cpp` 1422). Example: a Worm with `timePerMove` 18 waits 17, 18, 19, or 20 ticks. A Muncher with 1 waits 1 tick (`1+0-1` is raised to 1; `1+1-1` is 1; `1+2-1` is 2; `1+3-1` is 3).

Party sleep does not appear in `Monster.cpp`. Monster timers keep firing. **VERIFIED** for this file. Freeze Life is the exception in section 7.

## 5. Detection

**VERIFIED.** `Blocked` (`Monster.cpp` 513) returns 0 when the party is not seen, otherwise the orthogonal distance.

Order:

1. If the party is invisible and the monster lacks `seesInvisible`, return 0. `d.Invisible` is the party flag. A monster’s own invisible bit is a different field.
2. Unless `See360`, collect the distinct facings in the group (or one member’s facing). For each facing, `IsItInThisDirection` tests whether the party lies in that facing. A miss continues to the next facing.
3. Sight range is `sightDistance`. If the monster cannot see in the dark, subtract `d.CurrentPalette / 2`. The result is at least 1. `CurrentPalette` is the light level from report 02. Darker palettes shorten sight for monsters that lack `canSeeInDark`.
4. If orthogonal distance is greater than that range, return 0.
5. `TestDirectPathOpen` (`Monster.cpp` 370) walks from the party toward the monster. Adjacent cells (distance ≤ 1) are open. Otherwise it steps along the line, choosing the step that stays closer to the original slope, and rejects the path when `BlockedTypeA` says the cell blocks. On an exact diagonal both side cells and the diagonal cell are tested.

Smell is separate and does not require facing (`GetBestMonsterFacingP1`, `Monster.cpp` 595):

- Smell 0 does nothing.
- If `(smell + 1) / 2` is at least the orthogonal distance, and `TestDirectPathOpen` using `StoneOrClosedFalseWall` is open, the monster prefers the primary direction toward the party.
- Otherwise `SearchFootprints` may point at an old party cell. The test is `random(4) + footprintAge > 30 - 2*smell`. A stronger smell accepts older tracks. The footprint stores a previous party coordinate, not a path.

Sound is not a detection input in these functions. **VERIFIED** absence in `Blocked` and `GetBestMonsterFacingP1`.

## 6. Pursuit

**VERIFIED.** There is no pathfinding search.

When the party is seen, `uByte6` and `uByte7` store party X and Y (`Monster.cpp` 2210–2212 and 3859–3862). Later movement uses `GetPrimaryAndSecondaryDirection` from the monster to that point. `GetPrimaryAndSecondaryDirection` returns one main compass step and one diagonal alternative. Corners are handled by trying the secondary direction when the primary step is blocked, not by planning around the corner.

Fear 5 reverses that direction (section 4). War Cry, Calm, Brandish, Blow Horn, and Confuse set fear 5 when they succeed (`Attack.cpp` 1512–1527) unless bravery is 15 or the random bravery test fails. Bravery 15 never takes fear 5 from that function.

If the party is not seen and smell fails, footprints are the only remembered position. If those fail, movement is a random legal step (`FindPossibleMove`, `Monster.cpp` 792) or a random turn (`Try_Directions_D5_To_D4` from timer 30, `Monster.cpp` 3558).

When the party is more than 3 cells away in X or in Y, an active member does not close in on that update. It only schedules the next B time using `AdditionalDelayFromMoveToAttack` (`Monster.cpp` 3742–3762).

## 7. Attacks

**VERIFIED** decision. **Damage math is report 07.**

`MonsterAttacks` (`Monster.cpp` 966) runs when a member’s update says it is attacking. It sets `LastMonsterAttackTime`, which report 07 uses for skill experience.

Shoot check: `word14_12_15 > 1` and (distance > 1 or a coin flip). Missile type is a type switch, not a table:

| Type | Missile |
|---|---|
| Vexirk, Lord Chaos | Half the time a fireball. Otherwise equal chance of Dispel, Lightning, Poison Cloud, or Zo. |
| Slime Devil | Poison cloud `RNPoison`. |
| Flying Eye | Lightning unless `STRandom(8)` is 0, then Zo. |
| Zytaz | Poison cloud or fireball, a coin flip. |
| Demon, Dragon | Fireball. |

Range is `attack/4 + 1`, then plus `STRandom(range)` twice (`Monster.cpp` 1129–1131). The damage byte passed with the missile is `dexterity12`, not `attack10`.

Giggler sets `monsterShouldSteal` and uses `StealFromCharacter` (`Monster.cpp` 229), which calls `Quickness`.

Target: if `word2Bit4` is set, a random party slot with hit points. Otherwise `GetCharacterToDamage` uses the monster’s side of the cell. Report 07’s “can’t reach” rule is the champion-side version of the same occupancy test.

Melee damage, poison, armor, and shields are `MonsterDamagesCharacter` (`Monster.cpp` 4509) and `DamageCharacter`. Skill 7 (Parry) is granted `word16_8_11` and then subtracted twice from the damage seed. See report 07 sections 2, 3, and 8.

Attack period, once the member is marked attacking (`Monster.cpp` 3830–3845):

```
delay = attackTicks + STRandom0_3() - 1
if attackTicks > 15:
    delay += (STRandom() & 7) - 2
```

The second term can be negative. A Scorpion (`attackTicks` 20) therefore waits `20+0-1` through `20+3-1`, then adds a value from −2 to +5. A Deth Knight (`attackTicks` 6) does not get the second term.

The next AI time also adds `AdditionalDelayFromAttackToMove` or `AdditionalDelayFromMoveToAttack`, plus 0 or 1 (`NextMonsterUpdateTime`, `CSBCode.cpp` 6316–6330).

Group members do not share one attack. Timers `TT_MONSTER_A0`–`A3` and `B0`–`B3` are per index. Only the member whose timer fired is passed to `MonsterAttacks`.

## 8. Groups and subtiles

**VERIFIED** occupancy. **UNRESOLVED** screen-corner names.

`CreateMonster` (`Monster.cpp` 140) clamps the count: size 0 keeps the requested count, size 1 forces at most 2 (`numMonM1` ≤ 1), size 2 forces exactly 1.

Positions are two-bit nibbles in one byte (`Monster.cpp` 192–208):

- One monster: the position byte is 255, which means “no corner”, and drawing treats the monster as filling the cell.
- Several monsters: start at `STRandom0_3()` and increment. Size 1 increments an extra step so the two monsters sit on opposite values. The index wraps with `& 3`.

Those values are the same 0–3 compass positions as champion `charPosition`, not “top left” by themselves. `DrawMonster` (`Code51a4.cpp` 581) looks up `MonsterXYonScreen[2 * positionWithinCell]` in graphic `0x22E`, selected by relative cell. Relative cell already depends on party facing. This pass did not decode that graphic into top left, top right, bottom left, and bottom right. That mapping is **UNRESOLVED**. Unity’s four names must not be assigned to nibbles 0–3 until that table, or an equivalent facing test, is read.

The group moves as one object. `MoveObject` carries every member. Turning can be individual or paired. Death removes one index, shifts the later hit points and position nibbles down, and deletes that member’s timer (`Code11f52.cpp` 1237–1258, `DeleteMonsterInGroup` in `Monster.cpp` 4599). The survivor keeps its own nibble. There is no split into a second `DB4` during ordinary movement. `InsertMonsterInGroup` (`Monster.cpp` 4751) can add a member if a corner is free: 4 for size 0, 2 for size 1, none for size 2.

On the party level, fear 6 and a bravery roll can set fear 5 when a member dies (`Code11f52.cpp` 1221–1232). Bravery 15 skips that.

## 9. Timers

**VERIFIED.** All of these are entries in the one game-timer queue from report 01. Times are `d.Time` ticks.

| Timer | Value | Role |
|---|---:|---|
| `TT_M1` | −1 | Party bumped the monster or the monster was attacked. Converted to timer 31. |
| `TT_M2` | −2 | Monster took damage. Converted to timer 30. |
| `TT_M3` | −3 | Cloud or door damage. Converted to timer 29. |
| `TT_29`–`TT_31` | 29–31 | The positive forms of those three reactions. |
| `TT_MONSTER_Agroup` | 32 | Group A update. |
| `TT_MONSTER_A0`–`A3` | 33–36 | Member 0–3, phase A. |
| `TT_MONSTER_Bgroup` | 37 | Group B update. This is the timer `StartMonsterMovementTimers` posts (`CSBCode.cpp` 6361). |
| `TT_MONSTER_B0`–`B3` | 38–41 | Member 0–3, phase B. Phase B is where attacks and steps are launched. |

`InitialChecks` (`Monster.cpp` 2763):

- Drops the event if the monster record is gone.
- Off the party level, ignores every timer except 32, 33, 37, and 38.
- A member with 0 hit points is removed through `DamageMonster`, and the same timer is posted again at the current time.
- Sets `attackDistance` to 24 before the real sight test. The comment says the Atari binary left this uninitialized and this port patched it (`Monster.cpp` 2889–2902). **Atari difference.**
- Clears timer byte 8. The comment says the Atari value was sometimes garbage (`Monster.cpp` 2910–2922). **Atari difference.**
- If `freezeLifeTimer` is set and the monster is not invincible, the same timer is postponed 4 ticks and the function returns (`Monster.cpp` 3036–3051). Invincible monsters ignore Freeze Life.
- Negative functions are rewritten to 29, 30, or 31 and delayed. `TT_M1` waits 1 tick. The others wait `(timePerMove+2)/4 - age`, at least 1.

Timer 31 (`Monster.cpp` 3439): if fear is not 5 or 6, delete movement timers, set fear 6, and turn members. If fear is already 5 or 6, store the party coordinates and return.

Timer 30 (`Monster.cpp` 3478): fear 5 or 6 does nothing. Fear 2 or 3, or a random 3-in-4, turns the group if `Blocked` is clear, then reschedules. Otherwise it picks a random direction and calls `Try_Directions_D5_To_D4`.

Fear 0 or 3 on the A-timer path can set fear 7, store the party cell, and wait 1 tick (`Monster.cpp` 3692–3711). The source comment says the condition looks redundant against the original machine code and might have been written backwards. **PARTIALLY VERIFIED**: the C++ condition is what this tree runs; the intended Atari boolean is marked uncertain by the author.

Party sleep does not cancel these timers. Monsters outside the viewport still run if their timer is due. Off-level groups only move on the leader timers, one random step at a time.

`NextMonsterUpdateTime` also writes the mirror and attacking bits from `word4` nibbles 12–13 and 14–15 (`CSBCode.cpp` 6218–6303). Deth Knight is the only type with an extra mirror sound (sound 16) when it is already attacking.

## 10. Reactions

**VERIFIED** unless marked.

| Event | Behavior |
|---|---|
| Champion melee or bump (`TT_M1`) | Becomes timer 31. Fear other than 5 or 6 becomes 6, timers are cleared, members turn. Fear 5 or 6 only records the party cell. |
| Damage (`TT_M2`) | Becomes timer 30. Fear 5 or 6 ignores it. Otherwise turn or try a random direction. |
| Cloud or door damage (`TT_M3`) | Becomes timer 29. Timer 29 sets the function back to `TT_M3`, picks `STRandom0_3()` as the direction, and calls `Try_Directions_D5_To_D4` (`Monster.cpp` 3588–3626). |
| Projectile | Impact damage is report 07. A hit that kills a member uses the group-death path. Black Flame is healed by fireballs in `Code11f52.cpp` 2001. That is type-specific. |
| Blocked by a monster | `PossibleMove` returns false. The caller tries another direction or waits. |
| Closed door | Material monsters stop when the door byte is greater than their vertical size, except state 5. Non-material monsters pass. |
| Party moves away | The stored cell in `uByte6`/`uByte7` remains until the next sight success. Smell and footprints can still point at the party. Beyond 3 cells in X or Y the member does not step closer on that update. |
| Party invisible | `Blocked` returns 0 unless `seesInvisible`. Smell does not check `d.Invisible`. **VERIFIED** for those two functions. A smelling monster can still turn toward an invisible party inside smell range. |
| Frightened | Fear 5 flees (direction + 2) and moves 25% sooner. Fear 6 is the active/alert state used after contact. Fear 7 is the “just lost them” state that stores the party cell. Bravery 15 skips War Cry fear and skips the death-fear roll. |
| Freeze Life | Non-invincible groups postpone the current timer by 4 ticks and skip the rest of `InitialChecks`. |
| Member dies | Later members shift down. Fear 6 may become fear 5. The group object remains if anyone is alive. |
| Giggler | Steal instead of a normal strike. |
| Screamer | `P4` 6, so champion damage uses the Wisdom formula in report 07. Move delay 120. Bravery 15. |

## 11. Dungeon features

**VERIFIED** in `PossibleMove` unless noted.

| Feature | Monster result |
|---|---|
| Stone | Blocked. |
| Stairs | Blocked. Monsters do not climb stairs in this function. |
| Open pit | Blocked unless levitating, or the fake-pit bit allows it when the caller passes the false-pit flag. Fleeing sets that flag (`Try_To_Move` passes true). |
| False wall | Closed wall blocks unless the fake bit and the same flag allow it. |
| Door | Material monsters need the door byte ≤ vertical size, or state 5. Non-material monsters pass. |
| Teleporter | Taken only when active, aimed at monsters, `word16` bits 12–15 ≥ 10, and the destination level allows the type. Otherwise the step fails. |
| Another monster | Step fails. |
| Party cell | Step fails. Monsters do not walk onto the party. They attack from the adjacent cell. |
| Pressure plate | Not tested inside `PossibleMove`. A successful `MoveObject` is the shared step function. Plate actuators live in `Code11f52.cpp` around the “stepped on pressure pad” trace (line 3935). **PARTIALLY VERIFIED**: monsters use `MoveObject`, and that path knows about plates. This pass did not follow every actuator target. |
| Flux cage | Invincible monsters treat it as a blocked direction and count it. Fusion is a party action, not a monster step. |

## 12. Graphics and animation timing

**VERIFIED** selection rules. Frame counts inside the graphic file were not dumped. **PARTIALLY VERIFIED** for how many frames each type has.

`DrawMonster` in `Code51a4.cpp` (the block at 567–700, and a second copy near 1392) chooses the picture:

- Side view if `HasSideGraphic` and the relative facing is odd (facing across the party’s view).
- Else back view if `HasBackGraphic` and relative facing is 0 (facing away).
- Else attack view if the member’s attacking bit is set and `HasAttackGraphic`.
- Else the front view.
- Mirror when `NonAttackingMirrorOK` or `AttackingMirrorOK` and the status bit is set. The bit is chosen in `NextMonsterUpdateTime`, not by a walk-cycle timer.
- Graphic number starts at `446 +` the monster’s graphic index, then increments for side, back, or attack. Distance changes which viewport slot is used (`RelativeCellNumber` and `MonsterXYonScreen`). Farther cells use different destination rectangles from graphic `0x22E`. There is not a separate animation clock. The attacking bit and the mirror bit are the frame state, and they change when the AI timer runs.

Left and right are mirrors of one drawing when the mirror flag is set. Types without `NonAttackingMirrorOK` clear the mirror bit.

This is independent of Unity’s V17 renderer. Nothing here authorizes a change to that renderer. Screen corner names still have to be derived from position nibble plus party facing plus the `0x22E` coordinate table. That last table was not decoded.

## 13. Worked examples

**Worm sees the party.** Size 1, sight 1, no dark vision, smell 10, move 18.

Orthogonal distance 1 is inside sight even after `CurrentPalette/2`, because sight is clamped to at least 1. `TestDirectPathOpen` returns immediately for distance ≤ 1. `Blocked` can succeed if a member faces the party. The B timer then calls `MonsterAttacks` when that member’s attacking bit is set. Damage uses attack 45, defense 72, dexterity 35, and `P4` 4. Report 07 applies armor. The next step waits 17–20 ticks unless fear 5 shortens `timePerMove` by a quarter.

**Black Flame.** `timePerMove` 255, so `PossibleMove` returns false. It still has sight 4, 360-degree vision, and sees in the dark. It can face and attack. It does not walk. A fireball adds hit points instead of removing them (`Code11f52.cpp` 2001).

**Giggler and an invisible party.** Sight 6, no `seesInvisible`, smell 3. `Blocked` returns 0 while `d.Invisible` is set. Smell range `(3+1)/2 = 2` can still return a direction if stone and closed false walls do not block. Steal is selected only when `MonsterAttacks` runs.

**Lord Chaos and Freeze Life.** Invincible, so `InitialChecks` does not take the 4-tick postpone. Teleporter gate 15 passes the `>= 10` test. Shoot flag 6 is greater than 1, so the missile switch can run.

**Screamer in the dark.** Sight base 1, and it cannot see in the dark, so range becomes `Larger(1, 1 - CurrentPalette/2)`, which is 1. It only sees an adjacent party. Move delay 120 means steps are rare. Bravery 15 ignores War Cry fear.

## 14. Atari versus CSBwin

**VERIFIED** as comments in this tree, not by opening an Atari binary.

- `attackDistance` is set to 24 before use. The author says the Atari value was uninitialized (`Monster.cpp` 2889).
- New monster timers clear byte 8. The author says Atari sometimes left garbage there (`Monster.cpp` 2910).
- `PossibleMove` uses the current monster as `d.Obj13020`. The comment says Atari could have that word zero or stale (`Monster.cpp` 640–650 and 747–750).
- Fear 0/3 handling includes a condition the author could not reconcile with a straightforward reading of the machine code (`Monster.cpp` 3685–3692).
- Report 07’s Anti-Fire / Anti-Magic choice remains the only approved Unity difference. Monster AI should follow this CSBwin tree unless a later decision says otherwise. The timer-byte and `attackDistance` patches are already what this tree plays.

## 15. Unresolved

- Printed names for types 5 and 25.
- Which position nibble is top left, top right, bottom left, and bottom right after party facing. The pixels live in graphic `0x22E`.
- Every call site of fire resistance and poison resistance.
- How `Try_Directions_D5_To_D4` orders the three directions after the random start used by timer 29. The call itself is verified.
- Whether the fear 0/3 test matches the Atari binary.
- Per-type graphic frame counts inside the monster graphic set.
- `word2` bit 3.
- Actuator targets reached when a monster, rather than the party, steps on a plate.

## 16. Source index

| Topic | Function | File |
|---|---|---|
| Descriptor | `MONSTERDESC`, `fixItem26` | `CSB.h` 2291, `CSBCode.cpp` 10452 |
| Names | `MonsterName` | `AsciiDump.cpp` 307 |
| Creation and corners | creature creation at `Monster.cpp` 140 | `Monster.cpp` |
| Step test | `PossibleMove`, `PossibleDoubleMove` | `Monster.cpp` 635, 818 |
| Turn | `TurnMonster` | `Monster.cpp` 840 |
| Sight | `Blocked`, `TestDirectPathOpen` | `Monster.cpp` 513, 370 |
| Smell and footprints | `GetBestMonsterFacingP1` | `Monster.cpp` 595 |
| Scheduler | `ProcessTimers29to41`, `InitialChecks` | `Monster.cpp` 3278, 2763 |
| Flee and wander | `Try_To_Move` | `Monster.cpp` 2177 |
| Attack launch | `MonsterAttacks` | `Monster.cpp` 966 |
| Melee damage | `MonsterDamagesCharacter` | `Monster.cpp` 4509 |
| Next time, mirror, attack bit | `NextMonsterUpdateTime` | `CSBCode.cpp` 6187 |
| First B timer | `StartMonsterMovementTimers` | `CSBCode.cpp` 6334 |
| Move period | `Increment_Time_By_W52_Plus_Random` | `Monster.cpp` 1413 |
| Drawing | monster draw block | `Code51a4.cpp` 567 |
| Timer numbers | `TIMERTYPE` | `CSB.h` 1782 |

Internet notes that match this reading: Paul Stevens’ monster-AI trace strings are the comments already compiled into `Monster.cpp`. No second algorithm was taken from the web. Dungeon Master Encyclopedia was not used to override a field.

## 17. Notes for a later Unity port

These are constraints for a future port, not a request to change the current renderer.

- Drive monsters from the same tick as report 01. Do not invent a seconds-based cooldown.
- Use the 27-row table in section 3 and the hit-point formula. Do not store one fixed health.
- Sight, smell, footprints, and random steps are the whole pursuit model.
- Keep groups as one object with up to four hit-point slots and 2-bit positions. Size 1 uses opposite nibbles. Size 2 uses one monster and position 255.
- Map nibbles 0–3 through party facing before using top left, top right, bottom left, and bottom right. Do not guess that mapping.
- Call the report 07 damage function for melee. Use `P4` from this table.
- Freeze Life delays non-invincible groups by 4 ticks. Party sleep does not.
- Stairs, stone, the party cell, and a cell that already holds a monster are illegal steps.
- Leave V17 visibility and clipping alone. Monster pictures are selected by relative facing, the attacking bit, and the mirror bit.
