#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RenameCsbFoodArtwork
{
    private const string RegistryPath = "Assets/Editor/ItemArtworkRegistry.json";
    private const string Folder = "Assets/Art/Food/";
    private static readonly string[] Items = {
        "Apple_32x13", "Bread_32x16", "Cheese_32x15", "Corn_32x10",
        "Dragon_Steak_32x10", "Drumstick_32x11", "Screamer_Slice_32x24", "Worm_Round_32x23"
    };

    [MenuItem("Tools/Dungeon Master/Rename Food Source Art to F0")]
    public static void Run()
    {
        if (!File.Exists(RegistryPath)) {
            Debug.LogError("FOOD F0 RENAME: registry missing: " + RegistryPath);
            return;
        }
        string json = File.ReadAllText(RegistryPath);
        var moves = new List<string[]>();
        foreach (string item in Items) {
            int split = item.LastIndexOf('_');
            string name = item.Substring(0, split);
            string dimensions = item.Substring(split + 1);
            string finalPath = Folder + name + "_F0_" + dimensions + ".png";
            string oldCsb = Folder + name + "_csb_F1_" + dimensions + ".png";
            string oldClean = Folder + name + "_F1_" + dimensions + ".png";
            string oldPath = null;
            // Support either migration state without guessing or loading substitute artwork.
            if (json.Contains("\"groundF1Path\": \"" + oldCsb + "\"")) oldPath = oldCsb;
            if (json.Contains("\"groundF1Path\": \"" + oldClean + "\"")) {
                if (oldPath != null) {
                    Debug.LogError("FOOD F0 RENAME: conflicting registry paths for " + name);
                    return;
                }
                oldPath = oldClean;
            }
            if (oldPath == null) {
                Debug.LogError("FOOD F0 RENAME: missing/unsupported original registry mapping for " + name);
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(oldPath) == null) {
                Debug.LogError("FOOD F0 RENAME: source asset missing: " + oldPath);
                return;
            }
            if (File.Exists(finalPath) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(finalPath) != null) {
                Debug.LogError("FOOD F0 RENAME: destination already exists: " + finalPath);
                return;
            }
            moves.Add(new[] { oldPath, finalPath });
        }
        var moved = new List<string[]>();
        foreach (string[] pair in moves) {
            string error = AssetDatabase.MoveAsset(pair[0], pair[1]);
            if (!string.IsNullOrEmpty(error)) {
                Debug.LogError("FOOD F0 RENAME: failed " + pair[0] + ": " + error);
                for (int i = moved.Count - 1; i >= 0; i--) {
                    string rollback = AssetDatabase.MoveAsset(moved[i][1], moved[i][0]);
                    if (!string.IsNullOrEmpty(rollback)) Debug.LogError("FOOD F0 RENAME: rollback FAILED " + moved[i][1] + ": " + rollback);
                }
                AssetDatabase.Refresh();
                return;
            }
            moved.Add(pair);
        }
        foreach (string[] pair in moves) json = json.Replace(pair[0], pair[1]);
        File.WriteAllText(RegistryPath, json);
        AssetDatabase.ImportAsset(RegistryPath);
        AssetDatabase.Refresh();
        Debug.Log("FOOD F0 RENAME: renamed 8 assets and updated ItemArtworkRegistry.json; Unity GUIDs retained.");
    }
}
#endif
