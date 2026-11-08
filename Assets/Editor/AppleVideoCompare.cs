// Apple throwing reference comparison — standalone EditorWindow; no game/throwing code changed.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Globalization;

public sealed class AppleVideoCompare : EditorWindow
{
    private const int Width = 320, Height = 200, FrameCount = 276;
    private const double Fps = 70.086303;
    private const double SoundOnsetSeconds = 1.95; // First clear sample peak in supplied AVI
    private int frame;
    private bool playing, difference;
    private double lastTick;
    private Texture2D reference, unityShot;
    private int loadedFrame = -1;
    private string status = "Open this tool from Tools to run one automatic Apple throw through landing.";
    private bool recording;
    private bool autoRun;
    private ViewportLayoutEditor autoViewEdit;
    private double startTime;
    private readonly List<double> capturedTimes = new List<double>();
    private readonly List<string> capturedFiles = new List<string>();
    private int unityIndex;
    private int lastSyncedReferenceFrame = -1;
    private double alignmentOffset; // Unity elapsed seconds at the selected DOS reference frame.
    private string sessionFolder;
    private int capturedCount;
    private double lastCaptureTime;

    [MenuItem("Tools/Apple Video Comparison")]
    public static void Open()
    {
        var window = GetWindow<AppleVideoCompare>("Apple Video Compare");
        window.minSize = new Vector2(690, 440);
        EditorApplication.delayCall += window.BeginAutomaticComparison;
    }

    private static string ReferenceFolder => Path.Combine(Application.dataPath, "Editor/AppleReference");

    private void OnEnable() { EditorApplication.update += Tick; }
    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        if (reference != null) DestroyImmediate(reference);
        if (recording) StopRecording();
        if (unityShot != null) DestroyImmediate(unityShot);
    }

    private void Tick()
    {
        if (recording)
        {
            // Include exactly one final image after the real throw finishes.
            // No additional idle-frame tail is recorded.
            if (autoRun && autoViewEdit != null && !autoViewEdit.AppleVideoComparisonThrowIsActive())
            {
                RecordFrame(true);
                StopRecording();
                status = "Automatic Apple throw complete: final landing captured. " + status;
            }
            else RecordFrame();
        }
        if (!playing) return;
        double now = EditorApplication.timeSinceStartup;
        if (now - lastTick < 1.0 / Fps) return;
        int delta = Math.Max(1, (int)((now - lastTick) * Fps));
        lastTick = now;
        frame = Mathf.Min(FrameCount - 1, frame + delta);
        if (frame == FrameCount - 1) playing = false;
        Repaint();
    }

    private void LoadFrame()
    {
        if (loadedFrame == frame && reference != null) return;
        string path = Path.Combine(ReferenceFolder, $"Frame_{frame+1:0000}.png");
        if (!File.Exists(path)) { status = "Missing reference: " + path; return; }
        byte[] data = File.ReadAllBytes(path);
        if (reference == null) reference = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        if (!reference.LoadImage(data, false)) { status = "Cannot read " + path; return; }
        reference.filterMode = FilterMode.Point;
        loadedFrame = frame;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Original Apple throw (320×200) vs Unity viewport", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Opening this tool automatically records one real Apple throw until landing. Select DOS and Unity frames afterward to compare. Unity audio onset is not yet measured.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = recording ? new Color(1f,.65f,.65f) : Color.white;
            if (GUILayout.Button(recording ? "STOP Unity recording" : "START Unity recording", GUILayout.Width(200)))
            { if (recording) StopRecording(); else StartRecording(); }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Load last recording", GUILayout.Width(160))) LoadLastRecording();
            GUILayout.Label($"Captured: {capturedCount}");
        }
        if (capturedTimes.Count > 0)
        {
            unityIndex = EditorGUILayout.IntSlider("Unity recorded frame", unityIndex, 0, capturedTimes.Count-1);
            if (GUILayout.Button("Align this Unity frame with selected DOS frame", GUILayout.Width(360)))
            {
                alignmentOffset = capturedTimes[unityIndex] - frame/Fps;
                lastSyncedReferenceFrame = frame;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync Unity to DOS time", GUILayout.Width(180))) SyncToDOS();
                GUILayout.Label($"Unity t={capturedTimes[unityIndex]:F4}s   Offset={alignmentOffset:+0.0000;-0.0000;0.0000}s");
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("|<", GUILayout.Width(38))) { playing = false; frame = 0; }
            if (GUILayout.Button("<", GUILayout.Width(38))) { playing = false; frame = Mathf.Max(0, frame - 1); }
            if (GUILayout.Button(playing ? "Pause" : "Play", GUILayout.Width(72))) { playing = !playing; lastTick = EditorApplication.timeSinceStartup; }
            if (GUILayout.Button(">", GUILayout.Width(38))) { playing = false; frame = Mathf.Min(FrameCount - 1, frame + 1); }
            if (GUILayout.Button("Sound onset", GUILayout.Width(100))) { playing = false; frame = Mathf.Clamp(Mathf.RoundToInt((float)(SoundOnsetSeconds * Fps)), 0, FrameCount-1); }
            if (GUILayout.Button("Capture Unity viewport", GUILayout.Width(160))) CaptureViewport();
            difference = GUILayout.Toggle(difference, "Difference", GUILayout.Width(95));
        }
        frame = EditorGUILayout.IntSlider("Reference frame", frame, 0, FrameCount - 1);
        if (capturedTimes.Count > 0 && !recording && lastSyncedReferenceFrame != frame)
        {
            SyncToDOS(); lastSyncedReferenceFrame = frame;
        }
        EditorGUILayout.LabelField($"Reference t = {frame / Fps:F3} s   |   frame {frame+1}/{FrameCount}   |   Audio onset approx. frame {Math.Round(SoundOnsetSeconds*Fps)+1}");
        LoadFrame();
        if (capturedTimes.Count > 0 && !recording) LoadUnityFrame(unityIndex);
        float scale = Mathf.Max(1, Mathf.Min((position.width - 25f) / (2 * Width + 10), (position.height - 205f) / Height));
        scale = Mathf.Min(scale, 2f);
        float w = Width * scale, h = Height * scale;
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("ORIGINAL", GUILayout.Width(w));
            GUILayout.Label(difference ? "UNITY / DIFFERENCE" : "UNITY (last capture)", GUILayout.Width(w));
        }
        Rect area = GUILayoutUtility.GetRect(2*w+8, h, GUILayout.ExpandWidth(false));
        if (reference != null) GUI.DrawTexture(new Rect(area.x, area.y, w, h), reference, ScaleMode.StretchToFill, false);
        if (unityShot != null)
        {
            Rect right = new Rect(area.x+w+8, area.y, w, h);
            GUI.DrawTexture(right, unityShot, ScaleMode.StretchToFill, false);
            if (difference && reference != null)
            {
                // Side by side plus a translucent original overlay reveals misaligned apples.
                Color old = GUI.color;
                GUI.color = new Color(1, 1, 1, .45f);
                GUI.DrawTexture(right, reference, ScaleMode.StretchToFill, true);
                GUI.color = old;
            }
        }
        EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.LabelField("DOS: 3.938s / ~70.086 fps. Unity capture: actual Editor-update timestamps, not interpolated 70fps.", EditorStyles.miniLabel);
    }

    private void BeginAutomaticComparison()
    {
        if (this == null || recording) return;
        autoViewEdit = null;
        foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            if (window is ViewportLayoutEditor viewEdit) { autoViewEdit = viewEdit; break; }
        if (autoViewEdit == null)
        {
            status = "Open ViewEdit first, then choose Tools → Apple Video Comparison again.";
            Repaint();
            return;
        }
        // Verify the live viewport exists before consuming the existing Apple.
        if (!CaptureViewportInternal(false)) { Repaint(); return; }
        // Launch the actual game throw BEFORE recording: no setup/idle screenshots.
        // The first sample is taken immediately after the release is rendered.
        if (!autoViewEdit.BeginAppleVideoComparisonThrow())
        {
            status = "Cannot launch the Apple: inspect Unity Console for the exact error.";
        }
        else
        {
            autoRun = true;
            StartRecording(false);
            status = "Automatic capture: first release frame through final landing only.";
        }
        Repaint();
    }

    private static string CaptureRoot => Path.Combine(Application.dataPath, "Editor/AppleComparisonCaptures");

    private void StartRecording(bool restoreViewEditFocus = true)
    {
        if (recording) return;
        sessionFolder = Path.Combine(CaptureRoot, "Apple_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(sessionFolder);
        capturedTimes.Clear(); capturedFiles.Clear();
        capturedCount = 0; unityIndex = 0;
        startTime = EditorApplication.timeSinceStartup;
        lastCaptureTime = startTime - 1;
        recording = true;
        status = "Recording from the Apple release frame through the landing frame.";
        RecordFrame();
        // Return keyboard/mouse focus to the existing ViewEdit window after the button click.
        // Do not initialize ViewEdit or touch its cursor/item state.
        if (restoreViewEditFocus) EditorApplication.delayCall += FocusViewEditAfterStart;
    }

    private void FocusViewEditAfterStart()
    {
        if (!recording) return;
        foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
        {
            if (window != null && window.GetType().Name == "ViewportLayoutEditor")
            {
                window.Focus();
                status = "Recording. ViewEdit has focus; throw the Apple from its existing mouse hand.";
                return;
            }
        }
        status = "Recording, but ViewEdit is not open. Open ViewEdit and throw the Apple; recording continues.";
    }

    private void RecordFrame(bool force = false)
    {
        if (!recording) return;
        double now = EditorApplication.timeSinceStartup;
        // A snapshot per Editor update, capped at 70 fps. The timestamps reveal skipped frames.
        if (!force && now - lastCaptureTime < 1.0 / Fps) return;
        if (capturedCount >= 1000) { StopRecording(); status = "Stopped after 1000 frames (safety cap)."; return; }
        if (!CaptureViewportInternal(false)) return;
        double elapsed = now - startTime;
        string file = $"Unity_{capturedCount:0000}.png";
        File.WriteAllBytes(Path.Combine(sessionFolder, file), unityShot.EncodeToPNG());
        capturedFiles.Add(file); capturedTimes.Add(elapsed);
        capturedCount++;
        lastCaptureTime = now;
        Repaint();
    }

    private void StopRecording()
    {
        if (!recording) return;
        recording = false;
        autoRun = false;
        if (capturedCount == 0) { status = "No frames recorded; check that DungeonViewport is available."; return; }
        string manifest = Path.Combine(sessionFolder, "UnityCaptureTiming.csv");
        using (var writer = new StreamWriter(manifest))
        {
            writer.WriteLine("frame,elapsed_seconds,image");
            for (int i = 0; i < capturedCount; i++)
                writer.WriteLine($"{i},{capturedTimes[i].ToString("F6", CultureInfo.InvariantCulture)},{capturedFiles[i]}");
        }
        File.WriteAllText(Path.Combine(CaptureRoot, "last_session.txt"), sessionFolder);
        unityIndex = 0;
        status = $"Recorded {capturedCount} frames across {capturedTimes[capturedCount-1]:F3}s. Capture in {sessionFolder}. Adjust alignment by selecting matching DOS/Unity frames.";
        AssetDatabase.Refresh();
    }

    private void LoadLastRecording()
    {
        string pointer = Path.Combine(CaptureRoot, "last_session.txt");
        if (!File.Exists(pointer)) { status = "No saved Unity recording found."; return; }
        sessionFolder = File.ReadAllText(pointer).Trim();
        string csv = Path.Combine(sessionFolder, "UnityCaptureTiming.csv");
        if (!File.Exists(csv)) { status = "Missing timing file: " + csv; return; }
        capturedTimes.Clear(); capturedFiles.Clear();
        foreach (string row in File.ReadAllLines(csv))
        {
            if (row.StartsWith("frame,")) continue;
            var col = row.Split(',');
            if (col.Length < 3 || !double.TryParse(col[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double t)) continue;
            capturedTimes.Add(t); capturedFiles.Add(col[2]);
        }
        capturedCount = capturedTimes.Count;
        unityIndex = 0;
        status = $"Loaded {capturedCount} genuine Unity recorded frames from {sessionFolder}.";
        Repaint();
    }

    private void SyncToDOS()
    {
        if (capturedTimes.Count == 0) return;
        double target = frame/Fps + alignmentOffset;
        int best = 0; double bestError = double.MaxValue;
        for (int i = 0; i < capturedTimes.Count; i++)
        {
            double error = Math.Abs(capturedTimes[i] - target);
            if (error < bestError) { bestError = error; best = i; }
        }
        unityIndex = best;
    }

    private void LoadUnityFrame(int index)
    {
        if (index < 0 || index >= capturedFiles.Count || string.IsNullOrEmpty(sessionFolder)) return;
        string file = Path.Combine(sessionFolder, capturedFiles[index]);
        if (!File.Exists(file)) { status = "Missing captured Unity frame " + file; return; }
        if (unityShot == null) unityShot = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        unityShot.LoadImage(File.ReadAllBytes(file), false);
        unityShot.filterMode = FilterMode.Point;
    }

    private void CaptureViewport() { CaptureViewportInternal(true); }

    private bool CaptureViewportInternal(bool report)
    {
        RawImage target = null;
        foreach (var img in UnityEngine.Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None))
        {
            if (img == null || img.texture == null) continue;
            if (img.gameObject.name.IndexOf("DungeonViewport", StringComparison.OrdinalIgnoreCase) >= 0) { target = img; break; }
            if (target == null && img.texture.width == Width && img.texture.height == Height) target = img;
        }
        if (target == null) { status = "No live DungeonViewport RawImage found; open the dungeon scene and refresh ViewEdit."; return false; }
        Texture source = target.texture;
        if (source.width != Width || source.height != Height) { status = $"Viewport texture is {source.width}×{source.height}, expected 320×200."; return false; }
        RenderTexture temporary = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            if (unityShot == null) unityShot = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            unityShot.ReadPixels(new Rect(0,0,Width,Height), 0, 0);
            unityShot.Apply(false);
            unityShot.filterMode = FilterMode.Point;
            if (report) status = "Captured live 320×200 Unity RawImage. This still frame alone cannot prove timing.";
        }
        catch (Exception ex) { status = "Capture error: " + ex.Message; return false; }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
        if (report) Repaint();
        return true;
    }
}
