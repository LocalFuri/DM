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

    [Header("Front Walls")]
    public Texture2D FrontWallF0;
    public Texture2D FrontWallF1;
    public Texture2D FrontWallF2;
    public Texture2D FrontWallF3;

    [Header("Left Walls")]
    public Texture2D LeftWallF0;
    public Texture2D LeftWallF1;
    public Texture2D LeftWallF2;
    public Texture2D LeftWallF3;

    [Header("Right Walls")]
    public Texture2D RightWallF0;
    public Texture2D RightWallF1;
    public Texture2D RightWallF2;
    public Texture2D RightWallF3;

    [Header("Side Wall Strips")]
    public Texture2D SideWallLeft;
    public Texture2D SideWallRight;

    [Header("Doors")]
    public Texture2D DoorClosed;
    public Texture2D DoorOpen;
    public Texture2D DoorFrameLeft;
    public Texture2D DoorFrameRight;
    public Texture2D DoorFrameTop;

    [Header("Wall Decorations")]
    public Texture2D Alcove;
    public Texture2D WallSwitch;
    public Texture2D TorchHolder;
    public Texture2D WallOrnament;

    [Header("Masks")]
    public Texture2D FrontWallMask;
    public Texture2D LeftWallMask;
    public Texture2D RightWallMask;
    public Texture2D DoorMask;
  }
}