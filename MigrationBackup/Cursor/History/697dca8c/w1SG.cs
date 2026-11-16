using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>
    /// Inspector-configurable layout data for a single player seat on the oval table.
    /// Offset fields (card0LocalPos, card1LocalPos, betLabelLocalPos,
    /// dealerButtonLocalPos) are in the seat panel's local space with anchor at center.
    /// </summary>
    [Serializable]
    public struct SeatConfig
    {
        [Tooltip("Panel pivot: (0.5,1)=top-edge away from table, (0.5,0)=bottom-edge, " +
                 "(0,0.5)=left-edge, (1,0.5)=right-edge.")]
        public Vector2 pivot;

        [Tooltip("Width and height of the seat panel in canvas pixels.")]
        public Vector2 size;

        [Space]
        [Tooltip("AnchoredPosition of hole-card slot 0 inside the seat root " +
                 "(anchor at panel centre). Y is relative to the card-area centre (+55 px).")]
        public Vector2 card0LocalPos;

        [Tooltip("AnchoredPosition of hole-card slot 1 inside the seat root " +
                 "(anchor at panel centre). Y is relative to the card-area centre (+55 px).")]
        public Vector2 card1LocalPos;

        [Tooltip("BetAnchor position in seat local space — chip stack is placed here (between avatar and table centre).")]
        public Vector2 betLabelLocalPos;

        [Tooltip("AnchoredPosition of the HudPanel inside the seat root (anchor at panel center).")]
        public Vector2 hudLocalPx;

        [Tooltip("DealerButtonAnchor position in seat local space — dealer token is placed here.")]
        public Vector2 dealerButtonLocalPos;

        [Tooltip("When true, avatar sits on the right and name/chips on the left (left table seats).")]
        public bool mirrorHud;

        [Tooltip("Display name shown on the seat HUD and used for game messages.")]
        public string playerName;
    }

    /// <summary>
    /// Positions all player seats, hole-card slots, pot label, and dealer button on the
    /// table canvas. Exactly 6 seats are supported, arranged around an oval. All positions
    /// are configurable in the Inspector. Community-card slots are static named children of
    /// CommunityCardsContainer — their positions are set once in the Editor and never
    /// touched at runtime.
    /// Press the Apply Layout button in the custom editor to preview changes immediately.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [ExecuteAlways]
    public class TableLayoutManager : MonoBehaviour
    {
        /// <summary>Fixed number of seats at the table.</summary>
        public const int SeatCount = 6;

        // ── Inspector References ──────────────────────────────────────────

        [Header("Canvas")]
        [Tooltip("Root RectTransform of the Screen Space Canvas — required for gizmo drawing.")]
        [SerializeField] private RectTransform _canvasRect;

        [Header("Seats  (index 0 = human player)")]
        [SerializeField] private PlayerView[] _playerViews = new PlayerView[SeatCount];

        [Header("Seat Layout  —  counter-clockwise from bottom-center")]
        [SerializeField] private SeatConfig[] _seats = CreateDefaultSeatConfigs();

        [Header("Hole Cards")]
        [Tooltip("Card width in canvas pixels. Height is derived automatically from the aspect ratio.")]
        [SerializeField] private float _cardWidth = 120f;

        [Tooltip("Pixel gap between the two hole cards.")]
        [SerializeField] private float _cardGap = 16f;

        [SerializeField, HideInInspector] private float _cardHeight = 120f * (95f / 65f);

        private const float CardAspectRatio = 95f / 65f;

        /// <summary>Former CardsBehindPanel anchor Y — added to SeatConfig card Y at runtime.</summary>
        private const float HoleCardsAreaCenterY = 55f;

        private Vector2 CardSize => new Vector2(_cardWidth, _cardWidth * CardAspectRatio);

        [Header("Community Card Slots")]
        [Tooltip("The five community-card RectTransforms (Flop1, Flop2, Flop3, Turn, River). " +
                 "They receive the same size as hole cards.")]
        [SerializeField] private RectTransform[] _communityCardSlots = new RectTransform[5];

        [Header("Pot Label")]
        [Tooltip("AnchoredPosition of the pot label relative to canvas center.")]
        [SerializeField] private Vector2 _potLabelPosition = new Vector2(0f, 70f);
        [SerializeField] private RectTransform _potLabel;

        [Header("Dealer Button")]
        [SerializeField] private RectTransform _dealerButton;
        [SerializeField, Tooltip("Legacy sprite — SDF disc is used at runtime; kept for reference only.")]
        private Sprite        _dealerButtonSprite;
        [SerializeField] private float         _dealerButtonSize = 48f;

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Returns the player views in seat order (index 0 = human player).</summary>
        public PlayerView[] GetPlayerViews() => _playerViews;

        /// <summary>Seat layout entry for the given index.</summary>
        public SeatConfig GetSeatConfig(int seatIndex)
            => (uint)seatIndex < (uint)SeatCount ? _seats[seatIndex] : default;

        /// <summary>Hole-card width in canvas pixels (Inspector).</summary>
        public float HoleCardWidth => _cardWidth;

        /// <summary>Gap between hole cards in canvas pixels.</summary>
        public float HoleCardGap => _cardGap;

        /// <summary>
        /// Display name for a seat — uses the <see cref="PlayerView"/> on that slot first,
        /// then <see cref="SeatConfig.playerName"/>, then a generic fallback.
        /// </summary>
        public string GetPlayerName(int seatIndex)
        {
            if ((uint)seatIndex >= (uint)SeatCount)
                return FallbackPlayerName(seatIndex);

            if (_playerViews != null
                && seatIndex < _playerViews.Length
                && _playerViews[seatIndex] != null)
            {
                string viewName = _playerViews[seatIndex].ResolveDisplayName();
                if (!string.IsNullOrWhiteSpace(viewName))
                    return viewName;
            }

            string configured = _seats[seatIndex].playerName;
            if (!string.IsNullOrWhiteSpace(configured))
                return configured.Trim();

            return FallbackPlayerName(seatIndex);
        }

        /// <summary>Copies each seat view's display name onto its HUD label and SeatConfig.</summary>
        public void SyncPlayerDisplayNames()
        {
            for (int i = 0; i < SeatCount; i++)
            {
                if (i >= _playerViews.Length || _playerViews[i] == null)
                    continue;

                PlayerView view = _playerViews[i];
                if (!string.IsNullOrWhiteSpace(view.DisplayName))
                    view.ApplyDisplayNameToHud();

                string name = view.ResolveDisplayName();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (i < _seats.Length)
                {
                    SeatConfig cfg = _seats[i];
                    cfg.playerName = name;
                    _seats[i]      = cfg;
                }
            }
        }

        private static string FallbackPlayerName(int seatIndex)
            => seatIndex == 0 ? "You" : $"Bot {seatIndex}";

        /// <summary>Copies mirrorHud from seat configs onto each PlayerView.</summary>
        public void SyncHudMirrorFlags()
        {
            for (int i = 0; i < SeatCount; i++)
            {
                if (i >= _playerViews.Length || _playerViews[i] == null) continue;
                _playerViews[i].SetHudMirrored(_seats[i].mirrorHud);
            }
        }

        /// <summary>Sets card dimensions and gap at runtime, then re-applies the full layout.</summary>
        public void SetCardLayout(float cardWidth, float cardGap)
        {
            _cardWidth  = cardWidth;
            _cardGap    = cardGap;
            _cardHeight = _cardWidth * CardAspectRatio;
            ApplyLayout();
        }

        /// <summary>Applies all layout positions immediately.</summary>
        public void ApplyLayout()
        {
            SyncPlayerDisplayNames();
            ApplySeats();
            ApplyPotLabel();
            ApplyCommunityCards();
        }

        /// <summary>
        /// Moves the dealer button to the given seat index (0-based).
        /// Has no effect when the dealer button reference is unassigned.
        /// </summary>
        public void PlaceDealerButton(int seatIndex)
        {
            if (_dealerButton == null) return;
            if ((uint)seatIndex >= SeatCount) return;
            if (seatIndex >= _playerViews.Length || _playerViews[seatIndex] == null) return;

            EnsureDealerButtonVisual();

            PlayerView view = _playerViews[seatIndex];

            _dealerButton.anchorMin        = new Vector2(0.5f, 0.5f);
            _dealerButton.anchorMax        = new Vector2(0.5f, 0.5f);
            _dealerButton.pivot            = new Vector2(0.5f, 0.5f);
            _dealerButton.sizeDelta        = new Vector2(_dealerButtonSize, _dealerButtonSize);
            _dealerButton.anchoredPosition = ComputeDealerButtonCanvasPosition(view);
            EnsureDealerButtonVisual();
            _dealerButton.SetAsLastSibling();
            _dealerButton.gameObject.SetActive(true);
        }

        /// <summary>Hides the dealer button token.</summary>
        public void HideDealerButton()
        {
            if (_dealerButton != null)
                _dealerButton.gameObject.SetActive(false);
        }

        private void EnsureDealerButtonVisual()
        {
            if (_dealerButton == null) return;

            RemoveLegacyDealerGraphics(_dealerButton.gameObject);

            DealerButtonSdfGraphic disc  = EnsureDealerDisc(_dealerButton);
            TextMeshProUGUI        label = EnsureDealerLabel(_dealerButton);
            disc.transform.SetAsFirstSibling();
            label.transform.SetAsLastSibling();

            float radius = _dealerButtonSize * 0.5f - 1f;
            disc.RadiusPx       = radius;
            disc.raycastTarget  = false;
            disc.AssignShaderIfNeeded();
            disc.ForceRefresh();
            label.fontSize      = Mathf.RoundToInt(_dealerButtonSize * 0.58f);
            label.raycastTarget = false;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        private static void RemoveLegacyDealerGraphics(GameObject root)
        {
            foreach (Graphic graphic in root.GetComponents<Graphic>())
                DestroyGraphic(graphic);

            CanvasRenderer rootRenderer = root.GetComponent<CanvasRenderer>();
            if (rootRenderer != null && root.GetComponent<Graphic>() == null)
                DestroyGraphic(rootRenderer);
        }

        private static void DestroyGraphic(UnityEngine.Object target)
        {
            if (target == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(target);
            else
#endif
                UnityEngine.Object.Destroy(target);
        }

        private static DealerButtonSdfGraphic EnsureDealerDisc(RectTransform root)
        {
            Transform discT = FindDirectChild(root, "Disc");
            GameObject discGo = discT != null ? discT.gameObject : new GameObject("Disc", typeof(RectTransform));
            if (discT == null)
                discGo.transform.SetParent(root, false);

            var disc = discGo.GetComponent<DealerButtonSdfGraphic>();
            if (disc == null)
                disc = discGo.AddComponent<DealerButtonSdfGraphic>();

            StretchRect((RectTransform)discGo.transform);
            disc.color = Color.white;
            return disc;
        }

        private static TextMeshProUGUI EnsureDealerLabel(RectTransform root)
        {
            Transform labelT = FindDirectChild(root, "DealerLabel");
            if (labelT == null)
                labelT = FindDirectChild(root, "Label");

            GameObject labelGo = labelT != null ? labelT.gameObject : new GameObject("DealerLabel", typeof(RectTransform));
            if (labelT == null)
                labelGo.transform.SetParent(root, false);
            else if (labelGo.name == "Label")
                labelGo.name = "DealerLabel";

            var label = labelGo.GetComponent<TextMeshProUGUI>();
            if (label == null)
                label = labelGo.AddComponent<TextMeshProUGUI>();

            StretchRect((RectTransform)labelGo.transform);
            if (label.font == null)
            {
                TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF")
                                  ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
                if (font != null)
                    label.font = font;
            }
            label.text               = "D";
            label.fontStyle          = FontStyles.Bold;
            label.alignment          = TextAlignmentOptions.Center;
            label.enableAutoSizing   = false;
            label.color              = Color.black;
            label.enableWordWrapping  = false;
            label.overflowMode        = TextOverflowModes.Overflow;
            label.margin              = Vector4.zero;
            return label;
        }

        private static void StretchRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    return child;
            }
            return null;
        }

        /// <summary>Canvas-local position for the dealer token at the seat's DealerButtonAnchor.</summary>
        private Vector2 ComputeDealerButtonCanvasPosition(PlayerView view)
        {
            RectTransform anchor = view.DealerButtonAnchorRect;
            if (anchor == null)
                return Vector2.zero;

            if (_canvasRect == null)
            {
                var canvas = anchor.GetComponentInParent<Canvas>();
                if (canvas != null)
                    _canvasRect = (RectTransform)canvas.transform;
            }

            if (_canvasRect != null)
            {
                Vector3 canvasLocal = _canvasRect.InverseTransformPoint(anchor.position);
                return new Vector2(canvasLocal.x, canvasLocal.y);
            }

            return anchor.position;
        }

        private const float BetChipStackWidth      = 50f;
        private const float BetChipStackHeight     = 48f;
        private const float BetChipBadgeGap        = 6f;
        private const float BetAmountBadgeWidth    = 90f;
        private const float BetAmountBadgeHeight   = 30f;
        private const float BetAmountBadgeCenterX  =
            BetChipStackWidth * 0.5f + BetChipBadgeGap + BetAmountBadgeWidth * 0.5f;

        private static void SetBetDisplayChildRect(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta        = new Vector2(w, h);
        }

        private static Transform FindBetDisplayRoot(Transform betAnchor)
        {
            if (betAnchor == null)
                return null;

            Transform nested = betAnchor.Find("BetDisplay");
            return nested != null ? nested : betAnchor;
        }

        private static void ApplyBetAnchorContentLayout(RectTransform layoutRoot)
        {
            if (layoutRoot == null)
                return;

            Transform contentRoot = FindBetDisplayRoot(layoutRoot);

            Transform chipStack = contentRoot.Find("ChipStack");
            if (chipStack != null)
            {
                SetBetDisplayChildRect(
                    (RectTransform)chipStack,
                    0f, 0f, BetChipStackWidth, BetChipStackHeight);
            }

            Transform amountBadge = contentRoot.Find("AmountBadge");
            if (amountBadge != null)
            {
                SetBetDisplayChildRect(
                    (RectTransform)amountBadge,
                    BetAmountBadgeCenterX, 0f, BetAmountBadgeWidth, BetAmountBadgeHeight);
            }
        }

        private void ApplyBetAnchor(PlayerView view, SeatConfig cfg)
        {
            Transform seatRoot = view.transform;

            RectTransform anchor = view.BetAnchorRect;
            Transform betDisplayT = seatRoot.Find("BetAnchor/BetDisplay");
            if (betDisplayT == null)
                betDisplayT = seatRoot.Find("BetDisplay");
            if (betDisplayT == null)
                return;

            bool nestedUnderAnchor = anchor != null && betDisplayT.IsChildOf(anchor);
            RectTransform layoutRoot = nestedUnderAnchor ? anchor : (RectTransform)betDisplayT;

            ApplyBetAnchorContentLayout(layoutRoot);

            var betRt = (RectTransform)betDisplayT;
            betRt.anchorMin        = new Vector2(0.5f, 0.5f);
            betRt.anchorMax        = new Vector2(0.5f, 0.5f);
            betRt.pivot            = new Vector2(0.5f, 0.5f);
            betRt.sizeDelta        = new Vector2(146f, 52f);

            RectTransform posTarget = nestedUnderAnchor ? anchor : betRt;
            if (nestedUnderAnchor)
                betRt.anchoredPosition = Vector2.zero;

            posTarget.anchorMin        = new Vector2(0.5f, 0.5f);
            posTarget.anchorMax        = new Vector2(0.5f, 0.5f);
            posTarget.pivot            = new Vector2(0.5f, 0.5f);
            posTarget.anchoredPosition = cfg.betLabelLocalPos;
        }

        private void ApplyDealerButtonAnchor(PlayerView view, SeatConfig cfg)
        {
            RectTransform anchor = view.DealerButtonAnchorRect;
            if (anchor == null)
                return;

            anchor.anchorMin        = new Vector2(0.5f, 0.5f);
            anchor.anchorMax        = new Vector2(0.5f, 0.5f);
            anchor.pivot            = new Vector2(0.5f, 0.5f);
            anchor.anchoredPosition = cfg.dealerButtonLocalPos;
        }

        // ── Unity Callbacks ───────────────────────────────────────────────

        private void Reset()
        {
            _seats = CreateDefaultSeatConfigs();

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                _canvasRect = (RectTransform)canvas.transform;
        }

        private void Awake()
        {
            if (Application.isPlaying)
            {
                ApplyLayout();
                EnsureDealerButtonVisual();
            }
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            ApplyLayout();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _cardHeight = _cardWidth * CardAspectRatio;
            UnityEditor.EditorApplication.delayCall -= OnDelayedApply;
            UnityEditor.EditorApplication.delayCall += OnDelayedApply;
        }

        private void OnDelayedApply()
        {
            UnityEditor.EditorApplication.delayCall -= OnDelayedApply;
            if (this != null)
            {
                ApplyLayout();
                if (_dealerButton != null)
                    EnsureDealerButtonVisual();
            }
        }
#endif

        // ── Layout ────────────────────────────────────────────────────────

        private void ApplySeats()
        {
            for (int i = 0; i < SeatCount; i++)
            {
                if (i >= _playerViews.Length || _playerViews[i] == null) continue;

                SeatConfig cfg = _seats[i];
                var rt         = (RectTransform)_playerViews[i].transform;

                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.pivot            = cfg.pivot;
                rt.sizeDelta        = cfg.size;

                _playerViews[i].SetHudMirrored(cfg.mirrorHud);
                ApplyHoleCards(_playerViews[i], cfg);
                ApplyHudPanel(_playerViews[i], cfg);
                _playerViews[i].ApplyHudLayout();
                ApplyBetAnchor(_playerViews[i], cfg);
                ApplyDealerButtonAnchor(_playerViews[i], cfg);
            }
        }

        private void ApplyHudPanel(PlayerView view, SeatConfig cfg)
        {
            Transform panelT = view.transform.Find("HudPanel");
            if (panelT != null)
            {
                var staleCam = panelT.GetComponent<Camera>();
                if (staleCam != null)
                    UnityEngine.Object.DestroyImmediate(staleCam);
            }

            PlayerHudLayout.ApplyHudPanelFromCard1(
                view.transform, view.GetCardRect(1), cfg.hudLocalPx);
        }

        private void ApplyHoleCards(PlayerView view, SeatConfig cfg)
        {
            RectTransform rt0 = view.GetCardRect(0);
            RectTransform rt1 = view.GetCardRect(1);

            float cy0 = cfg.card0LocalPos.y + HoleCardsAreaCenterY;
            float cy1 = cfg.card1LocalPos.y + HoleCardsAreaCenterY;

            PlayerHudLayout.ComputeHoleCardCenterX(
                cfg.hudLocalPx.x, CardSize.x, _cardGap,
                out float x0, out float x1);

            if (rt0 != null)
            {
                rt0.anchorMin        = new Vector2(0.5f, 0.5f);
                rt0.anchorMax        = new Vector2(0.5f, 0.5f);
                rt0.pivot            = new Vector2(0.5f, 0.5f);
                rt0.anchoredPosition = new Vector2(x0, cy0);
                rt0.sizeDelta        = CardSize;
                rt0.localScale       = Vector3.one;
            }

            if (rt1 != null)
            {
                rt1.anchorMin        = new Vector2(0.5f, 0.5f);
                rt1.anchorMax        = new Vector2(0.5f, 0.5f);
                rt1.pivot            = new Vector2(0.5f, 0.5f);
                rt1.anchoredPosition = new Vector2(x1, cy1);
                rt1.sizeDelta        = CardSize;
                rt1.localScale       = Vector3.one;
            }
        }

        private void ApplyCommunityCards()
        {
            if (_communityCardSlots == null) return;

            int   count      = _communityCardSlots.Length;
            float step       = _cardWidth + _cardGap;
            float totalWidth = count * _cardWidth + (count - 1) * _cardGap;
            float startX     = -totalWidth * 0.5f + _cardWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var slot = _communityCardSlots[i];
                if (slot == null) continue;

                slot.sizeDelta        = CardSize;
                slot.localScale       = Vector3.one;
                slot.anchorMin        = new Vector2(0.5f, 0.5f);
                slot.anchorMax        = new Vector2(0.5f, 0.5f);
                slot.pivot            = new Vector2(0.5f, 0.5f);
                slot.anchoredPosition = new Vector2(startX + i * step, slot.anchoredPosition.y);
            }
        }

        private void ApplyBetLabel(PlayerView view, SeatConfig cfg)
            => ApplyBetAnchor(view, cfg);

        private void ApplyPotLabel()
        {
            if (_potLabel == null) return;

            _potLabel.anchorMin        = new Vector2(0.5f, 0.5f);
            _potLabel.anchorMax        = new Vector2(0.5f, 0.5f);
            _potLabel.pivot            = new Vector2(0.5f, 0.5f);
            _potLabel.anchoredPosition = _potLabelPosition;
        }

        // ── Gizmos ────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (_canvasRect == null || _seats == null) return;

            float scale = _canvasRect.localScale.x;

            for (int i = 0; i < SeatCount && i < _seats.Length; i++)
            {
                if (i >= _playerViews.Length || _playerViews[i] == null) continue;

                SeatConfig cfg  = _seats[i];
                var seatRt      = (RectTransform)_playerViews[i].transform;
                Vector3    wPos = CanvasToWorld(seatRt.anchoredPosition);
                float      r    = Mathf.Min(cfg.size.x, cfg.size.y) * 0.5f * scale;

                // Seat ring — green for human, cyan for bots.
                Gizmos.color = i == 0
                    ? new Color(0.2f, 1.0f, 0.4f, 0.9f)
                    : new Color(0.3f, 0.8f, 1.0f, 0.9f);
                Gizmos.DrawWireSphere(wPos, r);

                // Hole-card slot outlines.
                Gizmos.color = new Color(1f, 1f, 0.2f, 0.75f);
                DrawCardGizmo(wPos, HoleCardGizmoOffset(cfg.card0LocalPos), scale);
                DrawCardGizmo(wPos, HoleCardGizmoOffset(cfg.card1LocalPos), scale);

                // Bet anchor (chip stack toward pot).
                Gizmos.color = new Color(0.9f, 0.5f, 1f, 0.75f);
                Gizmos.DrawWireSphere(
                    CanvasToWorld(seatRt.anchoredPosition + cfg.betLabelLocalPos), r * 0.2f);

                // Dealer button anchor.
                Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.85f);
                Gizmos.DrawWireSphere(
                    CanvasToWorld(seatRt.anchoredPosition + cfg.dealerButtonLocalPos), r * 0.22f);
            }

            // Pot label marker.
            if (_potLabel != null)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.9f, 0.8f);
                Gizmos.DrawWireSphere(
                    CanvasToWorld(_potLabelPosition), 20f * scale);
            }
        }

        private static Vector2 HoleCardGizmoOffset(Vector2 cfgPos)
            => new Vector2(cfgPos.x, cfgPos.y + HoleCardsAreaCenterY);

        private void DrawCardGizmo(Vector3 seatWorld, Vector2 localOffset, float scale)
        {
            Vector3 cardWorld = seatWorld + new Vector3(localOffset.x, localOffset.y, 0f) * scale;
            Gizmos.DrawWireCube(cardWorld,
                new Vector3(CardSize.x, CardSize.y, 0f) * scale);
        }

        private Vector3 CanvasToWorld(Vector2 canvasPos)
            => _canvasRect.TransformPoint(new Vector3(canvasPos.x, canvasPos.y, 0f));

        // ── Default Configs ───────────────────────────────────────────────

        private static SeatConfig[] CreateDefaultSeatConfigs()
        {
            // All positions in canvas pixels relative to canvas center (1920×1080 reference).
            // Card size: 65×95. Half-gap between two cards: 2.5px → centres at ±35.
            // cy = -15 places card centres 15 px below the panel centre, comfortably inside
            // every 185×152 panel while keeping cards away from the panel top labels.
            var panel  = new Vector2(185f, 152f);
            const float cx = 35f;   // horizontal offset from panel centre to each card centre
            const float cy = -15f;  // vertical offset from panel centre (negative = below centre)

            return new SeatConfig[SeatCount]
            {
                // Seat 0 — Human  (bottom-center)
                new SeatConfig
                {
                    pivot              = new Vector2(0.5f, 1f),
                    size               = panel,
                    card0LocalPos      = new Vector2(-cx, cy),
                    card1LocalPos      = new Vector2( cx, cy),
                    hudLocalPx         = Vector2.zero,
                    betLabelLocalPos   = new Vector2(  0f, 86f),
                    dealerButtonLocalPos = new Vector2(-133f, -113f),
                    playerName         = "Ace Maverick",
                },
                // Seat 1 — Bot 1  (bottom-left)
                new SeatConfig
                {
                    pivot              = new Vector2(1f, 1f),
                    size               = panel,
                    card0LocalPos      = new Vector2(-cx, cy),
                    card1LocalPos      = new Vector2( cx, cy),
                    hudLocalPx         = Vector2.zero,
                    betLabelLocalPos   = new Vector2( 70f, 86f),
                    dealerButtonLocalPos = new Vector2(133f, -113f),
                    mirrorHud          = true,
                    playerName         = "Lady Luck",
                },
                // Seat 2 — Bot 2  (upper-left)
                new SeatConfig
                {
                    pivot              = new Vector2(1f, 0f),
                    size               = panel,
                    card0LocalPos      = new Vector2(-cx, cy),
                    card1LocalPos      = new Vector2( cx, cy),
                    hudLocalPx         = Vector2.zero,
                    betLabelLocalPos   = new Vector2( 70f, -86f),
                    dealerButtonLocalPos = new Vector2(133f, 113f),
                    mirrorHud          = true,
                    playerName         = "Prince Beaumont",
                },
                // Seat 3 — Bot 3  (top-center)
                new SeatConfig
                {
                    pivot              = new Vector2(0.5f, 0f),
                    size               = panel,
                    card0LocalPos      = new Vector2(-cx, cy),
                    card1LocalPos      = new Vector2( cx, cy),
                    hudLocalPx         = Vector2.zero,
                    betLabelLocalPos   = new Vector2(  0f, -86f),
                    dealerButtonLocalPos = new Vector2(-133f, 113f),
                    playerName         = "Victor Shark",
                },
                // Seat 4 — Bot 4  (upper-right)
                new SeatConfig
                {
                    pivot              = new Vector2(0f, 0f),
                    size               = panel,
                    card0LocalPos      = new Vector2(-cx, cy),
                    card1LocalPos      = new Vector2( cx, cy),
                    hudLocalPx         = Vector2.zero,
                    betLabelLocalPos   = new Vector2(-70f, -86f),
                    dealerButtonLocalPos = new Vector2(-133f, 113f),
                    playerName         = "Jasmine Vale",
                },
                // Seat 5 — Bot 5  (bottom-right)
                new SeatConfig
                {
                    pivot              = new Vector2(0f, 1f),
                    size               = panel,
                    card0LocalPos      = new Vector2(-cx, cy),
                    card1LocalPos      = new Vector2( cx, cy),
                    hudLocalPx         = Vector2.zero,
                    betLabelLocalPos   = new Vector2(-70f, 86f),
                    dealerButtonLocalPos = new Vector2(-133f, -113f),
                    playerName         = "Alex Hunter",
                },
            };
        }
    }
}
