# 03 — Food and water

Food and water are signed 16-bit fields on the champion: `CHARDESC::food` and `CHARDESC::water` (`CSB.h` 2600–2601).

They change in `TenSecondUpdate` (`Code17818.cpp` 151–439). That function runs from `_MainLoop` when `(d.Time & mask) == 0`. The mask is **63** awake and **15** asleep (`CSBCode.cpp` 682–685). So the period is **64 ticks** awake and **16 ticks** asleep.

At the commented 6 Hz clock that is about 10.67 seconds awake and about 2.67 seconds asleep. At this port's Normal speed (255 ms per tick) it is 16320 ms awake. Sleep with `d.Speed = 0` makes the 16 ticks last 16 virtual blanks.

Standing, walking, and fighting do not select a different function. Load is not read here. Sleep does not skip the function. A dead champion (`HP() == 0`) is skipped. The champion indexed by `d.PotentialCharacterOrdinal` (still being recruited) is skipped (lines 205–208).

## Limits

| Bound | Value | Where | Confidence |
|---|---:|---|---|
| Value stored by eating or drinking | −1023 … 2048 | `Character.cpp` 4447–4448, `LIMIT` | CONFIRMED |
| Floor applied by `TenSecondUpdate` | −1024 | `Code17818.cpp` 336–342 | CONFIRMED |
| New champion | `(STRandom() & 0xff) + 1500` → 1500–1755 | `Character.cpp` 543–544 | CONFIRMED |

DSA code uses the same −1023 … 2048 clamp (`DSA.cpp` 4498–4499 and 4563–4564).

There is no separate "starving" flag. Food or water below 0 stops the stamina bonus from that bar. Food or water below −512 can also penalise stamina. If `AdjustStamina` is asked to drive stamina below 0, half the overshoot becomes hit points (`Code11f52.cpp` 4477–4494).

## The update algorithm

**CONFIRMED.** Integer arithmetic. `D6W` and `LOCAL_4` are 16-bit. Divisions truncate toward zero for these non-negative values.

```
# Code17818.cpp 258–335
D6 = 4
half = MaxStamina
while stamina < (half >>= 1):
    D6 += 2
# D6 stays 4 when stamina >= MaxStamina/2
# D6 becomes 6 when stamina >= MaxStamina/4 and < MaxStamina/2
# D6 becomes 8 when stamina >= MaxStamina/8, and so on

LOCAL_4 = clamp( (MaxStamina >> 8) - 1 , 1, 6 )    # ApplyLimits(1, x, 6)
if PartySleeping: LOCAL_4 <<= 1

idle = d.Time - d.LastPartyMoveTime          # 16-bit subtract, Code17818.cpp 270
if idle > 80:  LOCAL_4 += 1
if idle > 250: LOCAL_4 += 1

pending = 0
repeat:
    highStamina = (D6 <= 4)                   # true only while D6 is still 4
    if food < -512:
        if highStamina:
            pending += LOCAL_4                # stamina will fall
            food -= 2
    else:
        if food >= 0:
            pending -= LOCAL_4                # stamina will rise
        food -= (2 if highStamina else D6 / 2)

    if water < -512:
        if highStamina:
            pending += LOCAL_4
            water -= 1
    else:
        if water >= 0:
            pending -= LOCAL_4
        water -= (1 if highStamina else D6 / 2)

    D6 -= 1
while D6 != 0 and stamina < MaxStamina + pending

AdjustStamina(champion, pending)
if food < -1024: food = -1024
if water < -1024: water = -1024
```

`AdjustStamina(chIdx, decrement)` computes `stamina - decrement` (`Code11f52.cpp` 4486). A negative `pending` is a gain. The result is clamped to `0 … MaxStamina`. A result below 0 deals `abs(result) / 2` damage and sets stamina to 0.

`highStamina` is recomputed every pass from the current `D6`. Only passes that still have `D6 <= 4` use the flat 2 food / 1 water cost. Earlier passes, while `D6` is 6, 8, …, use `D6 / 2`.

Both bars can change `pending` in the same pass. Food ≥ 0 and water ≥ 0 subtracts `LOCAL_4` twice.

The loop stops when `D6` reaches 0 or when current stamina is already at least `MaxStamina + pending`. Because a food/water surplus makes `pending` more negative, `MaxStamina + pending` falls, and the loop stops once the pending gain would reach maximum. The gain is then applied once, after the loop.

### What one awake update does to a fed champion

Assume food ≥ 0, water ≥ 0, stamina ≥ half of maximum so the loop starts at `D6 = 4`, and `LOCAL_4 = L`.

Each of the four passes (`D6` = 4, 3, 2, 1) has `highStamina` true:

- food −2, water −1
- pending −L from food and −L from water

If stamina is far enough below maximum that all four passes run:

- food −8, water −4
- pending = −8L
- stamina gain = 8L, still clamped to maximum by the loop condition and by `AdjustStamina`

If the champion is close to full stamina, fewer passes run, and less food and water are consumed on that update.

If stamina is below half maximum, `D6` starts higher. Those extra passes consume `D6 / 2` food and `D6 / 2` water each, and they also add the `LOCAL_4` stamina credit when the bar is still ≥ 0. Tired champions burn food and water faster. That is the only stamina effect on consumption. Weight is not in this function. Weight spends stamina in `MoveParty`, and the lower stamina then raises `D6` here.

### Starvation and dehydration

Food in `-512 … -1` does not add or subtract `LOCAL_4`. The food counter still falls by 2 or by `D6 / 2`.

Food `< -512` skips the normal subtraction. Only a pass with `D6 <= 4` subtracts 2 more food and adds `LOCAL_4` to `pending` (a stamina loss). The same pattern applies to water, except the extra subtraction is 1.

Sleep doubles `LOCAL_4` before the idle bonuses, so both the recovery and the starvation penalty are larger. Idle time still adds 1 at more than 80 ticks and another 1 at more than 250 ticks (`Code17818.cpp` 270–278).

`LOCAL_4` itself is `clamp((MaxStamina >> 8) - 1, 1, 6)`. A maximum stamina below 512 yields the floor of 1. A maximum of 2048 or more yields the cap of 6 before sleep and idle bonuses. Sleep can push it past 6 because the `<<= 1` happens after the clamp.

## Eating and drinking

`FeedCharacter` records a `FEEDPARAMETERS` block, optionally lets a feeding-filter DSA edit it, then applies it (`Character.cpp` 4084–4448).

Water (`Character.cpp` 4123–4137):

- Object name `objNI_Waterskin` through `objNI_Water` (enum values 8 and 9, `Objects.h` 446–447).
- Flask value 0: `waterValue = 0`.
- Flask value non-zero: `waterValue = 800`, `gulp = 8`, and the flask is emptied one gulp.

Water-flask potion, `potion_WaterFlask` (lines 4287–4290): `waterValue = 1600`.

Food (lines 4298–4308): names `objNI_Apple` through `objNI_DragonSteak` add `FoodValue[name - objNI_Apple]`. The object is consumed (`emptyHand`, `chew`).

Applied clamps (lines 4447–4448):

```
water = LIMIT(-1023, water + waterValue, 2048)
food  = LIMIT(-1023, food  + foodValue,  2048)
```

## FoodValue from this graphics.dat

**CONFIRMED** by decompressing graphic `0x22F` from this file.

`ReadAndExpandGraphic(0x8000 | 0x22F, (ui8 *)d.Byte10340, …)` (`CSBCode.cpp` 10570). Decompressed size assert `0xC0E` = 3086 (line 10569). This file produced 3086 bytes. Each entry is then byte-swapped with `LE16` (lines 10594–10595).

The graphic image starts at Atari address 10340 (`Data.h` 1052). `FoodValue` is address 8962 (line 1060). Offset `10340 - 8962 = 1378`. Eight big-endian signed words:

| Index | `objNI` | Enum value | Name in source | Food points |
|---:|---|---:|---|---:|
| 0 | `objNI_Apple` | 168 | Apple | 500 |
| 1 | `objNI_Corn` | 169 | Corn | 600 |
| 2 | `objNI_Bread` | 170 | Bread | 650 |
| 3 | `objNI_Cheese` | 171 | Cheese | 820 |
| 4 | `objNI_ScreamerSlice` | 172 | Screamer Slice | 550 |
| 5 | `objNI_WormRound` | 173 | Worm Round | 350 |
| 6 | `objNI_Shank` | 174 | Shank | 990 |
| 7 | `objNI_DragonSteak` | 175 | Dragon Steak | 1400 |

Enum list: `Objects.h` 609–616. The array comment says "obj_Apple to obj_DragonSteak", which is these eight names. External notes sometimes call Shank a Drumstick. The source name is `objNI_Shank`. The value 990 matches that published Drumstick figure. Worm Round (350) and Dragon Steak (1400) are from this file; they were not in the earlier published snippet used before this extraction.

SHA-256 of the file: `196cb349827e8926e2763fa61ecbe8e10810003045ef72a435b7da4dded56d8e`.

## What this system does not do

- No per-step food or water cost in `MoveParty`.
- No separate combat consumption.
- No test of current hit points except the dead-champion skip.
- No test of load. Load changes stamina, and stamina changes how many times the loop body runs.
