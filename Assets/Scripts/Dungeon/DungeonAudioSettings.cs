using UnityEngine;

[CreateAssetMenu(
    fileName = "AudioSettings",
    menuName = "Audio Settings")]
public class DungeonAudioSettings : ScriptableObject
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

    [Header("Dungeon Floor Switch")]
    [Tooltip("Sound played when the player enters the pressure-switch tile.")]
    public AudioClip switchSound;

    [Range(0f, 1f)]
    public float switchVolume = 1f;
}