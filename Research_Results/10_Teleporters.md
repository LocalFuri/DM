# Dungeon Master – Teleporters (Levels 00–13)

Source: Cursor research supplied November 4, 2026. Original research data and behavior descriptions preserved; table formatted for readability.

The complete set is 175 teleporters. The table below is the verified dataset.

Coordinates are local level tiles, the same axes as DungeonMaster_LevelXX.json and as CSBwin’s partyX/partyY. They do not include OffsetMapX/OffsetMapY. Every placed teleporter matched the JSON record for position, scope, rotation, audible flag, and the active and visible cell-flag bits. JSON TargetMapIndex is 0 on all 175 records. That field is the unused low byte of the teleporter word. The destination level is the high byte, DB1::destLevel().

## Confirmed behavior
DB1 (CSB.h) is the teleporter record. Destination X and Y are bits 0–4 and 5–9. Rotation is bits 10–11. Bit 12 chooses the facing mode: 1 means set facing to that direction (0 north, 1 east, 2 south, 3 west); 0 means add it to the current facing. Bits 13–14 select who may pass. Bit 15 is the teleport buzz. MoveObject in Code11f52.cpp performs the move. TeleportMonster and TeleportMissile apply the same facing rule to monsters and missiles.

### Who may pass

| Type | Party | Monsters | Objects and missiles |
|---|---|---|---|
| Objects | no | no | yes |
| Monsters | no | yes | no |
| Party+Objects | yes | no | yes |
| All | yes | yes | yes |
Doors, text, actuators, and the teleporter record itself are not moved. A destination that is the same cell still applies facing and sound, then stops. If the destination is another active teleporter that accepts the traveler, the move continues, with a limit of 1000 hops.

The cell flag (CSBTypes.h) stores the room state. Room type 5 is a teleporter. Bit 0x08 means initially active. An inactive teleporter does nothing until that bit is set. Bit 0x04 means the visible flag is set. The floor graphic is drawn only when both bits are set (Codea59a.cpp: (cell & 0x0C) == 0x0C). An active but invisible teleporter still works and shows no haze.

An actuator changes the active bit only when it is not local-only and its target cell is a teleporter. QueueSwitchAction then creates timer function 8, and ProcessTT_TELEPORTER in Timer.cpp sets, clears, or toggles bit 0x08. There are no type-47 DSA actuators in this dungeon, so nothing else in this file rewrites teleporters.

### Actuator behavior

Floor pad type 1 is anything. Type 2 is party or a monster. Type 3 is the party: value 0 is any facing, value 1–4 requires north, east, south, or west. Type 4 is one object name-index. Type 8 means the party is carrying object name-index value. Types 5 and 6 do not fire.
Wall switch types 1–4, reached through Mouse.cpp or PressurePadOrAlcove, are click or object switches. The object name comes from the graphics file and is not in DUNGEON.DAT, so those indexes are left as numbers.
Hold is constant pressure. With normally-open, the teleporter is activated while the switch is pressed and deactivated on release. The Level 00 pad at (9,1) is normally closed, so that sense is reversed.
Set, Clear, and Toggle happen when the switch closes. once means the actuator type is then set to 0.
fixed means no working actuator targets that cell. Its initial active bit does not change.
Four database entries, indexes 150, 151, 152, and 163, are not linked from any cell or object list. They are not teleporters on the map. Levels 00–13 contain 175 placed teleporters: 114 start active, 85 have the visible flag, 57 have both, 79 buzz, 22 target their own cell, and 13 use an absolute facing. Seventy have at least one working actuator.

## All 175 placed teleporters
rel +0 does not change facing. rel +1, +2, and +3 turn clockwise from the current facing. abs N/E/S/W replaces facing. self means the destination is this same cell. chain means the destination cell is also a teleporter.

| Level | X | Y | Dest. level | Dest. X | Dest. Y | Active | Rotation | Type | Visible | Audible | Control |
|---|---:|---:|---:|---:|---:|---|---|---|---|---|---|
| 00 | 9 | 1 | 0 | 9 | 1 | No | abs N | All | No | No | Hold at 9,1 type 3, normally closed, facing north only; self |
| 00 | 1 | 3 | 0 | 9 | 1 | No | abs N | All | No | No | Set at 1,3 type 8 once, carrying object 27; chain |
| 01 | 31 | 1 | 1 | 29 | 4 | Yes | abs E | All | Yes | Yes | fixed |
| 01 | 30 | 3 | 1 | 30 | 1 | Yes | abs N | All | Yes | Yes | fixed |
| 01 | 20 | 4 | 1 | 19 | 4 | Yes | rel +0 | Monsters | No | No | fixed |
| 01 | 13 | 16 | 1 | 14 | 14 | Yes | rel +0 | Objects | Yes | No | fixed |
| 01 | 31 | 20 | 1 | 30 | 20 | Yes | rel +0 | Monsters | No | No | fixed |
| 01 | 18 | 21 | 1 | 19 | 21 | Yes | rel +0 | Monsters | No | No | fixed |
| 01 | 26 | 29 | 1 | 25 | 29 | Yes | rel +0 | All | Yes | Yes | fixed |
| 02 | 5 | 0 | 2 | 5 | 0 | Yes | rel +3 | All | No | No | fixed; self |
| 02 | 3 | 4 | 2 | 3 | 4 | Yes | rel +1 | All | No | No | fixed; self |
| 02 | 5 | 4 | 2 | 5 | 4 | Yes | abs S | All | No | No | fixed; self |
| 02 | 16 | 4 | 2 | 17 | 4 | No | rel +0 | Objects | Yes | No | Clear and Set at 17,6 type 1 |
| 02 | 3 | 6 | 2 | 3 | 6 | Yes | rel +1 | All | No | No | fixed; self |
| 02 | 5 | 6 | 2 | 5 | 6 | Yes | rel +1 | All | No | No | fixed; self |
| 02 | 3 | 8 | 2 | 3 | 8 | Yes | rel +1 | All | No | No | fixed; self |
| 02 | 19 | 8 | 1 | 24 | 4 | Yes | rel +0 | All | Yes | Yes | fixed |
| 02 | 2 | 11 | 1 | 7 | 7 | Yes | rel +0 | All | Yes | Yes | fixed |
| 02 | 20 | 14 | 2 | 20 | 12 | No | rel +0 | Objects | No | No | Set and Clear at 21,11 type 1 |
| 02 | 8 | 19 | 1 | 12 | 16 | Yes | rel +0 | Objects | No | No | fixed |
| 02 | 8 | 20 | 1 | 13 | 16 | Yes | rel +0 | All | Yes | Yes | fixed; chain |
| 02 | 0 | 26 | 2 | 6 | 26 | No | rel +0 | All | Yes | No | Clear and Set at 1,25 type 1; chain |
| 02 | 2 | 26 | 2 | 8 | 26 | No | rel +0 | All | Yes | No | Clear and Set at 3,25 type 1; chain |
| 02 | 4 | 26 | 2 | 10 | 26 | No | rel +0 | All | Yes | No | Clear and Set at 5,25 type 1; chain |
| 02 | 6 | 26 | 2 | 12 | 26 | No | rel +0 | All | Yes | No | Clear and Set at 7,25 type 1; chain |
| 02 | 8 | 26 | 2 | 4 | 26 | No | rel +0 | All | Yes | No | Clear and Set at 9,25 type 1; chain |
| 02 | 10 | 26 | 2 | 0 | 24 | No | rel +0 | All | Yes | No | Clear and Set at 11,25 type 1 |
| 02 | 12 | 26 | 2 | 2 | 26 | No | rel +0 | All | Yes | No | Clear and Set at 13,25 type 1; chain |
| 02 | 4 | 28 | 2 | 10 | 28 | Yes | rel +0 | All | Yes | Yes | fixed; a type 5 actuator at 4,27 also points here and does not fire |
| 03 | 2 | 1 | 3 | 1 | 6 | No | rel +0 | All | No | No | Set at 7,5 type 2, party or monster |
| 03 | 3 | 1 | 3 | 2 | 6 | No | rel +0 | All | No | No | Set at 7,5 type 2, party or monster |
| 03 | 5 | 1 | 3 | 4 | 6 | No | rel +0 | All | No | No | Set at 7,5 type 2, party or monster |
| 03 | 6 | 1 | 3 | 5 | 6 | No | rel +0 | All | No | No | Set at 7,5 type 2, party or monster |
| 03 | 30 | 7 | 3 | 14 | 18 | No | rel +0 | All | Yes | Yes | Set at 28,8 type 4 once, object 184 |
| 03 | 23 | 15 | 3 | 23 | 16 | No | rel +0 | Monsters | Yes | Yes | Set at 24,13 type 4 once, object 127 |
| 03 | 21 | 16 | 2 | 16 | 6 | Yes | rel +0 | All | Yes | Yes | fixed |
| 03 | 15 | 18 | 3 | 29 | 6 | No | rel +0 | All | Yes | Yes | Set at 28,8 type 3 once, wall object switch, object 184 |
| 03 | 25 | 24 | 2 | 20 | 14 | Yes | rel +0 | All | Yes | Yes | fixed; chain |
| 03 | 11 | 26 | 2 | 7 | 15 | Yes | rel +0 | Objects | No | No | fixed |
| 03 | 12 | 26 | 2 | 7 | 16 | Yes | rel +0 | All | Yes | Yes | fixed |
| 03 | 17 | 30 | 3 | 15 | 30 | No | rel +0 | All | Yes | Yes | Clear and Set at 17,31 type 1 |
| 04 | 6 | 0 | 4 | 6 | 0 | Yes | rel +3 | All | No | No | fixed; self |
| 04 | 11 | 0 | 4 | 10 | 0 | Yes | rel +0 | Monsters | No | No | fixed |
| 04 | 2 | 3 | 3 | 7 | 3 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 18 | 5 | 4 | 19 | 6 | Yes | rel +0 | Monsters | No | No | fixed |
| 04 | 3 | 14 | 4 | 9 | 17 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 5 | 14 | 4 | 3 | 20 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 6 | 14 | 4 | 3 | 15 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 8 | 14 | 4 | 9 | 17 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 24 | 14 | 4 | 22 | 14 | Yes | rel +0 | All | No | No | Clear at 20,20 type 1 |
| 04 | 4 | 15 | 4 | 5 | 19 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 6 | 15 | 4 | 3 | 18 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 7 | 15 | 4 | 3 | 18 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 24 | 15 | 4 | 22 | 15 | Yes | rel +0 | All | No | No | fixed |
| 04 | 3 | 16 | 4 | 5 | 17 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 5 | 16 | 4 | 4 | 14 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 7 | 16 | 4 | 9 | 17 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 8 | 16 | 4 | 7 | 19 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 24 | 16 | 4 | 22 | 16 | Yes | rel +0 | All | No | No | fixed |
| 04 | 3 | 17 | 4 | 9 | 17 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 4 | 17 | 4 | 3 | 18 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 6 | 17 | 4 | 3 | 18 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 7 | 17 | 4 | 3 | 18 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 24 | 17 | 4 | 22 | 17 | Yes | rel +0 | All | No | No | fixed |
| 04 | 4 | 18 | 4 | 6 | 18 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 5 | 18 | 4 | 9 | 17 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 7 | 18 | 4 | 3 | 18 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 8 | 18 | 4 | 9 | 17 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 24 | 18 | 4 | 22 | 18 | Yes | rel +0 | All | No | No | fixed |
| 04 | 3 | 19 | 4 | 9 | 17 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 4 | 19 | 4 | 3 | 18 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 6 | 19 | 4 | 9 | 17 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 8 | 19 | 4 | 7 | 14 | Yes | rel +0 | All | Yes | Yes | fixed |
| 04 | 24 | 19 | 4 | 22 | 19 | Yes | rel +0 | All | No | No | fixed |
| 04 | 24 | 20 | 4 | 22 | 20 | Yes | rel +0 | All | No | No | fixed |
| 04 | 28 | 20 | 4 | 28 | 19 | Yes | rel +0 | Monsters | No | No | fixed |
| 05 | 4 | 2 | 5 | 6 | 25 | Yes | rel +0 | All | Yes | Yes | fixed |
| 05 | 21 | 4 | 5 | 21 | 7 | No | rel +0 | All | Yes | Yes | Clear at 20,9 type 3, wall object 178; inert type 5 at 19,5 |
| 05 | 15 | 5 | 4 | 15 | 10 | Yes | rel +0 | All | Yes | Yes | fixed |
| 05 | 21 | 6 | 5 | 21 | 7 | No | rel +0 | All | Yes | Yes | Clear at 20,9 type 3, wall object 178; inert type 5 at 19,5 |
| 05 | 11 | 10 | 5 | 18 | 10 | No | rel +0 | All | Yes | Yes | Hold at 11,10 type 1 |
| 05 | 12 | 10 | 5 | 18 | 10 | No | rel +0 | All | Yes | Yes | Hold at 12,10 type 1 |
| 05 | 13 | 10 | 5 | 18 | 10 | No | rel +0 | All | Yes | Yes | Hold at 13,10 type 1 |
| 05 | 14 | 10 | 5 | 18 | 10 | No | rel +0 | All | Yes | Yes | Hold at 14,10 type 1 |
| 05 | 15 | 10 | 5 | 18 | 10 | No | rel +0 | All | Yes | Yes | Hold at 15,10 type 1 |
| 05 | 16 | 10 | 5 | 18 | 10 | No | rel +0 | All | Yes | Yes | Hold at 16,10 type 1 |
| 05 | 17 | 10 | 5 | 18 | 10 | No | rel +0 | All | Yes | Yes | Hold at 17,10 type 1 |
| 05 | 10 | 11 | 5 | 10 | 8 | Yes | rel +0 | Objects | No | No | fixed |
| 05 | 10 | 12 | 5 | 10 | 8 | Yes | rel +0 | Objects | No | No | fixed |
| 05 | 15 | 23 | 5 | 15 | 22 | Yes | rel +0 | Monsters | No | No | fixed |
| 05 | 6 | 24 | 5 | 3 | 1 | Yes | rel +0 | All | Yes | Yes | fixed |
| 05 | 14 | 27 | 5 | 15 | 28 | No | abs N | All | Yes | Yes | Toggle at 12,27 type 1 |
| 06 | 12 | 17 | 6 | 13 | 17 | Yes | abs W | Monsters | No | No | Clear at 14,16 type 2 once, wall object 27 |
| 06 | 16 | 17 | 6 | 15 | 17 | Yes | abs E | Monsters | No | No | Clear at 14,16 type 2 once, wall object 27 |
| 06 | 15 | 26 | 5 | 15 | 25 | Yes | rel +0 | All | Yes | Yes | fixed |
| 07 | 14 | 0 | 7 | 14 | 0 | Yes | rel +3 | All | Yes | No | fixed; self |
| 07 | 18 | 0 | 7 | 18 | 0 | Yes | rel +3 | All | Yes | No | fixed; self |
| 07 | 9 | 2 | 7 | 9 | 2 | Yes | rel +1 | All | Yes | No | Toggle at 9,0 type 1; self |
| 07 | 6 | 6 | 7 | 6 | 6 | Yes | rel +3 | All | Yes | No | fixed; self |
| 07 | 27 | 6 | 7 | 27 | 6 | Yes | rel +3 | All | Yes | No | fixed; self |
| 07 | 6 | 12 | 7 | 6 | 12 | Yes | rel +3 | All | Yes | No | fixed; self |
| 07 | 18 | 12 | 7 | 18 | 12 | Yes | rel +3 | All | Yes | No | fixed; self |
| 07 | 9 | 17 | 7 | 9 | 17 | Yes | rel +3 | All | Yes | No | fixed; self |
| 07 | 27 | 17 | 7 | 27 | 17 | Yes | rel +3 | All | Yes | No | fixed; self |
| 07 | 14 | 22 | 7 | 14 | 22 | Yes | rel +3 | All | Yes | No | fixed; self |
| 07 | 17 | 22 | 7 | 17 | 22 | Yes | rel +1 | All | Yes | No | fixed; self |
| 07 | 17 | 26 | 7 | 17 | 26 | Yes | rel +3 | All | Yes | No | fixed; self |
| 07 | 22 | 26 | 7 | 22 | 26 | Yes | rel +3 | All | Yes | No | fixed; self |
| 07 | 3 | 30 | 7 | 29 | 30 | Yes | rel +0 | All | No | No | fixed |
| 08 | 28 | 0 | 8 | 9 | 7 | No | rel +0 | All | No | No | Set at 9,11 type 1 once |
| 08 | 21 | 1 | 8 | 20 | 1 | Yes | rel +0 | Monsters | No | No | fixed |
| 08 | 12 | 5 | 8 | 12 | 6 | Yes | rel +0 | Monsters | No | No | fixed |
| 08 | 21 | 6 | 8 | 21 | 5 | Yes | rel +0 | Monsters | No | No | fixed |
| 08 | 23 | 21 | 7 | 15 | 18 | Yes | rel +0 | All | Yes | Yes | fixed |
| 08 | 2 | 30 | 8 | 2 | 30 | Yes | rel +2 | All | No | No | fixed; self |
| 09 | 16 | 3 | 9 | 13 | 25 | No | rel +0 | Party+Objects | Yes | Yes | Set at 16,4 type 1 |
| 09 | 8 | 9 | 9 | 14 | 12 | Yes | rel +0 | All | No | No | fixed |
| 09 | 12 | 9 | 9 | 14 | 12 | Yes | rel +0 | All | No | No | fixed |
| 09 | 8 | 11 | 9 | 14 | 12 | Yes | rel +0 | All | No | No | fixed |
| 09 | 12 | 11 | 9 | 14 | 12 | Yes | rel +0 | All | No | No | fixed |
| 09 | 10 | 12 | 9 | 14 | 12 | Yes | rel +0 | All | No | No | fixed |
| 09 | 8 | 13 | 9 | 14 | 12 | Yes | rel +0 | All | No | No | fixed |
| 09 | 12 | 13 | 9 | 14 | 12 | Yes | rel +0 | All | No | No | fixed |
| 09 | 10 | 14 | 9 | 14 | 12 | Yes | rel +0 | All | No | No | fixed |
| 09 | 20 | 14 | 7 | 8 | 3 | Yes | abs N | All | Yes | Yes | fixed |
| 09 | 22 | 14 | 9 | 22 | 15 | No | rel +0 | All | No | Yes | Hold at 22,14 type 1; chain |
| 09 | 23 | 14 | 9 | 22 | 14 | No | rel +3 | All | No | Yes | Hold at 23,14 type 3, any facing; chain |
| 09 | 24 | 14 | 9 | 23 | 14 | No | rel +0 | All | No | Yes | Hold at 24,14 type 1; chain |
| 09 | 22 | 15 | 9 | 22 | 16 | No | rel +0 | All | No | Yes | Hold at 22,15 type 1; chain |
| 09 | 24 | 15 | 9 | 24 | 14 | No | rel +3 | All | No | Yes | Hold at 24,15 type 1; chain |
| 09 | 22 | 16 | 9 | 22 | 17 | No | rel +0 | All | No | Yes | Hold at 22,16 type 1; chain |
| 09 | 24 | 16 | 9 | 24 | 15 | No | rel +0 | All | No | Yes | Hold at 24,16 type 1; chain |
| 09 | 25 | 16 | 9 | 15 | 12 | Yes | abs E | All | Yes | Yes | fixed |
| 09 | 22 | 17 | 9 | 22 | 18 | No | rel +3 | All | No | Yes | Hold at 22,17 type 1; chain |
| 09 | 24 | 17 | 9 | 24 | 16 | No | rel +0 | All | No | Yes | Hold at 24,17 type 1; chain |
| 09 | 22 | 18 | 9 | 23 | 18 | No | rel +0 | All | No | Yes | Hold at 22,18 type 1; chain |
| 09 | 23 | 18 | 9 | 24 | 18 | No | rel +3 | All | No | Yes | Hold at 23,18 type 1; chain |
| 09 | 24 | 18 | 9 | 24 | 17 | No | rel +0 | All | No | Yes | Hold at 24,18 type 1; chain |
| 09 | 1 | 23 | 9 | 0 | 23 | Yes | rel +0 | Monsters | No | No | Clear at 6,24 type 1 |
| 09 | 1 | 24 | 9 | 0 | 24 | Yes | rel +0 | Monsters | No | No | Clear at 1,31 type 3, any facing |
| 09 | 5 | 24 | 9 | 5 | 25 | Yes | rel +0 | Monsters | No | No | Clear at 6,24 type 1 |
| 09 | 14 | 24 | 9 | 16 | 2 | No | rel +0 | Party+Objects | Yes | Yes | Set at 16,4 type 1 |
| 09 | 4 | 25 | 9 | 5 | 25 | Yes | rel +0 | Monsters | No | No | fixed |
| 09 | 4 | 26 | 9 | 5 | 26 | Yes | rel +0 | Monsters | No | No | fixed |
| 09 | 4 | 27 | 9 | 4 | 28 | Yes | rel +0 | Monsters | No | No | fixed |
| 09 | 2 | 28 | 9 | 2 | 29 | Yes | rel +0 | Monsters | No | No | fixed |
| 09 | 3 | 28 | 9 | 3 | 29 | Yes | rel +0 | Monsters | No | No | Clear at 6,24 type 1 |
| 09 | 1 | 29 | 9 | 2 | 29 | Yes | rel +0 | Monsters | No | No | Clear at 1,31 type 3, any facing |
| 10 | 0 | 0 | 10 | 6 | 0 | No | rel +0 | All | Yes | No | Set and Clear at 0,0 type 3, facing east |
| 10 | 9 | 1 | 10 | 9 | 7 | No | rel +0 | All | Yes | No | Clear and Set at 9,1 type 3, facing south |
| 10 | 0 | 8 | 10 | 0 | 2 | No | rel +0 | All | Yes | No | Clear and Set at 0,8 type 3, facing north |
| 10 | 8 | 9 | 10 | 2 | 9 | No | rel +0 | All | Yes | No | Clear and Set at 8,9 type 3, facing west |
| 10 | 0 | 11 | 10 | 0 | 9 | No | rel +0 | All | No | No | Set at 0,5 type 3 once, wall object 178 |
| 10 | 2 | 11 | 10 | 9 | 3 | No | rel +0 | All | No | No | Set at 0,5 type 3 once, wall object 178 |
| 10 | 1 | 12 | 10 | 3 | 1 | No | rel +0 | All | No | No | Set at 0,5 type 3 once, wall object 178 |
| 10 | 3 | 12 | 10 | 11 | 6 | No | rel +0 | All | No | No | Set at 0,5 type 3 once, wall object 178 |
| 10 | 19 | 14 | 10 | 18 | 14 | Yes | rel +0 | Monsters | No | No | fixed |
| 10 | 20 | 20 | 10 | 20 | 21 | No | abs S | All | No | Yes | Hold at 20,20 type 1; chain |
| 10 | 21 | 20 | 10 | 20 | 20 | No | rel +0 | All | No | Yes | Hold at 21,20 type 1; chain |
| 10 | 22 | 20 | 10 | 21 | 20 | No | abs W | All | No | Yes | Set at 22,19 type 2 once, wall object 189; Clear at 20,23 type 3, any facing; chain |
| 10 | 20 | 21 | 10 | 20 | 22 | No | rel +0 | All | No | Yes | Hold at 20,21 type 1; chain |
| 10 | 22 | 21 | 10 | 22 | 20 | No | rel +0 | All | No | Yes | Hold at 22,21 type 1; chain |
| 10 | 20 | 22 | 10 | 20 | 23 | No | rel +0 | All | No | Yes | Hold at 20,22 type 1; Set at 20,23 type 3, any facing |
| 10 | 22 | 22 | 10 | 22 | 21 | No | rel +0 | All | No | Yes | Hold at 22,22 type 1; chain |
| 10 | 22 | 23 | 10 | 22 | 22 | No | abs N | All | No | Yes | Hold at 22,23 type 1; chain |
| 11 | 18 | 1 | 11 | 18 | 2 | Yes | rel +0 | Monsters | No | No | fixed |
| 11 | 6 | 7 | 11 | 6 | 6 | Yes | rel +0 | Monsters | No | No | fixed |
| 11 | 24 | 26 | 11 | 24 | 25 | Yes | rel +0 | Monsters | No | No | fixed |
| 12 | 9 | 2 | 11 | 18 | 12 | Yes | rel +0 | All | Yes | Yes | fixed |
| 12 | 1 | 11 | 11 | 12 | 27 | Yes | rel +0 | All | Yes | Yes | fixed |
| 12 | 10 | 11 | 12 | 13 | 12 | Yes | rel +0 | Monsters | No | No | fixed |
| 12 | 11 | 11 | 12 | 12 | 11 | Yes | rel +0 | Monsters | No | No | fixed |
| 12 | 10 | 13 | 12 | 13 | 12 | Yes | rel +0 | Monsters | No | No | fixed |
| 12 | 11 | 13 | 12 | 12 | 13 | Yes | rel +0 | Monsters | No | No | fixed |
| 13 | 23 | 1 | 12 | 5 | 9 | No | rel +0 | All | No | No | Set at 24,7 type 8 once, carrying object 28 |

## Additional research notes

Where a cell has both Set and Clear, both actuators are in the object list. Which timer is queued last depends on list order at the moment the switch closes. That order is data, not a second rule, and it was not reduced further.

Where a Level 10 pad has both a Set and a Clear of the same facing requirement, both close together on that facing. The resulting active bit is whichever of those two timers runs last.
