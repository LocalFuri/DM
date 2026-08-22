using UnityEngine;
using UnityEngine.Serialization;

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
    [FormerlySerializedAs("WallS3L")]
    public Texture2D WallD3L2;

    [FormerlySerializedAs("WallS3R")]
    public Texture2D WallD3R2;

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

    [Header("Interface")]
    public Texture2D ChampionStatusBackground;

    [Header("Hall of Champions")]
    public Texture2D BlackDoor;
    public Texture2D BlackDoorFrameLeft;
    public Texture2D BlackDoorFrameLeftF2;

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
          return GetFrontWallF1Texture(
              StraightF1WallLogic.DefaultFrontWallF1Width);
        case DungeonGraphicType.FrontWallF2:
          return FrontWallF2;
        case DungeonGraphicType.FrontWallF3:
          return ExpandedF3WallTexture.BuildExpandedF3Wall(
              FrontWallF3,
              WallF3L,
              WallF3R);
        case DungeonGraphicType.WallD3L2:
          return WallD3L2;
        case DungeonGraphicType.WallD3R2:
          return WallD3R2;
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
        case DungeonGraphicType.ChampionStatusBackground:
          return ChampionStatusBackground;
        case DungeonGraphicType.BlackDoor:
          return BlackDoor;
        case DungeonGraphicType.BlackDoorFrameLeft:
          return BlackDoorFrameLeft;
        case DungeonGraphicType.BlackDoorFrameLeftF2:
          return BlackDoorFrameLeftF2;
        default:
          return null;
      }
    }

    public Texture2D GetFrontWallF1Texture(int width)
    {
      switch (StraightF1WallLogic.NormalizeFrontWallF1Width(width))
      {
        case StraightF1WallLogic.CompositeWidth160:
          return FrontWallF1;
        case StraightF1WallLogic.CompositeWidth:
          return ExpandedF1WallTexture.BuildExpandedF1Wall(
              FrontWallF1,
              WallF1L,
              WallF1R);
        default:
          return ExpandedF1WallTexture.BuildExpandedF1Wall191(
              FrontWallF1,
              WallF1R);
      }
    }

    public Texture2D GetFrontWallF2Texture(int width)
    {
      int normalized = FrontWallF2Logic.Normalize(width);
      if (normalized == FrontWallF2Logic.Width160)
      {
        Texture2D expanded160 = ExpandedF2WallTexture.BuildExpandedF2Wall160(
            FrontWallF2);
        if (expanded160 != null
            && expanded160.width == FrontWallF2Logic.Width160)
        {
          return expanded160;
        }

        return FrontWallF2;
      }

      if (normalized == FrontWallF2Logic.Width131)
      {
        return ExpandedF2WallTexture.BuildExpandedF2Wall(
            FrontWallF2,
            WallF2R);
      }

      return FrontWallF2;
    }
  }
}
