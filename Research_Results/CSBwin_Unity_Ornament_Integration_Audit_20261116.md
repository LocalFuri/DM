# Dungeon Master — CSBwin ornament integration audit

Date: 2026-11-16 (Europe/Berlin)

## Scope and source evidence

Read-only comparison of the user-supplied `ViewportLayoutEditor.cs` (from `ViewportLayoutEditor(20261116-184124).zip`) and recovered `graphics.dat` graphic `0x22e` tables (from `CSBwin_Ornament_Tables_20261116.zip`). No source files, calibration JSON, scenes or assets were changed. Source-code line references refer to the uploaded snapshot.

## Confirmed technical findings

- CSBwin's eight placement sets each have 13 wall-relative records: 104 original records in total. Each record has inclusive viewport x1,x2,y1,y2 and *separate* 4bpp byte stride and pixel height. Unity must not confuse the source stride with the visible width.
- The table's 16 Medium and 16 Far palette remaps are already represented by `WallOrnamentMediumColorMap` and `WallOrnamentFarColorMap` at lines 37005–37019. Their decoded target palette indices match the recovered values; do not duplicate these maps before determining how to share a single source of truth.
- The current Unity code includes `GenerateDmScaledWallDecoration` (37328–37420) and `BuildWallOrnamentPerspectiveTexture` (39338 onward), which sample/scale differently. A common CSBwin comparison path must not silently replace either existing call path.
- `GenerateDmScaledWallDecoration` uses an F3-specific rounding bias of zero (37358–37361), and calls `MatchFarTopRowBlackToNeighbors` (37406–37407); those behaviors differ from the unmodified CSBwin scaling and palette-copy rules described in the source research. Preserve current behavior until reference comparisons prove a replacement is safe.
- Hook currently has specific rendering methods. `GetHookSide2Texture` (40264–40298) scales a cropped image using center-sampled nearest-neighbor, while `GetHookSide3Texture` (40300–40320) imports an existing 3×5 file. `GetHookFrontTexture` (40183–40222) searches for alternative assets if its canonical path fails, violating the project's no-silent-fallback rule. Do **not** remove any of these in the first change.
- `TryGet...` projection handling (around 27138–27192) has ornament-specific depth/position decisions. `BlitOrnamentPieceIntoPreview` (43397–43468) applies accepted X/Y/Mirror calibration and preview overrides, subject to special Manacles F2/F3 behavior. Protect this path.

## High-priority safety issue — calibration reset on another PC

At `OnEnable` line 2836, `ResetFloorWallOrnamentCalibrationsOnce()` is called. Lines 24202–24270 show that its one-time guard is an `EditorPrefs` key (`DM.ViewEdit.FloorWallOrnamentsReset20261109.V1`). Because EditorPrefs are machine-local rather than Git-tracked, the guard could be absent on a new PC. In that case the routine can remove non-champion entries from `Assets/Editor/OrnamentCalibration.json` and delete saved EditorPrefs calibration keys. Do not open or execute that Unity editor version on the new machine before safeguarding the calibration file and resolving this risk. This is a source-derived risk, not a claim that any reset has occurred.

## Concrete placement-table example: set 0 (Hook-sized profile in captured data)

| Position | CSBwin slot | x1 | y1 | Visible W×H | Byte stride |
|---|---|---:|---:|---:|---:|
| 0 | S3 Left | 80 | 41 | 4×5 | 8 |
| 1 | S3 Right | 140 | 41 | 4×5 | 8 |
| 3 | F3 Center | 107 | 39 | 14×12 | 8 |
| 5 | S2 Left | 67 | 40 | 11×10 | 8 |
| 6 | S2 Right | 146 | 40 | 11×10 | 8 |
| 8 | F2 Center | 102 | 38 | 22×18 | 16 |
| 10 | S1 Left | 48 | 38 | 16×19 | 8 |
| 11 | S1 Right | 160 | 38 | 16×19 | 8 |
| 12 | F1 Center | 96 | 36 | 32×28 | 16 |

These are CSBwin's 224×136 viewport-relative coordinates, **not necessarily Unity's 320×200 destination coordinates**. Do not assign them directly to Unity's existing X/Y defaults without establishing the coordinate transformation and checking manual calibration overrides.

## Recommended minimal change sequence

1. **Protect calibrations separately:** remove or safely disable the destructive migration trigger in `ViewportLayoutEditor.cs`, in one deliberately scoped change; do not alter the calibration store contents. Verify against a backup and make sure this does not revive other stale calibration state. This should precede testing on the new PC.
2. **Read-only table comparison:** add a no-side-effect validation function or a standalone offline script to parse the 104 original placement rows and compare with current Unity constants. No runtime ornament drawing change, no `OnEnable` startup hookup, no JSON writes. An offline script outside Unity is safer initially.
3. **Pure code-generation path:** when ready, implement a separate CSBwin-like variant using one source side image and one front image, original fixed-point scaling, palette mapping, mirroring and source-x clipping, with no distance-specific PNG assets.
4. Compare Hook G2 output against archived *post-blit* references, then Ring G4. Be careful that the existing extracted PNGs were reconstructed from intermediate bitmap exports and were not themselves verified as final live viewport images.
5. Switch ordinary ornaments over only after matching results and confirming V17 visibility and approved manual calibration behavior. Do not touch verified Manacles, Torch Holder or unrelated ornaments until explicitly needed.

## Current implementation decision

No Unity code was changed in this audit. The first safe code modification should be limited to `ViewportLayoutEditor.cs` and the `OnEnable` calibration migration risk, **only after a backup/checkpoint has been confirmed**, or wait for Cursor's restricted code-inspection report. Do not attempt a wholesale ornament renderer migration now.
