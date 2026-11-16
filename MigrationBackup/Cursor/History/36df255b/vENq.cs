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
        public const float PillH     = 76f;
        /// <summary>Minimum HudPanel height for Seat_You — band over card rank area + name/chips.</summary>
        public const float SeatYouPanelMinHeight = PillH;
        public const float PillY     = 0f;
        /// <summary>RoundedRect.png spriteBorder — panel rect extends this far past Card_1 so opaque fill reaches the card edge.</summary>
        public const float RoundedRectBorderPx = 14f;
        public const float HudGlowSpreadPx = 14f;
        public const float HudGlowPeakIntensity = 1.1f;
        public const float HudGlowFalloff = 1.1f;
        public const float AvatarD   = 162f;
        public const float AvatarX   = -133f;
        public const float TextX     = 25f;
        public const float TextW     = 155f;
        /// <summary>Wider name row; inner edge lines up with the chips row beside the avatar.</summary>
        public const float NameTextW = TextW;
        public const float NameTextH = 36f;
        public const float TextAvatarPad = 10f; // horizontal gap between text and avatar ring
        public const float NameChipsGap = 8f; // vertical space between name and chips rows
        public const float HudTextPadPx = 12f; // inset when centering text inside HudPanel
        public const float ChipsTextH   = 26f;

        /// <summary>Name/chips block centered vertically on HudPanel center.</summary>
        public static float GetCenteredNameY(float panelCenterY)
            => panelCenterY + (NameChipsGap + ChipsTextH) * 0.5f;

        public static float GetCenteredChipsY(float panelCenterY)
            => panelCenterY - (NameChipsGap + NameTextH) * 0.5f;

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
        /// Legacy avatar-side name X — not used for HudPanel name/chips (see ApplyHudPanelTextBlock).
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

        public static TextAlignmentOptions HudPanelTextAlign => TextAlignmentOptions.Midline;

        /// <summary>Reads HudPanel center and width after layout.</summary>
        public static void GetHudPanelLayout(Transform root, out float centerX, out float centerY, out float width)
        {
            centerX = 0f;
            centerY = 0f;
            width   = PillW;
            Transform hudPanel = FindChild(root, "HudPanel");
            if (hudPanel == null) return;
            var panelRt = (RectTransform)hudPanel;
            centerX = panelRt.anchoredPosition.x;
            centerY = panelRt.anchoredPosition.y;
            width   = panelRt.sizeDelta.x > 0f ? panelRt.sizeDelta.x : panelRt.rect.width;
        }

        /// <summary>
        /// NameText + ChipsText share the center of the uncovered HudPanel area (excluding avatar), stacked vertically.
        /// </summary>
        public static void ApplyHudPanelTextBlock(Transform root, float panelCenterX, float panelCenterY, float panelWidth)
        {
            var pv = root != null ? root.GetComponent<PlayerView>() : null;
            bool mirrored = pv != null ? pv.HudMirrored : false;

            float avatarX = AvatarPosX(mirrored);
            float textCenterX;
            float uncoveredWidth;

            if (mirrored)
            {
                float panelLeft = panelCenterX - panelWidth * 0.5f;
                float avatarLeft = avatarX - AvatarD * 0.5f;
                textCenterX = (panelLeft + avatarLeft) * 0.5f;
                uncoveredWidth = avatarLeft - panelLeft;
            }
            else
            {
                float panelRight = panelCenterX + panelWidth * 0.5f;
                float avatarRight = avatarX + AvatarD * 0.5f;
                textCenterX = (avatarRight + panelRight) * 0.5f;
                uncoveredWidth = panelRight - avatarRight;
            }

            float textWidth   = Mathf.Max(uncoveredWidth - HudTextPadPx * 2f, TextW);
            float nameY       = GetCenteredNameY(panelCenterY);
            float chipsY      = GetCenteredChipsY(panelCenterY);

            ApplyText(FindChild(root, "NameText"),   textCenterX, nameY, textWidth, NameTextH, HudPanelTextAlign);
            ApplyText(FindChild(root, "ChipsText"),  textCenterX, chipsY, textWidth, ChipsTextH, HudPanelTextAlign);
            ApplyText(FindChild(root, "StatusText"), textCenterX, chipsY, textWidth, 22f, HudPanelTextAlign);
        }

        /// <summary>Fixed left edge of the HUD content band (avatar overlap side).</summary>
        public static float ComputePanelLeftX(float hudCenterX) => hudCenterX - PillW * 0.5f;

        /// <summary>Rect left edge for a centre-anchored, centre-pivot RectTransform.</summary>
        public static float GetRectLeftX(RectTransform rt)
        {
            if (rt == null) return 0f;
            float w = rt.rect.width;
            if (w <= 0f)
                w = rt.sizeDelta.x;
            return rt.anchoredPosition.x - w * 0.5f;
        }

        /// <summary>Rect right edge for a centre-anchored, centre-pivot RectTransform.</summary>
        public static float GetRectRightX(RectTransform rt)
        {
            if (rt == null) return 0f;
            float w = rt.rect.width;
            if (w <= 0f)
                w = rt.sizeDelta.x;
            return rt.anchoredPosition.x + w * 0.5f;
        }

        /// <summary>Rect bottom edge for a centre-anchored, centre-pivot RectTransform.</summary>
        public static float GetRectBottomY(RectTransform rt)
        {
            if (rt == null) return 0f;
            float h = rt.rect.height;
            if (h <= 0f)
                h = rt.sizeDelta.y;
            return rt.anchoredPosition.y - h * 0.5f;
        }

        /// <summary>Symmetric hole-card centres — independent of HudPanel width.</summary>
        public static void ComputeHoleCardCenterX(
            float hudCenterX, float cardWidth, float cardGap,
            out float card0CenterX, out float card1CenterX)
        {
            float cx = (cardWidth + cardGap) * 0.5f;
            card0CenterX = hudCenterX - cx;
            card1CenterX = hudCenterX + cx;
        }

        /// <summary>
        /// Sizes HudPanel from Card_1. Borders on HudGlow extend past Card_1 so RoundedRect opaque fill covers card edges.
        /// Seat_You: compact PillH band over cards, bottom inset by Panel Bottom Border.
        /// </summary>
        public static void ApplyHudPanelFromCard1(Transform root, RectTransform card1, Vector2 hudLocalPx)
        {
            if (root == null) return;

            var pv = root.GetComponent<PlayerView>();
            bool mirrored = pv != null ? pv.HudMirrored : false;
            RectTransform card0 = pv != null ? pv.GetCardRect(0) : null;
            if (card1 == null && pv != null) card1 = pv.GetCardRect(1);

            var hudGlow = FindChild(root, "HudGlow")?.GetComponent<HudPanelGlowGraphic>();

            float panelWidth = PillW;
            float panelHeight = PillH;
            float panelCenterX = hudLocalPx.x;
            float panelCenterY = hudLocalPx.y;

            if (mirrored)
            {
                float panelRight = hudLocalPx.x + PillW * 0.5f;
                if (card0 != null)
                {
                    float card0Left = GetRectLeftX(card0);
                    float border = ResolvePanelRightBorderPx(hudGlow);
                    float panelRectLeft = card0Left - border;
                    panelWidth = Mathf.Max(PillW, panelRight - panelRectLeft);
                    panelCenterX = panelRight - panelWidth * 0.5f;

                    if (root.name.StartsWith("Seat_") || root.name == "PlayerView")
                    {
                        float cardBottom = GetRectBottomY(card0);
                        float bottomBorder = ResolvePanelBottomBorderPx(hudGlow);
                        float panelBottom = cardBottom - bottomBorder;
                        panelHeight = SeatYouPanelMinHeight;
                        panelCenterY = panelBottom + panelHeight * 0.5f;
                    }
                }
            }
            else
            {
                float panelLeft = ComputePanelLeftX(hudLocalPx.x);
                if (card1 != null)
                {
                    float card1Right = GetRectRightX(card1);
                    float border = ResolvePanelRightBorderPx(hudGlow);
                    float panelRectRight = card1Right + border;
                    panelWidth = Mathf.Max(PillW, panelRectRight - panelLeft);
                    panelCenterX = panelLeft + panelWidth * 0.5f;

                    if (root.name.StartsWith("Seat_") || root.name == "PlayerView")
                    {
                        float cardBottom = GetRectBottomY(card1);
                        float bottomBorder = ResolvePanelBottomBorderPx(hudGlow);
                        float panelBottom = cardBottom - bottomBorder;
                        panelHeight = SeatYouPanelMinHeight;
                        panelCenterY = panelBottom + panelHeight * 0.5f;
                    }
                }
            }

            float glowSpread = ResolveGlowSpreadPx(hudGlow);

            SetRect(FindChild(root, "HudPanel"), panelCenterX, panelCenterY, panelWidth, panelHeight);
            SetRect(FindChild(root, "HudGlow"), panelCenterX, panelCenterY,
                panelWidth + glowSpread * 2f, panelHeight + glowSpread * 2f);

            if (hudGlow != null)
            {
                hudGlow.PanelWidthPx  = panelWidth;
                hudGlow.PanelHeightPx = panelHeight;
            }
        }

        /// <summary>Same rim bloom on every seat — overrides per-seat Inspector drift.</summary>
        public static void ApplyStandardHudGlow(HudPanelGlowGraphic glow, Color glowColor)
        {
            if (glow == null)
                return;

            glow.GlowSpreadPx        = HudGlowSpreadPx;
            glow.PeakGlowIntensity   = HudGlowPeakIntensity;
            glow.GlowFalloff         = HudGlowFalloff;
            glow.GlowColor           = glowColor;
            glow.PanelRightBorderPx  = RoundedRectBorderPx;
            glow.PanelBottomBorderPx = RoundedRectBorderPx;
        }

        /// <summary>Uses the HudGlow component spread when set; default constant for new seats.</summary>
        private static float ResolveGlowSpreadPx(HudPanelGlowGraphic hudGlow)
        {
            if (hudGlow != null && hudGlow.GlowSpreadPx >= 8f)
                return hudGlow.GlowSpreadPx;
            return HudGlowSpreadPx;
        }

        private static float ResolvePanelRightBorderPx(HudPanelGlowGraphic hudGlow)
        {
            if (hudGlow != null)
                return hudGlow.PanelRightBorderPx;
            return RoundedRectBorderPx;
        }

        private static float ResolvePanelBottomBorderPx(HudPanelGlowGraphic hudGlow)
        {
            if (hudGlow != null)
                return hudGlow.PanelBottomBorderPx;
            return RoundedRectBorderPx;
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
            return new Vector2(0f, 0f);
        }

        /// <summary>Positions avatar, name/chips, action UI, and card-driven HudPanel.</summary>
        public static void Apply(Transform root, bool mirrored)
        {
            if (root == null) return;

            float avatarX   = AvatarPosX(mirrored);
            float textX     = TextPosX(mirrored);
            Vector2 hudLocalPx = ResolveHudLocalPx(root);

            SetRect(FindChild(root, "AvatarFrame"), avatarX, PillY, AvatarD, AvatarD);

            var pv = root.GetComponent<PlayerView>();
            RectTransform card1 = pv != null ? pv.GetCardRect(1) : null;
            ApplyHudPanelFromCard1(root, card1, hudLocalPx);

            GetHudPanelLayout(root, out float panelCenterX, out float panelCenterY, out float panelWidth);
            ApplyHudPanelTextBlock(root, panelCenterX, panelCenterY, panelWidth);

            SetRect(FindChild(root, "ActionBadge"),    textX, ActionBadgeY, ActionBadgeGlowW, ActionBadgeGlowH);
            SetRect(FindChild(root, "SeatActionMenu"), textX, SeatActionMenuY, SeatActionMenuW, SeatActionMenuH);

            ApplyHudDrawOrder(root, pv);
        }

        /// <summary>UGUI draw order: cards → HudPanel → HudGlow → avatar → rings → text (back to front).</summary>
        public static void ApplyHudDrawOrder(Transform root, PlayerView pv)
        {
            if (root == null) return;

            int idx = 0;
            if (pv != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    RectTransform card = pv.GetCardRect(i);
                    if (card != null)
                        card.SetSiblingIndex(idx++);
                }
            }

            Transform hudPanel = FindChild(root, "HudPanel");
            Transform hudGlow  = FindChild(root, "HudGlow");
            if (hudPanel != null) hudPanel.SetSiblingIndex(idx++);
            if (hudGlow != null)  hudGlow.SetSiblingIndex(idx++);

            Transform avatarFrame = FindChild(root, "AvatarFrame");
            if (avatarFrame != null)
            {
                avatarFrame.SetSiblingIndex(idx++);
                ApplyAvatarFrameDrawOrder(avatarFrame);
            }

            string[] frontOrder =
            {
                "NameText", "ChipsText", "StatusText",
                "SeatActionMenu", "ActionBadge", "BetAnchor", "BetDisplay", "DealerButtonAnchor"
            };
            foreach (string name in frontOrder)
            {
                Transform t = FindChild(root, name);
                if (t != null)
                    t.SetSiblingIndex(idx++);
            }
        }

        /// <summary>Inside AvatarFrame: portrait → chrome ring → gold ring (back to front).</summary>
        private static void ApplyAvatarFrameDrawOrder(Transform avatarFrame)
        {
            int idx = 0;
            string[] order = { "Avatar", "AvatarRingChrome", "AvatarRingGold" };
            foreach (string name in order)
            {
                Transform t = avatarFrame.Find(name);
                if (t != null)
                    t.SetSiblingIndex(idx++);
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
