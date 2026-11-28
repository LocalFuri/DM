One game tick is one increment of `d.Time`. CSBwin produces that tick after a counted number of vertical blanks. Timers, monsters, doors, missiles, and clouds do not run on their own clocks. They run only when `ProcessTimers` finds a record whose time is less than or equal to `d.Time`.

The 17 ms blank and the speed table are CSBwin. The source does not state an Atari or DOS vertical-blank rate. The error text “1/6 second” is a comment in `ProcessTimers`, not a measured tick length.

## 1. The clock

`millisecondsPerVBL` is 17 (`data.cpp` 210). `checkVBL` counts elapsed milliseconds divided by 17 and calls the vertical-blank handler once for each (`system.cpp` 549–570). The PC wait comment says blanks arrive in bursts because the platform timer cannot be shorter than about 50 ms (`system.cpp` 629–633).

A blank increments `d.vblCount`. When that count reaches `d.Speed`, `d.clockTick` becomes 1 (`CSBCode.cpp` 1118–1130). The main loop then accepts one simulation step. `d.Speed` is copied from the speed table whenever the party is awake (`CSBCode.cpp` 601–604):

| **Setting** | **Blanks per tick** |
| :---------- | ------------------: |
| Glacial     |                1000 |
| Molasses    |                  55 |
| Very slow   |                  33 |
| Slow        |                  22 |
| Normal      |                  15 |
| Fast        |                  11 |
| Quick       |                   7 |

At the modeled 17 ms blank, normal speed is 15 × 17 = 255 ms per tick, about 3.9 ticks per second. `VBLperTimer` starts at 10 (`data.cpp` 211). The overflow message says “1/6 second” (`CSBCode.cpp` 6756). Ten sixtieths of a second is one sixth, but the file never says the Atari blank was 60 Hz, and the playable normal speed is 15 blanks, not 10.

`d.Time` increases in two places:

- `_MainLoop`, after the viewport and the sound queue (`CSBCode.cpp` 654).
- The attack continuation, after its own timer pass and viewport (`Attack.cpp` 2178).

`NoSpeedLimit` forces a tick on every blank until `d.Time` reaches the limit (`CSBCode.cpp` 1120–1128). `playerClock` also forces a tick when a mouse event is waiting and more than 10 blanks have passed (`CSBCode.cpp` 1134–1137). `d.ClockRunning == 0` holds the loop before the next tick (`CSBCode.cpp` 767). Input is still read while it waits.

Sleep does not change `d.Speed`. While sleeping, the loop skips the speed assignment and the viewport (`CSBCode.cpp` 601 and 630). It still increments `d.Time`. The ten-second work uses mask 15 instead of 63, so it runs four times as often in ticks (`CSBCode.cpp` 683). Entering sleep sets `VBLperTimer` to 50 only when `NoSpeedLimit` is already active (`Mouse.cpp` 1722–1723).

The dungeon is drawn once per tick, before `d.Time` increases. The blank handler copies that bitmap to the screen only when `VBLMultiplier` is 1, or every third blank, or when the multiplier is 99 (`CSBCode.cpp` 1073). A multiplier other than 1 also discards sound (`Sound.cpp` 617). Skipping screen copies does not skip the tick. The tick still waits for `d.Speed` blanks, so a slower blank rate slows the simulation.

## 2. One pass of the main loop

`_MainLoop` (`CSBCode.cpp` 575) repeats this order:

1. If the party changed level, load that level and move the party object.
2. `ProcessTimers` for every timer whose time is due.
3. Draw the viewport, unless a character sheet is open or the party is sleeping.
4. Flash buttons, start a queued sound, and display pending damage.
5. If the game is not lost, increment `d.Time`, then the footprint clock, the 511-tick torch update, the freeze-life counter, and the 63-tick or 15-tick champion update.
6. Wait for a key or mouse message. Party movement is one of those messages, not a step taken on every tick.
7. If `clockTick` is still 0 or the clock is stopped, keep waiting.
8. Return to step 1.

Doors, actuators, monsters, missiles, clouds, poison, generator retries, and spell expiry run only in step 2, and only when their timer is due. Torch charge and champion recovery are the masked tests in step 5, not timer records. Sound is queued by those systems and started in step 4.

## 3. The timer record

The original file record is 10 bytes. `GetNextTimerEntry` still comments that the old copy was five words (`Timer.cpp` 1196). CSBwin first widened that to 12 bytes and then to the current 16-byte `TIMER` (`Timer.cpp` 2870–2907).

| **Offset** | **Field**                  | **Origin**                                                                                                            |
| ---------: | :------------------------- | :-------------------------------------------------------------------------------------------------------------------- |
|          0 | `m_time`, 32 bits          | Originally 24-bit time with the level in the top byte. Since version 12.4 the whole word is time (`CSB.h` 1853–1856). |
|          4 | Function ID                | Original.                                                                                                             |
|          5 | Byte 5                     | Original parameter. Sound priority, champion index, or monster extra time.                                            |
|          6 | Byte 6                     | Original. Usually X.                                                                                                  |
|          7 | Byte 7                     | Original. Usually Y.                                                                                                  |
|          8 | Byte 8                     | Original. Position, or the low byte of an object name.                                                                |
|          9 | Byte 9                     | Original. Action, or the high byte of an object name.                                                                 |
|         10 | `m_timerSequence`, 16 bits | CSBwin. Insertion order.                                                                                              |
|         12 | `m_timerLevel`             | CSBwin’s separated level byte.                                                                                        |
|         13 | Three unused bytes         | Padding.                                                                                                              |

Object names occupy bytes 6–7 for missiles and bytes 8–9 for clouds, generators, and the Vi altar. The queue holds handles, not the records themselves. It grows by a quarter when fewer than five slots remain, and it stops at 5000 records (`Timer.cpp` 954–962).

## 4. The scheduler

`SetTimer` (`Timer.cpp` 944) finds an empty slot, copies the record, assigns the next sequence number, and inserts the handle so the queue stays ordered by `TIMER::operator<` (`Timer.cpp` 715).

due while queue is not empty and queue[0].time <= d.Time:

&#x20;   if more than 50000 timers this tick: stop

&#x20;   copy queue[0]

&#x20;   delete it and repair the heap

&#x20;   load its level

&#x20;   dispatch its function

a timer created with time <= d.Time can run before this loop ends

Comparison (`Timer.cpp` 715–774):

1. Smaller time comes first.
2. At the same time, function 101 comes before every other function.
3. Otherwise the larger function ID comes first.
4. Then the larger byte 5 comes first.
5. Then the smaller sequence number comes first. This is the CSBwin addition in `stdafx.h`.
6. Then the lower record address comes first.

Deletion marks the slot empty and moves the last handle into the hole (`Timer.cpp` 913). The same slot can be reused. Map messages, functions 5 through 10, replace an existing message with the same time, level, position, and cell (`Timer.cpp` 964–979). Nothing else detects a cycle. A timer that reschedules itself at the current time runs again in the same loop until the 50000 limit.

## 5. Function IDs

| **ID** | **Handler**                     | **What it does**                                        | **Reschedule**                                                     |
| -----: | :------------------------------ | :------------------------------------------------------ | :----------------------------------------------------------------- |
|      1 | `ProcessTT_1`                   | One door step.                                          | `time + 1` until state 0 or 4.                                     |
|      2 | `ProcessTT_BASH_DOOR`           | Set door state 5.                                       | No.                                                                |
|      5 | `ProcessTT_OPENROOM`            | Text and monster generators.                            | Generator may queue 65.                                            |
|      6 | `ProcessTT_STONEROOM`           | Counters, bit gates, missile launchers.                 | Only if the target queues another timer.                           |
|      7 | `ProcessTT_FALSEWALL`           | Cell bit 2.                                             | `time + 1` while the party or a non-material monster stands there. |
|      8 | `ProcessTT_TELEPORTER`          | Cell bit 3, then move occupants.                        | No.                                                                |
|      9 | `ProcessTT_PITROOM`             | Cell bit 3, then move occupants.                        | No.                                                                |
|     10 | `ProcessTT_DOOR`                | Convert the action to function 1.                       | Same timer, function changed to 1.                                 |
|     11 | `EnableCharacterAction`         | End attack recovery.                                    | No.                                                                |
|     12 | `TAG011366`                     | Clear the movement portrait timer.                      | No.                                                                |
|     13 | `ProcessTT_ViAltar`             | Bones and revival.                                      | `time + 5`, then `time + 1`, as the state falls from 2 to 0.       |
|     20 | `QueueSound`                    | Delayed sound.                                          | No.                                                                |
|     22 | Disabled                        | The comment says processing is disabled (`CSB.h` 1937). | —                                                                  |
|     24 | inline                          | Remove a fluxcage.                                      | No.                                                                |
|     25 | `ProcessTT_25`                  | Cloud damage.                                           | `time + 1` while the cloud survives.                               |
|  29–41 | `ProcessTimers29to41`           | Monster AI.                                             | The monster queues its next move or attack.                        |
| 48, 49 | `MissileTimer`                  | One projectile step. 48 is the first step.              | `time + 1` until the range is spent.                               |
|     53 | `SetWatchdogTimer`              | Watchdog.                                               | It sets `WatchdogTime`.                                            |
| 60, 61 | `ProcessTimer60and61`           | A monster waiting for an empty cell.                    | `time + 5` while blocked.                                          |
|     65 | `ProcessTT_ReactivateGenerator` | Restore generator type 6.                               | No.                                                                |
|     70 | `ProcessLightLevelTimer`        | One brightness step.                                    | `time + 4` while steps remain.                                     |
|     71 | inline                          | Invisibility count − 1.                                 | No.                                                                |
|     72 | inline                          | Shield strength.                                        | No.                                                                |
|     73 | inline                          | See-through count − 1.                                  | No.                                                                |
|     74 | inline                          | Party shield.                                           | No.                                                                |
|     75 | `PoisonCharacter`               | One poison tick.                                        | Poison creates the next timer.                                     |
| 77, 78 | inline                          | Spell shield and fire shield.                           | No.                                                                |
|     79 | inline                          | Footprint-spell count − 1.                              | No.                                                                |
|    101 | `ProcessTT_OPENROOM` or stone   | CSBwin parameter message.                               | Same as 5 or 6.                                                    |
|    102 | `ProcessTT_OPENROOM`            | CSBwin DSA message.                                     | Same as 5.                                                         |

Functions 101 and 102 are CSBwin. The sequence number and the separated level byte are CSBwin. The 50000-event stop is CSBwin.

## 6. Work that is not a timer

These run after `d.Time++`, using the new value (`CSBCode.cpp` 675–685):

| **Test**                          | **Game ticks** | **At 255 ms per tick** | **Work**                                             |
| :-------------------------------- | -------------: | ---------------------: | :--------------------------------------------------- |
| `(time & 511) == 0`               |            512 |            about 130 s | Torch charge − 1. The comment says about 85 seconds. |
| `(time & 63) == 0`                |             64 |             about 16 s | Champion update. The comment says about 10 seconds.  |
| `(time & 15) == 0` while sleeping |             16 |              about 4 s | The same champion update.                            |

That update regenerates mana, stamina, and health, subtracts food and water, and subtracts 1 from each positive temporary skill (`Code17818.cpp` 151). Footprint ages also decrease there. Poison damage is timer 75, not this mask. Temporary attributes have no separate tick in this function.

## 7. Other levels

`ProcessTimers` loads the timer’s level before calling the handler (`CSBCode.cpp` 6766). A due door, missile, cloud, generator, or monster timer therefore runs on its own level even when the party is elsewhere. Handlers that compare `LoadedLevel` with `partyLevel` still skip party damage and party sight. Moving the party calls `LoadPartyLevel`, which drops the old level’s monster group records and reads the new graphics (`CSBCode.cpp` 788–800). The timer queue is not rebuilt.

## 8. Saving

The save block stores `d.Time`, `RandomGameID`, and `ranseed` as `d.RandomNumber` (`SaveGame.cpp` 1035 and 1781). Restoring that seed restores `STRandom`. Pending timers are written with the queue. On load they are ordered again by time, function, byte 5, and sequence, so equal-time order survives in this build. Door state is the cell flag, not a separate animation clock. Monster facing, health, and fear are in the monster record. A missile’s remaining range is in database 14, and its next move is its timer. Party shield, fire shield, invisibility, and light are global values plus their remaining timers.

## 9. A fixed step for Unity

accum += realMilliseconds

while accum >= 255 and awake at normal speed:

&#x20;   accum -= 255

&#x20;   dispatch every timer with time <= gameTime

&#x20;   gameTime += 1

&#x20;   if (gameTime & 511) == 0: torches

&#x20;   if (gameTime & 63) == 0: champions

draw using the last logical state

Use 255 only for the CSBwin normal setting. The original tick is “one step,” and the blank count that produces it is the speed setting.

Door states are 0, 1, 2, 3, and 4. Function 1 changes that state by one per tick. A 4-frame, 16-frame, 24-frame, 32-frame, or 48-frame animation is a picture drawn between two of those states. It must not enqueue another function-1 timer and must not change the cell flag. The next logical step waits for the next game tick, whatever the frame count is.

## Unresolved

The source never states the Atari or DOS blank rate. The 17 ms value and the 15-blank normal speed are CSBwin. Whether function 22 still exists in the original binary is only the comment that processing is disabled. The poison timer’s reschedule arithmetic was not re-read in this pass.