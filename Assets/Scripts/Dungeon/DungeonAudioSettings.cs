using UnityEngine;

/// <summary>
/// Central sound configuration for the project.
/// Create one asset via Assets > Create > Audio Settings.
/// The asset works in Edit Mode and can later be reused by Play Mode/build code.
/// </summary>
[CreateAssetMenu(
    fileName = "AudioSettings",
    menuName = "Audio Settings")]
public sealed class DungeonAudioSettings : ScriptableObject
{
  [Header("Champion Mirror")]

  [Tooltip("Sound played when the Champion sheet is cancelled/closed.")]
  public AudioClip championExitSound;

  [Range(0f, 1f)]
  public float championExitVolume = 1f;

  [Tooltip("Sound played when RESURRECT is clicked.")]
  public AudioClip championResurrectSound;

  [Range(0f, 1f)]
  public float championResurrectVolume = 1f;
}