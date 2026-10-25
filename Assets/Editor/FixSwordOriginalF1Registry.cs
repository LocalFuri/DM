#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class FixSwordOriginalF1Registry
{
    private const string Registry = "Assets/Editor/ItemArtworkRegistry.json";
    private const string NewArt = "Assets/Art/Weapons/Sword_F1_80x8.png";

    [MenuItem("Tools/Fix Sword Original F1 Registration")]
    public static void Run()
    {
        if (AssetDatabase.LoadMainAssetAtPath(NewArt) == null)
        {
            Debug.LogError("SWORD F1 FIX: Original artwork not found: " + NewArt);
            return;
        }
        if (!File.Exists(Registry))
        {
            Debug.LogError("SWORD F1 FIX: Registry not found: " + Registry);
            return;
        }

        string json = File.ReadAllText(Registry);
        string entryPattern = @"\{\s*""itemType""\s*:\s*""Sword""\s*,\s*""groundF1Path""\s*:\s*""(?<path>[^""]+)""\s*,\s*""width""\s*:\s*(?<width>\d+)\s*,\s*""height""\s*:\s*(?<height>\d+)\s*\}";
        MatchCollection matches = Regex.Matches(json, entryPattern);
        if (matches.Count != 1)
        {
            Debug.LogError("SWORD F1 FIX: Expected exactly one explicit Sword entry, found " + matches.Count + ". No changes made.");
            return;
        }
        Match m = matches[0];
        string oldPath = m.Groups["path"].Value;
        if (oldPath != "Assets/Art/Weapons/Sword_F1_72x8.png" && oldPath != NewArt)
        {
            Debug.LogError("SWORD F1 FIX: Sword artwork has an unexpected path: " + oldPath + ". No changes made.");
            return;
        }
        if (m.Groups["height"].Value != "8")
        {
            Debug.LogError("SWORD F1 FIX: Unexpected Sword height; no changes made.");
            return;
        }
        if (oldPath == NewArt && m.Groups["width"].Value == "80")
        {
            Debug.Log("SWORD F1 FIX: Sword is already correctly registered at " + NewArt);
            return;
        }

        string entry = m.Value;
        entry = entry.Replace("\"" + oldPath + "\"", "\"" + NewArt + "\"");
        entry = Regex.Replace(entry, @"(""width""\s*:\s*)\d+", "$1" + "80");
        string updated = json.Substring(0, m.Index) + entry + json.Substring(m.Index + m.Length);
        File.WriteAllText(Registry, updated);
        AssetDatabase.ImportAsset(Registry, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        Debug.Log("SWORD F1 FIX: Updated only 'Sword' to " + NewArt + " (80x8). Other entries preserved.");
    }
}
#endif
