using UnityEngine;

namespace DM.Dungeon
{
  /// <summary>
  /// Temporary forward-wall bump feedback (console only).
  /// Shared by Edit Mode preview navigation and Play/Build input.
  /// </summary>
  public static class PlayerWallBumpFeedback
  {
    public static void HandlePlayerHitWall()
    {
      Debug.Log("Player Hit Wall");
    }
  }
}
