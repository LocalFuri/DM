using DM.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DM.Dungeon
{
  public class DungeonKeyboardInput : MonoBehaviour
  {
    private DungeonMap map;
    private DungeonRenderer dungeonRenderer;

    public DungeonMap Map => map;

    public void Initialize(DungeonMap dungeonMap, DungeonRenderer renderer)
    {
      map = dungeonMap;
      dungeonRenderer = renderer;
    }

    private void Update()
    {
      if (map == null || dungeonRenderer == null)
        return;

      Keyboard keyboard = Keyboard.current;
      if (keyboard == null)
        return;

      if (keyboard.upArrowKey.wasPressedThisFrame)
        TryMoveRelative(0, 1);

      if (keyboard.downArrowKey.wasPressedThisFrame)
        TryMoveRelative(0, -1);

      if (keyboard.leftArrowKey.wasPressedThisFrame)
        TryMoveRelative(-1, 0);

      if (keyboard.rightArrowKey.wasPressedThisFrame)
        TryMoveRelative(1, 0);

      if (keyboard.deleteKey.wasPressedThisFrame)
        TurnLeft();

      if (keyboard.pageDownKey.wasPressedThisFrame)
        TurnRight();
    }

    // dx/dy are in facing-local space: +y forward, +x strafe right.
    private void TryMoveRelative(int localX, int localY)
    {
      map.GetWorldOffset(localX, localY, out int worldDx, out int worldDy);

      bool moved = map.TryMoveBy(worldDx, worldDy);

      if (moved)
      {
        Debug.Log(
            $"Moved to ({map.PlayerX},{map.PlayerY}) " +
            $"facing {map.PlayerFacing}."
        );
        dungeonRenderer.RequestRedraw();
      }
      else
      {
        Debug.Log(
            $"Movement blocked at ({map.PlayerX},{map.PlayerY}) " +
            $"facing {map.PlayerFacing}."
        );
      }
    }

    private void TurnLeft()
    {
      map.TurnLeft();
      Debug.Log(
          $"Turned left. Now at ({map.PlayerX},{map.PlayerY}) " +
          $"facing {map.PlayerFacing}."
      );
      dungeonRenderer.RequestRedraw();
    }

    private void TurnRight()
    {
      map.TurnRight();
      Debug.Log(
          $"Turned right. Now at ({map.PlayerX},{map.PlayerY}) " +
          $"facing {map.PlayerFacing}."
      );
      dungeonRenderer.RequestRedraw();
    }
  }
}
