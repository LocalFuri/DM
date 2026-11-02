# 05 — Health and stamina

Natural recovery of both is in `TenSecondUpdate` (`Code17818.cpp` 151–439), every **64 ticks** awake and every **16 ticks** asleep (`CSBCode.cpp` 682–685). Poison, attacks, and spells are separate timers. See report 06.

`AdjustStamina` (`Code11f52.cpp` 4477–4507) is the shared writer:

```
newStamina = stamina - decrement
if newStamina <= 0:
    stamina = 0
    DamageCharacter(champion, abs(newStamina) / 2, 0, 0)
else if newStamina > MaxStamina:
    stamina = MaxStamina
else:
    stamina = newStamina
```

A negative `decrement` is a heal. Maximum stamina is the champion's `MaxStamina`. There is no second cap in this function.

## Stamina recovery

**CONFIRMED.** The food and water loop builds `pending`, then calls `AdjustStamina(champion, pending)`. The full loop is in report 03. The stamina-relevant part:

```
LOCAL_4 = clamp( (MaxStamina >> 8) - 1 , 1, 6 )
if sleeping: LOCAL_4 <<= 1

idle = (i16)(d.Time - d.LastPartyMoveTime)
if idle > 80:  LOCAL_4 += 1
if idle > 250: LOCAL_4 += 1
```

`d.LastPartyMoveTime` is the tick of the last party move (`Code11f52.cpp` 3275–3277). Standing still for more than 80 ticks adds 1 to the unit. More than 250 ticks adds another 1. The compare is on a 16-bit difference (`Code17818.cpp` line 270).

For each pass of the loop, if food ≥ 0 then `pending -= LOCAL_4`. If water ≥ 0 then `pending -= LOCAL_4` again. If food < −512 and this pass still has `D6 <= 4`, `pending += LOCAL_4`. Water below −512 does the same. So a fed, watered champion gains stamina. A deeply starved or dehydrated champion loses it, and that loss can become hit points through `AdjustStamina`.

The number of passes starts at 4 when stamina is at least half of maximum, and increases by 2 each time stamina is below the next halved maximum. See report 03. Low stamina therefore increases both the food burned and the number of `LOCAL_4` credits attempted. The loop stops early once `stamina >= MaxStamina + pending`, so the pending gain does not overshoot by a full extra pass's worth beyond the maximum. `AdjustStamina` clamps again.

Sleep doubles `LOCAL_4` before the idle additions. It does not change the 80-tick and 250-tick tests. Those tests use game ticks, which are much shorter in wall time while `d.Speed` is 0.

Mana recovery also calls `AdjustStamina` with a positive cost. That cost can erase the stamina gain from this same `TenSecondUpdate`, because mana is applied first (lines 209–238, then the food loop at 258).

## Health recovery

**CONFIRMED.** `Code17818.cpp` 344–367.

```
if HP < MaxHP:
    if stamina >= MaxStamina / 4:
        if random112 < Vitality.Current() + 12:
            heal = (MaxHP >> 7) + 1
            if sleeping:
                heal *= 2
            if necklace == objNI_EkkhardCross:
                heal = heal + heal / 2 + 1
            HP += min(heal, MaxHP - HP)
```

`random112` is the same time-bit value as mana. See report 04. It is not a random call.

Vitality is added to 12 and compared with `random112`. It does not scale `heal`. A current Vitality of 0 needs `random112 < 12`, which is only the pattern 0. A current Vitality of 105 or more satisfies `random112 < 117` for every pattern, since the largest `random112` is 112.

`heal` is at least 1. `MaxHP >> 7` is 0 until maximum health reaches 128, then 1 until 256, and so on. Sleep doubles that result. The Ekkhard Cross then does `heal + heal/2 + 1` in integers. Example: heal 2 becomes `2 + 1 + 1 = 4`. Heal 1 becomes `1 + 0 + 1 = 2`.

Gates that are actually in the code:

- `HP == 0` skips the whole champion, including this block (line 205).
- Stamina must be at least `MaxStamina / 4` on this update. The compare uses stamina **after** the food/water `AdjustStamina` and after the mana stamina cost, because those run first.
- `random112` must be strictly less than current Vitality + 12.

Food and water are not read in this block. They matter only because they changed stamina just above. Poison is not read here. An injury bit is not read here.

Hit points never rise above `MaxHP` in this function. The add is `min(heal, MaxHP - HP)`.

## Movement stamina

**CONFIRMED.** `MoveParty` (`CSBCode.cpp` 8455–8499) charges every existing champion before it knows whether the step is blocked by a wall or a monster:

```
staminaCost = 3 * load / MaxLoad + 1
AdjustStamina(champion, staminaCost)
```

`load` and the division are integers. Minimum cost is 1 when load is 0. Cost is 4 when `load == MaxLoad` (`3 * MaxLoad / MaxLoad + 1`). Load above maximum keeps growing the cost. `MaxLoad` is described at the call as a multiple of 10 (line 8461). The load field itself is in tenths of a kilogram (`CSB.h` line 2621).

A party-move filter that sets `PM_INHIBITMOVE` returns before this charge (lines 8485–8487). A wall bump that happens later still pays the cost.

`MoveParty` does not read food or water.

## Movement cooldown

**CONFIRMED.** `TimeToMove` (`CSBCode.cpp` 7374–7411) returns a tick count for one champion. `MoveParty` takes the maximum among living champions, and at least 1 (lines 8683–8703), then stores it in `d.partyMoveDisableTimer` unless a filter already set or added a delay.

```
if load < MaxLoad:
    time = 2
    if 8 * load > 5 * MaxLoad:
        time = 3
    injuryAdd = 1
else:
    time = 4 + 4 * (load - MaxLoad) / MaxLoad
    injuryAdd = 2

if ouches & 32:
    time += injuryAdd
if feet == objNI_BootsOfSpeed:
    time -= 1
```

`8 * load > 5 * MaxLoad` is load strictly above 5/8 of maximum. Equal to 5/8 stays at 2 ticks.

The leg-injury bit is `ouches & 32` (line 7402). The source does not name the body part in this function. The added amount is 1 when under maximum load and 2 when at or over it.

Boots of Speed are possession slot 5, named in the comment as feet (line 7406). They subtract 1 with no floor in this function. A result of 0 or negative would still be stored if `Larger(1, TimeToMove)` were not used. The caller initialises `time2move` to 1 and then does `Larger(time2move, TimeToMove)` (lines 8683–8691), so a Boots result of 0 becomes 1 because of that initial 1. A Boots result of 1 is kept. A negative `TimeToMove` would lose to the running maximum only if another champion is slower; `Larger` would keep the negative if every champion returned negative. That requires load under maximum, no injury, and Boots, which returns at least `2 - 1 = 1`. The overloaded branch starts at 4. **CONFIRMED:** the under-load Boots case returns at least 1.

`d.partyMoveDisableTimer` decrements once per tick in `_MainLoop` (`CSBCode.cpp` 686–689). Move clicks are ignored while it is non-zero (`Mouse.cpp` line 1515). Turning is not given a separate timer in `TimeToMove`. The disable is set for direction buttons that call `MoveParty`.

## Attribute drift

On a slower divider inside the same function (`Code17818.cpp` 378–408):

```
mask = 63 if sleeping else 255
if (d.Time & mask) == 0:
    for each of 7 attributes:
        if current < maximum: current += 1
        else if current > maximum:
            current -= current / maximum     # maximum 0 is treated as 1
```

Awake, this is every 256 ticks. Asleep, every 64 ticks. One point per attribute per hit. This is current strength, wisdom, vitality, and the other attributes, not maximums and not hit points.

## How the five bars meet

On one `TenSecondUpdate`, for a living champion, the order is:

1. Mana gain or the one-point mana decay, and the mana stamina cost.
2. Skill `tempAdjust` values above 0 decrease by 1 (lines 250–256).
3. Food, water, and the stamina `pending` update.
4. Health regen, which reads the stamina value left by steps 1 and 3.
5. The slow attribute drift, on its own bitmask.
6. If awake, and this champion is not facing the party direction, and the last monster attack was more than 60 ticks ago, the champion turns to the party facing (lines 410–423). The comment says 60 ticks is about 10 seconds.

Low stamina does not change the mana amount. It can fail the `stamina >= MaxStamina / 4` test for health. Very low stamina also increases food and water use on the next passes of step 3.
