using DM.Heroes;
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

    public void Initialize(
        DungeonMap dungeonMap,
        DungeonRenderer renderer)
    {
      map = dungeonMap;
      dungeonRenderer = renderer;

      DetectChampionInFront();
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

    // localX/localY are in facing-local space:
    // +Y = forward
    // -Y = backward
    // +X = strafe right
    // -X = strafe left
    private void TryMoveRelative(int localX, int localY)
    {
      map.GetWorldOffset(
          localX,
          localY,
          out int worldDx,
          out int worldDy
      );

      bool moved = map.TryMoveBy(worldDx, worldDy);

      if (moved)
      {
        Debug.Log(
            $"Moved to ({map.PlayerX},{map.PlayerY}) " +
            $"facing {map.PlayerFacing}."
        );

        dungeonRenderer.RequestRedraw();
        DetectChampionInFront();
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
      DetectChampionInFront();
    }

    private void TurnRight()
    {
      map.TurnRight();

      Debug.Log(
          $"Turned right. Now at ({map.PlayerX},{map.PlayerY}) " +
          $"facing {map.PlayerFacing}."
      );

      dungeonRenderer.RequestRedraw();
      DetectChampionInFront();
    }

    private void DetectChampionInFront()
    {
      HeroWallDirection wallDirection =
          ConvertToHeroWallDirection(map.PlayerFacing);

      HeroDefinition hero = HeroDatabase.GetByPlacement(
          0,
          map.PlayerX,
          map.PlayerY,
          wallDirection
      );

      if (hero == null)
        return;

      string displayName = string.IsNullOrEmpty(hero.Title)
          ? hero.Name
          : $"{hero.Name} {hero.Title}";

      Debug.Log(
          $"Champion in front: {displayName} " +
          $"at ({map.PlayerX},{map.PlayerY}) " +
          $"on the {wallDirection} wall."
      );
    }

    private static HeroWallDirection ConvertToHeroWallDirection(
        DungeonFacing facing)
    {
      switch (facing)
      {
        case DungeonFacing.North:
          return HeroWallDirection.North;

        case DungeonFacing.East:
          return HeroWallDirection.East;

        case DungeonFacing.South:
          return HeroWallDirection.South;

        case DungeonFacing.West:
          return HeroWallDirection.West;

        default:
          return HeroWallDirection.North;
      }
    }
  }
}