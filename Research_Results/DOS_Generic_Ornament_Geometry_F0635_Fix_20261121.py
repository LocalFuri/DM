#!/usr/bin/env python3
"""PC Dungeon Master (713-graphic X720) wall-ornament geometry prototype.

Reads original GRAPHICS.DAT or a ZIP containing it; exports a CSV of all 15
view slots for a supplied wall-ornament graphic TYPE and LAY1 coordinate SET.
No display-pixel decoding or guessed missing records. No Unity changes.

Usage:
 python DOS_Generic_Ornament_Geometry_20261121.py GRAPHICS_DOS.ZIP --type 4 --set 0 --label Hook
 python DOS_Generic_Ornament_Geometry_20261121.py GRAPHICS_DOS.ZIP --type 36 --set 1 --label Manacles

This script models the non-text/non-shift branch of F0107/F0675 and F0635.
Unsupported layouts and conditions raise errors; do not treat results as
runtime measurements or as a complete F0635 port.
"""
from __future__ import annotations
import argparse
import csv
import json
import struct
import zipfile
from dataclasses import dataclass, asdict
from pathlib import Path

# 60 ornament coordinate sets, transcribed from the X720 branch of
# G0194_auc_Graphic558_WallOrnamentCoordinateSetIndices in DUNVIEW.C.
# This removes the need for per-ornament manually supplied placements.
COORDINATE_SETS = (
    7, 1, 1, 1, 0, 0, 0, 8, 0, 8, 0, 2, 2, 0, 0,
    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    0, 0, 0, 2, 2, 1, 1, 1, 1, 1, 4, 4, 4, 5, 0,
    0, 1, 0, 0, 0, 2, 0, 0, 0, 0, 2, 6, 6, 6, 6,
)

VIEW_NAMES = (
    'D3 far-left right', 'D3 far-right left',
    'D3 left right', 'D3 right left',
    'D3 left front', 'D3 center front', 'D3 right front',
    'D2 left right', 'D2 right left',
    'D2 left front', 'D2 center front', 'D2 right front',
    'D1 left right', 'D1 right left', 'D1 center front',
)

@dataclass(frozen=True)
class Record:
    kind: int
    parent: int
    data1: int
    data2: int


def read_graphics(path: Path) -> bytes:
    if zipfile.is_zipfile(path):
        with zipfile.ZipFile(path) as arc:
            matches = [n for n in arc.namelist() if n.upper().endswith('GRAPHICS.DAT')]
            if len(matches) != 1:
                raise ValueError('ZIP must contain exactly one GRAPHICS.DAT')
            return arc.read(matches[0])
    return path.read_bytes()


def read_dimensions(data: bytes):
    if len(data) < 4:
        raise ValueError('Truncated GRAPHICS.DAT')
    version, count = struct.unpack_from('<HH', data)
    if version != 0x8001 or count != 713:
        raise ValueError(f'Unsupported graphics version/count: {version:04x}/{count}; expected 8001/713')
    pos = 4 + 4 * count
    if pos + count * 4 > len(data):
        raise ValueError('Truncated dimensions table')
    return [struct.unpack_from('<HH', data, pos + 4 * i) for i in range(count)]


def read_layout696(data: bytes):
    matches = []
    marker = b'\x0d\xfc\x17\x00'
    offset = data.find(marker)
    while offset >= 0:
        if offset + 4 + 4 * 23 <= len(data):
            range_count = struct.unpack_from('<H', data, offset + 2)[0]
            if range_count == 23:
                ranges = [struct.unpack_from('<HH', data, offset + 4 + i * 4) for i in range(23)]
                count = sum(last - first + 1 for first, last in ranges)
                end = offset + 4 + 4 * 23 + count * 8
                if all(last >= first for first, last in ranges) and count == 1133 and end <= len(data):
                    records = {}
                    pointer = offset + 4 + 4 * 23
                    for first, last in ranges:
                        for number in range(first, last + 1):
                            if number in records:
                                raise ValueError('Duplicate layout index')
                            records[number] = Record(*struct.unpack_from('<hhhh', data, pointer))
                            pointer += 8
                    matches.append((offset, records))
        offset = data.find(marker, offset + 1)
    if len(matches) != 1:
        raise ValueError(f'Expected exactly one valid layout resource 696, got {len(matches)}')
    return matches[0]


def scaled(dim: int, scale: int) -> int:
    return ((dim * scale) + (scale >> 1)) >> 5


def geometry(records, zone_index, image_width, image_height):
    """Layout F0635 subset for ordinary unshifted bitmap zones.

    Supports record types 0..8, parent type 1 (translation) and type 9
    (clipping container). Uses inclusive clipping, returns source offsets.
    Rejects unsupported 10..18 child-parent positioning rather than guessing.
    """
    if zone_index not in records:
        raise ValueError(f'Layout record {zone_index} missing')
    if min(image_width, image_height) <= 0:
        raise ValueError('Image dimensions must be positive')
    record = records[zone_index]
    if not 0 <= record.kind <= 8:
        raise NotImplementedError(f'Record type {record.kind} at {zone_index} not supported')
    kind = record.kind
    x, y = record.data1, record.data2
    # F0625_GetZoneInitializedFromDimensions(20000, 20000)
    clip = [0, 0, 20000, 20000]  # left, top, width, height
    seen = {zone_index}
    while record.parent:
        idx = record.parent
        if idx in seen:
            raise ValueError(f'Cycle at {idx}')
        seen.add(idx)
        if idx not in records:
            raise ValueError(f'Missing parent record {idx}')
        parent = records[idx]
        if parent.kind == 1:
            x += parent.data1
            y += parent.data2
            clip[0] += parent.data1
            clip[1] += parent.data2
        elif parent.kind == 9:
            pw, ph = parent.data1, parent.data2
            # F0635 uses the IMMEDIATE child record as the reference for parent
            # clipping, not the accumulated final ornament position.
            parent_child_kind = record.kind
            parent_child_x, parent_child_y = record.data1, record.data2
            # F0635's unshifted record-9 clipping case, in local coordinates.
            if parent_child_kind == 0:
                px, py = parent_child_x - ((pw + 1) >> 1), parent_child_y - ((ph + 1) >> 1)
            elif parent_child_kind == 1:
                px, py = parent_child_x, parent_child_y
            elif parent_child_kind == 2:
                px, py = parent_child_x - (pw - 1), parent_child_y
            elif parent_child_kind == 3:
                px, py = parent_child_x - (pw - 1), parent_child_y - (ph - 1)
            elif parent_child_kind == 4:
                px, py = parent_child_x, parent_child_y - (ph - 1)
            elif parent_child_kind == 5:
                px, py = parent_child_x - ((pw + 1) >> 1), parent_child_y
            elif parent_child_kind == 6:
                px, py = parent_child_x - (pw - 1), parent_child_y - ((ph + 1) >> 1)
            elif parent_child_kind == 7:
                px, py = parent_child_x - ((pw + 1) >> 1), parent_child_y - (ph - 1)
            elif parent_child_kind == 8:
                px, py = parent_child_x, parent_child_y - ((ph + 1) >> 1)
            else:
                raise AssertionError('unreachable layout type')
            left = max(clip[0], px)
            top = max(clip[1], py)
            right = min(clip[0] + clip[2], px + pw)
            bottom = min(clip[1] + clip[3], py + ph)
            clip = [left, top, right - left, bottom - top]
        else:
            raise NotImplementedError(f'Parent record kind {parent.kind} at {idx}; exact F0635 port required')
        record = parent
    if kind == 0:
        x -= (image_width + 1) >> 1
        y -= (image_height + 1) >> 1
    elif kind == 1:
        pass
    elif kind == 2:
        x -= image_width - 1
    elif kind == 3:
        x -= image_width - 1
        y -= image_height - 1
    elif kind == 4:
        y -= image_height - 1
    elif kind == 5:
        x -= (image_width + 1) >> 1
    elif kind == 6:
        x -= image_width - 1
        y -= (image_height + 1) >> 1
    elif kind == 7:
        x -= (image_width + 1) >> 1
        y -= image_height - 1
    elif kind == 8:
        y -= (image_height + 1) >> 1
    left = max(x, clip[0])
    top = max(y, clip[1])
    right = min(x + image_width, clip[0] + clip[2])
    bottom = min(y + image_height, clip[1] + clip[3])
    if right <= left or bottom <= top:
        return dict(x=x, y=y, width=image_width, height=image_height, clipped_x=None,
                    clipped_y=None, clipped_width=0, clipped_height=0, source_x=None, source_y=None)
    return dict(x=x, y=y, width=image_width, height=image_height, clipped_x=left,
                clipped_y=top, clipped_width=right-left, clipped_height=bottom-top,
                source_x=left-x, source_y=top-y)


def view_graphic(base_index, index):
    """F0107, X720 branch. Excludes special inscription/shift cases."""
    if not 0 <= index < 15:
        raise ValueError('View must be 0..14')
    # Source code increments native index for D3 front and D2 front, plus D1 front.
    mirrored = index in (1, 3, 8, 13)
    front = index >= 4 and index != 7 and index != 8 and index != 12 and index != 13
    native_index = base_index + int(front)
    if index <= 1:
        sx, sy, palette = 30, 14, 'D3 special'
    elif index <= 6:
        sx, sy, palette = 14, 14, 'D3'
    elif index <= 11:
        sx, sy, palette = 21, 21, 'D2'
    else:
        sx, sy, palette = 32, 32, 'native'
    return native_index, mirrored, sx, sy, palette


def calculate(data, ornament_type: int, coordinate_set: int, label: str):
    dims = read_dimensions(data)
    layout_offset, records = read_layout696(data)
    if not (0 <= ornament_type <= 59):
        raise ValueError('Type must be 0..59 for the 60 known wall-ornament graphics pairs')
    if coordinate_set < 0:
        raise ValueError('Coordinate set must be >= 0')
    results = []
    for view in range(15):
        index, mirror, sx, sy, palette = view_graphic(259+2*ornament_type, view)
        nw, nh = dims[index]
        w, h = scaled(nw, sx), scaled(nh, sy)
        zone = 1004 + 15 * coordinate_set + view
        rect = geometry(records, zone, w, h)
        results.append(dict(label=label, ornament_type=ornament_type, coordinate_set=coordinate_set,
                            view_index=view, view_name=VIEW_NAMES[view], zone_index=zone,
                            native_graphic=index, native_width=nw, native_height=nh,
                            scale_x=sx, scale_y=sy, palette_group=palette,
                            mirrored_horizontally=int(mirror), **rect))
    return layout_offset, results


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument('graphics', type=Path)
    p.add_argument('--type', type=int, dest='ornament_type', help='0..59; omit with --all')
    p.add_argument('--set', type=int, dest='coordinate_set', help='Optional explicit override for research, otherwise original G0194 map')
    p.add_argument('--all', action='store_true', help='Export all 60 types x 15 view positions')
    p.add_argument('--label', default='Ornament')
    p.add_argument('--output', type=Path)
    args = p.parse_args()
    data = read_graphics(args.graphics)
    if args.all:
        if args.ornament_type is not None or args.coordinate_set is not None:
            p.error('--all cannot be combined with --type or --set')
        rows = []
        for ornament_type in range(60):
            _, current = calculate(data, ornament_type, COORDINATE_SETS[ornament_type], f'Type_{ornament_type:02d}')
            rows.extend(current)
        dest = args.output or Path('DOS_All_Ornament_Geometry.csv')
        offset, _ = read_layout696(data)
    else:
        if args.ornament_type is None:
            p.error('specify --type or --all')
        if not 0 <= args.ornament_type < len(COORDINATE_SETS):
            p.error('--type must be 0..59')
        coord = COORDINATE_SETS[args.ornament_type] if args.coordinate_set is None else args.coordinate_set
        offset, rows = calculate(data, args.ornament_type, coord, args.label)
        dest = args.output or Path(f'DOS_{args.label.replace(" ", "_")}_Geometry.csv')
    with dest.open('w', newline='', encoding='utf-8') as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    print(f'Resource 696 offset: {offset}; saved {len(rows)} view rows to {dest}')
    if not args.all:
        for row in rows:
            if row['view_index'] in (5, 10, 14):
                print(f"  {row['view_name']}: gfx={row['native_graphic']}, "
                      f"scale={row['scale_x']}/{row['scale_y']}, "
                      f"rect=({row['x']},{row['y']},{row['width']},{row['height']}) "
                      f"clipped=({row['clipped_x']},{row['clipped_y']},"
                      f"{row['clipped_width']},{row['clipped_height']})")


if __name__ == '__main__':
    main()
