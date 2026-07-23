using UnityEngine;

namespace DM.Rendering
{
  [CreateAssetMenu(
      fileName = "DungeonGraphics",
      menuName = "Dungeon Master/Dungeon Graphics")]
  public class DungeonGraphics : ScriptableObject
  {
    [Header("Environment")]
    public Texture2D Ceiling;
    public Texture2D Floor;

    [Header("Wall Graphics - F0")]
    public Texture2D WallF0L;
    public Texture2D WallF0R;

    [Header("Wall Graphics - F1")]
    public Texture2D WallF1L;
    public Texture2D WallF1R;

    [Header("Wall Graphics - F2")]
    public Texture2D WallF2L;
    public Texture2D WallF2R;

    [Header("Wall Graphics - F3")]
    public Texture2D WallF3L;
    public Texture2D WallF3R;

    [Header("Side Wall Graphics")]
    public Texture2D WallS2L;
    public Texture2D WallS2R;
    public Texture2D WallS3L;
    public Texture2D WallS3R;

    [Header("Wall Masks - F0")]
    public Texture2D MaskWallF0L;
    public Texture2D MaskWallF0R;

    [Header("Wall Masks - F1")]
    public Texture2D MaskWallF1L;
    public Texture2D MaskWallF1R;

    [Header("Wall Masks - F2")]
    public Texture2D MaskWallF2L;
    public Texture2D MaskWallF2R;

    [Header("Wall Masks - F3")]
    public Texture2D MaskWallF3L;
    public Texture2D MaskWallF3R;

    [Header("Side Wall Masks")]
    public Texture2D MaskWallS2L;
    public Texture2D MaskWallS2R;
    public Texture2D MaskWallS3L;
    public Texture2D MaskWallS3R;

    [Header("Doors")]
    public Texture2D DoorClosed;
    public Texture2D DoorOpen;
    public Texture2D DoorFrameLeft;
    public Texture2D DoorFrameRight;
    public Texture2D DoorFrameTop;
    public Texture2D DoorMask;

    [Header("Wall Features")]
    public Texture2D Alcove;
    public Texture2D WallSwitch;
    public Texture2D TorchHolder;
    public Texture2D WallOrnament;
  }
}