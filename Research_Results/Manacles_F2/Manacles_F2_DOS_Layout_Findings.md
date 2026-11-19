# Manacles F2 — DOS layout investigation

Date (Europe/Berlin): 2026-11-19 21:53:45 CET

## Evidence / verified

- Uploaded genuine DOS `GRAPHICS(20261119-201903).DAT`: 363,417 bytes, indexed 713 resources (prior extraction).
- DOS native Manacles F1 resource 332: 92x52 pixels; research image `Manacles_DOS_Front_92x52.png`.
- Resource index (previously established): entry 696 starts at byte 293,487 (0x47a6f) and has length 9,160 bytes.
- The attached raw resource is a byte-for-byte slice of those offsets; prefix `0d fc 17 00 01 00 11 00 90 01 b4 01`.
- ScummVM DOS-port developer's 2026 notes identify graphic 696 as a DOS layout-tree source for walls / view regions. Its record format and specific Manacles wall ornament mapping have not yet been verified here.
- Latest C# `GetManaclesFrontF2Rect` reads `GetOriginalWallDecorationRect(1,8)` (CSBwin-derived), yielding viewport X=80, top Y=37, 64x37 image.
- Original DOS screenshot brown pixels: 237. Current Unity brown pixels: 251. Source-derived candidate 60x38 at X=82, Y=39, bias=0 gives 208 matching brown, 10 extra and 29 missing (screenshot fit only, not a DOS-derived rule).

## Key limitation

Resource 696 is confirmed present and extracted, but we have not decoded its record structure or established whether its entries provide an ornament's F2 output dimensions directly. It must not be treated as proof that the 60x38 candidate is correct. The older CSBwin renderer's Group 1 position 8 is not authoritative for DOS.

## Recommended next technical step

Trace the latest ScummVM DOS implementation's `getLayoutRecord`, `getZoneBox` and wall-ornament draw function through the actual DOS graphics structure. Only then map Manacles F2 layout and sampling. Do not modify Unity or any approved ornament renderer before the result is source-derived.

## Sources

- User's DAT binary and screenshots (same conversation)
- ScummVM DOS-port blog (22 August 2026): https://blogs.scummvm.org/mohit/2026/08/22/dm-dos/
- August 2026 technical summary: https://blogs.scummvm.org/mohit/2026/08/04/week-10/
