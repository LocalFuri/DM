#!/usr/bin/env python3
"""Checksum-guarded Hook S2 DOS 6-bit installer; Python stdlib only."""
from pathlib import Path
from datetime import datetime
from hashlib import sha256
import os

TARGET = Path(r"C:\Unity\DM\Assets\Editor\ViewportLayoutEditor.cs")
PAYLOAD = Path(__file__).resolve().parent / 'ViewportLayoutEditor.cs'
EXPECTED_OLD_SHA = '472b361bd3cc60687bb43e89fa1dd9837e5317175864f924ee788e70d6207e53'
EXPECTED_NEW_SHA = 'aad9222b174d02a9e3502574f4ac86810eac0cc0e5b060ac403ebe1228c8df30'

def digest(b): return sha256(b).hexdigest()

def main():
    if not TARGET.is_file() or not PAYLOAD.is_file():
        raise SystemExit('STOP: Unity source or adjacent payload missing; nothing changed.')
    original = TARGET.read_bytes()
    replacement = PAYLOAD.read_bytes()
    if digest(replacement) != EXPECTED_NEW_SHA:
        raise SystemExit('STOP: Package source failed checksum; nothing changed.')
    if digest(original) == EXPECTED_NEW_SHA:
        print('Already installed; nothing changed.')
        return
    if digest(original) != EXPECTED_OLD_SHA:
        raise SystemExit('STOP: Current Unity file differs from verified Manacles S2 source; nothing changed.')
    stamp = datetime.now().astimezone().strftime('%Y%m%d-%H%M%S')
    backup = TARGET.with_name(TARGET.name + '.before_hook_s2_dos6bit_' + stamp + '.bak')
    if backup.exists():
        raise SystemExit('STOP: Backup already exists; nothing changed.')
    with backup.open('xb') as out:
        out.write(original)
    os.utime(backup, ns=(TARGET.stat().st_atime_ns, TARGET.stat().st_mtime_ns))
    tmp = TARGET.with_name(TARGET.name + '.hook_s2_dos6bit_tmp')
    try:
        with tmp.open('xb') as out: out.write(replacement)
        os.replace(tmp, TARGET)
    finally:
        if tmp.exists(): tmp.unlink()
    installed=datetime.now().timestamp()
    os.utime(TARGET,(installed,installed))
    print('Updated:', TARGET)
    print('Backup:', backup)
    print('Hook S2: x=66/149; display y=39; 9x12; native unpadded 14x19')
    print('Scaling: shared DOS 6-bit, medium palette, zero bias, top-down')
    print('Preserved: Hook S3; Manacles S2/F2/F3; Hook F2/F3')
    print('SHA256:',digest(TARGET.read_bytes()))

if __name__ == '__main__': main()
