"""Read-only DOS FIRES mapping verifier for the 2026-11-21 research checkpoint.

Usage:
  python DOS_FIRES_Runtime_Address_Map_20261121.py [CS:IP]

Place this script next to DM_CODE.BI and DOS_FIRES_Engine_Image_20261121.bin.
For full relocation validation, also place DOS_FIRES_Unpacked_20261121.zip here.
This script does NOT execute or modify game files.
"""
from pathlib import Path
import re
import struct
import sys
import zipfile

ROOT = Path(__file__).resolve().parent
DUMP_BASE = 0x3620
KNOWN_CS = 0x114C
LOAD_SEGMENT = KNOWN_CS - (DUMP_BASE >> 4)  # 0x0DEA

def load_inputs():
    dump = (ROOT / 'DM_CODE.BI').read_bytes()
    image = (ROOT / 'DOS_FIRES_Engine_Image_20261121.bin').read_bytes()
    zpath = ROOT / 'DOS_FIRES_Unpacked_20261121.zip'
    relocations = None
    if zpath.exists():
        with zipfile.ZipFile(zpath) as z:
            exe = z.read('FIRES_UNPACKED.EXE')
        n = struct.unpack_from('<H',exe,6)[0]
        table = struct.unpack_from('<H',exe,0x18)[0]
        relocations = {struct.unpack_from('<HH',exe,table+i*4)[1]*16 + struct.unpack_from('<HH',exe,table+i*4)[0] for i in range(n)}
    return dump,image,relocations

def main():
    dump,img,relocs=load_inputs()
    assert len(dump)==0x1000, 'Unexpected dump length'
    assert img[DUMP_BASE:DUMP_BASE+len(dump)] != dump, 'Expected DOS relocation patches are absent'
    differences=[i for i in range(len(dump)) if dump[i]!=img[DUMP_BASE+i]]
    starts=[]
    if relocs is not None:
        starts=sorted(p-DUMP_BASE for p in relocs if DUMP_BASE<=p and p+1<DUMP_BASE+len(dump))
        assert differences == sorted(x for i in starts for x in (i,i+1)), 'Mismatch beyond relocated words or missing relocation changes'
        deltas={(struct.unpack_from('<H',dump,i)[0]-struct.unpack_from('<H',img,DUMP_BASE+i)[0])&0xFFFF for i in starts}
        assert deltas=={LOAD_SEGMENT}, f'Unexpected relocation delta: {deltas}'
    print(f'Live segment origin: {LOAD_SEGMENT:04X}:0000')
    print(f'Dump: {len(dump)} bytes, FIRES load-image start 0x{DUMP_BASE:04X}')
    print(f'Byte-identical: {len(dump)-len(differences)}/{len(dump)}')
    if relocs is not None:
        print(f'All {len(starts)} relocation entries agree; applied segment delta +{LOAD_SEGMENT:04X}')
    else:
        print('Supply DOS_FIRES_Unpacked_20261121.zip for authoritative relocation-table check')
    arg = sys.argv[1] if len(sys.argv)>1 else '114C:145C'
    if not re.fullmatch(r'[0-9a-fA-F]{1,4}:[0-9a-fA-F]{1,4}',arg):
        raise SystemExit('Address must look like 114C:145C')
    seg,off=(int(x,16) for x in arg.split(':'))
    position=((seg-LOAD_SEGMENT)<<4)+off
    if not 0<=position<len(img):
        raise SystemExit('Address outside identified FIRES load image')
    print(f'Runtime {seg:04X}:{off:04X} -> FIRES load offset 0x{position:05X} (EXE file offset 0x{position+0x2800:05X})')
    print('Machine bytes:',img[position:position+16].hex(' '))
    print('Warning: mapping identifies bytes, not the ornament-rendering function.')

if __name__=='__main__': main()
