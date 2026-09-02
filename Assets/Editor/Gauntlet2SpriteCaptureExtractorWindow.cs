// Put this file in Assets/Editor/ in the Unity project.
// It reads the G2PX v3 files produced by gauntlet2_capture_with_palette.lua.
// The window exports transparent, temporal-change candidate crops. It does not
// decode the Atari graphics ROM tile order; that is a separate next step.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class Gauntlet2SpriteCaptureExtractorWindow : EditorWindow
{
    private const int HeaderSize = 18;
    private const int MotionObjectBytes = 0x2000;
    private const int PaletteBytes = 0x0200;
    private const int SlipBytes = 0x0080;
    private const int PerSampleNonPixelBytes = 4 + MotionObjectBytes + PaletteBytes + SlipBytes;

    private string capturePath = string.Empty;
    private G2PxCapture capture;
    private Texture2D selectedScreen;
    private int selectedSample;
    private int minimumChangedPixels = 250;
    private int cropMargin = 3;
    private readonly List<Candidate> candidates = new List<Candidate>();
    private Vector2 scroll;

    [MenuItem("Tools/Sprite Capture Extractor")]
    private static void Open() => GetWindow<Gauntlet2SpriteCaptureExtractorWindow>("Gauntlet II Extractor");

    private void OnDisable()
    {
        DestroyImmediate(selectedScreen);
        foreach (Candidate candidate in candidates) DestroyImmediate(candidate.Preview);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("G2PX v3 capture", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Input: gauntlet2_sprite_capture_compact.bin created by the MAME Lua script. " +
            "This first extractor identifies moving screen regions and exports transparent candidate crops. " +
            "It deliberately does not pretend to solve Atari's still-unknown non-linear graphics-ROM tile order.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(capturePath) ? "No capture selected" : capturePath, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUILayout.Button("Choose BIN", GUILayout.Width(100)))
            {
                string chosen = EditorUtility.OpenFilePanel("Choose G2PX capture", string.IsNullOrEmpty(capturePath) ? Application.dataPath : Path.GetDirectoryName(capturePath), "bin");
                if (!string.IsNullOrEmpty(chosen))
                {
                    capturePath = chosen;
                    LoadCapture();
                }
            }
        }

        if (capture == null) return;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Capture", $"G2PX v{capture.Version} · {capture.SampleCount} samples · {capture.Width} × {capture.Height} · RGBA32");
        EditorGUILayout.LabelField("Per sample", $"frame number + {capture.MotionObjectBytes:N0} object bytes + {capture.PaletteBytes:N0} palette bytes + {capture.SlipBytes:N0} SLIP bytes + {capture.PixelBytes:N0} screen-pixel bytes");

        int newSample = EditorGUILayout.IntSlider("Preview sample", selectedSample, 0, Mathf.Max(0, capture.SampleCount - 1));
        if (newSample != selectedSample)
        {
            selectedSample = newSample;
            RefreshScreenPreview();
        }
        EditorGUILayout.LabelField("MAME frame", capture.Samples[selectedSample].FrameNumber.ToString());

        if (selectedScreen != null)
        {
            float aspect = (float)capture.Width / capture.Height;
            Rect rect = GUILayoutUtility.GetAspectRect(aspect, GUILayout.MaxHeight(360));
            EditorGUI.DrawPreviewTexture(rect, selectedScreen, null, ScaleMode.ScaleToFit);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Candidate detection", EditorStyles.boldLabel);
        minimumChangedPixels = EditorGUILayout.IntSlider("Minimum changed pixels", minimumChangedPixels, 1, 10000);
        cropMargin = EditorGUILayout.IntSlider("Transparent margin", cropMargin, 0, 16);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find candidates in all samples")) FindCandidates();
            using (new EditorGUI.DisabledScope(candidates.Count == 0))
            {
                if (GUILayout.Button("Export transparent PNGs")) ExportCandidates();
            }
        }

        if (candidates.Count == 0) return;
        EditorGUILayout.LabelField($"{candidates.Count} candidates (deduplicated by changed-pixel mask)", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        const float previewSize = 100f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 24f) / 124f));
        for (int start = 0; start < candidates.Count; start += columns)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int index = start; index < Mathf.Min(start + columns, candidates.Count); index++)
                {
                    Candidate candidate = candidates[index];
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(118)))
                    {
                        GUILayout.Label(candidate.Preview, GUILayout.Width(previewSize), GUILayout.Height(previewSize));
                        GUILayout.Label($"F{candidate.FrameNumber}  {candidate.Rect.width}×{candidate.Rect.height}", EditorStyles.miniLabel);
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void LoadCapture()
    {
        try
        {
            capture = G2PxCapture.Load(capturePath);
            selectedSample = 0;
            ClearCandidates();
            RefreshScreenPreview();
            Debug.Log($"Gauntlet II: loaded {capture.SampleCount} G2PX samples from {capturePath}");
        }
        catch (Exception exception)
        {
            capture = null;
            DestroyImmediate(selectedScreen);
            selectedScreen = null;
            EditorUtility.DisplayDialog("Gauntlet II capture", exception.Message, "OK");
        }
    }

    private void RefreshScreenPreview()
    {
        DestroyImmediate(selectedScreen);
        selectedScreen = capture.CreateScreenTexture(selectedSample);
    }

    private void FindCandidates()
    {
        ClearCandidates();
        var fingerprints = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            for (int sampleIndex = 0; sampleIndex < capture.SampleCount; sampleIndex++)
            {
                bool[] changed = capture.BuildTemporalDifferenceMask(sampleIndex);
                foreach (RectInt component in FindComponents(changed, capture.Width, capture.Height, minimumChangedPixels))
                {
                    Candidate candidate = capture.CreateCandidate(sampleIndex, component, cropMargin);
                    if (!fingerprints.Add(candidate.Fingerprint))
                    {
                        DestroyImmediate(candidate.Preview);
                        continue;
                    }
                    candidates.Add(candidate);
                }
            }
            candidates.Sort((a, b) => a.FrameNumber.CompareTo(b.FrameNumber));
            Debug.Log($"Gauntlet II: found {candidates.Count} unique moving-region candidates.");
        }
        catch (Exception exception)
        {
            ClearCandidates();
            EditorUtility.DisplayDialog("Candidate extraction failed", exception.Message, "OK");
        }
    }

    private void ExportCandidates()
    {
        string folder = EditorUtility.OpenFolderPanel("Export candidate PNGs", Application.dataPath, "Gauntlet2Candidates");
        if (string.IsNullOrEmpty(folder)) return;

        for (int index = 0; index < candidates.Count; index++)
        {
            Candidate candidate = candidates[index];
            string filename = $"Candidate_{index:D3}_Frame_{candidate.FrameNumber}_X{candidate.Rect.x}_Y{candidate.Rect.y}.png";
            File.WriteAllBytes(Path.Combine(folder, filename), candidate.Preview.EncodeToPNG());
        }
        AssetDatabase.Refresh();
        EditorUtility.RevealInFinder(folder);
        Debug.Log($"Gauntlet II: exported {candidates.Count} transparent PNG candidates to {folder}");
    }

    private void ClearCandidates()
    {
        foreach (Candidate candidate in candidates) DestroyImmediate(candidate.Preview);
        candidates.Clear();
    }

    private static IEnumerable<RectInt> FindComponents(bool[] mask, int width, int height, int minimumPixels)
    {
        var visited = new bool[mask.Length];
        var queue = new Queue<int>();
        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || visited[start]) continue;
            int minX = width, minY = height, maxX = 0, maxY = 0, count = 0;
            visited[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int x = current % width;
                int y = current / width;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                count++;
                TryEnqueue(x - 1, y); TryEnqueue(x + 1, y); TryEnqueue(x, y - 1); TryEnqueue(x, y + 1);
            }
            if (count >= minimumPixels)
                yield return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);

            void TryEnqueue(int x, int y)
            {
                if (x < 0 || x >= width || y < 0 || y >= height) return;
                int next = y * width + x;
                if (!mask[next] || visited[next]) return;
                visited[next] = true;
                queue.Enqueue(next);
            }
        }
    }

    private sealed class Candidate
    {
        public readonly int FrameNumber;
        public readonly RectInt Rect;
        public readonly Texture2D Preview;
        public readonly string Fingerprint;
        public Candidate(int frameNumber, RectInt rect, Texture2D preview, string fingerprint)
        {
            FrameNumber = frameNumber; Rect = rect; Preview = preview; Fingerprint = fingerprint;
        }
    }

    private sealed class G2PxCapture
    {
        public readonly int Version, MotionObjectBytes, PaletteBytes, SlipBytes, Interval, Width, Height, PixelBytes;
        public readonly Sample[] Samples;
        private G2PxCapture(int version, int motionObjectBytes, int paletteBytes, int slipBytes, int interval, int width, int height, int pixelBytes, Sample[] samples)
        {
            Version = version; MotionObjectBytes = motionObjectBytes; PaletteBytes = paletteBytes; SlipBytes = slipBytes;
            Interval = interval; Width = width; Height = height; PixelBytes = pixelBytes; Samples = samples;
        }

        public static G2PxCapture Load(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < HeaderSize || bytes[0] != 'G' || bytes[1] != '2' || bytes[2] != 'P' || bytes[3] != 'X')
                throw new InvalidDataException("This is not a G2PX capture file.");
            int version = ReadU16(bytes, 4);
            int objectBytes = ReadU16(bytes, 6);
            int paletteBytes = ReadU16(bytes, 8);
            int slipBytes = ReadU16(bytes, 10);
            int interval = ReadU16(bytes, 12);
            int width = ReadU16(bytes, 14);
            int height = ReadU16(bytes, 16);
            if (version != 3) throw new InvalidDataException($"G2PX version {version} is not supported; expected version 3.");
            if (objectBytes != Gauntlet2SpriteCaptureExtractorWindow.MotionObjectBytes
                || paletteBytes != Gauntlet2SpriteCaptureExtractorWindow.PaletteBytes
                || slipBytes != Gauntlet2SpriteCaptureExtractorWindow.SlipBytes)
                throw new InvalidDataException("Unexpected G2PX motion-object, palette, or SLIP size.");
            int fixedBytes = 4 + objectBytes + paletteBytes + slipBytes;
            int payload = bytes.Length - HeaderSize;
            int rgbaBytes = width * height * 4;
            int recordBytes = fixedBytes + rgbaBytes;
            if (width <= 0 || height <= 0 || payload <= 0 || payload % recordBytes != 0)
                throw new InvalidDataException("The file length does not match whole RGBA32 G2PX samples.");
            int sampleCount = payload / recordBytes;
            var samples = new Sample[sampleCount];
            int offset = HeaderSize;
            for (int index = 0; index < sampleCount; index++)
            {
                int frame = ReadI32(bytes, offset);
                int pixelOffset = offset + fixedBytes;
                var rgba = new Color32[width * height];
                for (int pixel = 0; pixel < rgba.Length; pixel++)
                {
                    int source = pixelOffset + pixel * 4;
                    rgba[pixel] = new Color32(bytes[source + 2], bytes[source + 1], bytes[source], bytes[source + 3]);
                }
                samples[index] = new Sample(frame, rgba);
                offset += recordBytes;
            }
            return new G2PxCapture(version, objectBytes, paletteBytes, slipBytes, interval, width, height, rgbaBytes, samples);
        }

        public Texture2D CreateScreenTexture(int sampleIndex)
        {
            Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = $"G2PX_Frame_{Samples[sampleIndex].FrameNumber}" };
            texture.SetPixels32(Samples[sampleIndex].Pixels);
            texture.Apply(false, false);
            return texture;
        }

        public bool[] BuildTemporalDifferenceMask(int sampleIndex)
        {
            var mask = new bool[Width * Height];
            Color32[] current = Samples[sampleIndex].Pixels;
            Color32[] previous = Samples[Mathf.Max(0, sampleIndex - 1)].Pixels;
            Color32[] next = Samples[Mathf.Min(SampleCount - 1, sampleIndex + 1)].Pixels;
            for (int pixel = 0; pixel < mask.Length; pixel++)
                mask[pixel] = Different(current[pixel], previous[pixel]) || Different(current[pixel], next[pixel]);
            return mask;
        }

        public Candidate CreateCandidate(int sampleIndex, RectInt sourceRect, int margin)
        {
            int xMin = Mathf.Max(0, sourceRect.xMin - margin);
            int yMin = Mathf.Max(0, sourceRect.yMin - margin);
            int xMax = Mathf.Min(Width, sourceRect.xMax + margin);
            int yMax = Mathf.Min(Height, sourceRect.yMax + margin);
            RectInt rect = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
            bool[] changed = BuildTemporalDifferenceMask(sampleIndex);
            var crop = new Color32[rect.width * rect.height];
            var fingerprintBytes = new List<byte>(rect.width * rect.height * 2);
            for (int y = 0; y < rect.height; y++)
            for (int x = 0; x < rect.width; x++)
            {
                int sourceIndex = (rect.y + y) * Width + rect.x + x;
                int targetIndex = y * rect.width + x;
                Color32 color = Samples[sampleIndex].Pixels[sourceIndex];
                crop[targetIndex] = changed[sourceIndex] ? color : new Color32(0, 0, 0, 0);
                if (changed[sourceIndex]) { fingerprintBytes.Add(color.r); fingerprintBytes.Add(color.g); fingerprintBytes.Add(color.b); }
            }
            var texture = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixels32(crop); texture.Apply(false, false);
            string fingerprint = rect.width + "x" + rect.height + ":" + Convert.ToBase64String(fingerprintBytes.ToArray());
            return new Candidate(Samples[sampleIndex].FrameNumber, rect, texture, fingerprint);
        }

        public int SampleCount => Samples.Length;
        private static bool Different(Color32 a, Color32 b) => a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a;
        private static int ReadU16(byte[] bytes, int offset) => (bytes[offset] << 8) | bytes[offset + 1];
        private static int ReadI32(byte[] bytes, int offset) => (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

        public sealed class Sample
        {
            public readonly int FrameNumber;
            public readonly Color32[] Pixels;
            public Sample(int frameNumber, Color32[] pixels) { FrameNumber = frameNumber; Pixels = pixels; }
        }
    }
}
