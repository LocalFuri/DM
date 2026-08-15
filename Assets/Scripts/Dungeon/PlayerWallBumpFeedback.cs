using UnityEngine;

namespace DM.Dungeon
{
  /// <summary>
  /// Temporary wall bump feedback (console only).
  /// Shared by Edit Mode preview navigation and Play/Build input.
  /// Logs "Player Hit Wall" once per blocked encounter; turns never bump.
  /// </summary>
  public static class PlayerWallBumpFeedback
  {
    private static bool hasLoggedWallHit;

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
      if (hasLoggedWallHit)
        return;

      hasLoggedWallHit = true;
      Debug.Log("Player Hit Wall");
    }

    /// <summary>
    /// Allow the next blocked move to log again after a successful move or turn.
    /// </summary>
    public static void ResetWallHitLog()
    {
      hasLoggedWallHit = false;
    }
  }
}
