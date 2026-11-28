Floor items use one shared draw function. Each item has one native bitmap. F2 and F3, and the far corners of F1, are scaled with the same 20/32 and 16/32 factors as monsters. The corner, not the cell alone, chooses the factor. The numeric rectangles and the per-item graphic bytes are in the graphics file and were not read.

This is CSBwin’s Atari translation. No DOS PC item routine was compared.

## 1. How an item selects its graphic

A floor object is not drawn from its inventory icon. `ProcessOneObject` (`Code51a4.cpp` 2079–2110) does this:

descriptor = object.DescIndex()

graphicClass = ObjDesc[descriptor].graphicClass     // byte, 0..84

floorGraphic = 360 + s5604[graphicClass].objectGraphicIndex

`DescIndex` (`data.cpp` 569–604):

| **Database**                | **Descriptor**                                  |
| :-------------------------- | :---------------------------------------------- |
| Scroll                      | always 0 (`objDI_Scroll`)                       |
| Chest                       | `1 + bits 1–2 of the chest word` (`CSB.h` 1266) |
| Potion, flask, or bomb      | `2 + potionType`                                |
| Weapon                      | `23 + weaponType`                               |
| Clothing, including shields | `69 + clothingType`                             |
| Food, keys, and other misc  | `127 + miscType`                                |

The examples therefore land on these descriptor indices. The graphic-class byte inside each descriptor is not in the C++ source.

| **Item**                     | **Index** |
| :--------------------------- | --------: |
| Scroll                       |         0 |
| Chest, bits 1–2 equal 0      |         1 |
| Sword (`weapon_Sword` is 10) |        33 |
| Shield of Ra                 |       116 |
| Dragon shield                |       121 |
| Apple (`misc_Apple` is 29)   |       156 |
| Iron key                     |       136 |
| Key of B                     |       137 |

Potions are descriptors 2 through 22, one record each (`Objects.h` 250–270). Keys are descriptors 136 through 155. Nothing in the draw function recolors a potion or key. Two items look the same only when their descriptors point at the same `GRAPHIC_CLASS`, or two classes contain the same `objectGraphicIndex`. That sharing is in the 180 descriptor records.

`GRAPHIC_CLASS` (`CSB.h` 2177–2186) is six bytes:

| **Offset** | **Field**            | **Use**                                                                        |
| ---------: | :------------------- | :----------------------------------------------------------------------------- |
|          0 | `objectGraphicIndex` | Added to 360                                                                   |
|          1 | `uByte1`             | Derived-cache base                                                             |
|          2 | `width`              | Half the source pixel width                                                    |
|          3 | `height`             | Source pixel height                                                            |
|          4 | `byte4`              | Bit 0 mirrors on the right. Bit 4 means the next graphic is the alcove version |
|          5 | `byte5`              | Which of the three position tables to use                                      |

Source pixel width is `width << 1` (`Code51a4.cpp` 1876). A class whose width and height are both 16 is a 32×16 bitmap. At 20/32 that becomes a 20×10 bitmap: `(16 * 20 + 10) >> 5 = 10` byte-widths, and the blit uses twice that many pixels. There is no per-item transparent color in this structure. The floor blit always treats color 10 as transparent (`Code51a4.cpp` 2039–2047).

The distance palettes are shared by every floor item. Full-size draws are not remapped. Scaled draws use `Byte5744` at distance 1 and `Byte5728` at distance 2 (`Code51a4.cpp` 1843–1850). `ShrinkBLT` divides each map byte by 10.

Inventory icons are a different set. `DrawItem` (`Character.cpp` 31–76) copies a 16×16 cell from graphics 42 through 48. `Word612[7]` gives the first object-name index on each sheet. Column is the low four bits of the index, times 16. Row is the upper bits, also in 16-pixel steps. The sheet stride is 128. These icons are what the chest and paper-doll screens use.

Several states change the inventory icon and do not change the floor bitmap, because the floor path never calls `NameIndex`:

- An open scroll uses `objNI_Scroll` instead of `objNI_OpenScroll` (`data.cpp` 648–655).
- A lit torch selects an icon from its charges (`data.cpp` 635–646).
- Compass direction, waterskin and jewel charges, and charged weapons also change only the icon (`data.cpp` 630–673).

The chest is the exception on the floor. Its descriptor itself includes bits 1–2, so those bits can select descriptor 1, 2, 3, or 4. Descriptors 2–4 are also the first potion slots. Whether a dungeon sets those bits is in the chest records, not in this source. The open-chest picture in the inventory is `objNI_OpenChest` (`Character.cpp` 3067), which is an icon, not the floor graphic.

An alcove can select one extra floor graphic. If `byte4` bit 4 is set, the level loader also loads graphic `361 + objectGraphicIndex` (`Code390e.cpp` 142–146). While drawing the alcove in the center cell, `ProcessOneObject` uses that next graphic (`Code51a4.cpp` 2103–2107).

A weapon in flight is not this path. If bits 8–12 of its weapon descriptor are nonzero, `GetGraphicClass` returns a negative class (`Code51a4.cpp` 2902–2908`) and the missile code uses graphics `316 + STRUCT5688.uByte0` (`Code51a4.cpp\` 2354–2360). A sword lying still uses the 360-based floor graphic.

## 2. Scaling

`DrawSingleObject` (`Code51a4.cpp` 1806–1880\`) classifies the corner:

near = (corner >= 2)

if cell == 9 or (cell >= 6 and near):

&#x20;   distance = 0

&#x20;   destination byte width = source byte width

&#x20;   destination height = source height

else if cell >= 6 or (cell >= 3 and near):

&#x20;   distance = 1

&#x20;   size = (source \* 20 + 10) >> 5

&#x20;   palette = Byte5744

else:

&#x20;   distance = 2

&#x20;   size = (source \* 16 + 8) >> 5

&#x20;   palette = Byte5728

The `+ 10` and `+ 8` are half of 20 and 16. This is the same rounding monsters use for a whole cell. Items apply 20/32 to the near side of F2 and the far side of F1, and 16/32 to F3 and the far side of F2. Monsters do not make that near/far split.

`ShrinkBLT` (`Graphics.cpp` 444–614) is the same sampler used for ornaments and monsters:

step = ((source << 10) / destination) << 6

sample(i) = (step/2 + 0x7FFF + i\*step) >> 16

Source rows are padded to a multiple of 16 pixels. The destination byte width passed into the blit is rounded up with `(width + 7) & ~7` (`Code51a4.cpp` 2045). That padding is storage, not the visible width. The visible rectangle uses the unrounded width.

At distance 0 the native bitmap is mirrored with `MakeMirror` before the blit, and it is not cached (`Code51a4.cpp` 1825–1828). At distance 1 and 2 the bitmap is scaled into a derived slot and then mirrored (`Code51a4.cpp` 1852–1936). A cache hit skips both operations, so the stored image is already mirrored. The slot is `uByte1 + 104`, plus 1 at distance 1, plus 2 when mirrored, plus 4 for the alcove alternate (`Code51a4.cpp` 1834–1862).

Cells farther than F3 are not scaled. `objectDrawingLocations` is negative for the F4 row (`Viewport.cpp` 1885–1887), and a negative cell is skipped (`Code51a4.cpp` 2255–2256).

## 3. Position

The object’s position is two bits, 0–3 (`CSBTypes.h` 325–334). The screen corner comes from the draw-order nibble (`Code51a4.cpp` 2218–2223):

screenCorner = (nibble - 1)          // 0, 1, 2, 3

absoluteCorner = (screenCorner + partyFacing) & 3

The object is drawn only when `object.pos()` equals that absolute corner. Screen corners (`Code51a4.cpp` 1806–1808):

| **Screen corner** | **Side** | **Near or far** |
| ----------------: | :------- | :-------------- |
|                 0 | left     | far             |
|                 1 | right    | far             |
|                 2 | right    | near            |
|                 3 | left     | near            |

The anchor is two bytes in `viewportPosition[byte5][cell][corner]` (`Code51a4.cpp` 2110, `Data.h` 1105–1118). X is the center. Y is the bottom:

x += smallOffsets[distance\*8 + indexX]

bottom += smallOffsets[distance\*8 + indexY]

top = bottom - (height - 1)

right = min(223, x + byteWidth)

left = max(0, x - byteWidth + 1)

`Code51a4.cpp` 1941–1982. After each item, the small-offset index advances by one (`Code51a4.cpp` 1962–1973). The offsets are signed table bytes, not random numbers. The draw function itself does not add a random jitter.

`objectDrawingLocations` (`Viewport.cpp` 1885–1887) maps the script cell to the position-table row:

| **View**       | **Table row** |
| :------------- | ------------: |
| F3, F3L1, F3R1 |       0, 1, 2 |
| F2, F2L1, F2R1 |       3, 4, 5 |
| F1, F1L1, F1R1 |       6, 7, 8 |
| F0             |             9 |
| F0L1, F0R1     |        10, 11 |
| F4 row         |      negative |

Rows above 9 are rejected (`Code51a4.cpp` 2258). Items in the cells beside the party are not drawn. F0, the party’s own cell, is drawn at full size.

If the left edge is clipped to 0, the source X is `byteWidth - anchorX - 1` (`Code51a4.cpp` 1996–1999). The bottom is limited to 135. The top is not given a separate lower bound; it is stored as an unsigned byte.

## 4. Several items on one corner

`AddObjectToRoom` appends a new object to the end of the cell’s linked list (`Code11f52.cpp` 441–504). `ProcessOnePosition` walks that list from the first object (`Code51a4.cpp` 2230). The oldest item is drawn first and the newest is drawn last, so the newest covers the older ones where its pixels are not color 10.

There is no sort by size. Every item uses the same center-and-bottom anchor. The only separation between items on a corner is the successive small-offset entry. A large bitmap obscures a smaller one only because it is drawn later and is opaque.

Monsters are not in that stack. The scan remembers the monster, finishes every floor item at the corner, then calls `DrawMonster` (`Code51a4.cpp` 2284–2332). Missiles are drawn after the monster (`Code51a4.cpp` 2336–2629).

Some created objects do get a random corner. `CreateAndDropObject` stores `STRandom0_3()` (`Code11f52.cpp` 920). That happens when the object is created, not while it is drawn.

## 5. What is drawn over what

An open-room script draws the wall, the wall decoration, the floor decoration, and then the objects (`Viewport.cpp` 1071–1106). A facing door draws far-side objects, the frame and button, the door panel, then near-side objects (`Viewport.cpp` 1395–1429).

Inside one object call the order is:

1. Floor items at one corner, oldest first.
2. The monster at that corner.
3. Missiles at that corner.
4. The next corner in the draw-order nibble.
5. After all four corners, clouds (`Code51a4.cpp` 2802–2809).

Door nibbles with bit 3 set split the corners into the far pass and the near pass. The door panel is between those passes. There is no stencil against the wall shape. Occlusion is the order of the blits and color 10.

Missiles that are not ordinary objects use `STRUCT5688` and scale by `Byte5752[distance]`, not by 16 or 20 (`Code51a4.cpp` 2377–2392). Clouds use graphics 348 and up, with sizes in `Byte5696`, and their own scale (`Code51a4.cpp` 21–74).

## 6. Clicking, pickup, and drop

While drawing a touchable item, the engine stores its rectangle in `ViewportObjectButtons[corner]` and the object in `rn10574[corner]` (`Code51a4.cpp` 2001–2037`). Touchable means the center cell and either F0 or a near corner of F1 (`Code51a4.cpp\` 1810–1820). Side cells, F2, and F3 are visible at those distances but are not clickable.

The first item at a corner copies its rectangle. If that rectangle is shorter than 14 pixels, the hit box is pulled inward (`Code51a4.cpp` 2012–2022). Later items replace the object id and expand the rectangle to the union. The click therefore picks the newest item, while the hit area can include the older items underneath it.

`HandleClickInViewport` (`Mouse.cpp` 1122–1159) tests buttons 0 through 5 with `clickY - 33`. Buttons 0–3 call `TAG01a148`. Button 5 is the door button, the alcove, or the fountain. A pickup in the cell ahead is refused when a non-levitating monster occupies that corner (`Mouse.cpp` 821–838).

Drop rectangles are separate. They are four screen-coordinate rectangles in `DropAreas`, and the click is not adjusted by 33 (`Mouse.cpp` 1186–1197 and 1259–1268). Against a stone wall only the first two are tested, and those drop into the party’s cell. Otherwise four rectangles are tested. `DropObject` (`Mouse.cpp` 866–913) converts the rectangle index:

if index == 4: index = 2          // alcove

if index > 1: step into the cell ahead

corner = (partyFacing + index) & 3

The alcove uses the same corner the draw uses, `(facing + 2) & 3` (`Code51a4.cpp` 2210–2213). Its position comes from slot 4 of the position table, which the table comment calls the niche (`Data.h` 1114–1118). The draw order is 0, and a zero order is the alcove pass (`Code51a4.cpp` 2770). The wall decoration is drawn first; `DrawWallDecoration` returns whether that decoration is an alcove (`Viewport.cpp` 2521–2525).

The fountain is not a floor-item position. A click on button 5 while facing it can fill a flask and then calls `TouchWallF1` (`Mouse.cpp` 1215–1249). There is no separate holder bitmap in the floor-item path.

## 7. Tables to extract

Graphic `0x22e` is 4720 bytes (`0x1270`). Words in `STRUCT5688` are little-endian. The other item tables here are bytes.

| **Table**                    | **Offset** | **Length** | **Layout**                                                                                                                                 |
| :--------------------------- | ---------: | ---------: | :----------------------------------------------------------------------------------------------------------------------------------------- |
| `viewportPosition[3]`        |       1156 |        300 | 3 sets × 10 cells × 5 corners × 2 bytes. Byte 0 is center X, byte 1 is bottom Y. Y of 0 suppresses the slot. Set is `GRAPHIC_CLASS.byte5`. |
| `smallOffsetIndicies[16][2]` |       1456 |         32 | Unsigned. `[n][0]` indexes X, `[n][1]` indexes Y.                                                                                          |
| `smallOffsets[24]`           |        594 |         24 | Signed. Index is `distance * 8 + tableIndex`.                                                                                              |
| `Byte5744`                   |       1502 |         16 | Distance-1 color map. Each byte is color × 10.                                                                                             |
| `Byte5728`                   |       1518 |         16 | Distance-2 color map, same encoding.                                                                                                       |
| `s5604[85]`                  |       1642 |        510 | `GRAPHIC_CLASS`, stride 6, as listed above.                                                                                                |
| `struct5688[14]`             |       1558 |         84 | Missile and cloud-like graphics, stride 6. `word4` is little-endian.                                                                       |
| `Byte5752[8]`                |       1494 |          8 | Missile scale factors.                                                                                                                     |
| `Byte5696[8]`                |       1550 |          8 | Cloud source width and height pairs.                                                                                                       |

Graphic `0x22f` is 3086 bytes (`0xc0e`). `ObjDesc[180]` starts at offset 1958. Stride 6: little-endian object type, graphic-class byte, attack-class byte, little-endian carry mask. The loop that swaps them is `CSBCode.cpp` 10627–10631. One hundred eighty records are swapped; the declared array length is also 180.

Graphic `0x231` is 2052 or 2004 bytes (`0x804` or `0x7d4`). `DropAreas` is 16 bytes at offset 472: four rectangles of unsigned `x1, x2, y1, y2` in screen coordinates.

Graphic `0x232` is 1826 bytes (`0x722`). `Word612[7]` is seven little-endian signed words at offset 1218. They are the first inventory object-name index on each of the seven icon sheets.

Floor graphics are not one record. Each is basic graphic `360 + objectGraphicIndex`, loaded by `GetBasicGraphicAddress`. The class width and height say how much of that graphic is used.

## What is not in this source

- The 85 graphic-class records, the 180 descriptor bytes, and the position and offset tables. Their locations are above.
- Which keys or potions share a class.
- Which chest bit values occur in a dungeon.
- DOS PC item scaling and placement. The 20/32 and 16/32 factors, the `0x7FFF` sampler bias, and these tables are CSBwin’s Atari translation.