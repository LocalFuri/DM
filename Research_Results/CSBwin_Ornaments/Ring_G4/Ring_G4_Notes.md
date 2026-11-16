# CSBwin Ring G4 reconstruction

Source: user-provided `CSBwin_Extractions(7).zip`. Nine captured perspectives (P00, P01, P03, P05, P06, P08, P10, P11, P12).

## Decode and crop

- Decode the 4-bit planar bitmap from the recorded `source_stride_bytes` and `source_height`.
- Convert each palette index using its captured runtime RGB palette. Palette index **10** is fully transparent; **0** is **opaque black**.
- Use `source_offset_x` and `source_offset_y` **before** cropping to the recorded destination rectangle width and height. This matters for **P01 (X=12)** and **P06 (X=5)**. Cropping these from X=0 would be incorrect.
- `Viewport/` preserves the full rectangle sent to the CSBwin viewport; `Tight/` removes fully transparent outer rows/columns. No resampling, interpolation, manual color substitution or mirroring is applied.
- To place a `Tight/` image, use `tight_x`, `tight_y` in `Ring_G4_Metadata.json`. These coordinates refer to the original 320 × 200 viewport with a top-left origin.

## Known wall perspectives

- P00 S3 Left; P01 S3 Right; P03 F3 Center; P05 S2 Left; P06 S2 Right; P08 F2 Center; P10 S1 Left; P11 S1 Right; P12 F1 Center.

This is a *replacement graphic candidate*, not an automatic modification of the Unity project. Existing Ring placements, calibration and renderer have not been edited. Verify Unity's existing slot conventions and palette against CSBwin before installing.

**Caveat:** These are reconstructed source pixels within the recorded blit rectangle. Final viewport compositing and occlusion may require further checks against Cursor's rendering research.
