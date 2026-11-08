# Original DOS AVI vs Unity Apple throw — timing comparison (8 Nov 2026)

## Method
- DOS source: `apple.avi`, 320×200, 276 frames, nominal frame rate 70.086303 FPS.
- **Second** visible DOS throw examined; red Apple pixels in the dungeon viewport (region x=80..187, y=48..121), identified from frame-to-frame appearance and bounding-box shifts. First Apple visible at DOS frame 136, and absent starting frame 231. Avoid comparing the earlier throw around frames 81–123 to this one.
- Unity source: `Apple_20261108_224952.zip`, `UnityCaptureTiming.csv`; unique image-change frames 0, 17, 27, 36, 46, 56. Timings use actual recorder samples, not interpolated 70 FPS.
- DOS first-red-pixel detection and Unity first captured frame may not coincide with audio onset to sub-frame precision.

## Flight-time measurements

| Stage | DOS frame range | DOS relative start | DOS frame hold | Unity relative start | Unity frame hold |
|---|---|---:|---:|---:|---:|
| 1 | 136–156 | 0.0000 s | 299.6 ms | 0.0000 s | 497.3 ms |
| 2 | 157–168 | 0.2996 s | 171.2 ms | 0.4973 s | 246.3 ms |
| 3 | 169–184 | 0.4708 s | 228.3 ms | 0.7436 s | 233.5 ms |
| 4 | 185–200 | 0.6991 s | 228.3 ms | 0.9771 s | 235.5 ms |
| 5 | 201–214 | 0.9274 s | 199.8 ms | 1.2125 s | 248.5 ms |
| 6 | 215–230 | 1.1272 s | 228.3 ms | 1.4610 s | not recorded ms |

**DOS:** Apple appears ~1.941 s after AVI start and disappears ~3.296 s after AVI start. Total visible duration **~1.355 s**. At DOS frame 157 an atypical partial/overlap appearance occurs; frame separation should be confirmed visually before treating every detected red-bounding-box transition as one animation stage.

**Unity:** Apple visible for ~1.461 s across the captured sequence. Its first image is held for ~497 ms, versus ~300 ms between the first and next red-region shape changes in DOS (possible stage ambiguity). The later Unity image holds last about 234–247 ms; DOS transitions later in the throw are around 200–228 ms.

## What we can and cannot conclude
- **Confirmed:** The first Unity hold is longer than the first DOS detected hold by ~197 ms; the full visible span is longer by ~106 ms. These differences reflect the captured samples, which have different time resolutions.
- **Not yet proved:** strict stage-for-stage sprite equivalence; DOS stage 157 is anomalous and there appear to be more distinct red-image segments than Unity's five visible flight stages. Do not force an automatic one-to-one mapping without visual verification.
- **Audio:** Earlier rough onset estimate was around AVI 1.95 s, near video frame 136, but no measured Unity audio capture exists. Exact sound/frame synchronization remains unverified.
- **Geometry:** Right-side Unity release appears consistent horizontally with the compared DOS throw; preserve current throwing mechanics until stage identity and timing are checked.

## Recommended next step
Keep the right-side release and physics unchanged. Inspect DOS frames **136, 156, 157, 169, 185, 201, 215, 230, 231** in the existing comparison viewer to decide whether frame 157 is a separate true stage or a partial display/artifact. Once that is resolved, adjust only stage-0 timing (if needed) and repeat one automatic capture for validation. Do not adjust audio blindly.
