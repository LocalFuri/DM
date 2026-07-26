using UnityEngine;

namespace DM.Core
{
  // Map loading and play bootstrap live on GameBootstrap.
  // This component is kept for compatibility but does not create a dungeon.
  public class GameManager : MonoBehaviour
  {
    private void Awake()
    {
      Debug.Log(
          "GameManager: inactive. Use GameBootstrap with a map TextAsset."
      );
    }
  }
}
