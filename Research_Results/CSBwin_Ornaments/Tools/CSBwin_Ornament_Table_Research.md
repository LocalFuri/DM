# CSBwin ornament placement and color table extraction

Decoded on 2026-11-16 19:32:19 CET, from user-supplied `graphics.dat` inside `graphics(3).zip`.

## Verification

- File index is Atari big-endian; graphics count 563. Graphic `0x22e` = decimal 558.
- Graphic `0x22e`: compressed 3976 bytes; decompressed **4720 bytes (0x1270)**, exactly matching CSBwin's expected `ReadTablesFromGraphicsFile` size.
- Source LZW implementation: `LZWExpand`/`LZWGetNextCodeword`/`TAG022eec` in `Graphics.cpp` (9 to 12-bit LZW with 0x90 repetition escape). Decoder uses LSB-first codes in blocks of 8 codes; output size verified.
- **104/104 placement records** recovered, each 6 bytes at offset 2410 + 6*(13*set+position).
- **16/16 MediumColorMapping entries** recovered from offset 3090; **16/16 FarColorMapping entries** from offset 3106. Table values are decimal (palette index × 10); a value of 100 represents palette index 10 (transparent on copy).
- **60/60 placement-set lookup values** recovered from offset 3146; **12/12 derived-graphic offsets** from 3222.
- **91/91** available per-ornament metadata records from `CSBwin_Extractions(7).zip` have destination rectangles matching at least one original placement-set record; unmatched records must be reviewed separately.
- All 104 descriptor rectangle heights match their height fields. All visible widths fit the recorded byte-stride capacity (4-bit planar; 8 bytes per 16 pixels).

## Key distinction

The six bytes of a placement record are **x1, x2, y1, y2, bitmap byte stride, bitmap pixel height**. `x2` and `y2` are inclusive. The last two bytes are *not* viewport width and height. The viewport is 224×136 for CSBwin, while the Unity Game View has a larger 320×200 canvas.

## Shared derived graphics

- F3 front positions 2, 3, 4 share a cached bitmap. F2 front positions 7, 8, 9 also share a cached bitmap.
- Across all 8 sets, all three positions within each group have **identical bitmap stride and bitmap height** (16/16 groups compatible at the descriptor-dimension level).
- Source rectangles often have different visible widths. Different positions crop different portions of the shared bitmap. This matters for exact reproduction.
- **Exception:** the original code contains a special-case width correction for set 7 positions 3/4 (`0x5e`/`0x5f`). The stored 48-byte width is deliberately overridden to 40 bytes when checked by code. Do **not** apply stored width blindly in that case; inspect and reproduce the runtime correction. The expected rectangle bytes match extracted data: set 7 pos 3 = `[74,149,25,75,48,51]` and pos 4 = `[139,223,25,75,48,51]`.
- Descriptor compatibility does not, by itself, prove every displayed pixel is correct. Live CSBwin screen comparison is still needed.

## Files

- `CSBwin_Ornament_Placement_Tables.csv`: all 104 six-byte records and interpretation.
- `CSBwin_Ornament_Color_Mappings.csv`: 32 palette remapping entries, exact raw values and decoded indices.
- `CSBwin_Ornament_Placement_Set_Lookup.csv`: global ornament-type index to placement set.
- `CSBwin_Ornament_Derived_Offsets.csv`: relative-position derived-cache slot offsets.
- `CSBwin_Ornament_Shared_Cache_Check.csv`: dimension consistency by set and shared group.
- `CSBwin_Ornament_Extraction_Crosscheck.json`: rectangle comparisons against captured runtime metadata.
- `Graphic_0x22e_Raw.bin`: full 4720-byte decompressed original table graphic (for reproducibility).
- `decode_graphics_22e.py`: decoder, for rerunning from the original `graphics.dat` ZIP.

## Integration decision

Use the original CSBwin side/front source artwork, scaled/mirrored/color-mapped/cropped **in Unity code**. These CSVs are canonical reference data, not a request to import a different PNG for every distance. Preserve approved ViewEdit calibration and do not modify Unity or the CSBwin executable at this stage.
