Monster drawing is one shared path. Every family, including the Screamer (monster type 6), goes through DrawMonster in Code51a4.cpp. The C++ source does not contain the Screamer’s widths, graphic index, or placement bytes. Those values are loaded from GRAPHICS.DAT.

CSBwin is a translation of the Atari engine. This tree has no DOS disassembly, so original DOS behavior is verified only where the source says it was checked against the original machine code. Everything else below is CSBwin behavior.

1. Monster graphics
Where the tables and bitmaps come from
ReadTablesFromGraphicsFile (CSBCode.cpp 10531) expands three graphics straight onto the global data bank:

Graphic	Decompressed size	Lands on	Contents
0x22f
0xc0e
d.Byte10340
MONSTERDESC MonsterDescriptor[27] (Data.h 1059)
0x22e
0x1270
d.Byte7248 + 2, which is d.Word7246
Placement, color maps, ITEM12 Item6414[27], viewport positions (Data.h 1092–1120)
0x232
0x722
d.Byte1832 + 2
Palettes, including specialColors used later for monster recoloring
#pragma pack(1) is on (Data.h 2), so these structs match the file layout. On the little-endian build, LE16 byte-swaps 16-bit fields (CSBTypes.h 15–16, 133–141). ITEM12_word0 is swapped explicitly (CSBCode.cpp 10566–10568). MONSTERDESC words are swapped in fixItem26 (CSBCode.cpp 10483–10490).

Monster bitmaps are ordinary GRAPHICS.DAT entries. At level load, indices 446 through 532 are preloaded (Code390e.cpp 360–363). The bitmap used for a draw is:


Code51a4.cpp
Ln 588–589

graphicNum0 = sw(446 + pI12_A2->ITEM12_word0);
graphicNumFBSAE = pI12_A2->ITEM12_word2;
GetBasicGraphicAddress (Graphics.cpp 2612) expands that entry from 4-bitplane Atari data. Width in the file header is in pixels; the expander stores two pixels per byte (Graphics.cpp 2647–2664). ITEM12 widths are those byte widths. Pixel width is 2 * byteWidth.

ITEM12_word2 is not a file index. ReadTablesFromGraphicsFile assigns it as a derived-graphic cache id, starting at 0x1ef (CSBCode.cpp 10814–10820).

Descriptor used by the renderer
ITEM12 (CSB.h 2188–2201):

Field	Role
ITEM12_word0
Added to 446 to get the front bitmap index
ITEM12_word2
First derived-cache slot for this monster
frontRearByteWidth, frontRearHeight
Front and back, byte width and pixel height
sideByteWidth, sideHeight
Side view
attackByteWidth, attackHeight
Attack view
uByte10 bits 0–3
Transparent color 0–15
uByte10 bits 4–7
Which of the three ITEM110 placement tables (d.s6982[0..2])
uByte11 low / high nibble
Optional recolor schemes for palette colors 9 and 10
MONSTERDESC.uByte0 selects the ITEM12 (Code51a4.cpp 426–427). The struct comment says this byte is the monster type index itself (CSB.h 2297–2300). That is the author’s reading of the file, not something the C++ assigns. The lookup is Item6414[descriptor.uByte0], so if the file ever stored a different value, that value would win.

Graphic-existence flags are MONSTERDESC.word4 (CSB.h 2266–2288):

Mask	Accessor	Meaning in the renderer
0x0003
Nibble0()
How many extra front-mirror cache groups to reserve (0–3)
0x0004
NonAttackingMirrorOK()
Front view may be mirrored
0x0008
HasSideGraphic()
A side bitmap exists
0x0010
HasBackGraphic()
A back bitmap exists
0x0020
HasAttackGraphic()
An attack bitmap exists
0x0080
HasBit7()
At distance 1, a non-attacking front view may take an extra mirror path
0x0100
HasBit8()
Combined with bit 7, that distance-1 mirror is actually performed
0x0200
AttackingMirrorOK()
Attack pose may be mirrored
0x0400
HasBit10()
While already attacking, do not randomize the mirror bit again
0x3000
Nibble12()
Random range for the X jitter index
0xC000
Nibble14()
Random range for the Y jitter index
Horizontal size is word2 bits 0–1 (CSB.h 2322): 0 = one corner, 1 = two corners, 2 = the whole tile. Vertical size (word2 bits 7–8) is not read by DrawMonster.

Front, back, side, attack order
There is one source bitmap per view, not a walk cycle. Relative facing is (partyFacing - monsterFacing) & 3 (Code51a4.cpp 467–469): 0 = showing its back, 1 = its right, 2 = facing the party, 3 = its left.

DrawMonster walks the source index like this (Code51a4.cpp 600–716):

Start at front: 446 + word0.
Side, if HasSideGraphic and relative facing is 1 or 3. Index becomes front+1. Side dimensions are used. Back and attack are not considered.
Otherwise back, if HasBackGraphic and relative facing is 0. Index is front+1, or front+2 if a side bitmap exists.
Otherwise attack, if the monster is not showing its back, sms bit 0x80 is set, and HasAttackGraphic. Index is front+1, plus one if a side exists, plus one if a back exists.
Otherwise the front bitmap is used. If mirroring is allowed and sms bit 0x40 is set, the source index stays on the front bitmap and a later derived slot holds the mirror.
So the file order of bitmaps is:

Front, then Side if present, then Back if present, then Attack if present.

The derived-cache slots reserved in CSBCode.cpp 10814–10888 follow the same view order, and for each view they reserve scale 16/32 then scale 20/32. After those, word4.Nibble0() groups of three slots are reserved for the mirrored front: full size, scale 16/32, scale 20/32.

What the code mirrors
Mirroring is MakeMirror (CSBCode.cpp 3006), which copies and then calls Mirror (CSBCode.cpp 4674). Mirror reverses each byte through the 256-byte bit-reversal table data008734 (CSBCode.cpp 4398). That reverses pixels inside the 4-bitplane words.

Verified mirror cases inside DrawMonster:

Side, relative facing 1 (monster’s right side toward the party). Facing 3 uses the side bitmap as stored. At distance 0 this is MakeMirror into a temporary bitmap (Code51a4.cpp 724–729). Farther away it is Mirror after scaling (Code51a4.cpp 857–871).
Attack, when sms bit 0x40 is set. Same split: MakeMirror at distance 0 (Code51a4.cpp 737–743), Mirror after ShrinkBLT farther away.
Non-attacking front, when NonAttackingMirrorOK and bit 0x40 are both set, and the cell is at distance 0 (RelativeCellNumber >= 6). The mirrored front is cached in the derived slot (Code51a4.cpp 746–765).
Distance 1 only, and only if HasBit7 and HasBit8 are set, and the view is not side, back, or attack. b_102 is that condition (Code51a4.cpp 798) and the later test mirrors the scaled bitmap (Code51a4.cpp 857–859).
At distance 2 the code forces frontViewMirror_106 = false (Code51a4.cpp 808). The test frontViewMirror && !NonAttackingMirrorOK() cannot succeed, because frontViewMirror is only set when NonAttackingMirrorOK is already true (Code51a4.cpp 695 and 861). That matches the commented original branch the author left in place (Code51a4.cpp 850–851). So a mirrored idle front is produced at distance 0. At distance 1 and 2 the mirror bit only changes which cache slot is selected; the pixels stay unmirrored unless the bit-7/bit-8 path runs at distance 1.

The author wrote that b_102 = false at distance 2 is a guess because the variable would otherwise be uninitialized (Code51a4.cpp 803–806). That guess is CSBwin, not a verified original-machine-code fact.

Transparency and palette
TAG0088b2 (Graphics.cpp 661) is the viewport blit. A transparent color of 0–15 skips that color. A negative color copies every pixel. The viewport destination stride is 112 bytes, which is 224 pixels.

Distance 0: transparent index is uByte10 & 15 (Code51a4.cpp 722). Pixels are not recolored.
Distance 1 (scale 20/32): ShrinkBLT remaps every pixel through d.Byte6628. The transparent index becomes Byte6628[rawTransparent] / 10 (Code51a4.cpp 799, 818–820).
Distance 2 (scale 16/32): the same, through d.Byte6612 (Code51a4.cpp 809).
ShrinkBLT reads a 4-bitplane pixel, looks it up in the 16-byte map, and writes color mapValue / 10 (Graphics.cpp 525–531). The maps are therefore stored as multiples of 10. d.IdentityColorMap is the identity series 0,10,20,... (Data.h 1450). Monster draws do not use that identity map. Clouds and floor decorations do.

Byte6628 and Byte6612 sit inside graphic 0x22e, but level load overwrites individual entries. ModifyPalettes (CSBCode.cpp 2926–2941) copies specialColors[scheme][6] into palette color whichColor and into those two maps (low byte to Byte6628, high byte to Byte6612). The defaults are color 9 from scheme 8 and color 10 from scheme 12 (Code390e.cpp 316–317). For each monster type allowed on the level, ITEM12.uByte11 can replace those: low nibble, if nonzero, is the scheme for color 9; high nibble, if nonzero, is the scheme for color 10 (Code390e.cpp 318–336).

2. Generic renderer
Call chain
DrawViewport walks cells 0 through 20 (Viewport.cpp 6450). newgraphics is defined (Viewport.cpp 319), so this loop is the active one. The older per-cell calls under #ifndef newgraphics are not compiled.
Each cell runs pStdDrawCode[cell][roomType] (Viewport.cpp 1447, 6472).
The opcode StdDrawRoomObjects calls DrawRoomObjects (Code51a4.cpp 2650), originally Atari address TAG0051a4.
DrawRoomObjects consumes the draw-order word four bits at a time and calls ProcessOnePosition (Code51a4.cpp 2781–2801).
ProcessOnePosition draws floor objects at that corner, then calls DrawMonster (Code51a4.cpp 2295–2332).
DrawMonster selects the bitmap, scales, mirrors, and blits with TAG0088b2.
GetAlternateGraphicsRecord is a CSBwin extension. If it returns a record, the second DrawMonster (defined at Code51a4.cpp 1135) draws from CSBgraphics.dat instead. The author says that function was copied from the original DrawMonster. The original GRAPHICS.DAT path is the first function. A shared Unity renderer should follow the first one.

Cell names, distances, and scales
The names S1, S2, and S3 do not appear in this source. The viewport cells are F4, F3, F2, F1, F0 plus L1/R1/L2/R2. Object-drawing indices come from objectDrawingLocations (Viewport.cpp 1885–1887):

Viewport cell	objectDrawingLocations index	Value passed as RelativeCellNumber	DrawMonster distance	Scale
F3
9
0
2
16/32
F3L1
7
1
2
16/32
F3R1
8
2
2
16/32
F2
14
3
1
20/32
F2L1
12
4
1
20/32
F2R1
13
5
1
20/32
F1
17
6
0
none (full bitmap)
F1L1
15
7
0
none
F1R1
16
8
0
none
F0
20
9
0
none
F0L1
18
10, then decremented to 9 inside DrawMonster
0
none
F0R1
19
11, then decremented to 10
0
none
F4, F4L1, F4R1
6, 4, 5
−3, −2, −1
not drawn
—
DrawMonster sets the distance here (Code51a4.cpp 718–810):

RelativeCellNumber >= 6: distance 0, no ShrinkBLT.
>= 3: distance 1, scaleFactor = 20.
otherwise: distance 2, scaleFactor = 16.
If the incoming number is greater than 9, it is decremented before the position-table lookup (Code51a4.cpp 568–572). MiddleLeftRight was already computed from the original number as (RelativeCellNumber + 3) % 3 (Code51a4.cpp 2772): 0 = center, 1 = left, 2 = right.

F4 cells exit before DrawMonster because RelativeCellNumber < 0 (Code51a4.cpp 2290–2293). F3L2 and F3R2 only have a stone side-wall script (Viewport.cpp 942–948). They never call DrawRoomObjects.

There are three scales for every monster, not a separate scale per left/center/right cell. Left and right cells use the same bitmap size as the center cell at that depth. They are shifted by ±100 pixels (Code51a4.cpp 897–906).

Destination size (Code51a4.cpp 812–817):


dstByteWidth = (srcByteWidth * scale + scale/2) >> 5
dstHeight    = (srcHeight    * scale + scale/2) >> 5
scale is 16 or 20, and the shift by 5 divides by 32. The same formula, written as a division, is TAG020880 (CSBCode.cpp 10431–10442), which only computes the cache buffer size. It then rounds the byte width up to a multiple of 8. The blit itself uses the unrounded dstByteWidth.

ShrinkBLT is called with pixel widths 2 * srcByteWidth and 2 * dstByteWidth (Code51a4.cpp 830–836).

Same scaler as wall ornaments
Yes for the sampler. No for the scale factors.

ShrinkBLT (Graphics.cpp 444) is the only scaler. Wall ornaments call it from DrawWallDecoration (CSBCode.cpp 3618–3627). Floor decorations, missiles, and clouds call it too.

The step is a 16.16 fixed point. The source builds it as ((srcWidth << 10) / dstWidth) << 6 (Graphics.cpp 469–475). The << 6 is the low six fraction bits that complete the << 16. The initial accumulator is step/2 + 0x7fff (Graphics.cpp 475–477). The same construction is used for Y. Each output pixel samples one source pixel. There is no filtering.

Wall ornaments do not pass 16 or 20. Their destination width and height are the on-screen DECORATION_DESC rectangle, and the color map is FarColorMapping or MediumColorMapping (CSBCode.cpp 3584–3627). Monsters pass a destination that is exactly 16/32 or 20/32 of the source, and they pass Byte6612 or Byte6628.

Object graphics (GRAPHIC_CLASS s5604[85]) do use cache scales 16 and 20 (CSBCode.cpp 10723–10726). Those are the same two fractions as monsters, still through ShrinkBLT.

Screen position
The placement table is ITEM110 s6982[3] (CSB.h 2413–2420): 3 groups, 11 cells, 5 pairs of (x, y). Pair index is positionWithinCell:

Index	Use
0
Far-left corner of the tile, clockwise from the party’s view
1
Far-right
2
Near-right, also a two-square sideways case
3
Near-left, also a centered sideways two-square monster
4
Centered monster
A Y byte of 0 means “do not draw this slot” (Code51a4.cpp 584). The bytes are in graphic 0x22e. They are not numeric constants in the C++.

DrawMonster (Code51a4.cpp 887–938):


y = tableY + (signed char)smallOffsets[(distance >> 3) + sms.Nibble3()]
y2 = min(y, 135)
y1 = max(0, y - dstHeight + 1)
x = tableX + smallOffsets[(distance << 3) + sms.Nibble0()]
if left cell:  x -= 100
if right cell: x += 100
x2 = clamp(x + dstByteWidth, 0, 223)
x1 = clamp(x - dstByteWidth + 1, 0, 223)
distance is only 0, 1, or 2, so distance >> 3 is always 0. The Y jitter index is just Nibble3. The X index is 0..7, 8..15, or 16..23, which covers all 24 bytes of d.smallOffsets (Data.h 1100).

Y is the bottom of the bitmap. X is the center, because byte width equals half the pixel width, and the rectangle extends dstByteWidth pixels to each side.

If x2 == 0 or x1 == 223, nothing is drawn. If x1 == 0, the source X offset is increased so the clipped left edge lines up (Code51a4.cpp 909–921).

Draw-order words for F1, F2, F3 and the side cells
DrawRoomObjects reads the order from the low nibble upward. Each nibble is a 1-based corner: 1 far-left, 2 far-right, 3 near-right, 4 near-left (Code51a4.cpp 2656–2658, 2219–2224). If bit 3 of the original word is set, bit 0 selects the door side and the word is shifted right by 4 (Code51a4.cpp 2755–2767): bit 0 clear = far side of the door, bit 0 set = near side.

Open-room scripts (Viewport.cpp 1071–1118):

Cell	Order	Corners, first to last
F3, F2, F1
0x3421
far-left, far-right, near-left, near-right
F3R1, F2R1
0x4312
far-right, far-left, near-right, near-left
F1L1
0x32
far-right, near-right
F1R1
0x41
far-left, near-left
F0
0x21
far-left, far-right
F0L1
0x02
far-right
F0R1
0x01
far-left
Door scripts draw twice (Viewport.cpp 1397–1429). Far corners, then the door bitmap, then near corners:

Cell	Before the door	After the door
F3, F2, F1
0x218 → far side, corners 1 then 2
0x349 → near side, corners 4 then 3
F3R1, F2R1
0x128 → far side, corners 2 then 1
0x439 → near side, corners 3 then 4
F1L1
0x28 → far side, corner 2
0x39 → near side, corner 3
F1R1
0x18 → far side, corner 1
0x49 → near side, corner 4
A stone F1/F2/F3 script passes DrawOrder00, which is 0. That takes the alcove path and returns before DrawMonster (Code51a4.cpp 2210–2215, 2290–2293). Monsters behind a stone wall are not drawn. The wall bitmap for that cell is drawn by the same script, and later cells overdraw earlier pixels because cells run from index 0 to 20.

3. Monster groups
A group is one DB4 plus one ITEM16 (CSB.h 2224–2261). ITEM16 holds up to four monsters:

facings: four 2-bit absolute facings, monster 0 in the low bits.
positions: four 2-bit corners. A two-square monster uses corner 0 or 2.
positions == 255: the group is drawn as one centered figure.
singleMonsterStatus[4]: per-creature attack, mirror, and jitter.
OrdinalOfMonsterAtPosition (Code11f52.cpp 627) walks from the last creature down to 0 and returns the first match as a 1-based ordinal. One corner therefore shows the highest-index creature standing there.

Size 0: the corner must equal that creature’s position nibble.
Size 1: the creature occupies its nibble and the next clockwise corner. If the creature’s facing is perpendicular to the queried corner, the search corner is rotated one step counterclockwise first (Code11f52.cpp 639–655).
positions == 255: the function returns 1 immediately, meaning “something is here” (Code11f52.cpp 635–636).
DrawMonster then (Code51a4.cpp 449–565):

Size 2 with a real position byte returns without drawing. The comment says multiple full-tile creatures, such as dragons, are invisible, and that this was in the original behavior (Code51a4.cpp 475–487).
Size 2 with positions == 255 waits until the last corner of the cell, and until it is not the far side of a door, then draws one centered figure at slot 4.
Size 1 with positions == 255 does the same, but uses slot 3 when the relative facing is sideways and slot 4 otherwise (Code51a4.cpp 495–501).
Size 1 with real corners is only drawn on the last far corner or the last corner of the pass. Sideways, it uses slot 2 or 4. Facing along the party axis, it uses slot 0 or 1 based on whether its position nibble equals the party facing or the facing to the party’s left (Code51a4.cpp 506–552). That path can draw a second image: it swaps slots 0 and 1 and switches between singleMonsterStatus[0] and [1] (Code51a4.cpp 942–956). Facing is not recomputed for the second image.
Size 0 is drawn at the corner currently being visited, if OrdinalOfMonsterAtPosition found a creature there.
drawAsSize4 (Code51a4.cpp 421, 428–429) is a CSBwin option (drawAsSize4Monsters && DB4.drawAsSize4()). It forces size 2 and picks one creature: the attacker if any, otherwise the one facing the party, otherwise the first (DrawSize4Index, Code51a4.cpp 323–349). That is not the original path.

Drawing order and occlusion
Inside one tile, objects at the current corner are drawn before the monster (ProcessOnePosition, the default branch, then DrawMonster). Missiles at that corner are drawn after the monster (Code51a4.cpp 2335–2349). Clouds are drawn after every corner (DrawRoomObjects, Code51a4.cpp 2803–2809).

Creatures in the same tile overdraw in nibble order. Creatures in different tiles overdraw in cell-index order, 0 through 20. There is no extra depth buffer and no stencil against the wall shape. Occlusion is “later blit covers earlier pixels,” plus these skips:

Stone cells do not submit their monsters (DrawOrder 0).
F1L1/F1R1 only submit the two corners on the inner side.
F0 only submits the far corners. F0L1 and F0R1 submit one corner each.
A door is blitted between the far-corner pass and the near-corner pass, so the door covers creatures on the far side.
Centered creatures (positions == 255) refuse the far-side-of-door pass and wait for the last corner (Code51a4.cpp 489–494).
The blit rectangle is clamped to x 0–223 and y 0–135.
Monsters do not clip each other except by this order. A later corner or a nearer cell simply paints over the earlier bitmap.

4. Animation
There is no frame number and no idle or walk-cycle bitmap. The bitmap set is front, side, back, and attack. “Idle” and “moving” use the same bitmap. Motion changes the map position and the facing nibbles, which changes which of those four views is selected and which viewport cell draws it.

The per-creature byte SINGLE_MONSTER_STATUS (CSB.h 2204–2221) is the whole pose:

Bits	Meaning
0x80
Attacking. Selects the attack bitmap when the creature is not showing its back.
0x40
Mirror.
bits 5–3
Y jitter index, 3-bit, passed to smallOffsets.
bits 2–0
X jitter index, 3-bit.
NextMonsterUpdateTime (CSBCode.cpp 6187) rewrites that byte. It is called from the monster timer handlers in Monster.cpp, not from the renderer.

Both jitter fields are cleared, then set from word4 bits 14–15 (Y) and 12–13 (X). The code draws STRandom(range) and, half the time, stores (-value) & 7 (CSBCode.cpp 6215–6239). The sign lives in the 3-bit index. smallOffsets supplies the actual pixel delta.
If the call says the creature is attacking (MonsterAttacks returned true): set bit 0x80. If AttackingMirrorOK, randomly set or clear bit 0x40, with a special case for the Deth Knight that toggles the bit and queues sound 16 (CSBCode.cpp 6241–6279). If attacking mirrors are not allowed, the mirror bit is cleared.
If the call says it is not attacking: clear bit 0x80. If NonAttackingMirrorOK, randomly set or clear the mirror bit. Otherwise clear it (CSBCode.cpp 6281–6303).
The attack bitmap is turned on when a B-timer calls NextMonsterUpdateTime with the return value of MonsterAttacks (Monster.cpp 4002–4005). MonsterAttacks (Monster.cpp 966) returns false when there is no living character, and true after it has applied the attack. While the flag is still set, the next individual B-timer calls NextMonsterUpdateTime(..., false), which clears the attack flag, and adds attackTicks07 + STRandom0_3() - 1 to the timer (Monster.cpp 3828–3837).

The delay returned by NextMonsterUpdateTime is d.Time + additionalDelay + STRandomBool() (CSBCode.cpp 6314–6330). additionalDelay is word20 bits 8–11 after an attack, or bits 4–7 after a non-attack (MONSTERDESC::AdditionalDelayFromAttackToMove and AdditionalDelayFromMoveToAttack, CSB.h 2395–2396).

Movement timing is also the game clock:

The group timer is TT_MONSTER_Bgroup, and its byte 5 is 255 - timePerMove() (CSBCode.cpp 6359–6363). timePerMove() is movementTicks06 (CSB.h 2400).
A movement step adds max(1, timePerMove + STRandom0_3() - 1) (Monster.cpp 1413–1423).
A shift inside the tile adds max(1, timePerMove/2 + STRandomBool()) (Monster.cpp 3983–3986).
This is timer-driven, not frame-driven. The viewport draws whatever the status byte says at the moment DrawViewport runs. Nothing in DrawMonster advances time.

ClearAttacking_DeleteMovementTimers clears bit 0x80 on all four creatures and deletes their AI timers (Code11f52.cpp 676–685). That drops the attack bitmap immediately.

5. What one shared Unity renderer needs
One algorithm, fed by per-type records. No per-family renderer.

Per monster type, from MONSTERDESC and ITEM12:

Graphic base 446 + ITEM12_word0.
Which of side, back, and attack exist (word4 bits 3, 4, 5).
Byte width and pixel height for front/back, side, and attack.
Transparent color (uByte10 bits 0–3).
Placement-table index (uByte10 bits 4–7), selecting one of three 11×5 position tables.
Mirror permission for idle front (bit 2) and attack (bit 9), plus bits 7, 8, and 10.
Jitter ranges (bits 12–13 and 14–15).
Horizontal size 0, 1, or 2.
uByte11 recolor schemes for colors 9 and 10.
movementTicks06, attackTicks07, and the two word20 delay nibbles, if the simulation is ported with the renderer. They do not affect the blit except by changing the status byte.
Shared tables, from graphic 0x22e:

ITEM110 s6982[3], 11 cells by 5 (x,y) pairs. Y of 0 suppresses the slot.
smallOffsets[24], signed.
Byte6628[16] and Byte6612[16], values in multiples of 10.
specialColors[13][7], because level load overwrites map entries for colors 9 and 10.
Per creature at runtime, from ITEM16:

Position nibble, facing nibble, and the status byte.
Group count, and whether positions == 255.
Shared functions:

View selection in the order front / side / back / attack, including the source-index increments in Code51a4.cpp 600–716.
ShrinkBLT’s 16.16 point sample, with destination size (src * scale + scale/2) >> 5 for scale 32 (skip the shrink), 20, and 16.
Bitplane mirror.
Transparent blit into a 224×136 viewport, stride 112 bytes, bottom-anchored, centered on X, ±100 for a side cell.
The corner order words and the door’s two-pass order.
The size-0 / size-1 / size-2 placement rules, including “size 2 with a non-255 position byte is not drawn.”
Pseudocode for the blit, matching DrawMonster:


view = FRONT
if hasSide and relativeFacing is odd:
    view = SIDE
else if hasBack and relativeFacing == 0:
    view = BACK
else if attacking and hasAttack and relativeFacing != 0:
    view = ATTACK
bitmap = graphic[view]
if view == SIDE and relativeFacing == 1: mirror(bitmap)
if view == ATTACK and mirrorBit: mirror(bitmap)
if view == FRONT and mirrorAllowed and mirrorBit and distance == 0:
    bitmap = cachedMirror(bitmap)
if distance == 1: scale(bitmap, 20/32) through colorMapA
if distance == 2: scale(bitmap, 16/32) through colorMapB
# distance 0 stays full size and unmapped
x = place[table][cell][slot].x + smallOffsets[(distance << 3) + xJitter]
y = place[table][cell][slot].y + smallOffsets[yJitter]   # distance>>3 is 0
if cell is left:  x -= 100
if cell is right: x += 100
if place[table][cell][slot].y == 0: return
blit bottom-centered at (x, y), clip to 0..223 by 0..135,
     skip pixels whose color is the (possibly remapped) transparent index
The Screamer is type 6 (Objects.h 161, Statistics.cpp 22). It uses MonsterDescriptor[6] and Item6414[descriptor.uByte0]. Its bitmaps, flags, and placement group are whatever those file records contain. The same function draws a Worm (size 1), a Dragon (size 2), and a one-corner creature.

Unresolved
The numeric ITEM12 rows, the three position tables, smallOffsets, and the Screamer’s word4 flags were not read out of graphic 0x22e / 0x22f in this session. They are not in the C++ source. A decoded image of graphic 0x22e is at Research_Results\CSBwin_Ornaments\Tools\Graphic_0x22e_Raw.bin. Words in that image are Atari big-endian and are swapped by LE16 on load. ITEM12[0] begins 832 bytes into that image: 24+60+120+60 bytes of earlier words, plus 3*110 bytes of s6982.
b_102 = false at distance 2 is marked by the author as a guess (Code51a4.cpp 803–806).
Whether MONSTERDESC.uByte0 is always equal to the type index is a comment (CSB.h 2297–2300), not an assignment.
No DOS binary was compared. CSBwin additions that are explicitly not the original path: drawAsSize4, alternate graphics from CSBgraphics.dat, and the distance-2 b_102 initialization. The author says the size-2 “invisible if positions are not 255” result, and the fear-factor test elsewhere in the AI, follow the original machine code.
