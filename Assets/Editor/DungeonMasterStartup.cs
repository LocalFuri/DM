#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dungeon Master editor-only startup preferences. Never modifies player build settings.
/// Place this file and Progression.json in Assets/Editor/.
/// </summary>
[InitializeOnLoad]
internal static class DungeonMasterStartup
{
    private const string SettingsPath = "Assets/Editor/Progression.json";
    private const double RetryIntervalSeconds = 1.0;
    private const int MaxAttempts = 15;
    private static int attempts;
    private static double nextAttempt;

    [Serializable]
    private sealed class Settings
    {
        public DevelopmentDefaults developmentDefaults;
    }

    [Serializable]
    private sealed class DevelopmentDefaults
    {
        public GameViewSettings gameView;
    }

    [Serializable]
    private sealed class GameViewSettings
    {
        public int width;
        public int height;
    }

    static DungeonMasterStartup()
    {
        // Defer until Unity has restored Editor windows after project load/domain reload.
        EditorApplication.delayCall += Begin;
    }

    private static void Begin()
    {
        attempts = 0;
        nextAttempt = EditorApplication.timeSinceStartup;
        EditorApplication.update -= ApplyWhenReady;
        EditorApplication.update += ApplyWhenReady;
    }

    private static void ApplyWhenReady()
    {
        if (EditorApplication.timeSinceStartup < nextAttempt)
            return;
        nextAttempt = EditorApplication.timeSinceStartup + RetryIntervalSeconds;
        attempts++;

        int width, height;
        if (!ReadResolution(out width, out height))
        {
            EditorApplication.update -= ApplyWhenReady;
            return;
        }

        string problem;
        if (TrySelectSize(width, height, out problem))
        {
            EditorApplication.update -= ApplyWhenReady;
            Debug.Log("[DungeonMasterStartup] Game view set to " + width + "x" + height + " from " + SettingsPath + ".");
            return;
        }

        if (attempts >= MaxAttempts)
        {
            EditorApplication.update -= ApplyWhenReady;
            Debug.LogWarning("[DungeonMasterStartup] Could not select " + width + "x" + height + " after " + MaxAttempts + " attempts: " + problem + ". Select the preset manually or check whether the Game window is open.");
        }
    }

    private static bool ReadResolution(out int width, out int height)
    {
        width = height = 0;
        try
        {
            if (!File.Exists(SettingsPath))
            {
                Debug.LogError("[DungeonMasterStartup] Missing settings file: " + SettingsPath);
                return false;
            }
            Settings settings = JsonUtility.FromJson<Settings>(File.ReadAllText(SettingsPath));
            if (settings == null || settings.developmentDefaults == null || settings.developmentDefaults.gameView == null)
            {
                Debug.LogError("[DungeonMasterStartup] Missing developmentDefaults.gameView in " + SettingsPath);
                return false;
            }
            width = settings.developmentDefaults.gameView.width;
            height = settings.developmentDefaults.gameView.height;
            if (width <= 0 || height <= 0)
            {
                Debug.LogError("[DungeonMasterStartup] Invalid Game view width/height in " + SettingsPath);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[DungeonMasterStartup] Failed reading " + SettingsPath + ": " + ex);
            return false;
        }
    }

    private static bool TrySelectSize(int width, int height, out string problem)
    {
        problem = "Unity internal GameView API not available";
        try
        {
            Assembly editorAssembly = typeof(Editor).Assembly;
            Type gameViewType = editorAssembly.GetType("UnityEditor.GameView");
            Type sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
            Type groupType = editorAssembly.GetType("UnityEditor.GameViewSizeGroupType");
            if (gameViewType == null || sizesType == null || groupType == null)
                return false;

            EditorWindow gameView = null;
            foreach (UnityEngine.Object window in Resources.FindObjectsOfTypeAll(gameViewType))
            {
                gameView = window as EditorWindow;
                if (gameView != null) break;
            }
            if (gameView == null)
            {
                problem = "Game window has not opened";
                return false;
            }

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo instanceProperty = sizesType.GetProperty("instance", flags);
            object sizes = instanceProperty != null ? instanceProperty.GetValue(null, null) : null;
            MethodInfo getGroup = sizesType.GetMethod("GetGroup", flags);
            if (sizes == null || getGroup == null)
                return false;

            // Game View's current group differs by platform. Use the GameView's own
            // current group if exposed, otherwise use Standalone (desktop editor).
            PropertyInfo currentGroup = gameViewType.GetProperty("currentSizeGroupType", flags);
            object groupEnum = currentGroup != null ? currentGroup.GetValue(gameView, null) : Enum.Parse(groupType, "Standalone");
            object group = getGroup.Invoke(sizes, new[] { groupEnum });
            if (group == null) return false;

            Type sizeGroupType = group.GetType();
            MethodInfo getTotal = sizeGroupType.GetMethod("GetTotalCount", flags);
            MethodInfo getSize = sizeGroupType.GetMethod("GetGameViewSize", flags);
            PropertyInfo selectedIndex = gameViewType.GetProperty("selectedSizeIndex", flags);
            if (getTotal == null || getSize == null || selectedIndex == null || !selectedIndex.CanWrite)
                return false;

            int count = (int)getTotal.Invoke(group, null);
            for (int i = 0; i < count; i++)
            {
                object size = getSize.Invoke(group, new object[] { i });
                if (size == null) continue;
                Type sizeType = size.GetType();
                PropertyInfo widthProperty = sizeType.GetProperty("width", flags);
                PropertyInfo heightProperty = sizeType.GetProperty("height", flags);
                if (widthProperty == null || heightProperty == null) continue;
                if ((int)widthProperty.GetValue(size, null) != width ||
                    (int)heightProperty.GetValue(size, null) != height)
                    continue;

                // Only FixedResolution entries, never 16:9 aspect presets.
                PropertyInfo sizeTypeProperty = sizeType.GetProperty("sizeType", flags);
                if (sizeTypeProperty == null || sizeTypeProperty.GetValue(size, null).ToString() != "FixedResolution")
                    continue;

                selectedIndex.SetValue(gameView, i, null);
                gameView.Repaint();
                problem = null;
                return true;
            }
            problem = "no fixed " + width + "x" + height + " preset exists in the current Game view size group";
            return false;
        }
        catch (Exception ex)
        {
            problem = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }
}
#endif
