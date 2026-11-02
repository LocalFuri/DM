# 02 — Lighting and darkness

Viewport brightness is a palette index, 0 through 5. Palette 0 is the brightest. The portraits are not dimmed by this palette. The Atari swapped palettes at the viewport (`The Palettes` note on the CSBwin site describes that hardware trick). This report covers the number that selects the viewport palette.

## Data tables extracted from this graphics.dat

**CONFIRMED** by decompressing this file on 2 November 2026.

| Item | Value |
|---|---|
| File | `graphics.dat` |
| SHA-256 | `196cb349827e8926e2763fa61ecbe8e10810003045ef72a435b7da4dded56d8e` |
| Endian | big-endian words (first header word was not `0x8001`) |
| Compression | CSBwin `LZWExpand` (`Graphics.cpp` 2778–2866) plus the `0x90` repeat in `TAG022eec` (2963–3013) |

Graphic `0x232` is read with flag `0x8000`, so the LZW output is the raw table, not a bitmap expand:

```
ReadAndExpandGraphic(0x8000 | 0x232, (ui8 *)d.Byte1832 + 2, 0, 0);
```

`CSBCode.cpp` line 10607. Asserted decompressed size `0x722` = 1826 bytes (line 10606). This file produced 1826 bytes.

`#pragma pack(1)` is set in `CSB.h` line 18. The graphic is stored at `Byte1830`, whose name is Atari address 1830 (`Data.h` 1373–1377). `swapNwords` byte-swaps each word (`CSBCode.cpp` 10422–10427), which on this little-endian port converts big-endian file words to host order.

`PaletteBrightness` is Atari address 1086 (`Data.h` 1403). Offset in the graphic:

```
1830 - 1086 = 744
```

Six signed 16-bit values, **CONFIRMED**:

| Index | Threshold |
|---:|---:|
| 0 | 99 |
| 1 | 75 |
| 2 | 50 |
| 3 | 25 |
| 4 | 1 |
| 5 | 0 |

`Word1074` is Atari address 1074 (`Data.h` 1405). Offset `1830 - 1074 = 756`. Sixteen signed 16-bit values, **CONFIRMED**:

| Intensity | Brightness |
|---:|---:|
| 0 | 0 |
| 1 | 5 |
| 2 | 12 |
| 3 | 24 |
| 4 | 33 |
| 5 | 40 |
| 6 | 46 |
| 7 | 51 |
| 8 | 59 |
| 9 | 68 |
| 10 | 76 |
| 11 | 82 |
| 12 | 89 |
| 13 | 94 |
| 14 | 97 |
| 15 | 100 |

These are the same numbers Paul Stevens published in [Technical Documentation: Miscellaneous](http://www.dianneandpaul.net/CSBwin/TechDoc/TDMiscellaneous.htm). The binary now confirms them for this `graphics.dat`.

## Final light level

`SelectPaletteForLightLevel` (`CSBCode.cpp` 7616–7710).

If `d.pCurLevelDesc->experienceMultiplier()` is 0, `d.CurrentPalette = 0` and the function returns. Torches and `d.Brightness` are ignored. **CONFIRMED.** Which levels have a zero multiplier is dungeon data, not this function.

Otherwise:

1. For each of up to four champions, read possessions 1 and 0 only (the two hands). The loop is `for (D4W = 2; (D4W--) != 0;)` (lines 7639–7653).
2. If the object name is `objNI_Torch_a` through `objNI_Torch_d`, store `DB5::charges()` in that slot. Any other hand object stores 0. A torch in the backpack is not read.
3. Selection-sort the first four of the eight slots so the larger charges move forward (lines 7656–7668). The comment says "Find four brightest torches."
4. Sum five slots, not four (lines 7669–7681):

```
shift = 6
totalLight = 0
for each of 5 slots:
    if charges == 0: continue
    contribution = (Word1074[charges] << shift) >> 6    # logical, then masked with 0x3ff
    totalLight += contribution
    shift = max(0, shift - 1)
totalLight += d.Brightness
```

The `<< shift` then `>> 6` weights are **1, 1/2, 1/4, 1/8, 1/16**. Integer truncation happens at the shift. The fifth slot is whatever remains after only four sort passes. It is not guaranteed to be the fifth-brightest torch.

5. Palette (lines 7683–7696):

```
if totalLight > 0:
    palette = 0
    while PaletteBrightness[palette] > totalLight:
        palette += 1
else:
    palette = 5
```

With the extracted thresholds, a positive total maps like this:

| totalLight | Palette |
|---:|---:|
| 99 or more | 0 |
| 75–98 | 1 |
| 50–74 | 2 |
| 25–49 | 3 |
| 1–24 | 4 |
| 0 or negative | 5 |

The sixth threshold is 0. A positive integer stops at threshold 1 before it reaches that 0. Palette 5 is the `totalLight <= 0` branch.

`d.Brightness` is a signed 16-bit field (`Data.h` line 750). Spells add and subtract it. It is not clamped in `SelectPaletteForLightLevel`.

`d.Word11778 = 1` asks the blank handler to copy `Palette552[d.CurrentPalette]` into the active palette (`CSBCode.cpp` 1064–1068).

## Torches

Fuel is `DB5::charges()`, and the burn step keeps only four bits: `(charges - 1) & 15` (`CSBCode.cpp` 7739–7746). Brightness uses `Word1074[charges]`, so the useful range is 0–15. Charge 0 adds nothing (`if (*pwA3 == 0) continue`).

`NinetySecondUpdate` (`CSBCode.cpp` 7713–7764) runs when `(d.Time & 511) == 0`, every **512 ticks**. For each champion except a champion still being recruited, for each of the two hands:

- name must be `objNI_Torch_a` … `objNI_Torch_d`
- charges must be non-zero
- charges become `(charges - 1) & 15`
- if the new charges are 0, `important` is cleared

The torch object remains. It simply stops contributing light. Decay is one charge step per 512 ticks, not a continuous fade. The displayed brightness jumps by `Word1074[old] - Word1074[new]` after the palette is rebuilt.

At the commented 6 Hz clock, 512 ticks is about 85 seconds. At this port's Normal speed, 512 × 255 ms = 130560 ms.

Putting `objNI_Torch_a` into a hand sets `litTorch(1)` and calls `SelectPaletteForLightLevel` (`Character.cpp` 2008–2013). The palette function keys off the name range and the charge count, not the `litTorch` flag.

A torch on the floor or in a backpack is not in the hand scan and is not decremented by `NinetySecondUpdate`.

## Illumulet

Necklace slot is possession 10.

- Equip `objNI_Illumulet_a` or `_b`: `d.Brightness += Word1074[2]`, which is **+12**, and the misc value becomes 1 (`Character.cpp` 2028–2036).
- Remove it: `d.Brightness -= Word1074[2]`, and the misc value becomes 0 (`Character.cpp` 1886–1892).

There is no timer. The +12 stays until the amulet is removed. `Word1074[2]` was confirmed as 12 in this file.

## Light spells

Spell class 3, `Magic.cpp` 1266–1289. Before the switch:

```
D6W = 4 * (spellPower + 1)
```

**OH IR RA**, missile type 0:

```
delay = 10000
delay = delay + (D6W - 8) * 512
D6W = D6W / 2 - 1          # this is the intensity
d.Brightness += Word1074[D6W]
SetBrightnessTimer(-D6W, delay)
```

`SetBrightnessTimer` (`Magic.cpp` 801–812) posts `TT_LIGHT` at `d.Time + delay` with `timerWord6 = -intensity`, then refreshes the palette immediately. The brightness is added now. The first downward step waits `delay` ticks.

Integer examples, using the extracted table:

| spellPower | D6W before | intensity | Word1074 added | delay ticks |
|---:|---:|---:|---:|---:|
| 1 | 8 | 3 | 24 | 10000 |
| 2 | 12 | 5 | 40 | 12048 |
| 3 | 16 | 7 | 51 | 14096 |
| 4 | 20 | 9 | 68 | 16144 |
| 5 | 24 | 11 | 82 | 18192 |
| 6 | 28 | 13 | 94 | 20240 |

Delay arithmetic: power 2 is `10000 + (12 - 8) * 512 = 12048`. The source uses 16-bit `sw()` on `delay`. 20240 still fits in a signed 16-bit value (max 32767). Higher powers are a dungeon-data question; the formula is what is written.

**Missile type 5** (the other light form in the same switch):

```
delay = 4000 + 128 * (D6W - 3)
intensity = D6W / 4 + 1
d.Brightness += Word1074[intensity]
SetBrightnessTimer(-intensity, delay)
```

Power 1: `D6W = 8`, delay `4000 + 128 * 5 = 4640`, intensity `8/4 + 1 = 3`, brightness +24.

**DES IR SAR**, missile type 1, darkness:

```
intensity = D6W / 4
d.Brightness -= Word1074[intensity]
SetBrightnessTimer(+intensity, 98)
```

Power 1: intensity 2, brightness **−12**, first recovery step after **98** ticks.

Positive `timerWord6` means the steps add brightness back. Negative means they remove it.

## How a light step decays

`ProcessLightLevelTimer` (`Timer.cpp` 2828–2862), timer type `TT_LIGHT` (70):

```
steps = timerWord6          # negative means a light that is fading
if steps == 0: return
negative = steps < 0
if negative: steps = -steps
next = steps - 1
delta = Word1074[steps] - Word1074[next]
if negative: delta = -delta; next = -next
d.Brightness += delta
if next != 0:
    schedule TT_LIGHT at d.Time + 4 with timerWord6 = next
```

The assert requires `steps < 16` (line 2845).

Each later step is **4 ticks** after the previous one. The brightness change is the difference of two table entries, so the fade follows the curve of `Word1074` rather than a constant subtraction.

Example. OH IR RA power 1 adds 24 at once (intensity 3). After `delay` ticks the first step runs with `steps = 3`:

```
delta = -(Word1074[3] - Word1074[2]) = -(24 - 12) = -12
```

Four ticks later, steps 2: `-(12 - 5) = -7`. Four ticks later, steps 1: `-(5 - 0) = -5`. Then `next` is 0 and the timer stops. The original +24 has been removed.

Darkness power 1 subtracts 12 (intensity 2) and schedules +2 at +98 ticks. The reverse steps add `12 - 5` and then `5 - 0`.

A staff Light attack (`Attack.cpp` 2050–2061) adds `Word1074[2]` (12) and decays with step count −2 after **2500** ticks, unless a filter changes `deltaLight`, `decayRate`, or `time`.

## Multiple sources

Hand torches, `d.Brightness` (spells, Illumulet, darkness), and the level-multiplier bypass are the whole sum. There is no per-cell light and no distance falloff in this function. Two torches do not add in full: the second is halved, the third quartered, and so on, after sorting by charges.
