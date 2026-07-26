using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DM.Core
{
  public class GameQuitHandler : MonoBehaviour
  {
    [SerializeField]
    private AudioClip quitSound;

    [SerializeField]
    private AudioSource audioSource;

    private bool quitting;

    private void Awake()
    {
      if (audioSource == null)
        audioSource = GetComponent<AudioSource>();

      if (audioSource == null)
        audioSource = gameObject.AddComponent<AudioSource>();

      audioSource.playOnAwake = false;
      audioSource.spatialBlend = 0f;
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
      EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private void OnDisable()
    {
      EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
    }

    private void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
      // Manual Stop: play pop if we did not already start a quit-from-Escape.
      if (state != PlayModeStateChange.ExitingPlayMode)
        return;

      if (quitting)
        return;

      PlayQuitSoundEditorPreview();
    }
#endif

    private void Update()
    {
      Keyboard keyboard = Keyboard.current;
      if (keyboard == null)
        return;

      if (keyboard.escapeKey.wasPressedThisFrame)
        RequestQuit();
    }

    public void RequestQuit()
    {
      if (quitting)
        return;

      quitting = true;
      StartCoroutine(QuitAfterSound());
    }

    private IEnumerator QuitAfterSound()
    {
      float waitSeconds = PlayQuitSoundRuntime();

      if (waitSeconds > 0f)
        yield return new WaitForSecondsRealtime(waitSeconds);

#if UNITY_EDITOR
      EditorApplication.isPlaying = false;
#else
      Application.Quit();
#endif
    }

    private float PlayQuitSoundRuntime()
    {
      if (quitSound == null || audioSource == null)
        return 0f;

      audioSource.PlayOneShot(quitSound);
      return quitSound.length;
    }

#if UNITY_EDITOR
    private void PlayQuitSoundEditorPreview()
    {
      if (quitSound == null)
        return;

      // Play through the Editor preview path so the clip can still be heard
      // while Play Mode is tearing down (Stop button).
      System.Type audioUtilType = typeof(AudioImporter).Assembly.GetType(
          "UnityEditor.AudioUtil");

      if (audioUtilType == null)
        return;

      System.Reflection.MethodInfo playMethod = audioUtilType.GetMethod(
          "PlayPreviewClip",
          System.Reflection.BindingFlags.Static |
              System.Reflection.BindingFlags.Public,
          null,
          new[] { typeof(AudioClip), typeof(int), typeof(bool) },
          null);

      if (playMethod == null)
        return;

      playMethod.Invoke(null, new object[] { quitSound, 0, false });
    }
#endif
  }
}
