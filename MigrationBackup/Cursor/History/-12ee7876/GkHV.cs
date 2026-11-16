using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace TexasHoldem
{
    /// <summary>
    /// Rebuilds all PlayerView seats to the PokerStars-style HUD layout and keeps the
    /// PlayerView.prefab in sync. Run via Texas Holdem → Apply Player Seat Layout.
    ///
    /// Target hierarchy (siblings listed back to front for UGUI draw order):
    ///   PlayerView            — pure positional anchor; no Image background
    ///   ├── Card_0 / Card_1   — hole cards; render behind HudPanel
    ///   ├── HudPanel          — dark rounded-rect pill over cards
    ///   ├── HudGlow           — turn pulse rim over panel edge, under avatar
    ///   ├── AvatarFrame       — Avatar → AvatarRingChrome → AvatarRingGold
    ///   ├── NameText
    ///   ├── ChipsText
        ///   ├── StatusText        — overlays ChipsText when Folded / All In / Eliminated
        ///   ├── SeatActionMenu    — tappable Check / Fold / Raise / All-In above the name (human turn)
        ///   ├── ActionBadge       — neon pill when player Checks / Folds / Raises / All-In
        ///   └── BetDisplay        — chip stack + amount badge beside avatar
    ///
    /// Re-running is safe: existing GameObjects are reused, old ones renamed or hidden.
    ///
    /// Hole-card Y in TableLayoutManager SeatConfig is relative to the card-area centre
    /// (root Y + 55). ApplyHoleCards adds that anchor offset at runtime.
    /// </summary>
    public static class PlayerSeatLayout
    {
        // ── Layout constants (root-local space, all anchors / pivots = centre) ─

        private const float PillW = 220f;
        private const float PillH =  76f;
        private const float PillY =   0f;   // pill centre Y
        private const float HudGlowSpreadPx = 14f; // tight side-wing bloom outside the pill edge

        private const float CardsAreaX = 25f;   // card-dimmer footprint centre X (root space)
        private const float CardsAreaY = 55f;   // card-dimmer footprint centre Y (root space)
        private const float CardsAreaW = 124f;
        private const float CardsAreaH =  82f;

        private const float AvatarD           = 162f;  // ring container outer diameter — matches AvatarRingChrome (crome_trans.png)
        private const float AvatarRingStrokePx =  6f;  // shared chrome + gold ring thickness
        private const float AvatarImgD = 122f;  // avatar mask diameter — fills the chrome ring inner transparent hole
        private const float AvatarX    = -133f; // ~25 % horizontal overlap with pill left edge

        private const float TextX = 25f;    // centre X for name / chips (right zone of pill)
        private const float TextW = 155f;

        // ── Font sizes — change here, then run Texas Holdem → Apply Text Sizes ──
        private const float NameFontSize   = 20f;
        private const float ChipsFontSize  = 13f;
        private const float StatusFontSize = 10f;

        private const float BadgeW =  80f;
        private const float BadgeH =  28f;
        private const float BadgeX =  25f;
        private const float BadgeY = 105f;  // floating above the seat root

        private const float ActionBadgeW     = 120f;
        private const float ActionBadgeH     =  40f;
        private const float ActionBadgeGlowW = 156f;
        private const float ActionBadgeGlowH =  64f;
        private const float ActionBadgeX     =  TextX; // centred on name/chips band
        private const float ActionBadgeY     =  32f;   // above NameText, overlapping lower card edge (reference)

        private const float SeatActionMenuX  =  25f;
        private const float SeatActionMenuY  =  32f;  // name/chips band on the HUD pill (name hidden while open)
        private const float SeatActionMenuW  = 155f;  // match TextW
        private const float SeatActionMenuH  = 118f;
        private const float SeatActionRowH   =  18f;
        private const float SeatActionInputH =  22f;

        // ── Colours ──────────────────────────────────────────────────────────

        private static readonly Color PillColor      = new Color(0.1235f, 0.1235f, 0.152f, 0.95f);
        private static readonly Color BadgeBgColor   = new Color(0.08f, 0.08f, 0.10f, 0.94f);
        private static readonly Color NameColor      = Color.white;
        private static readonly Color ChipsColor     = new Color(0.87f, 0.69f, 0.20f, 1f);
        private static readonly Color BadgeTextColor = new Color(1.00f, 0.85f, 0.15f, 1f);
        private static readonly Color StatusColor    = new Color(1.00f, 0.35f, 0.35f, 1f);

        // ── Asset paths ───────────────────────────────────────────────────────

        private const string CirclePath      = "Assets/Graphic/UI/Circle.png";
        private const string RoundedRectPath = "Assets/Graphic/UI/RoundedRect.png";
        private const string ChromeRingPath  = "Assets/Graphic/UI/ChromeRing.png";
        private const string CromeTransPath  = "Assets/Graphic/crome_trans.png";
        private const string CircleGoldPath  = "Assets/Graphic/CircleGold_trans.png";
        private const string Chip1Path       = "Assets/Graphic/Chips/chip1.png";
        private const string Chip5Path       = "Assets/Graphic/Chips/chip5.png";
        private const string Chip25Path      = "Assets/Graphic/Chips/chip25.png";
        private const string PrefabPath      = "Assets/Prefabs/PlayerView.prefab";

        // ── Undo mode — disabled when editing the prefab asset directly ───────

        private static bool _useUndo = true;

        // ── Menu entry ────────────────────────────────────────────────────────

        /// <summary>
        /// Removes fontSize prefab overrides from every seat's NameText, ChipsText and StatusText
        /// so they all inherit the value set directly on the prefab.
        /// </summary>
        [MenuItem("Texas Holdem/Clear Text Size Overrides")]
        public static void ClearTextSizeOverrides()
        {
            string[] textNames = { "NameText", "ChipsText", "StatusText" };
            string[] propNames = { "m_fontSize", "m_fontSizeBase", "m_enableAutoSizing",
                                   "m_fontSizeMin", "m_fontSizeMax" };

            int cleared = 0;
            PlayerView[] views = Object.FindObjectsOfType<PlayerView>(true);
            foreach (PlayerView v in views)
            {
                foreach (string textName in textNames)
                {
                    Transform t = FindDeep(v.transform, textName);
                    if (t == null) continue;
                    TMP_Text txt = t.GetComponent<TMP_Text>();
                    if (txt == null) continue;

                    SerializedObject so = new SerializedObject(txt);
                    foreach (string propName in propNames)
                    {
                        SerializedProperty sp = so.FindProperty(propName);
                        if (sp != null && sp.prefabOverride)
                        {
                            PrefabUtility.RevertPropertyOverride(sp, InteractionMode.AutomatedAction);
                            cleared++;
                        }
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[PlayerSeatLayout] Cleared {cleared} text size override(s) across {views.Length} seat(s).");
        }

        /// <summary>Sets font sizes on all seats directly. Change the constants above, then run this.</summary>
        [MenuItem("Texas Holdem/Apply Text Sizes")]
        public static void ApplyTextSizes()
        {
            PlayerView[] views = Object.FindObjectsOfType<PlayerView>(true);
            foreach (PlayerView v in views)
            {
                SetTextSize(v.transform, "NameText",   NameFontSize);
                SetTextSize(v.transform, "ChipsText",  ChipsFontSize);
                SetTextSize(v.transform, "StatusText", StatusFontSize);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[PlayerSeatLayout] Text sizes applied to {views.Length} seat(s).");
        }

        private static void SetTextSize(Transform root, string childName, float size)
        {
            Transform t = FindDeep(root, childName);
            if (t == null) return;
            TMP_Text txt = t.GetComponent<TMP_Text>();
            if (txt == null) return;
            Undo.RecordObject(txt, "Apply Text Sizes");
            txt.enableAutoSizing = false;
            txt.fontSize = size;
            EditorUtility.SetDirty(txt);
        }

        /// <summary>
        /// Sets ChipsText on every seat to the starting-chip count formatted with German
        /// number style (period as thousand separator). Run this to keep scene mode in sync
        /// with the runtime formatting in PlayerView.RefreshHud.
        /// </summary>
        [MenuItem("Texas Holdem/Apply Chips Format")]
        public static void ApplyChipsFormat()
        {
            // Read _startingChips from GameManager via SerializedObject.
            int chips = 1000;
            GameManager gm = Object.FindObjectOfType<GameManager>();
            if (gm != null)
            {
                SerializedProperty chipsProp = new SerializedObject(gm).FindProperty("_startingChips");
                if (chipsProp != null) chips = chipsProp.intValue;
            }

            // Platform-independent German NFI: period as thousand separator.
            var nfi = new System.Globalization.NumberFormatInfo
            {
                NumberGroupSeparator   = ".",
                NumberDecimalSeparator = ",",
                NumberDecimalDigits    = 0,
                NumberGroupSizes       = new[] { 3 }
            };
            string formatted = chips.ToString("N0", nfi) + " \u20AC";

            int count = 0;
            foreach (PlayerView v in Object.FindObjectsOfType<PlayerView>(true))
            {
                Transform chipsT = FindDeep(v.transform, "ChipsText");
                if (chipsT == null) continue;
                TMP_Text txt = chipsT.GetComponent<TMP_Text>();
                if (txt == null) continue;
                Undo.RecordObject(txt, "Apply Chips Format");
                txt.text = formatted;
                EditorUtility.SetDirty(txt);
                count++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[PlayerSeatLayout] ChipsText set to \"{formatted}\" on {count} seat(s).");
        }

        [MenuItem("Texas Holdem/Repair Action Badges In Scene")]
        public static void RepairActionBadgesInScene()
        {
            ActionBadge[] badges = Object.FindObjectsOfType<ActionBadge>(true);
            foreach (ActionBadge badge in badges)
                ActionBadgeUtility.Repair(badge.gameObject, badge);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[PlayerSeatLayout] Repaired {badges.Length} ActionBadge(s) in the active scene.");
        }

        [MenuItem("Texas Holdem/Apply Player Seat Layout")]
        public static void ApplyLayout()
        {
            Sprite circle      = EnsureCircleSprite();
            Sprite roundedRect = EnsureRoundedRectSprite();
            Sprite chromeRing  = EnsureChromeRingSprite();

            // 1. Edit the prefab asset directly (Undo does not apply to asset contents).
            try
            {
                _useUndo = false;
                ApplyToPrefabAsset(circle, roundedRect, chromeRing);
            }
            finally
            {
                _useUndo = true;
            }

            // 2. Sync mirror flags from TableLayoutManager before applying geometry.
            SyncHudMirrorFromTableLayout();

            // 3. Apply to all scene instances with full undo support.
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Apply Player Seat Layout");

            PlayerView[] views = Object.FindObjectsOfType<PlayerView>(true);
            foreach (PlayerView v in views)
                ApplyToView(v, circle, roundedRect, chromeRing);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[PlayerSeatLayout] Applied to prefab + {views.Length} scene seat(s).");

            EditorApplication.delayCall -= RefreshSceneAvatars;
            EditorApplication.delayCall += RefreshSceneAvatars;
        }

        private static void RefreshSceneAvatars()
        {
            EditorApplication.delayCall -= RefreshSceneAvatars;
            if (Application.isPlaying) return;

#if UNITY_2022_2_OR_NEWER
            UIManager[] managers = Object.FindObjectsByType<UIManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            UIManager[] managers = Object.FindObjectsOfType<UIManager>(true);
#endif
            foreach (UIManager manager in managers)
            {
                if (manager != null)
                    manager.ApplySceneModePreview();
            }
        }

        // ── Prefab asset editing ──────────────────────────────────────────────

        private static void ApplyToPrefabAsset(Sprite circle, Sprite roundedRect, Sprite chromeRing)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (contents == null)
            {
                Debug.LogWarning("[PlayerSeatLayout] Prefab not found: " + PrefabPath);
                return;
            }
            try
            {
                PlayerView view = contents.GetComponent<PlayerView>();
                if (view != null) ApplyToView(view, circle, roundedRect, chromeRing);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ── Legacy names that are fully removed (not just hidden) ────────────

        private static readonly string[] LegacyDestroyNames =
            { "ChipsBarBg", "ActiveTurnIndicator" };

        /// <summary>
        /// Removes fully obsolete GameObjects and any duplicate direct children
        /// that share the same name (keeps the first occurrence of each name).
        /// Call this before GetOrCreate so every subsequent lookup is unambiguous.
        /// </summary>
        private static void CleanupLegacyAndDuplicates(Transform root)
        {
            // 1. Destroy all occurrences of known-obsolete names.
            foreach (string legacyName in LegacyDestroyNames)
            {
                bool again = true;
                while (again)
                {
                    again = false;
                    for (int i = root.childCount - 1; i >= 0; i--)
                    {
                        if (root.GetChild(i).name == legacyName)
                        {
                            DestroyObj(root.GetChild(i).gameObject);
                            again = true;
                            break;
                        }
                    }
                }
            }

            // 2. For any name that appears more than once keep only the first child
            //    (the one the layout tool already set up) and destroy the rest.
            var seen      = new HashSet<string>();
            var toDestroy = new List<GameObject>();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (!seen.Add(child.name))
                    toDestroy.Add(child.gameObject);
            }
            foreach (GameObject go in toDestroy)
                DestroyObj(go);
        }

        private static void SyncHudMirrorFromTableLayout()
        {
#if UNITY_2022_2_OR_NEWER
            TableLayoutManager layout = Object.FindFirstObjectByType<TableLayoutManager>(
                FindObjectsInactive.Include);
#else
            TableLayoutManager layout = Object.FindObjectOfType<TableLayoutManager>(true);
#endif
            layout?.SyncHudMirrorFlags();
        }

        private static bool ResolveHudMirrored(PlayerView view)
        {
#if UNITY_2022_2_OR_NEWER
            TableLayoutManager layout = Object.FindFirstObjectByType<TableLayoutManager>(
                FindObjectsInactive.Include);
#else
            TableLayoutManager layout = Object.FindObjectOfType<TableLayoutManager>(true);
#endif
            if (layout != null)
            {
                PlayerView[] views = layout.GetPlayerViews();
                for (int i = 0; i < views.Length; i++)
                {
                    if (views[i] == view)
                        return layout.GetSeatConfig(i).mirrorHud;
                }
            }

            return view.HudMirrored;
        }

        private static Vector2 ResolveHudLocalPx(PlayerView view)
        {
#if UNITY_2022_2_OR_NEWER
            TableLayoutManager layout = Object.FindFirstObjectByType<TableLayoutManager>(
                FindObjectsInactive.Include);
#else
            TableLayoutManager layout = Object.FindObjectOfType<TableLayoutManager>(true);
#endif
            if (layout != null)
            {
                PlayerView[] views = layout.GetPlayerViews();
                for (int i = 0; i < views.Length; i++)
                {
                    if (views[i] == view)
                        return layout.GetSeatConfig(i).hudLocalPx;
                }
            }

            // Fallback to mirrored default
            return new Vector2(0f, 0f);
        }

        private static void ResolveHoleCardLayout(
            PlayerView view, Vector2 hudLocalPx, bool mirrored,
            out float card0X, out float card1X, out float cardW, out float cardH)
        {
            cardW = 120f;
            cardH = cardW * (95f / 65f);
            float cardGap = 16f;

#if UNITY_2022_2_OR_NEWER
            TableLayoutManager layout = Object.FindFirstObjectByType<TableLayoutManager>(
                FindObjectsInactive.Include);
#else
            TableLayoutManager layout = Object.FindObjectOfType<TableLayoutManager>(true);
#endif
            if (layout != null)
            {
                cardW   = layout.HoleCardWidth;
                cardH   = cardW * (95f / 65f);
                cardGap = layout.HoleCardGap;
            }

            PlayerHudLayout.ComputeHoleCardCenterX(
                hudLocalPx.x, cardW, cardGap, out card0X, out card1X);
        }

        // ── Per-seat logic ────────────────────────────────────────────────────

        private static void ApplyToView(PlayerView view, Sprite circle, Sprite roundedRect, Sprite chromeRing)
        {
            Transform root = view.transform;
            bool mirrored  = ResolveHudMirrored(view);
            view.SetHudMirrored(mirrored);

            float avatarX = PlayerHudLayout.AvatarPosX(mirrored);
            float textX   = PlayerHudLayout.TextPosX(mirrored);
            var   textAlign = PlayerHudLayout.TextAlign(mirrored);

            // 0a. Remove stale duplicates and obsolete legacy objects first so that
            //     every subsequent GetOrCreate finds exactly one match (or none).
            CleanupLegacyAndDuplicates(root);

            // 0b. Root is a pure positional anchor — remove any background Image.
            var rootImg = view.GetComponent<Image>();
            if (rootImg != null)
            {
                RecordObj(rootImg);
                rootImg.color  = Color.clear;
                rootImg.sprite = null;
            }

            // 1. Hole cards — direct children of seat root, drawn behind HudPanel.
            List<CardView> cards = ReadCardSlots(view);
            MigrateHoleCardsFromLegacyPanel(root, cards);
            foreach (CardView card in cards)
            {
                if (card != null && card.transform.parent != root)
                    ReparentTo(card.transform, root);
            }
            if (cards.Count > 0 && cards[0] != null)
            {
                Vector2 hudLocalPxEarly = ResolveHudLocalPx(view);
                ResolveHoleCardLayout(view, hudLocalPxEarly, mirrored,
                    out float x0, out float x1, out float cardW, out float cardH);
                SetRect(cards[0].gameObject, x0, CardsAreaY, cardW, cardH);
                if (cards.Count > 1 && cards[1] != null)
                    SetRect(cards[1].gameObject, x1, CardsAreaY, cardW, cardH);
            }
            else if (cards.Count > 1 && cards[1] != null)
            {
                Vector2 hudLocalPxEarly = ResolveHudLocalPx(view);
                ResolveHoleCardLayout(view, hudLocalPxEarly, mirrored,
                    out float x0, out float x1, out float cardW, out float cardH);
                SetRect(cards[1].gameObject, x1, CardsAreaY, cardW, cardH);
            }

            // 1b. Remove legacy CardDimmer if it still exists.
            DestroyIfExists(root, "CardDimmer");

            // 2. HudGlow + HudPanel — width/position derived from Card_1 after cards are placed.
            GameObject         hudGlowGo  = GetOrCreate(root, "HudGlow");
            var                staleGlowImg = hudGlowGo.GetComponent<Image>();
            if (staleGlowImg != null) DestroyObj(staleGlowImg);
            HudPanelGlowGraphic hudGlowGfx = GetOrAdd<HudPanelGlowGraphic>(hudGlowGo);
            RecordObj(hudGlowGo);
            RecordObj(hudGlowGfx);
            Vector2 hudLocalPx = ResolveHudLocalPx(view);
            if (hudGlowGfx.GlowSpreadPx < 8f)
                hudGlowGfx.GlowSpreadPx = HudGlowSpreadPx;
            hudGlowGfx.GlowIntensity  = 0f;
            hudGlowGfx.color          = Color.white;
            hudGlowGfx.raycastTarget  = false;

            //    HudPanel — dark rounded-rect pill (renders on top of HudGlow).
            GameObject hudGo  = GetOrCreate(root, "HudPanel");
            CleanupHudPanelComponents(hudGo);
            var        hudImg = GetOrAdd<Image>(hudGo);
            RecordObj(hudImg);
            hudImg.sprite = roundedRect;
            hudImg.type   = Image.Type.Sliced;
            hudImg.color  = PillColor;

            RectTransform card1Rt = cards.Count > 1 && cards[1] != null
                ? (RectTransform)cards[1].transform
                : null;
            PlayerHudLayout.ApplyHudPanelFromCard1(root, card1Rt, hudLocalPx);

            DestroyIfExists(root, "TimerBar");
            DestroyIfExists(root, "ActiveGlow");

            // 4. AvatarFrame — pure positioning container; no Image or Mask at this level.
            //    Clipping and the decorative ring live in separate children so the chrome
            //    ring can render on top of the masked avatar without itself being clipped.
            RenameIfExists(root, "AvatarIcon", "AvatarFrame");
            RenameIfExists(root, "Avatar",     "AvatarFrame");  // guard against very old names
            GameObject avatarFrameGo = GetOrCreate(root, "AvatarFrame");
            RecordObj(avatarFrameGo);
            // Remove stale Image / Mask from AvatarFrame root (moved to children in this version).
            var staleFrameImg  = avatarFrameGo.GetComponent<Image>();
            if (staleFrameImg  != null) DestroyObj(staleFrameImg);
            var staleFrameMask = avatarFrameGo.GetComponent<Mask>();
            if (staleFrameMask != null) DestroyObj(staleFrameMask);
            avatarFrameGo.SetActive(true);
            SetRect(avatarFrameGo, avatarX, PillY, AvatarD, AvatarD);

            // Migrate: old layout placed Avatar as a direct child of AvatarFrame.
            // Remove stale containers from old layout (Mask-based approach).
            DestroyIfExists(avatarFrameGo.transform, "AvatarMask"); // obsolete Mask container + children

            // Migrate legacy single "AvatarRing" child to AvatarRingChrome.
            Transform legacyRing = avatarFrameGo.transform.Find("AvatarRing");
            if (legacyRing != null)
            {
                if (avatarFrameGo.transform.Find("AvatarRingChrome") == null)
                    legacyRing.name = "AvatarRingChrome";
                else
                    DestroyObj(legacyRing.gameObject);
            }

            // Remove stale Image / Shadow from ring children built with the old Image approach.
            foreach (string ringName in new[] { "AvatarRingChrome", "AvatarRingGold" })
            {
                Transform ringT = avatarFrameGo.transform.Find(ringName);
                if (ringT == null) continue;
                var staleImg    = ringT.GetComponent<Image>();
                if (staleImg    != null) DestroyObj(staleImg);
                var staleShadow = ringT.GetComponent<Shadow>();
                if (staleShadow != null) DestroyObj(staleShadow);
            }

            //    Avatar — AvatarCircleImage; clips itself to AvatarFrame's circle bounds in the shader.
            //    Stretch-fills AvatarFrame so AvatarCircleImage derives the mask radius from the parent rect.
            GameObject avatarImgGo = GetOrCreate(avatarFrameGo.transform, "Avatar");
            var        avatarImg   = GetOrAdd<AvatarCircleImage>(avatarImgGo);
            RecordObj(avatarImgGo);
            RecordObj(avatarImg);
            avatarImg.type          = Image.Type.Simple;
            avatarImg.color         = Color.white; // always visible; runtime sprite is set via PlayerView.SetAvatar
            avatarImg.raycastTarget = false;
            avatarImgGo.SetActive(true);
            Stretch(avatarImgGo);

            //    AvatarRingChrome — always-on silver SDF ring beneath the gold overlay.
            GameObject    chromeRingGo  = GetOrCreate(avatarFrameGo.transform, "AvatarRingChrome");
            var           chromeRingGfx = GetOrAdd<AvatarRingSdfGraphic>(chromeRingGo);
            RecordObj(chromeRingGo);
            RecordObj(chromeRingGfx);
            chromeRingGfx.Look          = AvatarRingSdfGraphic.RingLook.Chrome;
            chromeRingGfx.StrokeWidthPx = AvatarRingStrokePx;
            chromeRingGfx.color         = Color.white;
            chromeRingGfx.raycastTarget = false;
            chromeRingGo.SetActive(true);
            SetRect(chromeRingGo, 0f, 0f, AvatarD, AvatarD);

            //    AvatarRingGold — gold countdown overlay; hidden until SetActiveTurn().
            GameObject    goldRingGo  = GetOrCreate(avatarFrameGo.transform, "AvatarRingGold");
            var           goldRingGfx = GetOrAdd<AvatarRingSdfGraphic>(goldRingGo);
            RecordObj(goldRingGo);
            RecordObj(goldRingGfx);
            goldRingGfx.Look          = AvatarRingSdfGraphic.RingLook.Gold;
            goldRingGfx.StrokeWidthPx = AvatarRingStrokePx;
            goldRingGfx.color         = Color.clear;
            goldRingGfx.raycastTarget = false;
            goldRingGo.SetActive(true);
            SetRect(goldRingGo, 0f, 0f, AvatarD, AvatarD);

            // Internal sibling order: Avatar (photo) → Chrome → Gold (top).
            avatarImgGo.transform.SetSiblingIndex(0);
            chromeRingGo.transform.SetSiblingIndex(1);
            goldRingGo.transform.SetSiblingIndex(2);

            // 7. NameText — centered in HudPanel, above chips (rects applied in ApplyHudLayout at end).
            Transform nameT = FindDeep(root, "NameText");
            if (nameT != null)
            {
                ReparentTo(nameT, root);
                var txt = nameT.GetComponent<TMP_Text>();
                if (txt != null)
                {
                    RecordObj(txt);
                    txt.alignment          = PlayerHudLayout.HudPanelTextAlign;
                    txt.fontStyle          = FontStyles.Bold;
                    if (!_useUndo)
                    {
                        txt.enableAutoSizing = true;
                        txt.fontSize         = NameFontSize;
                        txt.fontSizeMin      = 14f;
                        txt.fontSizeMax      = NameFontSize;
                    }
                    txt.color              = NameColor;
                    txt.overflowMode       = TextOverflowModes.Ellipsis;
                    txt.enableWordWrapping = false;
                    txt.raycastTarget      = false;
                }

                string seatName = view.ResolveDisplayName();
                if (!string.IsNullOrWhiteSpace(seatName))
                    view.SetDisplayName(seatName);
                else if (txt != null && !string.IsNullOrWhiteSpace(txt.text))
                    view.SetDisplayName(txt.text);
            }

            // 8. ChipsText — centered in HudPanel, below name.
            Transform chipsT = FindDeep(root, "ChipsText");
            if (chipsT != null)
            {
                ReparentTo(chipsT, root);
                var txt = chipsT.GetComponent<TMP_Text>();
                if (txt != null)
                {
                    RecordObj(txt);
                    txt.alignment          = PlayerHudLayout.HudPanelTextAlign;
                    txt.fontStyle          = FontStyles.Bold;
                    if (!_useUndo) { txt.enableAutoSizing = false; txt.fontSize = 13f; }
                    txt.color              = ChipsColor;
                    txt.overflowMode       = TextOverflowModes.Ellipsis;
                    txt.enableWordWrapping = false;
                    txt.raycastTarget      = false;
                }
            }

            // 9. StatusText — overlays the chips area; empty string renders as invisible.
            Transform statusT = FindDeep(root, "StatusText");
            if (statusT != null)
            {
                ReparentTo(statusT, root);
                RecordObj(statusT.gameObject);
                statusT.gameObject.SetActive(true);
                var txt = statusT.GetComponent<TMP_Text>();
                if (txt != null)
                {
                    RecordObj(txt);
                    txt.alignment          = PlayerHudLayout.HudPanelTextAlign;
                    if (!_useUndo) { txt.enableAutoSizing = false; txt.fontSize = 10f; }
                    txt.color              = StatusColor;
                    txt.overflowMode       = TextOverflowModes.Ellipsis;
                    txt.enableWordWrapping = false;
                    txt.raycastTarget      = false;
                }
            }

            // 9b. ActionBadge — SDF cyan pill above the name row after a player acts.
            GameObject actionBadgeGo  = GetOrCreate(root, "ActionBadge");
            var        actionBadgeComp = GetOrAdd<ActionBadge>(actionBadgeGo);
            RecordObj(actionBadgeGo);
            RecordObj(actionBadgeComp);
            var actionBadgeRootImg = actionBadgeGo.GetComponent<Image>();
            if (actionBadgeRootImg != null) DestroyObj(actionBadgeRootImg);
            DestroyIfExists(actionBadgeGo.transform, "GlowBorder");
            DestroyIfExists(actionBadgeGo.transform, "Background");
            CleanupActionBadgeComponents(actionBadgeGo);
            ActionBadgeUtility.Repair(actionBadgeGo, actionBadgeComp);
            SetRect(actionBadgeGo, textX, PlayerHudLayout.ActionBadgeY, ActionBadgeGlowW, ActionBadgeGlowH);

            var pillGfx = GetOrAdd<ActionBadgeSdfGraphic>(actionBadgeGo);
            RecordObj(pillGfx);
            pillGfx.raycastTarget = false;
            pillGfx.color         = Color.white;
            Shader badgeShader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Shaders/UI/ActionBadgeSDF.shader");
            var pillGfxSo = new SerializedObject(pillGfx);
            pillGfxSo.Update();
            if (badgeShader != null)
                pillGfxSo.FindProperty("_shader").objectReferenceValue = badgeShader;
            pillGfxSo.FindProperty("_pillWidthPx").floatValue    = ActionBadgeW;
            pillGfxSo.FindProperty("_pillHeightPx").floatValue   = ActionBadgeH;
            pillGfxSo.FindProperty("_cornerRadiusPx").floatValue   = 20f;
            pillGfxSo.FindProperty("_borderWidthPx").floatValue    = 2f;
            pillGfxSo.FindProperty("_glowSpreadPx").floatValue     = 8f;
            pillGfxSo.FindProperty("_glowStrength").floatValue     = 1.0f;
            pillGfxSo.FindProperty("_glowFalloff").floatValue      = 1.15f;
            pillGfxSo.FindProperty("_highlightStrength").floatValue = 0f;
            pillGfxSo.FindProperty("_fillColorTop").colorValue   = Color.black;
            pillGfxSo.FindProperty("_fillColorBot").colorValue   = Color.black;
            pillGfxSo.FindProperty("_borderColor").colorValue    = ButtonLabelStyle.RaiseText;
            pillGfxSo.ApplyModifiedProperties();

            GameObject actionLabelGo = GetOrCreate(actionBadgeGo.transform, "Label");
            var        actionLabel   = GetOrAdd<TextMeshProUGUI>(actionLabelGo);
            RecordObj(actionLabelGo);
            RecordObj(actionLabel);
            Stretch(actionLabelGo);
            actionLabel.alignment          = TextAlignmentOptions.Center;
            actionLabel.fontStyle          = FontStyles.Bold;
            actionLabel.fontSize           = 21f;
            actionLabel.color              = ButtonLabelStyle.RaiseText;
            actionLabel.overflowMode       = TextOverflowModes.Overflow;
            actionLabel.enableWordWrapping = false;
            actionLabel.raycastTarget      = false;
            TMP_FontAsset badgeFont = LoadBadgeLabelFont();
            if (badgeFont != null)
            {
                actionLabel.font               = badgeFont;
                actionLabel.fontSharedMaterial = badgeFont.material;
            }

            var badgeSo = new SerializedObject(actionBadgeComp);
            badgeSo.Update();
            SetRef(badgeSo, "_pillGraphic", pillGfx);
            SetRef(badgeSo, "_label",       actionLabel);
            badgeSo.ApplyModifiedProperties();

            actionBadgeGo.SetActive(false);

            TMP_FontAsset casinoFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Fonts/Casino3D SDF.asset");
            if (casinoFont == null)
                casinoFont = Resources.Load<TMP_FontAsset>("Fonts/Casino3D SDF");

            // 9c. SeatActionMenu — human betting choices above the player name.
            GameObject seatMenuGo  = GetOrCreate(root, "SeatActionMenu");
            var        seatMenuComp = GetOrAdd<SeatActionMenu>(seatMenuGo);
            RecordObj(seatMenuGo);
            RecordObj(seatMenuComp);
            var seatMenuRootImg = seatMenuGo.GetComponent<Image>();
            if (seatMenuRootImg != null) DestroyObj(seatMenuRootImg);
            SetRect(seatMenuGo, textX, PlayerHudLayout.SeatActionMenuY, SeatActionMenuW, SeatActionMenuH);

            GameObject seatMenuBgGo  = GetOrCreate(seatMenuGo.transform, "Background");
            var        seatMenuBgImg = GetOrAdd<Image>(seatMenuBgGo);
            RecordObj(seatMenuBgGo);
            RecordObj(seatMenuBgImg);
            seatMenuBgImg.sprite        = roundedRect;
            seatMenuBgImg.type          = Image.Type.Sliced;
            seatMenuBgImg.color         = new Color(BadgeBgColor.r, BadgeBgColor.g, BadgeBgColor.b, 1f);
            seatMenuBgImg.raycastTarget = true;
            Stretch(seatMenuBgGo);
            seatMenuBgGo.transform.SetAsFirstSibling();

            GameObject actionsColGo = GetOrCreate(seatMenuGo.transform, "ActionsColumn");
            RecordObj(actionsColGo);
            var actionsColImg = actionsColGo.GetComponent<Image>();
            if (actionsColImg != null) DestroyObj(actionsColImg);
            Stretch(actionsColGo);
            var actionsLayout = GetOrAdd<VerticalLayoutGroup>(actionsColGo);
            RecordObj(actionsLayout);
            actionsLayout.spacing               = 1f;
            actionsLayout.padding               = new RectOffset(6, 6, 5, 5);
            actionsLayout.childAlignment        = TextAnchor.UpperCenter;
            actionsLayout.childForceExpandWidth  = true;
            actionsLayout.childForceExpandHeight = false;
            actionsLayout.childControlWidth      = true;
            actionsLayout.childControlHeight     = true;

            Button foldBtn      = CreateSeatMenuButton(actionsColGo.transform, "FoldButton",      "FOLD",  new Color(1f, 0f, 0f, 1f),      casinoFont, SeatActionRowH);
            Button checkCallBtn = CreateSeatMenuButton(actionsColGo.transform, "CheckCallButton", "CHECK", new Color(0f, 1f, 0f, 1f),      casinoFont, SeatActionRowH);
            Button raiseBtn     = CreateSeatMenuButton(actionsColGo.transform, "RaiseButton",     "RAISE", ButtonLabelStyle.RaiseText,     casinoFont, SeatActionRowH);

            GameObject raiseInputRowGo = GetOrCreate(actionsColGo.transform, "RaiseInputRow");
            RecordObj(raiseInputRowGo);
            var raiseInputRowImg = raiseInputRowGo.GetComponent<Image>();
            if (raiseInputRowImg != null) DestroyObj(raiseInputRowImg);
            SetLayoutRow(raiseInputRowGo, SeatActionInputH);
            var raiseInputRowLayout = GetOrAdd<LayoutElement>(raiseInputRowGo);
            RecordObj(raiseInputRowLayout);
            raiseInputRowLayout.preferredHeight = SeatActionInputH;
            raiseInputRowLayout.minHeight       = SeatActionInputH;

            GameObject raiseInputGo  = GetOrCreate(raiseInputRowGo.transform, "RaiseInput");
            var        raiseInputImg = GetOrAdd<Image>(raiseInputGo);
            var        raiseInput    = GetOrAdd<TMP_InputField>(raiseInputGo);
            RecordObj(raiseInputGo);
            RecordObj(raiseInputImg);
            RecordObj(raiseInput);
            raiseInputImg.color         = new Color(0.08f, 0.08f, 0.10f, 0.95f);
            raiseInputImg.raycastTarget = true;
            Stretch(raiseInputGo);
            raiseInput.contentType          = TMP_InputField.ContentType.IntegerNumber;
            raiseInput.lineType             = TMP_InputField.LineType.SingleLine;
            raiseInput.characterValidation  = TMP_InputField.CharacterValidation.Integer;
            raiseInput.targetGraphic        = raiseInputImg;

            GameObject raiseTextAreaGo = GetOrCreate(raiseInputGo.transform, "Text Area");
            RecordObj(raiseTextAreaGo);
            Stretch(raiseTextAreaGo);
            GetOrAdd<RectMask2D>(raiseTextAreaGo);

            GameObject raiseTextGo = GetOrCreate(raiseTextAreaGo.transform, "Text");
            var        raiseText   = GetOrAdd<TextMeshProUGUI>(raiseTextGo);
            RecordObj(raiseTextGo);
            RecordObj(raiseText);
            Stretch(raiseTextGo);
            raiseText.fontSize           = 12f;
            raiseText.color              = new Color(1f, 1f, 0f, 1f);
            raiseText.alignment          = TextAlignmentOptions.Midline;
            raiseText.enableWordWrapping = false;
            if (casinoFont != null) raiseText.font = casinoFont;

            GameObject raisePlaceholderGo = GetOrCreate(raiseTextAreaGo.transform, "Placeholder");
            var        raisePlaceholder   = GetOrAdd<TextMeshProUGUI>(raisePlaceholderGo);
            RecordObj(raisePlaceholderGo);
            RecordObj(raisePlaceholder);
            Stretch(raisePlaceholderGo);
            raisePlaceholder.text      = "40";
            raisePlaceholder.fontSize  = 12f;
            raisePlaceholder.color     = new Color(1f, 1f, 1f, 0.35f);
            raisePlaceholder.fontStyle = FontStyles.Italic;
            raisePlaceholder.alignment = TextAlignmentOptions.Midline;
            if (casinoFont != null) raisePlaceholder.font = casinoFont;

            raiseInput.textViewport  = (RectTransform)raiseTextAreaGo.transform;
            raiseInput.textComponent = raiseText;
            raiseInput.placeholder   = raisePlaceholder;

            raiseInputRowGo.SetActive(false);

            Button allInBtn = CreateSeatMenuButton(actionsColGo.transform, "AllInButton", "ALL IN",
                new Color(1f, 0f, 1f, 1f), casinoFont, SeatActionRowH);
            raiseInputRowGo.transform.SetSiblingIndex(raiseBtn.transform.GetSiblingIndex() + 1);

            var seatMenuSo = new SerializedObject(seatMenuComp);
            seatMenuSo.Update();
            SetRef(seatMenuSo, "_background",      seatMenuBgImg);
            SetRef(seatMenuSo, "_foldButton",      foldBtn);
            SetRef(seatMenuSo, "_checkCallButton", checkCallBtn);
            SetRef(seatMenuSo, "_raiseButton",     raiseBtn);
            SetRef(seatMenuSo, "_allInButton",     allInBtn);
            SetRef(seatMenuSo, "_foldLabel",       foldBtn.GetComponentInChildren<TMP_Text>());
            SetRef(seatMenuSo, "_checkCallLabel",  checkCallBtn.GetComponentInChildren<TMP_Text>());
            SetRef(seatMenuSo, "_raiseLabel",      raiseBtn.GetComponentInChildren<TMP_Text>());
            SetRef(seatMenuSo, "_allInLabel",      allInBtn.GetComponentInChildren<TMP_Text>());
            SetRef(seatMenuSo, "_raiseInput",      raiseInput);
            SetRef(seatMenuSo, "_raiseInputRow",   raiseInputRowGo);
            seatMenuSo.ApplyModifiedProperties();

            seatMenuGo.SetActive(false);

            // 10. BetDisplay — chip stack beside avatar + amount badge (position from TableLayoutManager).
            //     Destroy legacy BetBadge GO from previous layout versions if still present.
            Transform oldBadge = root.Find("BetBadge");
            if (oldBadge != null) DestroyObj(oldBadge.gameObject);

            GameObject betDisplayGo   = GetOrCreate(root, "BetDisplay");
            var        betDisplayComp = GetOrAdd<BetDisplay>(betDisplayGo);
            RecordObj(betDisplayGo);
            RecordObj(betDisplayComp);
            // BetDisplay root has no background — it's a pure positional anchor.
            var betDisplayImg = betDisplayGo.GetComponent<Image>();
            if (betDisplayImg != null) DestroyObj(betDisplayImg);
            SetRect(betDisplayGo, BadgeX, BadgeY, 146f, 52f);

            // Drop shadow on the root so both children inherit it visually.
            var betShadow = GetOrAdd<Shadow>(betDisplayGo);
            RecordObj(betShadow);
            betShadow.effectColor    = new Color(0f, 0f, 0f, 0.65f);
            betShadow.effectDistance = new Vector2(2f, -3f);

            // ── ChipStack: Chip_0 / Chip_1 / Chip_2 — fixed stack layers (layout unchanged) ─
            const float ChipD  = 38f;
            const float ChipDx =  6f;
            const float ChipDy =  5f;

            Sprite chip1Sprite  = AssetDatabase.LoadAssetAtPath<Sprite>(Chip1Path);
            Sprite chip5Sprite  = AssetDatabase.LoadAssetAtPath<Sprite>(Chip5Path);
            Sprite chip25Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Chip25Path);

            GameObject chipStackGo = GetOrCreate(betDisplayGo.transform, "ChipStack");
            RecordObj(chipStackGo);
            var chipStackBg = chipStackGo.GetComponent<Image>();
            if (chipStackBg != null) DestroyObj(chipStackBg);
            SetRect(chipStackGo, -48f, 0f, ChipD + ChipDx * 2f, ChipD + ChipDy * 2f);

            string[] chipNames   = { "Chip_0", "Chip_1", "Chip_2" };
            Sprite[] slotSprites = { chip1Sprite, chip5Sprite, chip25Sprite };
            Image[]  slotImages  = new Image[3];

            for (int ci = 0; ci < chipNames.Length; ci++)
            {
                float backness = chipNames.Length - 1 - ci;
                float ox = ChipDx * backness;
                float oy = ChipDy * backness;

                GameObject chipGo  = GetOrCreate(chipStackGo.transform, chipNames[ci]);
                var        chipImg = GetOrAdd<Image>(chipGo);
                RecordObj(chipGo);
                RecordObj(chipImg);
                chipImg.sprite         = slotSprites[ci];
                chipImg.type           = Image.Type.Simple;
                chipImg.color          = Color.white;
                chipImg.preserveAspect = true;
                chipImg.raycastTarget  = false;
                SetRect(chipGo, ox, oy, ChipD, ChipD);
                chipGo.transform.SetSiblingIndex(ci);
                slotImages[ci] = chipImg;
            }

            var chipStackView = GetOrAdd<ChipStackView>(chipStackGo);
            RecordObj(chipStackView);
            WireChipStackView(chipStackView, slotImages, chip1Sprite, chip5Sprite, chip25Sprite);
            chipStackView.Clear();

            // ── AmountBadge: dark rounded pill with the euro amount ───────────────
            GameObject badgeGo  = GetOrCreate(betDisplayGo.transform, "AmountBadge");
            var        badgeImg = GetOrAdd<Image>(badgeGo);
            RecordObj(badgeGo);
            RecordObj(badgeImg);
            badgeImg.sprite        = roundedRect;
            badgeImg.type          = Image.Type.Sliced;
            badgeImg.color         = new Color(0.06f, 0.06f, 0.08f, 0.93f);
            badgeImg.raycastTarget = false;
            SetRect(badgeGo, 28f, 0f, 90f, 30f);

            // AmountText inside the badge — bold gold euro label.
            GameObject amountGo  = GetOrCreate(badgeGo.transform, "AmountText");
            var        amountTMP = GetOrAdd<TextMeshProUGUI>(amountGo);
            RecordObj(amountGo);
            RecordObj(amountTMP);
            amountTMP.text               = "";
            amountTMP.alignment          = TextAlignmentOptions.Center;
            amountTMP.fontStyle          = FontStyles.Bold;
            amountTMP.fontSize           = 14f;
            amountTMP.color              = new Color(1.00f, 0.85f, 0.15f, 1f);
            amountTMP.enableWordWrapping = false;
            amountTMP.raycastTarget      = false;
            amountTMP.overflowMode       = TextOverflowModes.Ellipsis;
            Stretch(amountGo);

            // Chip stack behind the amount badge in the root's child order.
            chipStackGo.transform.SetSiblingIndex(0);
            badgeGo.transform.SetSiblingIndex(1);

            // Wire BetDisplay's own serialized fields.
            var betDispSo = new SerializedObject(betDisplayComp);
            betDispSo.Update();
            SetRef(betDispSo, "_amountText",    amountTMP);
            SetRef(betDispSo, "_chipStackView", chipStackView);
            SetRef(betDispSo, "_chipStackRoot", (RectTransform)chipStackGo.transform);
            betDispSo.ApplyModifiedProperties();

            betDisplayGo.SetActive(false);

            // 11. Hide any remaining legacy elements not covered by cleanup.
            HideIfExists(root, "AvatarIcon");   // very old name from pre-AvatarFrame runs
            HideIfExists(root, "BetText");      // stale BetText that escaped BetBadge destruction
            HideIfExists(root, "ChipIcon");     // old single-icon approach replaced by ChipStack
            DestroyIfExists(avatarFrameGo.transform, "RingCrome"); // typo duplicate of AvatarRingChrome

            // 12. Sibling render order (back → front):
            //     Card slots → HudPanel → HudGlow → AvatarFrame (Avatar → Chrome → Gold)
            //     → NameText → ChipsText → StatusText → SeatActionMenu → ActionBadge → BetDisplay
            int idx = 0;
            foreach (CardView card in cards)
            {
                if (card == null) continue;
                card.transform.SetSiblingIndex(idx++);
            }
            hudGo.transform.SetSiblingIndex(idx++);
            hudGlowGo.transform.SetSiblingIndex(idx++);
            avatarFrameGo.transform.SetSiblingIndex(idx++);
            if (nameT   != null) nameT.SetSiblingIndex(idx++);
            if (chipsT  != null) chipsT.SetSiblingIndex(idx++);
            if (statusT != null) statusT.SetSiblingIndex(idx++);
            seatMenuGo.transform.SetSiblingIndex(idx++);
            actionBadgeGo.transform.SetSiblingIndex(idx++);
            betDisplayGo.transform.SetSiblingIndex(idx);

            // 13. Wire serialized fields on PlayerView.
            var so = new SerializedObject(view);
            so.Update();
            SetRef(so, "_nameText",    nameT   != null ? nameT.GetComponent<TMP_Text>()   : null);
            SetRef(so, "_chipsText",   chipsT  != null ? chipsT.GetComponent<TMP_Text>()  : null);
            SetRef(so, "_statusText",  statusT != null ? statusT.GetComponent<TMP_Text>() : null);
            ClearRef(so, "_activeGlow");
            ClearRef(so, "_timerBar");
            SetRef(so, "_betDisplay",  betDisplayComp);
            SetRef(so, "_actionBadge",  actionBadgeComp);
            SetRef(so, "_seatActionMenu", seatMenuComp);
            SetRef(so, "_avatarFrame", (RectTransform)avatarFrameGo.transform);
            SetRef(so, "_avatarImage",     avatarImg);
            SetRef(so, "_avatarRingChrome", chromeRingGfx);
            SetRef(so, "_avatarRingGold",   goldRingGfx);
            SetRef(so, "_hudGlow",          hudGlowGfx);
            // Clear fields removed from the new PlayerView layout.
            ClearRef(so, "_avatarFitter");
            ClearRef(so, "_avatarRingSdf");
            ClearRef(so, "_avatarMask");
            ClearRef(so, "_avatarRing");
            ClearRef(so, "_ringInnerRatio");
            // Clear other legacy fields.
            ClearRef(so, "_betBadge");
            ClearRef(so, "_betText");
            ClearRef(so, "_chipIcon");         // replaced by _chipStackRoot on BetDisplay
            ClearRef(so, "_avatarInitialText");
            ClearRef(so, "_activeTurnIndicator");
            ClearRef(so, "_chipsBar");
            // Reset testing toggles so layout always starts from a fully-visible state.
            // The user can flip them off in the Inspector afterwards if needed.
            var showRingProp   = so.FindProperty("_showChromeRing");
            var showAvatarProp = so.FindProperty("_showAvatarImages");
            if (showRingProp   != null) showRingProp.boolValue   = true;
            if (showAvatarProp != null) showAvatarProp.boolValue = true;
            var mirroredProp = so.FindProperty("_hudMirrored");
            if (mirroredProp != null) mirroredProp.boolValue = mirrored;
            so.ApplyModifiedProperties();

            view.ApplyHudLayout();

            if (_useUndo) EditorUtility.SetDirty(view);
        }

        // ── Helper: find BetText and move it into the badge, or create it there ─

        private static Transform FindOrReparentText(Transform root, Transform badgeParent, string name)
        {
            // Check direct root children first (legacy location in the prefab).
            for (int i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i).name == name)
                {
                    Transform t = root.GetChild(i);
                    if (t.parent != badgeParent) ReparentTo(t, badgeParent);
                    return t;
                }
            }

            // Deep search handles the already-inside-badge case from previous runs.
            Transform found = FindDeep(root, name);
            if (found != null)
            {
                if (found.parent != badgeParent) ReparentTo(found, badgeParent);
                return found;
            }

            // Create a brand-new child inside the badge.
            GameObject go = GetOrCreate(badgeParent, name);
            GetOrAdd<TMP_Text>(go);
            return go.transform;
        }

        // ── Sprite generation ─────────────────────────────────────────────────

        /// <summary>Generates a smooth anti-aliased white circle PNG and imports it as a Sprite.</summary>
        private static Sprite EnsureCircleSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
            if (existing != null) return existing;

            const int   size = 128;
            const float half = (size - 1) / 2f;
            const float r    = half - 1f;

            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r + 1f - d));
            }
            tex.SetPixels(pixels);
            tex.Apply();
            SavePng(tex, CirclePath);

            var imp = (TextureImporter)AssetImporter.GetAtPath(CirclePath);
            imp.textureType      = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode       = FilterMode.Bilinear;
            imp.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
        }

        /// <summary>Generates a 9-sliced white rounded-rectangle PNG and imports it as a Sprite.</summary>
        private static Sprite EnsureRoundedRectSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedRectPath);
            if (existing != null) return existing;

            const int   size   = 64;
            const float radius = 14f;

            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x, radius, size - 1 - radius);
                float cy = Mathf.Clamp(y, radius, size - 1 - radius);
                float d  = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius + 1f - d));
            }
            tex.SetPixels(pixels);
            tex.Apply();
            SavePng(tex, RoundedRectPath);

            var imp = (TextureImporter)AssetImporter.GetAtPath(RoundedRectPath);
            imp.textureType      = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode       = FilterMode.Bilinear;
            imp.spriteBorder     = new Vector4(radius, radius, radius, radius);
            imp.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedRectPath);
        }

        private static void WireChipStackView(
            ChipStackView view, Image[] slots, Sprite sprite1, Sprite sprite5, Sprite sprite25)
        {
            var so = new SerializedObject(view);
            so.Update();
            if (slots.Length > 0) SetRef(so, "_chip0", slots[0]);
            if (slots.Length > 1) SetRef(so, "_chip1", slots[1]);
            if (slots.Length > 2) SetRef(so, "_chip2", slots[2]);
            SetRef(so, "_sprite1",  sprite1);
            SetRef(so, "_sprite5",  sprite5);
            SetRef(so, "_sprite25", sprite25);
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Generates a 256×256 polished chrome bezel PNG — luxury watch style.
        /// Always regenerates so design tweaks take effect immediately.
        ///
        /// Rendering model:
        ///   1. Angular diffuse  — chrome-tube effect, light from upper-left (120°).
        ///   2. Bevel profile    — dark groove at ring midpoint with chamfered edges.
        ///   3. Primary specular — tight white flash at outer chamfer, upper-left.
        ///   4. Secondary spec   — softer bounce light at inner chamfer, lower-right.
        ///   5. Reflection streaks — 4 thin mirror flashes distributed in the lit zone.
        ///
        /// Geometry: AvatarD = 108, AvatarImgD = 80  →  14 display-px ring per side.
        /// Texture scale: 256 / 108 ≈ 2.37 tex-px / display-px  →  ~33 tex-px ring.
        /// </summary>
        private static Sprite EnsureChromeRingSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(ChromeRingPath) != null)
                AssetDatabase.DeleteAsset(ChromeRingPath);

            // ── Geometry ────────────────────────────────────────────────────────
            const int   size   = 256;
            const float center = (size - 1) * 0.5f;        // 127.5
            const float outerR = center - 2f;               // 125.5  (1-px feather)
            const float innerR = 40f * (256f / 108f);       //  94.8  matches AvatarImgD/2 in tex space
            const float ringW  = outerR - innerR;            // ≈ 30.7 tex-px ≈ 13 display-px

            // Primary light: upper-left at 120° (2π/3); bounce: lower-right at 300° (−π/3)
            const float kLightAngle  =  2.094f;
            const float kBounceAngle = -1.047f;

            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx   = x - center;
                float dy   = y - center;   // dy > 0 = top of screen (tex Y=0 is bottom)
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float outerA = Mathf.Clamp01(outerR + 1f - dist);
                float innerA = Mathf.Clamp01(dist - innerR);
                float alpha  = outerA * innerA;
                if (alpha <= 0f) { pixels[y * size + x] = Color.clear; continue; }

                float angle = Mathf.Atan2(dy, dx);        // −π..+π; 0=right, π/2=top
                float rPos  = (dist - innerR) / ringW;    // 0 = inner edge, 1 = outer edge

                // ── 1. Angular diffuse: chrome-tube lit from upper-left ──────────
                float da   = WrapAngle(angle - kLightAngle);
                float angD = Mathf.Cos(da) * 0.5f + 0.5f;  // 0 (shadow) → 1 (lit)

                // ── 2. Bevel cross-section profile ───────────────────────────────
                float bv = EvalBevelProfile(rPos);

                // ── 3. Base metal value: +30 % brighter diffuse ramp ────────────
                // Ambient floor 0.10 stays; diffuse coefficient 0.82 → 1.06 (+29.3 %)
                float baseV = Mathf.Clamp01((0.10f + angD * 1.06f) * bv);
                float r = baseV, g = baseV, b = baseV;

                // ── 4a. Upper-left broad arc — bright silver sweep on outer face ─
                // Wide Gaussian (σ≈29°) centred at 120°, sweeps rPos 0.60–0.95.
                // Sits below the tight specular flash so the flash still reads as a
                // distinct point; this layer gives the broad "silver gleam" zone.
                float arcUL  = Mathf.Exp(-(da * da) / (2f * 0.50f * 0.50f));
                float arcULr = Mathf.Clamp01((rPos - 0.60f) * 4.0f) *
                               Mathf.Clamp01((0.95f - rPos) * 4.0f);
                float sheenUL = arcUL * arcULr * 0.55f;
                r = Mathf.Lerp(r, 0.96f, sheenUL);
                g = Mathf.Lerp(g, 0.96f, sheenUL);
                b = Mathf.Lerp(b, 0.96f, sheenUL);

                // ── 4b. Primary specular: tight white flash, upper-left outer chamfer
                float angS1  = Mathf.Exp(-(da * da) / (2f * 0.09f * 0.09f));
                float dRO    = rPos - 0.88f;
                float radS1  = Mathf.Exp(-(dRO * dRO) / (2f * 0.07f * 0.07f));
                float spec1  = angS1 * radS1;
                r = Mathf.Lerp(r, 1.00f, spec1 * 0.98f);
                g = Mathf.Lerp(g, 1.00f, spec1 * 0.98f);
                b = Mathf.Lerp(b, 1.00f, spec1 * 0.98f);

                // Same light also hits the inner chamfer at the same angle
                float dRI    = rPos - 0.10f;
                float radS1i = Mathf.Exp(-(dRI * dRI) / (2f * 0.07f * 0.07f));
                float spec1i = angS1 * radS1i * 0.65f;
                r = Mathf.Lerp(r, 0.95f, spec1i);
                g = Mathf.Lerp(g, 0.95f, spec1i);
                b = Mathf.Lerp(b, 0.95f, spec1i);

                // ── 5a. Lower-right arc — smaller silver highlight on inner face ──
                // Moderate Gaussian (σ≈20°) centred at 300°, covers rPos 0.05–0.42.
                // Deliberately narrower and dimmer than the upper-left arc.
                float da2     = WrapAngle(angle - kBounceAngle);
                float arcLR   = Mathf.Exp(-(da2 * da2) / (2f * 0.35f * 0.35f));
                float arcLRr  = Mathf.Clamp01((rPos - 0.05f) * 5.5f) *
                                Mathf.Clamp01((0.42f - rPos) * 5.5f);
                float sheenLR = arcLR * arcLRr * 0.38f;
                r = Mathf.Lerp(r, 0.78f, sheenLR);
                g = Mathf.Lerp(g, 0.78f, sheenLR);
                b = Mathf.Lerp(b, 0.78f, sheenLR);

                // ── 5b. Secondary specular: tight bounce, lower-right inner chamfer
                float angS2 = Mathf.Exp(-(da2 * da2) / (2f * 0.20f * 0.20f));
                float dR2   = rPos - 0.15f;
                float radS2 = Mathf.Exp(-(dR2 * dR2) / (2f * 0.10f * 0.10f));
                float spec2 = angS2 * radS2;
                r = Mathf.Lerp(r, 0.80f, spec2 * 0.72f);
                g = Mathf.Lerp(g, 0.80f, spec2 * 0.72f);
                b = Mathf.Lerp(b, 0.80f, spec2 * 0.72f);

                // ── 6. Reflection streaks: 4 thin mirror flashes in lit zone ──────
                // Angles (radians): 17°, 52°, 83°, 112° — upper-right / upper-left arc
                const float kSig2 = 2f * 0.045f * 0.045f;
                float ds1 = WrapAngle(angle - 0.30f);
                float ds2 = WrapAngle(angle - 0.90f);
                float ds3 = WrapAngle(angle - 1.45f);
                float ds4 = WrapAngle(angle - 1.95f);
                float sMax = Mathf.Max(
                    Mathf.Max(Mathf.Exp(-ds1 * ds1 / kSig2), Mathf.Exp(-ds2 * ds2 / kSig2)),
                    Mathf.Max(Mathf.Exp(-ds3 * ds3 / kSig2), Mathf.Exp(-ds4 * ds4 / kSig2)));
                // Only on outer face zone (rPos 0.64-0.86), only in lit half, not under spec1
                float outerFace = Mathf.Clamp01((rPos  - 0.64f) * 12.5f) *
                                  Mathf.Clamp01((0.86f - rPos)  * 12.5f);
                float streakMix = sMax * outerFace
                                * Mathf.Clamp01((angD - 0.30f) * 5f)
                                * (1f - Mathf.Clamp01(spec1 * 3f))
                                * 0.30f;
                r = Mathf.Lerp(r, 0.88f, streakMix);
                g = Mathf.Lerp(g, 0.88f, streakMix);
                b = Mathf.Lerp(b, 0.88f, streakMix);

                pixels[y * size + x] = new Color(r, g, b, alpha);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SavePng(tex, ChromeRingPath);

            var imp = (TextureImporter)AssetImporter.GetAtPath(ChromeRingPath);
            imp.textureType      = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode       = FilterMode.Bilinear;
            imp.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(ChromeRingPath);
        }

        /// <summary>
        /// Wraps a radian angle delta into [−π, +π].
        /// </summary>
        private static float WrapAngle(float da)
        {
            while (da >  Mathf.PI) da -= 2f * Mathf.PI;
            while (da < -Mathf.PI) da += 2f * Mathf.PI;
            return da;
        }

        /// <summary>
        /// Piecewise-linear bevel cross-section profile. rPos = 0 (inner) → 1 (outer).
        /// Creates: bright inner chamfer → face → dark groove → face → bright outer chamfer.
        /// This factor multiplies the angular diffuse value to produce the final base brightness.
        ///
        ///   rPos  0.00  0.10  0.22  0.38  0.50  0.62  0.75  0.88  1.00
        ///   val   0.80  1.00  0.70  0.28  0.20  0.30  0.70  1.00  0.65
        /// </summary>
        private static float EvalBevelProfile(float rPos)
        {
            float[] ts = { 0.00f, 0.10f, 0.22f, 0.38f, 0.50f, 0.62f, 0.75f, 0.88f, 1.00f };
            float[] vs = { 0.80f, 1.00f, 0.70f, 0.28f, 0.20f, 0.30f, 0.70f, 1.00f, 0.65f };

            if (rPos <= ts[0]) return vs[0];
            if (rPos >= ts[ts.Length - 1]) return vs[vs.Length - 1];
            for (int i = 0; i < ts.Length - 1; i++)
                if (rPos < ts[i + 1])
                {
                    float seg = (rPos - ts[i]) / (ts[i + 1] - ts[i]);
                    return Mathf.Lerp(vs[i], vs[i + 1], seg);
                }
            return 0.5f;
        }

        private static void SavePng(Texture2D tex, string assetPath)
        {
            string fullPath = System.IO.Path.Combine(
                Application.dataPath, assetPath.Substring("Assets/".Length));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
            System.IO.File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            AssetDatabase.Refresh();
        }

        // ── RectTransform helpers ─────────────────────────────────────────────

        private const string BadgeFontFallbackPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";
        private const string BadgeFontPrimaryPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        /// <summary>Scene edits sometimes stack extra CanvasRenderers on ActionBadge — that breaks UI drawing.</summary>
        private static void CleanupActionBadgeComponents(GameObject actionBadgeGo)
        {
            ActionBadgeUtility.CleanupDuplicateComponents(actionBadgeGo);
        }

        private static TMP_FontAsset LoadBadgeLabelFont()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BadgeFontFallbackPath);
            if (font != null)
                return font;

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BadgeFontPrimaryPath);
            if (font != null)
                return font;

            return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback")
                ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        private static void MigrateHoleCardsFromLegacyPanel(Transform root, List<CardView> cards)
        {
            Transform legacyPanel = root.Find("CardsBehindPanel");
            if (legacyPanel == null)
                return;

            foreach (CardView card in cards)
            {
                if (card != null && card.transform.IsChildOf(legacyPanel))
                    ReparentTo(card.transform, root);
            }

            Transform legacyDimmer = legacyPanel.Find("CardDimmer");
            if (legacyDimmer != null)
                DestroyObj(legacyDimmer.gameObject);

            DestroyObj(legacyPanel.gameObject);
        }

        private static Button CreateSeatMenuButton(Transform parent, string name, string labelText,
            Color labelColor, TMP_FontAsset font, float rowHeight)
        {
            GameObject btnGo  = GetOrCreate(parent, name);
            var        btnImg = GetOrAdd<Image>(btnGo);
            var        btn    = GetOrAdd<Button>(btnGo);
            RecordObj(btnGo);
            RecordObj(btnImg);
            RecordObj(btn);

            btnImg.color         = Color.clear;
            btnImg.raycastTarget = true;
            btn.transition       = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor      = Color.clear;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
            colors.pressedColor     = new Color(1f, 1f, 1f, 0.22f);
            colors.fadeDuration     = 0.06f;
            btn.colors = colors;

            var rowElement = GetOrAdd<LayoutElement>(btnGo);
            RecordObj(rowElement);
            rowElement.preferredHeight = rowHeight;
            rowElement.minHeight       = rowHeight;

            var btnRt = (RectTransform)btnGo.transform;
            btnRt.anchorMin        = new Vector2(0f, 0.5f);
            btnRt.anchorMax        = new Vector2(1f, 0.5f);
            btnRt.pivot            = new Vector2(0.5f, 0.5f);
            btnRt.sizeDelta        = new Vector2(0f, rowHeight);
            btnRt.anchoredPosition = Vector2.zero;

            GameObject labelGo = GetOrCreate(btnGo.transform, "Label");
            var        label   = GetOrAdd<TextMeshProUGUI>(labelGo);
            RecordObj(labelGo);
            RecordObj(label);
            Stretch(labelGo);
            label.text               = labelText;
            label.alignment          = TextAlignmentOptions.Center;
            label.fontStyle          = FontStyles.Bold;
            label.fontSize           = 14f;
            label.color              = labelColor;
            label.overflowMode       = TextOverflowModes.Overflow;
            label.enableWordWrapping = false;
            label.raycastTarget      = false;
            if (font != null) label.font = font;

            return btn;
        }

        private static void SetLayoutRow(GameObject go, float height)
        {
            RecordObj(go.transform);
            var rt = (RectTransform)go.transform;
            rt.anchorMin        = new Vector2(0f, 0.5f);
            rt.anchorMax        = new Vector2(1f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
        }

        private static void SetRect(GameObject go, float x, float y, float w, float h)
        {
            RecordObj(go.transform);
            var rt              = (RectTransform)go.transform;
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta        = new Vector2(w, h);
        }

        private static void Stretch(GameObject go)
        {
            RecordObj(go.transform);
            var rt       = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ── Scene / hierarchy helpers ─────────────────────────────────────────

        private static void RenameIfExists(Transform parent, string oldName, string newName)
        {
            Transform t = parent.Find(oldName);
            if (t == null) return;
            RecordObj(t.gameObject);
            t.gameObject.name = newName;
        }

        private static void ReparentTo(Transform child, Transform newParent)
        {
            if (child.parent == newParent) return;
            if (_useUndo) Undo.SetTransformParent(child, newParent, "Player Seat Layout");
            else          child.SetParent(newParent, false);
        }

        private static void HideIfExists(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t == null) return;
            RecordObj(t.gameObject);
            t.gameObject.SetActive(false);
        }

        private static void CleanupHudPanelComponents(GameObject hudGo)
        {
            if (hudGo == null) return;

#if UNITY_EDITOR
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(hudGo);
#endif
            var staleCam = hudGo.GetComponent<Camera>();
            if (staleCam != null)
                DestroyObj(staleCam);
        }

        private static void DestroyIfExists(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t == null) return;
            DestroyObj(t.gameObject);
        }

        private static GameObject GetOrCreate(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            if (_useUndo) Undo.RegisterCreatedObjectUndo(go, "Player Seat Layout");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null) return c;
            return _useUndo ? Undo.AddComponent<T>(go) : go.AddComponent<T>();
        }

        private static void RecordObj(Object obj)
        {
            if (obj == null) return;
            if (_useUndo) Undo.RecordObject(obj, "Player Seat Layout");
            EditorUtility.SetDirty(obj);
        }

        private static void DestroyObj(Object obj)
        {
            if (_useUndo) Undo.DestroyObjectImmediate(obj);
            else          Object.DestroyImmediate(obj);
        }

        private static void SetRef(SerializedObject so, string field, Object obj)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = obj;
        }

        private static void ClearRef(SerializedObject so, string field)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = null;
        }

        private static List<CardView> ReadCardSlots(PlayerView view)
        {
            var so   = new SerializedObject(view);
            var prop = so.FindProperty("_cardSlots");
            var list = new List<CardView>();
            if (prop == null) return list;
            for (int i = 0; i < prop.arraySize; i++)
            {
                var cv = prop.GetArrayElementAtIndex(i).objectReferenceValue as CardView;
                if (cv != null) list.Add(cv);
            }
            return list;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name) return child;
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// GPU-blits the ring sprite to a readable copy and scans from the horizontal centre
        /// outward to locate the first opaque pixel (inner ring edge).
        /// Returns <c>innerHoleDiameter / textureWidth</c> — a scale-independent ratio — or 0 on failure.
        /// No asset reimport is performed; the original import settings are untouched.
        /// </summary>
        private static float ComputeRingInnerRatio(string spritePath, float ringRenderSize)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[PlayerSeatLayout] Ring sprite not found at '{spritePath}'.");
                return 0f;
            }

            Texture2D src = sprite.texture;
            int w = src.width, h = src.height;

            // Blit to an ARGB RenderTexture so we can ReadPixels without touching import settings.
            RenderTexture rt   = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            int cx = w / 2, cy = h / 2;
            const float kAlpha = 0.05f;

            // Scan rightward from centre; first opaque pixel is the inner ring edge.
            for (int x = cx; x < w; x++)
            {
                if (copy.GetPixel(x, cy).a >= kAlpha)
                {
                    Object.DestroyImmediate(copy);
                    // ratio = innerHoleDiameter / textureWidth (scale-independent)
                    return 2f * (x - cx) / (float)w;
                }
            }

            Object.DestroyImmediate(copy);
            Debug.LogWarning("[PlayerSeatLayout] Could not find inner ring edge — is the sprite fully transparent?");
            return 0f;
        }
    }

}
