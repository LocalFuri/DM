# 06 — Other time-dependent mechanics

Unless noted, these are entries in the same queue that `ProcessTimers` drains at the start of each tick (`CSBCode.cpp` 6732–6936). A timer runs when its `Time()` is less than or equal to `d.Time` (`Timer.cpp` 1205–1239). The tick length is report 01. Counts below are ticks, not seconds.

Two counters are not queue entries. `_MainLoop` decrements them itself: `d.freezeLifeTimer` and `d.partyMoveDisableTimer` (`CSBCode.cpp` 679–689).

## Poison

**CONFIRMED.** `PoisonCharacter` (`CSBCode.cpp` 7584–7612).

```
if champion index < 0: return
if this champion is being recruited: return
DamageCharacter(champion, max(1, strength / 64), 0, 0)
strength -= 1
if strength != 0:
    poisonCount += 1
    schedule TT_75 at d.Time + 36
    timer byte 5 = champion index
    timer word 6 = strength
```

`TT_75` (`CSBCode.cpp` 6902–6911) decrements `poisonCount` and calls `PoisonCharacter` again with the stored strength. The gap between damage applications is **36 ticks**.

At 6 Hz that is 6 seconds. At this port's 255 ms Normal tick it is 9180 ms.

Monster hits call `PoisonCharacter` when `MONSTERDESC::poisonAttack11` is non-zero and `STRandomBool()` is non-zero, after `TAG016426` adjusts the strength (`Monster.cpp` 4583–4591). The initial strength is that adjusted monster value. Each later tick divides the remaining strength by 64, with a minimum of 1, then subtracts 1 from the strength used to schedule the next tick.

`poisonCount` is the number of outstanding poison timers (`CSB.h` line 2541). Anti-venom and death clear poison by deleting those timers (`Character.cpp` around 5274 and 5319). The feed path sets `fp.antiVenin` for `potion_AntiVenin` (`Character.cpp` 4199–4202).

## Light, darkness, shields, invisibility

Timer types are in `CSB.h` 1782–1838.

| Type | Role | When it is set | On expiry |
|---|---|---|---|
| `TT_LIGHT` (70) | Step `d.Brightness` | Spell, staff Light, and each decay step | `ProcessLightLevelTimer`. Next step at `d.Time + 4` while steps remain. Report 02 |
| `TT_71` | Invisibility counter | Spell missile type 3 | `d.Invisible--` (`CSBCode.cpp` 6867) |
| `TT_72` | One champion's shield | Potion `potion_YaPotion` | Subtract `timerUWord6` from `shieldStrength` (6868–6896) |
| `TT_73` | See through walls | Spell missile type 2, and the Window attack | `d.SeeThruWalls--` (6897) |
| `TT_74` | Party shield | Spell missile type 4 | Subtract `timerWord6` from `d.PartyShield` (6898–6900) |
| `TT_77` | Spell shield | Attack shield, `Attack.cpp` 1204–1226 | Subtract `timerWord6` from `d.SpellShield` (6913–6915) |
| `TT_78` | Fire shield | Same block, 1213–1226 | Subtract `timerWord6` from `d.FireShield` (6917–6919) |
| `TT_79` | Magic footprints | Spell missile type 6 | `d.MagicFootprintsActive--` (6920) |

`TT_77` / `TT_78` duration is the shield amount added, in ticks (`Attack.cpp` 1221–1222): `Time(d.Time + strength)`. If a shield of that family is already above 50, the added amount is divided by 4 before it is stored.

Ya potion (`Character.cpp` 4214–4238): shield gain is `w_12`, or `w_12 / 4` if `shieldStrength > 50`. Duration is `w_12 * w_12` ticks. `w_12` starts as `potionStrength / 25 + 8`.

Spell class 3 durations other than light (`Magic.cpp` 1266–1332):

```
D6W = 4 * (spellPower + 1)
```

- See-through walls (type 2): `D6W /= 2`, then duration `D6W * D6W` ticks, and `d.SeeThruWalls` increments.
- Invisibility (type 3): duration is `D6W` ticks (the square step is skipped by the `goto tag01cdaa`), and `d.Invisible` increments.
- Party shield (type 4): added shield is `D6W`, or `D6W >> 2` if `d.PartyShield > 50`. Duration becomes `D6W * D6W` after that possible divide.
- Footprints (type 6): duration `D6W * D6W`.

Window attack (`Attack.cpp` 1986–1994): `TT_73` at `d.Time + (random(mastery of skill 13 + 8) + 5)`, and `d.SeeThruWalls` increments.

## Freeze Life

**CONFIRMED.** Not a queue timer. `d.freezeLifeTimer` decrements by 1 at the end of every tick (`CSBCode.cpp` 679–680).

The Freeze Life attack (`Attack.cpp` 2019–2048):

- Magical box A adds 30.
- Magical box B adds 125.
- Otherwise adds 70 and spends a charge.
- If `freezeLifeTimer + amount > 200`, the added amount is reduced to `200 - freezeLifeTimer`.
- Stored result is `min(255, freezeLifeTimer + amount)`.

The 200 test and the 255 store both exist. The 200 test can leave the timer at 200. The final `Smaller(255, …)` only matters if the timer was already above 200.

## Champion action delay

**CONFIRMED.** `DisableCharacterAction` (`Magic.cpp` 485–519) posts `TT_11`.

```
when = d.Time + timeDelay
if a busy timer already exists:
    old = that timer's Time()
    if when >= old:
        when += (old - d.Time) / 2
    else:
        when = old + timeDelay / 2
    delete the old timer
else:
    set the attack-disable and weapon-attack flags
store the new timer index in CHARDESC.busyTimer
```

`TT_11` (`CSBCode.cpp` 6813–6822) calls `EnableCharacterAction`. If timer byte 6 is non-zero it calls `TAG0115ee` with `byte6 - 1`. The usual `DisableCharacterAction` path sets byte 6 to 0.

`TimeToMove` is the party step cooldown, not this per-champion attack timer. Report 05.

## Projectiles

**CONFIRMED.** `LaunchMissile` (`Magic.cpp` 16–71) creates a `DB14` missile and a timer at **`d.Time + 1`**. The first event is `TT_Missile0` unless `missileDelay` is set, in which case it is already `TT_Missile`.

`MissileTimer` (`Timer.cpp` 185–420):

- `TT_Missile0` is changed to `TT_Missile` and does not check the party on that first firing (lines 232–235).
- Later firings test the party and monsters in the current cell.
- Each cell step subtracts `decayRate` (timer word 8, bits 12–15, so 0–15) from range and from damage (lines 275–298). Range or damage exhausted removes the missile.
- A missile that survives is rescheduled at `timer.Time() + 1` (line 416).

One cell of travel is one tick. Thrown objects use this same timer. The comment at line 187 says a thrown Screamer Slice does.

Spell missiles go through `CharacterThrowsSpell` (`Magic.cpp` 453–478) into `LaunchObject` into `LaunchMissile`. Range and decay:

```
decayRate = 10 - min(8, MaxMana / 8)
if range < 4 * decayRate:
    range += 3
    decayRate -= 1
```

Fireball-type spells (class 2, lines 1244–1264) also set

```
range = clamp( 21, (2 * mastery + 4) * (spellPower + 2), 255 )
```

with mastery doubled when `missileType == 4`.

## Clouds

`ProcessTT_25` (`Timer.cpp` 426–612). A cloud that continues is copied and stored at `timer.Time() + 1` (line 602). Poison-cloud damage to the party uses `DamageAllCharacters` when the party is in the cell (lines 560–564). That is immediate damage on the cloud's tick, not the champion `TT_75` poison counter, unless some other path calls `PoisonCharacter`.

## Monster movement and attacks

**CONFIRMED** as tick delays. The per-type constants live in `MONSTERDESC` inside graphic `0x22F` and were not dumped in this pass.

`Increment_Time_By_W52_Plus_Random` (`Monster.cpp` 1413–1424):

```
timer.Time += max(1, timePerMove + STRandom0_3() - 1)
```

`timePerMove()` is `movementTicks06` (`CSB.h` 2345 and 2400). `STRandom0_3()` is 0, 1, 2, or 3. The added delay is `timePerMove - 1`, `timePerMove`, `timePerMove + 1`, or `timePerMove + 2`, and at least 1.

`NextMonsterUpdateTime` (`CSBCode.cpp` 6311–6330) adds a second piece when it is computing the next AI time:

- If the monster is attacking: `AdditionalDelayFromAttackToMove()` (monster word 20, bits 8–11).
- Otherwise: `AdditionalDelayFromMoveToAttack()` (bits 4–7).
- Then `+ STRandomBool()` (0 or 1).

`StartMonsterMovementTimers` (`CSBCode.cpp` 6334–6367) posts `TT_MONSTER_Bgroup` at `d.Time + 1` and stores `255 - timePerMove()` in timer byte 5. The comment on `movementTicks06` says that byte is "ticks per movement".

AI timer function numbers are 29 through 41 (`TT_FIRST_MONSTER_AI` … `TT_LAST_MONSTER_AI`, `CSB.h` 1806–1820). Group update versus individual monster update is the A-group and B-group split (`TT_MONSTER_Agroup` 32–36 and `TT_MONSTER_Bgroup` 37–41). `Set_Monster_Timer` (`Monster.cpp` 1355–1394) adds `timeUntilAlternateUpdate` either to the timer or to the paired timestamp, depending on `processingATimer`.

There is no single global monster attack period. An attack happens when that monster's AI timer says the group is attacking, plus the descriptor delays above.

`uByte4` on an `ITEM16` is set to `d.Time - 127` when a monster group is attached (comment "about 20 seconds", `CSBCode.cpp` line 6437).

## Doors, pits, teleporters, generators

| Type | Name | Delay used when rescheduled |
|---|---|---|
| 1 | `TT_1` | `Time + 1` in `ProcessTT_1` (`Timer.cpp` 1263, 1282, 1327) |
| 2 | `TT_BASH_DOOR` | Door bash. Scheduled with a dungeon delay (`Code11f52.cpp` 2314) |
| 5–10 | open room, stone, false wall, teleporter, pit, door | Map actuators. Processed in the `ProcessTimers` switch |
| 24 | `TT_24` | Fluxcage removal. One creation site uses `d.Time + 100` (`Code11f52.cpp` 1551) |
| 25 | `TT_25` | Cloud, `Time + 1` while it lives |
| 60, 61 | `TT_60`, `TT_61` | `ProcessTimer60and61` |
| 65 | `TT_ReactivateGenerator` | `d.Time + w_10` (`Timer.cpp` 1776). `w_10` comes from the actuator |
| 53 | `TT_53` | Watchdog, every 300 ticks (`Timer.cpp` 2810–2821) |

`ProcessTT_1` also advances its own time by 1 and reinserts, so a type-1 event can be a one-tick repeater.

## Temporary skills and attributes

Each `TenSecondUpdate`, every skill slot `tempAdjust` that is greater than 0 decreases by 1 (`Code17818.cpp` 250–256). Twenty slots, index 19 down to 0. This is once per 64 awake ticks, not once per tick.

Current attributes drift one point toward their maximum every 256 awake ticks, or every 64 sleep ticks. Report 05.

## Text and footprints

Text timeouts are absolute ticks. Examples in `_MainLoop`: `d.Time + 70` and `d.Time + 700` (`CSBCode.cpp` 1608–1612). The +70 comment says about 10 seconds, which matches 70/6.

Footprint entries age in `TenSecondUpdate` by decrementing `d.Byte13220` (`Code17818.cpp` 173–188). A zero entry can be deleted.

## Party step timer

Repeated here so this list is complete. `d.partyMoveDisableTimer` is set from `TimeToMove` and decremented once per tick. Formula, load, injury bit `ouches & 32`, and Boots of Speed are in report 05. It is not a `TIMER` queue entry.
