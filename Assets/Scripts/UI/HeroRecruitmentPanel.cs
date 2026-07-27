using DM.Heroes;
using UnityEngine;

namespace DM.UI
{
  public class HeroRecruitmentPanel : MonoBehaviour
  {
    [SerializeField]
    private GameObject panelRoot;

    private HeroDefinition currentHero;

    public HeroDefinition CurrentHero => currentHero;

    private void Awake()
    {
      Hide();
    }

    public void ShowHero(HeroDefinition hero)
    {
      if (hero == null)
      {
        Debug.LogWarning(
            "HeroRecruitmentPanel: Cannot show a null hero."
        );
        return;
      }

      currentHero = hero;

      if (panelRoot != null)
      {
        panelRoot.SetActive(true);
      }

      string displayName = string.IsNullOrEmpty(hero.Title)
          ? hero.Name
          : $"{hero.Name} {hero.Title}";

      Debug.Log(
          $"Hero recruitment panel opened for {displayName}."
      );
    }

    public void Hide()
    {
      currentHero = null;

      if (panelRoot != null)
      {
        panelRoot.SetActive(false);
      }
    }
  }
}