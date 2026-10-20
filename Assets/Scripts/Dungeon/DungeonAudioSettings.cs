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

    [Header("Champion Eating Sounds")]
    [Tooltip("Played once when a champion successfully eats an Apple. Assign Apple.wav.")]
    public AudioClip appleEatingSound;

    [Range(0f, 1f)]
    public float appleEatingVolume = 1f;

    [Tooltip("Played once when a champion successfully eats Bread. Assign Bread.wav.")]
    public AudioClip breadEatingSound;

    [Range(0f, 1f)]
    public float breadEatingVolume = 1f;

    [Header("Dungeon Floor Switch")]
    [Tooltip("Sound played when the player enters the pressure-switch tile.")]
    public AudioClip switchSound;

    [Range(0f, 1f)]
    public float switchVolume = 1f;

    [Header("Thrown Items - Sounds and Base Volumes")]
    [Tooltip("Played once when an item is successfully thrown. Assign Combat_Attack.wav.")]
    public AudioClip itemThrowSound;

    [Range(0f, 1f)]
    public float itemThrowVolume = 1f;

    [Tooltip("Played on impact for weapons. Assign Metallic_Thud.wav.")]
    public AudioClip weaponImpactSound;

    [Range(0f, 1f)]
    public float weaponImpactVolume = 1f;

    [Tooltip("Played on impact for every non-weapon item. Assign Wooden_Thud.wav.")]
    public AudioClip otherItemImpactSound;

    [Range(0f, 1f)]
    public float otherItemImpactVolume = 1f;

    [Header("Thrown Item Impact by Distance")]
    [Tooltip("Impact volume when an item lands or hits an obstacle at F1.")]
    [Range(0f, 1f)]
    public float itemImpactF1Volume = 1f;

    [Tooltip("Impact volume when an item lands or hits an obstacle at F2.")]
    [Range(0f, 1f)]
    public float itemImpactF2Volume = 0.75f;

    [Tooltip("Impact volume when an item lands or hits an obstacle at F3 (maximum throw distance).")]
    [Range(0f, 1f)]
    public float itemImpactF3Volume = 0.5f;
}