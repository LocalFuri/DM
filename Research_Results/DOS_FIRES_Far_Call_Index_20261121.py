#!/usr/bin/env python3
"""Read-only index of relocation-backed FAR CALL operands in unpacked FIRES.
Usage: python DOS_FIRES_Far_Call_Index_20261121.py FIRES_UNPACKED.EXE
Outputs two CSV files beside the input. This is NOT a function name matcher.
"""
import collections
import csv
from pathlib import Path
import struct
import sys


def u16(b, p):
    return struct.unpack_from('<H', b, p)[0]


def main():
    if len(sys.argv) != 2:
        print('Usage: python DOS_FIRES_Far_Call_Index_20261121.py FIRES_UNPACKED.EXE')
        return 2
    path = Path(sys.argv[1])
    b = path.read_bytes()
    if b[:2] != b'MZ':
        raise ValueError('Input is not an MZ executable')
    header_size = u16(b, 8) * 16
    relocation_count = u16(b, 6)
    relocation_table = u16(b, 0x18)
    if header_size >= len(b) or relocation_table + 4 * relocation_count > header_size:
        raise ValueError('Invalid MZ relocation table or header')
    code = b[header_size:]
    relocs = []
    for i in range(relocation_count):
        p = relocation_table + 4 * i
        relocs.append(u16(b, p) + 16 * u16(b, p + 2))
    entries = []
    for segpos in relocs:
        op = segpos - 3
        if op < 0 or segpos + 2 > len(code) or code[op] != 0x9A:
            continue
        offset = u16(code, op + 1)
        segment = u16(code, op + 3)
        dest = segment * 16 + offset
        if not 0 <= dest < len(code):
            continue
        entries.append((op, segment, offset, dest))
    # This is a syntactic candidate index, not a guaranteed decoded control-flow graph:
    # bytes in data regions may accidentally resemble a CALL opcode.
    detail = path.with_name('DOS_FIRES_Far_Calls_20261121.csv')
    with detail.open('w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(('call_site_image_offset_hex', 'target_segment_hex', 'target_offset_hex', 'target_image_offset_hex'))
        for callsite, segment, offset, dest in entries:
            w.writerow((f'{callsite:05X}', f'{segment:04X}', f'{offset:04X}', f'{dest:05X}'))
    count = collections.Counter(dest for _, _, _, dest in entries)
    summary = path.with_name('DOS_FIRES_Far_Call_Targets_20261121.csv')
    with summary.open('w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(('target_image_offset_hex', 'relocation_backed_far_calls'))
        for dest, n in count.most_common():
            w.writerow((f'{dest:05X}', n))
    print('MZ header bytes:', header_size)
    print('Relocation records:', relocation_count)
    print('Relocation-backed FAR CALL candidates:', len(entries))
    print('Distinct targets:', len(count))
    print('Wrote:', detail)
    print('Wrote:', summary)
    return 0


if __name__ == '__main__':
    sys.exit(main())
