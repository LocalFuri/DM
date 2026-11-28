Every original puzzle is a database-3 actuator that sends a delayed set, clear, or toggle message to one cell on the same level. The cell’s room type selects the timer, and that timer changes a cell flag, a counter, or another actuator. There is no per-puzzle function and no target level on an actuator.

This is the Atari translation. DSA type 47, portrait type 127, open-room counter type 48, `andOrEdge`, and `CursorFilter` are CSBwin additions. The eight timer-function bytes were not dumped from the graphic. The mapping below is the comment immediately above `QueueSwitchAction`. No DOS routine was opened.

## 1. The actuator record

A dungeon object list on each occupied cell points at a linked list. Actuators are database 3. Several can share a cell. Each is processed independently.

`DUNGEON.DAT` stores an 8-byte record. After loading, CSBwin inserts two zero bytes, so the runtime record is 10 bytes (`SaveGame.cpp` 2476–2503). `MakeBig` uses those two bytes only for DSA type 47 (`data.cpp` 1325–1332). All words are little-endian (`DB3::swap`, `data.cpp` 1317–1322).

The link is a `RecordName`: position in bits 14–15, database number in bits 10–13, index in bits 0–9. The dump prints it that way (`AsciiDump.cpp` 709).

| **Word** | **Bits** | **Field**                                                                          |
| :------- | :------- | :--------------------------------------------------------------------------------- |
| 2        | 0–6      | Actuator type.                                                                     |
| 2        | 7–15     | Value. Object type, monster type, counter, or bit pattern.                         |
| 4        | 2        | Once only. Cleared to type 0 when the message is queued.                           |
| 4        | 3–4      | Action: 0 set, 1 clear, 2 toggle, 3 constant pressure.                             |
| 4        | 5        | Normally closed.                                                                   |
| 4        | 6        | Audible. Sound 1.                                                                  |
| 4        | 7–10     | Delay in ticks. For a generator, monster count.                                    |
| 4        | 11       | Local action. No target coordinates.                                               |
| 4        | 12–15    | Wall graphic ordinal. Read for drawing. Nothing writes it when the switch is used. |
| 6        | 4–5      | Target position, used for stone and false walls.                                   |
| 6        | 6–10     | Target X.                                                                          |
| 6        | 11–15    | Target Y.                                                                          |
| 6        | 4–15     | Local-action parameter when bit 11 of word 4 is set.                               |

There is no target level and no target wall direction beyond the 2-bit position. A generator reuses word 6: bits 4–7 are the hit-point multiplier, bits 8–15 are the disable time, and bit 0 is `drawAsSize4` (`CSB.h` 936–1096).

Type 0 is disabled. `onceOnly` sets the type to 0 at the moment the message is queued, before the delay expires (`Code11f52.cpp` 3613–3614).

### Types the original functions implement

The same number means different things in an open cell and in stone. The comments in `CSB.h` 942–1001 and the dump in `AsciiDump.cpp` 1118–1257 agree with the switches in `PressurePadOrAlcove` and `TouchWall`.

| **Type** | **Open cell**                                                                            | **Stone or wall position**                                                    |
| -------: | :--------------------------------------------------------------------------------------- | :---------------------------------------------------------------------------- |
|        0 | Disabled.                                                                                | Disabled.                                                                     |
|        1 | Anything: party, monster, or object databases 5–13.                                      | Pushbutton. Any click.                                                        |
|        2 | Party or monster. Objects do not press it.                                               | Operated by an empty or full hand, chosen by normally closed.                 |
|        3 | Party. Value 0 means step on or off. Value 1–4 means facing north, east, south, or west. | Key or object. The object stays in the hand.                                  |
|        4 | One object type, `value`.                                                                | Same object type, removed from the hand.                                      |
|        5 | Ignored by the pad function.                                                             | Four-bit AND. Pressed when bits 7–10 equal bits 11–14 of `value`.             |
|        6 | Monster generator when a timer 5 arrives.                                                | Counter. Set increments, anything else decrements. Pressed at zero.           |
|        7 | Monsters only.                                                                           | Predefined missile launcher, one missile.                                     |
|        8 | Party only if someone carries object `value`. Normally closed inverts the test.          | Spell launcher, one spell. Value 0 is a fireball.                             |
|        9 | Party stepping on, and only when `value <= 20`.                                          | Two predefined missiles.                                                      |
|       10 | Not handled by the pad switch.                                                           | Two spells.                                                                   |
|       11 | Not handled.                                                                             | Key removed from the hand. Also calls local action 2.                         |
|       12 | Not handled.                                                                             | Empty hand receives a predefined object.                                      |
|       13 | Not handled.                                                                             | Holds the named object. The mouse places or takes it.                         |
|       14 | Not handled.                                                                             | Launches one object already at that wall position.                            |
|       15 | Not handled.                                                                             | Launches two such objects.                                                    |
|       16 | Not handled.                                                                             | Exchanges the held object with the object in the wall.                        |
|       17 | Not handled.                                                                             | Like type 11, and the actuator record is deleted after success.               |
|       18 | Not handled.                                                                             | The dump calls it an end-of-game sequence. `TouchWall` does not implement it. |

Types 47, 48, and 127 are CSBwin. Type 48 is marked “my invention” in `OperateCounterActuator` (`Timer.cpp` 1567–1568).

On a wall, `PressurePadOrAlcove` only accepts types 1, 2, and 3 (`Code11f52.cpp` 3874–3906). Type 1 is any object of database 5–13. Type 2 is one named object. Type 3 is any object that is not the named type. Those three are the alcove and receptacle tests. Keyholes, buttons, and holders are the mouse path in `TouchWall` (`Mouse.cpp` 289).

## 2. From a switch to a dungeon change

input closes the actuator

normallyClosed inverts that

action 0, 1, or 2 fires only on the closed edge

action 3 becomes set while closed and clear while open

if audible: sound 1

if onceOnly: type = 0

if local-only: PerformLocalAction

else: timer function = Byte1596[room type of target]

&#x20;     time = now + delay

&#x20;     position = target position for stone and false wall, else 0

`QueueSwitchAction` is `Code11f52.cpp` 3601–3651. `CreateTimer` writes function, X, Y, position, and action (`Mouse.cpp` 182–200). The timer runs later from the main timer loop (`CSBCode.cpp` 6760–6791).

The comment at `Code11f52.cpp` 3586–3598 documents graphic `0x232`, eight bytes at offset 234:

| **Room**     | **Value** | **Timer**                                 |
| :----------- | --------: | :---------------------------------------- |
| 0 stone      |         6 | `TT_STONEROOM`                            |
| 1 open       |         5 | `TT_OPENROOM`                             |
| 2 pit        |         9 | `TT_PITROOM`                              |
| 3 stairs     |         0 | Illegal. `WarnIllegalTimer` and no timer. |
| 4 door       |        10 | `TT_DOOR`                                 |
| 5 teleporter |         8 | `TT_TELEPORTER`                           |
| 6 false wall |         7 | `TT_FALSEWALL`                            |
| 7 unused     |         0 | Illegal.                                  |

Those eight bytes were not read from the file. The code uses `d.Byte1596[roomType]` (`Code11f52.cpp` 3634).

What the timer does:

- Stone, function 6: text bit 0 is shown, hidden, or toggled. Actuator type 5 updates one bit, selected by the timer position. Actuator type 6 updates the counter. Types 7, 8, 9, 10, 14, and 15 launch missiles (`ProcessTT_STONEROOM`, `Timer.cpp` 2135; `TriggerMissileLauncher`, `Timer.cpp` 1848).
- Open, function 5: text is shown, and a type 6 actuator generates a monster (`ProcessTT_OPENROOM`, `Timer.cpp` 1656).
- Pit: set sets cell bit 3. If it was closed, everything on the cell is moved zero squares so it can fall (`ProcessTT_PITROOM`, `Timer.cpp` 2505).
- Teleporter: set or clear cell bit 3, then the same zero-square move (`ProcessTT_TELEPORTER`, `Timer.cpp` 2373).
- False wall: set or clear cell bit 2. Clearing waits while the party or a non-material monster is standing there (`ProcessTT_FALSEWALL`, `Timer.cpp` 1362).
- Door: function 10 checks the current state and, if a step is needed, becomes function 1 (`ProcessTT_DOOR`, `Timer.cpp` 1523).

A target can contain several actuators. The stone and open handlers walk the whole list, so one message can move several counters. An actuator’s own target is still one cell. A chain is just that cell’s actuator sending another message. Nothing detects a circular actuator chain. A circular teleporter stops after 1000 steps (`Code11f52.cpp` 2937–3186).

Constant pressure is the only action that sends both edges. Set, clear, and toggle send nothing on the opening edge (`Code11f52.cpp` 3908–3927).

## 3. Plates

`PlaceOrRemoveObjectInRoom` (`Code11f52.cpp` 3945) counts what is already on the cell, then calls `PressurePadOrAlcove` for every database-3 record. The party is object `RNnul` and database −1. Monsters are database 4. Items are databases 5 through 13.

The plate does not fire again while it is already held down:

- Type 1 returns when any qualifying object or monster is already present, and when the mover did not change cells.
- Type 2 returns when a monster is already present. An object move never presses it.
- Type 4 returns when another object of that exact type is already present.
- Type 7 returns when a monster is already present. The party never presses it.
- Type 3 with value 0 returns when the party did not change cells. With a facing value, stepping off releases it, and stepping on presses it only when `facing + 1` equals the value.

Leaving is the same function with `place` false. Constant pressure then sends clear. The other actions send nothing, because the switch is no longer closed.

The artwork does not change. `wallGraphicOrdinal` is only read (`Codea59a.cpp` 135 and 331). Sound is sound 1, and only when the audible bit is set.

## 4. Wall devices

`TouchWall` (`Mouse.cpp` 289) walks actuators whose position matches the clicked wall. Types 11, 12, 16, and 17 also require that no later actuator remains at that position.

| **Type** | **Condition**                                                                          | **Object**                                                                                    |
| :------- | :------------------------------------------------------------------------------------- | :-------------------------------------------------------------------------------------------- |
| 1        | Any click, unless the action is constant pressure.                                     | Kept.                                                                                         |
| 2        | Empty or full hand, selected by normally closed.                                       | Kept.                                                                                         |
| 3, 4     | Held object type equals `value`. Normally closed inverts the comparison.               | Type 4 deletes it. Type 3 keeps it.                                                           |
| 11, 17   | Same object test, and it must be the last actuator there.                              | Deleted. Type 11 also posts local action 2. Type 17 deletes the actuator record.              |
| 12       | Empty hand, and it is the last actuator.                                               | Creates the predefined object in the hand.                                                    |
| 13       | Empty hand takes the named object. A matching object is placed if the holder is empty. | Local action 2. The queued action is suppressed for constant pressure while the hand is full. |
| 16       | The held type matches, and some drawable object is present.                            | The held object and the wall object are swapped.                                              |

Successful types other than the suppressed type-13 case call `QueueSwitchAction`. Sound is again sound 1. No type changes the wall graphic ordinal.

Types 1, 2, and 3 also respond when an object is placed in that wall position by `PressurePadOrAlcove`. A keyhole and an alcove therefore share the message function, but the key test is in `TouchWall` and the alcove test is in the object-placement function.

`CursorFilter` calls around these cases are CSBwin.

## 5. Doors

A door cell uses bits 0–2 for the state (`CSBTypes.h` 177–186):

| **State** | **Meaning**                                       |
| --------: | :------------------------------------------------ |
|         0 | Open.                                             |
|         1 | Almost open.                                      |
|         2 | Half open.                                        |
|         3 | Almost closed.                                    |
|         4 | Closed.                                           |
|         5 | Bashed. Further door messages return immediately. |

`ProcessTT_DOOR` turns a toggle into set or clear from the current state. Set while already open, or clear while already closed, does nothing. Otherwise it reschedules the same timer as function 1 (`Timer.cpp` 1523–1561).

Function 1 moves the state by one per tick. Clear subtracts one, set adds one, and the timer is kept until the state reaches 0 or 4. Each step plays sound 2. The interval is one game tick. The door type and the up-or-sideways bit are not read for the timing (`Timer.cpp` 1338–1355).

A closing door with the party on it snaps the state to 0, damages every champion with `DamageAllCharacters(5, 4, 2)`, and plays sound 18 if that damage is nonzero (`Timer.cpp` 1269–1283). A material monster is hit for 5 when the state is at least its vertical size for an upward door, or at least 1 for a sideways door. The state then steps back toward open (`Timer.cpp` 1286–1329).

There is no lock field. A locked door is a door whose actuator has not yet sent set. Two actuators aimed at the same door create two timers. The second set does nothing once the door is already open. Nothing merges them.

Bashing sets state 5 through function 2 (`ProcessTT_BASH_DOOR`, `Timer.cpp` 1464). A fireball door strike uses the same bash path when the door record allows it. That was established from `HitDoor` in the projectile work. The door record itself is database 0, four bytes: link plus word 2. Bit 0 is the door type, bits 1–4 are ornateness, bit 5 is upward, bit 6 is the door button, bit 7 allows a fireball, and bit 8 allows an axe (`CSB.h` 826–853).

## 6. Teleporters

Database 1 is six bytes: link, word 2, word 4 (`CSB.h` 856–887).

| **Word** | **Bits** | **Meaning**                                             |
| :------- | :------- | :------------------------------------------------------ |
| 2        | 0–4      | Destination X.                                          |
| 2        | 5–9      | Destination Y.                                          |
| 2        | 10–11    | Rotation, 0–3.                                          |
| 2        | 12       | 1 means absolute facing. 0 means add the rotation.      |
| 2        | 13–14    | 0 objects, 1 monsters, 2 party and objects, 3 anything. |
| 2        | 15       | Audible. Sound 17.                                      |
| 4        | 8–15     | Destination level.                                      |

Cell bit 2 is visible. Cell bit 3 is active (`CSBTypes.h` 195–197). A message sets or clears bit 3 and then calls `WiggleEverything`, which moves the party, monsters, items, missiles, and clouds zero squares (`Timer.cpp` 2373–2498).

`MoveObject` (`Code11f52.cpp` 2937–3053) reads the first database-1 record. Bit 3 must be set. The `what` field filters the mover. A teleporter whose destination is its own cell is taken once and then the loop stops. Otherwise the function repeats, up to 1000 times.

The party gets an absolute facing or `facing + rotation`, masked to two bits. A monster calls `TeleportMonster`. A missile calls `TeleportMissile`. Another object, when the mode is relative, adds the rotation to its corner. The audible bit queues sound 17.

## 7. Pits, stairs, and false walls

A pit message sets or clears cell bit 3. Opening it wiggles the cell, so occupants immediately take the pit path in `MoveObject` (`Timer.cpp` 2505–2546).

The fall itself requires an active pit, bit 0 clear, a non-levitating mover, and `PitGoesSomewhere`. The level becomes `IncrementLevel(level, 1, x, y)`. The party takes `DamageAllCharacters(20, 48, 2)` and sound 6 unless pit damage is suppressed, in which case each living champion loses stamina from load. A monster takes 20 damage (`Code11f52.cpp` 3056–3142). A levitating missile or monster does not enter this branch.

Stairs are room type 3. They are not a message target. The timer-function byte for stairs is 0, which is rejected. In `MoveObject`, a non-missile object on stairs with bit 2 clear goes down one level, steps one tile in `ExitStairsDirection`, and turns to `direction + 2`. Its corner is then recomputed from the old corner and that direction (`Code11f52.cpp` 3145–3171). Monsters refuse stairs in `PossibleMove` (`Monster.cpp` 688).

A false-wall message sets or clears bit 2. Clearing is postponed one tick while the party or a non-material monster occupies the cell (`Timer.cpp` 1426–1456). `PossibleMove` blocks a closed false wall unless bit 0 is set and the caller passed a nonzero “recognizes false pits and walls” flag (`Monster.cpp` 703–711). That flag is a monster-movement parameter, not a cell field. The party’s own false-wall collision was not traced in this pass.

Cell bit 4 means the cell has an object list. It is not an actuator state (`Code11f52.cpp` 440–459).

## 8. How a puzzle is assembled

There is no AND object and no OR object. The original combinations are:

- Type 5, the four-bit comparator. A message position selects one bit. Set turns it on, clear turns it off, and toggle flips it. The actuator is pressed when the low four bits equal the next four bits. Constant pressure sends set or clear as that comparison changes. The other actions send only when it becomes pressed (`Timer.cpp` 2221–2334).
- Type 6, the counter. Set increments, up to 511. Clear and toggle decrement. A value of zero cannot be changed. It fires when the count reaches zero (`Timer.cpp` 1575–1648).
- Several actuators with the same target. That is the OR for a simple door: either message opens it.
- A plate or keyhole whose target is a type 5 or type 6, which then targets the door. That is the AND or the sequence.
- Delay bits 7–10 of word 4. The message is queued immediately and executed later.
- Once-only, which disables the source when the message is queued.
- Constant pressure, which holds a pit or door only while the plate remains closed.

`andOrEdge` changes when a type 5 forwards its message. It runs only when `ExtendedFeaturesVersion` is not `'@'` (`Timer.cpp` 2275–2315`), so it is a CSBwin extension. DSA type 47 is also CSBwin, including the `ActivateDSA\` calls inserted into the door, pit, and teleporter timers.

## 9. Generators

A generator is a type 6 actuator on an open cell. Timer function 5 reaches it (`Timer.cpp` 1731–1784).

count = delay bits

if bit 3 is set: count = random(count & 7)

else: count = count - 1

multiplier = hitPointMultiplier

if multiplier is 0: multiplier = the level experience multiplier

CreateMonster(monsterType, multiplier, count, random facing 0..3, x, y, normallyClosed, drawAsSize4)

if audible: sound 17

if onceOnly: type = 0

else if disableTime != 0:

&#x20;   type = 0

&#x20;   if disableTime >= 128: delay = 64 \* (disableTime - 126)

&#x20;   else: delay = disableTime

&#x20;   timer 65 at now + delay

Timer 65 sets the type back to 6. The original search turned the first type 0 actuator on that cell into a generator. CSBwin first looks for the generator’s own record, and uses the first type 0 record only when that record is gone (`Timer.cpp` 1795–1839\`).

If the destination is occupied, creation goes through timers 60 and 61. Those wait until the cell is free of the party and of monsters, then call `MoveObject`. Each failed attempt adds 5 ticks (`Timer.cpp` 2553–2614`). The monster’s group size is the count passed to `CreateMonster`. The hit-point formula inside `CreateMonster\` was not re-read in this pass.

## 10. Where the bytes live

These are `DUNGEON.DAT` records, not graphics:

| **Database** | **File size** | **Runtime size** | **Contents**                                   |
| :----------- | ------------: | ---------------: | :--------------------------------------------- |
| 0 door       |             4 |                4 | Link, door flags.                              |
| 1 teleporter |             6 |                6 | Link, destination and filter, level.           |
| 2 text       |             4 |                4 | Link, show bit, text index.                    |
| 3 actuator   |             8 |               10 | Link, three flag words, plus two CSBwin bytes. |
| 4 monster    |            16 |               16 | Not part of the actuator record.               |

The counts come from the dungeon index, not from a fixed table. `Data.h` 956–962 records the file sizes. Words are little-endian. The value fields are unsigned bit fields except where a signed getter is used for delay and coordinates. Those getters return `i16`, but the stored fields are bit slices.

The only graphics table in this path is `Byte1596`, graphic `0x232`, offset 234, 8 bytes. Its documented values are 6, 5, 9, 0, 10, 8, 7, 0. They were not dumped here.

## 11. Eight generic traces

No dungeon coordinates are invented. Each trace is the record pattern the functions consume.

1. A plate opens a door. The plate is type 1, action set, audible optional, delay optional, target equal to the door cell. Stepping on it queues function 10 with action 0. Function 10 becomes function 1. Each tick adds one to the door state until it is 0. The door’s graphic set is in the door record and the level graphics. The actuator does not name a portcullis.
2. A button toggles a door. The button is type 1 in stone, action 2. `TouchWall` queues function 10 with action 2. If the door state is 0, the toggle becomes clear and the door closes. If it is 4, the toggle becomes set and the door opens.
3. A key enters a keyhole. The actuator is type 4 or 11, and `value` is the object-name index. The held object’s basic type must match. The object is deleted. The action and target then follow the same queue as a button. Type 3 performs the same test but leaves the object in the hand.
4. Two switches, one door. Two actuators carry the same target and action set. Each queues its own function-10 timer. The first moves the door. The second finds the door already at the requested end state and returns.
5. A plate opens a pit. The target cell is room type 2. The queued function is 9. Set turns on bit 3. If the bit was off, the party, monsters, and objects are moved in place and fall if the other pit tests pass.
6. A teleporter turns the party. The cell needs bit 3 set, either from the dungeon or from a function-8 set message. The database-1 record supplies the destination. Facing mode 1 replaces the facing with the rotation. Mode 0 adds it. Sound 17 plays when the audible bit is set.
7. A delayed generator. An ordinary actuator targets the generator’s open cell, with its delay set. When function 5 arrives, type 6 calls `CreateMonster`. A nonzero disable time changes the type to 0 and queues function 65 to restore it.
8. An alcove opens a wall. The receptacle is type 2 at a stone position, and its `value` is the object type. Placing that object in that position closes it. Its target is the false-wall cell. Function 7 sets bit 2, which is the open false wall.

## Unresolved

The eight `Byte1596` bytes were not read from graphic `0x232`. The hit-point arithmetic inside `CreateMonster` was not re-read. Party movement through a false wall was not traced beyond the cell-flag comments. Type 18 has a dump label and no action in `TouchWall`. No DOS binary was compared.


