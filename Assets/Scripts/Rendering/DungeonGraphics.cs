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

    [Header("Ceiling Strips")]
    public Texture2D CeilingStrip84;
    public Texture2D CeilingStrip85;

    [Header("Entrance")]
    public Texture2D EntranceDoorClosedOutside;
    public Texture2D EntranceDoorClosedLeft;
    public Texture2D EntranceDoorClosedRight;

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

    [Header("Front Walls")]
    public Texture2D FrontWallF1;
    public Texture2D FrontWallF2;
    public Texture2D FrontWallF3;

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

    public Texture2D GetTexture(DungeonGraphicType graphic)
    {
      switch (graphic)
      {
        case DungeonGraphicType.Ceiling:
          return Ceiling;
        case DungeonGraphicType.Floor:
          return Floor;
        case DungeonGraphicType.CeilingStrip84:
          return CeilingStrip84;
        case DungeonGraphicType.CeilingStrip85:
          return CeilingStrip85;
        case DungeonGraphicType.WallF0L:
          return WallF0L;
        case DungeonGraphicType.WallF0R:
          return WallF0R;
        case DungeonGraphicType.WallF1L:
          return WallF1L;
        case DungeonGraphicType.WallF1R:
          return WallF1R;
        case DungeonGraphicType.WallF2L:
          return WallF2L;
        case DungeonGraphicType.WallF2R:
          return WallF2R;
        case DungeonGraphicType.WallF3L:
          return WallF3L;
        case DungeonGraphicType.WallF3R:
          return WallF3R;
        case DungeonGraphicType.FrontWallF1:
        case DungeonGraphicType.FrontWallF1_A:
        case DungeonGraphicType.FrontWallF1_B:
          return FrontWallF1;
        case DungeonGraphicType.FrontWallF2:
          return FrontWallF2;
        case DungeonGraphicType.FrontWallF3:
          return FrontWallF3;
        case DungeonGraphicType.WallS2L:
          return WallS2L;
        case DungeonGraphicType.WallS2R:
          return WallS2R;
        case DungeonGraphicType.WallS3L:
          return WallS3L;
        case DungeonGraphicType.WallS3R:
          return WallS3R;
        case DungeonGraphicType.DoorClosed:
          return DoorClosed;
        case DungeonGraphicType.DoorOpen:
          return DoorOpen;
        case DungeonGraphicType.Alcove:
          return Alcove;
        case DungeonGraphicType.WallSwitch:
          return WallSwitch;
        case DungeonGraphicType.TorchHolder:
          return TorchHolder;
        case DungeonGraphicType.WallOrnament:
          return WallOrnament;
        default:
          return null;
      }
    }

    // Returns the mask for a side-wall graphic. Right-side pieces use the
    // matching left mask flipped horizontally when no explicit R mask is set.
    public Texture2D GetMask(
        DungeonGraphicType graphic,
        out bool flipHorizontal)
    {
      flipHorizontal = false;

      switch (graphic)
      {
        case DungeonGraphicType.WallF0L:
          return MaskWallF0L;

        case DungeonGraphicType.WallF0R:
          if (MaskWallF0R != null)
            return MaskWallF0R;

          flipHorizontal = true;
          return MaskWallF0L;

        case DungeonGraphicType.WallF1L:
          return MaskWallF1L;

        case DungeonGraphicType.WallF1R:
          if (MaskWallF1R != null)
            return MaskWallF1R;

          flipHorizontal = true;
          return MaskWallF1L;

        case DungeonGraphicType.WallF2L:
          return MaskWallF2L;

        case DungeonGraphicType.WallF2R:
          if (MaskWallF2R != null)
            return MaskWallF2R;

          flipHorizontal = true;
          return MaskWallF2L;

        case DungeonGraphicType.WallF3L:
          return MaskWallF3L;

        case DungeonGraphicType.WallF3R:
          if (MaskWallF3R != null)
            return MaskWallF3R;

          flipHorizontal = true;
          return MaskWallF3L;

        case DungeonGraphicType.WallS2L:
          return MaskWallS2L;

        case DungeonGraphicType.WallS2R:
          if (MaskWallS2R != null)
            return MaskWallS2R;

          flipHorizontal = true;
          return MaskWallS2L;

        case DungeonGraphicType.WallS3L:
          return MaskWallS3L;

        case DungeonGraphicType.WallS3R:
          if (MaskWallS3R != null)
            return MaskWallS3R;

          flipHorizontal = true;
          return MaskWallS3L;

        default:
          return null;
      }
    }
  }
}