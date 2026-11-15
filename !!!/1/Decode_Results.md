# CSBwin ornament exports — decoding results (2026-11-15)

Source: CSBwin_Extractions(1).zip (original files left unchanged).

CSBwin planar 4bpp decoded as successive 16-pixel groups (8 bytes each), with four big-endian 16-bit bitplanes and most-significant bit as the leftmost pixel. Dimensions recovered from `source_stride_bytes * 2` and `source_height`. Runtime RGB8 palette values were read from each palette.txt file. Palette index 10 is fully transparent; palette index 0 is opaque black.

- G10 P11, graphic 207: 16 x 35; corresponding palette present; destination rectangle (167,31)-(182,65). Appears as narrow decorative/mirror frame source.
- G9 P03, graphic 194: 48 x 27; palette present; destination rectangle (88,37)-(134,63). Manacles.
- G9 P08, graphic 194: 64 x 37; palette present; destination rectangle (80,37)-(143,73). Manacles.
- G9 P12, graphic 194: 96 x 56; palette MISSING in source ZIP; G9 P08 runtime palette used PROVISIONALLY for visual inspection; destination rectangle (64,36)-(159,91). Manacles. Do not approve its RGB colors before obtaining correct palette.

PNG files are transparent at palette index 10. The separately named `_5x_preview.png` files and `ALL_Ornaments_Comparison.png` show transparency against bright magenta solely for visibility; magenta is not encoded into the source PNG. No Unity source code or approved calibration was changed. Pixel-level matching against DOS/CSBwin reference screens remains outstanding.
