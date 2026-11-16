using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace TexasHoldem
{
    [ExecuteAlways]
    public class UIManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameManager        _gameManager;
        [SerializeField] private TableLayoutManager _tableLayout;

        [Header("Community Cards")]
        [SerializeField] private CardView _flop1;
        [SerializeField] private CardView _flop2;
        [SerializeField] private CardView _flop3;
        [SerializeField] private CardView _turnCard;
        [SerializeField] private CardView _riverCard;

        private CardView[] _communityCardSlots;

        [Tooltip("Duration for each flop card flying from the deal origin to its board slot.")]
        [SerializeField, Min(0.05f)] private float _flopCardFlyDuration = 0.12f;
        [Tooltip("Pause after each flop card lands before the next flop card is dealt.")]
        [SerializeField, Min(0f)] private float _flopCardStagger = 0.04f;

        public float FlopCardFlyDuration => _flopCardFlyDuration;
        public float FlopCardStagger     => _flopCardStagger;
        public float FlopDealTotalDuration =>
            3f * _flopCardFlyDuration + 2f * _flopCardStagger;

        [Header("Player Seats")]
        [SerializeField] private List<PlayerView> _playerViews;

        [Header("Avatars")]
        [SerializeField] private List<Sprite> _playerAvatars;

        [Header("Action Panel")]
        [SerializeField] private GameObject _actionPanel;
        [SerializeField] private Button     _startButton;
        [SerializeField] private Button     _foldButton;
        [SerializeField] private Button     _checkCallButton;
        [SerializeField] private Button     _raiseButton;
        [SerializeField] private TMP_Text   _checkCallLabel;
        [SerializeField] private TMP_InputField _raiseInput;
        [Tooltip("Vertical offset for the action button row (ActionPanel anchoredPosition.y). Tune in Play mode.")]
        [SerializeField] private float          _actionPanelYOffset;
        [Tooltip("Horizontal gap between action buttons (ButtonRow Horizontal Layout Group spacing). Tune in Play mode.")]
        [SerializeField, Min(0f)] private float _actionButtonSpacing = 12f;

        [Header("Action Column Offsets")]
        [Tooltip("Fine-tune each action column after auto-layout. Positive X = right, positive Y = up. Tune in Play mode.")]
        [SerializeField] private Vector2 _foldColumnOffset;
        [SerializeField] private Vector2 _checkCallColumnOffset;
        [SerializeField] private Vector2 _raiseColumnOffset;
        [SerializeField] private Vector2 _allInColumnOffset;

        /// <summary>Horizontal gap between action-panel buttons (see Action Button Spacing).</summary>
        public float ActionButtonSpacing => _actionButtonSpacing;

        [Header("HUD")]
        [SerializeField] private TMP_Text       _potText;
        [Tooltip("Horizontal nudge from the 5-card board center. Pot label and chips are centered on the full flop–river row.")]
        [SerializeField] private float          _potLabelX = 0f;
        [Tooltip("Visual bottom edge (parent local Y) of the pot label glyphs.")]
        [SerializeField] private float          _potBottomY = 131f;
        [Tooltip("Visual bottom edge (parent local Y) of the lowest pot chip. Independent from Pot Bottom Y.")]
        [SerializeField] private float          _potChipBottomY = 131f;
        [SerializeField] private ChipStackView  _potChipStack;
        [SerializeField] private Sprite         _potChipSprite100;
        [SerializeField] private Sprite         _potChipSprite500;
        [Tooltip("Horizontal gap between PotText right edge and the first pot chip column.")]
        [SerializeField, Min(0f)] private float _potChipPadding = 12f;
        [Tooltip("Horizontal gap between chip denominations in the pot stack (25 | 5 | 1). 0 = use TableLayoutManager Chip Column Gap X.")]
        [SerializeField, Range(0f, 48f)] private float _potChipColumnGapX;
        [Tooltip("Vertical step between identical chips in the pot stack only (bet stacks use TableLayoutManager).")]
        [SerializeField, Range(1f, 12f)] private float _potStackOverlapY = 2f;

        [Header("Copyright")]
        [SerializeField, InspectorName("CopyrightLabel")]
        [Tooltip("Copyright/version text. Edit Canvas â†’ CopyrightLabel TMP directly, or set this and use context menu Apply Copyright Label To TMP.")]
        private string _copyrightLabel = "v1.0";
        [SerializeField] private TMP_Text _copyrightLabelText;

        [Header("Between Hands")]
        [Tooltip("When on, opens the options menu after each hand. The next deal still starts immediately after Space.")]
        [SerializeField] private bool _openMenuBetweenHands;

        [Header("Debug")]
        [Tooltip("When on, bot hole cards stay face-up during the hand so you can watch AI decisions. Also available via Options menu (Show Bot Cards).")]
        [SerializeField] private bool _showBotCardsForTesting;

        [Header("Seat Bet Place")]
        [Tooltip("When off, bet chips appear instantly under the seat (GGPoker / PokerStars style).")]
        [SerializeField] private bool _animateBetPlace;

        [Header("Street Bet Collect")]
        [Tooltip("When off, seat bets clear instantly and the pot stack updates (GGPoker / PokerStars style).")]
        [SerializeField] private bool _animateStreetCollect;
        [Tooltip("Duration for each seat bet stack flying straight to the pot center.")]
        [SerializeField, Min(0.05f)] private float _collectFlyDuration = 0.5f;
        [Tooltip("Pause after all seat stacks arrive before updating the center pot display.")]
        [SerializeField, Min(0f)] private float _collectPotUpdateDelay = 0.1f;

        [Header("Preflop Deal")]
        [Tooltip("When on, hole cards fly from the dealer area with motion trails (GGPoker style).")]
        [SerializeField] private bool _animatePreflopDeal = true;
        [Tooltip("Optional anchor for deal origin. When unset, uses community-board centre plus Deal Origin Offset.")]
        [SerializeField] private RectTransform _dealOriginAnchor;
        [Tooltip("Canvas offset from the community-board centre when Deal Origin Anchor is unset.")]
        [SerializeField] private Vector2 _dealOriginCanvasOffset = new Vector2(-55f, -10f);
        [Tooltip("Shows a faint stacked deck at the deal origin while cards are dealt.")]
        [SerializeField] private bool _showDealDeckGhost = true;
        [Tooltip("Number of stacked card outlines in the deal-origin deck.")]
        [SerializeField, Range(1, 5)] private int _dealDeckGhostCount = 3;
        [Tooltip("Alpha of each deal-origin deck outline.")]
        [SerializeField, Range(0.05f, 0.6f)] private float _dealDeckGhostAlpha = 0.28f;
        [Tooltip("Pixel offset between stacked deck outlines (X = right, Y = up).")]
        [SerializeField] private Vector2 _dealDeckGhostStackOffset = new Vector2(3f, 5f);
        [Tooltip("Duration for each hole card flying to its seat.")]
        [SerializeField, Min(0.05f)] private float _holeCardFlyDuration = 0.5f;
        [Tooltip("Delay between starting each deal (cards overlap while in flight).")]
        [SerializeField, Min(0f)] private float _holeCardDealStagger = 0.06f;
        [Tooltip("Vertical stretch of each motion-smear copy (GGPoker-style blur streak).")]
        [SerializeField, Range(1f, 3.5f)] private float _holeCardSmearLength = 2f;
        [Tooltip("Starting alpha for each motion smear.")]
        [SerializeField, Range(0.05f, 0.8f)] private float _holeCardSmearAlpha = 0.4f;
        [Tooltip("Seconds between spawning smears along the flight path.")]
        [SerializeField, Range(0.01f, 0.08f)] private float _holeCardSmearSpawnInterval = 0.02f;
        [Tooltip("How long each smear fades out.")]
        [SerializeField, Range(0.05f, 0.35f)] private float _holeCardSmearFadeDuration = 0.24f;

        public bool AnimatePreflopDeal => _animatePreflopDeal;
        public float HoleCardDealStagger => _holeCardDealStagger;
        public float HoleCardSmearLength => _holeCardSmearLength;
        public float HoleCardSmearAlpha => _holeCardSmearAlpha;
        public float HoleCardSmearSpawnInterval => _holeCardSmearSpawnInterval;
        public float HoleCardSmearFadeDuration => _holeCardSmearFadeDuration;
        public float HoleCardFlyDuration => _holeCardFlyDuration;

        [Header("Winner Celebration")]
        [Tooltip("Duration for pot chips flying to the winner HUD (Backspace or B).")]
        [SerializeField, Min(0.05f)] private float _winnerPotCollectFlyDuration = 0.5f;
        [Tooltip("Pause after chips land at the winner before they vanish into the avatar.")]
        [SerializeField, Min(0f)] private float _winnerPotCollectHoldDuration = 0.5f;
        [Tooltip("Duration for chips shrinking and fading into the avatar center.")]
        [SerializeField, Min(0.05f)] private float _winnerPotCollectVanishDuration = 0.35f;
        [Tooltip("Avatar frame scale (photo + rings) at win — peak scale (1.2 = 20% bigger).")]
        [SerializeField, Min(1f)] private float _winnerAvatarPulseScale = 1.4f;
        [Tooltip("Number of avatar frame scale pulses when a player wins.")]
        [SerializeField, Range(0, 6)] private int _winnerAvatarPulseCount = 3;
        [Tooltip("Duration of each half pulse (scale up or down). Full pulse = 2× this (0.25 = 0.5s per pulse).")]
        [SerializeField, Min(0.03f)] private float _winnerAvatarPulseHalfDuration = 0.25f;

        [Header("Winner Card Highlight")]
        [Tooltip("Gold pulse rate in Hz while waiting for Space (1 = one pulse per second).")]
        [SerializeField, Min(0.1f)] private float _winnerCardPulseHz = 1f;
        [Tooltip("Minimum gold tint at the trough of each pulse.")]
        [SerializeField, Range(0f, 1f)] private float _winnerCardPulseMinBlend = 0.5f;
        [Tooltip("Maximum gold tint at the peak of each pulse.")]
        [SerializeField, Range(0f, 1f)] private float _winnerCardPulseMaxBlend = 0.85f;
        [Tooltip("Vertical lift in pixels for cards that are part of the winning hand.")]
        [SerializeField] private float _winnerCardLiftPx = 15f;
        [SerializeField] private Color _winnerCardPeakColor = new Color(1f, 0.82f, 0.22f, 1f);

        public static UIManager Instance { get; private set; }

        public float  WinnerCardPulseHz        => _winnerCardPulseHz;
        public float  WinnerCardPulseMinBlend  => _winnerCardPulseMinBlend;
        public float  WinnerCardPulseMaxBlend  => _winnerCardPulseMaxBlend;
        public float  WinnerCardLiftPx           => _winnerCardLiftPx;
        public Color  WinnerCardPeakColor        => _winnerCardPeakColor;

        [Header("Winning Hand")]
        [SerializeField] private TMP_Text _winningHandText;
        [Tooltip("Canvas position of WinningHandLabel (center anchor). Tune in Play mode.")]
        [SerializeField] private float _winningHandLabelX = 0f;
        [SerializeField] private float _winningHandLabelY = 90f;

        [Header("Showdown Rake")]
        [SerializeField] private TMP_Text _rakeText;
        [Tooltip("Canvas position of the rake label (center anchor, top-right inside the felt). Tune in Play mode.")]
        [SerializeField] private Vector2 _rakeLabelPosition = new Vector2(320f, 170f);
        [Tooltip("Font size for the rake label. Tune in Play mode.")]
        [SerializeField, Min(1f)] private float _rakeFontSize = 24f;
        [Tooltip("Rect size of the rake label. Tune in Play mode.")]
        [SerializeField] private Vector2 _rakeLabelSize = new Vector2(180f, 32f);

        [Header("Seat Action Badges")]
        [Tooltip("Global nudge for CALL / FOLD / RAISE badges on every player seat. Positive X = right, positive Y = up. Tune in Play mode.")]
        [SerializeField] private Vector2 _seatActionBadgeOffset;

        [Header("Button Font")]
        [SerializeField] private TMP_FontAsset _buttonFont;

        [Header("Action Amount Badges")]
        [Tooltip("Style for amount pills under Call and All-In. Edit in Play mode to tune live.")]
        [SerializeField] private ActionAmountBadgeStyle _actionAmountBadgeStyle = new ActionAmountBadgeStyle();

        private const string Casino3DSdfPath             = "Assets/TextMesh Pro/Fonts/Casino3D SDF.asset";
        private const string Casino3DSdfResourcesPath   = "Fonts/Casino3D SDF";

        // Vertex colors copied from BlackJack (HIT/STAND/DOUBLE) + yellow for START/raise amount.
        private static readonly Color TextStart = UiColors.PotGold;
        private static readonly Color TextFold  = ActionColors.FoldRed;
        private static readonly Color TextCheck = ActionColors.CheckCallGreen;
        private static readonly Color TextRaise = ButtonLabelStyle.RaiseText;
        private static readonly Color TextAllIn = new Color(1f, 0f, 1f, 1f);

        // Platform-independent German number format: 1000 â†’ "1.000"
        private static readonly NumberFormatInfo GermanNFI = new NumberFormatInfo
        {
            NumberGroupSeparator   = ".",
            NumberDecimalSeparator = ",",
            NumberDecimalDigits    = 0,
            NumberGroupSizes       = new[] { 3 }
        };

        private const float ActiveAlpha        = 1.0f;
        private const float InactiveAlpha      = 0.0f;
        private const float StartHideDelaySecs = 0.15f;
        private const string RaiseButtonLabel  = "RAISE";
        private const string RakeLabelName = "RakeLabel";

        private CanvasGroup _actionPanelGroup;
        private PlayerState _humanPlayer;
        private bool        _gameStarted;
        private Button      _allInButton;
        private ActionAmountBadge _checkCallAmountBadge;
        private ActionAmountBadge _allInAmountBadge;
        private bool              _actionAmountBadgesReady;
        private float             _actionPanelBaseY;
        private bool              _actionPanelBaseCaptured;
        private static TMP_FontAsset _runtimeButtonFont;
        private int                  _revealedCommunityCount;
        private Coroutine            _communityRevealCoroutine;
        private Coroutine            _playersRefreshCoroutine;
        private bool                 _playersRefreshInProgress;
        private Coroutine            _humanHoleRevealCoroutine;
        private Coroutine            _beginTurnCoroutine;
        private Coroutine            _timerCoroutine;
        private PlayerView           _activeTimerView;
        private PlayerView           _humanSeatView;
        private readonly Dictionary<int, int> _previousBets = new Dictionary<int, int>();
        private bool                          _suppressDealerButton;
        private bool                          _collectInProgress;
        private bool                          _preflopDealInProgress;
        private GameObject                    _dealDeckGhostRoot;
        private Canvas               _rootCanvas;

        private bool          _hasPendingActionBadge;
        private bool          _winnerCelebrationActive;
        private Coroutine     _winnerCelebrationCoroutine;
        private bool          _winnerPotChipsCollected;
        private bool          _winnerSplitPotActive;
        private int           _winnerDisplayPotAmount;
        private bool          _lastShowBotCardsForTesting;
        private Transform     _potChipStackHomeParent;
        private int           _potChipStackHomeSiblingIndex;
        private RectTransform _potAmountBadgeRt;
        private TMP_Text      _potAmountBadgeText;

        private const float PotAmountBadgeWidth  = 90f;
        private const float PotAmountBadgeHeight = 30f;
        private const float PotAmountBadgeFontSize = 20f;
        private PlayerState   _pendingBadgePlayer;
        private BettingAction _pendingBadgeAction;
        private int           _pendingBadgeAmount;
        private bool              _raiseInputListenersBound;
        private TMP_InputField    _raiseInputListenerTarget;
        private bool              _suppressRaiseClamp;

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                ApplySceneModePreview();
#endif
            if (Application.isPlaying)
                Instance = this;
        }

        private void Start()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ApplySceneModePreview();
                return;
            }
#endif
            if (!Application.isPlaying)
                return;

            _lastShowBotCardsForTesting = _showBotCardsForTesting;

            if (_tableLayout != null)
                _playerViews = new List<PlayerView>(_tableLayout.GetPlayerViews());

            SyncSeatActionBadgeOffset();
            RepairAllActionBadges();
            ActionBadgeSprites.EnsureLoaded();
            ApplyPlayerAvatars();

            _communityCardSlots = new[] { _flop1, _flop2, _flop3, _turnCard, _riverCard };

            foreach (CardView slot in _communityCardSlots)
                slot?.Hide();

            if (_actionPanel != null)
            {
                _actionPanelGroup = _actionPanel.GetComponent<CanvasGroup>();
                if (_actionPanelGroup == null)
                    _actionPanelGroup = _actionPanel.AddComponent<CanvasGroup>();

                CaptureActionPanelBaseY();
                ApplyActionPanelPosition();
                RebuildActionButtonRowLayout();
            }

            if (_gameManager != null)
                ButtonLabelStyle.RegisterFontSizeProvider(() => _gameManager.ButtonFontSize);

            BindButtonListeners();
            RestoreActionPanelSpriteButtons();
            ApplyStyles();
            ApplyActionButtonSpriteTints();
            _gameStarted = false;
            SetActionPanelVisible(true);
            HideBettingControls();
            SetStartButtonVisible(true);
            HideBettingControls();
            _rootCanvas = ResolveRootCanvas();
            EnsurePotChipStack();
            EnsureBetChipHighDenominations();
            ApplyPotLabelLayout();
            UpdatePotLabel();
            HidePotChipStack();
            SubscribeToGameEvents();
            StartCoroutine(BindOptionsMenuWhenReady());
            HideAllSeatMenus();
            EnsureWinningHandLabel();
            if (_winningHandText != null)
            {
                ApplyWinningHandLabelLayout();
                _winningHandText.gameObject.SetActive(false);
            }

            EnsureRakeLabel();
            HideRakeDisplay();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (OptionsMenu.Instance != null)
            {
                OptionsMenu.Instance.OnOptionsChanged -= OnOptionsMenuChanged;
                OptionsMenu.Instance.OnMenuClosed     -= OnOptionsMenuClosed;
            }
        }

        /// <summary>Backspace / B / Delete — flies pot chips to the winner during the post-hand pause.</summary>
        public bool TryCollectWinnerPot()
        {
            if (!CanCollectPotToWinner())
                return false;

            return BeginCollectPotToWinner();
        }

        /// <summary>Space — allowed after pot chips were collected (or when there is no pot).</summary>
        public bool CanAdvancePastWinnerDismiss() => CanAdvanceAfterWinnerDismiss();

        /// <summary>True while Backspace/B/Delete should collect the center pot to the winner.</summary>
        public bool WinnerPotCollectPending => CanCollectPotToWinner();

        /// <summary>Stops table UI from eating clicks/keys during the post-win pause.</summary>
        public void PrepareTableForWinnerDismissInput()
        {
            if (!Application.isPlaying)
                return;

            HideBettingControls();
            HideAllSeatMenus();
            ClearUiKeyboardFocus();
            SetActionPanelVisible(false);
        }

        private bool CanCollectPotToWinner()
        {
            if (_winnerPotChipsCollected || _winnerDisplayPotAmount <= 0)
                return false;

            if (_gameManager != null && _gameManager.AwaitingWinnerDismiss)
                return true;

            return _winnerCelebrationActive;
        }

        private bool CanAdvanceAfterWinnerDismiss()
        {
            if (_winnerDisplayPotAmount <= 0)
                return true;

            return _winnerPotChipsCollected;
        }

        private IEnumerator BindOptionsMenuWhenReady()
        {
            while (OptionsMenu.Instance == null)
                yield return null;

            OptionsMenu.Instance.OnOptionsChanged += OnOptionsMenuChanged;
            OptionsMenu.Instance.OnMenuClosed     += OnOptionsMenuClosed;

            if (OptionsMenu.Instance.AutoAdvance && !_gameStarted)
                yield return AutoStartGameNextFrame();
        }

        private void OnOptionsMenuChanged()
        {
            if (_gameManager?.Players != null)
                OnPlayersUpdated(_gameManager.Players);
        }

        private void OnOptionsMenuClosed()
        {
            RefreshActiveBotTurnTimer();
        }

        /// <summary>Restarts ring/timer for the active bot seat after bot-think delay changes.</summary>
        private void RefreshActiveBotTurnTimer()
        {
            if (_gameManager == null || _activeTimerView == null)
                return;

            int playerIndex = _playerViews.IndexOf(_activeTimerView);
            if (playerIndex < 0 || playerIndex >= _gameManager.Players.Count)
                return;

            PlayerState player = _gameManager.Players[playerIndex];
            if (player.Type != PlayerType.AI)
                return;

            float duration = _gameManager.AiActionDelay;
            _activeTimerView.SetActiveTurn(true, duration);

            StopTurnTimer();
            _activeTimerView = _playerViews[playerIndex];
            _timerCoroutine  = StartCoroutine(RunTurnTimer(_activeTimerView, duration, isHuman: false));
        }

        private IEnumerator AutoStartGameNextFrame()
        {
            yield return null;
            if (!_gameStarted && _gameManager != null)
                OnStartClicked();
        }

        private void HideAllSeatMenus()
        {
            foreach (PlayerView view in ResolvePlayerViews())
                view?.SeatActionMenu?.Hide();
        }

        private void RepairAllActionBadges()
        {
            foreach (PlayerView view in ResolvePlayerViews())
            {
                if (view == null) continue;
                ActionBadge badge = view.GetComponentInChildren<ActionBadge>(true);
                if (badge != null)
                    ActionBadgeUtility.Repair(badge.gameObject, badge);
            }
        }

        private void SyncSeatActionBadgeOffset()
        {
            PlayerHudLayout.ActionBadgeOffset = _seatActionBadgeOffset;
        }

        private void RefreshVisibleSeatActionBadges()
        {
            SyncSeatActionBadgeOffset();

            foreach (PlayerView view in ResolvePlayerViews())
            {
                if (view == null)
                    continue;

                ActionBadge badge = view.GetComponentInChildren<ActionBadge>(true);
                if (badge == null)
                    continue;

                if (badge.gameObject.activeInHierarchy)
                    badge.RefreshLayout();
                else
                    ActionBadgeUtility.Repair(badge.gameObject, badge);
            }
        }

        /// <summary>Assigns <see cref="_playerAvatars"/> only when a seat has no scene portrait.</summary>
        private void ApplyPlayerAvatars()
        {
            IReadOnlyList<PlayerView> views = ResolvePlayerViews();
            if (views == null || views.Count == 0) return;

            for (int i = 0; i < views.Count; i++)
            {
                PlayerView view = views[i];
                if (view == null)
                    continue;

                if (view.HasSeatAvatar())
                    continue;

                Sprite avatar = (_playerAvatars != null && i < _playerAvatars.Count) ? _playerAvatars[i] : null;
                view.SetAvatar(avatar);
            }
        }

        private IReadOnlyList<PlayerView> ResolvePlayerViews()
        {
            if (_playerViews != null && _playerViews.Count > 0)
                return _playerViews;

            if (_tableLayout != null)
                return _tableLayout.GetPlayerViews();

            return _playerViews;
        }

        private void ResolveAllInButton()
        {
            _allInButton = FindButtonInRow("AllInButton");
        }

        private void EnsureBettingButtonsResolved()
        {
            if (_foldButton == null)      _foldButton      = FindButtonInRow("FoldButton");
            if (_checkCallButton == null) _checkCallButton = FindButtonInRow("CheckCallButton");
            if (_raiseButton == null)     _raiseButton     = FindButtonInRow("RaiseButton");
            if (_allInButton == null)     ResolveAllInButton();
            if (_startButton == null)     _startButton     = FindButtonInRow("StartButton");

            if (_raiseInput == null && _actionPanel != null)
                _raiseInput = _actionPanel.GetComponentInChildren<TMP_InputField>(true);

            if (_raiseInput == null)
            {
                Transform row = GetButtonRowTransform();
                _raiseInput = row != null
                    ? row.GetComponentInChildren<TMP_InputField>(true)
                    : null;
            }
        }

        private void EnsureActionPanelReady()
        {
            if (_actionPanel != null && !_actionPanel.activeSelf)
                _actionPanel.SetActive(true);

            if (_actionPanel != null && _actionPanelGroup == null)
            {
                _actionPanelGroup = _actionPanel.GetComponent<CanvasGroup>();
                if (_actionPanelGroup == null)
                    _actionPanelGroup = _actionPanel.AddComponent<CanvasGroup>();
            }

            Transform row = GetButtonRowTransform();
            if (row != null && !row.gameObject.activeSelf)
                row.gameObject.SetActive(true);

            ActionPanelLayout.ConfigureRowAlignment(row);
            EnsureActionAmountBadges();
            EnsureRaiseColumnAttached();
        }

        private void EnsureActionAmountBadges()
        {
            if (_actionAmountBadgesReady)
                return;

            EnsureBettingButtonsResolved();
            Transform row = GetButtonRowTransform();
            if (row == null)
                return;

            if (_checkCallButton != null)
            {
                Transform column = ActionPanelLayout.EnsureButtonColumn(
                    _checkCallButton, row, ActionPanelLayout.CheckCallColumnName);
                _checkCallAmountBadge = ActionAmountBadge.Ensure(column);

                if (_checkCallLabel == null)
                {
                    TMP_Text label = _checkCallButton.GetComponentInChildren<TMP_Text>(true);
                    if (label != null && label.GetComponentInParent<ActionAmountBadge>() == null)
                        _checkCallLabel = label;
                }
            }

            ResolveAllInButton();
            if (_allInButton != null)
            {
                Transform column = ActionPanelLayout.EnsureButtonColumn(
                    _allInButton, row, ActionPanelLayout.AllInColumnName);
                _allInAmountBadge = ActionAmountBadge.Ensure(column);
            }

            _actionAmountBadgesReady = true;
            ApplyActionAmountBadgeSettings();
        }

        private float ResolveActionBelowSlotHeight(bool reserveBelow)
        {
            if (!reserveBelow)
                return 0f;

            return ActionPanelLayout.BelowButtonSlotHeight(_actionAmountBadgeStyle.badgeHeight);
        }

        private void ApplyActionPanelPosition()
        {
            if (_actionPanel?.transform is not RectTransform rt)
                return;

            CaptureActionPanelBaseY();

            Vector2 pos = rt.anchoredPosition;
            pos.y       = _actionPanelBaseY + _actionPanelYOffset;
            rt.anchoredPosition = pos;
        }

        private void CaptureActionPanelBaseY()
        {
            if (_actionPanelBaseCaptured || _actionPanel?.transform is not RectTransform rt)
                return;

            _actionPanelBaseY       = rt.anchoredPosition.y - _actionPanelYOffset;
            _actionPanelBaseCaptured = true;
        }

        private void ApplyActionButtonSpacing()
        {
            Transform row = GetButtonRowTransform();
            if (row == null || !row.TryGetComponent(out HorizontalLayoutGroup hlg))
                return;

            hlg.spacing = _actionButtonSpacing;
        }

        private void RebuildActionButtonRowLayout()
        {
            ApplyActionButtonSpacing();

            if (GetButtonRowTransform() is RectTransform rowRt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rowRt);

            ApplyActionColumnOffsets();
        }

        private void ApplyActionColumnOffsets()
        {
            Transform row = GetButtonRowTransform();
            if (row == null)
                return;

            ApplyColumnOffset(row, ActionPanelLayout.FoldColumnName, _foldColumnOffset);
            ApplyColumnOffset(row, ActionPanelLayout.CheckCallColumnName, _checkCallColumnOffset);
            ApplyColumnOffset(row, RaiseInputBuilder.RaiseColumnName, _raiseColumnOffset);
            ApplyColumnOffset(row, ActionPanelLayout.AllInColumnName, _allInColumnOffset);
        }

        private static void ApplyColumnOffset(Transform row, string columnName, Vector2 offset)
        {
            if (offset == Vector2.zero)
                return;

            Transform column = row.Find(columnName);
            if (column is not RectTransform rt)
                return;

            rt.anchoredPosition += offset;
        }

        private void ApplyActionAmountBadgeSettings()
        {
            _checkCallAmountBadge?.Configure(_actionAmountBadgeStyle);
            _allInAmountBadge?.Configure(_actionAmountBadgeStyle);
            RefreshActionAmountBadgeLayout();
        }

        private void RefreshActionAmountBadgeLayout()
        {
            if (!Application.isPlaying)
                return;

            float buttonHeight = _gameManager != null ? _gameManager.ButtonHeight : 50f;
            float belowSlot    = ResolveActionBelowSlotHeight(reserveBelow: true);

            if (_checkCallAmountBadge != null && ResolveCheckCallButton()?.transform is RectTransform checkRt)
                _checkCallAmountBadge.ApplyLayout(checkRt.sizeDelta.x, belowSlot);

            if (_allInAmountBadge != null && _allInButton?.transform is RectTransform allInRt)
                _allInAmountBadge.ApplyLayout(allInRt.sizeDelta.x, belowSlot);
        }

        private void BindButtonListeners()
        {
            EnsureBettingButtonsResolved();
            ResolveAllInButton();
            _startButton?.onClick.AddListener(OnStartClicked);
            _foldButton?.onClick.AddListener(OnFoldClicked);
            _checkCallButton?.onClick.AddListener(OnCheckCallClicked);
            _raiseButton?.onClick.AddListener(OnRaiseClicked);
            _allInButton?.onClick.AddListener(OnAllInClicked);
            BindRaiseInputListeners();
        }

        private void BindRaiseInputListeners()
        {
            EnsureBettingButtonsResolved();
            if (_raiseInput == null)
                return;

            RaiseInputBuilder.EnableSelectAllOnFocusAndClick(_raiseInput);
            ApplyRaiseInputLimits();

            if (_raiseInputListenerTarget == _raiseInput)
                return;

            _raiseInput.onSubmit.AddListener(_ => OnRaiseClicked());
            _raiseInput.onSelect.AddListener(_ => SelectAllRaiseInput());
            _raiseInput.onValueChanged.AddListener(OnRaiseInputValueChanged);
            _raiseInput.onEndEdit.AddListener(OnRaiseInputEndEdit);
            _raiseInputListenerTarget = _raiseInput;
            _raiseInputListenersBound = true;
        }

        private void SelectAllRaiseInput()
        {
            RaiseInputBuilder.SelectAllText(_raiseInput);
        }

        /// <summary>Human is always seat index 0 in TableLayoutManager.</summary>
        private PlayerView ResolveHumanSeatView()
        {
            if (_humanSeatView != null)
                return _humanSeatView;

            if (_playerViews != null && _playerViews.Count > 0 && _playerViews[0] != null)
                return _playerViews[0];

            if (_humanPlayer != null && _gameManager != null && _playerViews != null)
            {
                int index = _gameManager.Players.IndexOf(_humanPlayer);
                if (index >= 0 && index < _playerViews.Count)
                    return _playerViews[index];
            }

            return null;
        }

        private void SubscribeToGameEvents()
        {
            if (_gameManager == null)
            {
                Debug.LogError("[UIManager] GameManager reference missing â€” action badges and betting will not work.");
                return;
            }

            _gameManager.OnPhaseChanged.AddListener(OnPhaseChanged);
            _gameManager.OnPlayersUpdated.AddListener(OnPlayersUpdated);
            _gameManager.OnRoundStarting.AddListener(OnRoundStarting);
            _gameManager.OnDealerButtonPlaced.AddListener(OnDealerButtonPlaced);
            _gameManager.OnCommunityCardsUpdated.AddListener(OnCommunityCardsUpdated);
            _gameManager.OnPlayerTurn.AddListener(OnPlayerTurn);
            _gameManager.OnPlayerAction.AddListener(OnPlayerAction);
            _gameManager.OnWinnerDetermined.AddListener(OnWinnerDetermined);
            _gameManager.OnRoundEnded.AddListener(OnRoundEnded);
        }

        private void OnPlayerAction(PlayerState player, BettingAction action, int amount)
        {
            if (_gameManager == null || player == null) return;
            if (_winnerCelebrationActive) return;
            if (!ShouldShowActionBadge(player, action)) return;

            ShowActionBadgeForPlayer(player, action, amount);

            _pendingBadgePlayer    = player;
            _pendingBadgeAction    = action;
            _pendingBadgeAmount    = amount;
            _hasPendingActionBadge = true;
        }

        private static bool ShouldShowActionBadge(PlayerState player, BettingAction action)
            => !player.HasFolded || action == BettingAction.Fold;

        private void ShowActionBadgeForPlayer(PlayerState player, BettingAction action, int amount)
        {
            if (!ShouldShowActionBadge(player, action))
                return;

            PlayerView view = ResolvePlayerView(player);
            if (view == null)
            {
                Debug.LogWarning(
                    $"[UIManager] ActionBadge: no PlayerView for {player.Name} " +
                    $"(Players index {_gameManager.Players.IndexOf(player)}).");
                return;
            }

            float duration = player.Type == PlayerType.AI
                ? ActionBadge.BotDisplayDurationSecs
                : ActionBadge.DisplayDurationSecs;

            view.ShowAction(action, amount, duration);
        }

        /// <summary>Keeps the pending badge above bet chips after HUD refresh without restarting its timer.</summary>
        private void ApplyPendingActionBadge()
        {
            if (_winnerCelebrationActive)
                return;

            if (!_hasPendingActionBadge || _pendingBadgePlayer == null)
                return;

            PlayerView view = ResolvePlayerView(_pendingBadgePlayer);
            if (view != null && ShouldShowActionBadge(_pendingBadgePlayer, _pendingBadgeAction))
                view.BringActionBadgeToFrontIfVisible();

            _hasPendingActionBadge = false;
            _pendingBadgePlayer    = null;
        }

        private PlayerView ResolvePlayerView(PlayerState player)
        {
            if (_gameManager == null || player == null)
                return null;

            IReadOnlyList<PlayerView> views = ResolvePlayerViews();
            if (views == null || views.Count == 0)
                return null;

            IReadOnlyList<PlayerState> players = _gameManager.Players;
            if (players == null || players.Count == 0)
                return null;

            for (int i = 0; i < players.Count && i < views.Count; i++)
            {
                if (players[i] == player && views[i] != null)
                    return views[i];
            }

            for (int i = 0; i < players.Count && i < views.Count; i++)
            {
                if (views[i] != null && players[i].Name == player.Name)
                    return views[i];
            }

            return null;
        }

        private void HideAllActionBadges()
        {
            foreach (PlayerView view in ResolvePlayerViews())
                view?.HideActionBadge();
        }

        private void ClearPendingActionBadge()
        {
            _hasPendingActionBadge = false;
            _pendingBadgePlayer    = null;
        }

        private void OnRoundStarting()
        {
            _winnerCelebrationActive = false;
            _winnerSplitPotActive    = false;
            _preflopDealInProgress   = false;
            _suppressDealerButton = true;
            _tableLayout?.HideDealerButton();
            HideDealDeckGhost();

            StopHumanHoleReveal();
            HideAllActionBadges();

            IReadOnlyList<PlayerView> views = ResolvePlayerViews();
            IReadOnlyList<PlayerState> players = _gameManager?.Players;
            if (views != null && players != null)
            {
                for (int i = 0; i < views.Count && i < players.Count; i++)
                {
                    PlayerView  view   = views[i];
                    PlayerState player = players[i];
                    if (view == null || player == null)
                        continue;

                    view.HideBetDisplay();
                    view.ResetHoleCardReveal();
                    view.SetActiveTurn(false);
                    view.HideActionBadge();
                    view.RefreshHud(player, ResolveBigBlindAmount());

                    if (player.Type == PlayerType.Human)
                        view.SyncHumanHoleCardDisplay(player);
                    else
                        view.RefreshOpponentCards(player);
                }
            }
            else
            {
                foreach (PlayerView view in ResolvePlayerViews())
                {
                    view?.HideBetDisplay();
                    view?.ResetHoleCardReveal();
                    view?.SetActiveTurn(false);
                    view?.HideActionBadge();
                }
            }

            _previousBets.Clear();
            ClearWinningHandDisplay();
            HidePotChipStack();
            UpdatePotLabel();
        }

        private void OnDealerButtonPlaced(int seatIndex)
        {
            _suppressDealerButton = false;

            if (_tableLayout != null && seatIndex >= 0)
                _tableLayout.PlaceDealerButton(seatIndex);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Showdown) RevealAllCards();
            if (phase == GamePhase.RoundOver)
                HideBettingControls();
            if (phase == GamePhase.GameOver)
            {
                HideBettingControls();
                SetActionPanelVisible(false);
                _tableLayout?.HideDealerButton();
            }
        }

        private void OnPlayersUpdated(List<PlayerState> players)
        {
            if (_playersRefreshCoroutine != null)
            {
                StopCoroutine(_playersRefreshCoroutine);
                _playersRefreshCoroutine = null;
                _playersRefreshInProgress = false;
            }

            _playersRefreshCoroutine = StartCoroutine(RefreshPlayers(players));
        }

        private void StopHumanHoleReveal()
        {
            if (_humanHoleRevealCoroutine != null)
            {
                StopCoroutine(_humanHoleRevealCoroutine);
                _humanHoleRevealCoroutine = null;
            }

            _humanSeatView?.CancelHoleCardFlips();
        }

        private void TryScheduleHumanHoleReveal(PlayerView humanView, PlayerState humanState)
        {
            if (humanView == null || humanState == null)
                return;
            if (humanState.HoleCards.Count == 0)
                return;
            if (humanView.RevealedHoleCount >= humanState.HoleCards.Count)
                return;
            if (_humanHoleRevealCoroutine != null)
                return;

            _humanHoleRevealCoroutine = StartCoroutine(HumanHoleRevealRoutine(humanView, humanState));
        }

        private IEnumerator HumanHoleRevealRoutine(PlayerView humanView, PlayerState humanState)
        {
            yield return humanView.RevealHumanHoleCards(humanState);
            _humanHoleRevealCoroutine = null;
        }

        private IEnumerator WaitForTableUiIdle(bool waitForHoleReveal)
        {
            yield return WaitForPlayersRefreshIdle();

            if (waitForHoleReveal)
            {
                while (_humanHoleRevealCoroutine != null)
                    yield return null;
            }
        }

        private IEnumerator WaitForPlayersRefreshIdle()
        {
            while (_playersRefreshInProgress)
                yield return null;
        }

        private void ForceEndPlayersRefresh()
        {
            if (_playersRefreshCoroutine != null)
            {
                StopCoroutine(_playersRefreshCoroutine);
                _playersRefreshCoroutine  = null;
            }

            _playersRefreshInProgress = false;
        }

        private bool ShouldShowBotHoleCards()
            => _showBotCardsForTesting
                || (OptionsMenu.Instance != null && OptionsMenu.Instance.ShowBotCards);

        private void RefreshBotHoleCardVisibilityIfNeeded()
        {
            if (_showBotCardsForTesting == _lastShowBotCardsForTesting)
                return;

            _lastShowBotCardsForTesting = _showBotCardsForTesting;
            if (_gameManager?.Players != null)
                OnPlayersUpdated(_gameManager.Players);
        }

        private IEnumerator RefreshPlayers(List<PlayerState> players)
        {
            _playersRefreshInProgress = true;
            try
            {
                if (players == null)
                    yield break;

                IReadOnlyList<PlayerView> views = ResolvePlayerViews();
                if (views == null || views.Count == 0)
                    yield break;

            _humanPlayer = players.Find(p => p.Type == PlayerType.Human);

            PlayerView humanView = null;
            PlayerState humanState = null;
            bool holeRevealRunning = _humanHoleRevealCoroutine != null;
            bool anyBetIncreased   = false;

            for (int i = 0; i < views.Count && i < players.Count; i++)
            {
                PlayerState player  = players[i];
                PlayerView  view    = views[i];
                bool        isHuman = player.Type == PlayerType.Human;

                if (view == null) continue;

                view.SetIsHuman(isHuman);
                view.RefreshHud(player, ResolveBigBlindAmount());

                if (isHuman)
                {
                    humanView  = view;
                    humanState = player;
                    _humanSeatView = view;
                    if (!_preflopDealInProgress)
                        view.SyncHumanHoleCardDisplay(player, holeRevealRunning);
                }
                else
                {
                    bool showBotCards = ShouldShowBotHoleCards();
                    bool isShowdown   = _gameManager != null
                                        && _gameManager.CurrentPhase == GamePhase.Showdown;
                    if (!_preflopDealInProgress)
                    {
                        if (!player.HasFolded && (isShowdown || showBotCards))
                            view.RevealCards(player);
                        else
                            view.RefreshOpponentCards(player);
                    }
                }

                // Update BetDisplay — animate chip only when the player's bet increases.
                _previousBets.TryGetValue(i, out int prevBet);
                bool betIncreased = player.CurrentBet > prevBet;
                if (betIncreased)
                    anyBetIncreased = true;
                _previousBets[i]  = player.CurrentBet;

                if (_winnerSplitPotActive)
                {
                    if (TryGetWinnerSplitShare(player, out int splitShare) && splitShare > 0)
                        view.ShowBetDisplay(splitShare);
                    else
                        view.HideBetDisplay();
                }
                else if (player.CurrentBet > 0)
                    view.ShowBetDisplay(
                        player.CurrentBet,
                        _animateBetPlace && betIncreased && !_collectInProgress ? view.AvatarRect : null);
                else
                    view.HideBetDisplay();

                if (!player.HasFolded && !player.IsAllIn)
                    view.BringActionBadgeToFrontIfVisible();
            }

            ApplyPendingActionBadge();

            TryScheduleHumanHoleReveal(humanView, humanState);

            UpdatePotLabel();
            if (anyBetIncreased && !_collectInProgress && !_winnerCelebrationActive)
                HidePotChipStack();
            RefreshDealerButton();
            }
            finally
            {
                _playersRefreshInProgress = false;
                _playersRefreshCoroutine  = null;
            }
        }

        private void RefreshDealerButton()
        {
            if (_tableLayout == null || _gameManager == null)
                return;

            if (_suppressDealerButton)
                return;

            if (!_gameStarted
                || _gameManager.CurrentPhase == GamePhase.WaitingToStart
                || _gameManager.CurrentPhase == GamePhase.GameOver)
            {
                _tableLayout.HideDealerButton();
                return;
            }

            int seatIndex = _gameManager.GetDealerSeatIndex();
            if (seatIndex < 0)
                _tableLayout.HideDealerButton();
            else
                _tableLayout.PlaceDealerButton(seatIndex);
        }

        private void OnCommunityCardsUpdated(List<Card> cards)
        {
            if (_communityRevealCoroutine != null)
                StopCoroutine(_communityRevealCoroutine);

            _communityRevealCoroutine = StartCoroutine(RevealCommunityCards(cards));
        }

        private IEnumerator RevealCommunityCards(List<Card> cards)
        {
            if (_communityCardSlots == null)
                yield break;

            if (cards.Count == 0)
            {
                _revealedCommunityCount = 0;
                foreach (CardView slot in _communityCardSlots)
                    slot?.Hide();

                foreach (PlayerView view in _playerViews)
                    view?.ResetHoleCardReveal();

                _communityRevealCoroutine = null;
                yield break;
            }

            int startIndex = _revealedCommunityCount;

            if (startIndex == 0 && cards.Count >= 3)
            {
                yield return AnimateFlopDeal(cards);
            }
            else
            {
                for (int i = startIndex; i < cards.Count; i++)
                {
                    CardView slot = _communityCardSlots[i];
                    if (slot == null)
                        continue;

                    slot.Show(cards[i]);
                    _revealedCommunityCount = i + 1;
                }
            }

            for (int i = cards.Count; i < _communityCardSlots.Length; i++)
                _communityCardSlots[i]?.Hide();

            _communityRevealCoroutine = null;
            yield break;
        }

        private IEnumerator AnimateFlopDeal(List<Card> cards)
        {
            _rootCanvas ??= ResolveRootCanvas();
            if (_rootCanvas == null || _communityCardSlots == null)
            {
                for (int i = 0; i < 3 && i < cards.Count; i++)
                {
                    _communityCardSlots[i]?.Show(cards[i]);
                    _revealedCommunityCount = i + 1;
                }
                yield break;
            }

            if (!TryResolveDealOriginCanvasPosition(out Vector2 dealOrigin))
                dealOrigin = Vector2.zero;

            var canvasRt = (RectTransform)_rootCanvas.transform;

            for (int i = 0; i < 3; i++)
                _communityCardSlots[i]?.Hide();

            for (int i = 0; i < 3; i++)
            {
                CardView slot = _communityCardSlots[i];
                if (slot == null)
                    continue;

                yield return slot.AnimateStraightFlyOnCanvas(
                    canvasRt, dealOrigin, cards[i], _flopCardFlyDuration);

                _revealedCommunityCount = i + 1;

                if (i < 2)
                {
                    float elapsed = 0f;
                    while (elapsed < _flopCardStagger)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
            }

            _revealedCommunityCount = Mathf.Max(_revealedCommunityCount, cards.Count);
        }

        private void OnPlayerTurn(PlayerState player)
        {
            if (_beginTurnCoroutine != null)
            {
                StopCoroutine(_beginTurnCoroutine);
                _beginTurnCoroutine = null;
            }

            _beginTurnCoroutine = StartCoroutine(BeginPlayerTurn(player));
        }

        private IEnumerator BeginPlayerTurn(PlayerState player)
        {
            bool isHumanTurn = player.Type == PlayerType.Human;
            yield return WaitForTableUiIdle(isHumanTurn);

            if (isHumanTurn)
                _humanPlayer = player;

            float duration = TurnTimerDuration(isHumanTurn);

            for (int i = 0; i < _playerViews.Count && i < _gameManager.Players.Count; i++)
            {
                if (_playerViews[i] == null) continue;
                bool isActive = _gameManager.Players[i] == player;
                _playerViews[i].SetActiveTurn(isActive, isActive ? duration : 0f);
            }

            if (isHumanTurn)
                PrepareRaiseInputForTurn();

            UpdateHumanActionButtons(isHumanTurn);

            if (isHumanTurn && CanHumanRaise())
                yield return RaiseInputBuilder.FocusAndSelectAllWhenReady(_raiseInput);

            StopTurnTimer();
            int playerIndex = _gameManager.Players.IndexOf(player);
            if (playerIndex >= 0 && playerIndex < _playerViews.Count && _playerViews[playerIndex] != null)
            {
                _activeTimerView = _playerViews[playerIndex];
                _timerCoroutine  = StartCoroutine(RunTurnTimer(_activeTimerView, duration, isHumanTurn));
            }

            _beginTurnCoroutine = null;
        }

        private float TurnTimerDuration(bool isHumanTurn)
        {
            if (_gameManager == null)
                return 0f;

            return isHumanTurn ? _gameManager.HumanThinkTime : _gameManager.AiActionDelay;
        }

        /// <summary>Returns UI to the initial title state (Start button visible, table cleared).</summary>
        public void ResetToStartScreen()
        {
            if (!Application.isPlaying)
                return;

            StopAllCoroutines();

            _gameStarted          = false;
            _humanPlayer          = null;
            _humanSeatView        = null;
            _revealedCommunityCount = 0;
            _hasPendingActionBadge    = false;
            _winnerCelebrationActive  = false;
            _winnerSplitPotActive     = false;
            _suppressDealerButton     = true;

            _previousBets.Clear();
            StopTurnTimer();
            HideAllActionBadges();
            HideAllSeatMenus();
            HideBettingControls();
            SetActionPanelVisible(true);
            SetStartButtonVisible(true);
            ClearWinningHandDisplay();

            foreach (PlayerView view in ResolvePlayerViews())
            {
                if (view == null) continue;
                view.SetActiveTurn(false);
                view.HideBetDisplay();
                view.HideActionBadge();
                view.ClearTableCards();
            }

            _tableLayout?.HideDealerButton();

            if (_communityCardSlots != null)
            {
                foreach (CardView slot in _communityCardSlots)
                    slot?.Hide();
            }

            UpdatePotLabel();
            HidePotChipStack();
        }

        private void OnRoundEnded()
        {
            StopTurnTimer();
            HideBettingControls();
            ClearWinnerCelebration();

            foreach (PlayerView view in _playerViews)
                view?.HideBetDisplay();
            _previousBets.Clear();

            if (_openMenuBetweenHands && OptionsMenu.Instance != null)
                OptionsMenu.Instance.Open();
        }

        private void OnStartClicked()
        {
            _gameStarted = true;
            _gameManager.StartGame();
            StartCoroutine(HideStartButtonAfterPress());
        }

        private IEnumerator HideStartButtonAfterPress()
        {
            yield return new WaitForSeconds(StartHideDelaySecs);
            SetStartButtonVisible(false);
            ApplyButtonRowSize();
        }

        private void SetStartButtonVisible(bool visible)
        {
            if (!Application.isPlaying || _startButton == null) return;
            _startButton.gameObject.SetActive(visible);
            _startButton.interactable = visible;
        }

        private void OnFoldClicked() => SubmitAction(BettingAction.Fold);

        private void OnAllInClicked() => SubmitAction(BettingAction.AllIn);

        private void OnRaiseClicked()
        {
            int raiseAmount = ParseRaiseIncrement();
            if (raiseAmount <= 0) return;
            SubmitAction(BettingAction.Raise, raiseAmount);
        }

        private void OnCheckCallClicked()
        {
            if (_humanPlayer == null) return;
            int callAmount = _gameManager.CurrentBet - _humanPlayer.CurrentBet;
            SubmitAction(callAmount <= 0 ? BettingAction.Check : BettingAction.Call);
        }

        private void UpdateCheckCallLabel()
        {
            if (_humanPlayer == null || _gameManager == null) return;

            int callAmount = _gameManager.CurrentBet - _humanPlayer.CurrentBet;
            bool isCheck   = callAmount <= 0;
            string action  = isCheck ? "CHECK" : "CALL";

            TMP_Text label = _checkCallLabel;
            if (label == null && _checkCallButton != null)
            {
                label = _checkCallButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null && label.GetComponentInParent<ActionAmountBadge>() != null)
                    label = null;
            }

            if (label != null)
            {
                label.text = action;
                StylePanelLabel(label, TextCheck);
            }

            if (_checkCallAmountBadge != null)
            {
                if (isCheck)
                    _checkCallAmountBadge.Hide();
                else
                    _checkCallAmountBadge.SetAmount(callAmount);
            }
        }

        private void UpdateAllInLabel()
        {
            if (_humanPlayer == null || _allInButton == null) return;

            TMP_Text label = _allInButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.GetComponentInParent<ActionAmountBadge>() == null)
            {
                label.text = "ALL IN";
                StylePanelLabel(label, TextAllIn);
            }

            _allInAmountBadge?.SetAmount(_humanPlayer.Chips);
        }

        private void UpdateRaiseButtonLabel()
        {
            if (_raiseButton == null) return;

            TMP_Text label = _raiseButton.GetComponentInChildren<TMP_Text>();
            if (label == null) return;

            label.text = RaiseButtonLabel;
            StylePanelLabel(label, TextRaise);
        }

        private void PrepareRaiseInputForTurn()
        {
            EnsureBettingButtonsResolved();
            if (_raiseInput == null) return;

            if (!CanHumanRaise())
            {
                _raiseInput.text = string.Empty;
                return;
            }

            int minTotal = GetMinRaiseTotal();

            _raiseInput.text = minTotal.ToString();
            UpdateRaiseInputPlaceholder();
            ApplyRaiseInputLimits();
            StyleRaiseInput();
            RaiseInputBuilder.ResetRaiseInputEntryState(_raiseInput, _raiseInput.text);
        }

        private void UpdateRaiseInput(bool preserveTypedValue = true)
        {
            if (_humanPlayer == null || _gameManager == null) return;

            bool canRaise = CanHumanRaise();
            int minTotal  = GetMinRaiseTotal();

            if (_raiseInput == null) return;

            UpdateRaiseInputPlaceholder();

            if (!canRaise)
            {
                _raiseInput.text = string.Empty;
                return;
            }

            if (!preserveTypedValue || string.IsNullOrWhiteSpace(_raiseInput.text))
                _raiseInput.text = minTotal.ToString();

            ApplyRaiseInputLimits();
            StyleRaiseInput();
            RaiseInputBuilder.ResetRaiseInputEntryState(_raiseInput, _raiseInput.text);
        }

        private void ApplyRaiseInputLimits()
        {
            if (_raiseInput == null)
                return;

            _raiseInput.characterLimit = 0;

            if (!_raiseInput.isFocused)
                NormalizeRaiseInputText();
        }

        private void OnRaiseInputValueChanged(string _)
        {
            if (_suppressRaiseClamp)
                return;

            NormalizeRaiseInputText();
        }

        private void OnRaiseInputEndEdit(string _)
        {
            if (_suppressRaiseClamp)
                return;

            NormalizeRaiseInputText();
        }

        private void NormalizeRaiseInputText()
        {
            if (_raiseInput == null)
                return;

            string raw = _raiseInput.text;
            if (string.IsNullOrEmpty(raw))
                return;

            string normalized = StripLeadingZeros(raw);

            if (int.TryParse(normalized, out int value))
            {
                int maxTotal = GetMaxRaiseTotal();
                if (maxTotal > 0 && value > maxTotal)
                    normalized = string.Empty;
            }

            if (normalized == raw)
                return;

            _suppressRaiseClamp = true;
            _raiseInput.SetTextWithoutNotify(normalized);
            _raiseInput.caretPosition  = normalized.Length;
            _raiseInput.stringPosition = normalized.Length;
            _suppressRaiseClamp = false;
            RaiseInputBuilder.ResetRaiseInputEntryState(_raiseInput, normalized);
        }

        private static string StripLeadingZeros(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            int i = 0;
            while (i < raw.Length - 1 && raw[i] == '0')
                i++;

            return raw.Substring(i);
        }

        private void UpdateRaiseInputPlaceholder()
        {
            if (_raiseInput?.placeholder is not TMP_Text placeholder)
                return;

            placeholder.text = string.Empty;
        }

        private static string FormatEuroAmount(int amount) =>
            amount.ToString("N0", GermanNFI);

        private int GetCallAmount()
        {
            if (_humanPlayer == null || _gameManager == null) return 0;
            return _gameManager.GetCallAmountFor(_humanPlayer);
        }

        private int GetMinRaiseIncrement() =>
            _gameManager != null ? _gameManager.GetMinRaiseIncrement() : 40;

        private int ResolveBigBlindAmount() =>
            _gameManager != null ? _gameManager.BigBlindAmount : 0;

        private int GetMaxRaiseIncrement()
        {
            if (_humanPlayer == null || _gameManager == null) return 0;
            return _gameManager.GetMaxRaiseIncrement(_humanPlayer);
        }

        /// <summary>Minimum total chips to put in via raise (call + min raise increment).</summary>
        private int GetMinRaiseTotal() => GetCallAmount() + GetMinRaiseIncrement();

        /// <summary>Maximum total chips to put in via raise (full stack).</summary>
        private int GetMaxRaiseTotal() => _humanPlayer?.Chips ?? 0;

        private int ParseRaiseIncrement()
        {
            if (_humanPlayer == null || _gameManager == null) return 0;

            int minIncrement = GetMinRaiseIncrement();
            int maxIncrement = GetMaxRaiseIncrement();
            if (maxIncrement < minIncrement) return 0;

            int minTotal = GetMinRaiseTotal();
            int maxTotal = GetMaxRaiseTotal();
            int callAmount = GetCallAmount();

            NormalizeRaiseInputText();

            string raw = _raiseInput != null ? _raiseInput.text : string.Empty;

            if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw, out int totalIn))
                return 0;

            totalIn = Mathf.Clamp(totalIn, minTotal, maxTotal);

            if (_raiseInput != null)
            {
                _raiseInput.text = totalIn.ToString();
                RaiseInputBuilder.ResetRaiseInputEntryState(_raiseInput, _raiseInput.text);
            }

            return totalIn - callAmount;
        }

        /// <summary>Updates the pot number only â€” used while a betting street is in progress.</summary>
        private void ApplyPotLabelLayout()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                ApplyPotAreaEditorPreview();
#endif
            LayoutPotArea();
        }

#if UNITY_EDITOR
        private void ApplyPotAreaEditorPreview()
        {
            if (_potText == null)
                return;

            EnsurePotChipStack();
            if (_potChipStack == null)
                return;

            ApplyPotChipStackSettings();
            if (string.IsNullOrWhiteSpace(_potText.text))
                _potText.text = "Pot: 1.250";

            _potChipStack.SetExactAmount(1250);
            _potChipStack.StackRoot.gameObject.SetActive(true);
        }
#endif

        /// <summary>Center X of the full five community-card row (flop through river), even when turn/river are hidden.</summary>
        private float ResolveCommunityBoardCenterX()
        {
            RectTransform firstRt = CommunityCardSlotRect(_flop1);
            RectTransform lastRt  = CommunityCardSlotRect(_riverCard);
            if (firstRt == null || lastRt == null)
                return 0f;

            RectTransform rowRoot = firstRt.parent as RectTransform;
            float rowX            = rowRoot != null ? rowRoot.anchoredPosition.x : 0f;
            return rowX + (firstRt.anchoredPosition.x + lastRt.anchoredPosition.x) * 0.5f;
        }

        private static RectTransform CommunityCardSlotRect(CardView card)
            => card != null ? card.transform as RectTransform : null;

        private float ResolvePotLabelCenterY(RectTransform potRt, float targetBottomY)
        {
            Bounds textBounds = _potText.textBounds;
            if (textBounds.size.y > 0f)
                return targetBottomY - textBounds.min.y;

            return targetBottomY + _potText.preferredHeight * 0.5f;
        }

        private void LayoutPotArea()
        {
            if (_potText == null || _potText.transform is not RectTransform potRt)
                return;

            potRt.anchorMin = new Vector2(0.5f, 0.5f);
            potRt.anchorMax = new Vector2(0.5f, 0.5f);
            potRt.pivot     = new Vector2(0.5f, 0.5f);

            float boardCenterX = ResolveCommunityBoardCenterX() + _potLabelX;
            _potText.ForceMeshUpdate();
            float textWidth = _potText.preferredWidth;

            bool showStack = _potChipStack != null
                             && _potChipStack.StackRoot.gameObject.activeSelf;

            float stackWidth = 0f;
            if (showStack)
            {
                RectTransform stackRt = _potChipStack.StackRoot;
                stackWidth = stackRt.rect.width > 0f ? stackRt.rect.width : stackRt.sizeDelta.x;
            }

            float potCenterX;
            float stackCenterX;
            if (!showStack || stackWidth <= 0f)
            {
                potCenterX   = boardCenterX;
                stackCenterX = boardCenterX;
            }
            else
            {
                float groupWidth = textWidth + _potChipPadding + stackWidth;
                float groupLeft  = boardCenterX - groupWidth * 0.5f;
                potCenterX   = groupLeft + textWidth * 0.5f;
                stackCenterX = groupLeft + textWidth + _potChipPadding + stackWidth * 0.5f;
            }

            float labelCenterY = ResolvePotLabelCenterY(potRt, _potBottomY);
            potRt.anchoredPosition = new Vector2(potCenterX, labelCenterY);

            if (_potChipStack == null)
                return;

            if (!IsPotChipStackAtHome())
                return;

            RectTransform stackRoot = _potChipStack.StackRoot;
            stackRoot.anchorMin = new Vector2(0.5f, 0.5f);
            stackRoot.anchorMax = new Vector2(0.5f, 0.5f);
            stackRoot.pivot     = new Vector2(0.5f, 0.5f);
            float chipBottomLocal = showStack ? _potChipStack.GetBottomLocalY() : 0f;
            stackRoot.anchoredPosition = new Vector2(
                stackCenterX,
                _potChipBottomY - chipBottomLocal);
        }

        private void UpdatePotLabel()
        {
            if (_potText == null || _gameManager == null)
                return;

            if (_winnerCelebrationActive)
                return;

            int pot = _gameManager.PotAmount;
            _potText.text = "Pot: " + pot.ToString("N0", GermanNFI);

            if (_potChipStack == null || !IsPotChipStackAtHome())
                return;

            bool stackActive = _potChipStack.StackRoot.gameObject.activeSelf;
            if (!stackActive)
                return;

            if (pot <= 0)
            {
                HidePotChipStack();
                return;
            }

            _potChipStack.SetExactAmount(pot);
            PositionPotChipStack(showStack: true);
        }

        private Transform ResolvePotChipStackHomeParent()
            => _potText != null ? _potText.transform.parent : null;

        private bool IsPotChipStackAtHome()
        {
            if (_potChipStack == null)
                return true;

            Transform expectedHome = _potChipStackHomeParent ?? ResolvePotChipStackHomeParent();
            if (expectedHome == null)
                return true;

            return _potChipStack.StackRoot.parent == expectedHome;
        }

        private void ResetPotChipStackTransform()
        {
            if (_potChipStack == null)
                return;

            RectTransform stackRt = _potChipStack.StackRoot;
            stackRt.localScale    = Vector3.one;
            stackRt.localRotation = Quaternion.identity;
        }

        /// <summary>Hides the central pot chip stack (label unchanged).</summary>
        private void HidePotChipStack()
        {
            if (_potChipStack == null)
                EnsurePotChipStack();
            if (_potChipStack == null)
                return;

            _potChipStack.Clear();
            ResetPotChipStackTransform();
            _potChipStack.StackRoot.gameObject.SetActive(false);
            HidePotAmountBadge();
            ResetPotCollectVisualAlpha();
            LayoutPotArea();
        }

        /// <summary>Shows the pot chip breakdown â€” after street bets are collected into the pot.</summary>
        private void ShowPotChipStack(int potAmount = -1)
        {
            if (_potText == null || _gameManager == null)
                return;

            if (potAmount < 0)
                potAmount = _gameManager.PotAmount;

            if (potAmount <= 0)
            {
                HidePotChipStack();
                return;
            }

            if (_potChipStack == null)
                EnsurePotChipStack();
            if (_potChipStack == null)
                return;

            ApplyPotChipStackSettings();
            _potChipStack.SetExactAmount(potAmount);
            PositionPotChipStack(showStack: true);
        }

        private void ApplyPotChipStackSettings()
        {
            if (_potChipStack == null)
                return;

            _potChipStack.SetStackOverlapY(_potStackOverlapY);

            if (_potChipColumnGapX > 0f)
                _potChipStack.SetColumnGapX(_potChipColumnGapX);
            else
                _potChipStack.ClearColumnGapOverride();
        }

        private void EnsurePotChipStack()
        {
            if (_potChipStack == null)
            {
                if (_potText == null)
                    return;

                Transform parent = _potText.transform.parent;
                if (parent == null)
                    return;

                Transform existing = parent.Find("PotChipStack");
                GameObject stackGo;
                if (existing != null)
                {
                    stackGo = existing.gameObject;
                }
                else
                {
                    stackGo = new GameObject("PotChipStack", typeof(RectTransform), typeof(ChipStackView));
                    var stackRt = (RectTransform)stackGo.transform;
                    stackRt.SetParent(parent, false);
                    stackRt.anchorMin        = new Vector2(0.5f, 0.5f);
                    stackRt.anchorMax        = new Vector2(0.5f, 0.5f);
                    stackRt.pivot            = new Vector2(0.5f, 0.5f);
                    stackRt.anchoredPosition = ((RectTransform)_potText.transform).anchoredPosition;
                    stackRt.sizeDelta        = new Vector2(ChipStackView.MaxLayoutWidth, ChipStackView.MaxLayoutHeight);

                    for (int i = 0; i < 3; i++)
                    {
                        var chipGo = new GameObject(
                            $"Chip_{i}",
                            typeof(RectTransform),
                            typeof(CanvasRenderer),
                            typeof(Image));
                        chipGo.transform.SetParent(stackGo.transform, false);
                        var chipRt = (RectTransform)chipGo.transform;
                        chipRt.sizeDelta = new Vector2(ChipStackView.ResolveChipDisplaySize(), ChipStackView.ResolveChipDisplaySize());
                        var img = chipGo.GetComponent<Image>();
                        img.raycastTarget  = false;
                        img.preserveAspect = true;
                        chipGo.SetActive(false);
                    }
                }

                _potChipStack = stackGo.GetComponent<ChipStackView>();
                if (_potChipStack == null)
                    return;

                ChipStackView sample = FindSampleBetChipStack();
                if (sample != null)
                    _potChipStack.CopySpritesFrom(sample);

                _potChipStack.AssignHighDenominations(_potChipSprite100, _potChipSprite500);
                ApplyPotChipStackSettings();
                _potChipStack.Clear();
                stackGo.SetActive(false);
            }

            EnsurePotAmountBadge();
        }

        private float ResolvePotBadgeGap()
            => _tableLayout != null ? _tableLayout.BetChipBadgeGap : 3f;

        private void EnsurePotAmountBadge()
        {
            if (_potChipStack == null)
                return;

            RectTransform stackRt = _potChipStack.StackRoot;
            Transform existing    = null;

            for (int i = stackRt.childCount - 1; i >= 0; i--)
            {
                Transform child = stackRt.GetChild(i);
                if (child.name != "PotAmountBadge")
                    continue;

                if (existing == null)
                    existing = child;
                else if (Application.isPlaying)
                    Destroy(child.gameObject);
#if UNITY_EDITOR
                else
                    DestroyImmediate(child.gameObject);
#endif
            }

            if (existing != null)
            {
                _potAmountBadgeRt   = (RectTransform)existing;
                _potAmountBadgeText = FindDirectChildIncludingInactive(existing, "AmountText")
                    ?.GetComponent<TMP_Text>();
                ApplyPotAmountBadgeStyle(_potAmountBadgeText);
                return;
            }

            var badgeGo = new GameObject(
                "PotAmountBadge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            badgeGo.transform.SetParent(stackRt, false);

            var badgeImg = badgeGo.GetComponent<Image>();
            Sprite sprite = BetDisplay.ResolveAmountBadgeSprite();
            badgeImg.sprite         = sprite;
            badgeImg.type           = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            badgeImg.color          = BetDisplay.DefaultAmountBadgeColor;
            badgeImg.raycastTarget  = false;

            var textGo = new GameObject("AmountText", typeof(RectTransform));
            textGo.transform.SetParent(badgeGo.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var amountText = textGo.AddComponent<TextMeshProUGUI>();
            ApplyPotAmountBadgeStyle(amountText);

            _potAmountBadgeRt   = (RectTransform)badgeGo.transform;
            _potAmountBadgeText = amountText;
            badgeGo.SetActive(false);
        }

        private static Transform FindDirectChildIncludingInactive(Transform parent, string name)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                    return child;
            }

            return null;
        }

        private static void ApplyPotAmountBadgeStyle(TMP_Text text)
        {
            if (text == null)
                return;

            text.alignment          = TextAlignmentOptions.Center;
            text.fontStyle          = FontStyles.Bold;
            text.enableWordWrapping = false;
            text.overflowMode       = TextOverflowModes.Ellipsis;
            text.raycastTarget      = false;
            text.color              = UiColors.PotGold;
            text.fontSize           = PotAmountBadgeFontSize;
            PlayerHudLayout.ApplyStackAmountFontIfMissing(text);
        }

        private void LayoutPotAmountBadge()
        {
            if (_potAmountBadgeRt == null || _potChipStack == null)
                return;

            RectTransform stackRt = _potChipStack.StackRoot;
            float stackHeight = stackRt.sizeDelta.y > 0f ? stackRt.sizeDelta.y : stackRt.rect.height;
            if (stackHeight <= 0f)
                stackHeight = ChipStackView.MaxLayoutHeight;

            float badgeGap     = ResolvePotBadgeGap();
            float badgeCenterY = -(stackHeight + badgeGap) * 0.5f;

            _potAmountBadgeRt.anchorMin        = new Vector2(0.5f, 0.5f);
            _potAmountBadgeRt.anchorMax        = new Vector2(0.5f, 0.5f);
            _potAmountBadgeRt.pivot            = new Vector2(0.5f, 0.5f);
            _potAmountBadgeRt.anchoredPosition = new Vector2(0f, badgeCenterY);
            _potAmountBadgeRt.sizeDelta        = new Vector2(PotAmountBadgeWidth, PotAmountBadgeHeight);
        }

        private void ShowPotAmountBadge(int amount)
        {
            if (amount <= 0)
            {
                HidePotAmountBadge();
                return;
            }

            EnsurePotAmountBadge();
            LayoutPotAmountBadge();

            if (_potAmountBadgeRt != null)
            {
                _potAmountBadgeRt.SetAsLastSibling();
                _potAmountBadgeRt.gameObject.SetActive(true);
            }

            if (_potAmountBadgeText != null)
            {
                _potAmountBadgeText.text = amount.ToString("N0", GermanNFI);
                _potAmountBadgeText.ForceMeshUpdate();
            }
        }

        private void HidePotAmountBadge()
        {
            if (_potAmountBadgeRt != null)
                _potAmountBadgeRt.gameObject.SetActive(false);
        }

        private static Vector2 CanvasPointFromRect(RectTransform canvasRt, RectTransform target)
        {
            if (canvasRt == null || target == null)
                return Vector2.zero;

            return canvasRt.InverseTransformPoint(target.TransformPoint(Vector3.zero));
        }

        private void SetPotCollectVisualAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);

            if (_potChipStack != null)
            {
                foreach (Image img in _potChipStack.GetComponentsInChildren<Image>(true))
                {
                    if (img == null)
                        continue;

                    Color c = img.color;
                    img.color = new Color(c.r, c.g, c.b, alpha);
                }
            }

            if (_potAmountBadgeRt != null)
            {
                Image badgeImg = _potAmountBadgeRt.GetComponent<Image>();
                if (badgeImg != null)
                {
                    Color c = badgeImg.color;
                    badgeImg.color = new Color(c.r, c.g, c.b, alpha);
                }
            }

            if (_potAmountBadgeText != null)
            {
                Color c = _potAmountBadgeText.color;
                _potAmountBadgeText.color = new Color(c.r, c.g, c.b, alpha);
            }
        }

        private void ResetPotCollectVisualAlpha()
        {
            SetPotCollectVisualAlpha(1f);
        }

        /// <summary>Seat bet stacks use the same 100/500 sprites as the pot chip stack.</summary>
        private void EnsureBetChipHighDenominations()
        {
            if (_potChipSprite100 == null && _potChipSprite500 == null)
                return;

            foreach (PlayerView view in ResolvePlayerViews())
            {
                if (view == null)
                    continue;

                ChipStackView stack = view.GetComponentInChildren<ChipStackView>(true);
                stack?.AssignHighDenominations(_potChipSprite100, _potChipSprite500);
            }
        }

        private ChipStackView FindSampleBetChipStack()
        {
            foreach (PlayerView view in ResolvePlayerViews())
            {
                if (view == null) continue;
                ChipStackView stack = view.GetComponentInChildren<ChipStackView>(true);
                if (stack != null)
                    return stack;
            }

            return null;
        }

        private void PositionPotChipStack(bool showStack)
        {
            if (_potChipStack == null || _potText == null)
                return;

            RectTransform stackRt = _potChipStack.StackRoot;
            bool show = showStack
                        && _gameManager != null
                        && (_gameManager.PotAmount > 0
                            || (_winnerCelebrationActive && _winnerDisplayPotAmount > 0));

            if (show)
                ResetPotChipStackTransform();

            stackRt.gameObject.SetActive(show);
            LayoutPotArea();
        }

        /// <summary>
        /// Clears seat bet stacks and refreshes the pot HUD after a betting street.
        /// Called between betting streets (preflopâ†’flop, flopâ†’turn, turnâ†’river).
        /// </summary>
        public IEnumerator CollectStreetBetsToPot()
        {
            if (!Application.isPlaying || _gameManager == null)
                yield break;

            if (_playersRefreshCoroutine != null)
            {
                StopCoroutine(_playersRefreshCoroutine);
                _playersRefreshCoroutine  = null;
                _playersRefreshInProgress = false;
            }

            IReadOnlyList<PlayerView> views = ResolvePlayerViews();
            if (views == null)
                yield break;

            if (_animateStreetCollect)
            {
                _rootCanvas ??= ResolveRootCanvas();
                EnsurePotChipStack();

                if (TryResolvePotCollectCanvasPosition(out Vector2 potCanvasPos))
                {
                    List<PlayerState> players = _gameManager.Players;
                    if (players != null)
                    {
                        var routines = new List<IEnumerator>();
                        var viewsToHide = new List<PlayerView>();

                        for (int i = 0; i < players.Count && i < views.Count; i++)
                        {
                            PlayerState player = players[i];
                            if (player.CurrentBet <= 0)
                                continue;

                            PlayerView view = views[i];
                            if (view == null)
                                continue;

                            BetDisplay bet = view.GetComponentInChildren<BetDisplay>(true);
                            if (bet == null || !bet.HasVisibleBet)
                                continue;

                            routines.Add(bet.AnimateBetStackToPot(potCanvasPos, _collectFlyDuration));
                            viewsToHide.Add(view);
                        }

                        if (routines.Count > 0)
                        {
                            _collectInProgress = true;
                            yield return RunParallel(routines);

                            _previousBets.Clear();
                            ApplyPotCenterDisplay();

                            foreach (PlayerView view in viewsToHide)
                                view.HideBetDisplay();

                            _collectInProgress = false;

                            if (_collectPotUpdateDelay > 0f)
                                yield return new WaitForSecondsRealtime(_collectPotUpdateDelay);
                        }
                    }
                }
            }

            FinishStreetBetCollect(views);
        }

        private void FinishStreetBetCollect(IReadOnlyList<PlayerView> views)
        {
            foreach (PlayerView view in views)
                view?.HideBetDisplay();

            _previousBets.Clear();
            ApplyPotCenterDisplay();
        }

        /// <summary>Shows pot label and chip stack together to avoid a label-only flash.</summary>
        private void ApplyPotCenterDisplay()
        {
            if (_potText == null || _gameManager == null)
                return;

            int pot = _gameManager.PotAmount;
            if (pot <= 0)
            {
                _potText.text = string.Empty;
                HidePotChipStack();
                return;
            }

            ShowPotChipStack(pot);
            _potText.text = "Pot: " + pot.ToString("N0", GermanNFI);
            LayoutPotArea();
        }

        private RectTransform ResolvePotCollectTarget()
        {
            if (_potChipStack != null)
                return _potChipStack.StackRoot;

            return _potText != null ? (RectTransform)_potText.transform : null;
        }

        private bool TryResolvePotCollectCanvasPosition(out Vector2 canvasAnchoredPosition)
        {
            canvasAnchoredPosition = Vector2.zero;

            _rootCanvas ??= ResolveRootCanvas();
            RectTransform potTarget = ResolvePotCollectTarget();
            if (_rootCanvas == null || potTarget == null)
                return false;

            canvasAnchoredPosition = CanvasPointFromRect(
                (RectTransform)_rootCanvas.transform, potTarget);
            return true;
        }

        /// <summary>Flies one hole card from the table centre to a seat (preflop deal animation).</summary>
        public IEnumerator AnimateHoleCardDeal(int seatIndex, int slotIndex, Card card, bool faceUp)
        {
            if (!_animatePreflopDeal || card == null)
                yield break;

            _rootCanvas ??= ResolveRootCanvas();
            if (_rootCanvas == null)
                yield break;

            IReadOnlyList<PlayerView> views = ResolvePlayerViews();
            if (seatIndex < 0 || seatIndex >= views.Count)
                yield break;

            PlayerView view = views[seatIndex];
            if (view == null)
                yield break;

            var canvasRt = (RectTransform)_rootCanvas.transform;
            if (!TryResolveDealOriginCanvasPosition(out Vector2 dealOrigin))
                dealOrigin = Vector2.zero;

            yield return view.AnimateHoleCardDeal(
                slotIndex,
                card,
                faceUp,
                canvasRt,
                dealOrigin,
                _holeCardFlyDuration,
                _holeCardSmearLength,
                _holeCardSmearAlpha,
                _holeCardSmearSpawnInterval,
                _holeCardSmearFadeDuration);
        }

        public void BeginPreflopDealAnimation()
        {
            _preflopDealInProgress = true;
            ShowDealDeckGhost();
        }

        public void EndPreflopDealAnimation()
        {
            _preflopDealInProgress = false;
            HideDealDeckGhost();
        }

        private void ShowDealDeckGhost()
        {
            if (!_showDealDeckGhost || !_animatePreflopDeal)
                return;

            HideDealDeckGhost();

            _rootCanvas ??= ResolveRootCanvas();
            if (_rootCanvas == null || !TryResolveDealOriginCanvasPosition(out Vector2 origin))
                return;

            if (!TryResolveDealCardVisuals(out Sprite cardBack, out Vector2 cardSize))
                return;

            var canvasRt = (RectTransform)_rootCanvas.transform;
            _dealDeckGhostRoot = new GameObject("DealDeckGhost", typeof(RectTransform));
            var rootRt = (RectTransform)_dealDeckGhostRoot.transform;
            rootRt.SetParent(canvasRt, false);
            rootRt.anchorMin        = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax        = new Vector2(0.5f, 0.5f);
            rootRt.pivot            = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = origin;
            rootRt.localScale       = Vector3.one;

            int count = Mathf.Clamp(_dealDeckGhostCount, 1, 5);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"DeckOutline_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(rootRt, false);
                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.sizeDelta        = cardSize;
                rt.anchoredPosition = _dealDeckGhostStackOffset * i;

                var img = go.GetComponent<Image>();
                img.sprite        = cardBack;
                img.raycastTarget = false;
                img.color         = new Color(1f, 1f, 1f, _dealDeckGhostAlpha);

                var outline = go.AddComponent<Outline>();
                outline.effectColor    = new Color(1f, 1f, 1f, Mathf.Clamp01(_dealDeckGhostAlpha + 0.25f));
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                outline.useGraphicAlpha = true;
            }
        }

        private void HideDealDeckGhost()
        {
            if (_dealDeckGhostRoot == null)
                return;

            Destroy(_dealDeckGhostRoot);
            _dealDeckGhostRoot = null;
        }

        private bool TryResolveDealCardVisuals(out Sprite cardBack, out Vector2 cardSize)
        {
            cardBack = null;
            cardSize = Vector2.zero;

            CardView reference = _flop1;
            if (reference == null)
            {
                IReadOnlyList<PlayerView> views = ResolvePlayerViews();
                if (views != null)
                {
                    foreach (PlayerView view in views)
                    {
                        reference = view?.GetHoleCardSlot(0);
                        if (reference != null)
                            break;
                    }
                }
            }

            if (reference == null)
                return false;

            cardBack = reference.CardBackSprite;
            cardSize = reference.GetDisplaySize();
            return cardBack != null && cardSize.sqrMagnitude > 0f;
        }

        private bool TryResolveDealOriginCanvasPosition(out Vector2 canvasAnchoredPosition)
        {
            canvasAnchoredPosition = Vector2.zero;

            _rootCanvas ??= ResolveRootCanvas();
            if (_rootCanvas == null)
                return false;

            var canvasRt = (RectTransform)_rootCanvas.transform;

            if (_dealOriginAnchor != null)
            {
                canvasAnchoredPosition = CanvasPointFromRect(canvasRt, _dealOriginAnchor);
                return true;
            }

            RectTransform flopRt = CommunityCardSlotRect(_flop1);
            if (flopRt == null)
                return false;

            canvasAnchoredPosition = CanvasPointFromRect(canvasRt, flopRt) + _dealOriginCanvasOffset;
            return true;
        }

        private IEnumerator RunParallel(List<IEnumerator> routines)
        {
            int remaining = routines.Count;
            foreach (IEnumerator routine in routines)
                StartCoroutine(RunParallelRoutine(routine, () => remaining--));

            while (remaining > 0)
                yield return null;
        }

        private static IEnumerator RunParallelRoutine(IEnumerator routine, System.Action onComplete)
        {
            if (routine != null)
                yield return routine;

            onComplete?.Invoke();
        }

        private void SubmitAction(BettingAction action, int raiseAmount = 0)
        {
            if (_gameManager == null)
                return;

            StopTurnTimer();
            HideBettingControls();

            _gameManager.SubmitPlayerAction(action, raiseAmount);
        }

        /// <summary>Stops any running turn timer coroutine.</summary>
        private void StopTurnTimer()
        {
            if (_timerCoroutine != null)
            {
                StopCoroutine(_timerCoroutine);
                _timerCoroutine = null;
            }

            _activeTimerView = null;
        }

        /// <summary>
        /// Waits for the turn duration, then auto-submits Check (if free) or Fold for the human.
        /// Visual countdown is shown by the gold avatar ring via SetActiveTurn().
        /// </summary>
        private IEnumerator RunTurnTimer(PlayerView view, float duration, bool isHuman)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (OptionsMenu.Instance == null || !OptionsMenu.Instance.IsOpen)
                    elapsed += Time.deltaTime;
                yield return null;
            }

            _timerCoroutine  = null;
            _activeTimerView = null;

            if (isHuman && _gameManager != null)
            {
                while (OptionsMenu.Instance != null && OptionsMenu.Instance.IsOpen)
                    yield return null;
                bool canCheck     = _humanPlayer != null
                                    && (_gameManager.CurrentBet - _humanPlayer.CurrentBet) <= 0;
                BettingAction autoAction = canCheck ? BettingAction.Check : BettingAction.Fold;
                HideBettingControls();
                _gameManager.SubmitPlayerAction(autoAction);
            }
        }

        private Transform GetButtonRowTransform()
        {
            if (_actionPanel != null)
            {
                Transform row = _actionPanel.transform.Find("ButtonRow");
                if (row != null) return row;
            }

            return GameObject.Find("ButtonRow")?.transform;
        }

        /// <summary>Activates every direct child of ButtonRow (Scene layout preview).</summary>
        private void ShowAllButtonRowChildren()
        {
            Transform row = GetButtonRowTransform();
            if (row == null) return;

            foreach (Transform child in row)
                child.gameObject.SetActive(true);
        }

        private Button FindButtonInRow(string buttonName)
        {
            Transform row = GetButtonRowTransform();
            if (row == null)
                return null;

            Transform direct = row.Find(buttonName);
            if (direct != null)
                return direct.GetComponent<Button>();

            foreach (Transform child in row)
            {
                Transform nested = child.Find(buttonName);
                if (nested != null)
                    return nested.GetComponent<Button>();
            }

            return null;
        }

        private Button ResolveCheckCallButton() =>
            _checkCallButton != null ? _checkCallButton : FindButtonInRow("CheckCallButton");

        private void ApplyActionButtonSpriteTints()
        {
            Image foldImage = _foldButton != null
                ? _foldButton.GetComponent<Image>()
                : FindButtonInRow("FoldButton")?.GetComponent<Image>();
            if (foldImage != null)
                foldImage.color = ActionColors.FoldRed;

            Image checkCallImage = ResolveCheckCallButton()?.GetComponent<Image>();
            if (checkCallImage != null)
                checkCallImage.color = ActionColors.CheckCallGreen;
        }

        private void SetActionPanelVisible(bool visible)
        {
            if (!Application.isPlaying || _actionPanelGroup == null) return;
            _actionPanelGroup.alpha          = visible ? ActiveAlpha : InactiveAlpha;
            _actionPanelGroup.blocksRaycasts = visible;
            _actionPanelGroup.interactable   = true;
        }

        private void SetActionButtonVisible(Button button, bool visible)
        {
            if (!Application.isPlaying || button == null) return;

            button.gameObject.SetActive(visible);

            Transform column = button.transform.parent;
            if (column != null && ActionPanelLayout.IsButtonColumn(column.name))
                column.gameObject.SetActive(visible);
        }

        private void HideSeatMenuOnly()
        {
            _humanSeatView?.SetSeatMenuHudMode(false);
            _humanSeatView?.SeatActionMenu?.Hide();
        }

        private void HideBettingControls()
        {
            if (!Application.isPlaying) return;

            if (_raiseInput != null && _raiseInput.isFocused)
                _raiseInput.DeactivateInputField();

            SetActionButtonVisible(_foldButton, false);
            SetActionButtonVisible(ResolveCheckCallButton(), false);
            SetActionButtonVisible(_raiseButton, false);
            ResolveAllInButton();
            SetActionButtonVisible(_allInButton, false);
            SetRaiseInputVisible(false);
            SetAllBettingInteractable(false);
            HideSeatMenuOnly();
        }

        private void ShowBottomBettingControls(bool canCheck, bool canCall, bool canRaise, bool canAllIn)
        {
            EnsureBettingButtonsResolved();
            EnsureActionPanelReady();
            SetActionPanelVisible(true);

            SetActionButtonVisible(_foldButton, true);
            SetActionButtonVisible(ResolveCheckCallButton(), canCheck || canCall);
            SetActionButtonVisible(_raiseButton, canRaise);
            ResolveAllInButton();
            SetActionButtonVisible(_allInButton, canAllIn);
            SetRaiseInputVisible(canRaise);

            if (_foldButton != null)
                _foldButton.interactable = true;

            Button checkCall = ResolveCheckCallButton();
            if (checkCall != null)
                checkCall.interactable = canCheck || canCall;

            if (_raiseButton != null)
                _raiseButton.interactable = canRaise;

            if (_raiseInput != null)
                EnableRaiseInputForKeyboard(canRaise);

            if (_allInButton != null)
                _allInButton.interactable = canAllIn;

            if (canCheck || canCall)
                UpdateCheckCallLabel();

            if (canAllIn)
                UpdateAllInLabel();

            if (canRaise)
            {
                UpdateRaiseButtonLabel();
                UpdateRaiseInput(preserveTypedValue: true);
                StyleRaiseInput();
            }

            EnsureRaiseInputLayout();
            FitBettingButtonWidths(canCheck, canCall, canRaise, canAllIn);
            SyncBettingRowBottomAlignment(canCheck, canCall, canRaise, canAllIn);

            if (canRaise)
                UpdateRaiseButtonLabel();

            RebuildActionButtonRowLayout();
        }

        private void SyncBettingRowBottomAlignment(bool canCheck, bool canCall, bool canRaise, bool canAllIn)
        {
            float buttonHeight = _gameManager != null ? _gameManager.ButtonHeight : 50f;
            bool reserveBelow  = canCall || canRaise || canAllIn;
            float belowSlot    = ResolveActionBelowSlotHeight(reserveBelow);

            ApplyActionAmountBadgeSettings();

            Transform row = GetButtonRowTransform();
            ActionPanelLayout.ConfigureRowAlignment(row);
            if (row == null)
                return;

            if (_foldButton != null && _foldButton.gameObject.activeSelf)
            {
                ActionPanelLayout.EnsurePlainButtonColumn(_foldButton, row, ActionPanelLayout.FoldColumnName);
                float width = _foldButton.transform is RectTransform foldRt ? foldRt.sizeDelta.x : 0f;
                ActionPanelLayout.SyncPlainButtonColumn(_foldButton, buttonHeight, belowSlot, width);
            }

            if (canCheck || canCall)
            {
                float width = ResolveCheckCallButton()?.transform is RectTransform rt ? rt.sizeDelta.x : 0f;
                ActionPanelLayout.SyncAmountBadgeColumn(
                    ResolveCheckCallButton(), _checkCallAmountBadge, canCall, buttonHeight, belowSlot, width);
            }

            ResolveAllInButton();
            if (_allInButton != null && _allInButton.gameObject.activeSelf && canAllIn)
            {
                float width = _allInButton.transform is RectTransform rt ? rt.sizeDelta.x : 0f;
                ActionPanelLayout.SyncAmountBadgeColumn(
                    _allInButton, _allInAmountBadge, badgeVisible: true, buttonHeight, belowSlot, width);
            }

            if (_raiseButton != null && _raiseButton.gameObject.activeSelf && canRaise)
            {
                float width = _raiseButton.transform is RectTransform rt ? rt.sizeDelta.x : 0f;
                ActionPanelLayout.SyncRaiseColumn(
                    _raiseButton, _raiseInput, inputVisible: true, buttonHeight, belowSlot, width);
            }
        }

        private void FitBettingButtonWidths(bool canCheck, bool canCall, bool canRaise, bool canAllIn)
        {
            if (_gameManager == null)
                return;

            Transform row = GetButtonRowTransform();
            if (row == null)
                return;

            var sizer = row.GetComponent<ButtonRowFontSize>();
            if (sizer == null)
                return;

            var buttons = new List<Button>(4);
            if (_foldButton != null && _foldButton.gameObject.activeSelf)
                buttons.Add(_foldButton);

            Button checkCall = ResolveCheckCallButton();
            if (checkCall != null && checkCall.gameObject.activeSelf && (canCheck || canCall))
                buttons.Add(checkCall);

            ResolveAllInButton();
            if (_allInButton != null && _allInButton.gameObject.activeSelf && canAllIn)
                buttons.Add(_allInButton);

            if (_raiseButton != null && _raiseButton.gameObject.activeSelf && canRaise)
                buttons.Add(_raiseButton);

            sizer.FitActiveButtons(
                _gameManager.ButtonWidth,
                _gameManager.ButtonHeight,
                _gameManager.ButtonFontSize,
                buttons);

            if (_raiseButton != null && _raiseInput != null)
            {
                bool reserveBelow = canCall || canRaise || canAllIn;
                float belowSlot   = ResolveActionBelowSlotHeight(reserveBelow);
                float width       = _raiseButton.transform is RectTransform rt ? rt.sizeDelta.x : 0f;
                ActionPanelLayout.SyncRaiseColumn(
                    _raiseButton,
                    _raiseInput,
                    canRaise,
                    _gameManager.ButtonHeight,
                    belowSlot,
                    width);
            }
        }

        /// <summary>Shows valid actions on the bottom action panel during the human turn.</summary>
        private void UpdateHumanActionButtons(bool isHumanTurn)
        {
            if (!Application.isPlaying) return;

            if (_winnerCelebrationActive)
            {
                HideBettingControls();
                return;
            }

            if (!isHumanTurn || !_gameStarted || _humanPlayer == null || _humanPlayer.HasFolded || _humanPlayer.IsAllIn)
            {
                HideBettingControls();
                return;
            }

            int callAmount = _gameManager.CurrentBet - _humanPlayer.CurrentBet;
            bool canCheck  = callAmount <= 0;
            bool canCall   = callAmount > 0 && _humanPlayer.Chips > 0;
            bool canRaise  = CanHumanRaise();
            bool canAllIn  = _humanPlayer.Chips > 0;

            _humanSeatView = ResolveHumanSeatView();
            _humanSeatView?.HideActionBadge();
            HideSeatMenuOnly();

            SetStartButtonVisible(false);
            ShowBottomBettingControls(canCheck, canCall, canRaise, canAllIn);
        }

        private void EnableRaiseInputForKeyboard(bool enabled)
        {
            if (_raiseInput == null) return;

            _raiseInput.interactable = enabled;
            _raiseInput.readOnly     = !enabled;
        }

        private void SetAllBettingInteractable(bool interactable)
        {
            if (_foldButton != null)      _foldButton.interactable      = interactable;
            if (_checkCallButton != null) _checkCallButton.interactable = interactable;
            if (_raiseButton != null)     _raiseButton.interactable     = interactable;
            if (_raiseInput != null)
                EnableRaiseInputForKeyboard(interactable);

            if (_allInButton != null)     _allInButton.interactable     = interactable;
        }

        private void SetRaiseInputVisible(bool visible)
        {
            if (!Application.isPlaying)
                return;

            EnsureBettingButtonsResolved();
            if (_raiseInput == null || _raiseButton == null)
                return;

            EnsureRaiseColumnAttached();
            ActionPanelLayout.HideLegacyRaiseRow(_actionPanel != null ? _actionPanel.transform : null);

            _raiseInput.gameObject.SetActive(visible);

            float buttonHeight = _gameManager != null ? _gameManager.ButtonHeight : 50f;
            float width        = _raiseButton.transform is RectTransform rt ? rt.sizeDelta.x : 0f;
            float belowSlot    = visible ? ResolveActionBelowSlotHeight(reserveBelow: true) : 0f;
            ActionPanelLayout.SyncRaiseColumn(_raiseButton, _raiseInput, visible, buttonHeight, belowSlot, width);

            if (visible)
                EnsureRaiseInputLayout();
        }

        private void EnsureRaiseInputLayout()
        {
            if (_raiseInput == null || _raiseButton == null)
                return;

            RaiseInputBuilder.ApplyButtonBackground(_raiseInput, _raiseButton);

            float buttonHeight = _gameManager != null ? _gameManager.ButtonHeight : 50f;
            bool visible       = _raiseInput.gameObject.activeSelf;
            float width        = _raiseButton.transform is RectTransform rt ? rt.sizeDelta.x : 0f;
            float belowSlot    = visible ? ResolveActionBelowSlotHeight(reserveBelow: true) : 0f;
            ActionPanelLayout.SyncRaiseColumn(_raiseButton, _raiseInput, visible, buttonHeight, belowSlot, width);
            StyleRaiseInput();

            if (_actionPanel != null)
                ActionPanelLayout.RebuildPanel((RectTransform)_actionPanel.transform);
        }

        private bool CanHumanRaise()
        {
            if (_humanPlayer == null || _gameManager == null) return false;
            return _gameManager.CanPlayerRaise(_humanPlayer);
        }

        private void RevealAllCards()
        {
            for (int i = 0; i < _playerViews.Count && i < _gameManager.Players.Count; i++)
            {
                PlayerState player = _gameManager.Players[i];
                if (!player.HasFolded)
                    _playerViews[i].RevealCards(player);
            }
        }

        private void ApplyStyles()
        {
            float cardGap = _gameManager != null ? _gameManager.CardGap : 16f;
            _tableLayout?.SetCardLayout(120f, cardGap);

            TMP_FontAsset font = ResolveButtonFont();
            if (!IsUsableButtonFont(font))
                Debug.LogWarning("[UIManager] Casino3D SDF unavailable â€” button labels will use scene defaults.");

            _buttonFont = font;
            _raiseInput = ActionPanelLayout.Apply(
                _actionPanel,
                _startButton,
                _foldButton,
                _checkCallButton,
                _raiseButton,
                font);

            if (IsUsableButtonFont(font))
            {
                float fontSize = _gameManager != null ? _gameManager.ButtonFontSize : ButtonLabelStyle.ActionButtonFontSize;
                StyleActionButton(_startButton,     TextStart, fontSize);
                StyleActionButton(_foldButton,      TextFold,  fontSize);
                StyleActionButton(ResolveCheckCallButton(), TextCheck, fontSize);
                ResolveAllInButton();
                StyleActionButton(_allInButton,     TextAllIn, fontSize);
                StyleActionButton(_raiseButton,     TextRaise, fontSize);
                StylePanelLabel(_checkCallLabel,  TextCheck);
                StyleRaiseInput(font);
                UpdateRaiseButtonLabel();
            }

            ApplyButtonRowSize();
            EnsureRaiseColumnAttached();
        }

        private void EnsureRaiseColumnAttached()
        {
            if (_raiseButton == null || _actionPanel == null)
                return;

            _raiseInput = ActionPanelLayout.AttachRaiseInputToButton(
                _raiseButton,
                _actionPanel.transform,
                _buttonFont != null ? _buttonFont : ResolveButtonFont());

            StyleRaiseInput();

            BindRaiseInputListeners();
            Transform row = GetButtonRowTransform();
            ActionPanelLayout.ConfigureRowAlignment(row);
            ActionPanelLayout.HideLegacyRaiseRow(_actionPanel.transform);
        }

        private void HideLegacyRaiseRow()
        {
            if (_actionPanel == null)
                return;

            ActionPanelLayout.HideLegacyRaiseRow(_actionPanel.transform);
        }

        private float ResolveActionButtonFontSize() =>
            _gameManager != null ? _gameManager.ButtonFontSize : ButtonLabelStyle.ActionButtonFontSize;

        private void StyleRaiseInput(TMP_FontAsset font = null)
        {
            if (_raiseInput == null)
                return;

            font ??= _buttonFont ?? ResolveButtonFont();
            if (!IsUsableButtonFont(font))
                return;

            if (_raiseButton != null)
                RaiseInputBuilder.ApplyButtonBackground(_raiseInput, _raiseButton);

            float fontSize = ResolveActionButtonFontSize();
            float width    = _raiseButton != null && _raiseButton.transform is RectTransform rt
                ? rt.sizeDelta.x
                : 0f;

            RaiseInputBuilder.ApplyTextStyle(_raiseInput, font, fontSize);
            RaiseInputBuilder.NormalizeInputLayout(_raiseInput, width, fontSize);
            RaiseInputBuilder.EnableSelectAllOnFocusAndClick(_raiseInput);
        }

        private TMP_FontAsset ResolveButtonFont()
        {
            if (IsUsableButtonFont(_runtimeButtonFont))
                return _runtimeButtonFont;

            TMP_FontAsset resourcesFont = Resources.Load<TMP_FontAsset>(Casino3DSdfResourcesPath);
            if (IsUsableButtonFont(resourcesFont))
            {
                _runtimeButtonFont = resourcesFont;
                return _runtimeButtonFont;
            }

            if (IsUsableButtonFont(_buttonFont))
                return _buttonFont;

#if UNITY_EDITOR
            TMP_FontAsset editorFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Casino3DSdfPath);
            if (IsUsableButtonFont(editorFont))
            {
                _buttonFont = editorFont;
                return _buttonFont;
            }
#endif

            Font source = Resources.Load<Font>("Fonts/Casino3D");
            if (source == null)
                return null;

            _runtimeButtonFont = TMP_FontAsset.CreateFontAsset(
                source,
                samplingPointSize: 72,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic);

            _runtimeButtonFont.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");
            return IsUsableButtonFont(_runtimeButtonFont) ? _runtimeButtonFont : null;
        }

        private static bool IsUsableButtonFont(TMP_FontAsset fontAsset)
        {
            return fontAsset != null
                && fontAsset.characterTable != null
                && fontAsset.characterTable.Count > 0;
        }

        private void StyleActionButton(Button button, Color textColor, float fontSize = 0f)
        {
            if (fontSize <= 0f) fontSize = ButtonLabelStyle.ActionButtonFontSize;
            if (button == null) return;

            ActionBadgeUtility.RestoreSpriteButton(button);

            Image img = button.GetComponent<Image>();
            if (img != null)
            {
                img.enabled          = true;
                img.type             = Image.Type.Simple;
                button.transition    = Selectable.Transition.SpriteSwap;
                button.targetGraphic = img;
            }

            var colors = button.colors;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
                StylePanelLabel(label, textColor, fontSize);

            if (button.GetComponent<ButtonHoverFix>() == null)
                button.gameObject.AddComponent<ButtonHoverFix>();
        }

        /// <summary>Bottom action row uses textured sprite buttons â€” not seat <see cref="ActionBadge"/> SDF pills.</summary>
        private void RestoreActionPanelSpriteButtons()
        {
            EnsureBettingButtonsResolved();
            ResolveAllInButton();

            RestoreSpriteButton(_startButton);
            RestoreSpriteButton(_foldButton);
            RestoreSpriteButton(ResolveCheckCallButton());
            RestoreSpriteButton(_raiseButton);
            RestoreSpriteButton(_allInButton);
        }

        private static void RestoreSpriteButton(Button button) =>
            ActionBadgeUtility.RestoreSpriteButton(button);

        private void StylePanelLabel(TMP_Text label, Color textColor, float fontSize = 0f)
        {
            if (fontSize <= 0f) fontSize = ButtonLabelStyle.ActionButtonFontSize;
            if (label == null) return;

            TMP_FontAsset font = _buttonFont ?? ResolveButtonFont();
            if (!IsUsableButtonFont(font))
                return;

            label.font = font;
            ButtonLabelStyle.Apply(label, textColor, fontSize);
        }

#if UNITY_EDITOR
        /// <summary>Restores action-panel buttons for Scene view after Play mode or on scene load.</summary>
        public void ApplySceneModePreview()
        {
            if (Application.isPlaying) return;

            TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Casino3DSdfPath);
            if (IsUsableButtonFont(font))
                _buttonFont = font;

            if (_actionPanel != null)
            {
                var group = _actionPanel.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha          = ActiveAlpha;
                    group.blocksRaycasts = true;
                    group.interactable   = true;
                }
            }

            RestoreSceneModeButtonState();
            EnsureRaiseColumnAttached();
            ApplyButtonRowSize();
            ApplyActionButtonLabelsInEditor();
            EnsureCheckCallSceneVisibility();
            ApplyPlayerAvatars();
            ApplyPotLabelLayout();
            ApplyCommunityCardsEditorLayout();
            Canvas.ForceUpdateCanvases();
        }

        private void ApplyCommunityCardsEditorLayout()
        {
#if UNITY_2022_2_OR_NEWER
            TableLayoutManager layout = FindFirstObjectByType<TableLayoutManager>(FindObjectsInactive.Include);
#else
            TableLayoutManager layout = FindObjectOfType<TableLayoutManager>(true);
#endif
            layout?.ApplyLayout();
        }

        /// <summary>Scene view only â€” keeps every ButtonRow child active and fully opaque.</summary>
        private void RestoreSceneModeButtonState()
        {
            if (_actionPanel != null && !_actionPanel.activeSelf)
                _actionPanel.SetActive(true);

            ShowAllButtonRowChildren();

            Transform row = GetButtonRowTransform();
            if (row == null) return;

            foreach (Button btn in row.GetComponentsInChildren<Button>(includeInactive: true))
            {
                btn.gameObject.SetActive(true);
                btn.interactable = true;
                ActionBadgeUtility.RestoreSpriteButton(btn);

                if (btn.targetGraphic != null)
                    btn.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
            }
        }

        /// <summary>Scene view: tint check/call button so GreenNormal reads on the felt.</summary>
        private void EnsureCheckCallSceneVisibility()
        {
            Button check = ResolveCheckCallButton();
            if (check == null) return;

            check.gameObject.SetActive(true);
            ApplyActionButtonSpriteTints();

            TMP_Text label = check.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;

            label.gameObject.SetActive(true);
            if (string.IsNullOrWhiteSpace(label.text))
                label.text = "CHECK";
        }

        private void OnValidate()
        {
            SyncSeatActionBadgeOffset();
            ApplyActionPanelPosition();
            RebuildActionButtonRowLayout();
            ApplyPotLabelLayout();

            if (Application.isPlaying)
            {
                if (_potChipStack == null && _potText != null)
                    EnsurePotChipStack();
                ApplyPotChipStackSettings();
                UpdatePotLabel();
                ApplyActionAmountBadgeSettings();
                EnsureWinningHandLabel();
                ApplyWinningHandLabelLayout();
                EnsureRakeLabel();
                StyleRakeLabel();
                RefreshVisibleSeatActionBadges();
                RefreshBotHoleCardVisibilityIfNeeded();
                return;
            }

            ApplySceneModePreview();
            RepairAllActionBadges();
        }

        [ContextMenu("Apply Copyright Label To TMP")]
        private void EditorApplyCopyrightLabelToTmp()
        {
            if (Application.isPlaying)
                return;

            TMP_Text text = _copyrightLabelText;
            if (text == null)
            {
                GameObject labelGo = GameObject.Find("CopyrightLabel");
                if (labelGo != null)
                    text = labelGo.GetComponent<TMP_Text>();
            }

            if (text == null)
                return;

            text.text = _copyrightLabel ?? string.Empty;
            UnityEditor.EditorUtility.SetDirty(text);
        }

        private void ApplyActionButtonLabelsInEditor()
        {
            TMP_FontAsset font = _buttonFont ?? ResolveButtonFont();
            if (!IsUsableButtonFont(font)) return;

            float fontSize = _gameManager != null ? _gameManager.ButtonFontSize : ButtonLabelStyle.ActionButtonFontSize;
            StyleActionButton(_startButton,     TextStart, fontSize);
            StyleActionButton(_foldButton,      TextFold,  fontSize);
            StyleActionButton(ResolveCheckCallButton(), TextCheck, fontSize);
            StyleActionButton(FindButtonInRow("AllInButton"), TextAllIn, fontSize);
            StyleActionButton(_raiseButton,     TextRaise, fontSize);
            if (_raiseButton != null)
            {
                TMP_Text raiseLabel = _raiseButton.GetComponentInChildren<TMP_Text>(true);
                if (raiseLabel != null)
                {
                    raiseLabel.text = RaiseButtonLabel;
                    StylePanelLabel(raiseLabel, TextRaise, fontSize);
                }
            }

            StylePanelLabel(_checkCallLabel, TextCheck);
        }
#endif

        /// <summary>Applies GameManager button row settings via ButtonRowFontSize.</summary>
        public void ApplyButtonRowSize()
        {
            if (_actionPanel == null) return;
            _actionPanel.transform.Find("ButtonRow")?.GetComponent<ButtonRowFontSize>()?.Apply();
            RebuildActionButtonRowLayout();
            StyleRaiseInput();
        }

        // â”€â”€ Winner Celebration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void DismissBettingUiForWinnerCelebration()
        {
            StopTurnTimer();

            if (_beginTurnCoroutine != null)
            {
                StopCoroutine(_beginTurnCoroutine);
                _beginTurnCoroutine = null;
            }

            HideBettingControls();
            HideAllSeatMenus();
            ClearUiKeyboardFocus();
            SetActionPanelVisible(false);

            foreach (PlayerView view in ResolvePlayerViews())
                view?.SetActiveTurn(false);
        }

        private void ClearUiKeyboardFocus()
        {
            if (_raiseInput != null && _raiseInput.isFocused)
                _raiseInput.DeactivateInputField();

            foreach (PlayerView view in ResolvePlayerViews())
            {
                if (view == null)
                    continue;

                foreach (TMP_InputField input in view.GetComponentsInChildren<TMP_InputField>(true))
                {
                    if (input != null && input.isFocused)
                        input.DeactivateInputField();
                }
            }

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void OnWinnerDetermined(PlayerState winner)
        {
            if (_gameManager == null)
                return;

            IReadOnlyList<PlayerView> views = ResolvePlayerViews();
            if (views == null || views.Count == 0)
                return;

            IReadOnlyList<PlayerState> winners = _gameManager.LastRoundWinners;
            if (winners == null || winners.Count == 0)
            {
                if (winner == null) return;
                winners = new List<PlayerState> { winner };
            }

            _winnerCelebrationActive = true;
            ClearPendingActionBadge();
            HideAllActionBadges();
            DismissBettingUiForWinnerCelebration();

            float duration = _gameManager.RoundEndPauseSecs;
            int winnerCount = winners.Count > 0 ? winners.Count : 1;
            bool splitPot = winnerCount >= 2;

            ShowWinningHandDisplay();
            ShowRakeDisplay(_gameManager.LastRakeDisplayText);

            _winnerSplitPotActive    = splitPot;
            _winnerPotChipsCollected = false;

            if (splitPot)
            {
                _winnerDisplayPotAmount = 0;
                HidePotChipStack();
                HidePotAmountBadge();
                if (_potText != null)
                    _potText.text = string.Empty;
            }
            else
            {
                _winnerDisplayPotAmount = _gameManager.LastGrossPot;
                ShowPotDisplayForWinner(_winnerDisplayPotAmount);
            }

            int bigBlind = ResolveBigBlindAmount();
            for (int i = 0; i < winners.Count; i++)
            {
                PlayerState roundWinner = winners[i];
                PlayerView winnerView = ResolvePlayerView(roundWinner);
                if (winnerView == null)
                    continue;

                int winnerNetShare = PotAward.ShareForWinnerIndex(
                    _gameManager.LastPotAwarded, winnerCount, i);

                winnerView.RefreshHud(roundWinner, bigBlind);
                winnerView.StartWinnerHighlight(winnerNetShare, duration);
                TryStartWinnerAvatarPulse(winnerView);

                if (splitPot && winnerNetShare > 0)
                    winnerView.ShowBetDisplay(winnerNetShare);
            }

            if (splitPot)
                CompleteInstantSplitPotAward(bigBlind, winners);
            else if (_winnerDisplayPotAmount <= 0)
            {
                _gameManager.ApplyPendingPotAward();
                _winnerPotChipsCollected = true;
            }

            _winnerCelebrationCoroutine = StartCoroutine(RunWinnerCelebrationPrepare());
        }

        private bool TryGetWinnerSplitShare(PlayerState player, out int share)
        {
            share = 0;
            if (!_winnerSplitPotActive || _gameManager == null || player == null)
                return false;

            IReadOnlyList<PlayerState> winners = _gameManager.LastRoundWinners;
            if (winners == null || winners.Count == 0)
                return false;

            for (int i = 0; i < winners.Count; i++)
            {
                if (winners[i] != player)
                    continue;

                share = PotAward.ShareForWinnerIndex(
                    _gameManager.LastPotAwarded, winners.Count, i);
                return true;
            }

            return false;
        }

        private void CompleteInstantSplitPotAward(int bigBlind, IReadOnlyList<PlayerState> winners)
        {
            _gameManager.ApplyPendingPotAward();

            if (winners != null)
            {
                foreach (PlayerState winner in winners)
                {
                    if (winner == null)
                        continue;

                    ResolvePlayerView(winner)?.RefreshHud(winner, bigBlind);
                }
            }

            _winnerPotChipsCollected  = true;
            _winnerDisplayPotAmount   = 0;
            HidePotChipStack();
            HidePotAmountBadge();

            if (_potText != null)
                _potText.text = string.Empty;
        }

        private void TryStartWinnerAvatarPulse(PlayerView winnerView)
        {
            if (winnerView == null || _winnerAvatarPulseCount <= 0)
                return;

            winnerView.StartCoroutine(winnerView.PulseAvatarImage(
                _winnerAvatarPulseCount,
                _winnerAvatarPulseScale,
                _winnerAvatarPulseHalfDuration));
        }

        private readonly struct WinnerCollectLeg
        {
            public readonly PlayerView View;
            public readonly int        NetShare;

            public WinnerCollectLeg(PlayerView view, int netShare)
            {
                View     = view;
                NetShare = netShare;
            }
        }

        private List<WinnerCollectLeg> ResolveWinnerCollectLegs()
        {
            var legs = new List<WinnerCollectLeg>();
            if (_gameManager == null)
                return legs;

            IReadOnlyList<PlayerState> winners = _gameManager.LastRoundWinners;
            if (winners == null || winners.Count == 0)
                return legs;

            int winnerCount = winners.Count;
            int netPot      = _gameManager.LastPotAwarded;

            for (int i = 0; i < winners.Count; i++)
            {
                PlayerState winner = winners[i];
                if (winner == null)
                    continue;

                PlayerView view = ResolvePlayerView(winner);
                if (view == null)
                    continue;

                int share = PotAward.ShareForWinnerIndex(netPot, winnerCount, i);
                if (share <= 0)
                    continue;

                legs.Add(new WinnerCollectLeg(view, share));
            }

            return legs;
        }

        private void ShowPotDisplayForWinner(int potAmount)
        {
            if (potAmount <= 0)
                return;

            if (_potText != null)
                _potText.text = "Pot: " + potAmount.ToString("N0", GermanNFI);

            RestorePotChipStackHome();
            ShowPotChipStack(potAmount);
        }

        /// <summary>Shrinks the center pot stack and label to the net amount (after rake).</summary>
        private void ApplyNetPotToCenterDisplay(int netAmount)
        {
            if (netAmount <= 0)
            {
                if (_potText != null)
                    _potText.text = string.Empty;
                HidePotChipStack();
                return;
            }

            if (_potText != null)
                _potText.text = "Pot: " + netAmount.ToString("N0", GermanNFI);

            if (_potChipStack == null)
                EnsurePotChipStack();
            if (_potChipStack == null)
                return;

            ApplyPotChipStackSettings();
            _potChipStack.SetExactAmount(netAmount);
            PositionPotChipStack(showStack: true);
        }

        private bool BeginCollectPotToWinner()
        {
            if (_winnerPotChipsCollected || _winnerSplitPotActive)
                return false;

            ClearWinningCardHighlights();
            HideRakeDisplay();

            ForceEndPlayersRefresh();
            ClearUiKeyboardFocus();

            List<WinnerCollectLeg> legs = ResolveWinnerCollectLegs();
            if (legs.Count == 0)
            {
                ApplyNetPotToCenterDisplay(0);
                CompleteWinnerPotCollection();
                return true;
            }

            if (_winnerCelebrationCoroutine != null)
            {
                StopCoroutine(_winnerCelebrationCoroutine);
                _winnerCelebrationCoroutine = null;
            }

            _winnerCelebrationCoroutine = StartCoroutine(AnimatePotChipsToAllWinners(legs));
            return true;
        }

        private void ClearWinnerCelebration()
        {
            if (_winnerCelebrationCoroutine != null)
            {
                StopCoroutine(_winnerCelebrationCoroutine);
                _winnerCelebrationCoroutine = null;
            }

            _gameManager?.ApplyPendingPotAward();

            _winnerCelebrationActive  = false;
            _winnerPotChipsCollected    = false;
            _winnerSplitPotActive       = false;
            _winnerDisplayPotAmount     = 0;
            ClearWinningHandDisplay();
            RestorePotChipStackHome();
            HidePotChipStack();
            DestroyFlyingWinChips();

            foreach (PlayerView view in ResolvePlayerViews())
                view?.SetActiveTurn(false);
        }

        private void DestroyFlyingWinChips()
        {
            if (_rootCanvas == null)
                return;

            Transform root = _rootCanvas.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.name == "_WinChip")
                    Destroy(child.gameObject);
            }
        }

        private void RestorePotChipStackHome()
        {
            if (_potChipStack == null)
                return;

            Transform home = _potChipStackHomeParent ?? ResolvePotChipStackHomeParent();
            if (home == null)
                return;

            RectTransform stackRt = _potChipStack.StackRoot;
            if (stackRt.parent != home)
            {
                stackRt.SetParent(home, false);
                if (_potChipStackHomeParent != null)
                    stackRt.SetSiblingIndex(_potChipStackHomeSiblingIndex);
            }

            _potChipStackHomeParent       = null;
            _potChipStackHomeSiblingIndex = 0;
            ResetPotChipStackTransform();
        }

        private IEnumerator RunWinnerCelebrationPrepare()
        {
            yield return WaitForPlayersRefreshIdle();
            _winnerCelebrationCoroutine = null;
        }

        private IEnumerator AnimatePotChipsToAllWinners(List<WinnerCollectLeg> legs)
        {
            for (int i = 0; i < legs.Count; i++)
            {
                WinnerCollectLeg leg = legs[i];
                yield return AnimatePotChipsToWinnerLeg(leg.View, leg.NetShare);
            }

            CompleteWinnerPotCollection();
        }

        private bool TryResolveWinnerCollectCanvasPosition(
            PlayerView winnerView, RectTransform canvasRt, out Vector2 canvasPosition)
        {
            canvasPosition = Vector2.zero;
            if (winnerView == null || canvasRt == null)
                return false;

            RectTransform target = winnerView.AvatarRect
                                   ?? winnerView.ChipsHudRect
                                   ?? winnerView.BetAnchorRect;
            if (target == null)
                return false;

            canvasPosition = CanvasPointFromRect(canvasRt, target);
            return true;
        }

        private static void AttachStackToCanvasForFly(
            RectTransform stackRt, RectTransform canvasRt, out Vector2 canvasAnchoredPosition)
        {
            canvasAnchoredPosition = canvasRt.InverseTransformPoint(stackRt.position);
            stackRt.SetParent(canvasRt, false);
            stackRt.anchorMin        = new Vector2(0.5f, 0.5f);
            stackRt.anchorMax        = new Vector2(0.5f, 0.5f);
            stackRt.pivot            = new Vector2(0.5f, 0.5f);
            stackRt.anchoredPosition = canvasAnchoredPosition;
            stackRt.localScale       = Vector3.one;
        }

        private IEnumerator AnimatePotChipsToWinnerLeg(PlayerView winnerView, int amount)
        {
            ForceEndPlayersRefresh();

            if (winnerView == null)
            {
                Debug.LogError("[UIManager] Pot collect FAILED — winner view missing.", this);
                yield break;
            }

            if (amount <= 0)
                yield break;

            if (ResolveRootCanvas() == null)
            {
                Debug.LogError("[UIManager] Pot collect FAILED — root canvas not found.", this);
                yield break;
            }

            EnsurePotChipStack();
            if (_potChipStack == null)
            {
                Debug.LogError("[UIManager] Pot collect FAILED — pot chip stack missing.", this);
                yield break;
            }

            RestorePotChipStackHome();
            ApplyNetPotToCenterDisplay(amount);
            ShowPotAmountBadge(amount);

            RectTransform stackRt = _potChipStack.StackRoot;
            var canvasRt = (RectTransform)_rootCanvas.transform;

            if (!TryResolveWinnerCollectCanvasPosition(winnerView, canvasRt, out Vector2 endPos))
            {
                Debug.LogError(
                    $"[UIManager] Pot collect FAILED — could not resolve winner collect position (amount={amount}).",
                    this);
                yield break;
            }

            if (_potChipStackHomeParent == null)
            {
                _potChipStackHomeParent       = stackRt.parent;
                _potChipStackHomeSiblingIndex = stackRt.GetSiblingIndex();
            }

            AttachStackToCanvasForFly(stackRt, canvasRt, out Vector2 startPos);
            stackRt.gameObject.SetActive(true);

            float flyDuration = Mathf.Max(0.05f, _winnerPotCollectFlyDuration);
            float elapsed     = 0f;

            while (elapsed < flyDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / flyDuration));
                stackRt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                yield return null;
            }

            stackRt.anchoredPosition = endPos;
            stackRt.localScale       = Vector3.one;

            if (_potText != null)
                _potText.text = string.Empty;

            if (_winnerPotCollectHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(_winnerPotCollectHoldDuration);

            float vanishDuration = Mathf.Max(0.05f, _winnerPotCollectVanishDuration);
            elapsed              = 0f;
            Vector3 scaleStart   = stackRt.localScale;

            while (elapsed < vanishDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / vanishDuration));

                stackRt.localScale = Vector3.Lerp(scaleStart, Vector3.zero, t);
                SetPotCollectVisualAlpha(1f - t);

                yield return null;
            }

            HidePotAmountBadge();
            RestorePotChipStackHome();
            HidePotChipStack();
        }

        private void CompleteWinnerPotCollection()
        {
            if (_potText != null)
                _potText.text = string.Empty;

            _gameManager?.ApplyPendingPotAward();

            _winnerPotChipsCollected    = true;
            _winnerCelebrationCoroutine = null;
        }

        private void EnsureWinningHandLabel()
        {
            if (_winningHandText == null)
            {
                Transform parent = _potText != null
                    ? _potText.transform.parent
                    : transform;

                Transform existing = parent != null ? parent.Find("WinningHandLabel") : null;
                if (existing != null)
                    _winningHandText = existing.GetComponent<TMP_Text>();
            }

            if (_winningHandText != null)
            {
                StyleWinningHandLabel();
                return;
            }

            Transform labelParent = _potText != null
                ? _potText.transform.parent
                : transform;

            var labelGo = new GameObject("WinningHandLabel", typeof(RectTransform));
            labelGo.transform.SetParent(labelParent, false);

            var rt = (RectTransform)labelGo.transform;
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = WinningHandLabelPosition;
            rt.sizeDelta        = new Vector2(640f, 40f);

            _winningHandText = labelGo.AddComponent<TextMeshProUGUI>();
            StyleWinningHandLabel();
            labelGo.transform.SetAsLastSibling();
        }

        private void StyleWinningHandLabel()
        {
            if (_winningHandText == null)
                return;

            TMP_FontAsset font = _potText != null && _potText.font != null
                ? _potText.font
                : ResolveButtonFont();

            if (font != null)
                _winningHandText.font = font;

            _winningHandText.alignment          = TextAlignmentOptions.Center;
            _winningHandText.fontSize           = _potText != null ? _potText.fontSize : 28f;
            _winningHandText.color              = UiColors.PotGold;
            _winningHandText.raycastTarget      = false;
            _winningHandText.enableWordWrapping = false;

            if (_winningHandText.transform is RectTransform rt)
                ApplyWinningHandLabelLayout(rt);
        }

        private Vector2 WinningHandLabelPosition =>
            new Vector2(_winningHandLabelX, _winningHandLabelY);

        private void ApplyWinningHandLabelLayout(RectTransform rt = null)
        {
            if (rt == null && _winningHandText != null)
                rt = _winningHandText.transform as RectTransform;

            if (rt == null)
                return;

            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = WinningHandLabelPosition;
        }

        private void ShowWinningHandDisplay()
        {
            EnsureWinningHandLabel();
            if (_winningHandText == null)
                return;

            IReadOnlyList<PlayerState> winners = _gameManager?.LastRoundWinners;
            var names = new List<string>();
            if (winners != null)
            {
                foreach (PlayerState player in winners)
                {
                    if (player != null && !string.IsNullOrEmpty(player.Name))
                        names.Add(player.Name);
                }
            }

            WinningHandEvaluation evaluation = _gameManager?.LastWinningHand;
            bool tiebreakerDecisive = evaluation?.Result != null
                && HandDisplayNames.WasTiebreakerDecisive(
                    evaluation.Result,
                    winners,
                    _gameManager?.LastShowdownHands);
            string message = evaluation?.Result != null
                ? HandDisplayNames.FormatWithWinners(names, evaluation.Result, tiebreakerDecisive)
                : HandDisplayNames.FormatFoldWin(names);

            if (string.IsNullOrEmpty(message))
            {
                ClearWinningHandDisplay();
                return;
            }

            _winningHandText.text = message;
            _winningHandText.gameObject.SetActive(true);
            _winningHandText.transform.SetAsLastSibling();

            if (winners != null && winners.Count > 0 && evaluation?.BestCards != null && evaluation.BestCards.Count > 0)
                ApplyWinningCardHighlightsForRound(winners, tiebreakerDecisive);
            else
                ClearWinningCardHighlights();
        }

        private void ApplyWinningCardHighlightsForRound(
            IReadOnlyList<PlayerState> winners, bool kickerDecisive)
        {
            ClearWinningCardHighlights();

            List<(PlayerState Player, WinningHandEvaluation Evaluation)> winnerEvaluations =
                ResolveWinnerEvaluations(winners);
            if (winnerEvaluations.Count == 0)
                return;

            ApplyCommunityWinningCardHighlights(winnerEvaluations, kickerDecisive);

            foreach ((PlayerState winner, WinningHandEvaluation evaluation) in winnerEvaluations)
            {
                IReadOnlyList<Card> glowCards = WinningHandEvaluation.GetGlowCards(evaluation, kickerDecisive);
                if (glowCards == null || glowCards.Count == 0)
                    continue;

                ResolvePlayerView(winner)?.ApplyWinningCardHighlights(winner, glowCards);
            }
        }

        private List<(PlayerState Player, WinningHandEvaluation Evaluation)> ResolveWinnerEvaluations(
            IReadOnlyList<PlayerState> winners)
        {
            var resolved = new List<(PlayerState Player, WinningHandEvaluation Evaluation)>();
            if (winners == null || winners.Count == 0)
                return resolved;

            var winnerSet = new HashSet<PlayerState>();
            foreach (PlayerState winner in winners)
            {
                if (winner != null)
                    winnerSet.Add(winner);
            }

            IReadOnlyList<(PlayerState Player, WinningHandEvaluation Evaluation)> showdownEvaluations =
                _gameManager?.LastShowdownEvaluations;
            if (showdownEvaluations != null)
            {
                foreach ((PlayerState player, WinningHandEvaluation evaluation) in showdownEvaluations)
                {
                    if (player != null && winnerSet.Contains(player) && evaluation != null)
                        resolved.Add((player, evaluation));
                }
            }

            if (resolved.Count == 0 && _gameManager?.LastWinningHand != null)
            {
                foreach (PlayerState winner in winners)
                {
                    if (winner != null)
                        resolved.Add((winner, _gameManager.LastWinningHand));
                }
            }

            return resolved;
        }

        private void ApplyCommunityWinningCardHighlights(
            IReadOnlyList<(PlayerState Player, WinningHandEvaluation Evaluation)> winnerEvaluations,
            bool kickerDecisive)
        {
            if (_communityCardSlots == null || _gameManager?.CommunityCards == null)
                return;

            IReadOnlyList<Card> board = _gameManager.CommunityCards;
            for (int i = 0; i < _communityCardSlots.Length && i < board.Count; i++)
            {
                CardView slot = _communityCardSlots[i];
                if (slot == null)
                    continue;

                Card boardCard = board[i];
                bool highlight = false;
                foreach ((PlayerState _, WinningHandEvaluation evaluation) in winnerEvaluations)
                {
                    IReadOnlyList<Card> glowCards = WinningHandEvaluation.GetGlowCards(evaluation, kickerDecisive);
                    if (glowCards != null && WinningHandEvaluation.ContainsCard(glowCards, boardCard))
                    {
                        highlight = true;
                        break;
                    }
                }

                slot.SetWinnerHighlight(highlight);
            }
        }

        private void ClearWinningCardHighlights()
        {
            if (_communityCardSlots != null)
            {
                foreach (CardView slot in _communityCardSlots)
                    slot?.SetWinnerHighlight(false);
            }

            if (_playerViews == null)
                return;

            foreach (PlayerView view in _playerViews)
                view?.ClearWinningCardHighlights();
        }


        private Transform RakeLabelParent =>
            _potText != null ? _potText.transform.parent : transform;

        private Vector2 RakeLabelPosition => _rakeLabelPosition;

        private void EnsureRakeLabel()
        {
            if (_rakeText == null)
            {
                Transform parent = RakeLabelParent;
                Transform existing = parent != null ? parent.Find(RakeLabelName) : null;
                if (existing != null)
                    _rakeText = existing.GetComponent<TMP_Text>();
            }

            if (_rakeText != null)
            {
                StyleRakeLabel();
                return;
            }

            Transform labelParent = RakeLabelParent;
            var labelGo = new GameObject(RakeLabelName, typeof(RectTransform));
            labelGo.transform.SetParent(labelParent, false);

            var rt = (RectTransform)labelGo.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.sizeDelta = _rakeLabelSize;

            _rakeText = labelGo.AddComponent<TextMeshProUGUI>();
            StyleRakeLabel();
        }

        private void StyleRakeLabel()
        {
            if (_rakeText == null)
                return;

            TMP_FontAsset font = _potText != null && _potText.font != null
                ? _potText.font
                : ResolveButtonFont();

            if (font != null)
                _rakeText.font = font;

            _rakeText.alignment          = TextAlignmentOptions.TopRight;
            _rakeText.fontSize           = _rakeFontSize;
            _rakeText.color              = UiColors.PotGold;
            _rakeText.raycastTarget      = false;
            _rakeText.enableWordWrapping = false;

            ApplyRakeLabelLayout();
        }

        private void ApplyRakeLabelLayout()
        {
            if (_rakeText == null || _rakeText.transform is not RectTransform rt)
                return;

            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(1f, 1f);
            rt.sizeDelta        = _rakeLabelSize;
            rt.anchoredPosition = RakeLabelPosition;
        }

        private void ShowRakeDisplay(string displayText)
        {
            if (string.IsNullOrEmpty(displayText))
            {
                HideRakeDisplay();
                return;
            }

            EnsureRakeLabel();
            if (_rakeText == null)
                return;

            _rakeText.text = "Rake: " + displayText;
            _rakeText.gameObject.SetActive(true);
            ApplyRakeLabelLayout();
            _rakeText.transform.SetAsLastSibling();
        }

        private void HideRakeDisplay()
        {
            if (_rakeText != null)
                _rakeText.gameObject.SetActive(false);
        }
        private void ClearWinningHandDisplay()
        {
            ClearWinningCardHighlights();
            HideRakeDisplay();

            if (_winningHandText != null)
                _winningHandText.gameObject.SetActive(false);
        }

        /// <summary>Finds the UI root canvas (UIManager lives on Main Camera, not under Canvas).</summary>
        private Canvas ResolveRootCanvas()
        {
            if (_rootCanvas != null)
                return _rootCanvas;

            if (_potText != null)
            {
                _rootCanvas = FindRootCanvasForTransform(_potText.transform);
                if (_rootCanvas != null)
                    return _rootCanvas;
            }

            EnsurePotChipStack();
            if (_potChipStack != null)
            {
                _rootCanvas = FindRootCanvasForTransform(_potChipStack.StackRoot);
                if (_rootCanvas != null)
                    return _rootCanvas;
            }

            if (_actionPanel != null)
            {
                _rootCanvas = FindRootCanvasForTransform(_actionPanel.transform);
                if (_rootCanvas != null)
                    return _rootCanvas;
            }

#if UNITY_2023_1_OR_NEWER
            _rootCanvas = FindFirstObjectByType<Canvas>();
#else
            _rootCanvas = FindObjectOfType<Canvas>();
#endif
            return _rootCanvas;
        }

        private static Canvas FindRootCanvasForTransform(Transform t)
        {
            if (t == null)
                return null;

            Canvas c = t.GetComponentInParent<Canvas>();
            while (c != null && !c.isRootCanvas)
            {
                Transform parent = c.transform.parent;
                c = parent != null ? parent.GetComponentInParent<Canvas>() : null;
            }

            return c;
        }
    }
}

