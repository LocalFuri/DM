using DM.Heroes;
using DM.Rendering;
using DM.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DM.Dungeon
{
  public class DungeonKeyboardInput : MonoBehaviour
  {
    private DungeonMap map;
    private DungeonRenderer dungeonRenderer;
    private HeroRecruitmentPanel heroRecruitmentPanel;

    public DungeonMap Map => map;

    public void Initialize(
        DungeonMap dungeonMap,
        DungeonRenderer renderer,
        HeroRecruitmentPanel recruitmentPanel)
    {
      map = dungeonMap;
      dungeonRenderer = renderer;
      heroRecruitmentPanel = recruitmentPanel;

      DetectChampionInFront();
    }

    private void Update()
    {
      if (map == null || dungeonRenderer == null)
        return;

      Keyboard keyboard = Keyboard.current;
      if (keyboard == null)
        return;

      // TEMPORARY TEST: Space opens the entrance doors.
      if (keyboard.spaceKey.wasPressedThisFrame)
        dungeonRenderer.OpenEntranceDoor();

      if (dungeonRenderer.IsEntranceBlockingInput)
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
      GetHeroPlacementLookup(
          map.PlayerFacing,
          out int heroX,
          out int heroY,
          out HeroWallDirection wallDirection
      );

      HeroDefinition hero = HeroDatabase.GetByPlacement(
          0,
          heroX,
          heroY,
          wallDirection
      );

      if (hero == null)
      {
        if (heroRecruitmentPanel != null)
          heroRecruitmentPanel.Hide();

        return;
      }

      string displayName = string.IsNullOrEmpty(hero.Title)
          ? hero.Name
          : $"{hero.Name} {hero.Title}";

      Debug.Log(
          $"Champion in front: {displayName} " +
          $"using placement ({heroX},{heroY}) " +
          $"on the {wallDirection} wall. " +
          $"Player is at ({map.PlayerX},{map.PlayerY})."
      );

      if (heroRecruitmentPanel != null)
        heroRecruitmentPanel.ShowHero(hero);
    }

    private void GetHeroPlacementLookup(
        DungeonFacing facing,
        out int heroX,
        out int heroY,
        out HeroWallDirection wallDirection)
    {
      heroX = map.PlayerX;
      heroY = map.PlayerY;

      switch (facing)
      {
        case DungeonFacing.North:
          // Verified with DAROOU:
          // Player (6,12), facing North
          // Hero placement (6,13), North
          heroY += 1;
          wallDirection = HeroWallDirection.North;
          break;

        case DungeonFacing.East:
          wallDirection = HeroWallDirection.East;
          break;

        case DungeonFacing.South:
          wallDirection = HeroWallDirection.South;
          break;

        case DungeonFacing.West:
          wallDirection = HeroWallDirection.West;
          break;

        default:
          wallDirection = HeroWallDirection.North;
          break;
      }
    }
  }
}