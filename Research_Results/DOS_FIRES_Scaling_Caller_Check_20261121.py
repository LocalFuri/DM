#!/usr/bin/env python3
"""Read-only check of scaled-buffer helper FAR CALL sites in unpacked FIRES.
Usage: python DOS_FIRES_Scaling_Caller_Check_20261121.py FIRES_UNPACKED.EXE
"""
import sys
import struct
from pathlib import Path
TARGET = 0x1C7A6
SITES = [0x244A5, 0x244E4, 0x24570, 0x24606, 0x246B6, 0x2470A,
         0x24760, 0x247EF, 0x2489A, 0x248D3, 0x24917, 0x24950,
         0x24994, 0x249CD, 0x24A11, 0x24A4A]

def main(path):
    exe = Path(path).read_bytes()
    assert exe[:2] == b'MZ', 'Not a DOS MZ executable'
    load_start = struct.unpack_from('<H', exe, 8)[0] * 16
    image = exe[load_start:]
    count = struct.unpack_from('<H', exe, 6)[0]
    table_start = struct.unpack_from('<H', exe, 0x18)[0]
    relocs = set()
    for n in range(count):
        off, seg = struct.unpack_from('<HH', exe, table_start + n*4)
        relocs.add(seg*16 + off)
    found = []
    for p in sorted(relocs):
        pos = p - 3
        if pos < 0 or pos + 5 > len(image) or image[pos] != 0x9A:
            continue
        off, seg = struct.unpack_from('<HH', image, pos + 1)
        if seg*16+off == TARGET:
            found.append(pos)
    print('FAR CALL sites to scaled-size helper:')
    print(' '.join(f'{i:05X}' for i in sorted(found)))
    assert sorted(found) == SITES, 'Site list differs from research checkpoint'
    for site, scale in [(0x244A5,14),(0x244E4,21), (0x2489A,14), (0x248D3,21)]:
        # Find the nearest immediate mov ax,<scale>; push ax sequence, within 50 bytes.
        pattern = b'\xb8' + struct.pack('<H', scale) + b'\x50'
        assert image[max(0,site-55):site].find(pattern) >= 0, f'Missing scale setup before {site:05X}'
    print('All 16 direct calls verified, plus representative scale 14/21 setups.')
    print('Interpretation: likely scaled-buffer size table setup, not an ornament draw function.')

if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
