# CSBwin ShrinkBLT — exact verification (2026-11-15)

## Inputs
- Latest user extraction: `CSBwin_Extractions(3).zip` (the files under `CSBwin_Extractions/`)
- CSBwin source: `Graphics.cpp`, `ShrinkBLT()`; ornament call in `CSBCode.cpp`
- Source bitmap: `CSB_Ornament_G9_P12.planar`, 96 × 56, 4-bit planar
- Actual runtime color mapping and scaling dimensions from the two diagnostic `.txt` files

## Verified result

| Perspective | Bitmap source | Runtime output width × height | Storage width × height | Visible pixel differences | Raw planar byte differences |
| --- | --- | --- | --- | --- | --- |
| F2 / P08 | G9 P12, 96 × 56 | 64 × 37 | 64 × 37 | **0 / 2368** | **0 / 1184** |
| F3 / P03 | G9 P12, 96 × 56 | 47 × 27 | 48 × 27 | **0 / 1269** | **0 / 648** |

Both outputs reproduce the extracted .planar files *exactly, byte for byte*. F3's unused 48th storage column contains palette index 0 in every row.

## Fixed-point selection, from `Graphics.cpp`

For each axis, and integer source size `S`, destination size `D`:

```
step = ((S * 1024) // D) << 6
source_coordinate(i) = (step // 2 + 0x7fff + i * step) >> 16
```

The first sample starts near the center of the first scaled interval; the `0x7fff` term reflects the source routine's rounding. This is **not** conventional nearest-neighbor with an arbitrary offset.

| Axis | F2 fixed-point step | F3 fixed-point step |
| --- | ---: | ---: |
| X | 98304 | 133824 |
| Y | 99136 | 135872 |

After sampling each source pixel's original palette index, CSBwin applies the corresponding runtime mapping and divides its value by 10 to obtain the destination palette index. The four output bitplanes are then packed with 16 pixels per 8-byte unit, with F3 padded to 48 stored pixels per row.

## Reproduction

Keep `Verify_ShrinkBLT.py` beside the source `.planar` files in this folder and run:

```
python Verify_ShrinkBLT.py
```

Requires Python and NumPy. It prints visible pixel mismatch totals and raw byte equality for F2 and F3. The script does not modify input files.

## Limits

- This verifies G9 Manacles at two front perspectives using the extracted runtime values. It does **not** yet prove exact rendering of all ornaments or side-wall layouts.
- The F1 source was taken from the extracted CSBwin bitmap, not decoded from original game resource formats.
- Unity was not edited. Previously approved Manacles calibrations should remain unchanged.
- The older `CSBwin_Extractions(3).zip` also contained duplicate root-level captures from an earlier run; this folder retains the newer in-folder captures for an unambiguous reference set.
