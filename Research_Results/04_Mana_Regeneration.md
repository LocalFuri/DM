# 04 — Mana regeneration

Periodic mana recovery is inside `TenSecondUpdate` (`Code17818.cpp` 209–248). It uses the same call as food and water: every **64 ticks** awake and every **16 ticks** asleep (`CSBCode.cpp` 682–685).

Movement, combat, food, and water do not have their own mana clocks. They also do not appear in the mana formula.

## Algorithm

**CONFIRMED.** `Code17818.cpp` 209–248.

```
if HP == 0: skip this champion
if this champion is the one being recruited: skip

if Mana < MaxMana:
    mastery = DetermineMastery(champion, SKILL_Wizard)
            + DetermineMastery(champion, SKILL_Priest)
    if random112 >= mastery + Wisdom.Current():
        # no gain
        # the inner "if Mana > MaxMana: Mana -= 1" cannot run here
    else:
        amount = MaxMana / 40          # integer division
        if PartySleeping:
            amount *= 2
        amount += 1
        staminaCost = amount * max(7, 16 - mastery)
        AdjustStamina(champion, staminaCost)
        Mana += min(amount, MaxMana - Mana)
else if Mana > MaxMana:
    Mana -= 1
```

`SKILL_Priest` is 2 and `SKILL_Wizard` is 3 (`CSB.h` 2486–2487).

`random112` (`Code17818.cpp` line 198) is not a call to `STRandom`. It is three bits of `d.Time`:

```
random112 = (d.Time & 0x40) | ((d.Time & 0x80) >> 2) | ((d.Time & 0x100) >> 4)
```

| Bits 8, 7, 6 of d.Time | random112 |
|---|---:|
| 000 | 0 |
| 001 | 64 |
| 010 | 32 |
| 011 | 96 |
| 100 | 16 |
| 101 | 80 |
| 110 | 48 |
| 111 | 112 |

The value is one of 0, 16, 32, 48, 64, 80, 96, 112. Bit 6 of `d.Time` changes every 64 ticks, which is the same period as an awake `TenSecondUpdate`. Success is deterministic for a given tick and a given threshold.

Regen runs when `random112 < Wizard mastery + Priest mastery + current Wisdom`. Wisdom is added only to that threshold. It does not multiply `amount`.

`amount` is `MaxMana / 40`, then +1, and doubled **before** the +1 when sleeping. Sleep therefore does `2 * (MaxMana / 40) + 1`, not `2 * (MaxMana / 40 + 1)`.

Mana already above `MaxMana` falls by 1 on each update until it is no longer above the maximum. The gain path will not push mana past `MaxMana`.

## Mastery

`DetermineMastery` (`Code17818.cpp` 13–147).

While `d.PartySleeping != 0` it returns **1** immediately (lines 29–36). Sleep still doubles `amount`, but the success test uses Priest mastery 1 plus Wizard mastery 1 plus Wisdom.

Awake:

```
experience = skill.experience
if the 0x8000 flag is clear: experience += skill.tempAdjust
if skill > 3:                         # subskill
    parent = skills[(skill - 4) / 4]
    experience += parent.experience
    if the 0x8000 flag is clear: experience += parent.tempAdjust
    experience >>= 1

mastery = 1
while experience >= 500:
    mastery += 1
    experience >>= 1
```

Wizard and Priest are skills 3 and 2, so the subskill branch does not run for them. Their hidden subskills are not added into this particular call. The call is `DetermineMastery(chIdx, SKILL_Wizard)` with no 0x4000 or 0x8000 flag, so possessions and temporary adjustments count.

Possession bonuses, only when the 0x4000 "ignore possessions" flag is clear (lines 73–141):

| Item | Where | Effect |
|---|---|---|
| `objNI_TheFirestaff_a` | hand slot 1 | mastery +1 |
| `objNI_TheFirestaff_b` | hand slot 1 | mastery +2 |
| `objNI_PendantFeral` | necklace slot 10 | Wizard mastery +1 |
| `objNI_EkkhardCross` | necklace slot 10 | skill 15 only |
| `objNI_GemOfAges` or `objNI_SceptreOfLyf` | neck / hand | skill 13 only |
| `objNI_Moonstone` | necklace | skill 14 only |

The Ekkhard Cross, Gem of Ages, Sceptre, and Moonstone do not change the Wizard or Priest skill numbers passed into the mana test. The Firestaff and the Pendant Feral do, because they modify the mastery value this function returns for the skill it was asked about. Pendant Feral is gated on `skill == SKILL_Wizard`.

A higher mastery makes `random112 < threshold` succeed on more of the eight time slots. It also lowers the stamina cost through `max(7, 16 - mastery)`. The mana amount does not use mastery.

## Stamina cost

`staminaCost = amount * max(7, 16 - mastery)` is passed to `AdjustStamina` as a decrement (`Code11f52.cpp` 4477–4507). If that drives stamina below 0, stamina becomes 0 and `abs / 2` hit points are lost. Mana is still added on that same update. The cost does not cancel the gain.

Nothing in this block requires a minimum stamina, a minimum food value, or a minimum water value. Injuries are not read. `HP == 0` is the only health gate.

## Numerical example

Awake champion, `MaxMana = 40`, current Wisdom 15, Wizard mastery 2, Priest mastery 1. No Firestaff and no Pendant Feral.

```
threshold = 2 + 1 + 15 = 18
amount    = 40 / 40 + 1 = 2
stamina   = 2 * max(7, 16 - 3) = 2 * 13 = 26
```

`random112` values below 18 are 0 and 16. That is 2 of the 8 bit patterns. Two successes per eight updates.

Awake updates are 64 ticks apart, so a success is 256 ticks apart if the patterns alternate in order. The actual order follows bits 6–8 of `d.Time`, not a shuffle. At the commented 6 Hz clock, 256 ticks is about 42.7 seconds. At this port's 255 ms Normal tick, 256 ticks is 65280 ms.

Each success: **+2 mana**, **−26 stamina**, while mana stays below 40.

Same champion asleep. `DetermineMastery` returns 1 for both skills, and `amount` is doubled before +1:

```
threshold = 1 + 1 + 15 = 17
amount    = 2 * (40 / 40) + 1 = 3
stamina   = 3 * max(7, 16 - 2) = 3 * 14 = 42
```

`random112 < 17` is still only 0 and 16. The update itself is every 16 ticks, and with `d.Speed = 0` those ticks are one blank each.

## What does not affect this formula

**CONFIRMED** by absence from `TenSecondUpdate`'s mana block:

- Food and water.
- Current stamina, except as a cost that can spill into hit points.
- Current hit points, other than the dead skip.
- Load and armour.
- Whether the party moved on this tick. Idle time changes `LOCAL_4` only in the food/stamina loop below this block.
- Experience, except through the mastery loop above.
- A random number generator. The only varying input is `d.Time`.

Potions that set mana directly (`potion_EEPotion` in `Character.cpp` 4247–4257) are instant adjustments, not this regenerator. Mana can be set above `MaxMana` by that potion. The periodic update then removes 1 per visit until mana is back within `MaxMana`.
