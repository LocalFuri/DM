#!/usr/bin/env python3
"""Hook S2: recover original DOS 14x19 pixels from Unity 16x19 image.
Checksum guard, backup, and atomic replacement. Python standard library only.
"""
from pathlib import Path
from datetime import datetime
from hashlib import sha256
import os

TARGET = Path(r"C:\Unity\DM\Assets\Editor\ViewportLayoutEditor.cs")
OLD_SHA256 = "aad9222b174d02a9e3502574f4ac86810eac0cc0e5b060ac403ebe1228c8df30"
NEW_SHA256 = "b8432395afe6770373dd6428892f19d4e5166ba960b3cd4a6c66888b03496987"
OLD_CODE = '    Texture2D cropped = CropWallDecorationToOpaqueBounds(source);'
NEW_CODE = '    // The Unity S1 asset is 16x19 with a one-column transparent left border\n    // and a one-column extra right border. DOS graphic 267 is exactly source\n    // columns 1..14 (14x19), INCLUDING its transparent trailing columns.\n    // Do not trim to opaque bounds: that incorrectly produces 9x19.\n    if (source.width != 16 || source.height != 19)\n    {\n      Debug.LogError("Hook S2: expected 16x19 padded source; got "\n          + source.width + "x" + source.height);\n      return null;\n    }\n    Color32[] fullPixels = source.GetPixels32();\n    Color32[] nativePixels = new Color32[14 * 19];\n    for (int sy = 0; sy < 19; sy++)\n      for (int sx = 0; sx < 14; sx++)\n        nativePixels[sy * 14 + sx] = fullPixels[sy * 16 + (sx + 1)];\n    Texture2D cropped = new Texture2D(14, 19, TextureFormat.RGBA32, false);\n    cropped.SetPixels32(nativePixels);\n    cropped.Apply(false, false);'

def apply(target):
    if not target.is_file():
        raise SystemExit('STOP: Unity source missing; nothing changed: ' + str(target))
    original = target.read_bytes()
    digest = sha256(original).hexdigest()
    if digest == NEW_SHA256:
        print('Already installed; nothing changed.')
        return
    if digest != OLD_SHA256:
        raise SystemExit('STOP: Source checksum differs from expected Hook S2 patch; nothing changed. SHA256=' + digest)
    before = OLD_CODE.encode('utf-8')
    after = NEW_CODE.encode('utf-8')
    if original.count(before) != 1:
        raise SystemExit('STOP: Expected Hook S2 source block not uniquely present; nothing changed.')
    replacement = original.replace(before, after)
    if sha256(replacement).hexdigest() != NEW_SHA256:
        raise SystemExit('STOP: Output checksum verification failed; nothing changed.')
    stamp = datetime.now().astimezone().strftime('%Y%m%d-%H%M%S')
    backup = target.with_name(target.name + '.before_hook_s2_native14_' + stamp + '.bak')
    if backup.exists():
        raise SystemExit('STOP: Backup already exists; nothing changed.')
    with backup.open('xb') as out:
        out.write(original)
    os.utime(backup, ns=(target.stat().st_atime_ns, target.stat().st_mtime_ns))
    tmp = target.with_name(target.name + '.hook_s2_native14_tmp')
    try:
        with tmp.open('xb') as out:
            out.write(replacement)
        os.replace(tmp, target)
    finally:
        if tmp.exists():
            tmp.unlink()
    print('Updated:', target)
    print('Backup:', backup)
    print('Hook S2: padded 16x19 -> original DOS native 14x19, columns 1..14')
    print('Unchanged: common 6-bit scaler, Hook S2 9x12 geometry, all other ornaments')
    print('SHA256:', sha256(target.read_bytes()).hexdigest())

if __name__ == '__main__':
    apply(TARGET)
