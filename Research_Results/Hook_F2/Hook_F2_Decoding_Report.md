# Hook F2 — verified Atari-planar reconstruction

Source: user-uploaded `Hook_F2(3).zip` (the seven November 18 CSBwin diagnostic files).
Analysis: 2026-11-19 18:26:26 Europe/Berlin.

## Verified pixel encoding
Each scanline is a sequence of 16-pixel groups containing eight bytes. Each group stores four consecutive 16-bit, big-endian bitplane words, from plane 0 through plane 3. For each pixel, the most-significant bit is the leftmost pixel; palette index = sum of the four plane bits weighted 1,2,4,8. The layout is consistent with CSBwin's documented Atari graphics representation. This decoding reproduces the prior recorded 220 changed pixels, all within the declared drawing rectangle.

## Verified geometry
- Graphic ID 130, ordinal 2, relative position 8.
- Final-blit rect inclusive: X=102..123, Y=38..55, width 22, height 18.
- Bitmap source: 288 bytes = 18 rows x 16 bytes = 32 nominal pixels/row.
- Final-blit uses the leftmost 22 source columns, positioned at X=102..123.
- Pixels with source palette index 10: transparent; they preserve previous viewport value.
- Nontransparent source pixels in the destination 22x18 area: **226**.
- Pixels actually changed between viewport-before and viewport-after: **220**.
- Nontransparent pixels drawn over an identical existing color: **6**.
- Changed-pixel bounding box: **X=104..121, Y=38..55** (18x18).
- All changes are located inside final-blit rectangle. No changed pixel lies outside the nontransparent source mask.
- The remaining 170 pixels in the 22x18 destination are transparent source pixels.
- Changed byte count in packed viewport files: **120** (different metric from pixel count).

## Interpretation / limitation
This establishes CSBwin F2's actual drawn pixel pattern and position, but **does not yet establish** that the DOS engine draws the same mask or that Unity uses the same palette/scaling. A DOS reference and Unity-rendered data or source are still needed for a pixel-exact three-way comparison. No claim of a Unity code root cause is justified by these captures alone.

Do not alter approved Hook F3, Manacles, or V17 geometry. The Unity 22x18 experiment remains unapproved.

## Files
- `Hook_F2_DecodedPixels.csv`: every destination pixel, decoded source/before/after indices, transparency flag and change flag.
- `Hook_F2_PixelMasks.png`: monochrome schematic of the source opaque mask, actual changed pixels, and six overwritten-same-color pixels. **Not** an actual colored screenshot.
