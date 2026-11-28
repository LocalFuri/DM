Door drawing is one shared path. A level picks two door-type indices. Each index has three already-sized panel bitmaps. Ornaments, the button, the frames, and the clip rectangles are separate. The C++ source never uses the names Portcullis or Black Door, and the rectangle bytes live in graphic `0x22e`, which this session could not read.

This is CSBwin’s Atari translation. No DOS PC door routine was compared.

## 1. Artwork

### Door panels: three images per type

`ReadGraphicsForLevel` (`Code390e.cpp` 174–190) fills `d.DoorGraphic[3][2]` (`Data.h` 1334). For each of the level’s two types:

base = 108 + 3 \* levelDescriptor.Door1Type or Door2Type

DoorGraphic[2][typeBit] = base       // F3

DoorGraphic[1][typeBit] = base + 1   // F2

DoorGraphic[0][typeBit] = base + 2   // F1

`Door1Type()` and `Door2Type()` are bits 8–11 and 12–15 of the level descriptor (`CSB.h` 2162–2163). `DB0::doorType()` is one bit and selects column 0 or 1 (`CSB.h` 841). The viewport then selects the row: F3 pushes 2, F2 pushes 1, F1 pushes 0 (`Viewport.cpp` 2082–2085).

`DrawDoor` copies that bitmap into a temporary buffer. The copy lengths are fixed (`Viewport.cpp` 2118–2130):

| **Distance** | **`nearness` passed to `DrawDoor`** | **Bytes copied** |
| :----------- | ----------------------------------: | ---------------: |
| F1           |                                   2 |             4224 |
| F2           |                                   1 |             1952 |
| F3           |                                   0 |              984 |

The panel is not run through `ShrinkBLT`. F1, F2, and F3 are three native graphics. Graphics 108 through 119 are preloaded (`Code390e.cpp` 340–348), which is four type indices times three distances. `DoorCharacteristics` has four entries, at offset 580 in graphic `0x22f`, four big-endian words, swapped on load (`Data.h` 1056, `CSBCode.cpp` 10616). A type index above 3 has no characteristics entry. The source does not name these four types.

### Door ornaments: one native image, then scaled

Ornateness is `DB0` bits 1–4, a value 0–15 (`CSB.h` 844). Zero means no ornament (`TAG004b26`, `CSBCode.cpp` 4092–4093).

For each door decoration on the level (`Code390e.cpp` 270–279):

graphic = 303 + DoorDecorationTOC[i]

placementSet = Byte4122[DoorDecorationTOC[i]]

`TAG004b26` (`CSBCode.cpp` 4084–4150) uses that one graphic at every distance:

- `nearness == 2` (F1): copy the graphic unchanged.
- `nearness == 0` (F3): `ShrinkBLT` through `Byte4172`, cache slot `68 + 2*ornament + 0`.
- `nearness == 1` (F2): `ShrinkBLT` through `Byte4188`, cache slot `68 + 2*ornament + 1`.

Destination width is `x2 - x1 + 1` from the ornament rectangle. Destination height is that rectangle’s byte 5. Source pixel width is twice byte 4 of the source-size record. Those rectangles are not constants in the C++.

Two extra ornament slots are filled at startup (`CSBCode.cpp` 10694–10705):

| **Call**                              | **`Word2514` index** | **Graphic** | **Placement set** |
| :------------------------------------ | -------------------: | ----------: | ----------------: |
| `TAG004b26(16, nearness)` bashed door |                   15 |         301 |                 1 |
| `TAG004b26(17, 2)` F1 see-through     |                   16 |         302 |                 1 |

### Why three images versus one

The panel path never scales. Each distance is a different `GRAPHICS.DAT` index, so a type such as a portcullis is stored as three panels if it uses this path.

The ornament path stores one F1-sized graphic and builds F2 and F3 with `ShrinkBLT`. A decoration such as a black-door plate follows that path. The source does not attach those names to indices. Which index is the portcullis and which ornament is the black door is in the game data, not in this C++.

### Frames, tracks, and the button

Frames come from the wall set, not the door type (`CSBCode.cpp` 2956–2978):

first = 77 + 13 \* wallGraphicSet

pDoorBitmaps[i + 1] = graphic first + i    // i = 0..6

pDoorBitmaps[0] = mirror of pDoorBitmaps[2]

Buffer sizes allocated in `ReadTablesFromGraphicsFile` (`CSBCode.cpp` 10553–10560), and the viewport use (`Viewport.cpp` 1739–1747):

| **`pDoorBitmaps`** | **Allocated bytes** | **Graphic**       | **Drawn as**                                        |
| -----------------: | ------------------: | :---------------- | :-------------------------------------------------- |
|                  1 |                1968 | `first + 0`       | F0 edge frame                                       |
|                  2 |                1504 | `first + 1`       | F1 left pillar                                      |
|                  0 |                1504 | mirror of index 2 | F1 right pillar                                     |
|                  3 |                1560 | `first + 2`       | F2 left pillar; right side is mirrored at draw time |
|                  4 |                 704 | `first + 3`       | F3 left pillar; right side is mirrored at draw time |
|                  5 |                 688 | `first + 4`       | F3L1, and mirrored for F3R1                         |
|                  6 |                 256 | `first + 5`       | F1 top track                                        |
|                  7 |                 144 | `first + 6`       | F2 top track                                        |

The button is graphic `315 + (argument - 1)`. The standard scripts pass 1, so the graphic is 315 (`CSBCode.cpp` 4165–4168). Its placement set is `Byte4124[0]`.

## 2. Composition

The full picture is not built before scaling. The order inside `DrawDoor` (`CSBCode.cpp` 4227–4297) is:

1. Copy the distance-specific panel into `g_tempBitmap`. It is already F1, F2, or F3 size.
2. Stamp the ornament onto that panel. F2 and F3 ornaments are scaled first, then blitted. F1 is blitted at native size. Transparent color is 9. Destination stride is 48 bytes at F1, 32 at F2, and 24 at F3 (`CSBCode.cpp` 4102–4149).
3. If `DoorTOC[type] & 0x400` and the door is not bashed, randomly mirror and randomly flip the whole composite (`CSBCode.cpp` 4250–4260). `TAG008840` (`Code51a4.cpp` 2823) swaps rows. The comment at `Data.h` 1013 calls bit `0x400` the lightning door.
4. On F1, if see-through walls are on, stamp ornament 17 (`CSBCode.cpp` 4263–4268).
5. If the state is bashed (5), stamp ornament 16.
6. Blit a window of that composite to the viewport with transparent color 10 (`BltTempToViewport`, `CSBCode.cpp` 3061–3073).

Frames, the top track, and the button are drawn to the viewport by the cell script. They are not in the temp bitmap. The button is drawn before `DrawDoor`, so the panel covers it where the panel is not color 10. The F1 button rectangle is also copied into `ViewportObjectButtons[5]` (`CSBCode.cpp` 4172–4174) for the click test (`Mouse.cpp` 1092–1105).

`DB0` (`CSB.h` 826–854):

| **Bits** | **Meaning**                          |
| :------- | :----------------------------------- |
| 0        | Which of the two level types         |
| 1–4      | Ornament index; 0 = none             |
| 5        | 1 = opens upward, 0 = opens sideways |
| 6        | Has a button                         |
| 7        | Fireball may bash it                 |
| 8        | Axe or chop may bash it              |

Cell flag bits 0–2 are the state (`CSBTypes.h` 177–186): 0 open, 1 almost open, 2 half, 3 almost closed, 4 closed, 5 bashed. Bit 3 is north/south orientation. The author notes that states 0, 4, and 5 are the ones he had seen in play.

## 3. Scaling and the Black Door

Panel destination size is not calculated. The engine selects one of the three native graphics and clips it with a table rectangle.

Ornament destination size is loaded, not computed from a 16/32 or 20/32 factor. `ShrinkBLT` receives the rectangle width and the stored height (`CSBCode.cpp` 4124–4129). The sampler is the same routine as everything else (`Graphics.cpp` 444–614):

step = ((source << 10) / dest) << 6

sample(i) = (step/2 + 0x7FFF + i \* step) >> 16

Source rows are padded to a multiple of 16 pixels, 8 bytes per 16 pixels. A destination width that is not a multiple of 16 still stores a final 8-byte group; unused bits stay 0. The color map byte is divided by 10 to get the output color. F3 ornaments use `Byte4172`. F2 ornaments use `Byte4188`. F1 ornaments are not remapped. These are not the monster maps `Byte6612` and `Byte6628`.

A Black Door, if it is an ornament, therefore has one native bitmap. F1 stamps it unscaled. F2 and F3 scale it into the rectangle for that distance and that placement set, then stamp it onto whichever panel type the door uses. The panel under it still has its own F1, F2, and F3 graphics. The numeric rectangles were not read.

### Table locations in graphic `0x22e`

Decompressed size `0x1270`. Offsets follow `#pragma pack(1)` and match the already checked wall-decoration offset 2410.

| **Record**                       | **Offset** |                   **Length** | **Structure**                                                                                                                       |
| :------------------------------- | ---------: | ---------------------------: | :---------------------------------------------------------------------------------------------------------------------------------- |
| `Byte5058` ornament source sizes |       2188 |                           60 | Stride 18. Bytes 4 and 5 are source byte-width and pixel height. `TAG004b26` allows set index 0–3; index 3 extends past this array. |
| `Byte5070` ornament rectangles   |       2176 | declared 12, indexed further | `18 * set + 6 * nearness`. Six bytes: x1, x2, y1, y2, byte width, height. Nearness 0, 1, 2.                                         |
| `Byte4172` F3 ornament map       |       3074 |                           16 | Values are color × 10.                                                                                                              |
| `Byte4188` F2 ornament map       |       3058 |                           16 | Same encoding.                                                                                                                      |
| `Byte4122` ornament set lookup   |       3124 |                           12 | One byte per decoration index.                                                                                                      |
| `Byte4124` button set            |       3122 |                            2 | Scripts use byte 0.                                                                                                                 |
| `DoorRectsF1R1[10]`              |       3332 |                           80 | Ten `RectPos`, 8 bytes each.                                                                                                        |
| `DoorRectsF1[10]`                |       3412 |                           80 |                                                                                                                                     |
| `DoorRectsF1L1[10]`              |       3492 |                           80 |                                                                                                                                     |
| `DoorRectsF2R1[10]`              |       3572 |                           80 |                                                                                                                                     |
| `DoorRectsF2[10]`                |       3652 |                           80 |                                                                                                                                     |
| `DoorRectsF2L1[10]`              |       3732 |                           80 |                                                                                                                                     |
| `DoorRectsF3R1[10]`              |       3812 |                           80 |                                                                                                                                     |
| `DoorRectsF3[10]`                |       3892 |                           80 |                                                                                                                                     |
| `DoorRectsF3L1[10]`              |       3972 |                           80 |                                                                                                                                     |
| `DoorTrackTopRect[7]`            |       4052 |                           56 | 0 F1R1, 1 F1, 2 F1L1, 3 F2R1, 4 F2, 5 F2L1, 6 F0 (`Data.h` 1199–1206).                                                              |
| `DoorFrameRect[8]`               |       4108 |                           64 | 0 F1 right, 1 F1 left, 2 F2 right, 3 F2 left, 4 F3 right, 5 F3 left, 6 F3R1, 7 F3L1 (`Data.h` 1208–1219).                           |

Each `RectPos` (`CSB.h` 1407–1417) is x1, x2, y1, y2, byte 4, byte 5, byte 6, byte 7. For the door blit, byte 4 is the source stride, byte 5 is the height used by the lightning flip, and bytes 6 and 7 are the source X and Y of the window (`CSBCode.cpp` 3066–3072).

`DoorCharacteristics[4]` is graphic `0x22f`, offset 580, 8 bytes. Low byte is bash strength. Bit `0x100` lets a monster through a nearly closed door (`Monster.cpp` 300–316). Bit `0x200` is tested for missiles (`Code11f52.cpp` 1895–1896). Bit `0x400` is the random mirror and flip.

## 4. Drawing order

Facing versus edge is chosen in `SummarizeRoomInfo` (`Codea59a.cpp` 365–377). If the door’s north/south bit matches the party’s facing axis, the cell is an edge. Otherwise it is a facing door, and `decorations[0]` is the state and `decorations[1]` is the `DB0` index.

F1 facing, center (`Viewport.cpp` 1425):

1. Floor decoration.
2. Objects and monsters on the far side of the door (`DrawOrder 0x218`: far side, corners 1 then 2).
3. F1 top track.
4. F1 left pillar.
5. F1 right pillar, mirrored from the same bitmap.
6. Button, if `doorSwitch()` is set. `JumpZ` skips five instruction words when it is clear.
7. `DrawDoor`: composite, then the state window.
8. Objects and monsters on the near side (`DrawOrder 0x349`: near side, corners 4 then 3).

F2 center adds the ceiling pit and uses the F2 track and F2 pillars (`Viewport.cpp` 1411–1413). F3 center has no top track; it draws the F3 left pillar, mirrors it for the right pillar, then the button and the door (`Viewport.cpp` 1399–1401).

F1L1 and F1R1 draw only the top track and the door, not the pillars (`Viewport.cpp` 1421–1429). F3L1 draws one side frame and no button. F3R1 mirrors that frame and may draw the button.

The F1 upper corners are the overlap of step 4 or 5 with step 7. The pillars are drawn first. The door is drawn later with transparent color 10, so an opaque door pixel replaces the pillar and a color-10 door pixel leaves the pillar. The source does not contain the corner coordinates, so which pixels are transparent is not known here.

Edge cells do not draw a panel. F3, F2, and F1 edge centers are empty scripts (`Viewport.cpp` 1331–1357). The side edge cells draw floor objects only.

The party’s own cell (`Viewport.cpp` 1367–1437):

- Edge: if see-through is off, blit `pDoorBitmaps[1]` through `DoorTrackTopRect[6]`; then the ceiling pit; then objects and monsters with order `0x21`.
- Facing: no panel and no frame. Ceiling pit, then objects and monsters with order `0x21`.

A fully open door (`state == 0`) returns before any panel blit (`CSBCode.cpp` 4241–4242). The frames already drawn remain.

## 5. Animation, blocking, and damage

There is no pixel velocity. Each timer step adds or subtracts 1 from the 3-bit state. The picture changes because `DrawDoor` selects a different rectangle (`CSBCode.cpp` 4270–4295):

| **State** | **Upward door (`mode() == 1`)** | **Sideways door**       |
| :-------- | :------------------------------ | :---------------------- |
| 0         | Not drawn                       | Not drawn               |
| 1         | `rect[1]`                       | `rect[4]` and `rect[7]` |
| 2         | `rect[2]`                       | `rect[5]` and `rect[8]` |
| 3         | `rect[3]`                       | `rect[6]` and `rect[9]` |
| 4         | `rect[0]`, the closed window    | Same                    |
| 5         | Ornament 16, then `rect[0]`     | Same                    |

The comments at `CSBCode.cpp` 4286–4293 identify `rect[1..3]` as the upward windows and `rect[4..6]` plus `rect[7..9]` as the two halves of a sideways door. Both halves are blitted from the same composite.

`ProcessTT_DOOR` (`Timer.cpp` 1523–1561) does not move the door. It turns the timer into `TT_1` unless the state is already 5. Action 2 toggles direction: if the state is 0 the timer becomes “close,” otherwise it becomes “open.”

`ProcessTT_1` (`Timer.cpp` 1246–1355) moves one state per call:

- Timer byte 9 = 0 opens: state decreases. Byte 9 = 1 closes: state increases.
- It stops when opening reaches 0 or closing reaches 4.
- Each normal step plays sound 2 and reschedules at `time + 1`.
- State 5 returns immediately, so a bashed door does not animate.

Closing onto the party (`Timer.cpp` 1269–1284): if the party is on that cell, the state is not 0, and someone is alive, the state is forced to 0, `DamageAllCharacters(5, 4, 2)` runs, sound 18 (`soundDOOROPENING`, `CSB.h` 640) plays if damage was applied, and the timer waits one extra tick. The door snaps fully open instead of continuing to close.

Closing onto a material monster (`Timer.cpp` 1286–1330): the blocking height is the monster’s vertical size if the door opens upward, and 1 if it opens sideways (`Monster.cpp` 765–772 uses the same rule for passage). If the current state is at least that height, the monster group is damaged, the state opens by one instead of closing, sound 4 plays, and the timer waits. A non-material monster does not take this branch. A monster can pass a door when `state <= verticalSize` for an upward door, or when `state <= 1` for a sideways door, and also when the state is 5 or the monster is non-material (`Monster.cpp` 761–780). `DoorTOC` bit `0x100` also lets a monster through states 3 and 4 (`Monster.cpp` 300–316).

Bashing is `HitDoor` (`Code11f52.cpp` 2289–2327). A chop requires `canOpenWithAxe`. A fireball explosion requires `canOpenWithFireball`. The force must be at least the low byte of `DoorTOC[type]`. Only state 4 changes. It becomes 5 immediately, or after `TT_BASH_DOOR` if a delay was supplied (`Timer.cpp` 1464–1469). The bashed picture is the closed window plus ornament 16.

The button click (`Mouse.cpp` 1078–1109) requires an empty hand, the button bit, and a hit on `ViewportObjectButtons[5]`. It plays sound 1 and queues `TT_DOOR` with action 2, which toggles.

## Pseudocode

panel = graphic(108 + 3\*typeIndex + distance)   # 0=F3, 1=F2, 2=F1

copy panel into temp

if ornament != 0:

&#x20;   if distance == F1: stamp native ornament, transparent 9

&#x20;   else: ShrinkBLT(native, rect[distance]) through F3 or F2 map, then stamp

if lightning and not bashed:

&#x20;   maybe mirror(temp); maybe flip vertically

if F1 and see-through: stamp graphic 302

if state == bashed: stamp graphic 301

if state == 0: draw nothing

else if state == 4 or bashed: blit temp through rect[0], transparent 10

else if opens up: blit temp through rect[state]

else: blit temp through rect[3+state] and rect[6+state]

Frames and the button are viewport blits before that last step. Monsters on the far side are drawn before the frame. Monsters on the near side are drawn after the door.

## Unresolved

- The names Portcullis and Black Door are not in this source, so their type and ornament indices are not assigned.
- Every door rectangle, frame rectangle, ornament rectangle, and the two ornament color maps. Offsets and sizes are above.
- The four `DoorCharacteristics` words.
- Whether a missing `break` after `StdDrawDoor` (`Viewport.cpp` 2143–2145) exists in the Atari binary. In this C++ it falls into `push(0)`. The door arguments have already been popped, so the blit itself is unchanged.
- DOS PC door drawing. Nothing in this session opened a DOS door routine.