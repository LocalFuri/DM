# Hook F2 — Fixed-point vertical stepping investigation

Date: 17 November 2026 (Europe/Berlin project date)

## Inputs

- `ViewportLayoutEditor(20261117-214835).zip` / `ViewportLayoutEditor.cs` (baseline 0x7FFF)
- `Hook_F2_Original_Source_32x28.png` (original source image)
- `Hook_F2_PixelTrace.csv` (known source→Medium palette mapping)
- `image(20261117-195032).png` (DOS PC reference, 224×136)
- `image(20261117-195900).png` (Unity 0x7FFF reference)
- `image(20261117-221337).png` (Unity 0x5700 experiment)

## Reconstructed current algorithm

The shared `GenerateDmScaledWallDecoration` computes:

```
stepX = ((32 << 10) / 19) << 6
stepY = ((28 << 10) / 18) << 6
sourceY = (stepY / 2 + roundBias + destinationY * stepY) >> 16
```

This truncates the fixed-point step to multiples of 64. The color-map conversion is deterministic: for all source color values found in the 32×28 image, the original CSV gives exactly one corresponding generated RGBA color.

The overlay comparison uses DOS X=103–121, Y=38–55, matching the earlier three-pixel alignment correction, with fixed horizontal sampling.

## Results: matches among opaque generated F2 pixels

| Vertical starting bias | Exact RGB matches | Generated opaque pixels |
|---|---:|---:|
| 0x7FFF | 154 | 193 |
| 0x5700 | 169 | 193 |
| Zero | 163 | 191 |

A systematic search of vertical biases from -65536 to +131072 in increments of 64 finds a maximum of 169 opaque exact-color matches for the existing stepY. Multiple biases give the same 18-row sampling sequence and therefore identical results; 0x5700 is not unique.

Exploring vertical step sizes from 90%–110% of the existing fixed-point step (increment 64) and varying start positions (increment 256) also found no score higher than 169 using the current horizontal source coordinates and palette. This search is not exhaustive over all possible algorithms or full source rectangle choices; it tests a family of uniform fixed-point resampling methods.

Caution: An opaque-only score is not an overall screen-pixel score. The earlier full-screen RGB study reported 213/342 for 0x7FFF versus 215/342 for 0x5700. The latter is only a marginal net gain, so neither offset is DOS-verified.

## Engineering conclusion

Changing the vertical initial accumulator alone is unlikely to produce a DOS-perfect Hook F2. Remaining mismatches may depend on horizontal sampling, the exact DOS source graphics and palette, image compositing, or a different source sampling rule. Do not add special-case source-row patches. Do not alter the approved Hook F3.

## Next step

Analyze horizontal fixed-point source selection (X start/step) jointly with Y and test the resulting complete RGB matches, including transparency/background; if no consistent rule is found, use DOS-code or reference-image extraction to establish which source selection convention the original renderer uses.

No Unity source file was changed by this study.

## Additional X/Y offset search

A follow-up systematic test enumerated 55 distinct horizontal sampling patterns and 52 vertical patterns resulting from bias offsets (from -65536 to +131072 in increments of 64) at the unchanged step sizes. All 2,860 combinations were compared against opaque DOS RGB pixels at the same 19×18 location. **The maximum remained 169 exact matches among 193 generated opaque pixels.** The best X sampling pattern is already produced by the existing 0x7FFF X bias; the Y sequence matching 0x5700 is one of the optimum sequences. Therefore simply adjusting *either* initial bias, even jointly, does not yield a complete match under this model.
