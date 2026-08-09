using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// Editor-only: clear the Unity Console once when entering Play Mode,
/// once when returning to Edit Mode, and once when a player build starts.
/// Does not clear mid-session on move/turn/redraw.
/// Edit Mode POS/wall restore is owned by ViewportLayoutEditor.
/// </summary>
[InitializeOnLoad]
internal static class ClearConsoleOnPlayAndBuild
{
  static ClearConsoleOnPlayAndBuild()
  {
    EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
  }

  private static void OnPlayModeStateChanged(PlayModeStateChange state)
  {
    // Clear once just before Play begins so Awake/Start / POS logs remain.
    if (state == PlayModeStateChange.ExitingEditMode)
    {
      ClearConsole();
      return;
    }

    // Clear once when returning to Edit Mode — drop the previous Play session.
    // Reset Edit Mode log cache so the next preview refresh re-logs POS/walls.
    if (state == PlayModeStateChange.EnteredEditMode)
    {
      ClearConsole();
      ViewportLayoutEditor.ResetEditModeViewportLogCache();
    }
  }

  internal static void ClearConsole()
  {
    Type logEntriesType = typeof(EditorWindow).Assembly.GetType(
        "UnityEditor.LogEntries"
    );
    if (logEntriesType == null)
      return;

    MethodInfo clearMethod = logEntriesType.GetMethod(
        "Clear",
        BindingFlags.Static | BindingFlags.Public
    );
    if (clearMethod == null)
      return;

    clearMethod.Invoke(null, null);
  }
}

/// <summary>
/// Clears the Console once at the start of a Unity player build.
/// </summary>
internal sealed class ClearConsoleBeforeBuild : IPreprocessBuildWithReport
{
  public int callbackOrder => 0;

  public void OnPreprocessBuild(BuildReport report)
  {
    ClearConsoleOnPlayAndBuild.ClearConsole();
  }
}
