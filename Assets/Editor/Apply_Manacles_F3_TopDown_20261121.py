#!/usr/bin/env python3
"""Guarded Manacles F3 sampling orientation fix; leaves all shared code unchanged."""
from pathlib import Path
from hashlib import sha256
from datetime import datetime
from zoneinfo import ZoneInfo
import shutil

TARGET = Path(r"C:\Unity\DM\Assets\Editor\ViewportLayoutEditor.cs")
OLD_SHA256 = "b688856d76be66a8507c2154d77b46e80cd93f9e80a07b39a5a48011e916f73b"
NEW_SHA256 = "79ef26c637bce51c226f8699a6ab29690183dca20380ae97238b87df7565fc2f"
OLD = '        WallOrnamentFarColorMap,\n        "Manacles F3 Generated from F1");'
NEW = '        WallOrnamentFarColorMap,\n        "Manacles F3 Generated from F1",\n        roundBiasOverride: 0,\n        sampleTopDown: true);'

def main():
    if not TARGET.is_file():
        raise SystemExit("STOP: Unity source not found: " + str(TARGET))
    before = TARGET.read_bytes()
    digest = sha256(before).hexdigest()
    if digest == NEW_SHA256:
        print("Already installed; nothing changed.")
        return
    if digest != OLD_SHA256:
        raise SystemExit("STOP: Unity source differs from verified F3 geometry patch. No changes made. SHA256="+digest)
    old = OLD.encode('ascii')
    new = NEW.encode('ascii')
    if before.count(old) != 1:
        raise SystemExit("STOP: Expected Manacles F3 caller not found exactly once.")
    after = before.replace(old, new, 1)
    if sha256(after).hexdigest() != NEW_SHA256:
        raise SystemExit("STOP: New source checksum mismatch. No changes made.")
    stamp = datetime.now(ZoneInfo("Europe/Berlin")).strftime("%Y%m%d-%H%M%S")
    backup = TARGET.with_name("ViewportLayoutEditor.cs.before_manacles_f3_topdown_"+stamp+".bak")
    if backup.exists():
        raise SystemExit("STOP: Backup already exists. No changes made.")
    temp = TARGET.with_suffix(TARGET.suffix + ".tmp")
    if temp.exists():
        raise SystemExit("STOP: Temporary file already exists. No changes made.")
    shutil.copy2(TARGET, backup)
    try:
        temp.write_bytes(after)
        temp.replace(TARGET)
    finally:
        if temp.exists(): temp.unlink()
    print("Updated: " + str(TARGET))
    print("Backup: " + str(backup))
    print("Change: Manacles F3 uses shared 6-bit scaler in DOS top-down orientation, zero bias")
    print("Unchanged: Manacles F2; Hook F2; F3 rectangle; generic scaler; other ornaments")
    print("SHA256: " + NEW_SHA256)

if __name__ == "__main__":
    main()
