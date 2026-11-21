#!/usr/bin/env python3
"""Guarded replacement: Manacles S2 path only; no external Python packages.
Run after extracting the ZIP next to this script and ViewportLayoutEditor.cs.
"""
from pathlib import Path
from datetime import datetime
from hashlib import sha256
import os
import sys

TARGET = Path(r"C:\Unity\DM\Assets\Editor\ViewportLayoutEditor.cs")
PAYLOAD = Path(__file__).resolve().parent / "ViewportLayoutEditor.cs"
EXPECTED_ORIGINAL_SHA256 = "79ef26c637bce51c226f8699a6ab29690183dca20380ae97238b87df7565fc2f"
EXPECTED_PATCHED_SHA256 = "472b361bd3cc60687bb43e89fa1dd9837e5317175864f924ee788e70d6207e53"

def checksum(data):
    return sha256(data).hexdigest()

def main():
    if not TARGET.is_file() or not PAYLOAD.is_file():
        raise SystemExit("STOP: Unity source or adjacent replacement file not found. Nothing changed.")
    current = TARGET.read_bytes()
    replacement = PAYLOAD.read_bytes()
    if checksum(replacement) != EXPECTED_PATCHED_SHA256:
        raise SystemExit("STOP: Replacement file checksum mismatch. Nothing changed.")
    if checksum(current) == EXPECTED_PATCHED_SHA256:
        print("Already installed. No files changed.")
        return
    if checksum(current) != EXPECTED_ORIGINAL_SHA256:
        raise SystemExit("STOP: Current Unity source does not match the reviewed 2026-11-21 ZIP. Nothing changed.")
    # Local Windows timezone supplies the timestamp; no tzdata dependency.
    stamp = datetime.now().astimezone().strftime("%Y%m%d-%H%M%S")
    backup = TARGET.with_name(TARGET.name + ".before_manacles_s2_dos6bit_" + stamp + ".bak")
    if backup.exists():
        raise SystemExit("STOP: Backup name already exists. Nothing changed.")
    backup.write_bytes(current)
    os.utime(backup, ns=(TARGET.stat().st_atime_ns, TARGET.stat().st_mtime_ns))
    temp = TARGET.with_name(TARGET.name + ".manacles_s2_dos6bit_tmp")
    try:
        with temp.open("xb") as stream:
            stream.write(replacement)
        os.replace(temp, TARGET)
    finally:
        if temp.exists():
            temp.unlink()
    # Preserve current file time if the replacement is already extracted;
    # the newly modified Unity source must indicate installation time.
    now = datetime.now().timestamp()
    os.utime(TARGET, (now, now))
    print("Updated:", TARGET)
    print("Backup:", backup)
    print("Manacles S2: X=57/144, viewport Y=40, 21x27, native 32x42")
    print("Scaling: shared DOS 6-bit, medium palette, top-down, zero bias")
    print("Unchanged: F2/F3, S3, Hook and other ornament sources")
    print("SHA256:", checksum(TARGET.read_bytes()))

if __name__ == "__main__":
    main()
