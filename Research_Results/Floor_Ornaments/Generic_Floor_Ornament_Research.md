Standard floor ornaments are pre-drawn bitmaps placed with a rectangle table. The Atari path does not scale them. Pits and stairs are separate full-size graphics. A pressure plate uses the same picture whether it is pressed or not.

This is CSBwin’s Atari translation. The blue puddle’s match to DOS is already established and is not re-derived here. Nothing below is a recommendation to change the approved renderer.

## 1. Artwork

A floor picture is an ordinal in `decorations[2]`. `DrawFloorDecorations` (`CSBCode.cpp` 3877) turns a nonzero ordinal into a graphic:

index = ordinal - 1

base  = LoadedFloorDecorations[index][0]

set   = LoadedFloorDecorations[index][1]

graphic = base + RelativeCellGraphicIncrement[cell]

rect    = floorDecorDesc[set][cell]

For a normal level decoration, the loader (`Code390e.cpp` 259–268) fills those two words:

base = 247 + 6 \* FloorDecorationTOC[i]

set  = Byte4110[FloorDecorationTOC[i]]

The bases are six graphic numbers apart. The comment at `Data.h` 1309–1312 says each decoration has several graphics, one for each relative cell. Which of those six is used at each cell is `RelativeCellGraphicIncrement[10]`. Those ten bytes were not read, so the F1, F2, and F3 graphic numbers are not stated here.

`SummarizeRoomInfo` (`Codea59a.cpp` 296–335) chooses the ordinal:

- An open cell starts with `RandomWallDecoration` of the level’s random floor decorations.
- Each actuator on the cell can replace it. If `wallGraphicOrdinal()` is from 1 through `NumFloorDecorations()`, that ordinal becomes the picture.
- A CSBwin extension with selector 255 stores `0x4000 | ParameterB()` instead. That path is not the Atari table.

The source never names a blue puddle, a plate, or a switch. Those are level data: a decorative cell has only the random ordinal, and a plate is an actuator whose 4-bit `wallGraphicOrdinal()` (`CSB.h` 1067) points at one of the same decorations. Two cells share artwork when their ordinals select the same TOC entry.

Footprints are not one of those level entries. Startup forces slot 15 (`CSBCode.cpp` 10714–10716):

LoadedFloorDecorations[15][0] = 241

LoadedFloorDecorations[15][1] = 1

The footprint graphic is `241 + RelativeCellGraphicIncrement[cell]`, using geometry set 1. The draw call is ordinal 16, which becomes index 15.

Pits and stairs do not use this table. They are individual `GRAPHICS.DAT` images and their own rectangles.

| **View**                | **Open, not obscure** | **Obscure (`decorations[0] != 0`)** |
| :---------------------- | --------------------: | ----------------------------------: |
| F3L1, and F3R1 mirrored |                    49 |                           not drawn |
| F3                      |                    50 |                           not drawn |
| F2L1, and F2R1 mirrored |                    51 |                                  57 |
| F2                      |                    52 |                                  58 |
| F1L1, and F1R1 mirrored |                    53 |                                  59 |
| F1                      |                    54 |                                  60 |

For an open pit, `decorations[0]` is cell-flag bit 2, the obscure bit (`Codea59a.cpp` 258–264). A closed pit is drawn as an open room and has no pit graphic.

Stairs use 18 graphic numbers filled backward from `StairFacingUpGraphic[6]` (`Code390e.cpp` 119–128). The executed values are `wallGraphicSet` through `wallGraphicSet + 17`. The expression `wallGraphicSet * 18 + 90` is stored in `D6L` and never read. The slot comments (`Data.h` 1338–1357) are:

| **Slot** | **Up**   | **Down** |
| -------: | :------- | :------- |
|        0 | F0       | F0       |
|        1 | F1       | F1       |
|        2 | F1 sides | F1 sides |
|        3 | F2       | F2       |
|        4 | F2 sides | F2 sides |
|        5 | F3       | F3       |
|        6 | F3R1     | F3 sides |

`StairEdgeGraphic` has four slots, F0 through F2 edge (`Data.h` 1338–1342). `decorations[0]` for stairs is cell-flag bit 2, the up bit (`Codea59a.cpp` 360). Nonzero selects the up graphic. The right-hand scripts mirror it.

## 2. How a standard ornament is drawn

`DECORATION_DESC` (`Data.h` 211–216) is six bytes: unsigned `x1, x2, y1, y2`, then byte width and pixel height. There are three geometry sets and nine cells, `floorDecorDesc[3][9]`.

The native path does not call `ShrinkBLT`, does not apply a depth palette, and does not build a derived cache. It selects one already-sized graphic and blits that rectangle with transparent color 10 (`CSBCode.cpp` 3997–4069). The visible window is the rectangle. The byte width is the source stride. There is no perspective calculation.

The cell index passed to that function is `floorDrawingLocations` (`Viewport.cpp` 1879–1882):

| **View** | **Cell index** | **Mirrored for every ornament** |
| :------- | -------------: | :------------------------------ |
| F3L1     |              0 | no                              |
| F3       |              1 | no                              |
| F3R1     |              2 | yes                             |
| F2L1     |              3 | no                              |
| F2       |              4 | no                              |
| F2R1     |              5 | yes                             |
| F1L1     |              6 | no                              |
| F1       |              7 | no                              |
| F1R1     |              8 | yes                             |

F0 is entry 9. The open, pit, stair, and door scripts for F0 do not call `StdDrawFloorDecoration`, and the geometry table only has cells 0 through 8. F4 scripts do not draw floor ornaments either.

A mirror is `MakeMirror` into a temporary buffer before the blit (`CSBCode.cpp` 4017–4034). Right-hand cells 2, 5, and 8 always mirror. Left and center cells do not, except footprints as described below.

The bit `0x4000` is a CSBwin extension (`CSBCode.cpp` 3895–3958). It keeps one bitmap and scales it. Cells below 3 use `FarColorMapping`. Cells 3 through 5 use `MediumColorMapping` and the rectangle of descriptor 4. Cells 6 and above are not scaled. `ShrinkBLT` is the same sampler as the other scaled graphics. That extension is not how the Atari floor tables are drawn.

if ordinal == 0: nothing

index = ordinal - 1

graphic = base[index] + increment[cell]

blit graphic through floorDecorDesc[set[index]][cell]

if cell is 2, 5, or 8: mirror first

transparent color = 10

if ordinal has bit 0x8000: draw the same way with ordinal 16

## 3. Position and what covers the ornament

The rectangle is copied to the viewport as stored. A partially visible ornament is a smaller rectangle in the table, not a clip computed from the party position.

On a facing door the order is (`Viewport.cpp` 1397–1413):

1. Floor ornament.
2. Objects and monsters on the far side of the door.
3. Frame, top track, and button.
4. Door panel.
5. Objects and monsters on the near side.

On an open cell (`Viewport.cpp` 1071–1106\`) the ornament is first, then the ceiling pit on F2 and F1, then objects. Inside the object call the order is floor items, the monster, missiles, and after all four corners the clouds. The ornament is therefore underneath the door, the items, and the monsters. A later opaque blit covers it. Color 10 leaves it visible. There is no stencil against the wall.

A pit or stair graphic is drawn before the ornament in those scripts (`Viewport.cpp` 1125–1259). Where the two rectangles overlap, the ornament is the later blit.

## 4. Pressure plates

The picture and the switch are separate. `wallGraphicOrdinal()` is copied once while summarizing the room. `PressurePadOrAlcove` (`Code11f52.cpp` 3722) never changes it, so the plate does not change image when it is pressed.

The actuator type selects who can press it (`Code11f52.cpp` 3773–3871\`):

| **Type** | **Who**                                                               |
| -------: | :-------------------------------------------------------------------- |
|        0 | Disabled                                                              |
|        1 | Anything in database types 5 through 13, and monsters                 |
|        2 | Party or monsters                                                     |
|        3 | Party. A nonzero value requires that facing plus one                  |
|        4 | One named object type                                                 |
|        7 | Monsters only                                                         |
|        8 | Party only if it is carrying the named object                         |
|        9 | Party. The action is taken only when the actuator value is 20 or less |

Nothing in this function compares party load or item weight. If another object of the same kind is already on the cell, types 1, 2, 4, and 7 do not fire again.

The action is bits of the same actuator (`CSB.h` 911–916, 1036–1059): set, clear, toggle, or constant pressure. Constant pressure sends set while the pad is down and clear when it is released. The others fire only on the closed transition. `normallyClosed()` inverts that sense. If `audible()` is set, the sound is `soundPRESSUREPAD`, sound 1 (`Code11f52.cpp` 3928–3930). `delay()` is added to the current time (`Code11f52.cpp` 3614). `onceOnly()` then sets the actuator type to 0, which disables it (`Code11f52.cpp` 3613–3614). Otherwise it can be used again. `QueueSwitchAction` sends that action to the target cell. The target’s room type selects the timer, so the same pad can operate a door, a false wall, or another target. The floor graphic is not part of that message.

## 5. Footprints

A party step records the new cell (`Code11f52.cpp` 3266–3285). The list is capped at 24 entries. `AddFootprintEntry` with `0x8018` stores 24 in `Byte13220` for the older entries (`Code11f52.cpp` 4422–4425). `TenSecondUpdate` decrements those bytes by one and deletes the oldest entry when the first byte reaches 0 (`Code17818.cpp` 173–187). The source does not say how often that function runs.

The footprint is drawn only when bit `0x8000` is set on the decoration ordinal (`CSBCode.cpp` 4075–4078). `SummarizeRoomInfo` sets that bit in two cases (`Codea59a.cpp` 388–412):

- The cell is in the spell’s footprint range.
- The cell’s extended flag `ECF_FOOTPRINTS` is set.

Spell 1-5-2 (`Magic.cpp` 1310–1320) increments `MagicFootprintsActive` and records the current list position. While that count is nonzero, each new step extends the end of the range (`Code11f52.cpp` 3279–3281). Timer `TT_79` decrements the count (`CSBCode.cpp` 6921). Steps taken outside that range are still stored, but they do not set the draw bit unless the extended flag is set.

The graphic is not rotated to the party’s facing. It uses the same cell increment as any other ornament. `UseMirrorImages` is `(x + y + facing) & 1` (`Viewport.cpp` 6401). When that is set, the footprint mirror test is cells 7 or 4, or any cell other than 1 (`CSBCode.cpp` 4009–4015). Combined with the normal right-side mirrors, the F3 center is the only cell whose footprint is not mirrored. When the flag is clear, only the three right-hand cells are mirrored. The same flag also mirrors the whole floor bitmap (`Viewport.cpp` 6403–6405). That is a mirror, not a turn toward north, east, south, or west.

## 6. Tables

Graphic `0x22e` is 4720 bytes (`0x1270`). These fields are bytes. Multi-byte words elsewhere in that graphic are little-endian, but the ornament tables here are unsigned bytes.

| **Table**                      | **Offset** | **Length** | **Layout**                                                                                       |
| :----------------------------- | ---------: | ---------: | :----------------------------------------------------------------------------------------------- |
| `floorDecorDesc[3][9]`         |       2248 |        162 | 3 sets, 9 cells, stride 6. Unsigned `x1, x2, y1, y2`, byte width, height.                        |
| `Byte4110`                     |       3136 |         10 | Geometry set for each TOC value. The code does not limit the TOC byte to this length.            |
| `RelativeCellGraphicIncrement` |       3212 |         10 | Unsigned addition to the base graphic for cells 0 through 9.                                     |
| `FloorPitRect[12]`             |       4364 |         96 | Stride 8. Order in the comments: F0R1, F0, F0L1, F1R1, F1, F1L1, F2R1, F2, F2L1, F3R1, F3, F3L1. |
| `StairEdgeRect[8]`             |       4460 |         64 | F0R1, F0L1, F1R1 down, F1L1 down, F1R1 up, F1L1 up, F2R1, F2L1.                                  |
| `StairFacingDownRect[11]`      |       4524 |         88 | Two F0 entries, then F1R1, F1, F1L1, F2R1, F2, F2L1, F3R1, F3, F3L1.                             |
| `StairFacingUpRect[11]`        |       4612 |         88 | Same order as the down table.                                                                    |

The bitmaps are separate graphic records, not bytes inside `0x22e`:

- Decoration `i` on the level: `247 + 6 * FloorDecorationTOC[i]`, plus the increment for the cell.
- Footprints: `241` plus that increment.
- Pits: graphics 49 through 54 and 57 through 60, as listed above.
- Stairs: the 18 values written from `wallGraphicSet`.

`FloorDecorationTOC` is level data in the dungeon, 16 bytes at `Data.h` 1045. It is not a `GRAPHICS.DAT` table. `NumFloorDecorations()` is four bits, so a level has at most 15 of these entries. Slot 15 is reserved for footprints.

## What remains outside this source

- The ten increment bytes, the ten geometry-set bytes, and the 27 rectangles in `floorDecorDesc`. Their locations are above.
- Which TOC value is the blue puddle. The C++ source does not name it.
- The numeric stair graphic actually stored for a level, because it depends on that level’s wall-graphic set and on the unused `* 18 + 90` expression.
- DOS placement of these rectangles. The Atari function uses the pre-drawn graphic and the stored rectangle. The extended `0x4000` path is the one that scales.


