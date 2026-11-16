using TMPro;
using UnityEngine;

namespace TexasHoldem
{
    /// <summary>
    /// Shared HUD band geometry for player seats. Default layout: avatar left, text right.
    /// Mirrored layout (left table seats): avatar right, text left — reference Lady Luck HUD.
    /// </summary>
    public static class PlayerHudLayout
    {
        public const float PillW     = 220f;
        /// <summary>Design content width; rect width = PillW + 2× corner inset for RoundedRect border.</summary>
        public const float PillH     = 76f;
        public const float PillY     = 0f;
        /// <summary>Shifts hole cards under HudPanel opaque fill (RoundedRect spriteBorder ≈ 14).</summary>
        public const float PillRoundedCornerInset = 14f;
        public const float HudGlowSpreadPx = 30f;
        public const float AvatarD   = 162f;
        public const float AvatarX   = -133f;
        public const float TextX     = 25f;
        public const float TextW     = 155f;
        /// <summary>Wider name row; inner edge lines up with the chips row beside the avatar.</summary>
        public const float NameTextW = 200f;
        public const float NameTextH = 36f;
        public const float TextAvatarPad = 10f; // horizontal gap between text and avatar ring
        public const float NameChipsGap = 8f; // vertical space between name and chips rows
        public const float NameY     = 16f;
        public const float ChipsY    = -12f - NameChipsGap * 0.5f;
        public const float ActionBadgeY = 32f;
        public const float ActionBadgeGlowW = 156f;
        public const float ActionBadgeGlowH = 64f;
        public const float SeatActionMenuY  = 32f;
        public const float SeatActionMenuW  = 155f;
        public const float SeatActionMenuH  = 118f;

        public static float AvatarPosX(bool mirrored) => mirrored ? -AvatarX : AvatarX;
        public static float TextPosX(bool mirrored)
            => mirrored ? -(TextX + TextAvatarPad) : (TextX + TextAvatarPad);

        /// <summary>
        /// Name row X — wide rect shares the chips row inner edge (toward the avatar) so both columns align.
        /// Long names grow away from the avatar into the extra width.
        /// </summary>
        public static float NameTextPosX(bool mirrored)
        {
            float textX    = TextPosX(mirrored);
            float halfChip = TextW * 0.5f;
            float halfName = NameTextW * 0.5f;
            float innerEdge = mirrored ? textX + halfChip : textX - halfChip;
            return mirrored ? innerEdge - halfName : innerEdge + halfName;
        }

        public static TextAlignmentOptions TextAlign(bool mirrored)
            => mirrored ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;

        /// <summary>Rect width so opaque fill right edge meets Card_1 rect right (RoundedRect border).</summary>
        public static float ComputePillRectWidth(float cornerInset) => PillW + cornerInset * 2f;

        /// <summary>Right edge of the 220px design content band (Card_1 target before corner tuck).</summary>
        public static float ComputeContentBandRight(float hudCenterX) => hudCenterX + PillW * 0.5f;

        /// <summary>
        /// HudPanel centre X: widened rect ends at content band right; opaque fill meets tucked Card_1.
        /// </summary>
        public static float ComputeHudPanelCenterX(float hudCenterX, float cornerInset, bool mirrored)
            => mirrored ? hudCenterX + cornerInset : hudCenterX - cornerInset;

        /// <summary>
        /// Card_1 tucks under opaque fill; HudPanel rect right = content band right (220px design edge).
        /// </summary>
        public static void ComputeHoleCardLayout(
            float hudCenterX, float cardWidth, float cardGap, float cornerInset,
            out float pillRectW, out float card0CenterX, out float card1CenterX)
        {
            float halfCard     = cardWidth * 0.5f;
            float contentRight = ComputeContentBandRight(hudCenterX);
            card1CenterX       = contentRight - halfCard - cornerInset;
            card0CenterX       = card1CenterX - cardWidth - cardGap;
            pillRectW          = ComputePillRectWidth(cornerInset);
        }

        private static float ResolvePillCornerInset(Transform root)
        {
            var pv = root.GetComponent<PlayerView>();
            if (pv != null)
            {
#if UNITY_EDITOR
                TableLayoutManager layout = Object.FindFirstObjectByType<TableLayoutManager>(
                    FindObjectsInactive.Include);
#else
                TableLayoutManager layout = Object.FindFirstObjectByType<TableLayoutManager>();
#endif
                if (layout != null)
                    return layout.PillCornerInset;
            }
            return PillRoundedCornerInset;
        }

        private static Vector2 ResolveHudLocalPx(Transform root)
        {
            var pv = root.GetComponent<PlayerView>();
            if (pv != null)
            {
#if UNITY_EDITOR
                TableLayoutManager layout = Object.FindFirstObjectByType<TableLayoutManager>(
                    FindObjectsInactive.Include);
#else
                TableLayoutManager layout = Object.FindFirstObjectByType<TableLayoutManager>();
#endif
                if (layout != null)
                {
                    PlayerView[] views = layout.GetPlayerViews();
                    for (int i = 0; i < views.Length; i++)
                    {
                        if (views[i] == pv)
                            return layout.GetSeatConfig(i).hudLocalPx;
                    }
                }
            }
            // Default fallback
            return new Vector2(0f, 0f);
        }

        /// <summary>Positions avatar, name/chips, and action UI for the chosen mirror mode.</summary>
        public static void Apply(Transform root, bool mirrored)
        {
            if (root == null) return;

            float avatarX   = AvatarPosX(mirrored);
            float textX     = TextPosX(mirrored);
            float nameTextX = NameTextPosX(mirrored);
            var   align     = TextAlign(mirrored);
            Vector2 hudLocalPx  = ResolveHudLocalPx(root);
            float   cornerInset = ResolvePillCornerInset(root);
            float   pillRectW   = ComputePillRectWidth(cornerInset);
            float   panelCenterX = ComputeHudPanelCenterX(hudLocalPx.x, cornerInset, mirrored);

            SetRect(FindChild(root, "AvatarFrame"), avatarX, PillY, AvatarD, AvatarD);
            ApplyText(FindChild(root, "NameText"),   nameTextX, NameY, NameTextW, NameTextH, align);
            ApplyText(FindChild(root, "ChipsText"),  textX, ChipsY, TextW, 26f, align);
            ApplyText(FindChild(root, "StatusText"), textX, ChipsY, TextW, 22f, align);

            SetRect(FindChild(root, "ActionBadge"),    textX, ActionBadgeY, ActionBadgeGlowW, ActionBadgeGlowH);
            SetRect(FindChild(root, "SeatActionMenu"), textX, SeatActionMenuY, SeatActionMenuW, SeatActionMenuH);
            SetRect(FindChild(root, "HudPanel"),       panelCenterX, hudLocalPx.y, pillRectW, PillH);
            SetRect(FindChild(root, "HudGlow"),        panelCenterX, hudLocalPx.y, pillRectW + HudGlowSpreadPx * 2f, PillH + HudGlowSpreadPx * 2f);

            var hudGlow = FindChild(root, "HudGlow")?.GetComponent<HudPanelGlowGraphic>();
            if (hudGlow != null)
            {
                hudGlow.PanelWidthPx  = pillRectW;
                hudGlow.PanelHeightPx = PillH;
            }
        }

        private static void ApplyText(Transform t, float x, float y, float w, float h, TextAlignmentOptions align)
        {
            if (t == null) return;
            SetRect(t, x, y, w, h);
            var txt = t.GetComponent<TMP_Text>();
            if (txt != null)
                txt.alignment = align;
        }

        private static void SetRect(Transform t, float x, float y, float w, float h)
        {
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt == null) return;
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta        = new Vector2(w, h);
        }

        private static Transform FindChild(Transform root, string name)
        {
            Transform direct = root.Find(name);
            if (direct != null) return direct;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }
            return null;
        }
    }
}
