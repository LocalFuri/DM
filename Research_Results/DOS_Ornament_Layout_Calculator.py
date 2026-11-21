#!/usr/bin/env python3
"""Read original Dungeon Master DOS resource 696 LAY1 zone records.

Research-only diagnostic; does not modify Unity or its artwork.
No ornament-specific scaling, size, or position corrections.
"""
import argparse
from pathlib import Path
import struct


def read_zones(binary):
    if len(binary) < 96 or struct.unpack_from('<H', binary, 0)[0] != 0xFC0D:
        raise ValueError('Expected original DOS LAY1 resource with signature FC0D')
    pair_count = struct.unpack_from('<H', binary, 2)[0]
    record_start = 4 + pair_count * 4
    records = {}
    offset = record_start
    for i in range(pair_count):
        a, b = struct.unpack_from('<HH', binary, 4 + i * 4)
        if b < a:
            raise ValueError('Invalid zone range')
        for zone in range(a, b + 1):
            if offset + 8 > len(binary):
                raise ValueError('Truncated LAY1 resource')
            records[zone] = struct.unpack_from('<hhhh', binary, offset)
            offset += 8
    if offset != len(binary):
        raise ValueError(f'Trailing data: {len(binary)-offset} bytes')
    return records, pair_count


def layout_center(records, category, view_index):
    if not (0 <= view_index < 15):
        raise ValueError('Perspective index must be 0..14')
    zone_id = 1004 + category * 15 + view_index
    type_id, parent, cx, cy = records[zone_id]
    if type_id != 0 or parent != 4:
        raise ValueError(f'Zone {zone_id} is not a viewport-centered record: {(type_id,parent,cx,cy)}')
    return zone_id, cx, cy


def centered_rectangle(cx, cy, width, height):
    """Research hypothesis from previously documented DOS center formula."""
    return (cx - ((width + 1) // 2), cy - ((height + 1) // 2), width, height)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('resource696', type=Path)
    p.add_argument('--set', type=int, default=0, dest='category')
    p.add_argument('--view-index', type=int, default=10)
    p.add_argument('--width', type=int, default=None, help='Known destination width (not computed by LAY1)')
    p.add_argument('--height', type=int, default=None, help='Known destination height (not computed by LAY1)')
    p.add_argument('--all', action='store_true', help='Show all 15 entries of chosen set')
    args = p.parse_args()
    rec, nranges = read_zones(args.resource696.read_bytes())
    print(f'LAY1: {len(rec)} zone records in {nranges} ranges')
    if args.all:
        for i in range(15):
            zid, x, y = layout_center(rec, args.category, i)
            print(f'view {i:2d}: zone {zid}, center ({x},{y})')
        return
    zid, cx, cy = layout_center(rec, args.category, args.view_index)
    print(f'zone {zid}: center ({cx},{cy})')
    if (args.width is None) != (args.height is None):
        p.error('--width and --height must be provided together')
    if args.width is not None:
        if args.width <= 0 or args.height <= 0:
            p.error('dimensions must be positive')
        print('hypothesis rectangle (x,y,w,h):', centered_rectangle(cx,cy,args.width,args.height))
        print('NOTE: destination dimensions and sampling phase MUST be recovered from rendering code.')


if __name__ == '__main__':
    main()
