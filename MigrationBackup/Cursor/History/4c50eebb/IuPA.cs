using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TexasHoldem
{
    [DefaultExecutionOrder(-100)]
    public class PlayerView : MonoBehaviour
    {
        private const string PrefabDefaultName = "Alex Hunter";
        // Platform-independent German number format: 1000 → "1.000"
        private static readonly NumberFormatInfo GermanNFI = new NumberFormatInfo
        {
            NumberGroupSeparator   = ".",
            NumberDecimalSeparator = ",",
            NumberDecimalDigits    = 0,
            NumberGroupSizes       = new[] { 3 }
        };

        [Header("HUD Text")]
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _chipsText;
        [SerializeField] private TMP_Text _statusText;
        [Tooltip("Character name for this seat — source of truth for game logic and HUD.")]
        [SerializeField] private string _displayName;

        [Header("Font Sizes")]
        [SerializeField] private float _nameFontSize   = 20f;
        [SerializeField] private float _nameFontSizeMin = 14f;
        [SerializeField] private float _chipsFontSize  = 13f;
        [SerializeField] private float _statusFontSize = 10f;

        [Header("Cards")]
        [SerializeField] private List<CardView> _cardSlots;

        [Header("Seat Visuals")]
        [SerializeField] private CanvasGroup          _canvasGroup;
        [FormerlySerializedAs("_avatarRingSdf")]
        [SerializeField] private AvatarRingSdfGraphic _avatarRingChrome; // always-on silver chrome base ring
        [SerializeField] private AvatarRingSdfGraphic _avatarRingGold;   // gold countdown overlay on active turn
        [SerializeField] private Image                _avatarImage;   // AvatarCircleImage — shader-clipped avatar photo
        [SerializeField] private BetDisplay           _betDisplay;    // chip stack + amount on BetAnchor
        [SerializeField] private ActionBadge          _actionBadge;   // neon pill when player acts
        [SerializeField] private SeatActionMenu     _seatActionMenu; // human Check/Fold/Raise above name
        [SerializeField] private RectTransform        _avatarFrame;   // chip animation origin
        [SerializeField] private HudPanelGlowGraphic _hudGlow; // SDF outer bloom; GGPoker-style smooth bright↔invisible pulse on active turn

        [Header("HUD Glow Pulse")]
        [FormerlySerializedAs("_hudGlowMaxIntensity")]
        [SerializeField, HideInInspector] private float _legacyHudGlowMaxIntensity;
        [SerializeField, Range(0f, 1f)] private float _hudGlowMinIntensity = 0f;
        [Tooltip("Seconds to hold at min and at max each cycle (GGPoker-style plateaus).")]
        [SerializeField, Range(0.05f, 0.6f)]  private float _hudGlowHoldSecs   = 0.25f;
        [Tooltip("Seconds for each fade between min and max.")]
        [SerializeField, Range(0.05f, 0.6f)] private float _hudGlowFadeSecs  = 0.25f;
        [SerializeField] private Color        _hudGlowColor                    = Color.white;

        [Header("Gold Ring Urgency")]
        [SerializeField, Range(0.05f, 0.5f)] private float _ringUrgencyWindowFrac = 0.25f;
        [SerializeField] private Color _ringUrgentColorTop = new Color(1.00f, 0.35f, 0.25f, 1f);
        [SerializeField] private Color _ringUrgentColorBot = new Color(0.55f, 0.08f, 0.05f, 1f);

        [Header("HUD Layout")]
        [Tooltip("Mirror HUD band: avatar on the right, name/chips on the left (left table seats).")]
        [SerializeField] private bool _hudMirrored;

        [Header("Testing")]
        [SerializeField] private bool _showAvatarImages = true; // disable to hide all avatar photos during testing
        [SerializeField] private bool _showChromeRing   = true; // disable to hide the chrome base ring during testing


        private bool      _isHuman;
        private int       _revealedHoleCount;
        private Coroutine _ringCountdown;
        private Sprite    _currentAvatarSprite; // cached so the toggle can restore it without re-calling SetAvatar

        public bool HudMirrored => _hudMirrored;

        /// <summary>Resolved display name for this seat (serialized field, then name label).</summary>
        public string DisplayName => ResolveDisplayName();

        public string ResolveDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(_displayName))
                return _displayName.Trim();

            if (_nameText != null && !string.IsNullOrWhiteSpace(_nameText.text))
            {
                string label = _nameText.text.Trim();
                if (!IsPrefabDefaultName(label))
                    return label;
            }

            return string.Empty;
        }

        /// <summary>True when this seat already has a portrait from the scene or an explicit assignment.</summary>
        public bool HasSeatAvatar()
            => _currentAvatarSprite != null
               || (_avatarImage != null && _avatarImage.sprite != null);

        public void SetDisplayName(string name)
        {
            _displayName = name?.Trim() ?? string.Empty;
            ApplyDisplayNameToHud();
        }

        /// <summary>Writes <see cref="_displayName"/> to the HUD label without changing the serialized name.</summary>
        public void ApplyDisplayNameToHud()
        {
            if (_nameText != null && !string.IsNullOrWhiteSpace(_displayName))
                _nameText.text = _displayName;
        }

        public void SetHudMirrored(bool mirrored)
        {
            if (_hudMirrored == mirrored) return;
            _hudMirrored = mirrored;
            ApplyHudLayout();
        }

        /// <summary>Repositions avatar and name/chips for default or mirrored HUD band.</summary>
        public void ApplyHudLayout()
        {
            EnsureHudGlowRef();
            ApplyHudGlowSettings();
            PlayerHudLayout.Apply(transform, _hudMirrored);
        }

        private void Awake()
        {
            CaptureAvatarFromImage();
            EnsureDisplayNameFromLabel();
            ApplyDisplayNameToHud();
            ApplyNameFontSize();
            ApplyFontSize(_chipsText,  _chipsFontSize);
            ApplyFontSize(_statusText, _statusFontSize);
            EnsureRingRefs();
            EnsureHudGlowRef();
            ApplyHudGlowSettings();
            SyncRingGeometry();
            ApplyChromeRingVisibility();
            ApplyGoldRingIdle();
            ApplyHudGlowIdle();
        }

        private void Start()
        {
            SyncRingGeometry();
            // Fallback when no TableLayoutManager drives seat layout (TableLayoutManager.Start applies all seats).
            if (FindFirstObjectByType<TableLayoutManager>() == null)
                ApplyHudLayout();
        }

        /// <summary>Applies font size and visibility changes immediately when Inspector values are modified.</summary>
        private void OnValidate()
        {
            CaptureAvatarFromImage();
            EnsureDisplayNameFromLabel();
            ApplyDisplayNameToHud();
            ApplyNameFontSize();
            ApplyFontSize(_chipsText,  _chipsFontSize);
            ApplyFontSize(_statusText, _statusFontSize);
            EnsureRingRefs();
            EnsureHudGlowRef();
            ApplyHudGlowSettings();
            SyncRingGeometry();
            ApplyAvatarVisibility();
            ApplyChromeRingVisibility();
            ApplyGoldRingIdle();
            ApplyHudGlowIdle();
            ApplyHudLayout();
        }

        private void ApplyNameFontSize()
        {
            if (_nameText == null) return;
            float min = Mathf.Clamp(_nameFontSizeMin, 8f, _nameFontSize);
            _nameText.enableAutoSizing   = true;
            _nameText.fontSize           = _nameFontSize;
            _nameText.fontSizeMin        = min;
            _nameText.fontSizeMax        = _nameFontSize;
            _nameText.enableWordWrapping = false;
            _nameText.overflowMode       = TextOverflowModes.Ellipsis;
            _nameText.alignment          = PlayerHudLayout.HudPanelTextAlign;
        }

        /// <summary>Sets a fixed font size on the text element without altering its RectTransform dimensions.</summary>
        private void ApplyFontSize(TMP_Text text, float size)
        {
            if (text == null) return;
            text.enableAutoSizing = false;
            text.fontSize         = size;
            text.alignment        = PlayerHudLayout.HudPanelTextAlign;
        }

        private void EnsureDisplayNameFromLabel()
        {
            if (!string.IsNullOrWhiteSpace(_displayName))
                return;

            if (_nameText == null || string.IsNullOrWhiteSpace(_nameText.text))
                return;

            string label = _nameText.text.Trim();
            if (IsPrefabDefaultName(label))
                return;

            _displayName = label;
        }

        private static bool IsPrefabDefaultName(string label)
            => string.Equals(label, PrefabDefaultName, System.StringComparison.OrdinalIgnoreCase);

        private void CaptureAvatarFromImage()
        {
            if (_currentAvatarSprite != null)
                return;

            if (_avatarImage != null && _avatarImage.sprite != null)
                _currentAvatarSprite = _avatarImage.sprite;
        }

        /// <summary>Returns the RectTransform of the hole-card slot at the given index.</summary>
        public RectTransform GetCardRect(int index)
        {
            if (index < 0 || index >= _cardSlots.Count || _cardSlots[index] == null) return null;
            return (RectTransform)_cardSlots[index].transform;
        }

        /// <summary>
        /// BetAnchor in seat local space — chip stack sits here (TableLayoutManager.ApplyBetAnchor).
        /// </summary>
        public RectTransform BetAnchorRect
        {
            get
            {
                // Prefer the anchor that owns BetDisplay (ignore empty scene overrides).
                foreach (Transform child in transform)
                {
                    if (child.name != "BetAnchor")
                        continue;
                    if (child.Find("BetDisplay") != null)
                        return (RectTransform)child;
                }

                Transform fallback = transform.Find("BetAnchor");
                return fallback != null ? (RectTransform)fallback : null;
            }
        }

        /// <summary>Alias for layout gizmos — same as <see cref="BetAnchorRect"/>.</summary>
        public RectTransform BetLabelRect => BetAnchorRect;

        /// <summary>
        /// DealerButtonAnchor in seat local space — dealer token is placed here (TableLayoutManager).
        /// </summary>
        public RectTransform DealerButtonAnchorRect
        {
            get
            {
                Transform t = transform.Find("DealerButtonAnchor");
                return t != null ? (RectTransform)t : null;
            }
        }

        /// <summary>RectTransform of the avatar frame — used as the chip animation origin.</summary>
        public RectTransform AvatarRect =>
            _avatarFrame != null ? _avatarFrame : (RectTransform)transform;

        public void SetIsHuman(bool isHuman) => _isHuman = isHuman;

        /// <summary>
        /// Assigns an avatar sprite to the shader-clipped photo image.
        /// </summary>
        public void SetAvatar(Sprite sprite)
        {
            if (sprite == null)
                return;

            _currentAvatarSprite = sprite;
            ApplyAvatarVisibility();
        }

        /// <summary>Shows or hides the avatar photo based on the testing toggle.</summary>
        private void ApplyAvatarVisibility()
        {
            if (_avatarImage == null) return;
#if UNITY_EDITOR
            // _currentAvatarSprite is null when called from OnValidate (field is non-serialized),
            // but is valid when called via SetAvatar (e.g. from UIManager.ApplySceneModePreview).
            // Apply the sprite when available; keep color white so any Inspector-assigned sprite
            // on the Image component remains visible even when _currentAvatarSprite is null.
            if (!Application.isPlaying)
            {
                bool editorVisible = _showAvatarImages && _currentAvatarSprite != null;
                _avatarImage.sprite = editorVisible ? _currentAvatarSprite : null;
                _avatarImage.color  = _showAvatarImages ? Color.white : Color.clear;
                return;
            }
#endif
            bool visible = _showAvatarImages && _currentAvatarSprite != null;
            _avatarImage.sprite = visible ? _currentAvatarSprite : null;
            _avatarImage.color  = visible ? Color.white : Color.clear;
        }

        /// <summary>Resolves ring refs from AvatarFrame children when the prefab was only partially wired.</summary>
        private void EnsureRingRefs()
        {
            if (_avatarFrame == null) return;
            if (_avatarRingChrome == null)
                _avatarRingChrome = _avatarFrame.Find("AvatarRingChrome")?.GetComponent<AvatarRingSdfGraphic>();
            if (_avatarRingGold == null)
                _avatarRingGold = _avatarFrame.Find("AvatarRingGold")?.GetComponent<AvatarRingSdfGraphic>();
        }

        /// <summary>Keeps the gold overlay on the same annulus as the chrome base ring.</summary>
        private void SyncRingGeometry()
        {
            if (_avatarRingChrome == null || _avatarRingGold == null) return;
            _avatarRingGold.CopyGeometryFrom(_avatarRingChrome);
        }

        /// <summary>Shows or hides the chrome base ring based on the testing toggle.</summary>
        private void ApplyChromeRingVisibility()
        {
            if (_avatarRingChrome == null) return;
            _avatarRingChrome.Look       = AvatarRingSdfGraphic.RingLook.Chrome;
            _avatarRingChrome.FillAmount = 1f;
            _avatarRingChrome.color      = _showChromeRing ? Color.white : Color.clear;
        }

        /// <summary>Hides the gold overlay when this seat is not on an active countdown.</summary>
        private void ApplyGoldRingIdle()
        {
            if (_avatarRingGold == null || _ringCountdown != null) return;
            _avatarRingGold.RestoreDefaultGoldColors();
            _avatarRingGold.Look       = AvatarRingSdfGraphic.RingLook.Gold;
            _avatarRingGold.FillAmount = 1f;
            _avatarRingGold.color      = Color.clear;
        }

        private void ApplyGoldRingUrgency(float remaining, float duration)
        {
            if (_avatarRingGold == null || duration <= 0f) return;

            float window  = duration * _ringUrgencyWindowFrac;
            float urgency = window <= 0f ? 0f : 1f - Mathf.Clamp01(remaining / window);
            Color top = Color.Lerp(_avatarRingGold.DefaultGoldColorTop, _ringUrgentColorTop, urgency);
            Color bot = Color.Lerp(_avatarRingGold.DefaultGoldColorBot, _ringUrgentColorBot, urgency);
            _avatarRingGold.SetGoldColors(top, bot);
        }

        private void EnsureHudGlowRef()
        {
            if (_hudGlow != null) return;
            _hudGlow = transform.Find("HudGlow")?.GetComponent<HudPanelGlowGraphic>();
        }

        /// <summary>Pushes shared glow settings and pulse color into the HudGlow graphic.</summary>
        private void ApplyHudGlowSettings()
        {
            if (_hudGlow == null) return;
            PlayerHudLayout.ApplyStandardHudGlow(_hudGlow, _hudGlowColor);
            _legacyHudGlowMaxIntensity = 0f;
        }

        /// <summary>Hides the HUD glow when this seat is not on an active countdown.</summary>
        private void ApplyHudGlowIdle()
        {
            if (_hudGlow == null || _ringCountdown != null) return;
#if UNITY_EDITOR
            // Edit mode: keep HudGlow intensity as set in the Inspector (layout/preview).
            if (!Application.isPlaying) return;
#endif
            _hudGlow.GlowIntensity = 0f;
        }

        /// <summary>GGPoker-style pulse: hold dark, fade up, hold bright, fade down.</summary>
        private float SampleHudGlowBreath(float elapsed)
        {
            float hold   = _hudGlowHoldSecs;
            float fade   = _hudGlowFadeSecs;
            float period = hold * 2f + fade * 2f;
            if (period <= 0f) return _hudGlowMinIntensity;

            float t = elapsed % period;
            float min = Mathf.Clamp(_hudGlowMinIntensity, 0f, 1.5f);
            float peak = _hudGlow != null ? _hudGlow.PeakGlowIntensity : 0f;
            if (peak < 0.01f) peak = 1.1f;
            float max = Mathf.Clamp(peak, min + 0.01f, 1.5f);

            if (t < hold) return min;
            t -= hold;
            if (t < fade) return Mathf.Lerp(min, max, SmoothStep01(t / fade));
            t -= fade;
            if (t < hold) return max;
            t -= hold;
            return Mathf.Lerp(max, min, SmoothStep01(t / fade));
        }

        private static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Clears hole-card reveal tracking (call at the start of a new round).</summary>
        public void ResetHoleCardReveal() => _revealedHoleCount = 0;

        /// <summary>Hides hole cards and clears reveal state (title screen / full reset).</summary>
        public void ClearTableCards()
        {
            ResetHoleCardReveal();
            HideAllCardSlots();
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;
            if (_statusText != null)
                _statusText.text = string.Empty;
        }

        /// <summary>Updates name, chips, status text, and canvas fade. Bet is handled separately via ShowBetDisplay.</summary>
        public void RefreshHud(PlayerState player)
        {
            if (_nameText  != null) _nameText.text  = player.Name;
            if (_chipsText != null) _chipsText.text = player.Chips.ToString("N0", GermanNFI) + " \u20AC";

            string status = player.Chips == 0 ? "Eliminated"
                                : player.HasFolded  ? "Folded"
                                : player.IsAllIn    ? "All In"
                                :                    "";
            if (_statusText != null) _statusText.text = status;

            if (_canvasGroup != null)
            {
                bool isEliminated = player.Chips == 0 && !player.IsAllIn;
                _canvasGroup.alpha = (player.HasFolded || isEliminated) ? 0.45f : 1f;
            }

            if (player.HasFolded)
                HideAllCardSlots();
        }

        /// <summary>
        /// Shows the bet display with the given amount.
        /// Triggers a chip animation from the avatar when <paramref name="fromRect"/> is provided.
        /// </summary>
        public void ShowBetDisplay(int amount, RectTransform fromRect = null)
        {
            if (_betDisplay != null)
                _betDisplay.ShowBet(amount, fromRect);
        }

        /// <summary>Hides the bet display.</summary>
        public void HideBetDisplay()
        {
            if (_betDisplay != null)
                _betDisplay.HideBet();
        }

        /// <summary>Shows a neon action badge (Check, Fold, Raise, Call, All-In).</summary>
        public void ShowAction(BettingAction action, int amount = 0)
        {
            ActionBadge badge = ResolveActionBadge();
            if (badge == null)
            {
                Debug.LogWarning("[PlayerView] ActionBadge missing — run Texas Holdem → Apply Player Seat Layout.", this);
                return;
            }

            badge.Show(action, amount);
        }

        /// <summary>Hides the action badge.</summary>
        public void HideActionBadge()
        {
            ResolveActionBadge()?.Hide();
        }

        /// <summary>Keeps a visible action badge above bet chips after HUD refresh.</summary>
        public void BringActionBadgeToFrontIfVisible()
        {
            ActionBadge badge = ResolveActionBadge();
            if (badge != null && badge.gameObject.activeInHierarchy)
                badge.BringToFront();
        }

        private ActionBadge ResolveActionBadge()
        {
            Transform badgeTransform = transform.Find("ActionBadge");
            if (badgeTransform != null)
                _actionBadge = badgeTransform.GetComponent<ActionBadge>();

            if (_actionBadge == null)
                _actionBadge = GetComponentInChildren<ActionBadge>(true);

            return _actionBadge;
        }

        /// <summary>Hides name/chips while the seat action menu replaces that HUD band.</summary>
        public void SetSeatMenuHudMode(bool menuOpen)
        {
            if (_nameText  != null) _nameText.gameObject.SetActive(!menuOpen);
            if (_chipsText != null) _chipsText.gameObject.SetActive(!menuOpen);
        }

        /// <summary>Seat menu for human betting choices above the player name.</summary>
        public SeatActionMenu SeatActionMenu
        {
            get
            {
                if (_seatActionMenu == null)
                    _seatActionMenu = GetComponentInChildren<SeatActionMenu>(true);
                return _seatActionMenu;
            }
        }

        /// <summary>
        /// Keeps the chrome ring visible and animates the gold overlay during an active turn countdown.
        /// Pass isActive=false (or zero duration) to hide the gold overlay.
        /// </summary>
        public void SetActiveTurn(bool isActive, float countdownDuration = 0f)
        {
            if (_ringCountdown != null)
            {
                StopCoroutine(_ringCountdown);
                _ringCountdown = null;
            }

            SyncRingGeometry();
            ApplyChromeRingVisibility();

            if (isActive && countdownDuration > 0f)
            {
                if (_avatarRingGold != null)
                {
                    _avatarRingGold.RestoreDefaultGoldColors();
                    _avatarRingGold.Look       = AvatarRingSdfGraphic.RingLook.Gold;
                    _avatarRingGold.FillAmount = 1f;
                    _avatarRingGold.color      = Color.white;
                }

                _ringCountdown = StartCoroutine(RunRingCountdown(countdownDuration));
            }
            else
            {
                ApplyGoldRingIdle();
                ApplyHudGlowIdle();
            }
        }

        /// <summary>Drains the gold ring and softly pulses the HUD glow over the given duration.</summary>
        private IEnumerator RunRingCountdown(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float remaining = duration - elapsed;

                if (_avatarRingGold != null)
                {
                    _avatarRingGold.FillAmount = Mathf.Clamp01(1f - elapsed / duration);
                    ApplyGoldRingUrgency(remaining, duration);
                }

                if (_hudGlow != null)
                    _hudGlow.GlowIntensity = SampleHudGlowBreath(elapsed);

                yield return null;
            }

            ApplyGoldRingIdle();
            ApplyHudGlowIdle();
            _ringCountdown = null;
        }

        /// <summary>Shows face-down hole cards for AI opponents.</summary>
        public void RefreshOpponentCards(PlayerState player)
        {
            if (player.HasFolded)
            {
                HideAllCardSlots();
                return;
            }

            for (int i = 0; i < _cardSlots.Count; i++)
            {
                if (i < player.HoleCards.Count)
                    _cardSlots[i].ShowFaceDown();
                else
                    _cardSlots[i].Hide();
            }
        }

        /// <summary>Staggered flip reveal for the human player's hole cards.</summary>
        public IEnumerator RevealHumanHoleCards(PlayerState player, float flipDuration, float flipGap)
        {
            if (!_isHuman) yield break;

            if (player.HoleCards.Count == 0)
            {
                _revealedHoleCount = 0;
                HideAllCardSlots();
                yield break;
            }

            for (int i = _revealedHoleCount; i < player.HoleCards.Count; i++)
            {
                if (i >= _cardSlots.Count || _cardSlots[i] == null) continue;

                CardView slot = _cardSlots[i];
                slot.SetFlipDuration(flipDuration);
                slot.ShowFaceDown();

                bool flipDone = false;
                slot.FlipToFace(player.HoleCards[i], () => flipDone = true);
                yield return new WaitUntil(() => flipDone);

                if (i < player.HoleCards.Count - 1)
                    yield return new WaitForSeconds(flipGap);
            }

            _revealedHoleCount = player.HoleCards.Count;

            for (int i = player.HoleCards.Count; i < _cardSlots.Count; i++)
                _cardSlots[i]?.Hide();
        }

        /// <summary>Flips all hole cards face-up during showdown.</summary>
        public void RevealCards(PlayerState player)
        {
            for (int i = 0; i < _cardSlots.Count && i < player.HoleCards.Count; i++)
                _cardSlots[i].Show(player.HoleCards[i]);

            _revealedHoleCount = player.HoleCards.Count;
        }

        private void HideAllCardSlots()
        {
            foreach (CardView slot in _cardSlots)
                slot?.Hide();
        }

        /// <summary>
        /// Triggers the winner celebration: shimmering gold ring, HUD glow pulse, and WIN badge.
        /// Automatically cleaned up when OnRoundEnded calls SetActiveTurn(false).
        /// </summary>
        public void StartWinnerHighlight(int potAmount, float duration)
        {
            if (_ringCountdown != null)
            {
                StopCoroutine(_ringCountdown);
                _ringCountdown = null;
            }

            SyncRingGeometry();
            ApplyChromeRingVisibility();

            if (_avatarRingGold != null)
            {
                // Bright, fully-saturated gold for the win moment.
                _avatarRingGold.SetGoldColors(
                    new Color(1.00f, 0.95f, 0.20f, 1f),
                    new Color(0.85f, 0.50f, 0.00f, 1f));
                _avatarRingGold.Look       = AvatarRingSdfGraphic.RingLook.Gold;
                _avatarRingGold.FillAmount = 1f;
                _avatarRingGold.color      = Color.white;
            }

            ResolveActionBadge()?.ShowWin(potAmount, duration);
            _ringCountdown = StartCoroutine(RunWinnerHighlight(duration));
        }

        /// <summary>Pulses the gold ring alpha and HUD glow for the winner celebration window.</summary>
        private IEnumerator RunWinnerHighlight(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                // Fast shimmer on the gold ring.
                if (_avatarRingGold != null)
                {
                    float pulse = 0.70f + 0.30f * Mathf.Sin(elapsed * Mathf.PI * 5f);
                    _avatarRingGold.color = new Color(1f, 1f, 1f, pulse);
                }

                if (_hudGlow != null)
                    _hudGlow.GlowIntensity = SampleHudGlowBreath(elapsed);

                yield return null;
            }

            ApplyGoldRingIdle();
            ApplyHudGlowIdle();
            _ringCountdown = null;
        }
    }
}
