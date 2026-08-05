using System;
using System.Collections.Generic;
using DM.Dungeon;
using DM.Rendering;
using UnityEngine;

/// <summary>
/// Editor-only per-pose snapshots of ViewportLayout pieces.
/// Keyed by map identity + X + Y + Facing.
/// </summary>
[CreateAssetMenu(
    fileName = "ViewportPoseLayouts",
    menuName = "Dungeon Master/Viewport Pose Layouts")]
public class ViewportPoseLayouts : ScriptableObject
{
  public List<ViewportPoseLayoutEntry> Entries = new();
}

[Serializable]
public class ViewportPoseLayoutEntry
{
  public string MapId;
  public int X;
  public int Y;
  public DungeonFacing Facing;
  public List<ViewportPieceSnapshot> Pieces = new();
}

[Serializable]
public class ViewportPieceSnapshot
{
  public string Name;
  public DungeonGraphicType Graphic;
  public int X;
  public int Y;
  public bool Enabled;
  public bool MirrorHorizontally;

  public static ViewportPieceSnapshot FromPiece(ViewportPiece piece)
  {
    if (piece == null)
      return null;

    return new ViewportPieceSnapshot
    {
      Name = piece.Name,
      Graphic = piece.Graphic,
      X = piece.X,
      Y = piece.Y,
      Enabled = piece.Enabled,
      MirrorHorizontally = piece.MirrorHorizontally
    };
  }

  public ViewportPiece ToPiece()
  {
    return new ViewportPiece
    {
      Name = Name,
      Graphic = Graphic,
      X = X,
      Y = Y,
      Enabled = Enabled,
      MirrorHorizontally = MirrorHorizontally
    };
  }
}
