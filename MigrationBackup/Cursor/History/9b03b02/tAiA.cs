using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
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
        [SerializeField] private ChipStackView  _potChipStack;
        [SerializeField] private Sprite         _potChipSprite100;
        [SerializeField] private Sprite         _potChipSprite500;
        [Tooltip("Vertical offset for the pot chip stack relative to PotText anchoredPosition.y (tune in Play mode).")]
        [SerializeField] private float          _potChipYOffset;
        [Tooltip("Horizontal gap between PotText right edge and the first pot chip column.")]
        [SerializeField, Min(0f)] private float _potChipPadding = 12f;
        [Tooltip("Horizontal gap between chip denominations in the pot stack (25 | 5 | 1). 0 = use TableLayoutManager Chip Column Gap X.")]
        [SerializeField, Range(0f, 48f)] private float _potChipColumnGapX;
        [Tooltip("Vertical step between identical chips in the pot stack only (bet stacks use TableLayoutManager).")]
        [SerializeField, Range(1f, 12f)] private float _potStackOverlapY = 2f;

        [Header("Copyright")]
        [SerializeField, InspectorName("CopyrightLabel")]
        [Tooltip("Copyright/version text. Edit Canvas → CopyrightLabel TMP directly, or set this and use context menu Apply Copyright Label To TMP.")]
        private string _copyrightLabel = "v1.0";
        [SerializeField] private TMP_Text _copyrightLabelText;

        [Header("Between Hands")]
        [Tooltip("After each hand, opens the options menu; the next deal waits until the menu is closed.")]
        [SerializeField] private bool _openMenuBetweenHands = true;

        [Header("Seat Bet Place")]
        [Tooltip("When off, bet chips appear instantly under the seat (GGPoker / PokerStars style).")]
        [SerializeField] private bool _animateBetPlace;

        [Header("Street Bet Collect")]
        [Tooltip("When off, seat bets clear instantly and the pot stack updates (GGPoker / PokerStars style).")]
        [SerializeField] private bool _animateStreetCollect;
        [Tooltip("Duration of each chip flying from a seat bet stack to the pot.")]
        [SerializeField, Min(0.05f)] private float _collectFlyDuration = 0.5f;
        [Tooltip("Delay between seats when collecting bets into the pot.")]
        [SerializeField, Min(0f)] private float _collectSeatStagger = 0.08f;
        [Tooltip("Flying chips spawned per seat with a visible bet.")]
        [SerializeField, Range(1, 4)] private int _collectChipsPerSeat = 2;
        [Tooltip("Pause after collect animation before updating the pot display.")]
        [SerializeField, Min(0f)] private float _collectPotUpdateDelay = 0.1f;

        [Header("Winner Celebration")]
        [SerializeField] private Sprite _winChipSprite;
        [SerializeField, Min(1)]  private int   _winChipCount   = 8;
        [SerializeField, Min(0f)] private float _winChipStagger = 0.08f;

        [Header("Winning Hand")]
        [SerializeField] private TMP_Text _winningHandText;
        [Tooltip("Canvas position of WinningHandLabel (center anchor). Tune in Play mode.")]
        [SerializeField] private float _winningHandLabelX = 0f;
        [SerializeField] private float _winningHandLabelY = 90f;

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

        // Platform-independent German number format: 1000 → "1.000"
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
        private Coroutine            _humanHoleRevealCoroutine;
        private Coroutine            _beginTurnCoroutine;
        private Coroutine            _timerCoroutine;
        private PlayerView           _activeTimerView;
        private PlayerView           _humanSeatView;
        private readonly Dictionary<int, int> _previousBets = new Dictionary<int, int>();
        private bool                          _suppressDealerButton;
        private bool                          _collectInProgress;
        private Canvas               _rootCanvas;

        private bool          _hasPendingActionBadge;
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
        }

        private void OnDestroy()
        {
            if (OptionsMenu.Instance != null)
            {
                OptionsMenu.Instance.OnOptionsChanged -= OnOptionsMenuChanged;
                OptionsMenu.Instance.OnMenuClosed     -= OnOptionsMenuClosed;
            }
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
                Debug.LogError("[UIManager] GameManager reference missing — action badges and betting will not work.");
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

            ShowActionBadgeForPlayer(player, action, amount);

            _pendingBadgePlayer    = player;
            _pendingBadgeAction    = action;
            _pendingBadgeAmount    = amount;
            _hasPendingActionBadge = true;
        }

        private void ShowActionBadgeForPlayer(PlayerState player, BettingAction action, int amount)
        {
            // Stop any blinking badge from the previous player before showing the new one.
            HideAllActionBadges();

            PlayerView view = ResolvePlayerView(player);
            if (view == null)
            {
                Debug.LogWarning(
                    $"[UIManager] ActionBadge: no PlayerView for {player.Name} " +
                    $"(Players index {_gameManager.Players.IndexOf(player)}).");
                return;
            }

            view.ShowAction(action, amount);
        }

        /// <summary>Re-shows the badge after seat HUD/bet display refresh (layering).</summary>
        private void ApplyPendingActionBadge()
        {
            if (!_hasPendingActionBadge || _pendingBadgePlayer == null)
                return;

            ShowActionBadgeForPlayer(_pendingBadgePlayer, _pendingBadgeAction, _pendingBadgeAmount);

            PlayerView view = ResolvePlayerView(_pendingBadgePlayer);
            view?.BringActionBadgeToFrontIfVisible();

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
            if (_playerViews == null) return;
            foreach (PlayerView view in _playerViews)
                view?.HideActionBadge();
        }

        private void OnRoundStarting()
        {
            _suppressDealerButton = true;
            _tableLayout?.HideDealerButton();

            StopHumanHoleReveal();
            HideAllActionBadges();

            foreach (PlayerView view in ResolvePlayerViews())
            {
                view?.HideBetDisplay();
                view?.ResetHoleCardReveal();
            }

            _previousBets.Clear();
            ClearWinningHandDisplay();
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
            float flipDuration = _gameManager != null ? _gameManager.CommunityFlipDuration : 0.35f;
            float flipGap      = _gameManager != null ? _gameManager.CommunityFlipGap : 0.1f;

            yield return humanView.RevealHumanHoleCards(humanState, flipDuration, flipGap);
            _humanHoleRevealCoroutine = null;
        }

        private IEnumerator WaitForTableUiIdle(bool waitForHoleReveal)
        {
            while (_playersRefreshCoroutine != null)
                yield return null;

            if (waitForHoleReveal)
            {
                while (_humanHoleRevealCoroutine != null)
                    yield return null;
            }
        }

        private IEnumerator RefreshPlayers(List<PlayerState> players)
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
                view.RefreshHud(player);

                if (isHuman)
                {
                    humanView  = view;
                    humanState = player;
                    _humanSeatView = view;
                    view.SyncHumanHoleCardDisplay(player, holeRevealRunning);
                }
                else
                {
                    bool showBotCards = OptionsMenu.Instance != null && OptionsMenu.Instance.ShowBotCards;
                    bool isShowdown   = _gameManager != null
                                        && _gameManager.CurrentPhase == GamePhase.Showdown;
                    if (!player.HasFolded && (isShowdown || showBotCards))
                        view.RevealCards(player);
                    else
                        view.RefreshOpponentCards(player);
                }

                // Update BetDisplay — animate chip only when the player's bet increases.
                _previousBets.TryGetValue(i, out int prevBet);
                bool betIncreased = player.CurrentBet > prevBet;
                if (betIncreased)
                    anyBetIncreased = true;
                _previousBets[i]  = player.CurrentBet;

                if (player.CurrentBet > 0)
                    view.ShowBetDisplay(
                        player.CurrentBet,
                        _animateBetPlace && betIncreased && !_collectInProgress ? view.AvatarRect : null);
                else
                    view.HideBetDisplay();

                view.BringActionBadgeToFrontIfVisible();
            }

            ApplyPendingActionBadge();

            TryScheduleHumanHoleReveal(humanView, humanState);

            UpdatePotLabel();
            if (anyBetIncreased && !_collectInProgress)
                HidePotChipStack();
            RefreshDealerButton();
            _playersRefreshCoroutine = null;
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

            float flipDuration = _gameManager != null ? _gameManager.CommunityFlipDuration : 0.35f;
            float flipGap      = _gameManager != null ? _gameManager.CommunityFlipGap : 0.1f;

            int startIndex = _revealedCommunityCount;
            for (int i = startIndex; i < cards.Count; i++)
            {
                CardView slot = _communityCardSlots[i];
                if (slot == null) continue;

                slot.SetFlipDuration(flipDuration);
                slot.ShowFaceDown();

                bool flipDone = false;
                slot.FlipToFace(cards[i], () => flipDone = true);
                yield return new WaitUntil(() => flipDone);
                _revealedCommunityCount = i + 1; // update per-card so StopCoroutine leaves count correct

                if (i < cards.Count - 1)
                    yield return new WaitForSeconds(flipGap);
            }

            for (int i = cards.Count; i < _communityCardSlots.Length; i++)
                _communityCardSlots[i]?.Hide();

            _communityRevealCoroutine = null;
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
            _hasPendingActionBadge  = false;
            _suppressDealerButton   = true;

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
            for (int i = 0; i < _playerViews.Count; i++)
                _playerViews[i].SetActiveTurn(false);

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
            return Mathf.Max(0, _gameManager.CurrentBet - _humanPlayer.CurrentBet);
        }

        private int GetMinRaiseIncrement() =>
            _gameManager != null ? _gameManager.BigBlindAmount * 2 : 40;

        private int GetMaxRaiseIncrement()
        {
            if (_humanPlayer == null || _gameManager == null) return 0;
            return _humanPlayer.Chips - GetCallAmount();
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

        /// <summary>Updates the pot number only — used while a betting street is in progress.</summary>
        private void UpdatePotLabel()
        {
            if (_potText == null || _gameManager == null)
                return;

            int pot = _gameManager.PotAmount;
            _potText.text = "Pot: " + pot.ToString("N0", GermanNFI);

            if (_potChipStack != null && _potChipStack.StackRoot.gameObject.activeSelf)
                PositionPotChipStack(showStack: true);
        }

        /// <summary>Hides the central pot chip stack (label unchanged).</summary>
        private void HidePotChipStack()
        {
            if (_potChipStack == null)
                EnsurePotChipStack();
            if (_potChipStack == null)
                return;

            _potChipStack.Clear();
            _potChipStack.StackRoot.gameObject.SetActive(false);
        }

        /// <summary>Shows the pot chip breakdown — after street bets are collected into the pot.</summary>
        private void ShowPotChipStack()
        {
            if (_potText == null || _gameManager == null)
                return;

            int pot = _gameManager.PotAmount;
            if (pot <= 0)
            {
                HidePotChipStack();
                return;
            }

            if (_potChipStack == null)
                EnsurePotChipStack();
            if (_potChipStack == null)
                return;

            ApplyPotChipStackSettings();
            _potChipStack.SetExactAmount(pot);
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
            if (_potChipStack != null || _potText == null)
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

            var potRt   = (RectTransform)_potText.transform;
            var stackRt = _potChipStack.StackRoot;
            bool show   = showStack
                          && _gameManager != null
                          && _gameManager.PotAmount > 0;

            stackRt.gameObject.SetActive(show);
            if (!show)
                return;

            _potText.ForceMeshUpdate();
            float textHalf   = _potText.preferredWidth * 0.5f;
            float stackHalfW = stackRt.rect.width > 0f
                ? stackRt.rect.width * 0.5f
                : stackRt.sizeDelta.x * 0.5f;

            float stackX = potRt.anchoredPosition.x + textHalf + _potChipPadding + stackHalfW;

            stackRt.anchoredPosition = new Vector2(
                stackX,
                potRt.anchoredPosition.y + _potChipYOffset);
        }

        /// <summary>
        /// Clears seat bet stacks and refreshes the pot HUD after a betting street.
        /// Called between betting streets (preflop→flop, flop→turn, turn→river).
        /// </summary>
        public IEnumerator CollectStreetBetsToPot()
        {
            if (!Application.isPlaying || _gameManager == null)
                yield break;

            if (_playersRefreshCoroutine != null)
            {
                StopCoroutine(_playersRefreshCoroutine);
                _playersRefreshCoroutine = null;
            }

            IReadOnlyList<PlayerView> views = ResolvePlayerViews();
            if (views == null)
                yield break;

            if (_animateStreetCollect)
            {
                _rootCanvas ??= ResolveRootCanvas();
                RectTransform potTarget = ResolvePotCollectTarget();
                if (potTarget == null)
                    yield break;

                EnsurePotChipStack();

                List<PlayerState> players = _gameManager.Players;
                if (players == null)
                    yield break;

                var routines = new List<IEnumerator>();
                int seatOrder = 0;

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

                    float seatDelay = seatOrder * _collectSeatStagger;
                    for (int c = 0; c < _collectChipsPerSeat; c++)
                    {
                        float chipDelay = seatDelay + c * (_collectSeatStagger * 0.35f);
                        routines.Add(bet.PlayCollectToPot(
                            potTarget, player.CurrentBet, _collectFlyDuration, chipDelay));
                    }

                    seatOrder++;
                }

                if (routines.Count > 0)
                {
                    _collectInProgress = true;
                    yield return RunParallel(routines);
                    _collectInProgress = false;

                    if (_collectPotUpdateDelay > 0f)
                        yield return new WaitForSeconds(_collectPotUpdateDelay);
                }
            }

            FinishStreetBetCollect(views);
        }

        private void FinishStreetBetCollect(IReadOnlyList<PlayerView> views)
        {
            foreach (PlayerView view in views)
                view?.HideBetDisplay();

            _previousBets.Clear();
            UpdatePotLabel();
            ShowPotChipStack();
        }

        private RectTransform ResolvePotCollectTarget()
        {
            if (_potChipStack != null)
                return _potChipStack.StackRoot;

            return _potText != null ? (RectTransform)_potText.transform : null;
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

            if (_humanPlayer != null)
            {
                PlayerView humanSeat = ResolveHumanSeatView() ?? ResolvePlayerView(_humanPlayer);
                humanSeat?.ShowAction(action, raiseAmount);
            }

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
            return GetMaxRaiseIncrement() >= GetMinRaiseIncrement();
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
                Debug.LogWarning("[UIManager] Casino3D SDF unavailable — button labels will use scene defaults.");

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

        /// <summary>Bottom action row uses textured sprite buttons — not seat <see cref="ActionBadge"/> SDF pills.</summary>
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
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>Scene view only — keeps every ButtonRow child active and fully opaque.</summary>
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

            if (Application.isPlaying)
            {
                if (_potChipStack == null && _potText != null)
                    EnsurePotChipStack();
                ApplyPotChipStackSettings();
                UpdatePotLabel();
                ApplyActionAmountBadgeSettings();
                EnsureWinningHandLabel();
                ApplyWinningHandLabelLayout();
                RefreshVisibleSeatActionBadges();
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

        // ── Winner Celebration ─────────────────────────────────────────────

        private void OnWinnerDetermined(PlayerState winner)
        {
            if (_gameManager == null || _playerViews == null) return;

            int index = _gameManager.Players.IndexOf(winner);
            if (index < 0 || index >= _playerViews.Count) return;

            PlayerView view = _playerViews[index];
            if (view == null) return;

            StartCoroutine(ShowWinnerCelebration(view, winner, _gameManager.LastPotAwarded, _gameManager.RoundEndPauseSecs));
        }

        private IEnumerator ShowWinnerCelebration(PlayerView view, PlayerState winner, int potAmount, float duration)
        {
            yield return null;
            yield return null;

            ShowWinningHandDisplay(winner, view);
            view.StartWinnerHighlight(potAmount, duration);

            // Fly chips from the pot label position to the winner's avatar.
            RectTransform origin = _potText != null ? (RectTransform)_potText.transform : null;
            RectTransform target = view.AvatarRect;

            if (origin != null && target != null && _rootCanvas != null)
            {
                for (int i = 0; i < _winChipCount; i++)
                {
                    StartCoroutine(SpawnFlyingChip(origin, target, i * _winChipStagger));
                    yield return null;
                }
            }

            if (duration > 0f)
                yield return new WaitForSecondsRealtime(duration);
        }

        private IEnumerator SpawnFlyingChip(RectTransform origin, RectTransform target, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (_rootCanvas == null) yield break;

            var chipGo  = new GameObject("_WinChip", typeof(RectTransform), typeof(Image));
            var chipImg = chipGo.GetComponent<Image>();
            chipImg.sprite         = _winChipSprite;
            chipImg.color          = _winChipSprite != null ? Color.white : UiColors.PotGold;
            chipImg.raycastTarget  = false;
            chipImg.preserveAspect = true;

            var chipRt = (RectTransform)chipGo.transform;
            chipRt.SetParent((RectTransform)_rootCanvas.transform, false);
            chipRt.sizeDelta  = new Vector2(40f, 40f);
            chipRt.anchorMin  = new Vector2(0.5f, 0.5f);
            chipRt.anchorMax  = new Vector2(0.5f, 0.5f);
            chipRt.pivot      = new Vector2(0.5f, 0.5f);

            var canvasRt = (RectTransform)_rootCanvas.transform;
            Vector2 startPos = canvasRt.InverseTransformPoint(origin.TransformPoint(Vector3.zero));
            Vector2 endPos   = canvasRt.InverseTransformPoint(target.TransformPoint(Vector3.zero));

            // Scatter each chip slightly so they don't all overlap.
            startPos += new Vector2(Random.Range(-24f, 24f), Random.Range(-12f, 12f));
            chipRt.anchoredPosition = startPos;

            const float FlyDuration = 0.55f;
            float elapsed = 0f;
            while (elapsed < FlyDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / FlyDuration);

                Vector2 pos = Vector2.Lerp(startPos, endPos, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * 70f; // parabolic arc upward

                chipRt.anchoredPosition = pos;
                chipRt.localScale       = Vector3.Lerp(Vector3.one * 1.3f, Vector3.one * 0.65f, t);

                // Fade out in the final 20% of the flight.
                float alpha = t > 0.8f ? Mathf.Lerp(1f, 0f, (t - 0.8f) / 0.2f) : 1f;
                chipImg.color = new Color(chipImg.color.r, chipImg.color.g, chipImg.color.b, alpha);

                yield return null;
            }

            if (chipGo != null) Destroy(chipGo);
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

        private void ShowWinningHandDisplay(PlayerState winner, PlayerView winnerView)
        {
            EnsureWinningHandLabel();
            if (_winningHandText == null)
                return;

            WinningHandEvaluation evaluation = _gameManager?.LastWinningHand;
            string message = evaluation?.Result != null
                ? HandDisplayNames.FormatWithWinner(winner?.Name, evaluation.Result)
                : string.IsNullOrEmpty(winner?.Name) ? string.Empty : $"{winner.Name} wins";

            if (string.IsNullOrEmpty(message))
            {
                ClearWinningHandDisplay();
                return;
            }

            _winningHandText.text = message;
            _winningHandText.gameObject.SetActive(true);
            _winningHandText.transform.SetAsLastSibling();

            if (evaluation?.BestCards != null && evaluation.BestCards.Count > 0)
                ApplyWinningCardHighlights(winner, winnerView, evaluation.BestCards);
            else
                ClearWinningCardHighlights();
        }

        private void ApplyWinningCardHighlights(
            PlayerState winner, PlayerView winnerView, IReadOnlyList<Card> winningCards)
        {
            ClearWinningCardHighlights();

            if (winningCards == null || winningCards.Count == 0)
                return;

            if (_communityCardSlots != null && _gameManager?.CommunityCards != null)
            {
                IReadOnlyList<Card> board = _gameManager.CommunityCards;
                for (int i = 0; i < _communityCardSlots.Length && i < board.Count; i++)
                {
                    CardView slot = _communityCardSlots[i];
                    if (slot == null)
                        continue;

                    slot.SetWinnerHighlight(WinningHandEvaluation.ContainsCard(winningCards, board[i]));
                }
            }

            winnerView?.ApplyWinningCardHighlights(winner, winningCards);
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

        private void ClearWinningHandDisplay()
        {
            ClearWinningCardHighlights();

            if (_winningHandText != null)
                _winningHandText.gameObject.SetActive(false);
        }

        /// <summary>Walks up the Canvas hierarchy to find the root canvas for spawning overlay objects.</summary>
        private Canvas ResolveRootCanvas()
        {
            Canvas c = GetComponentInParent<Canvas>();
            while (c != null && !c.isRootCanvas)
                c = c.transform.parent != null
                    ? c.transform.parent.GetComponentInParent<Canvas>()
                    : null;
            return c;
        }
    }
}
