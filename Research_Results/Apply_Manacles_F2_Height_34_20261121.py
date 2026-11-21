#!/usr/bin/env python3
"""Apply the verified Manacles front-F2 rectangle height correction.
Changes exactly one line inside GetManaclesFrontF2Rect; does NOT replace the whole .cs file.
Compatible with the 2026-11-21 shared DOS 6-bit patched Unity source.
"""
from pathlib import Path
from datetime import datetime
from zoneinfo import ZoneInfo
import hashlib
import os

TARGET = Path(r"C:\Unity\DM\Assets\Editor\ViewportLayoutEditor.cs")
EXPECTED_SHA256 = "39024b22164d1daa30371c5b5a4bd20e4ef491d0a9ae4c5f6c8dd5a5783094c6"
RESULT_SHA256 = "57dafedb0e98afe6fbbd1bb1f3ca03e6235f16d293619dbbb385f05605974910"
OLD = b'  private static void GetManaclesFrontF2Rect(\n      out int x, out int y, out int width, out int height)\n  {\n    // EXPERIMENT ONLY (2026-11-20): screenshot-derived DOS F2 candidate.\n    // NOT verified from the original executable or accepted as a generic rule.\n    // The original 64x37 CSBwin-derived rectangle remains the baseline.\n    width = 60;\n    height = 35;\n    x = 82;\n    y = DisplayYToUnityY(33 + 40, height);\n  }'
NEW = b'  private static void GetManaclesFrontF2Rect(\n      out int x, out int y, out int width, out int height)\n  {\n    // EXPERIMENT ONLY (2026-11-20): screenshot-derived DOS F2 candidate.\n    // NOT verified from the original executable or accepted as a generic rule.\n    // The original 64x37 CSBwin-derived rectangle remains the baseline.\n    width = 60;\n    height = 34;\n    x = 82;\n    y = DisplayYToUnityY(33 + 40, height);\n  }'

if not TARGET.is_file():
    raise SystemExit("STOP: file not found: " + str(TARGET))
content = TARGET.read_bytes()
digest = hashlib.sha256(content).hexdigest()
if digest == RESULT_SHA256:
    print("Already installed; no changes:", TARGET)
    raise SystemExit(0)
if digest != EXPECTED_SHA256:
    raise SystemExit("STOP: source has changed since the verified DOS 6-bit patch; no changes made. SHA256=" + digest)
if content.count(OLD) != 1:
    raise SystemExit("STOP: expected Manacles F2 rectangle missing or duplicated; no changes made.")
updated = content.replace(OLD, NEW, 1)
if hashlib.sha256(updated).hexdigest() != RESULT_SHA256:
    raise SystemExit("STOP: generated result hash does not match; no changes made.")
# Save a distinct backup of current source (never replace earlier .before_dos6bit.bak).
stamp = datetime.now(ZoneInfo("Europe/Berlin")).strftime("%Y%m%d-%H%M%S")
backup = TARGET.with_name(TARGET.name + ".before_manacles_f2_height_" + stamp + ".bak")
if backup.exists():
    raise SystemExit("STOP: backup path exists: " + str(backup))
# exclusive create, preserving all current source bytes
with backup.open("xb") as f:
    f.write(content)
# Atomic replacement in same directory
scratch = TARGET.with_name(TARGET.name + ".dos_f2_height_tmp")
try:
    with scratch.open("xb") as f:
        f.write(updated)
    # Ensure patched file has new local modification time, not old file timestamp.
    os.replace(scratch, TARGET)
except Exception:
    scratch.unlink(missing_ok=True)
    raise
print("Updated:", TARGET)
print("Backup:", backup)
print("Change: Manacles front F2 height 35 -> 34; width 60, x 82 and display-top 73 unchanged")
print("SHA256:", RESULT_SHA256)
