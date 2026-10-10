using DM.Dungeon;
using DM.Rendering;
using DM.UI;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;

public class GameBootstrap : MonoBehaviour
{
  [SerializeField]
  private DungeonRenderer dungeonRenderer;

  [SerializeField]
  private DungeonKeyboardInput keyboardInput;

  [SerializeField]
  private HeroRecruitmentPanel heroRecruitmentPanel;

  [SerializeField]
  private TextAsset mapJson;

  [Header("Dungeon Levels")]
  [SerializeField]
  [Tooltip(
      "Optional ordered level maps. Index 0 = Hall of Champions, " +
      "index 1 = Level 1, etc. If empty, mapJson remains the startup map.")]
  private TextAsset[] levelMaps;

  [SerializeField]
  [Min(0)]
  private int startingLevel = 0;

  private int currentLevel;
  private DungeonMap currentMap;

  private void Awake()
  {
    HideLeftoverGameplayUi();
  }

  private void Start()
  {
    HideLeftoverGameplayUi();

    if (keyboardInput == null)
      keyboardInput = GetComponent<DungeonKeyboardInput>();

    if (dungeonRenderer == null)
    {
      Debug.LogError(
          "GameBootstrap: DungeonRenderer is not assigned."
      );
      return;
    }

    TextAsset startupMap = GetStartupMap(out currentLevel);
    if (startupMap == null)
    {
      Debug.LogError(
          "GameBootstrap: No startup map is assigned. " +
          "Assign mapJson or levelMaps[0]."
      );
      return;
    }

    currentMap = DungeonMap.LoadFromJson(startupMap);

    if (keyboardInput != null)
    {
      keyboardInput.Initialize(
          currentMap,
          dungeonRenderer,
          heroRecruitmentPanel,
          this
      );
    }
    else
    {
      Debug.LogWarning(
          "GameBootstrap: DungeonKeyboardInput was not found."
      );
    }

    dungeonRenderer.Render(currentMap);
  }

  private TextAsset GetStartupMap(out int levelIndex)
  {
    levelIndex = 0;

    if (levelMaps != null && levelMaps.Length > 0)
    {
      int clampedLevel = Mathf.Clamp(
          startingLevel,
          0,
          levelMaps.Length - 1);

      if (levelMaps[clampedLevel] != null)
      {
        levelIndex = clampedLevel;
        return levelMaps[clampedLevel];
      }
    }

    return mapJson;
  }

  public bool TryTransitionOnCurrentTile(
      DungeonMap sourceMap,
      out DungeonMap transitionedMap)
  {
    transitionedMap = sourceMap;

    if (sourceMap == null
        || !sourceMap.TryGetStairsAtPlayer(out bool stairsUp))
    {
      return false;
    }

    int targetLevel = stairsUp ? currentLevel - 1 : currentLevel + 1;

    if (levelMaps == null
        || targetLevel < 0
        || targetLevel >= levelMaps.Length
        || levelMaps[targetLevel] == null)
    {
      Debug.LogWarning(
          $"GameBootstrap: Player stepped on " +
          $"{(stairsUp ? "stairs up" : "stairs down")} at " +
          $"level {currentLevel} ({sourceMap.PlayerX},{sourceMap.PlayerY}), " +
          $"but level {targetLevel} is not assigned in levelMaps."
      );
      return false;
    }

    // Staircases are paired by global coordinates, NOT nearest local tile.
    // The map_info offsets are part of the original DUNGEON.DAT layout.
    if (!TryGetMapOffsets(levelMaps[currentLevel],
            out int sourceOffsetX, out int sourceOffsetY) ||
        !TryGetMapOffsets(levelMaps[targetLevel],
            out int targetOffsetX, out int targetOffsetY))
    {
      Debug.LogError(
          "GameBootstrap: Stair transition requires OffsetMapX/OffsetMapY " +
          "in both level JSON files. Use DungeonMaster_Level00..13.json. " +
          "Transition cancelled; no destination guessed.");
      return false;
    }

    int sourceX = sourceMap.PlayerX;
    int sourceY = sourceMap.PlayerY;
    int targetX = sourceX + sourceOffsetX - targetOffsetX;
    int targetY = sourceY + sourceOffsetY - targetOffsetY;
    DungeonMap targetMap = DungeonMap.LoadFromJson(levelMaps[targetLevel]);

    if (!targetMap.IsInside(targetX, targetY) ||
        !targetMap.GetTile(targetX, targetY)
            .TryGetStairsDirection(out bool destinationIsUp) ||
        destinationIsUp == stairsUp ||
        !targetMap.CanEnter(targetX, targetY))
    {
      Debug.LogError(
          $"GameBootstrap: No matching opposite staircase at " +
          $"level {targetLevel} ({targetX},{targetY}) for " +
          $"level {currentLevel} ({sourceX},{sourceY}). " +
          "Transition cancelled; no fallback.");
      return false;
    }

    // Raw stair byte bit 3 selects N/S versus E/W orientation. The
    // original maps place only one traversable exit on that axis.
    // Arrival facing is therefore derived from the destination geometry.
    if (!TryGetStairExitFacing(targetMap, targetX, targetY,
            out DungeonFacing arrivalFacing))
    {
      Debug.LogError(
          $"GameBootstrap: Cannot determine unique stair exit at " +
          $"level {targetLevel} ({targetX},{targetY}); " +
          "transition cancelled.");
      return false;
    }

    targetMap.SetPlayerPose(targetX, targetY, arrivalFacing);

    currentLevel = targetLevel;
    currentMap = targetMap;
    transitionedMap = targetMap;

    if (heroRecruitmentPanel != null)
      heroRecruitmentPanel.Hide();

    if (keyboardInput != null)
      keyboardInput.SetMap(targetMap);

    dungeonRenderer.Render(targetMap);
    dungeonRenderer.RequestRedraw();

    Debug.Log(
        $"Dungeon level transition: now on level {currentLevel} at " +
        $"({targetMap.PlayerX},{targetMap.PlayerY}) facing " +
        $"{targetMap.PlayerFacing}."
    );

    return true;
  }

  private static bool TryGetMapOffsets(
      TextAsset asset, out int offsetX, out int offsetY)
  {
    offsetX = 0;
    offsetY = 0;
    if (asset == null) return false;
    Match mx = Regex.Match(asset.text, "\"OffsetMapX\"\\s*:\\s*(-?\\d+)");
    Match my = Regex.Match(asset.text, "\"OffsetMapY\"\\s*:\\s*(-?\\d+)");
    return mx.Success && my.Success &&
        int.TryParse(mx.Groups[1].Value, out offsetX) &&
        int.TryParse(my.Groups[1].Value, out offsetY);
  }

  private static bool TryGetStairExitFacing(
      DungeonMap map, int x, int y, out DungeonFacing facing)
  {
    facing = DungeonFacing.North;
    int raw = map.GetTile(x, y).Raw;
    bool northSouth = (raw & 8) != 0;
    DungeonFacing first = northSouth ? DungeonFacing.North : DungeonFacing.West;
    DungeonFacing second = northSouth ? DungeonFacing.South : DungeonFacing.East;
    bool firstOpen = IsStairExitEnterable(map, x, y, first);
    bool secondOpen = IsStairExitEnterable(map, x, y, second);
    if (firstOpen == secondOpen) return false;
    facing = firstOpen ? first : second;
    return true;
  }

  private static bool IsStairExitEnterable(
      DungeonMap map, int x, int y, DungeonFacing facing)
  {
    DungeonMap.GetForwardOffset(facing, out int dx, out int dy);
    return map.CanEnter(x + dx, y + dy);
  }

  // Keep leftover Canvas UI from sitting over the gameplay layout.
  // Does not touch DungeonViewport, MovementArrows, or DungeonRenderer.
  private void HideLeftoverGameplayUi()
  {
    if (heroRecruitmentPanel != null)
      heroRecruitmentPanel.Hide();

    HeroRecruitmentPanel[] panels =
        Object.FindObjectsByType<HeroRecruitmentPanel>(
            FindObjectsInactive.Include
        );

    foreach (HeroRecruitmentPanel panel in panels)
    {
      if (panel != null)
        panel.Hide();
    }

    RawImage[] rawImages = Object.FindObjectsByType<RawImage>(
        FindObjectsInactive.Include
    );

    foreach (RawImage rawImage in rawImages)
    {
      if (rawImage == null)
        continue;

      string objectName = rawImage.gameObject.name;
      if (objectName == "DungeonViewport")
        continue;

      if (objectName == "EntranceScreen"
          || objectName == "___DM_ViewportReferenceOverlay")
      {
        rawImage.gameObject.SetActive(false);
      }
    }
  }
}
