using System;
using UnityEngine;

namespace DM.Rendering
{
  [CreateAssetMenu(
      fileName = "ViewportLayout",
      menuName = "Dungeon Master/Viewport Layout")]
  public class ViewportLayout : ScriptableObject
  {
    [Header("Viewport")]
    public int Width = 320;
    public int Height = 200;

    [Header("Environment")]
    public ViewportPiece Ceiling;
    public ViewportPiece Floor;

    [Header("Front Walls")]
    public ViewportPiece WallF3L;
    public ViewportPiece WallF3R;

    public ViewportPiece WallF2L;
    public ViewportPiece WallF2R;

    public ViewportPiece WallF1L;
    public ViewportPiece WallF1R;

    public ViewportPiece WallF0L;
    public ViewportPiece WallF0R;

    [Header("Side Walls")]
    public ViewportPiece WallS3L;
    public ViewportPiece WallS3R;

    public ViewportPiece WallS2L;
    public ViewportPiece WallS2R;
  }

  [Serializable]
  public class ViewportPiece
  {
    public int X;
    public int Y;
  }
}