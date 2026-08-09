using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// Editor-only: clear the Unity Console once when entering Play Mode,
/// and once when a player build starts. Does not clear on stop or mid-session.
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
    // Do not clear on EnteredPlayMode (too late) or when stopping Play.
    if (state != PlayModeStateChange.ExitingEditMode)
      return;

    ClearConsole();
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
