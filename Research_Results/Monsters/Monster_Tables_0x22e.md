# Monster rendering tables – graphic 0x22e

Decoded directly from supplied binary using Cursor research offsets. CSBwin interpretation, not verified DOS PC behavior. No Unity source changed. Graphic 0x22f is still required for view existence flags and other behavior descriptors. The name-to-record association assumes type index equals descriptor index.

## Screamer (assumed record 6)
Front index 465; front/rear and attack descriptor size 64×60; side size zero; transparent index 13; placement group 0; recolor schemes 0/0. Attack existence and index require confirmation from 0x22f.

| View | Width | Height | Center-cell corner anchors (X center, Y bottom) |
|---|---:|---:|---|
| F1 | 64 | 60 | [[83, 103], [141, 103], [148, 119], [76, 119]] |
| F2 | 40 | 37 | [[92, 81], [131, 81], [132, 90], [91, 90]] |
| F3 | 32 | 30 | [[95, 70], [127, 70], [129, 75], [93, 75]] |

These dimensions use the CSBwin monster formula, not the approved DOS ornament renderer. No jitter applied. Raw palette maps may be modified at level load. Full decoded tables are in the JSON.
