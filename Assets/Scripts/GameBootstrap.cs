using DM.Dungeon;
using DM.Rendering;
using DM.UI;
using UnityEngine;
using UnityEngine.UI;

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

    int sourceX = sourceMap.PlayerX;
    int sourceY = sourceMap.PlayerY;
    DungeonFacing sourceFacing = sourceMap.PlayerFacing;

    DungeonMap targetMap = DungeonMap.LoadFromJson(levelMaps[targetLevel]);

    // A down stair arrives at an up stair on the next level, and vice versa.
    // Prefer the corresponding stair nearest the source local coordinate.
    // This keeps the transition generic and avoids Hall-of-Champions-specific
    // coordinate checks in gameplay code.
    bool targetStairsUp = !stairsUp;
    if (targetMap.TryFindStairs(
            targetStairsUp,
            sourceX,
            sourceY,
            out int targetX,
            out int targetY))
    {
      targetMap.SetPlayerPose(targetX, targetY, sourceFacing);
    }
    else
    {
      Debug.LogWarning(
          $"GameBootstrap: Level {targetLevel} has no " +
          $"{(targetStairsUp ? "stairs up" : "stairs down")} tile. " +
          "Using that map's playerStart instead."
      );
    }

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
