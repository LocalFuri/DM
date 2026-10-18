// Assets/Editor/ItemArtworkAutoRegistrar.cs
// Registers strictly named F1 ground sprites without guessing artwork or
// changing existing explicitly authored mappings/anchors.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed class ItemArtworkAutoRegistrar : AssetPostprocessor
{
    private const string RegistryPath = "Assets/Editor/ItemArtworkRegistry.json";
    private static readonly string[] ArtFolders = { "Assets/Art/Armor", "Assets/Art/Weapons" };
    private static readonly Regex F1Filename = new Regex(
        @"^(?<name>.+)_F1_(?<width>[0-9]+)[x_](?<height>[0-9]+)\.png$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static bool queued;
    private static bool running;

    [Serializable] private sealed class Registry
    {
        public Entry[] entries;
        public Anchors groundF1Anchors;
    }
    [Serializable] private sealed class Entry
    {
        public string itemType;
        public string groundF1Path;
        public int width;
        public int height;
    }
    [Serializable] private sealed class Anchors { public Anchor left; public Anchor right; }
    [Serializable] private sealed class Anchor { public int x; public int y; }

    [MenuItem("Tools/Register Item Ground Artwork")]
    private static void RunFromMenu() { Synchronize(); }

    private static bool IsRelevant(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string p = path.Replace('\\', '/');
        foreach (string folder in ArtFolders)
            if (p.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)
                && p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
        string[] moved, string[] movedFrom)
    {
        if (running || queued) return;
        foreach (string[] paths in new[] { imported, deleted, moved, movedFrom })
            foreach (string path in paths)
                if (IsRelevant(path))
                {
                    queued = true;
                    EditorApplication.delayCall += DeferredSynchronize;
                    return;
                }
    }

    private static void DeferredSynchronize()
    {
        queued = false;
        Synchronize();
    }

    private static void Synchronize()
    {
        if (running) return;
        running = true;
        try
        {
            if (!File.Exists(RegistryPath))
            {
                Debug.LogError("[ItemArtworkAutoRegistrar] Missing " + RegistryPath
                    + "; refusing to create guessed anchors or an empty registry.");
                return;
            }
            Registry registry;
            try { registry = JsonUtility.FromJson<Registry>(File.ReadAllText(RegistryPath)); }
            catch (Exception ex)
            {
                Debug.LogError("[ItemArtworkAutoRegistrar] Invalid registry JSON: " + ex.Message);
                return;
            }
            if (registry == null || registry.entries == null || registry.groundF1Anchors == null
                || registry.groundF1Anchors.left == null || registry.groundF1Anchors.right == null)
            {
                Debug.LogError("[ItemArtworkAutoRegistrar] Registry entries or existing left/right anchors missing; no changes made.");
                return;
            }
            var entries = new List<Entry>(registry.entries);
            var byName = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            foreach (Entry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.itemType)
                    || byName.ContainsKey(Normalize(entry.itemType)))
                {
                    Debug.LogError("[ItemArtworkAutoRegistrar] Duplicate or invalid existing itemType; registry unchanged.");
                    return;
                }
                byName.Add(Normalize(entry.itemType), entry);
            }
            var discovered = new Dictionary<string, List<Entry>>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in ArtFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogError("[ItemArtworkAutoRegistrar] Required art folder missing: " + folder);
                    continue;
                }
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Match match = F1Filename.Match(Path.GetFileName(path));
                    if (!match.Success) continue;
                    string name = match.Groups["name"].Value.Replace('_', ' ');
                    if (!int.TryParse(match.Groups["width"].Value, out int width)
                        || !int.TryParse(match.Groups["height"].Value, out int height)
                        || width <= 0 || height <= 0)
                    {
                        Debug.LogError("[ItemArtworkAutoRegistrar] Invalid F1 dimensions: " + path);
                        continue;
                    }
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture == null || texture.width != width || texture.height != height)
                    {
                        Debug.LogError("[ItemArtworkAutoRegistrar] F1 PNG size mismatch: " + path
                            + "; filename says " + width + "x" + height + ". No registration made.");
                        continue;
                    }
                    string key = Normalize(name);
                    if (!discovered.TryGetValue(key, out List<Entry> matches))
                    {
                        matches = new List<Entry>();
                        discovered.Add(key, matches);
                    }
                    matches.Add(new Entry { itemType = name, groundF1Path = path, width = width, height = height });
                }
            }
            int added = 0;
            foreach (var pair in discovered)
            {
                if (pair.Value.Count != 1)
                {
                    Debug.LogError("[ItemArtworkAutoRegistrar] Ambiguous F1 artwork for '"
                        + pair.Key + "': " + string.Join(", ", pair.Value.ConvertAll(e => e.groundF1Path))
                        + ". No registration changed for this item.");
                    continue;
                }
                Entry found = pair.Value[0];
                if (byName.TryGetValue(pair.Key, out Entry existing))
                {
                    if (!string.Equals(existing.groundF1Path, found.groundF1Path, StringComparison.Ordinal)
                        || existing.width != found.width || existing.height != found.height)
                        Debug.LogError("[ItemArtworkAutoRegistrar] Existing explicit entry for '"
                            + existing.itemType + "' differs from discovered F1 image "
                            + found.groundF1Path + "; preserving existing mapping. Review manually.");
                    continue;
                }
                entries.Add(found);
                byName.Add(pair.Key, found);
                added++;
            }
            if (added > 0)
            {
                registry.entries = entries.ToArray();
                File.WriteAllText(RegistryPath, JsonUtility.ToJson(registry, true) + "\n");
                AssetDatabase.ImportAsset(RegistryPath);
            }
            Debug.Log("[ItemArtworkAutoRegistrar] Registered " + added + " new F1 item(s); "
                + entries.Count + " total. Existing entries and anchors preserved.");
        }
        finally { running = false; }
    }

    // The game's itemType uses spaces whereas filenames use underscores.
    private static string Normalize(string name) => name.Trim().Replace('_', ' ');
}
