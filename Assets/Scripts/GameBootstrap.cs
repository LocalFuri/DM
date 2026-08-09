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

  private void Awake()
  {
    HideLeftoverGameplayUi();
  }

  private void Start()
  {
    HideLeftoverGameplayUi();

    if (mapJson == null)
    {
      Debug.LogError(
          "GameBootstrap: Map JSON TextAsset is not assigned."
      );
      return;
    }

    DungeonMap map = DungeonMap.LoadFromJson(mapJson);

    if (keyboardInput == null)
      keyboardInput = GetComponent<DungeonKeyboardInput>();

    if (keyboardInput != null)
    {
      keyboardInput.Initialize(
          map,
          dungeonRenderer,
          heroRecruitmentPanel
      );
    }
    else
    {
      Debug.LogWarning(
          "GameBootstrap: DungeonKeyboardInput was not found."
      );
    }

    if (dungeonRenderer == null)
    {
      Debug.LogError(
          "GameBootstrap: DungeonRenderer is not assigned."
      );
      return;
    }

    dungeonRenderer.Render(map);
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
