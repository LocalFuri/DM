using System;
using System.IO;
using UnityEngine;

// Shared original PC DOS resource-696 placement resolver, extracted from
// GRAPHICS.DAT. It reads every 8-byte record from the original resource.
// No hard-coded ornament coordinates and no fallback to CSBwin positions.
public static class DosOrnamentLayout696
{
  private static byte[] data;

  private static byte[] Load()
  {
    if (data != null) return data;
    string path = Path.Combine(Application.dataPath,
        "Editor/DOS_Resource696.bin");
    if (!File.Exists(path))
      throw new FileNotFoundException(
          "Original PC DOS resource 696 missing", path);
    byte[] loaded = File.ReadAllBytes(path);
    if (loaded.Length != 9160 || BitConverter.ToUInt16(loaded, 0) != 0xFC0D
        || BitConverter.ToUInt16(loaded, 2) != 23)
      throw new InvalidDataException("Invalid DOS resource 696: " + path);
    data = loaded;
    return data;
  }

  private static void ReadCenter(int zone, out int centerX, out int centerY)
  {
    byte[] blob = Load();
    int cursor = 4 + 23 * 4;
    for (int i = 0; i < 23; i++)
    {
      int start = BitConverter.ToUInt16(blob, 4 + i * 4);
      int end = BitConverter.ToUInt16(blob, 6 + i * 4);
      if (end < start) throw new InvalidDataException("DOS layout range reversed");
      int count = end - start + 1;
      if (zone >= start && zone <= end)
      {
        int offset = cursor + (zone - start) * 8;
        if (offset + 8 > blob.Length)
          throw new InvalidDataException("Truncated DOS layout record");
        int type = BitConverter.ToInt16(blob, offset);
        int parent = BitConverter.ToInt16(blob, offset + 2);
        if (type != 0 || parent != 4)
          throw new InvalidDataException(
              "Unexpected ornament layout record type/parent for " + zone);
        centerX = BitConverter.ToInt16(blob, offset + 4);
        centerY = BitConverter.ToInt16(blob, offset + 6);
        return;
      }
      cursor += count * 8;
    }
    throw new InvalidDataException("DOS layout zone not found: " + zone);
  }

  // Layout index: 1004 + coordinateSet * 15 + perspectiveIndex.
  // X/Y are Unity framebuffer coordinates (origin lower left), while
  // the layout centers are viewport-local with origin at its top-left.
  public static void GetRect(
      int coordinateSet, int perspectiveIndex,
      int nativeWidth, int nativeHeight, int scale,
      out int x, out int y, out int width, out int height)
  {
    if (coordinateSet < 0 || perspectiveIndex < 0 || perspectiveIndex >= 15
        || nativeWidth <= 0 || nativeHeight <= 0 || scale <= 0)
      throw new ArgumentOutOfRangeException("DOS ornament geometry arguments");
    int zone = checked(1004 + coordinateSet * 15 + perspectiveIndex);
    ReadCenter(zone, out int centerX, out int centerY);
    width = checked((nativeWidth * scale + (scale >> 1)) >> 5);
    height = checked((nativeHeight * scale + (scale >> 1)) >> 5);
    if (width <= 0 || height <= 0)
      throw new InvalidDataException("Zero-sized DOS ornament bitmap");
    x = centerX - ((width + 1) >> 1);
    int viewportTopY = centerY - ((height + 1) >> 1);
    y = 200 - (33 + viewportTopY) - height;
  }
}
