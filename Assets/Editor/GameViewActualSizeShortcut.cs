#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

public static class GameViewActualSizeShortcut
{
    [MenuItem("Tools/Game View/Actual Size 1x")]
    public static void SetGameViewToActualSize()
    {
        Type gameViewType =
            typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");

        if (gameViewType == null)
        {
            Debug.LogWarning("GameViewActualSizeShortcut: UnityEditor.GameView was not found.");
            return;
        }

        EditorWindow gameView = null;

        if (EditorWindow.focusedWindow != null &&
            gameViewType.IsInstanceOfType(EditorWindow.focusedWindow))
        {
            gameView = EditorWindow.focusedWindow;
        }
        else
        {
            UnityEngine.Object[] openViews =
                Resources.FindObjectsOfTypeAll(gameViewType);

            if (openViews != null && openViews.Length > 0)
                gameView = openViews[0] as EditorWindow;
        }

        if (gameView == null)
            gameView = EditorWindow.GetWindow(gameViewType);

        MethodInfo snapZoom = gameViewType.GetMethod(
            "SnapZoom",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (snapZoom == null)
        {
            Debug.LogWarning(
                "GameViewActualSizeShortcut: GameView.SnapZoom was not found. " +
                "This Unity version may have changed its internal Game View API.");
            return;
        }

        snapZoom.Invoke(gameView, new object[] { 1.0f });
        gameView.Repaint();
    }

    // Windows: Ctrl+H
    // macOS:   Cmd+H
    [Shortcut(
        "Game View/Actual Size 1x",
        KeyCode.H,
        ShortcutModifiers.Action)]
    private static void SetGameViewToActualSizeShortcut()
    {
        SetGameViewToActualSize();
    }
}
#endif
