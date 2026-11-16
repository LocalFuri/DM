# 11 — CSBwin Wall Ornament Rendering Research

Research checkpoint: 2026-11-16 18:50:31 CET (Europe/Berlin)

## Purpose and preservation

This document consolidates the CSBwin ornament investigation and links it to the research files archived in the Unity Git repository. It is a research record, not a statement that Unity rendering has been changed or that every exported image has been validated pixel-for-pixel against live CSBwin.

Repository root: `C:\Unity\DM\`
Permanent research: `C:\Unity\DM\Research_Results\`
Ornament archive: `C:\Unity\DM\Research_Results\CSBwin_Ornaments\`
Temporary CSBwin output: `C:\Unity\DM\CSBwin_Extractions\` (may be overwritten by CSBwin; not archival)
Progress log: `C:\Unity\DM\Assets\Editor\Progression.json`

The first ornament archive was committed on 2026-11-16, branch `main`, commit `66908` (abbreviated as shown by Git), message `Research: Archive CSBwin ornament extraction and reconstruction`. This *new document* must be added and committed separately after copying. The existing working tree still contained unrelated deleted/modified Unity files and an untracked `MigrationBackup/` folder; do not stage them as part of this research documentation commit.

### Archived directories

- `CSBwin_Ornaments/Original_Extractions/` — copy of a CSBwin extraction session (retain raw `.planar`, `.palette.txt`, metadata).
- `CSBwin_Ornaments/Hook_G2/` — Hook reconstruction package and outputs.
- `CSBwin_Ornaments/Ring_G4/` — Ring G4 package and outputs.
- `CSBwin_Ornaments/Tools/` — reusable `csbwin_ornament_postprocess.py`.
- `CSBwin_Ornaments/Source_Snapshots/` — reserved for manually verified CSBwin source snapshots; do not assume populated.

## Decisions and status

- Keep the original stone walls in CSBwin; the temporary grey-wall test is disabled.
- Keyboard layout: arrows forward/backward/strafe left/strafe right; Delete turns left; Page Down turns right; Escape uses original waiting/freeze behavior.
- Prefer direct CSBwin extraction and engine-derived positions/palette logic over manual ornament scale guesses.
- Adopt the CSBwin **Ring G4** appearance even if the DOS PC Ring differs.
- Hook G2 reconstructed from raw ordinal 2 captures. A later run provided 11 internally consistent Hook captures with graphic IDs 129/130; ordinal-2 P02 and P07 reportedly used graphic ID 188 and were excluded from the Hook set. Verify identity against level lookup before reusing cross-level ordinals.
- The delivered Hook postprocessor applies recorded offsets, destination cropping, palette-index-10 transparency, and index-0 opacity. It is a **pre-blit reconstruction**, not a verified final-composited viewport pixel capture.
- Existing Unity ornament placements/calibrations and approved checks remain untouched; do not re-test without a discrepancy.

## Next technical work

1. Obtain Cursor's separate placement-table investigation of decompressed graphic `0x22e` (if available), with the 8×13 placement descriptors and far/medium remap bytes.
2. Validate Hook G2 and Ring G4 against actual CSBwin viewport pixels, focusing on shared F2/F3 scaled caches, special source offsets, clipping, and light-dependent palette.
3. Update the reusable postprocessor only when discrepancies are demonstrated.
4. Import a verified asset into Unity without overwriting approved calibration entries.
5. Keep this document, original raw extractions, metadata, tooling and Progression.json aligned; commit research at meaningful checkpoints.

---

## Cursor's CSBwin source-code findings (supplied 2026-11-16)

The following section reproduces Cursor's source-code research supplied on 2026-11-16. The findings are based on the CSBwin source, not on a verified live-pixel comparison. The placement rectangles and color maps are bytes inside `graphics.dat` graphic `0x22e`; that file was not decompressed, so those numbers are located but not listed.

## 1. Rendering functions

Wall ornaments are drawn by `DrawWallDecoration` in `CSBCode.cpp` (line 3263), called from the viewport interpreter in `Viewport.cpp`.

| **Screen name** | **Engine position** | **Cell and wall**       | **Call site**                              |
| :-------------- | :------------------ | :---------------------- | :----------------------------------------- |
| S3 left         | 0 `RWF3L1Right`     | F3L1, decoration slot 0 | `StdDrawF3L1Stone`, `Viewport.cpp` 958–960 |
| S3 right        | 1 `RWF3R1Left`      | F3R1, slot 2            | `StdDrawF3R1Stone`, 966–968                |
| F3 left slice   | 2 `RWF3L1Front`     | F3L1, slot 1            | same                                       |
| F3              | 3 `RWF3Front`       | F3, slot 1              | `StdDrawF3Stone`, 962–964                  |
| F3 right slice  | 4 `RWF3R1Front`     | F3R1, slot 1            | 966–968                                    |
| S2 left         | 5 `RWF2L1Right`     | F2L1, slot 0            | `StdDrawF2L1Stone`, 970–972                |
| S2 right        | 6 `RWF2R1Left`      | F2R1, slot 2            | `StdDrawF2R1Stone`, 978–980                |
| F2 left slice   | 7 `RWF2L1Front`     | F2L1, slot 1            | 970–972                                    |
| F2              | 8 `RWF2Front`       | F2, slot 1              | `StdDrawF2Stone`, 974–976                  |
| F2 right slice  | 9 `RWF2R1Front`     | F2R1, slot 1            | 978–980                                    |
| S1 left         | 10 `RWF1L1Right`    | F1L1, slot 0            | `StdDrawF1L1Stone`, 982–984                |
| S1 right        | 11 `RWF1R1Left`     | F1R1, slot 2            | `StdDrawF1R1Stone`, 1060–1062              |
| F1              | 12 `RWF1Front`      | F1, slot 1              | `StdDrawF1Stone`, 986–1020                 |

The names S1/S2/S3 do not appear in the engine. The mapping above is the side wall that faces the party: the right wall of the left-hand cell, and the left wall of the right-hand cell. F3L2 and F3R2 draw stone only and have no ornament call. There is no F1 side-cell front ornament.

Cell order in `DrawViewport` (`Viewport.cpp` 6603–6773) is F3L1, F3R1, F3, F2L1, F2R1, F2, F1L1, F1R1, F1. Each stone script blits the wall with `BltShapeToViewport` (`CSBCode.cpp` 3044), which uses transparency index 10, then draws text, then the ornament.

Supporting functions:

- `ShrinkBLT`, `Graphics.cpp` 447. Runtime scale and color map for positions 0–9.
- `MakeMirror` / `Mirror`, `CSBCode.cpp` 3034 and 4943. Horizontal mirror.
- `TAG0088b2`, `Graphics.cpp` 702. Copies into the 224×136 viewport (`pViewportBMP` is 15232 bytes = 112×136, `CSBCode.cpp` 10830). Transparent index is 10.
- `GetBasicGraphicAddress` / `ExpandGraphic`, `Graphics.cpp` 2653 and `CSBCode.cpp` 11664.
- `ReadGraphicsForLevel`, `Code390e.cpp` 197–257. Binds each level ornament to two graphic numbers and one of eight placement sets.

## 2. Graphic selection

The cell stores a 1-based ordinal in `decorations[0|1|2]` (right, front, left). Zero means no ornament. `SetPartyLevel` (`CSBCode.cpp` 6410–6415) copies the level’s decoration list into `WallDecorationTOC`.

For a normal ordinal:

- `graphicIndex = ordinal - 1`
- global type = `WallDecorationTOC[graphicIndex]`
- side graphic number = `121 + 2 * type`
- front graphic number = side + 1
- placement set = `Byte4100[type]`, an index 0–7 into `wallDecorationPosition[8][13]`

`WallDecorationsForLevel[index][0]` holds the side graphic number. The comment at `CSBCode.cpp` 3373 says “front view”; the increment below shows that is the side view. Front positions then do `graphicID++` (`3560` for F2/F3, `3454` for F1).

If bit `0x10000` is set, the low 16 bits are a `CSBgraphics.dat` id. `ReadWallDecoration` (`CSBCode.cpp` 3190) loads 13 placement records plus the side and front bitmaps. Positions where `position % 5 > 1` (2, 3, 4, 7, 8, 9, 12) use the front bitmap.

Each `DECORATION_DESC` (`Data.h` 211–216) is six bytes: inclusive viewport rectangle `x1,x2,y1,y2`, then byte width and pixel height. The eight sets and the color maps are not constants in the C++ sources. `ReadTablesFromGraphicsFile` (`CSBCode.cpp` 10833–10834) LZW-expands graphic `0x22e` (0x1270 bytes) onto `d.Word7246`. With `#pragma pack(1)`, the Atari addresses match the struct:

- `wallDecorationPosition` is `Byte4836`, 2410 bytes into graphic `0x22e`, 8×13×6 = 624 bytes
- `MediumColorMapping` is `Byte4156`, offset 3090
- `FarColorMapping` is `Byte4140`, offset 3106
- `Byte4100` is offset 3146, 60 bytes
- `WallDecorationDerivedGraphicOffset` is `Byte4024`, offset 3222

The source comment at `CSBCode.cpp` 3816 says that offset table is `0,0,1,1,1,2,2,3,3,3,4,4` for positions 0–11. Derived slots are:

- 0: F3 side (positions 0 and 1)
- 1: F3 front (positions 2, 3, and 4)
- 2: F2 side (positions 5 and 6)
- 3: F2 front (positions 7, 8, and 9)

`ReadGraphicsForLevel` sizes those four buffers from positions 0, 2, 5, and 7 (`Code390e.cpp` 233–256).

## 3. Scaling

F1 (positions 10, 11, 12) is not scaled. It blits the expanded graphic at 1:1. The stored graphic is already the near-wall size.

F2 and F3 are runtime-scaled from those same two bitmaps. There are no separate pre-scaled ornament graphics. `ShrinkBLT` (`Graphics.cpp` 447–649) is nearest-neighbor in 16.16 fixed point. The sample position starts at half a step plus `0x7fff`, then advances by `(sourceSize << 16) / destinationSize` on each axis. Each sample is remapped before it is written.

Source size (`CSBCode.cpp` 3670–3691): the unexpanded header’s pixel width and height, width rounded up to a multiple of 16 and converted to bytes, then clamped to the descriptor width and height. Left side uses descriptor 10, right side uses descriptor 11, front uses descriptor 12.

Destination size (`3733–3741`): pixel width is `rect.x2 - rect.x1 + 1`, height is `pWDP->height`. Positions 7 and 9 are the exception: their scale width comes from position 8, the full F2 front rectangle (`3565–3576`). They are then cropped while copying.

Color maps, verified from the `ShrinkBLT` switch: the table value is `paletteIndex * 10`, and 100 means index 10. Positions 0–4 use `FarColorMapping`. Positions 5–9 use `MediumColorMapping`. F1 is not remapped. The 16 table bytes themselves are in graphic `0x22e`, not in the source.

Positions 2, 3, and 4 share one cached bitmap, and 7, 8, and 9 share another. The first cell that needs it creates it (`AllocateDerivedGraphic` returns false only the first time, `CSBCode.cpp` 12069). Because F3L1 is drawn before F3, an ornament on the F3 left slice creates the shared F3 front bitmap at that call’s destination size. The center and right slice reuse it. The same happens for F2: the left slice creates the shared bitmap at the center width and the left slice’s height.

## 4. Clipping, transparency, and black areas

`TAG0088b2` copies `x2 - x1 + 1` by `y2 - y1` pixels, inclusive, starting at `srcOffsetX` and source Y 0. It does not clip against the viewport edges itself. The destination rectangle is the visible window. The viewport is 224×136.

Horizontal crop:

- Positions 1 and 6, after the mirror: `srcOffsetX = 15 - ((rect.x2 - rect.x1) & 15)` (`CSBCode.cpp` 3760–3766). This drops the padding the mirror moved to the left of a width that is not a multiple of 16.
- Position 7: `srcOffsetX = fullF2Width - visibleWidth` (`3772–3780`), so the F2 left slice shows the right part of the full scaled front graphic.
- Position 9 keeps `srcOffsetX = 0`, so the F2 right slice shows the left part.
- Every other ornament position uses `srcOffsetX = 0`. A narrower destination rectangle cuts the right side of the bitmap.

Vertical crop is only the rectangle height, taken from the top of the bitmap. There is no source Y offset.

Transparency: the ornament call passes 10 (`CSBCode.cpp` 4054). `TAG0088b2` skips only that index (`Graphics.cpp` 863–886). Index 0 is copied. It is black only when `Palette552[CurrentPalette].color[0]` is black. The active palette changes with light level; the engine does not special-case index 0 as black.

`ExpandGraphic` (`CSBCode.cpp` 11717) clears the first 8 bytes, then expands the Atari RLE stream. A stored run of color 0 stays color 0. `ShrinkBLT` also leaves unwritten bits in the final 16-pixel group at 0, because the plane registers start at 0 and every row is flushed as 8 bytes.

So a black rectangle can be any of these, and the source distinguishes them:

- Index 0 inside the graphic. That is bitmap content, and it is opaque on the wall.
- Index 0 in the 16-pixel rounding gutter. That is scaling padding, not art.
- Bytes past the real graphic because `DECORATION_DESC.width` is too large. One case is hard-coded: placement index `0x5e` (set 7, position 3) and `0x5f` (set 7, position 4) say 48 bytes while the graphic is 40. The runtime forces 40 (`CSBCode.cpp` 3826–3858). Any similar error that does not match those two rectangles is not corrected.
- The export reading a shared cache with the current position’s stride and height when an earlier position created a smaller bitmap.

## 5. Mirroring

Left and right do not have different source graphics. Both use the side graphic.

- S1/S2/S3 left (positions 10, 5, 0) are unmirrored.
- S3 right and S2 right (1 and 6) mirror the scaled bitmap, then apply the 16-pixel offset above.
- S1 right (11) mirrors the unscaled side bitmap (`CSBCode.cpp` 3492–3500).
- Front graphics are never mirrored.

`Mirror` (`CSBCode.cpp` 4943) reverses each row with `data008734` (`4667`), a 256-byte bit-reversal table. Width is in bytes.

## 6. What the extractor saves

The exporter is the `#if CSB_ORNAMENT_RAW_EXPORT` block in `DrawWallDecoration` (`CSBCode.cpp` 3890–4043). It is compiled on by default. It writes `pFinalBitmap` before `TAG0088b2`, once per ordinal and position. The file is `srcByteWidth * height` planar bytes, plus a text file of rectangle, offset, and stride 112, plus the current 16-color palette. It does not change the draw.

It does not save final viewport pixels.

- F1 files are the full expanded side or front graphic, mirrored for position 11, at the descriptor stride. They are not cropped to `x1,y1,x2,y2`.
- F2/F3 files are the scaled cache bitmap after the color map, including 16-pixel black padding, and not cropped by `srcOffsetX`.
- Index 10 remains in the file. The screen skips it, so the wall shows through. The extracted picture shows the palette color of index 10 instead.
- Index 0 remains opaque black padding or art.
- A shared F3 or F2 cache can be exported at a stride and height that belong to a different perspective than the one that created it.

The extractor therefore cannot, by itself, produce the exact F1/F2/F3/S1/S2/S3 pixels that reach the screen.

## What is still required for an exact reconstruction

The algorithms and the file locations are in the source. These values are not:

- The 624 placement bytes and both 16-byte color maps in graphic `0x22e` at the offsets above. Those give every destination rectangle, source clamp, and F2/F3 recoloring.
- The expanded side and front bitmaps, graphic numbers `121 + 2*type` and `+1`, including header width and height.
- A crop to the destination rectangle and `srcOffsetX`, then a composite that skips index 10.
- Confirmation, from those bytes, that shared F3 positions 2/3/4 and shared F2 positions 7/8/9 have compatible width and height. The control flow is verified; whether the data makes the shared cache safe is not.
