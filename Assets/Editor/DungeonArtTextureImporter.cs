#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies consistent import settings to Dungeon Master artwork beneath Assets/Art.
/// Does not modify textures elsewhere in the Unity project.
/// </summary>
public sealed class DungeonArtTextureImporter : AssetPostprocessor
{
    private const string ArtFolder = "Assets/Art/";

    private static bool IsDungeonArtwork(string path)
    {
        return !string.IsNullOrEmpty(path) &&
            path.Replace('\\', '/').StartsWith(ArtFolder, System.StringComparison.OrdinalIgnoreCase);
    }

    private static void Configure(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 1f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;

        // Explicit platform overrides can defeat the default uncompressed setting.
        // Clear any overrides so all platforms inherit the common import format.
        string[] platformNames = { "Standalone", "Android", "iPhone", "WebGL" };
        foreach (string platformName in platformNames)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
            if (settings.overridden)
            {
                settings.overridden = false;
                importer.SetPlatformTextureSettings(settings);
            }
        }
    }

    private void OnPreprocessTexture()
    {
        if (!IsDungeonArtwork(assetPath))
            return;

        Configure((TextureImporter)assetImporter);
    }

    [MenuItem("Tools/Apply Pixel-Art Import Settings")]
    private static void ApplyToExistingArtwork()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art" });
        int updated = 0;
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsDungeonArtwork(path))
                    continue;

                if (EditorUtility.DisplayCancelableProgressBar(
                    "Dungeon Master artwork", "Applying import settings: " + path,
                    guids.Length == 0 ? 1f : (float)i / guids.Length))
                    break;

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                Configure(importer);
                importer.SaveAndReimport();
                updated++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        Debug.Log("[Dungeon Art Importer] Updated " + updated +
            " texture(s) under Assets/Art. Other assets were not changed.");
    }
}
#endif
