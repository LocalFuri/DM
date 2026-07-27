using DM.Dungeon;
using DM.Heroes;
using DM.Rendering;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
  [SerializeField]
  private DungeonRenderer dungeonRenderer;

  [SerializeField]
  private DungeonKeyboardInput keyboardInput;

  [SerializeField]
  private TextAsset mapJson;

  private void Start()
  {
    Debug.Log("GameBootstrap started.");

    TestHeroDatabase();

    if (mapJson == null)
    {
      Debug.LogError(
          "GameBootstrap: Map JSON TextAsset is not assigned."
      );
      return;
    }

    DungeonMap map = DungeonMap.LoadFromJson(mapJson);

    Debug.Log($"Loaded map: {map.Name}");
    Debug.Log($"Map size: {map.Width} x {map.Height}");
    Debug.Log(
        $"Player ({map.PlayerX},{map.PlayerY}) " +
        $"facing {map.PlayerFacing}."
    );
    Debug.Log(map.BuildDebugMap());

    if (keyboardInput == null)
      keyboardInput = GetComponent<DungeonKeyboardInput>();

    if (keyboardInput != null)
      keyboardInput.Initialize(map, dungeonRenderer);

    dungeonRenderer.Render(map);
  }

  private void TestHeroDatabase()
  {
    HeroDefinition hero = HeroDatabase.GetById(1);

    if (hero == null)
    {
      Debug.LogError(
          "Hero database test failed: Hero ID 1 was not found."
      );
      return;
    }

    Debug.Log(
        $"Hero database test: {hero.Name}, " +
        $"{hero.Title}, Health {hero.Health}, " +
        $"Starting items {hero.StartingItems.Count}."
    );
  }
}