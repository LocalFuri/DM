# Hook F2 — DOS screenshot comparison (19 November 2026)

## Ground truth
The two user screenshots are 224×136 at identical wall geometry. Orange means RGB (109,36,0) only; these counts and bounds are **orange pixels**, not the full opaque Hook.

* Unity: 140 orange pixels, bounds X=105–122, Y=43–52 (18×10).
* DOS: 101 orange pixels, bounds X=104–119, Y=41–49 (16×9).
* CSBwin V4 capture: full drawing rectangle X=102–123, Y=38–55 (22×18). This is not equivalent to orange bounds.

## Exploratory candidate
Nearest-neighbour reduction of the supplied 32×28 Hook F1 source to **20×18** with the Unity/CSBwin fixed-point step formula, but with rounding bias **0** (rather than 0x7FFF), mapped to top-left screen location (102,38), gives an orange mask of 101 pixels.

* Matched DOS orange pixels: 98 of 101
* Candidate-only orange pixels: 3, positions [(109, 43), (109, 44), (109, 45)]
* DOS-only orange pixels: 3, positions [(118, 42), (118, 48), (118, 49)]
* Orange IoU: 0.9423

The candidate is a **screenshot-derived hypothesis**, not a proven original DOS scaling rule. The source PNG used here has two orange/brown source colors; this test selects source pixels with red > 1.3× green, and tests only the orange silhouette. It does not prove that other colors, mask, or exact DOS palette match.

## Source inspection
The current user's installed Unity ZIP was supplied before the row-orientation fix. The separately delivered fix's source is inspected here: Hook F2 invokes `GenerateDmScaledWallDecoration` with width/height from `HookFrontWallOrnamentSet.f2`, medium palette and `sampleTopDown: true`. This function defaults to the `0x7FFF` sampling bias. Hook F3 is a separate far-palette path.

## Recommendation
Do not broadly replace CSBwin's method. Verify the complete DOS 16-color image/mask and whether the 20×18 size is specific to the PC edition; only then consider a narrowly scoped Hook F2 DOS-mode path. No Unity code is changed in this comparison.
