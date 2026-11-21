#!/usr/bin/env python3
"""Safely apply the FIRES 6-bit sampling patch to a matching Unity editor source.
Stop on source mismatch; never overwrite an unrecognized newer file.
"""
from pathlib import Path
import hashlib,sys
SRC_SHA256 = "5c336937eb03d0ea991b6b5bd7c610cc4c26a032b01725ccccd9ad53b746e4da"
PATCH_SHA256 = "39024b22164d1daa30371c5b5a4bd20e4ef491d0a9ae4c5f6c8dd5a5783094c6"
BASE = Path(__file__).with_name('ViewportLayoutEditor.cs')
TARGET = Path(r'C:\Unity\DM\Assets\Editor\ViewportLayoutEditor.cs')
if not TARGET.exists():
    raise SystemExit('ERROR: Unity source not found: '+str(TARGET))
a=TARGET.read_bytes(); h=hashlib.sha256(a).hexdigest()
if h==PATCH_SHA256:
    print('Already patched; no changes.');sys.exit(0)
if h!=SRC_SHA256:
    raise SystemExit('STOP: Unity source differs from 2026-11-21 reviewed version; nothing changed. Current SHA256='+h)
if hashlib.sha256(BASE.read_bytes()).hexdigest()!=PATCH_SHA256:
    raise SystemExit('STOP: supplied patched source checksum incorrect.')
# preserve original before replacing
bak=TARGET.with_suffix('.cs.before_dos6bit.bak')
if bak.exists():
    raise SystemExit('STOP: backup already exists; nothing changed: '+str(bak))
bak.write_bytes(a)
TARGET.write_bytes(BASE.read_bytes())
print('Updated:',TARGET)
print('Backup:',bak)
