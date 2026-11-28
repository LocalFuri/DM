All 27 monster types share one timer function, `ProcessTimers29to41` in `Monster.cpp`. The descriptor supplies the numbers. A few types add a missile or a theft inside that same function. There is no pathfinder and no separate idle, wander, or panic program.

This is the Atari translation. The author compared it with Atari machine language and, in one comment, with PC behavior. The Monster Move Filter, the Monster Attack Filter, and the delete filter are CSBwin additions. They run around the original decisions and can change them. No DOS routine was opened for this pass.

## 1. The pipeline

Loading a level calls `ProcessMonstersOnLevel` (`CSBCode.cpp` 6504). For every monster it deletes movement timers, attaches an `ITEM16` group record, and starts new timers. A monster on a level the party is not visiting does not run the full AI. `InitialChecks` (`Monster.cpp` 2785–2805) keeps only the group-leader timers, and those take one random legal step (`Monster.cpp` 2923–2944).

The live AI is timers 29 through 41 (`Monster.cpp` 3278). They are not a named state machine. The persistent state is the 4-bit fear field on the monster (`CSB.h` 1162) plus the party coordinates saved in `ITEM16` bytes 6 and 7.

| **Timer**         | **What it does**                                                                                                                                                           |
| :---------------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 29                | Pick a random direction and try to move.                                                                                                                                   |
| 30                | Usually turn the group. One time in four, if fear is not 2 or 3, try a random move instead.                                                                                |
| 31                | If fear is not 5 or 6, enter melee: set fear to 6 and turn the members toward the party.                                                                                   |
| A-group and A0–A3 | Update one member’s pose. If the party is visible and more than one tile away, set fear to 7 and remember the party’s coordinates. If the party is adjacent, enter fear 6. |
| B-group and B0–B3 | Move, flee, or attack.                                                                                                                                                     |

`timePerMove() == 255` makes `PossibleMove` return false (`Monster.cpp` 664), so that monster never steps. There is no separate sleep flag.

Fear, as the code uses it:

| **Value** | **Effect**                                                                                                                            |
| --------: | :------------------------------------------------------------------------------------------------------------------------------------ |
|      0, 3 | Seeing the party at a distance sets fear to 7.                                                                                        |
|      2, 3 | Timer 30 always considers turning instead of the usual one-in-four random move.                                                       |
|         5 | Retreat. Movement aims away from the last seen party position, and the move time is reduced by one quarter (`Monster.cpp` 2335–2346). |
|         6 | Melee. Members turn toward the party and their attack timers run.                                                                     |
|         7 | Pursuit. The saved coordinates are the goal. Leaving melee also sets fear to 7 (`Monster.cpp` 1439–1443).                             |

A companion’s death can set fear to 5. If bravery is not 15, the test is `bravery + monstersRemaining - 1 < random(16)` (`Code11f52.cpp` 1221–1232). Bravery 15 never flees. Fear values of 4 and above are cleared back to 0 in two other places (`CSBCode.cpp` 6344 and 6481).

The only type-specific branches inside the shared attack are the missile choice and the Giggler’s theft (`Monster.cpp` 1059–1101).

## 2. Perception

`Blocked` (`Monster.cpp` 513) is vision.

- If the party is invisible and the monster lacks `seesInvisible()`, the result is 0.
- Otherwise each living member must be facing the party, unless `See360()` is set.
- Sight range is `sightDistance()`, the low four bits of descriptor word 14. If the monster cannot see in the dark, the current palette divided by 2 is subtracted, and the result is at least 1 (`Monster.cpp` 575–580).
- The distance is Manhattan: `abs(dx) + abs(dy)` (`Monster.cpp` 4497–4503).
- `TestDirectPathOpen` then walks the straight line (`Monster.cpp` 370). Adjacent cells are always open. Farther cells are blocked by stone and by closed false walls through `BlockedTypeA`. Doors in states 3 and 4 also block unless descriptor bit `0x100` is set (`Monster.cpp` 283–316). Bashed doors, state 5, do not block there.
- Nothing in this function looks at another level.

Smell is `GetBestMonsterFacingP1` (`Monster.cpp` 595). If `smellingDistance()` is 0, it returns 0. Otherwise, when half that distance, rounded up, reaches the party and the line is not stone or a closed false wall, the monster faces the party. If not, it may follow a footprint. The footprint is used when `random(0..3) + footprintAge > 30 - 2 * smellingDistance` (`Monster.cpp` 613–626). Those footprints are the same list the floor renderer uses. They are not created by the monster.

When vision succeeds, bytes 6 and 7 of `ITEM16` store the party’s current coordinates (`Monster.cpp` 3699–3700 and 3861–3862). Later movement that cannot see the party walks toward those saved coordinates (`Monster.cpp` 2335). That is the only memory. It is not updated by smell alone.

## 3. Movement

A move is one tile in one direction. `PossibleMove` (`Monster.cpp` 635) rejects the step when:

- the monster’s move time is 255,
- the destination is outside the map, stone, or stairs,
- an open pit is real and the monster does not levitate,
- a false wall is closed and the monster does not treat it as open,
- the destination is the party’s tile,
- a door is too closed for the monster’s height, unless the monster is non-material or the door is bashed,
- another monster already occupies the tile.

An active teleporter is entered only when `word16` bits 12–15 are at least 10 and the monster is allowed on the destination level (`Monster.cpp` 738–754). The author says he patched the Atari code so the teleporter test uses the monster that is actually moving.

`Try_Preferred_Direction_Then_Others` tries the chosen direction, then the other three. There is no search beyond the next tile. On a level without the party, the direction is `random(0..3)` (`Monster.cpp` 2926).

The time until the next move is `max(1, timePerMove + random(0..3) - 1)` (`Monster.cpp` 1413–1423). Turning inside a tile, before an attack, uses `max(1, timePerMove / 2 + random(0 or 1))` (`Monster.cpp` 3983–3984). Retreat multiplies the move time by 3/4 (`Monster.cpp` 2346).

The whole group changes tiles together. Inside the tile, each member has its own 2-bit facing and 2-bit corner in `ITEM16`. A group of one that is not full-tile size sometimes recenters or shifts a corner when `random(0..255)` has its top two bits clear (`Monster.cpp` 3866–3908).

## 4. Groups

One `DB4` holds up to four creatures. `numMonM1()` is the count minus one. They share the map position, the fear value, and the move. Each has its own hit points, facing, corner, pose byte, and A/B timer.

Horizontal size comes from descriptor word 2, bits 0–1 (`CSB.h` 2322):

| **Value** | **Space**                                 |
| --------: | :---------------------------------------- |
|         0 | One corner.                               |
|         1 | Two corners.                              |
|         2 | The whole tile. The group turns together. |

When one member dies and others remain, its hit points, facing, and position are removed and the later members shift down (`Code11f52.cpp` 1237–1258). Its timer is deleted and higher member timers are renumbered (`Code11f52.cpp` 1189–1209). The group does not split into another map record. `InsertMonsterInGroup` exists, but the only caller is the CSBwin DSA code (`DSA.cpp` 1780), not this AI.

## 5. Combat

A B-timer for one member attacks when all of these are true (`Monster.cpp` 3911–3917):

- `Blocked` for that member is not zero, so it can see the party.
- It faces the party, or it can see in all directions.
- That result is less than or equal to bits 12–15 of word 14.
- The party shares the monster’s row or column.
- Those same bits are less than or equal to `random(16) + 1`, a value from 1 through 16.

If the range is exactly 1, a small monster may first step to the corner nearest the party (`Monster.cpp` 3920–3980). That costs the half-move delay above and returns without attacking yet.

`MonsterAttacks` (`Monster.cpp` 966) then runs. Its return value is passed to `NextMonsterUpdateTime`, which sets the attacking pose bit. That is the connection between this logic and the attack graphic. The next B-timer clears the bit (`Monster.cpp` 3830–3834).

Missile launch is chosen when bits 12–15 of word 14 are greater than 1, and either the party is more than one tile away or a coin toss is true (`Monster.cpp` 1053–1055). The missile depends on the type:

| **Type**           | **Missile**                                                                                         |
| :----------------- | :-------------------------------------------------------------------------------------------------- |
| Vexirk, Lord Chaos | Fireball, or one of Dispel, Lightning, Poison Cloud, and Zo, each with probability 1/2 and then 1/4 |
| Slime Devil        | Poison                                                                                              |
| Flying Eye         | Lightning seven times in eight, otherwise Zo                                                        |
| Zytaz              | Poison Cloud or Fireball, equal chance                                                              |
| Demon, Dragon      | Fireball                                                                                            |
| Giggler            | No missile. It steals from the chosen champion.                                                     |

Range is `attack / 4 + 1`, then that value is added twice as `random(range)` (`Monster.cpp` 1129–1131). Damage carried by the missile is `dexterity12`. The decay rate is the constant 8. The result is limited to 20 through 255.

A melee target is a random living champion if word 2 bit 4 is set (`Monster.cpp` 1104–1114). Otherwise it is the champion returned by `GetCharacterToDamage`. The attack sound is `uByte9672[attackSound - 1]` when `attackSound` is not zero (`Monster.cpp` 1260–1265).

`MonsterDamagesCharacter` (`Monster.cpp` 4509\`) uses integer arithmetic:

level = 2 \* experienceMultiplier

needed = random(32) + dexterity + level - 16

If the champion’s quickness is at least `needed` and `random(4)` is not zero, the attack misses. A separate luck test with threshold 60 can also miss. A sleeping party skips both tests and wakes.

The damage mask is chosen from `word22` by scanning its nibbles with `random() & 15` when `random() & 0x70` is nonzero. Otherwise the mask is the low bit of that random word (`Monster.cpp` 4543–4557).

raw = level + random(16) + attack

raw = raw - 2 \* fighterMastery

if raw <= 1:

&#x20;   miss if random(2) is nonzero

&#x20;   else raw = random(4) + 2

raw = raw / 2

raw = raw + random(raw) + random(4)

raw = raw + random(raw)

raw = raw / 4

raw = raw + random(4) + 1

if random(2) is nonzero:

&#x20;   raw = raw - (random(raw / 2 + 1) - 1)

`DamageCharacter` applies that result. If it is nonzero and `poisonAttack` is nonzero, a coin toss calls `PoisonCharacter` (`Monster.cpp` 4583–4591).

After the attack, the timer gains `word20` bits 0–3 plus `random(0 or 1)` (`Monster.cpp` 4002–4010). If the member was already in the attack pose, the timer instead gains `attackTicks + random(0..3) - 1`, and when `attackTicks` is greater than 15 it also gains `(random() & 7) - 2`, a value from -2 through 5 (`Monster.cpp` 3834–3844).

The Monster Attack Filter (`Monster.cpp` 1139–1187) is a CSBwin DSA. It can replace the target, the missile, and the damage before the original resolution.

## 6. Damage and death

`DamageMonster` (`Code11f52.cpp` 1067) ignores the hit when the descriptor says invincible, or when that member’s hit points are `0xffff`. Otherwise it subtracts the supplied damage from `hitPoints6[member]` (`Code11f52.cpp` 1295–1297). Hit points reaching zero removes that member.

Physical hits from the party are resolved in `DeterminePhysicalAttackDamage` (`Attack.cpp` 560). A non-material monster is skipped unless the attack is marked vorpal or disrupt (`Attack.cpp` 601). A successful swing compares throwing distance with `defense08 + experienceMultiplier + random(32)`. Diamond Edge reduces that defense by one quarter. The Executioner reduces it by one eighth (`Attack.cpp` 675–697). Fire resistance and poison resistance are descriptor fields (`CSB.h` 2393–2394). This function does not apply them. They are used by the spell and cloud damage paths, not by this physical formula.

The last member’s death calls `DropMonsterRemains` and `DeleteMonster` (`Code11f52.cpp` 1122–1130). `DeleteMonster` clears the eight hit-point bytes, removes the object from the tile, frees the database entry, and releases the `ITEM16` slot (`Code11f52.cpp` 1039–1058).

`DropMonsterRemains` (`Code11f52.cpp` 845) drops the descriptor’s remains only when `leavesDroppings()` is set. The remains tables are the 40 words `MonsterDroppings` in graphic `0x22f`. Each type that drops uses a fixed starting index: Dragon 0, Worm 11, Screamer 15, Hellhound 18, Rock Pile 21, Deth Knight 26, Ant Man 33, Stone Golem 35, Skeleton 37 (`Code11f52.cpp` 709–738). A word with bit 15 set is skipped half the time. The low 15 bits select a weapon, clothing, or miscellaneous object by the same 23 and 69 boundaries as the object descriptors. The monster’s carried possessions are then placed on random corners (`Code11f52.cpp` 910–926). The sound is 4, `soundMONDEATH`, unless a weapon was dropped, in which case the sound is 0 (`Code11f52.cpp` 930).

A death cloud uses `RNMonsterDeath`. Its size argument is 110, 190, or 255 when the horizontal size is 0, 1, or 2 (`Code11f52.cpp` 1261–1288). The delete filter can suppress that cloud. That filter is a CSBwin DSA.

## What this source does not settle

- DOS PC formulas. The author’s note at `Monster.cpp` 2892–2902 says an uninitialized attack distance made PC and Atari traces diverge, and that he patched the Atari image to store 24. That is his patch, not a DOS disassembly.
- The numeric bravery, sight, smell, and attack values. Those are the 27 descriptor records you already extracted. The functions above are what consume them.
- Whether fear value 4 has a behavior of its own. The AI tests 0, 2, 3, 5, 6, and 7. Value 4 is only cleared back to 0.


