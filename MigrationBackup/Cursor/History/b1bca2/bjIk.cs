using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Shared repair + layout for seat action badge PNGs.</summary>
    public static class ActionBadgeUtility
    {
        public static void Repair(GameObject actionBadgeGo, ActionBadge badge)
        {
            if (actionBadgeGo == null)
                return;

            MigrateFromSdfGraphic(actionBadgeGo);
            CleanupDuplicateComponents(actionBadgeGo);
            RemoveNonImageGraphics(actionBadgeGo);

            Image img = actionBadgeGo.GetComponent<Image>();
            if (img == null)
                img = actionBadgeGo.AddComponent<Image>();

            img.type           = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget  = false;
            img.maskable       = true;

            Transform label = actionBadgeGo.transform.Find("Label");
            if (label != null)
                label.gameObject.SetActive(false);

            if (badge != null && badge.UsesCustomLayout)
                badge.ApplyCustomLayout(actionBadgeGo.transform as RectTransform,
                    ActionBadgeSprites.For(BettingAction.Check) ?? ActionBadgeSprites.Winner);
            else
                ApplyAutoLayoutRect(actionBadgeGo.transform as RectTransform);

#if UNITY_EDITOR
            if (!Application.isPlaying && badge != null)
            {
                var so = new UnityEditor.SerializedObject(badge);
                so.Update();
                so.FindProperty("_badgeImage").objectReferenceValue = img;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
#endif

            if (badge != null)
                badge.WireBadgeImage(img);
        }

        private static void MigrateFromSdfGraphic(GameObject actionBadgeGo)
        {
            ActionBadgeSdfGraphic[] sdfs = actionBadgeGo.GetComponents<ActionBadgeSdfGraphic>();
            for (int i = 0; i < sdfs.Length; i++)
                DestroyGraphicImmediately(sdfs[i]);
        }

        private static void RemoveNonImageGraphics(GameObject actionBadgeGo)
        {
            Graphic[] graphics = actionBadgeGo.GetComponents<Graphic>();
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] is Image)
                    continue;

                DestroyGraphicImmediately(graphics[i]);
            }
        }

        public static void CleanupDuplicateComponents(GameObject actionBadgeGo)
        {
            ActionBadgeSdfGraphic[] graphics = actionBadgeGo.GetComponents<ActionBadgeSdfGraphic>();
            for (int i = 0; i < graphics.Length; i++)
                DestroyGraphicImmediately(graphics[i]);

            Image[] images = actionBadgeGo.GetComponents<Image>();
            for (int i = 1; i < images.Length; i++)
                DestroyComponent(images[i]);

            CanvasRenderer[] renderers = actionBadgeGo.GetComponents<CanvasRenderer>();
            for (int i = 1; i < renderers.Length; i++)
                DestroyComponent(renderers[i]);
        }

        public static void ApplyAutoLayoutRect(RectTransform rt, Sprite sprite = null)
        {
            if (rt == null)
                return;

            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(
                PlayerHudLayout.ResolveActionBadgeX(rt.parent),
                PlayerHudLayout.ResolveActionBadgeY(rt.parent));

            ActionBadgeSprites.EnsureLoaded();
            sprite ??= ActionBadgeSprites.For(BettingAction.Check) ?? ActionBadgeSprites.Winner;
            rt.sizeDelta = ActionBadgeSprites.SizeForSprite(sprite);
        }

        /// <summary>Legacy name — applies automatic card-centre layout.</summary>
        public static void ApplyLayoutRect(RectTransform rt) => ApplyAutoLayoutRect(rt);

        /// <summary>
        /// Seat <see cref="ActionBadge"/> pills only — removes mistaken SDF graphics from sprite buttons.
        /// </summary>
        public static void RestoreSpriteButton(Button button)
        {
            if (button == null)
                return;

            GameObject go = button.gameObject;

            ActionBadgeSdfGraphic sdf = go.GetComponent<ActionBadgeSdfGraphic>();
            if (sdf != null)
                DestroyGraphicImmediately(sdf);

            Transform sdfFrame = go.transform.Find("SdfFrame");
            if (sdfFrame != null)
                DestroyObject(sdfFrame.gameObject);

            Image img = go.GetComponent<Image>();
            if (img == null)
                return;

            img.enabled          = true;
            button.transition    = Selectable.Transition.SpriteSwap;
            button.targetGraphic = img;
        }

        private static void DestroyGraphicImmediately(Graphic graphic)
        {
            if (graphic == null)
                return;

            Object.DestroyImmediate(graphic);
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(obj);
            else
#endif
                Object.Destroy(obj);
        }

        private static void DestroyComponent(Object component)
        {
            if (component == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(component);
            else
#endif
                Object.Destroy(component);
        }
    }
}
