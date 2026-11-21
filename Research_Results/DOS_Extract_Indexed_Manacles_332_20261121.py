#!/usr/bin/env python3
"""Extract original 4-bit palette indices for DOS Manacles front graphic 332.
Research-only: no pixel sampling claims, no Unity/game modifications.
Usage: python DOS_Extract_Indexed_Manacles_332_20261121.py GRAPHICS_DOS.ZIP
Requires the previously installed dmcsb and Pillow.
"""
from __future__ import annotations
import collections, json, sys, zipfile
from pathlib import Path

GRAPHIC = 332
EXPECTED = (92, 52)
D2 = (0,12,1,3,4,3,6,7,5,9,10,11,0,2,14,13)
D3 = (0,0,12,3,4,3,0,6,3,9,10,11,0,1,0,2)


def graphics_bytes(path: Path) -> bytes:
    if zipfile.is_zipfile(path):
        with zipfile.ZipFile(path) as archive:
            matches = [name for name in archive.namelist() if name.upper().endswith('GRAPHICS.DAT')]
            if len(matches) != 1:
                raise ValueError(f'Exactly one GRAPHICS.DAT required; found {matches}')
            return archive.read(matches[0])
    if path.name.upper() != 'GRAPHICS.DAT':
        raise ValueError('Input must be GRAPHICS.DAT or an archive containing it')
    return path.read_bytes()


def indexed_png(path: Path, pixels: bytes, width: int, height: int) -> None:
    from PIL import Image
    # Artificial grayscale diagnostic palette. PNG pixel indices remain original 0..15.
    im = Image.frombytes('P', (width, height), pixels)
    palette = []
    for i in range(256):
        v = i * 16 if i < 16 else 0
        palette.extend((v,v,v))
    im.putpalette(palette)
    im.save(path)
    # Explicit round-trip asserts PNG retains the exact palette indices.
    with Image.open(path) as check:
        if check.mode != 'P' or check.tobytes() != pixels:
            raise RuntimeError(f'Index PNG round-trip failure: {path}')


def histogram(values: bytes) -> dict[str,int]:
    c = collections.Counter(values)
    return {str(i):c.get(i,0) for i in range(16)}


def run(path: Path) -> None:
    from dmcsb import open_graphics
    g = open_graphics(data=graphics_bytes(path))
    result = g.image_indexed(GRAPHIC)
    if result is None or len(result) != 3:
        raise ValueError('Decoder did not return (width,height,indices) for graphic 332')
    width,height,ind = result
    if (width,height) != EXPECTED:
        raise ValueError(f'Graphic 332 expected 92x52, got {width}x{height}')
    pixels = bytes(ind)
    if len(pixels) != width*height:
        raise ValueError(f'Expected {width*height} unpacked pixel indices, got {len(pixels)} bytes')
    if any(p > 15 for p in pixels):
        raise ValueError('Decoder returned values outside 4-bit palette indices 0..15')

    base = Path('DOS_Manacles332_Indexed_20261121')
    raw = base.with_suffix('.bin')
    raw.write_bytes(pixels)  # row-major, one byte per original 4-bit index; no palette applied
    indexed_png(Path(f'{base}.png'), pixels, width, height)

    converted = {}
    for name, lut in [('D2', D2),('D3', D3)]:
        changed = bytes(lut[p] for p in pixels)
        Path(f'{base}_{name}.bin').write_bytes(changed)
        indexed_png(Path(f'{base}_{name}.png'), changed, width, height)
        converted[name] = {'histogram':histogram(changed), 'transparent_pixels':changed.count(10)}

    summary = {
      'source':str(path), 'graphic_index':GRAPHIC, 'native_dimensions':[width,height],
      'pixel_format':'one unpacked 4-bit palette index stored per byte, row-major',
      'palette_indices_are_original':True, 'PNG_palette_is_diagnostic_grayscale_not_DOS_RGB':True,
      'transparency_index':10, 'original_histogram':histogram(pixels),
      'original_transparent_pixels':pixels.count(10),
      'LUT_before_resampling_examples_only':True,
      'palette_remaps':{'D2':list(D2),'D3':list(D3)},
      'remapped':converted,
      'no_pixel_resampling_performed':True,
      'sampling_algorithm_status':'NOT verified; F0129 implementation still needed',
      'warning':'These are native 92x52 indexed data and mapped values, NOT a rendered F2 or F3 image.'
    }
    Path(f'{base}_Report.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
    print('SUCCESS: original native indexed pixels extracted for graphic 332 (92 x 52).')
    print('Original palette histogram:',summary['original_histogram'])
    print('Transparent index 10 pixels:',summary['original_transparent_pixels'])
    print('Wrote native .bin/.png and D2/D3 index-mapped .bin/.png plus JSON report.')
    print('IMPORTANT: no shrinking or claim of pixel-perfect DOS output was performed.')

if __name__=='__main__':
    if len(sys.argv) != 2:
        print('Usage: python DOS_Extract_Indexed_Manacles_332_20261121.py GRAPHICS_DOS.ZIP')
        sys.exit(2)
    try:
        run(Path(sys.argv[1]))
    except Exception as exc:
        print(f'ERROR: {exc}',file=sys.stderr)
        sys.exit(1)
