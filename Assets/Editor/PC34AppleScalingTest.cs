// Standalone diagnostic for Dungeon Master pixel-shrinking research.
// Does NOT change ViewportLayoutEditor or the current CSBwin F2 renderer.
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PC34AppleScalingTest
{
    private const string SourcePath = "Assets/Editor/Apple_F0_32x13.png";
    private const string OutputDirectory = "Assets/Editor/PC34_Apple_Scaling_Test";
    private static readonly int[] Factors = { 32, 27, 21, 18, 14, 12 };
    private static readonly string[] Names = { "Reference_Full", "Scale_27", "Scale_21", "Scale_18", "Scale_14", "Scale_12" };

    [MenuItem("Tools/Dungeon Master/Generate PC34 Apple Scale Test")]
    public static void Generate()
    {
        try
        {
            string absoluteSource = Path.GetFullPath(SourcePath);
            if (!File.Exists(absoluteSource))
                throw new FileNotFoundException("Missing required Apple source; copy the padded 32x13 PNG to " + SourcePath, absoluteSource);

            byte[] bytes = File.ReadAllBytes(absoluteSource);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!source.LoadImage(bytes, false))
                throw new InvalidDataException("Could not read PNG: " + SourcePath);
            if (source.width != 32 || source.height != 13)
                throw new InvalidDataException("Expected padded 32x13 Apple, got " + source.width + "x" + source.height);
            Color32[] input = source.GetPixels32(); // Unity: bottom-up
            if (input[0].a != 0)
                Debug.LogWarning("PC34 APPLE TEST: lower-left corner is opaque. Check the transparency before comparing.");

            Directory.CreateDirectory(OutputDirectory);
            for (int i = 0; i < Factors.Length; ++i)
            {
                int factor = Factors[i];
                // Experimental dimensions from stored bitmap * factor / 32.
                // These factors are NOT yet proven to correspond directly to F1/F2/F3.
                int width = source.width * factor / 32;
                int height = source.height * factor / 32;
                if (width < 1 || height < 1) throw new InvalidDataException("Invalid scaled dimensions");
                Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                result.filterMode = FilterMode.Point;
                result.wrapMode = TextureWrapMode.Clamp;
                Color32[] dest = Shrink(input, source.width, source.height, width, height);
                result.SetPixels32(dest);
                result.Apply(false, false);
                string output = OutputDirectory + "/Apple_" + Names[i] + "_" + width + "x" + height + ".png";
                File.WriteAllBytes(output, result.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(result);
                Debug.Log("PC34 APPLE TEST: wrote " + output);
            }
            UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.Refresh();
            Debug.Log("PC34 APPLE TEST: six diagnostic images generated. The existing renderer was not modified.");
        }
        catch (Exception e)
        {
            Debug.LogError("PC34 APPLE TEST FAILED: " + e);
        }
    }

    // BLTSHRNK.C PC C implementation: six fractional bits, half-step phase.
    // Take source pixels, do not average colors or crop transparent padding.
    private static Color32[] Shrink(Color32[] input, int sw, int sh, int dw, int dh)
    {
        Color32[] output = new Color32[dw * dh];
        int sxStep = (sw << 6) / dw;
        int syStep = (sh << 6) / dh;
        int syPos = syStep >> 1;
        for (int yTop = 0; yTop < dh; yTop++)
        {
            int srcYTop = Math.Min(sh - 1, syPos >> 6);
            int srcYBottom = sh - 1 - srcYTop;
            int dstYBottom = dh - 1 - yTop;
            int sxPos = sxStep >> 1;
            for (int x = 0; x < dw; x++)
            {
                int srcX = Math.Min(sw - 1, sxPos >> 6);
                output[dstYBottom * dw + x] = input[srcYBottom * sw + srcX];
                sxPos += sxStep;
            }
            syPos += syStep;
        }
        return output;
    }
}
#endif
