using UnityEngine;

namespace DM.Dungeon
{
  /// <summary>
  /// Temporary wall bump feedback (console only).
  /// Shared by Edit Mode preview navigation and Play/Build input.
  /// Any blocked movement attempt reports; turns never call this path.
  /// </summary>
  public static class PlayerWallBumpFeedback
  {
    /// <summary>
    /// Call when a movement attempt was made and did not move.
    /// localX/localY are unused; kept for shared call-site signature.
    /// </summary>
    public static void ReportIfBlockedMove(int localX, int localY)
    {
      HandlePlayerHitWall();
    }

    public static void HandlePlayerHitWall()
    {
      Debug.Log("Player Hit Wall");
    }
  }
}
