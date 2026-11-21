"""Offline integrity check for the identified Dungeon Master PC DOS ornament renderer."""
from pathlib import Path
import csv

root = Path(__file__).resolve().parent
binary = root / 'DOS_FIRES_Engine_Image_20261121.bin'
if not binary.exists():
    raise SystemExit('Place DOS_FIRES_Engine_Image_20261121.bin beside this script.')
data = binary.read_bytes()
checks = {
    'F0107 entry': (0x1D886, bytes.fromhex('55 8B EC 83 EC 6A')),
    'scale 15': (0x1D8BA, bytes.fromhex('BA 0F 00 F7 EA')),
    'layout base 1004': (0x1D8C1, bytes.fromhex('05 EC 03')),
    'F3 scale 14': (0x1DA57, bytes.fromhex('C7 46 F2 0E 00')),
    'F2 scale 21': (0x1DA63, bytes.fromhex('C7 46 F2 15 00')),
    'scaled bitmap call': (0x1DA93, bytes.fromhex('0E E8 F3 FB')),
    'blit wrapper call': (0x1DB30, bytes.fromhex('0E E8 4F FC')),
}
for label, (offset, expected) in checks.items():
    actual = data[offset:offset + len(expected)]
    assert actual == expected, f'{label} @ {offset:05X}: got {actual.hex()} expected {expected.hex()}'
    print(f'OK {label}: {offset:05X}')
calls = [0x1F219, 0x1F36D, 0x1F4C4, 0x1F4D1, 0x1F64D,
         0x1F65A, 0x1F7F6, 0x1FA66, 0x1FA73, 0x1FC0B,
         0x1FC18, 0x1FDAE, 0x1FF69, 0x20107, 0x20379]
for address in calls:
    assert data[address] == 0xE8
    rel = int.from_bytes(data[address + 1:address + 3], 'little', signed=True)
    assert address + 3 + rel == 0x1D886
print(f'OK all {len(calls)} direct near-call sites -> 0x1D886')
print('Calculated previous-session runtime address: 2A64:10E6 (reverify after restart)')
