namespace DM.Rendering
{
  public enum DungeonGraphicType
  {
    None,

    Ceiling,
    Floor,

    WallF0L,
    WallF0R,

    WallF1L,
    WallF1R,

    WallF2L,
    WallF2R,

    WallF3L,
    WallF3R,

    // 11 / 12 were WallS2L / WallS2R — keep numeric IDs stable for serialization.
    WallS3L = 13,
    WallS3R = 14,

    DoorClosed,
    DoorOpen,

    Alcove,
    WallSwitch,
    TorchHolder,
    WallOrnament,

    CeilingStrip84,
    CeilingStrip85,

    FrontWallF1,
    FrontWallF2,
    FrontWallF3,

    MovementArrows,

    FrontWallF1_A,
    FrontWallF1_B,

    ChampionStatusBackground
  }
}