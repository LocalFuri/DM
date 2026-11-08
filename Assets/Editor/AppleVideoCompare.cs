// Apple throwing reference comparison — standalone EditorWindow; no game/throwing code changed.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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
    private string status = "Select Capture Unity viewport to compare with the reference.";

    [MenuItem("Tools/Dungeon Master/Apple Video Comparison")]
    public static void Open() { GetWindow<AppleVideoCompare>("Apple Video Compare").minSize = new Vector2(690, 440); }

    private static string ReferenceFolder => Path.Combine(Application.dataPath, "Editor/AppleReference");

    private void OnEnable() { EditorApplication.update += Tick; }
    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        if (reference != null) DestroyImmediate(reference);
        if (unityShot != null) DestroyImmediate(unityShot);
    }

    private void Tick()
    {
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
        EditorGUILayout.HelpBox("The AVI was decoded into 276 frames. Original audio first becomes prominent at ~1.95 s (frame ~137). Step to that frame, capture Unity, and compare pixel positions. The windows are not automatically time-locked.", MessageType.Info);
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
        EditorGUILayout.LabelField($"Reference t = {frame / Fps:F3} s   |   frame {frame+1}/{FrameCount}   |   Audio onset approx. frame {Math.Round(SoundOnsetSeconds*Fps)+1}");
        LoadFrame();
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
        EditorGUILayout.LabelField("Original recording: 3.938 s; video approx. 70.086 fps. Capture again after each Unity frame you want to compare.", EditorStyles.miniLabel);
    }

    private void CaptureViewport()
    {
        RawImage target = null;
        foreach (var img in UnityEngine.Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None))
        {
            if (img == null || img.texture == null) continue;
            if (img.gameObject.name.IndexOf("DungeonViewport", StringComparison.OrdinalIgnoreCase) >= 0) { target = img; break; }
            if (target == null && img.texture.width == Width && img.texture.height == Height) target = img;
        }
        if (target == null) { status = "No live DungeonViewport RawImage found; open the dungeon scene and refresh ViewEdit."; return; }
        Texture source = target.texture;
        if (source.width != Width || source.height != Height) { status = $"Viewport texture is {source.width}×{source.height}, expected 320×200."; return; }
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
            status = "Captured the live 320×200 Unity RawImage. Compare it to your selected reference frame.";
        }
        catch (Exception ex) { status = "Capture error: " + ex.Message; }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
        Repaint();
    }
}
