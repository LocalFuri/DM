// Dungeon Master Unity: request a full script rebuild when a JSON asset's content changes.
// Place at Assets/Editor/DungeonJsonRecompileWatcher.cs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

internal sealed class DungeonJsonRecompileWatcher : AssetPostprocessor
{
    private const string IgnoredSettings = "Assets/Data/ViewEditSettings.json";
    private static readonly HashSet<string> PendingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static bool requestScheduled;

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths,
        bool didDomainReload)
    {
        // The reload itself invokes this callback; it is not a new JSON edit.
        if (didDomainReload) return;

        CheckAssets(importedAssets);
        CheckAssets(movedAssets);
    }

    private static void CheckAssets(string[] paths)
    {
        if (paths == null) return;
        foreach (string path in paths)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;
            string normalized = path.Replace('\\', '/');
            if (string.Equals(normalized, IgnoredSettings, StringComparison.OrdinalIgnoreCase))
                continue;

            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), normalized);
            if (!File.Exists(absolutePath)) continue;

            string checksum;
            try
            {
                using (var sha = SHA256.Create())
                using (var stream = File.OpenRead(absolutePath))
                    checksum = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }
            catch (IOException ex)
            {
                Debug.LogWarning("JSON recompile watcher couldn't read " + normalized + ": " + ex.Message);
                continue;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogWarning("JSON recompile watcher couldn't read " + normalized + ": " + ex.Message);
                continue;
            }

            // EditorPrefs persists across assembly reloads and Unity restarts.
            // Include the project path to prevent collisions between projects.
            string key = "DungeonMaster.JsonRecompile.Hash." + Application.dataPath + "/" + normalized;
            if (EditorPrefs.GetString(key, "") == checksum) continue;
            EditorPrefs.SetString(key, checksum);
            PendingPaths.Add(normalized);
        }

        if (PendingPaths.Count > 0 && !requestScheduled)
        {
            requestScheduled = true;
            EditorApplication.delayCall += RequestRecompile;
        }
    }

    private static void RequestRecompile()
    {
        requestScheduled = false;
        if (PendingPaths.Count == 0) return;
        // Don't interrupt play mode. Keep the pending change and try once play mode ends.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= HandlePlayMode;
            EditorApplication.playModeStateChanged += HandlePlayMode;
            return;
        }

        string[] files = new string[PendingPaths.Count];
        PendingPaths.CopyTo(files);
        PendingPaths.Clear();
        Debug.Log("JSON changed: forcing Unity script recompilation (" + string.Join(", ", files) + ")");
        // A clean build is required; a normal request may skip unchanged C# files.
        CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
    }

    private static void HandlePlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= HandlePlayMode;
        if (PendingPaths.Count > 0 && !requestScheduled)
        {
            requestScheduled = true;
            EditorApplication.delayCall += RequestRecompile;
        }
    }
}
