using UnityEngine;
using UnityEngine.UI;

namespace DM.Rendering
{
  /// <summary>
  /// Positions the live MovementArrows UI Image from ViewportLayout X/Y.
  /// Not drawn into the framebuffer.
  /// </summary>
  public static class MovementArrowsLayout
  {
    public const float Width = 87f;
    public const float Height = 45f;

    public static void Apply(
        Image arrows,
        int layoutX,
        int layoutY,
        bool enabled)
    {
      if (arrows == null)
        return;

      RectTransform arrowsRect = arrows.rectTransform;
      arrowsRect.anchorMin = Vector2.zero;
      arrowsRect.anchorMax = Vector2.zero;
      arrowsRect.pivot = Vector2.zero;
      arrowsRect.sizeDelta = new Vector2(Width, Height);
      arrowsRect.anchoredPosition = new Vector2(layoutX, layoutY);
      arrowsRect.localScale = Vector3.one;
      arrowsRect.localRotation = Quaternion.identity;

      arrows.preserveAspect = true;

      if (arrows.mainTexture != null)
        arrows.mainTexture.filterMode = FilterMode.Point;

      arrows.gameObject.SetActive(enabled);
    }
  }
}
