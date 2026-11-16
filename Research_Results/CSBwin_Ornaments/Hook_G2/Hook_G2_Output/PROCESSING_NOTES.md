# CSBwin Hook G2 — planar-to-visible-ornament reconstruction

Input: `CSBwin_Extractions(7).zip`, ordinal G2.

## Results

- 11 captures reconstructed for graphic IDs **129 (side)** and **130 (front)**.
- P02 and P07 were deliberately excluded: both refer to graphic ID **188**, not 129 or 130. The same ordinal can identify another ornament in a different level context; do not interpret them as Hook.
- Source bitmap decoded as 4 interleaved big-endian 16-bit planes for every 16 horizontal pixels.
- Crop from (`source_offset_x`, `source_offset_y`) with the recorded inclusive destination rectangle width/height.
- Palette index **10** -> alpha 0. All other indices, including **0**, remain alpha 255.
- Images are saved at native pixel dimensions; preview alone is enlarged with nearest-neighbor scaling.
- The resulting transparent PNG is an isolated ornament overlay intended to be composited over existing stone walls, not a fully rendered viewport screenshot.

## Source offsets

P01: 12 pixels, P06: 5 pixels. Other accepted Hook records: 0 pixels.
These offsets are already in the capture metadata and are **not recomputed**.
The intermediate export for S1 Right is already mirrored by CSBwin, so no additional mirroring is applied.

## Outputs

`viewport/`: cropped overlay with recorded destination dimensions.
`tight/`: transparent-bounds crop; apply `tight_screen_x1/y1` from metadata for its viewport placement.
`metadata.json`, `metadata.csv`: positions, offsets, palette and pixel dimensions; exclusions and warnings.
`preview.png`: magnified inspection sheet.

## Scope and remaining verification

This post-processor reproduces the ornament **pre-blit source selection** described in Cursor's source audit, assuming each exported raw buffer and its metadata are self-consistent. It does **not** prove that a shared F2/F3 derived bitmap cache was correctly populated in every capture, and does **not** reconstruct the complete viewport or wall occlusion. Pixel-exact agreement with live CSBwin should be validated with reference screenshots or a final-viewport extraction.

The script accepts other ordinals and source graphic IDs, but do not assign an identity based on ordinal alone. For example, Ring G4 should be processed only after verifying its corresponding IDs and runtime palette/level context.

## Reproduce

```powershell
python csbwin_ornament_postprocess.py 'CSBwin_Extractions(7).zip' --ordinal 2 --graphic-ids 129,130 --name Hook --out Hook_G2_Output
```

Requires Python 3.9+ and Pillow. The script reads the ZIP directly and does not modify CSBwin or Unity.
