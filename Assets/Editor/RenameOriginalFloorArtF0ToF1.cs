#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class RenameOriginalFloorArtF0ToF1
{
    private const string RegistryPath = "Assets/Editor/ItemArtworkRegistry.json";

    [MenuItem("Tools/Rename Original Floor Art F0 to F1")]
    public static void Run()
    {
        if (!File.Exists(RegistryPath))
        {
            Debug.LogError("ORIGINAL F1 MIGRATION: Registry missing: " + RegistryPath);
            return;
        }
        string originalJson = File.ReadAllText(RegistryPath);
        var sourcePaths = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(originalJson,
            "Assets/[^\\\"\\r\\n]+?_F0_[0-9]+x[0-9]+\\.png", RegexOptions.IgnoreCase))
            sourcePaths.Add(match.Value);

        if (sourcePaths.Count == 0)
        {
            Debug.Log("ORIGINAL F1 MIGRATION: No _F0_ paths in registry. Nothing to rename.");
            return;
        }
        var moves = new List<KeyValuePair<string, string>>();
        foreach (string source in sourcePaths)
        {
            string target = Regex.Replace(source, "_F0_", "_F1_", RegexOptions.IgnoreCase);
            bool sourceExists = AssetDatabase.LoadMainAssetAtPath(source) != null;
            bool targetExists = AssetDatabase.LoadMainAssetAtPath(target) != null;
            if (!sourceExists && !targetExists)
            {
                Debug.LogError("ORIGINAL F1 MIGRATION: Missing source and destination: " + source);
                return;
            }
            if (sourceExists && targetExists)
            {
                Debug.LogError("ORIGINAL F1 MIGRATION: Both names exist; resolve conflict manually: "
                    + source + " and " + target);
                return;
            }
            if (sourceExists) moves.Add(new KeyValuePair<string, string>(source, target));
        }

        var completed = new List<KeyValuePair<string, string>>();
        foreach (var move in moves)
        {
            string error = AssetDatabase.MoveAsset(move.Key, move.Value);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError("ORIGINAL F1 MIGRATION: Rename failed: " + move.Key + ": " + error);
                for (int i = completed.Count - 1; i >= 0; i--)
                {
                    var done = completed[i];
                    string rollback = AssetDatabase.MoveAsset(done.Value, done.Key);
                    if (!string.IsNullOrEmpty(rollback))
                        Debug.LogError("ORIGINAL F1 MIGRATION: Rollback failed for " + done.Value + ": " + rollback);
                }
                return;
            }
            completed.Add(move);
        }
        string updatedJson = originalJson;
        foreach (string source in sourcePaths)
            updatedJson = updatedJson.Replace(source,
                Regex.Replace(source, "_F0_", "_F1_", RegexOptions.IgnoreCase));
        try
        {
            File.WriteAllText(RegistryPath, updatedJson);
            AssetDatabase.ImportAsset(RegistryPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            Debug.LogError("ORIGINAL F1 MIGRATION: Asset renames completed but registry save failed. "
                + "Correct registry paths before rendering. Error: " + ex);
            return;
        }
        Debug.Log("ORIGINAL F1 MIGRATION: Updated " + sourcePaths.Count
            + " registry paths and renamed " + completed.Count
            + " assets. GUIDs preserved. Original F2/F3 algorithms unchanged.");
    }
}
#endif
