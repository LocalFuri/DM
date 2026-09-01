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

    public const string FrontWallF1_224AssetPath =
        "Assets/Art/Walls/Front Wall F1_224x111.png";

    public const string FrontWallF3_141AssetPath =
        "Assets/Art/Walls/Front Wall F3_141x49.png";

    [System.NonSerialized]
    private Texture2D cachedFrontWallF1_224;

    [System.NonSerialized]
    private Texture2D cachedFrontWallF3_141;

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
    public Texture2D BlackDoorFrameRightF2;

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
              StraightF1WallLogic.CompositeWidth);
        case DungeonGraphicType.FrontWallF2:
          return FrontWallF2;
        case DungeonGraphicType.FrontWallF3:
          return GetFrontWallF3Texture();
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
        case DungeonGraphicType.BlackDoorFrameRightF2:
          return BlackDoorFrameRightF2 != null
              ? BlackDoorFrameRightF2
              : BlackDoorFrameLeftF2;
        default:
          return null;
      }
    }

    public Texture2D GetFrontWallF1Texture(int width)
    {
      if (cachedFrontWallF1_224 != null)
        return cachedFrontWallF1_224;

#if UNITY_EDITOR
      cachedFrontWallF1_224 =
          UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
              FrontWallF1_224AssetPath);
#endif
      if (cachedFrontWallF1_224 != null)
        return cachedFrontWallF1_224;

      if (FrontWallF1 != null
          && FrontWallF1.width == StraightF1WallLogic.CompositeWidth)
      {
        cachedFrontWallF1_224 = FrontWallF1;
        return cachedFrontWallF1_224;
      }

      return FrontWallF1;
    }

    public Texture2D GetFrontWallF3Texture()
    {
      if (cachedFrontWallF3_141 != null)
        return cachedFrontWallF3_141;

#if UNITY_EDITOR
      cachedFrontWallF3_141 =
          UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
              FrontWallF3_141AssetPath);
#endif
      if (cachedFrontWallF3_141 != null)
        return cachedFrontWallF3_141;

      if (FrontWallF3 != null
          && FrontWallF3.width == 141
          && FrontWallF3.height == 49)
      {
        cachedFrontWallF3_141 = FrontWallF3;
        return cachedFrontWallF3_141;
      }

      return FrontWallF3;
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
