# 01 — Game time and timers

**CONFIRMED** in this CSBwin tree unless a paragraph says otherwise.

The simulation clock is `d.Time`, a 32-bit integer. One unit is one game tick. Wall-clock seconds are only the wait between ticks.

## Main loop

`_MainLoop` (`CSBCode.cpp` 575–781), label `tag000698`, runs once per tick.

While `d.GameIsLost == 0`:

1. If the party is not sleeping, copy `speedTable[gameSpeed].vblPerTick` into `d.Speed` (lines 601–605).
2. Set `d.vblCount = 0` (line 606).
3. `ProcessTimers()` (line 617). Timers with `Time() <= d.Time` run **before** this pass increments time.
4. Draw the viewport unless a champion is selected or the party is sleeping (lines 628–632). An open character sheet does not stop the clock.
5. `d.Time++` (line 654).
6. If `(d.Time & 511) == 0`, call `NinetySecondUpdate()` (lines 675–678). The comment says "about every 85 seconds".
7. If `d.freezeLifeTimer != 0`, decrement it (lines 679–680).
8. Mask = `15` when `d.PartySleeping` is set, otherwise `63`. If `(d.Time & mask) == 0`, call `TenSecondUpdate()` (lines 682–685).
9. If `d.partyMoveDisableTimer != 0`, decrement it (lines 686–689). `d.Word11712` is decremented the same way (lines 691–693).
10. If `d.WatchdogTime < d.Time`, abort with "Watchdog Timer failure" (lines 696–700).
11. Set `d.clockTick = 0` (line 702).
12. Wait until `d.clockTick != 0` and `d.ClockRunning != 0` (lines 763–768), then go back to step 1.

A timer scheduled for time T is processed on the loop entry where `d.Time` is already T. The increment for that visit happens afterwards.

`ProcessTimers` (`CSBCode.cpp` 6732–6936) dies if more than 50000 timers fire in one visit. The message calls that visit "1/6 second" (lines 6754–6758).

## Timer queue

`GameTimers::CheckForTimerEvent` (`Timer.cpp` 1205–1239):

- Empty queue: no event.
- Otherwise compare the earliest timer's `Time()` with `d.Time`.
- Fire when `timer.Time() <= d.Time`.

`GetNextTimerEntry` (`Timer.cpp` 1188–1198) copies that timer and deletes it. Ordering is `TIMER::operator<` (`Timer.cpp` 715).

`TIMER::Time` is a 32-bit tick (`CSB.h` 1852–1859). Older saves packed the level in the top 8 bits. Current code stores the level separately with `TIMER::Level`.

`gameTimers.SetTimer` inserts a `TIMER`. Map, monster, missile, poison, light, shield, and sound events all use this one queue. They do not have separate clocks. The two counters decremented in `_MainLoop` (`freezeLifeTimer`, `partyMoveDisableTimer`) are the exceptions.

## Real time

`vblInterrupt` (`CSBCode.cpp` 948–1147) adds 1 to `d.vblCount`. It sets `d.clockTick = 1` when `d.vblCount >= d.Speed`, or immediately when `NoSpeedLimit != 0` (lines 1118–1130).

`checkVBL` (`system.cpp` 533–627) turns wall-clock milliseconds into those blanks:

```
increment = (now - prevTime) / millisecondsPerVBL
```

`millisecondsPerVBL` is **17** (`data.cpp` line 210). Each forced blank posts `UIM_VBL` and calls `vblInterrupt`.

`d.Speed` comes from `speedTable` (`CSBwin.cpp` 184–190). Default `gameSpeed` is `SPEED_NORMAL` (`data.cpp` line 270).

| Setting | Enum | VBLs per tick | Wall time at 17 ms |
|---|---|---:|---:|
| Glacial | `SPEED_GLACIAL` | 1000 | 17000 ms |
| Molasses | `SPEED_MOLASSES` | 55 | 935 ms |
| Very slow | `SPEED_VERYSLOW` | 33 | 561 ms |
| Slow | `SPEED_SLOW` | 22 | 374 ms |
| Normal | `SPEED_NORMAL` | 15 | 255 ms |
| Fast | `SPEED_FAST` | 11 | 187 ms |
| Quick | `SPEED_QUICK` | 7 | 119 ms |

Integer division: a tick lasts `vblPerTick * 17` milliseconds at this port's blank rate. Normal is `15 * 17 = 255` ms, about 3.92 ticks per second.

### Atari comments versus this port

The code that assigns `d.Speed` still says "Delay vblOccurred for 10 VBLs" (`CSBCode.cpp` line 604). `VBLperTimer` defaults to **10** (`data.cpp` line 211). These comments all divide cleanly at **6 ticks per second** (10 blanks at 60 Hz):

| Ticks | Comment | At 6 Hz |
|---:|---|---|
| 64 | "about every 10 seconds" (`CSBCode.cpp` 769) | 10.67 s |
| 512 | "about every 85 seconds" (`CSBCode.cpp` 675) | 85.33 s |
| 60 | "About 10 seconds" (`Code17818.cpp` 416) | 10.00 s |
| 70 | "about 10 seconds" (`CSBCode.cpp` 1701) | 11.67 s |
| 127 | "about 20 seconds" (`CSBCode.cpp` 6437) | 21.17 s |
| 300 | "about 50 seconds" (`Timer.cpp` 2817–2818) | 50.00 s |
| 16 while sleeping | "2.5 seconds while sleeping" (`Code17818.cpp` 150) | 2.67 s |

The "1/6 second" string in `ProcessTimers` is the same clock.

**CONFIRMED for this port:** Normal play waits 255 ms per tick.

**CONFIRMED as the clock the comments describe:** 1 tick = 1/6 second.

`VBLperTimer` is not the simulation tick. The UI sets `CallCheckVBL = VBLperTimer * VBLMultiplier` (`CSBUI.cpp` line 1178), which only changes how often `checkVBL` is polled. Sleep with `NoSpeedLimit > 0` sets `VBLperTimer = 50` (`Mouse.cpp` 1722–1723). `WakeUp` sets it back to 10 (`CSBCode.cpp` line 7423).

Whether a 50 Hz Atari kept the same 10-blank delay is **UNVERIFIED**. This source does not say.

## Sleep

Clicking sleep (`Mouse.cpp` 1712–1733):

- `d.PartySleeping = 1`
- `d.Speed = 0`
- text speed is set to 0
- video mode becomes `VM_SLEEPING`

`vblCount` is incremented before the compare. `1 >= 0` is true, so **every virtual blank is a tick** while `d.Speed` is 0. In this port that is one tick per 17 ms.

`TenSecondUpdate` then uses mask 15, so it runs every 16 ticks. At 1/6 second that is about 2.67 seconds, which is the function comment. At `Speed = 0` those 16 ticks are 16 blanks (272 ms at 17 ms).

Sleep also doubles several recovery amounts inside `TenSecondUpdate`. That is separate from the faster tick. See reports 03, 04, and 05.

`WakeUp` (`CSBCode.cpp` 7418–7434) clears `d.PartySleeping`, sets `d.clockTick = 1`, and restores `d.Speed` from the speed table.

`DetermineMastery` returns 1 for every skill while the party is sleeping (`Code17818.cpp` 29–36).

## Pause

"GAME FROZEN" sets `d.ClockRunning = 0` and `gameFrozen = true` (`Mouse.cpp` 1760–1787). The wait at `tag0007b4` requires `d.ClockRunning != 0` (`CSBCode.cpp` 767–768), so `d.Time` stays where it is. Timers do not run. Unfreeze sets `d.ClockRunning = 1` (`Mouse.cpp` 1789–1801).

`d.GameIsLost != 0` leaves `_MainLoop` without another increment (`CSBCode.cpp` 652 and 779–780).

## Movement and the clock

Movement does not have its own clock. A successful step sets `d.partyMoveDisableTimer` to the slowest living champion's `TimeToMove` (`CSBCode.cpp` 8683–8703). That counter decrements once per tick (lines 686–689). Clicks on the move buttons are ignored while it is non-zero (`Mouse.cpp` line 1515).

`extraTicks` defaults to **true** (`data.cpp` line 274). `MoveParty` sets `d.clockTick = 1` when `extraTicks` is set (`CSBCode.cpp` 8451–8454). That ends the current wait early. A click rejected by `partyMoveDisableTimer` never enters `MoveParty`, so the cooldown ticks still wait the full blank count.

`playerClock` defaults to **false** (`data.cpp` line 273). If enabled, a pending mouse event can force a tick after 10 blanks even when `d.Speed` is larger (`CSBCode.cpp` 1134–1137).

`NoSpeedLimit != 0` forces a tick on every blank until `d.Time` reaches that limit (`CSBCode.cpp` 1120–1128).

## Periodic champion work

| Function | Awake | Sleeping | What it does |
|---|---|---|---|
| `TenSecondUpdate` | every 64 ticks | every 16 ticks | food, water, stamina, mana, health, skill temps, attribute drift |
| `NinetySecondUpdate` | every 512 ticks | every 512 ticks | hand-torch charges |

Attribute drift inside `TenSecondUpdate` has a second gate: `(d.Time & 255) == 0` awake, `(d.Time & 63) == 0` asleep (`Code17818.cpp` 378–387). That is every 256 awake ticks, or every 64 sleep ticks.

`d.LastPartyMoveTime` is updated when the party actually changes cells (`Code11f52.cpp` 3275–3277, and `CSBCode.cpp` 7145). `TenSecondUpdate` uses `d.Time - d.LastPartyMoveTime` only to add 1 or 2 to the stamina-recovery unit after 80 and 250 idle ticks. It does not change the tick rate.

## Watchdog

`SetWatchdogTimer` (`Timer.cpp` 2810–2821) posts `TT_53` at `d.Time + 300` and stores that same value in `d.WatchdogTime`. Processing `TT_53` calls `SetWatchdogTimer` again (`CSBCode.cpp` line 6861). If the main loop finds `d.Time` already past `d.WatchdogTime`, the game aborts.
