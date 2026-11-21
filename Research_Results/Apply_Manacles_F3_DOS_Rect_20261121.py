#!/usr/bin/env python3
"""Guarded original-DOS Manacles F3 geometry correction, 2026-11-21.
Modifies exactly five literals/comment spans; does not replace the full source file.
"""
from pathlib import Path
from datetime import datetime
from zoneinfo import ZoneInfo
from hashlib import sha256
import shutil

TARGET=Path(r"C:\Unity\DM\Assets\Editor\ViewportLayoutEditor.cs")
EXPECTED_SHA256="57dafedb0e98afe6fbbd1bb1f3ca03e6235f16d293619dbbb385f05605974910"
NEW_SHA256="b688856d76be66a8507c2154d77b46e80cd93f9e80a07b39a5a48011e916f73b"
REPLACEMENTS=[
    ('// size 38x20, framebuffer bottom-left Y = 200 - 73 - 20 = 107.', '// size 40x22, framebuffer bottom-left Y = 200 - 72 - 22 = 106.'),
    ('private const int ManaclesD3FrontX = 93;', 'private const int ManaclesD3FrontX = 92;'),
    ('private const int ManaclesD3FrontY = 107;', 'private const int ManaclesD3FrontY = 106;'),
    ('private const int ManaclesD3FrontWidth = 38;', 'private const int ManaclesD3FrontWidth = 40;'),
    ('private const int ManaclesD3FrontHeight = 20;', 'private const int ManaclesD3FrontHeight = 22;'),
]

def fail(reason):
    raise SystemExit("STOP: "+reason)

def main():
    if not TARGET.is_file(): fail("Unity source file not found: "+str(TARGET))
    original=TARGET.read_bytes()
    digest=sha256(original).hexdigest()
    if digest==NEW_SHA256:
        print("Already installed; no changes made.")
        return
    if digest!=EXPECTED_SHA256:
        fail("Unity source checksum differs from the tested F2-patched version. No changes made. Actual SHA256: "+digest)
    modified=original
    for old,new in REPLACEMENTS:
        a,b=old.encode('ascii'),new.encode('ascii')
        if modified.count(a)!=1: fail("Expected one occurrence of "+old)
        modified=modified.replace(a,b,1)
    if sha256(modified).hexdigest()!=NEW_SHA256:
        fail("New checksum disagrees with tested output")
    stamp=datetime.now(ZoneInfo("Europe/Berlin")).strftime("%Y%m%d-%H%M%S")
    backup=TARGET.with_name("ViewportLayoutEditor.cs.before_manacles_f3_geometry_"+stamp+".bak")
    if backup.exists(): fail("Backup already exists: "+str(backup))
    shutil.copy2(TARGET,backup)
    tmp=TARGET.with_suffix(TARGET.suffix+".tmp")
    if tmp.exists(): fail("Temporary output already exists: "+str(tmp))
    try:
        tmp.write_bytes(modified)
        tmp.replace(TARGET)
    finally:
        if tmp.exists(): tmp.unlink()
    print("Updated: "+str(TARGET))
    print("Backup: "+str(backup))
    print("Change: Manacles F3 DOS rectangle x=92, display-top=39, size=40x22")
    print("Unchanged: Manacles F2 60x34; shared 6-bit scaler; other ornaments")
    print("SHA256: "+NEW_SHA256)

if __name__=="__main__":main()
