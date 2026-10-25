#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Standalone diagnostic. Does not modify the live dungeon renderer or its settings.
public static class PC34AppleOffsetDiagnostic
{
    private const string Source = "Assets/Editor/Apple_F0_32x13.png";
    private const string Prefix = "Assets/Editor/PC34_Apple_Offset_";
    private const int DestWidth = 21;
    private const int DestHeight = 8;
    private static readonly int[] Offsets = { -16, 0, 16 }; // 6-bit fixed-point quarter-pixel phases

    [MenuItem("Tools/Generate PC34 Apple Offset Tests")]
    public static void Generate()
    {
        Texture2D source = null;
        try
        {
            if (!File.Exists(Source))
                throw new FileNotFoundException("Required 32x13 padded Apple missing: " + Source);
            source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!source.LoadImage(File.ReadAllBytes(Source), false))
                throw new InvalidDataException("Could not decode Apple PNG: " + Source);
            if (source.width != 32 || source.height != 13)
                throw new InvalidDataException("Expected 32x13 source, found " + source.width + "x" + source.height);

            Color32[] src = source.GetPixels32(); // Unity stores pixels bottom-up.
            int stepX = (32 << 6) / DestWidth;
            int stepY = (13 << 6) / DestHeight;
            var html = new StringBuilder();
            html.AppendLine("<!doctype html><html><head><meta charset='utf-8'><title>PC34 Apple offset experiments</title><style>body{background:#20252b;color:#eee;font:16px sans-serif}main{display:grid;grid-template-columns:repeat(3,1fr);gap:18px;max-width:900px}.test{border:1px solid #555;padding:12px}img{width:336px;max-width:100%;image-rendering:pixelated;background:repeating-conic-gradient(#888 0% 25%,#555 0% 50%) 0 0/24px 24px}</style></head><body><h2>Apple 21x8 — 6-bit sampling phase tests</h2><p>These are full padded bitmaps. Baseline is X0_Y0. X/Y offsets are signed units of 1/64 source pixel, not screen-position shifts. All outputs remain 21x8 and are not cropped.</p><main>");
            int written = 0;
            foreach (int offsetY in Offsets)
            foreach (int offsetX in Offsets)
            {
                var dst = new Color32[DestWidth * DestHeight];
                int firstY = (stepY >> 1) + offsetY;
                for (int yTop = 0; yTop < DestHeight; ++yTop)
                {
                    int ySourceTop = Clamp((firstY + yTop * stepY) >> 6, 0, 12);
                    for (int x = 0; x < DestWidth; ++x)
                    {
                        int xSource = Clamp(((stepX >> 1) + offsetX + x * stepX) >> 6, 0, 31);
                        dst[(DestHeight - 1 - yTop) * DestWidth + x] = src[(12 - ySourceTop) * 32 + xSource];
                    }
                }
                var texture = new Texture2D(DestWidth, DestHeight, TextureFormat.RGBA32, false, true);
                try
                {
                    texture.SetPixels32(dst);
                    texture.filterMode = FilterMode.Point;
                    texture.Apply(false, false);
                    string tag = "X" + PhaseName(offsetX) + "_Y" + PhaseName(offsetY);
                    string filename = "PC34_Apple_Offset_" + tag + "_21x8.png";
                    File.WriteAllBytes("Assets/Editor/" + filename, texture.EncodeToPNG());
                    html.Append("<div class='test'><b>").Append(tag).Append(offsetX == 0 && offsetY == 0 ? " (baseline)" : "").Append("</b><p><img alt='").Append(tag).Append("' src='").Append(filename).Append("'></p></div>\n");
                    ++written;
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            html.AppendLine("</main></body></html>");
            File.WriteAllText(Prefix + "index.html", html.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log("PC34 APPLE OFFSET TEST: generated " + written + " 21x8 PNGs and Assets/Editor/PC34_Apple_Offset_index.html. Original renderer unchanged. Baseline X0_Y0 uses the existing half-step sampler.");
        }
        catch (Exception ex) { Debug.LogError("PC34 APPLE OFFSET TEST FAILED: " + ex); }
        finally { if (source != null) UnityEngine.Object.DestroyImmediate(source); }
    }

    private static int Clamp(int value, int min, int max) { return Math.Min(max, Math.Max(min, value)); }
    private static string PhaseName(int phase) { return phase < 0 ? "m" + (-phase) : phase > 0 ? "p" + phase : "0"; }
}
#endif
