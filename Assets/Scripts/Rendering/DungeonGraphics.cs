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

    [Header("Front Walls - Native DOS Centers")]
    public Texture2D FrontWallF1;
    public Texture2D FrontWallF2;
    public Texture2D FrontWallF3;

    public const string FrontWallF1NativeAssetPath =
        "Assets/Art/Walls/Front_Wall_F1_RAW_160x111.png";

    public const string FrontWallF2NativeAssetPath =
        "Assets/Art/Walls/Front_Wall_F2_RECOVERED_CENTER_106x74.png";

    public const string FrontWallF3NativeAssetPath =
        "Assets/Art/Walls/Front_Wall_F3_RAW_70x49.png";

    // Legacy public constant names are kept as compile-safe aliases for any
    // older callers elsewhere in the project. They now resolve to the native
    // center assets; no old composite PNG path remains in code.
    [System.Obsolete("Use FrontWallF1NativeAssetPath.")]
    public const string FrontWallF1_224AssetPath = FrontWallF1NativeAssetPath;

    [System.Obsolete("Use FrontWallF3NativeAssetPath.")]
    public const string FrontWallF3_141AssetPath = FrontWallF3NativeAssetPath;

    [System.NonSerialized]
    private Texture2D cachedFrontWallF1Native;

    [System.NonSerialized]
    private Texture2D cachedFrontWallF2Native;

    [System.NonSerialized]
    private Texture2D cachedFrontWallF3Native;

    [System.NonSerialized]
    private Texture2D cachedLeft2S;

    [Header("Side Wall Graphics")]
    [FormerlySerializedAs("WallS3L")]
    public Texture2D WallD3L2;

    [FormerlySerializedAs("WallS3R")]
    public Texture2D WallD3R2;

    [Header("2S Walls")]
    public Texture2D Left2S;

    public const string Left2SAssetPath = "Assets/Art/Walls/Left2S.png";

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
          return GetFrontWallF1Texture(160);
        case DungeonGraphicType.FrontWallF2:
          return GetFrontWallF2Texture(106);
        case DungeonGraphicType.FrontWallF3:
          return GetFrontWallF3Texture();
        case DungeonGraphicType.WallD3L2:
          return WallD3L2;
        case DungeonGraphicType.WallD3R2:
          return WallD3R2;
        case DungeonGraphicType.Left2S:
          return GetLeft2STexture();
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

    // Compatibility signature retained for existing callers. Native F1 is
    // always the original 160x111 center graphic; width expansion/composite
    // generation is intentionally no longer performed here.
    public Texture2D GetFrontWallF1Texture(int width)
    {
      if (cachedFrontWallF1Native != null)
        return cachedFrontWallF1Native;

      if (FrontWallF1 != null
          && FrontWallF1.width == 160
          && FrontWallF1.height == 111)
      {
        cachedFrontWallF1Native = FrontWallF1;
        return cachedFrontWallF1Native;
      }

#if UNITY_EDITOR
      cachedFrontWallF1Native =
          UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
              FrontWallF1NativeAssetPath);
#endif
      return cachedFrontWallF1Native;
    }

    // Compatibility signature retained for existing callers. Native F2 is
    // always the original 106x74 center graphic.
    public Texture2D GetFrontWallF2Texture(int width)
    {
      if (cachedFrontWallF2Native != null)
        return cachedFrontWallF2Native;

      if (FrontWallF2 != null
          && FrontWallF2.width == 106
          && FrontWallF2.height == 74)
      {
        cachedFrontWallF2Native = FrontWallF2;
        return cachedFrontWallF2Native;
      }

#if UNITY_EDITOR
      cachedFrontWallF2Native =
          UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
              FrontWallF2NativeAssetPath);
#endif
      return cachedFrontWallF2Native;
    }

    public Texture2D GetFrontWallF3Texture()
    {
      if (cachedFrontWallF3Native != null)
        return cachedFrontWallF3Native;

      if (FrontWallF3 != null
          && FrontWallF3.width == 70
          && FrontWallF3.height == 49)
      {
        cachedFrontWallF3Native = FrontWallF3;
        return cachedFrontWallF3Native;
      }

#if UNITY_EDITOR
      cachedFrontWallF3Native =
          UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
              FrontWallF3NativeAssetPath);
#endif
      return cachedFrontWallF3Native;
    }

    public Texture2D GetLeft2STexture()
    {
      if (Left2S != null)
        return Left2S;

      if (cachedLeft2S != null)
        return cachedLeft2S;

#if UNITY_EDITOR
      cachedLeft2S =
          UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
              Left2SAssetPath);
#endif
      return cachedLeft2S != null ? cachedLeft2S : Left2S;
    }
  }
}
