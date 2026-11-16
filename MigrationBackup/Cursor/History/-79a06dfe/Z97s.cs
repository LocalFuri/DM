//CodeRed Soft 2026-05-29
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;

namespace Blackjack
{
    /// <summary>
    /// Central game controller. Manages game state, deal flow, dealer AI, and UI updates.
    /// </summary>
    public class BlackjackGame : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────────
        // Inspector References
        // ──────────────────────────────────────────────────────────────────────────

        [Header("Registry")]
        [SerializeField] private CardSpriteRegistry spriteRegistry;

        [Header("Layout - Player")]
        [SerializeField] private Transform playerCardArea;
        [SerializeField] private Transform splitCardArea;

        [Header("Layout - Dealer")]
        [SerializeField] private Transform dealerCardArea;

        [Header("Card Prefab")]
        [SerializeField] private GameObject cardViewPrefab;

        [Header("Buttons")]
        [SerializeField] private Button dealButton;
        [SerializeField] private Button hitButton;
        [SerializeField] private Button standButton;
        [SerializeField] private Button surrenderButton;
        [SerializeField] private Button splitButton;
        [SerializeField] private Button doubleDownButton;
        [SerializeField] private Button ddTestButton;

        [Header("Button Sprites")]
        [SerializeField] private Sprite splitAvailableSprite;

        [Header("Menu")]
        [SerializeField] private MenuController menuController;

        [Header("Strategy Table")]
        [SerializeField] private Blackjack.UI.StrategyTableUI strategyTableUI;
        [SerializeField] private bool showStrategyTable = true;

        [Header("Score Labels")]
        [SerializeField] private TextMeshProUGUI playerScoreLabel;
        [SerializeField] private TextMeshProUGUI dealerScoreLabel;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI statusLabel;
        [SerializeField] private TextMeshProUGUI streakLabel;
        [SerializeField] private TextMeshProUGUI martingaleModeLabel;

        [Header("Player Info Labels")]
        [Tooltip("Equal vertical gap in pixels between name, money, streak, and Martingale mode labels.")]
        [SerializeField] private float playerInfoLabelGap = 8f;
        [Tooltip("Anchored Y of the top player name label; the other three labels stack below it.")]
        [SerializeField] private float playerInfoTopY = 303f;
        [Tooltip("Font size for the Martingale mode label (uses the status label font at this size).")]
        [SerializeField] private float martingaleModeFontSize = 22f;

        [Header("Money")]
        [SerializeField] private TextMeshProUGUI playerMoneyLabel;
        [SerializeField] private ChipBetting chipBetting;
        [SerializeField] private int startingMoney = 0;

        [Header("Basic Strategy")]
        [SerializeField] private StrategyDeviationPopup deviationPopup;

        [Header("Martingale")]
        [SerializeField] private MartingalePopup martingalePopup;

    [Header("Fireworks")]
    [SerializeField] private GameObject fireworksPrefab;
    [SerializeField] private float fireworksDuration = 4f;


    [Header("Audio")] //mark audio
        [SerializeField] private AudioSource audioSource;

    [SerializeField] private SoundEntry cardSlideSound;
    [SerializeField] private SoundEntry cheaterSound;
    [SerializeField] private SoundEntry chipSound;
    [SerializeField] private SoundEntry damnitSound;
    [SerializeField] private SoundEntry dealCardSound;
    [SerializeField] private SoundEntry ddSound;
    [SerializeField] private SoundEntry exitSound;
    [SerializeField] private SoundEntry hmhSound;
    [SerializeField] private SoundEntry knockSound;
    [SerializeField] private SoundEntry loseSound;
    [SerializeField] private SoundEntry naturalBlackjackSound;
    [SerializeField] private SoundEntry resetSound;
    [SerializeField] private SoundEntry startupSound;
    [SerializeField] private SoundEntry surrenderSound;
    [SerializeField] private SoundEntry tieSound;
    [SerializeField] private SoundEntry winSound;
    [SerializeField] private SoundEntry yuhuSound;

    private SoundEntry? _lastDoubleBJSound;
    private bool _doubleBJSoundPlaying;
    private bool _dealerNaturalBJPlaying;
    private float _fireworksEndTime;
    private float _dealerNaturalBJEndTime;

        [Header("Timing")]

        [Tooltip("dealDelay ist set to 0.45 in code, you can not change it in the inspector!")]
        [SerializeField] private float dealDelay          = 0.45f; //default is 0.45
        [SerializeField] private float dealerPauseDelay   = 0.7f;
        [Tooltip("Seconds to wait after doubling the bet before dealing the third card.")]
        [SerializeField] private float doubleDownBetPause = 0.6f;
        [SerializeField] private float endRoundDelay      = 3.0f;
        [SerializeField] private float newRoundPause      = 0.5f;

        [Header("Controls")]
        [SerializeField] private KeyboardControls controls;

        // ──────────────────────────────────────────────────────────────────────────
        // Constants mark auto
        // ──────────────────────────────────────────────────────────────────────────

        private const int AutoStandHard      = 17;
        private const int AutoStandSoft      = 19;
        private const int AutoHitMaxScore    = 0; //disable
        private const int DealerSoft17       = 17;
        private const int BlackjackValue     = 21;
        public  const int BetLimit           = 1000;

        private static readonly Color WinStatusColor = new Color(1f, 0f, 0f, 1f);        //gold
        private static readonly Color WinColor = new Color(0f, 1f, 0f, 1f);              //green
        private static readonly Color LoseColor = new Color(1f, 0f, 0f, 1f);             //red
        private static readonly Color PushColor = new Color(0.7f, 0f, 0.7f, 1f);        //magenta
        private static readonly Color SurrenderColor = new Color(0f, 0.6666667f, 1f, 1f);
    // ──────────────────────────────────────────────────────────────────────────
    // State
    // ──────────────────────────────────────────────────────────────────────────

      private readonly Deck _deck          = new();
        private readonly Hand _playerHand    = new();
        private readonly Hand _dealerHand    = new();
        private readonly Hand _splitHand     = new();

        private readonly List<CardView> _playerCardViews = new();
        private readonly List<CardView> _splitCardViews  = new();
        private readonly List<CardView> _dealerCardViews = new();

        private CardView _dealerHoleCardView;

        private bool _forcePlayerBlackjack;
        private bool _forceBothBlackjack;
        private bool _forceSplitHand;
        private bool _forceDoubleDownTest;
        private bool _forceDealerBlackjack;
        private bool _isSplitRound;
        private int  _activeHandIndex; // 0 = player, 1 = split

        private int _doubleDownExtraBet; // extra bet deducted when doubling down
        private int _savedBetBeforeAction; // bet amount before split/double-down, restored next round
        private int _betBeforeMartingale;  // bet placed before Martingale doubling; restored on a Martingale win

        // Snapshot of the dealer's visible upcard taken at the start of the player's turn.
        private CardData _dealerUpcardSnapshot;

        private readonly BasicStrategyAdvisor _strategyAdvisor = new();

        private decimal _playerMoney; //decimal need for 5 chips / 2 surrendering = 2.5 chips

        // Delayed Martingale detection
        private decimal _consecutiveLosses;
        private int _totalLosses;
        private decimal _totalAmountLost;
        private int _lastRoundBet;
        private bool _martingaleWin;
        private bool _playerWon;
        private bool _martingalePopupShown;
        // True once the player has accepted the Martingale popup. Cleared only on a win. Suppresses the popup while active.
        private bool _inMartingaleMode;
        // True when the player is in active Martingale mode and lost the last round — bet should be doubled on next betting screen.
        private bool _pendingMartingaleDouble;
        // True after the player explicitly declines the Martingale popup. Suppresses re-prompting on Deal until the streak resets.
        private bool _martingaleDeclined;

        private static readonly Color MartingaleModeGoldColor = new Color(1f, 0.85f, 0.3f, 1f);

        // Running score: +1 per win, -1 per loss, 0 for push or surrender.
        private int _playerScore;

        // Fallback used only when MenuController is not available. The authoritative value lives on MenuController.defaultMartingaleThreshold.
        private const int DelayedMartingaleThreshold = 1;

        // Returns the effective Martingale trigger threshold from the menu slider, falling back to the hardcoded constant.
        private int EffectiveMartingaleThreshold =>
            menuController != null
                ? menuController.MartingaleThreshold
                : DelayedMartingaleThreshold;

    private TextMeshProUGUI _splitScoreLabel;
        private Vector2 _defaultPlayerScorePosition;
        private ScoreLabelPulse _playerScorePulse;
        private ScoreLabelPulse _splitScorePulse;
        private Color _defaultStatusColor;

        private const float StatusLabelHeight = 50f;

        private HorizontalLayoutGroup _frozenLayoutGroup;
        private RectTransform           _doubleDownFirst;
        private RectTransform           _doubleDownSecond;
        private RectTransform           _doubleDownThird;
        private Coroutine               _doubleDownLayoutCoroutine;

        private const float DefaultCardWidth       = 120f;
        private const float DefaultCardHeight      = 168f;
        // Small upward shift from the pair center (see PlaceDoubleDownCard).
        private const float DoubleDownVerticalLift = 50f;

    private Hand           ActiveHand  => _activeHandIndex == 0 ? _playerHand  : _splitHand;
        private List<CardView> ActiveViews => _activeHandIndex == 0 ? _playerCardViews : _splitCardViews;

        private enum GameState { Idle, PlayerTurn, DealerTurn, RoundOver }
        private GameState _state = GameState.Idle;

        /// <summary>True when the player is allowed to place or remove bets (before a round begins).</summary>
        public bool IsBettingAllowed => _state == GameState.Idle;

        /// <summary>True when the current round has ended and the table is showing results.</summary>
        public bool IsRoundOver => _state == GameState.RoundOver;

        /// <summary>True while the developer menu is open. Used by <see cref="ChipBetting"/> to suppress chip input.</summary>
        public bool IsMenuOpen => menuController != null && menuController.IsMenuOpen;

        /// <summary>Closes the menu panel. Used by <see cref="ChipBetting"/> when a bet action is taken during the betting phase.</summary>
        /// <param name="playSound">When false, suppresses the close sound. Defaults to true.</param>
        public void CloseMenu(bool playSound = true) => menuController?.CloseMenu(playSound);

        /// <summary>Inspector-configured default for whether the strategy table starts visible.</summary>
        public bool ShowStrategyTable => showStrategyTable;

        /// <summary>When true, the player always loses the round regardless of card values. Used for Martingale testing.</summary>
        public bool AlwaysLose { get; set; }

        /// <summary>
        /// Called when the player manually enables the "Martingale is Active" checkbox.
        /// Clears any previous decline so the popup can fire again.
        /// If the loss threshold is already met, shows the popup immediately regardless of game state.
        /// Does NOT double the bet or start a round — the popup handles that.
        /// </summary>
        public void TryStartMartingaleFromToggle()
        {
            // Clear a previous decline — the player is explicitly re-enabling Martingale.
            _martingaleDeclined = false;

            bool thresholdMet = EffectiveMartingaleThreshold > 0 && _consecutiveLosses >= EffectiveMartingaleThreshold;

            Debug.Log($"[Martingale] TryStartMartingaleFromToggle: losses={_consecutiveLosses} threshold={EffectiveMartingaleThreshold} thresholdMet={thresholdMet} inMode={_inMartingaleMode}");

            if (thresholdMet && !_inMartingaleMode)
                ShowMartingalePopup();

            RefreshStreakLabel();
        }

        /// <summary>
        /// Arms Martingale after the player confirms the popup, then starts the next round
        /// (bet is doubled in <see cref="PrepareForBetting"/>).
        /// </summary>
        private void EnterMartingaleFromPopupConfirm()
        {
            if (AlwaysLose)
            {
                AlwaysLose = false;
                OnAlwaysLoseDisabled?.Invoke();
            }

            if (chipBetting != null && _betBeforeMartingale <= 0)
                _betBeforeMartingale = chipBetting.TotalBet;

            _inMartingaleMode        = true;
            _pendingMartingaleDouble = true;
            menuController?.DisableOverrideStrategy();
            menuController?.ActivateMartingale();
            RefreshStreakLabel();
            OnDeal();
        }

        private void ShowMartingalePopup()
        {
            Debug.Log($"[Martingale] ShowMartingalePopup: autoPlay={menuController?.IsMartingaleAutoPlay} popupNull={martingalePopup == null}");

            // Auto-play: skip popup, enter Martingale mode and deal immediately.
            if (menuController != null && menuController.IsMartingaleAutoPlay)
            {
                _inMartingaleMode        = true;
                _betBeforeMartingale     = chipBetting != null ? chipBetting.TotalBet : 0;
                _pendingMartingaleDouble = true;
                menuController.DisableOverrideStrategy();
                menuController.ActivateMartingale();
                RefreshStreakLabel();
                OnDeal();
                return;
            }

            // Manual: show the popup and wait for the player's choice.
            if (martingalePopup != null)
            {
                martingalePopup.Show(
                    "Play Martingale ?",
                    onDoIt: EnterMartingaleFromPopupConfirm,
                    onReconsider: () =>
                    {
                        _inMartingaleMode   = false;
                        _martingaleDeclined = true;
                        menuController?.DeactivateMartingale();
                        RefreshStreakLabel();
                    }
                );
            }
        }

        /// <summary>
        /// Cancels Martingale mode immediately.
        /// Called when the player unchecks the "Martingale is Active" checkbox in the menu.
        /// Clears all Martingale flags so no doubling or popup will occur in subsequent rounds.
        /// The consecutive-loss streak counter is preserved so the player can see their history.
        /// </summary>
        public void CancelMartingale()
        {
            _inMartingaleMode        = false;
            _pendingMartingaleDouble = false;
            _martingalePopupShown    = false;
            _martingaleDeclined      = false;
            RefreshStreakLabel();
        }

        /// <summary>Fired when the game automatically disables "Always Lose" upon entering Martingale mode.</summary>
        public event System.Action OnAlwaysLoseDisabled;

        /// <summary>
        /// Resets the game to its initial state: sets the player's money to zero,
        /// clears all cards and the bet area, and returns to the Idle state.
        /// </summary>
        public void ResetGame()
        {
            StopAllCoroutines();
            _doubleBJSoundPlaying = false;

            menuController?.CloseMenu();

            StopBlackjackCelebration();
            martingalePopup?.Hide();

            DestroyCardViews(_playerCardViews);
            DestroyCardViews(_splitCardViews);
            DestroyCardViews(_dealerCardViews);

            _playerHand.Clear();
            _splitHand.Clear();
            _dealerHand.Clear();
            _dealerHoleCardView   = null;
            _isSplitRound         = false;
            _activeHandIndex      = 0;
            _savedBetBeforeAction = 0;
            _betBeforeMartingale  = 0;

            chipBetting?.ClearBetArea();

            _playerMoney = 0;
            RefreshMoneyLabel();

            _consecutiveLosses        = 0;
            _totalLosses              = 0;
            _totalAmountLost          = 0;
            _playerScore              = 0;
            _lastRoundBet             = 0;
            _martingaleWin            = false;
            _playerWon                = false;
            _martingalePopupShown    = false;
            _inMartingaleMode        = false;
            _pendingMartingaleDouble = false;
            RefreshStreakLabel();

            // Restore the Override Strategy option in case Martingale mode was active.

            StopAllScorePulses();
            ResetPlayerScoreLabelPosition();
            SetScoreLabelsVisible(false);

            _state = GameState.Idle;

            SetButtonState(dealEnabled: true, actionEnabled: false, splitEnabled: false);
            SetStatus("Press Deal to start");

            if (resetSound.HasClip && audioSource != null)
                resetSound.Play(audioSource);
        }

        /// <summary>Returns true when the menu is open and a round is already in progress — input should be suppressed.</summary>
        private bool IsMenuBlocking => menuController != null && menuController.IsMenuOpen
            && (_state == GameState.PlayerTurn || _state == GameState.DealerTurn);

        /// <summary>
        /// Transitions from RoundOver back to Idle, clearing the table and prompting the player to bet.
        /// Called by ChipBetting when the player clicks a chip after a round ends.
        /// </summary>
        public void PrepareForBetting()
        {
            if (_state != GameState.RoundOver) return;

            DestroyCardViews(_playerCardViews);
            DestroyCardViews(_splitCardViews);
            DestroyCardViews(_dealerCardViews);

            _playerHand.Clear();
            _splitHand.Clear();
            _dealerHand.Clear();
            _dealerHoleCardView = null;
            _isSplitRound       = false;
            _activeHandIndex    = 0;

            if (_savedBetBeforeAction > 0 && chipBetting != null)
            {
                chipBetting.RestoreBet(_savedBetBeforeAction);
                _savedBetBeforeAction = 0;
            }

            // If the player lost while in Martingale mode, double the bet for the next round.
            bool martingaleBetLimitExceeded = false;
            if (_pendingMartingaleDouble && chipBetting != null)
            {
                if (_betBeforeMartingale <= 0)
                    _betBeforeMartingale = chipBetting.TotalBet;

                _pendingMartingaleDouble = false;
                martingaleBetLimitExceeded = !chipBetting.DoubleBetChips(
                    playSound: false, enforceMaxBet: true, notifyLimitExceeded: false);
            }

     
            StopAllScorePulses();
            ResetPlayerScoreLabelPosition();
            SetScoreLabelsVisible(false);
            if (martingaleBetLimitExceeded)
                NotifyBetLimitExceeded();
            else
                SetStatus("Place your bet");

            _state = GameState.Idle;
        }


        // ──────────────────────────────────────────────────────────────────────────

        private void Start()
        {
            if (startupSound.HasClip && audioSource != null)
                startupSound.Play(audioSource);

            _playerMoney = startingMoney;
            RefreshMoneyLabel();

            if (chipBetting != null)
                chipBetting.OnBetChanged += OnBetChangedHandler;

            if (strategyTableUI != null)
                strategyTableUI.gameObject.SetActive(showStrategyTable);

            _deck.Build();
            if (statusLabel != null)
                _defaultStatusColor = statusLabel.color;
            AlignStatusLabelToCardArea();
            InitSplitScoreLabel();
            InitMartingaleModeLabel();
            AlignPlayerInfoLabels();
            SetScoreLabelsVisible(false);
            RefreshStreakLabel();
            SetButtonState(dealEnabled: true, actionEnabled: false, splitEnabled: false);
            SetStatus("Press Deal to start");
        }

        private void OnDestroy()
        {
            if (chipBetting != null)
                chipBetting.OnBetChanged -= OnBetChangedHandler;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Public Audio API
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>Plays the exit sound and returns its length in seconds.</summary>
        public float PlayExitSound()
        {
            exitSound.Play(audioSource);
            return exitSound.Length;
        }

        /// <summary>Plays the exit sound without returning a duration. Used by menu close actions.</summary>
        public void PlayCloseSound() => exitSound.Play(audioSource);

        /// <summary>Plays the knock sound.</summary>
        public void PlayKnockSound()
        {
            knockSound.Play(audioSource);
        }

        /// <summary>
        /// Pulses <see cref="BetLimitStatusMessage"/> three times, then keeps it visible until the next round is dealt.
        /// </summary>
        public void NotifyBetLimitExceeded()
        {
            if (_betLimitStatusLocked || IsLimitPulsing) return;

            IsLimitPulsing = true;

            bool alreadyAtMax = chipBetting != null && chipBetting.TotalBet == chipBetting.MaxBet;

            if (!alreadyAtMax && chipBetting != null)
                chipBetting.SetBet(chipBetting.MaxBet);

            if (alreadyAtMax)
                knockSound.Play(audioSource);
            else
                resetSound.Play(audioSource);

            _limitPulseCoroutine = StartCoroutine(PulseLimitExceeded());
        }

        /// <summary>Clears the bet-limit status lock and pulse so the player can change chips again.</summary>
        public void ClearBetLimitStatus()
        {
            if (_limitPulseCoroutine != null)
            {
                StopCoroutine(_limitPulseCoroutine);
                _limitPulseCoroutine = null;
            }

            IsLimitPulsing        = false;
            _betLimitStatusLocked = false;

            if (_state == GameState.Idle || _state == GameState.RoundOver)
                SetStatus(CurrentBet > 0 ? "Press Deal to start" : "Place your bet");
        }

        private Coroutine _limitPulseCoroutine;

        private const int LimitPulseCount = 3;
        private const float LimitPulseDelay = 0.5f;
        private const string BetLimitStatusMessage = "Exceeding Limit, setting to Max";
        private bool _betLimitStatusLocked;

        /// <summary>True while the bet-limit pulse animation is running.</summary>
        public bool IsLimitPulsing { get; private set; }

        /// <summary>True while the bet-limit pulse is running or its message is locked until the next deal.</summary>
        public bool IsBetLimitStatusActive => IsLimitPulsing || _betLimitStatusLocked;

        private IEnumerator PulseLimitExceeded()
        {
            IsLimitPulsing = true;

            for (int i = 0; i < LimitPulseCount; i++)
            {
                SetStatus(BetLimitStatusMessage, LoseColor);
                yield return new WaitForSeconds(LimitPulseDelay);
                SetStatus(string.Empty, LoseColor);
                yield return new WaitForSeconds(LimitPulseDelay);
            }

            SetStatus(BetLimitStatusMessage, LoseColor);
            IsLimitPulsing        = false;
            _betLimitStatusLocked = true;
            _limitPulseCoroutine  = null;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Input
        // ──────────────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (controls != null && controls.DealOrHitPressed)
            {
                if (hitButton != null && hitButton.gameObject.activeSelf)
                    OnHit();
                else if (dealButton != null && dealButton.gameObject.activeSelf)
                    OnDeal();
            }
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Button Handlers
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>Starts a new round. Ensures a minimum bet of 1 chip and deducts the total bet from the player's balance.</summary>
        public void OnDeal()
        {
            if (IsLimitPulsing) return;
            if (_dealerNaturalBJPlaying) return;
            if (_state != GameState.Idle && _state != GameState.RoundOver) return;
            StopBlackjackCelebration();
            StartNewRound();
        }

        /// <summary>
        /// Ensures a minimum bet is placed, deducts it from the player's balance, and starts <see cref="DealRound"/>.
        /// All new-game entry points funnel through here.
        /// </summary>
        private void StartNewRound()
        {
            if (_doubleBJSoundPlaying) return;
            StopAllCoroutines();
            StopDoubleDownLayout();
            UnfreezeAreaLayout();
            IsLimitPulsing = false;
            _dealerNaturalBJPlaying = false;
            _dealerNaturalBJEndTime = 0f;

            // EndRound may have been interrupted before it could reset the bet after a win.
            ApplyPendingWinBetRestore();

            _martingaleWin = false;
            _playerWon = false;
            dealButton.gameObject.SetActive(false);
            menuController?.CloseMenu();
            _savedBetBeforeAction = 0;
            EnsureMinimumBet();

            // Martingale doubling may have hit the bet cap — let the limit pulse finish first.
            if (IsLimitPulsing)
            {
                dealButton.gameObject.SetActive(true);
                return;
            }

            _betLimitStatusLocked = false;

            chipBetting?.SnapshotBet();
            _playerMoney -= CurrentBet;
            RefreshMoneyLabel();
            _state = GameState.PlayerTurn;
            if (_inMartingaleMode)
                RefreshMartingaleModeLabel();
            StartCoroutine(DealRound());
        }

        /// <summary>
        /// Transitions to Idle if needed and places the smallest chip when <see cref="ChipBetting.TotalBet"/> is zero.
        /// </summary>
        private void EnsureMinimumBet()
        {
            if (chipBetting == null) return;

            if (_state == GameState.RoundOver)
            {
                // Capture the pre-double stake before Martingale raises the bet for the next round.
                if (_pendingMartingaleDouble && _betBeforeMartingale <= 0)
                    _betBeforeMartingale = chipBetting.TotalBet;

                PrepareForBetting();
            }

            if (chipBetting.TotalBet <= 0)
                chipBetting.PlaceSmallestChip();
        }

        /// <summary>Player draws another card.</summary>
        public void OnHit()
        {
            if (IsMenuBlocking) return;
            if (_state != GameState.PlayerTurn) return;
            ConfirmOrExecute(PlayerAction.Hit, () => StartCoroutine(PlayerHit()));
        }

        /// <summary>Player ends their turn; advances to split hand or dealer turn.</summary>
        public void OnStand()
        {
            if (IsMenuBlocking) return;
            if (_state != GameState.PlayerTurn) return;
            ConfirmOrExecute(PlayerAction.Stand, () => StartCoroutine(AdvanceOrDealerTurn()));
        }

        /// <summary>Player surrenders — forfeits half their bet and ends the round immediately.</summary>
        public void OnSurrender()
        {
            if (IsMenuBlocking) return;
            if (_state != GameState.PlayerTurn) return;
            if (ActiveHand.Cards.Count != 2) return;
            ConfirmOrExecute(PlayerAction.Surrender, () => StartCoroutine(PlayerSurrender()));
        }

        private IEnumerator PlayerSurrender()
        {
            _state = GameState.RoundOver;
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);

            yield return StartCoroutine(RevealHoleCard());
            UpdateScoreLabels(revealDealer: true);

            surrenderSound.Play(audioSource);
            RecordRoundOutcome(true, lostAmount: CurrentBet * 0.5m);
            SetStatus("Surrender returns 1/2 of bet", SurrenderColor);
            ApplyPayout(PayoutResult.Surrender, CurrentBet);

            yield return StartCoroutine(EndRound());
        }

        /// <summary>
        /// Splits the current two-card hand. Only available when both initial cards share the same rank.
        /// The split card moves to splitCardArea; each hand then receives one additional card stacked on top.
        /// </summary>
        public void OnSplit()
        {
            if (IsMenuBlocking) return;
            if (_state != GameState.PlayerTurn) return;
            if (!CanSplit()) return;
            ConfirmOrExecute(PlayerAction.Split, ExecuteSplit);
        }

        /// <summary>Performs all split setup (bet deduction, chip UI) and starts the split coroutine.</summary>
        private void ExecuteSplit()
        {
            _savedBetBeforeAction = CurrentBet;
            _playerMoney -= CurrentBet;
            RefreshMoneyLabel();
            chipBetting?.DoubleBetChips();
            StartCoroutine(PerformSplit());
        }

        /// <summary>
        /// Doubles down: player receives exactly one more card, then automatically stands.
        /// Only available on the initial two-card hand.
        /// </summary>
        public void OnDoubleDown()
        {
            if (IsMenuBlocking) return;
            if (_state != GameState.PlayerTurn) return;
            if (ActiveHand.Cards.Count != 2) return;
            ConfirmOrExecute(PlayerAction.Double, () => StartCoroutine(PerformDoubleDown()));
        }

        // ─── Strategy confirmation ─────────────────────────────────────────────────

        /// <summary>
        /// Evaluates <paramref name="chosenAction"/> against basic strategy.
        /// Correct actions execute immediately. Deviations show the popup:
        /// "Do Strategy" executes the recommended action; "Override" executes the player's chosen action.
        /// </summary>
        private void ConfirmOrExecute(PlayerAction chosenAction, Action executeChosen)
        {
            StrategyEvaluation evaluation = _strategyAdvisor.Evaluate(
                chosenAction, ActiveHand, _dealerUpcardSnapshot,
                canSplit: CanSplit(), canDouble: CanDoubleDown(), canSurrender: CanSurrender());

            if (evaluation.IsCorrect || deviationPopup == null || (menuController != null && menuController.IsStrategyOverrideEnabled))
            {
                executeChosen();
                return;
            }

            StrategyAction recommendation = evaluation.Recommendation;
            deviationPopup.Show(
                recommendation: recommendation.ToString(),
                onKeep:         () => ExecuteRecommendedAction(recommendation),
                onReconsider:   executeChosen);
        }

        private void ExecuteRecommendedAction(StrategyAction recommendation)
        {
            switch (recommendation)
            {
                case StrategyAction.Hit:
                    StartCoroutine(PlayerHit());
                    break;
                case StrategyAction.Stand:
                    StartCoroutine(AdvanceOrDealerTurn());
                    break;
                case StrategyAction.Double:
                    StartCoroutine(PerformDoubleDown());
                    break;
                case StrategyAction.Split:
                    ExecuteSplit();
                    break;
                case StrategyAction.Surrender:
                    StartCoroutine(PlayerSurrender());
                    break;
            }
        }

        /// <summary>Forces the next deal to give the player a natural blackjack, then starts the round.</summary>
        public void OnBlackjackTest()
        {
            if (_state != GameState.Idle && _state != GameState.RoundOver) return;
            StopBlackjackCelebration();
            DeactivateTestCheckboxes();
            _state = GameState.Idle;
            _forcePlayerBlackjack = true;
            StartNewRound();
        }

        /// <summary>Forces the next deal to give the player a matching pair of the rank chosen in the options slider.</summary>
        public void OnSplitTest()
        {
            if (_state != GameState.Idle && _state != GameState.RoundOver) return;
            StopBlackjackCelebration();
            DeactivateTestCheckboxes();
            _state = GameState.Idle;
            _forceSplitHand = true;
            StartNewRound();
        }

        /// <summary>Forces the next deal to give both player and dealer a natural blackjack, then starts the round.</summary>
        public void OnBothBlackjackTest()
        {
            if (_state != GameState.Idle && _state != GameState.RoundOver) return;
            StopBlackjackCelebration();
            DeactivateTestCheckboxes();
            _state = GameState.Idle;
            _forceBothBlackjack = true;
            StartNewRound();
        }

        /// <summary>Forces the next deal to give the dealer a natural blackjack, then starts the round.</summary>
        public void OnDealerBlackjackTest()
        {
            if (_state != GameState.Idle && _state != GameState.RoundOver) return;
            StopBlackjackCelebration();
            DeactivateTestCheckboxes();
            _forceBothBlackjack   = false;
            _forcePlayerBlackjack = false;
            _forceSplitHand       = false;
            _forceDoubleDownTest  = false;
            _state                = GameState.Idle;
            _forceDealerBlackjack = true;
            StartNewRound();
        }

        /// <summary>Forces the next deal to give the player a hard-11 two-card hand (random pair, e.g. 5+6 or 4+7), then starts the round.</summary>
        public void OnDoubleDownTest()
        {
            if (_state != GameState.Idle && _state != GameState.RoundOver) return;
            StopBlackjackCelebration();
            DeactivateTestCheckboxes();
            _state = GameState.Idle;
            _forceDoubleDownTest = true;
            StartNewRound();
        }

        /// <summary>Turns off "Always Lose" and "Override Strategy" before any test-button round.</summary>
        private void DeactivateTestCheckboxes() => menuController?.DisableTestCheckboxes();

        // ──────────────────────────────────────────────────────────────────────────
        // Round Flow
        // ──────────────────────────────────────────────────────────────────────────

        private IEnumerator DealRound()
        {
            _state = GameState.PlayerTurn;
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);

            _deck.Build();

            if (_forceBothBlackjack)   { _deck.ForceBothBlackjack();   _forceBothBlackjack   = false; }
            if (_forcePlayerBlackjack) { _deck.ForcePlayerBlackjack(); _forcePlayerBlackjack = false; }
            if (_forceSplitHand)
            {
                Rank selectedRank = (menuController != null)
                    ? (Rank)menuController.TestSplitRank
                    : Rank.Five;
                _deck.ForceSplitHandWithRank(selectedRank);
                _forceSplitHand = false;
            }
            if (_forceDoubleDownTest)  { _deck.ForceDoubleDownTest();  _forceDoubleDownTest  = false; }
            if (_forceDealerBlackjack) { _deck.ForceDealerBlackjack(); _forceDealerBlackjack = false; }

            ClearTable();
            SetStatus("");
            _doubleDownExtraBet = 0;
            yield return new WaitForSeconds(newRoundPause); //mark1
            //SetStatus("Dealing...");

            yield return StartCoroutine(DealCardTo(_playerHand, _playerCardViews, playerCardArea, faceUp: true));
            yield return StartCoroutine(DealCardTo(_dealerHand, _dealerCardViews, dealerCardArea, faceUp: true));
            yield return StartCoroutine(DealCardTo(_playerHand, _playerCardViews, playerCardArea, faceUp: true));
            yield return StartCoroutine(DealCardTo(_dealerHand, _dealerCardViews, dealerCardArea, faceUp: false));

            _dealerHoleCardView = _dealerCardViews[^1];
            UpdateScoreLabels(revealDealer: false);

            // ── Natural blackjack check (both dealer cards are already in the hand) ──
            bool playerNatural = IsNaturalBlackjack(_playerHand);
            bool dealerNatural = IsNaturalBlackjack(_dealerHand);

            if (playerNatural && dealerNatural)
            {
                yield return StartCoroutine(RevealDealerHoleForNaturalBlackjack());

                if (AlwaysLose)
                {
                    // Always Lose: treat double-BJ push as a loss.
                    PlayLoseSound();
                    RecordRoundOutcome(true, lostAmount: CurrentBet, scoreDelta: -1);
                    SetStatus("You lose", LoseColor);
                    ApplyPayout(PayoutResult.Lose, CurrentBet);
                }
                else
                {
                    StartCoroutine(PlayDoubleBJSoundRoutine());
                    RecordRoundOutcome(false, scoreDelta: 0, isPush: true);
                    SetStatus("Push", PushColor);
                    ApplyPayout(PayoutResult.Push, CurrentBet);
                }

                yield return StartCoroutine(EndRound());
                yield break;
            }

            if (playerNatural)
            {
                yield return StartCoroutine(RevealDealerHoleForNaturalBlackjack());

                if (AlwaysLose)
                {
                    // Always Lose: treat a natural blackjack as a loss.
                    PlayLoseSound();
                    RecordRoundOutcome(true, lostAmount: CurrentBet, scoreDelta: -1);
                    SetStatus("You lose", LoseColor);
                    ApplyPayout(PayoutResult.Lose, CurrentBet);
                }
                else
                {
                    ApplyBlackjackGlow();
                    SpawnFireworks(PlayNaturalBlackjackSound());
                    RecordRoundOutcome(false, scoreDelta: +1);
                    _playerWon = true;
                    SetStatus(_martingaleWin ? "Won with Martingale" : "You win", WinColor);
                    ApplyPayout(PayoutResult.BlackjackWin, CurrentBet);
                }

                yield return StartCoroutine(EndRound());
                yield break;
            }

            if (dealerNatural)
            {
                if (_dealerHoleCardView != null && !_dealerHoleCardView.IsFaceUp)
                    yield return StartCoroutine(RevealDealerHoleForNaturalBlackjack());
                else
                    UpdateScoreLabels(revealDealer: true);

                yield return StartCoroutine(ApplyDealerNaturalBlackjackLossRoutine(CurrentBet, revealHole: false));
                yield return StartCoroutine(EndRound());
                yield break;
            }

            // ── Player turn ──
            SetButtonState(dealEnabled: false, actionEnabled: true, splitEnabled: CanSplit(), doubleDownEnabled: CanDoubleDown(), surrenderEnabled: true);
            SetStatus($"Your turn");

            _dealerUpcardSnapshot = _dealerHand.Cards[0];
            bool hasPair = CanSplit();

            // Highlight the recommended cell in the strategy table (if visible).
            strategyTableUI?.HighlightRecommendation(
                _playerHand, _dealerUpcardSnapshot,
                canSplit: hasPair, canDouble: CanDoubleDown(), canSurrender: true);

            if (!hasPair && _playerHand.BestValue() <= AutoHitMaxScore)
            {
                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(AutoHitLoop());
                yield break;
            }

            bool shouldStand = ShouldAutoStand(_playerHand);

            if (shouldStand)
            {
                knockSound.Play(audioSource);
                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(DealerTurn());
            }
        }

        // ── Split ─────────────────────────────────────────────────────────────────

        private bool CanSplit() =>
            !_isSplitRound
            && _playerHand.Count == 2
            && _playerHand.Cards[0].Rank == _playerHand.Cards[1].Rank;

        private bool CanSurrender() =>
            ActiveHand.Count == 2 && !_isSplitRound;

        private IEnumerator PerformSplit()
        {
            _isSplitRound = true;
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);

            // Move card[1] from player hand to split hand
            CardData movedCard = _playerHand.Cards[1];
            _playerHand.RemoveAt(1);

            CardView movedView = _playerCardViews[1];
            _playerCardViews.RemoveAt(1);

            // Move card[1] to split card area
            movedView.transform.SetParent(splitCardArea, worldPositionStays: false);
            _splitCardViews.Add(movedView);
            _splitHand.AddCard(movedCard);

            cardSlideSound.Play(audioSource);
            yield return new WaitForSeconds(0.5f);

            bool isAces = _playerHand.Cards[0].Rank == Rank.Ace;

            // Deal second card to each hand
            yield return StartCoroutine(DealCardTo(_playerHand,  _playerCardViews, playerCardArea, faceUp: true));
            yield return StartCoroutine(DealCardTo(_splitHand,   _splitCardViews,  splitCardArea,  faceUp: true));

            UpdateScoreLabels(revealDealer: false);
            _activeHandIndex = 0;

            if (isAces)
            {
                SetStatus("Split Aces — one card each. Standing.");
                yield return new WaitForSeconds(0.5f);
                yield return StartCoroutine(DealerTurn());
                yield break;
            }

            SetButtonState(dealEnabled: false, actionEnabled: true, splitEnabled: false, doubleDownEnabled: CanDoubleDown());
            SetStatus($"Players turn Hand 1");

            if (ActiveHand.BestValue() <= AutoHitMaxScore)
            {
                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(AutoHitLoop());
                yield break;
            }

            if (ShouldAutoStand(ActiveHand))
            {
                knockSound.Play(audioSource);
                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(AdvanceOrDealerTurn());
            }
        }

        // ── Double Down ───────────────────────────────────────────────────────────

        private bool CanDoubleDown() =>
            ActiveHand.Cards.Count == 2;

        private IEnumerator PerformDoubleDown()
        {
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);

            // Status is set after the deal so a deviation message shown just before
            // this coroutine starts stays visible during the deal animation.
            _savedBetBeforeAction = CurrentBet;
            _doubleDownExtraBet = CurrentBet;
            _playerMoney -= _doubleDownExtraBet;
            RefreshMoneyLabel();
            chipBetting?.DoubleBetChips();
            ddSound.Play(audioSource); //mark dd sound
            yield return new WaitForSeconds(doubleDownBetPause);
            yield return StartCoroutine(
                DealCardTo(ActiveHand, ActiveViews,
                           _activeHandIndex == 0 ? playerCardArea : splitCardArea,
                           faceUp: true,
                           doubleDownPlacement: true));

            SetStatus("Double Down!");

            UpdateScoreLabels(revealDealer: false);

            if (ActiveHand.IsBust())
            {
                yield return StartCoroutine(RevealHoleCard());
                UpdateScoreLabels(revealDealer: true);
                if (IsDealerNaturalBlackjackLoss(_playerHand))
                {
                    yield return StartCoroutine(
                        ApplyDealerNaturalBlackjackLossRoutine(CurrentBet + _doubleDownExtraBet));
                    SetStatus("Busted");
                    yield return StartCoroutine(EndRound());
                    yield break;
                }

                PlayLoseSound();
                RecordRoundOutcome(true, lostAmount: CurrentBet + _doubleDownExtraBet, scoreDelta: -1);
                SetStatus($"Busted");
                yield return StartCoroutine(EndRound());
                yield break;
            }

            SetStatus($"Double Down stands at {ActiveHand.BestValue()}");
            yield return new WaitForSeconds(dealerPauseDelay);
            yield return StartCoroutine(AdvanceOrDealerTurn());
        }

        private IEnumerator AdvanceOrDealerTurn()
        {
            if (_isSplitRound && _activeHandIndex == 0)
            {
                _activeHandIndex = 1;
                UpdateScoreLabels(revealDealer: false);
                SetStatus($"Players turn Hand 2");
                SetButtonState(dealEnabled: false, actionEnabled: true, splitEnabled: false, doubleDownEnabled: CanDoubleDown());

                if (ActiveHand.BestValue() <= AutoHitMaxScore)
                {
                    yield return new WaitForSeconds(0.3f);
                    yield return StartCoroutine(AutoHitLoop());
                }
                else if (ShouldAutoStand(ActiveHand))
                {
                    knockSound.Play(audioSource);
                    yield return new WaitForSeconds(0.3f);
                    yield return StartCoroutine(DealerTurn());
                }
            }
            else
            {
                yield return StartCoroutine(DealerTurn());
            }
        }

        /// <summary>Automatically hits until the score exceeds AutoHitMaxScore, then returns control to the player or proceeds with auto-stand logic.</summary>
        private IEnumerator AutoHitLoop()
        {
            while (ActiveHand.BestValue() <= AutoHitMaxScore)
            {
                yield return StartCoroutine(PlayerHit());

                int score = ActiveHand.BestValue();
                if (score > BlackjackValue || score == BlackjackValue || ShouldAutoStand(ActiveHand))
                    yield break;
            }
        }

        private IEnumerator PlayerHit()
        {
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);

            Transform area = (_isSplitRound && _activeHandIndex == 1) ? splitCardArea : playerCardArea;
            yield return StartCoroutine(DealCardTo(ActiveHand, ActiveViews, area, faceUp: true));
            UpdateScoreLabels(revealDealer: false);

            strategyTableUI?.HighlightRecommendation(
                ActiveHand, _dealerUpcardSnapshot,
                canSplit: false, canDouble: CanDoubleDown(), canSurrender: CanSurrender());

            int score = ActiveHand.BestValue();

            if (score > BlackjackValue)
            {
                
              string label = _isSplitRound ? $"Hand {_activeHandIndex + 1} busts" : "Bust!";
              SetStatus($"{label}", LoseColor);
              PlayLoseSound();

                if (_isSplitRound)
                {
                    // Always advance to next hand or dealer turn so both hands get resolved.
                    yield return new WaitForSeconds(0.5f);
                    yield return StartCoroutine(AdvanceOrDealerTurn());
                }
                else
                {
                    RecordRoundOutcome(true, lostAmount: CurrentBet, scoreDelta: -1);
                    yield return StartCoroutine(RevealHoleCard());
                    UpdateScoreLabels(revealDealer: true);
                    if (IsDealerNaturalBlackjackLoss(_playerHand))
                        yield return StartCoroutine(ApplyDealerNaturalBlackjackPresentationRoutine());
                    yield return StartCoroutine(EndRound());
                }
                yield break;
            }

            if (score == BlackjackValue || ShouldAutoStand(ActiveHand))
            {
                if (score != BlackjackValue)
                    knockSound.Play(audioSource);
                yield return new WaitForSeconds(0.25f);
                yield return StartCoroutine(AdvanceOrDealerTurn());
                yield break;
            }

            if (score <= AutoHitMaxScore)
            {
                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(PlayerHit());
                yield break;
            }

            SetButtonState(dealEnabled: false, actionEnabled: true, splitEnabled: false);
            SetStatus(_isSplitRound
                ? $"Players turn Hand 1"
                : $"Your turn");
        }

        private IEnumerator DealerTurn()
        {
            _state = GameState.DealerTurn;
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);
            StopAllScorePulses();

            yield return StartCoroutine(RevealHoleCard());
            UpdateScoreLabels(revealDealer: true);

            if (IsDealerNaturalBlackjackLoss(_playerHand))
            {
                yield return StartCoroutine(
                    ApplyDealerNaturalBlackjackLossRoutine(CurrentBet + _doubleDownExtraBet));
                yield return StartCoroutine(EndRound());
                yield break;
            }

            // If both split hands busted, skip dealer drawing.
            bool allPlayerHandsBusted = _isSplitRound
                ? _playerHand.IsBust() && _splitHand.IsBust()
                : _playerHand.IsBust();

            if (!allPlayerHandsBusted)
            {
                SetStatus("Dealer's turn");
                yield return new WaitForSeconds(dealerPauseDelay);

                while (ShouldDealerHit())
                {
                    yield return StartCoroutine(DealCardTo(_dealerHand, _dealerCardViews, dealerCardArea, faceUp: true));
                    UpdateScoreLabels(revealDealer: true);
                    yield return new WaitForSeconds(dealerPauseDelay);
                }
            }

            yield return StartCoroutine(ResolveRound());
        }

        /// <summary>
        /// Dealer hits below 17 and stands on 17 or higher (including soft 17).
        /// </summary>
        private bool ShouldDealerHit()
        {
            return _dealerHand.BestValue() < DealerSoft17;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Money / Payout
        // ──────────────────────────────────────────────────────────────────────────

        private static readonly System.Globalization.CultureInfo GermanCulture =
            System.Globalization.CultureInfo.GetCultureInfo("de-DE");

        private enum PayoutResult { Win, BlackjackWin, Lose, Push, Surrender }

        /// <summary>
        /// Receives live bet delta from ChipBetting. Money is deducted at deal time,
        /// so this handler only refreshes the label to reflect any UI-only changes.
        /// </summary>
        private void OnBetChangedHandler(int delta)
        {
            RefreshMoneyLabel();

            if (!IsBettingAllowed) return;

            if (IsLimitPulsing || _betLimitStatusLocked) return;

            if (CurrentBet == 0)
            {
                SetStatus("Place your bet");
                return;
            }

            if (_consecutiveLosses >= DelayedMartingaleThreshold && CurrentBet > _lastRoundBet)
            {
                SetStatus("Doing Delayed Martingale", WinColor);
                return;
            }

            // A chip was just added — prompt the player to deal.
            if (delta > 0 && statusLabel.text == "Place your bet")
                SetStatus("Press Deal to start");
        }

        /// <summary>
        /// Applies end-of-round payout. The bet was deducted at deal time,
        /// so payouts return the appropriate amount to the balance.
        /// Win → bet×2 | BJ → bet×2.5 | Push → bet | Surrender → bet×0.5 | Lose → 0
        /// </summary>
        private void ApplyPayout(PayoutResult result, int bet)
        {
            _playerMoney += result switch
            {
                PayoutResult.Win          => bet * 2m,
                PayoutResult.BlackjackWin => bet * 2.5m,
                PayoutResult.Push         => bet,
                PayoutResult.Surrender    => bet * 0.5m,
                _                         => 0,                   // Lose — bet already gone
            };
            RefreshMoneyLabel();
        }

        private void RefreshMoneyLabel()
        {
            if (playerMoneyLabel == null) return;
            playerMoneyLabel.text = $"€ {((decimal)_playerMoney).ToString("N2", GermanCulture)}";
        }

        private int CurrentBet => chipBetting != null ? chipBetting.TotalBet : 0;

        /// <summary>
        /// Records whether the round was a net loss (bust, lose, surrender) or not.
        /// Maintains <see cref="_consecutiveLosses"/>, <see cref="_totalLosses"/>, <see cref="_totalAmountLost"/>,
        /// <see cref="_playerScore"/>, and snapshots the bet for Delayed Martingale detection.
        /// <paramref name="lostAmount"/> is the monetary amount forfeited this round (full bet for a loss, half for surrender, 0 for win/push).
        /// <paramref name="scoreDelta"/> is +1 for a win, -1 for a loss, 0 for push or surrender.
        /// <paramref name="isPush"/> when true, leaves the loss streak and all Martingale state completely unchanged — a push is neutral.
        /// <paramref name="isMartingaleNeutral"/> when true, leaves all Martingale and streak state completely unchanged (used for split rounds with no net score change).
        /// </summary>
        private void RecordRoundOutcome(bool isLoss, decimal lostAmount = 0, int scoreDelta = 0, bool isPush = false, bool isMartingaleNeutral = false)
        {
            Debug.Log($"[Martingale] RecordRoundOutcome called: isLoss={isLoss} isPush={isPush} isMartingaleNeutral={isMartingaleNeutral} AlwaysLose={AlwaysLose}");

            _lastRoundBet  = CurrentBet + _doubleDownExtraBet;
            _playerScore  += scoreDelta;

            // Split rounds where the player's score is unchanged leave the Martingale counter exactly as-is.
            if (isMartingaleNeutral)
            {
                RefreshStreakLabel();
                return;
            }

            // When AlwaysLose is active every round counts as a loss for Martingale tracking,
            // regardless of the actual card outcome.
            if (AlwaysLose)
            {
                isLoss    = true;
                isPush    = false;
                if (lostAmount == 0) lostAmount = _lastRoundBet;
            }

            if (isLoss)
            {
                _consecutiveLosses++;
                _totalLosses++;
                _totalAmountLost += lostAmount;

                bool thresholdReached = EffectiveMartingaleThreshold > 0 && _consecutiveLosses >= EffectiveMartingaleThreshold;

                Debug.Log($"[Martingale] Loss recorded: losses={_consecutiveLosses} threshold={EffectiveMartingaleThreshold} thresholdReached={thresholdReached} inMode={_inMartingaleMode} declined={_martingaleDeclined}");

                if (_inMartingaleMode)
                {
                    // Already in Martingale — schedule a bet double for the next betting phase.
                    _pendingMartingaleDouble = true;
                }
                else if (thresholdReached && !_martingaleDeclined)
                {
                    // Threshold just reached — arm the popup for EndRound.
                    _martingalePopupShown = true;
                }
            }
            else if (isPush)
            {
                // A push is neutral — the loss streak and all Martingale state are left unchanged.
            }
            else
            {
                // Player won — exit Martingale mode and schedule the minimum-bet reset for EndRound.
                if (_inMartingaleMode)
                    _martingaleWin = true;

                _consecutiveLosses       = 0;
                _totalAmountLost         = 0;
                _totalLosses             = 0;
                _martingalePopupShown    = false;
                _inMartingaleMode        = false;
                _martingaleDeclined      = false;
                _pendingMartingaleDouble = false;
            }
            RefreshStreakLabel();
        }

        /// <summary>
        /// Keeps player info labels on the same horizontal axis with equal vertical spacing between all four rows.
        /// </summary>
        private void AlignPlayerInfoLabels()
        {
            if (playerMoneyLabel == null) return;

            Transform canvas = playerMoneyLabel.transform.parent;
            if (canvas == null) return;

            EnsureMartingaleModeLabel();
            if (martingaleModeLabel != null)
            {
                martingaleModeLabel.text = "Martingale Mode";
                ApplyMartingaleModeLabelTypography(martingaleModeLabel);
            }

            TextMeshProUGUI playerLabel = canvas.Find("PlayerLabel")?.GetComponent<TextMeshProUGUI>();
            AlignLabelToPlayerMoney(playerLabel);
            AlignLabelToPlayerMoney(playerMoneyLabel);
            AlignLabelToPlayerMoney(streakLabel);
            AlignLabelToPlayerMoney(martingaleModeLabel);
            AlignLabelToPlayerMoney(canvas.Find("DealerLabel")?.GetComponent<TextMeshProUGUI>());

            float width = playerMoneyLabel.rectTransform.sizeDelta.x;
            List<TextMeshProUGUI> rows = CollectVisiblePlayerInfoRows(playerLabel);
            if (rows.Count == 0) return;

            float rowHeight = ComputePlayerInfoRowHeight(rows);
            for (int i = 0; i < rows.Count; i++)
                FitPlayerInfoRow(rows[i], width, rowHeight);

            Vector2 topPos = rows[0].rectTransform.anchoredPosition;
            topPos.y       = playerInfoTopY;
            rows[0].rectTransform.anchoredPosition = topPos;

            for (int i = 1; i < rows.Count; i++)
                StackLabelBelow(rows[i], rows[i - 1], playerInfoLabelGap);

            martingaleModeLabel?.rectTransform.SetAsLastSibling();
        }

        private List<TextMeshProUGUI> CollectVisiblePlayerInfoRows(TextMeshProUGUI playerLabel)
        {
            var rows = new List<TextMeshProUGUI>(4);
            if (playerLabel != null) rows.Add(playerLabel);
            if (playerMoneyLabel != null) rows.Add(playerMoneyLabel);
            if (streakLabel != null && streakLabel.gameObject.activeSelf) rows.Add(streakLabel);
            if (martingaleModeLabel != null && IsMartingaleModeActive) rows.Add(martingaleModeLabel);
            return rows;
        }

        private static float ComputePlayerInfoRowHeight(IReadOnlyList<TextMeshProUGUI> rows)
        {
            float height = 0f;
            for (int i = 0; i < rows.Count; i++)
                height = Mathf.Max(height, MeasureLabelHeight(rows[i]));
            return height;
        }

        private static void FitPlayerInfoRow(TextMeshProUGUI label, float width, float rowHeight)
        {
            if (label == null) return;

            label.rectTransform.sizeDelta = new Vector2(width, rowHeight);
            label.verticalAlignment       = VerticalAlignmentOptions.Bottom;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || playerMoneyLabel == null)
                return;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || playerMoneyLabel == null)
                    return;

                AlignPlayerInfoLabels();
            };
        }
#endif

        private static void StackLabelBelow(TextMeshProUGUI lower, TextMeshProUGUI upper, float gap)
        {
            if (lower == null || upper == null) return;

            RectTransform lowerRT = lower.rectTransform;
            RectTransform upperRT = upper.rectTransform;

            lowerRT.anchorMin = upperRT.anchorMin;
            lowerRT.anchorMax = upperRT.anchorMax;
            lowerRT.pivot     = upperRT.pivot;

            Vector2 pos = lowerRT.anchoredPosition;
            pos.y       = upperRT.anchoredPosition.y - gap - lowerRT.sizeDelta.y;
            lowerRT.anchoredPosition = pos;
        }

        private static float MeasureLabelHeight(TextMeshProUGUI label)
        {
            if (label == null) return 0f;
            label.ForceMeshUpdate(true, true);
            Bounds bounds = label.textBounds;
            if (bounds.size.y > 0.01f)
                return bounds.size.y;
            return label.preferredHeight;
        }

        private void AlignLabelToPlayerMoney(TextMeshProUGUI label)
        {
            if (label == null || playerMoneyLabel == null) return;

            RectTransform moneyRT = playerMoneyLabel.rectTransform;
            RectTransform labelRT = label.rectTransform;

            label.horizontalAlignment = HorizontalAlignmentOptions.Center;
            label.margin              = playerMoneyLabel.margin;

            Vector2 pos = labelRT.anchoredPosition;
            pos.x       = moneyRT.anchoredPosition.x;
            labelRT.anchoredPosition = pos;

            Vector2 size = labelRT.sizeDelta;
            size.x       = moneyRT.sizeDelta.x;
            labelRT.sizeDelta = size;
        }

        /// <summary>Updates the streak label to show the current loss streak, total amount lost, and when in Martingale mode, the chip amount being added each round.</summary>
        private void RefreshStreakLabel()
        {
            if (streakLabel == null) return;
            if (_consecutiveLosses > 0)
            {
                //string text = $"Lost: {_consecutiveLosses} times in row  |  -€ {_totalAmountLost:N2}";
                string text = $"Lost: {(int)System.Math.Ceiling(_consecutiveLosses)} times in row";
                streakLabel.text = text;
                streakLabel.gameObject.SetActive(true);
            }
            else
            {
                streakLabel.gameObject.SetActive(false);
            }

            RefreshMartingaleModeLabel();
        }

        private void RefreshMartingaleModeLabel()
        {
            EnsureMartingaleModeLabel();
            if (martingaleModeLabel == null) return;

            bool showLabel = _inMartingaleMode;
            if (!showLabel)
            {
                martingaleModeLabel.gameObject.SetActive(false);
                AlignPlayerInfoLabels();
                return;
            }

            martingaleModeLabel.gameObject.SetActive(true);
            martingaleModeLabel.text = "Martingale Mode";
            ApplyMartingaleModeLabelTypography(martingaleModeLabel);
            AlignPlayerInfoLabels();
            martingaleModeLabel.alpha = 1f;
            martingaleModeLabel.ForceMeshUpdate(true, true);
        }

        private bool IsMartingaleModeActive => _inMartingaleMode;

        private void EnsureMartingaleModeLabel()
        {
            if (martingaleModeLabel != null || streakLabel == null) return;

            GameObject labelObj = new("Martingale Mode", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(streakLabel.transform.parent, false);

            martingaleModeLabel = labelObj.GetComponent<TextMeshProUGUI>();
            ApplyMartingaleModeLabelTypography(martingaleModeLabel);
            martingaleModeLabel.text                = "Martingale Mode";
            martingaleModeLabel.raycastTarget       = false;
            martingaleModeLabel.horizontalAlignment = HorizontalAlignmentOptions.Center;
            martingaleModeLabel.verticalAlignment   = VerticalAlignmentOptions.Middle;
            martingaleModeLabel.enableWordWrapping  = false;
            martingaleModeLabel.margin              = streakLabel.margin;

            labelObj.SetActive(false);
        }

        private void ApplyMartingaleModeGoldColor(TextMeshProUGUI target)
        {
            if (target == null) return;

            target.color = MartingaleModeGoldColor;
            if (statusLabel != null)
            {
                target.outlineWidth = statusLabel.outlineWidth;
                target.outlineColor = statusLabel.outlineColor;
            }
        }

        private void ApplyMartingaleModeLabelTypography(TextMeshProUGUI target)
        {
            if (target == null || statusLabel == null) return;

            target.font = statusLabel.font;
            if (statusLabel.fontSharedMaterial != null)
                target.fontSharedMaterial = statusLabel.fontSharedMaterial;
            target.fontSize  = martingaleModeFontSize;
            target.fontStyle = statusLabel.fontStyle;

            target.enableVertexGradient  = false;
            target.horizontalAlignment = HorizontalAlignmentOptions.Center;
            target.verticalAlignment   = VerticalAlignmentOptions.Bottom;

            ApplyMartingaleModeGoldColor(target);

            if (target.font != null)
                target.UpdateFontAsset();
        }

        private void ApplyStatusLabelTypography(TextMeshProUGUI target)
        {
            target.font               = statusLabel.font;
            target.fontSharedMaterial = statusLabel.fontSharedMaterial;
            target.fontSize           = statusLabel.fontSize;
            target.fontStyle          = statusLabel.fontStyle;
            target.color              = statusLabel.color;
            target.UpdateFontAsset();
        }

        /// <summary>Creates the Martingale mode indicator beneath the streak label, using StatusLabel typography.</summary>
        private void InitMartingaleModeLabel()
        {
            EnsureMartingaleModeLabel();
            if (martingaleModeLabel != null)
                martingaleModeLabel.gameObject.SetActive(false);
        }

        private IEnumerator ResolveRound()
        {
            if (IsNaturalBlackjack(_dealerHand))
            {
                yield return StartCoroutine(ResolveDealerNaturalBlackjackRound());
                yield break;
            }

            int dealerScore = _dealerHand.BestValue();
            bool dealerBust = dealerScore > BlackjackValue;

            // When AlwaysLose is enabled, override the outcome: treat it as a dealer win.
            if (AlwaysLose)
            {
                dealerBust  = false;
                dealerScore = BlackjackValue; // dealer at 21 — beats any non-bust player hand
            }

            if (_isSplitRound)
            {
                var    results = new List<string>();
                bool   anyWin  = false;
                bool   anyLoss = false;
                bool   anyPush = false;
                int    splitLostAmount = 0;

                Hand[]   hands  = { _playerHand, _splitHand };
                string[] labels = { "Hand 1", "Hand 2" };

                for (int i = 0; i < hands.Length; i++)
                {
                    int s = hands[i].BestValue();
                    int handBet = CurrentBet / 2;

          if (s > BlackjackValue)
                    {
                        results.Add(ColorizeText($"{labels[i]}: Bust", LoseColor));
                        anyLoss = true;
                        splitLostAmount += handBet;
                        ApplyPayout(PayoutResult.Lose, handBet);
                    }
                    else if (s == BlackjackValue || dealerBust || s > dealerScore)
                    {
                        results.Add(ColorizeText($"{labels[i]}: Win", WinColor));
                        anyWin = true;
                        ApplyPayout(PayoutResult.Win, handBet);
                    }
                    else if (s < dealerScore)
                    {
                        results.Add(ColorizeText($"{labels[i]}: Lose", LoseColor));
                        anyLoss = true;
                        splitLostAmount += handBet;
                        ApplyPayout(PayoutResult.Lose, handBet);
                    }
                    else
                    {
                        if (AlwaysLose)
                        {
                            // Always Lose: treat per-hand push as a loss.
                            results.Add(ColorizeText($"{labels[i]}: Lose", LoseColor));
                            anyLoss = true;
                            splitLostAmount += handBet;
                            ApplyPayout(PayoutResult.Lose, handBet);
                        }
                        else
                        {
                            results.Add(ColorizeText($"{labels[i]}: Push", PushColor));
                            anyPush = true;
                            ApplyPayout(PayoutResult.Push, handBet);
                        }
                    }
                }

                if (anyWin)       { PlayWinRoutine(); _playerWon = true; }
                else if (anyLoss) PlayLoseSound();
                else              PlayTieSound();

                // Split 1W/1L or 1W/1Push counts as a push for the Martingale counter — streak is neither incremented nor reset.
                bool splitPush = (anyWin && anyLoss) || (anyWin && anyPush && !anyLoss);
                int  splitScoreDelta = anyWin && !anyLoss ? +1 : anyLoss && !anyWin ? -1 : 0;
                // If the split produced no net score change, leave the Martingale counter completely untouched.
                bool splitNeutral = splitScoreDelta == 0;
                RecordRoundOutcome(isLoss: anyLoss && !anyWin, lostAmount: splitLostAmount,
                    scoreDelta: splitScoreDelta,
                    isPush: !anyWin && !anyLoss || splitPush,
                    isMartingaleNeutral: splitNeutral);
                SetStatus(string.Join("  |  ", results));
            }
            else
            {
                int p = _playerHand.BestValue();
                // For losses/pushes the full staked amount (bet area + extra double-down deduction) is at risk.
                int totalBet = CurrentBet + _doubleDownExtraBet;
                // For wins, pay out based on what is visible in the bet area only (CurrentBet).
                // On a double-down CurrentBet already equals twice the original stake.
                int winBet = CurrentBet;
                if      (IsNaturalBlackjack(_playerHand)) { PlayWinRoutine();  RecordRoundOutcome(false, scoreDelta: +1); _playerWon = true; SetStatus(_martingaleWin ? "Won with Martingale" : "You win", WinColor);  ApplyPayout(PayoutResult.BlackjackWin, winBet); }
                else if (dealerBust)                    { PlayWinRoutine();  RecordRoundOutcome(false, scoreDelta: +1); _playerWon = true; SetStatus(_martingaleWin ? "Won with Martingale" : "You win", WinColor);  ApplyPayout(PayoutResult.Win,  winBet); }
                else if (p > dealerScore)               { PlayWinRoutine();  RecordRoundOutcome(false, scoreDelta: +1); _playerWon = true; SetStatus(_martingaleWin ? "Won with Martingale" : "You win", WinColor);  ApplyPayout(PayoutResult.Win,  winBet); }
                else if (dealerScore > p)               { PlayLoseSound(); RecordRoundOutcome(true, lostAmount: totalBet, scoreDelta: -1);  SetStatus($"You lose",LoseColor); ApplyPayout(PayoutResult.Lose, totalBet); }
                else
                {
                    if (AlwaysLose)
                    {
                        // Always Lose: treat push as a loss.
                        PlayLoseSound();
                        RecordRoundOutcome(true, lostAmount: totalBet, scoreDelta: -1);
                        SetStatus("You lose", LoseColor);
                        ApplyPayout(PayoutResult.Lose, totalBet);
                    }
                    else
                    {
                        PlayTieSound();
                        RecordRoundOutcome(false, scoreDelta: 0, isPush: true);
                        SetStatus("Push", PushColor);
                        ApplyPayout(PayoutResult.Push, totalBet);
                    }
                }
            }

            yield return StartCoroutine(EndRound());
        }

        /// <summary>
        /// After a win in Martingale mode, resets the bet area to the stake from before Martingale began.
        /// Returns true when the bet was restored.
        /// </summary>
        private bool ApplyPendingWinBetRestore()
        {
            if (!_martingaleWin || chipBetting == null)
                return false;

            int targetBet = _betBeforeMartingale > 0
                ? _betBeforeMartingale
                : chipBetting.SmallestChipValue;

            chipBetting.SetBet(targetBet, playSound: true);
            chipBetting.SnapshotBet();
            _betBeforeMartingale = 0;
            _martingaleWin       = false;
            _playerWon           = false;
            return true;
        }

        private IEnumerator EndRound()
        {
            _state = GameState.RoundOver;
            SetButtonState(dealEnabled: false, actionEnabled: false, splitEnabled: false);
            strategyTableUI?.ClearHighlight();

            bool restoredPreMartingaleBet = ApplyPendingWinBetRestore();

            yield return new WaitForSeconds(endRoundDelay);
            chipBetting?.ResetMaxBet();
            chipBetting?.ClampBetToMaxBet();

            if (!restoredPreMartingaleBet)
                chipBetting?.RestoreBetFromSnapshot();

            // If fireworks or dealer natural-blackjack presentation is still playing, wait before
            // letting the player interact again.
            float remaining = _fireworksEndTime - Time.time;
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);

            remaining = _dealerNaturalBJEndTime - Time.time;
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);

            if (!_doubleBJSoundPlaying && !_dealerNaturalBJPlaying && _state == GameState.RoundOver)
                SetButtonState(dealEnabled: true, actionEnabled: false, splitEnabled: false);

            if (_inMartingaleMode)
                RefreshMartingaleModeLabel();

            // Show Martingale popup immediately when the threshold was just reached.
            if (_martingalePopupShown && !_inMartingaleMode && !_martingaleDeclined)
            {
                Debug.Log("[Martingale] EndRound: showing popup");
                _martingalePopupShown = false;
                ShowMartingalePopup();
                yield break;
            }

            Debug.Log($"[Martingale] EndRound: popup NOT shown. shown={_martingalePopupShown} inMode={_inMartingaleMode} declined={_martingaleDeclined}");

            // Auto-deal the next round when already in Martingale mode and auto-play is enabled.
            if (_inMartingaleMode && _pendingMartingaleDouble && (menuController?.IsMartingaleAutoPlay ?? false))
                OnDeal();
            // State stays RoundOver — chip click or Deal press drives the next transition.
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Card Dealing
        // ──────────────────────────────────────────────────────────────────────────

        private IEnumerator DealCardTo(
            Hand hand, List<CardView> views, Transform area, bool faceUp, bool doubleDownPlacement = false)
        {
            yield return new WaitForSeconds(dealDelay);

            CardData card = _deck.Draw();

            if (dealCardSound.HasClip && audioSource != null)
                dealCardSound.Play(audioSource);

            if (doubleDownPlacement)
            {
                FreezeAreaLayout(area);
                for (int i = 0; i < views.Count; i++)
                    EnsureIgnoreLayout(views[i].GetComponent<RectTransform>());
            }

            CardView view = SpawnCardView(card, area, faceUp: false, ignoreLayout: doubleDownPlacement);
            if (view == null)
                yield break;

            hand.AddCard(card);
            views.Add(view);

            if (doubleDownPlacement && views.Count >= 3)
                ApplyDoubleDownLayout(views[0], views[1], view);

            if (faceUp)
            {
                bool flipDone = false;
                view.Flip(toFaceUp: true, () => flipDone = true);
                yield return new WaitUntil(() => flipDone);
            }

            if (doubleDownPlacement && views.Count >= 3)
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                ApplyDoubleDownLayout(views[0], views[1], view);
            }
        }

        private void FreezeAreaLayout(Transform area)
        {
            UnfreezeAreaLayout();
            if (area == null) return;

            _frozenLayoutGroup = area.GetComponent<HorizontalLayoutGroup>();
            if (_frozenLayoutGroup != null)
                _frozenLayoutGroup.enabled = false;
        }

        private void UnfreezeAreaLayout()
        {
            if (_frozenLayoutGroup != null)
            {
                _frozenLayoutGroup.enabled = true;
                _frozenLayoutGroup = null;
            }
        }

        private void ApplyDoubleDownLayout(
            CardView firstCard, CardView secondCard, CardView doubleDownCard)
        {
            if (firstCard == null || secondCard == null || doubleDownCard == null)
                return;

            RectTransform firstRt  = firstCard.GetComponent<RectTransform>();
            RectTransform secondRt = secondCard.GetComponent<RectTransform>();
            RectTransform thirdRt  = doubleDownCard.GetComponent<RectTransform>();

            EnsureIgnoreLayout(thirdRt);
            PlaceDoubleDownCard(firstRt, secondRt, thirdRt);

            _doubleDownFirst  = firstRt;
            _doubleDownSecond = secondRt;
            _doubleDownThird  = thirdRt;
            StartDoubleDownLayoutMaintenance();
        }

        private static void PlaceDoubleDownCard(
            RectTransform firstRt, RectTransform secondRt, RectTransform thirdRt)
        {
            if (firstRt == null || secondRt == null || thirdRt == null)
                return;

            RectTransform areaRt = firstRt.parent as RectTransform;
            if (areaRt == null || secondRt.parent != areaRt)
                return;

            if (thirdRt.parent != areaRt)
                thirdRt.SetParent(areaRt, false);

            SyncDoubleDownCardSize(thirdRt);
            EnsureDoubleDownCardVisible(thirdRt);

            thirdRt.anchorMin = new Vector2(0.5f, 0.5f);
            thirdRt.anchorMax = new Vector2(0.5f, 0.5f);
            thirdRt.pivot     = new Vector2(0.5f, 0.5f);

            Vector3 firstCenter  = firstRt.TransformPoint(firstRt.rect.center);
            Vector3 secondCenter = secondRt.TransformPoint(secondRt.rect.center);
            Vector3 pairCenter   = (firstCenter + secondCenter) * 0.5f;

            Vector3 worldCenter = pairCenter + areaRt.TransformVector(
                new Vector3(0f, DoubleDownVerticalLift, 0f));

            thirdRt.localRotation = Quaternion.Euler(0f, 0f, -90f);
            thirdRt.position      = worldCenter;
            thirdRt.SetAsLastSibling();
        }

        private void StartDoubleDownLayoutMaintenance()
        {
            if (_doubleDownLayoutCoroutine != null)
                StopCoroutine(_doubleDownLayoutCoroutine);

            _doubleDownLayoutCoroutine = StartCoroutine(MaintainDoubleDownLayout());
        }

        private IEnumerator MaintainDoubleDownLayout()
        {
            while (_doubleDownFirst != null && _doubleDownSecond != null && _doubleDownThird != null)
            {
                PlaceDoubleDownCard(_doubleDownFirst, _doubleDownSecond, _doubleDownThird);
                yield return null;
            }

            _doubleDownLayoutCoroutine = null;
        }

        private static void EnsureDoubleDownCardVisible(RectTransform thirdRt)
        {
            thirdRt.gameObject.SetActive(true);

            if (thirdRt.TryGetComponent(out CanvasRenderer renderer))
                renderer.cullTransparentMesh = false;
        }

        private static void EnsureIgnoreLayout(RectTransform rt)
        {
            if (!rt.TryGetComponent(out LayoutElement layoutElement))
                layoutElement = rt.gameObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
        }

        private static void SyncDoubleDownCardSize(RectTransform rt)
        {
            SyncCardLayoutElement(rt, DefaultCardWidth, DefaultCardHeight);
            rt.sizeDelta = new Vector2(DefaultCardWidth, DefaultCardHeight);
            rt.localScale = Vector3.one;
        }

        private static void SyncCardLayoutElement(RectTransform rt, float width, float height)
        {
            if (!rt.TryGetComponent(out LayoutElement layoutElement))
                layoutElement = rt.gameObject.AddComponent<LayoutElement>();

            layoutElement.ignoreLayout     = true;
            layoutElement.preferredWidth   = width;
            layoutElement.preferredHeight  = height;
            layoutElement.minWidth         = width;
            layoutElement.minHeight        = height;
        }

        private void StopDoubleDownLayout()
        {
            if (_doubleDownLayoutCoroutine != null)
            {
                StopCoroutine(_doubleDownLayoutCoroutine);
                _doubleDownLayoutCoroutine = null;
            }

            DestroyLegacyDoubleDownAnchors(playerCardArea);
            DestroyLegacyDoubleDownAnchors(splitCardArea);

            _doubleDownFirst  = null;
            _doubleDownSecond = null;
            _doubleDownThird  = null;
        }

        private static void DestroyLegacyDoubleDownAnchors(Transform area)
        {
            if (area == null)
                return;

            Transform anchor = area.Find("DoubleDownAnchor");
            if (anchor == null)
                return;

            var areaRt = (RectTransform)area;
            for (int i = anchor.childCount - 1; i >= 0; i--)
                anchor.GetChild(i).SetParent(areaRt, true);

            UnityEngine.Object.Destroy(anchor.gameObject);
        }

        private static void DestroyCardViews(System.Collections.Generic.List<CardView> views)
        {
            for (int i = views.Count - 1; i >= 0; i--)
            {
                CardView view = views[i];
                if (view == null) continue;
                UnityEngine.Object.Destroy(view.gameObject);
            }
            views.Clear();
        }

        private CardView SpawnCardView(CardData card, Transform parent, bool faceUp, bool ignoreLayout = false)
        {
            if (cardViewPrefab == null || parent == null)
            {
                Debug.LogError("BlackjackGame: cardViewPrefab or card area is not assigned.");
                return null;
            }

            GameObject go = Instantiate(cardViewPrefab, parent, false);
            go.SetActive(true);
            go.name = ignoreLayout ? "DoubleDownCard" : "CardView";

            RectTransform rt = go.GetComponent<RectTransform>();
            if (ignoreLayout)
            {
                EnsureIgnoreLayout(rt);
                SyncDoubleDownCardSize(rt);
            }

            CardView view = go.GetComponent<CardView>();
            view.Setup(
                spriteRegistry.GetSprite(card),
                spriteRegistry.GetBackSprite(),
                faceUp);
            return view;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Hole Card
        // ──────────────────────────────────────────────────────────────────────────

        private IEnumerator RevealDealerHoleForNaturalBlackjack()
        {
            yield return StartCoroutine(RevealHoleCard());
            UpdateScoreLabels(revealDealer: true);
        }

        private IEnumerator RevealHoleCard()
        {
            if (_dealerHoleCardView == null || _dealerHoleCardView.IsFaceUp)
                yield break;

            bool done = false;
            _dealerHoleCardView.Flip(toFaceUp: true, () => done = true);
            yield return new WaitUntil(() => done);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Glow Effect
        // ──────────────────────────────────────────────────────────────────────────

        private static bool IsNaturalBlackjack(Hand hand)
        {
            if (hand.Cards.Count != 2) return false;

            bool hasAce = false;
            bool hasTenValue = false;
            foreach (CardData card in hand.Cards)
            {
                if (card.Rank == Rank.Ace) hasAce = true;
                else if (card.BlackjackValue == 10) hasTenValue = true;
            }

            return hasAce && hasTenValue;
        }

        private bool IsDealerNaturalBlackjackLoss(Hand playerHand) =>
            IsNaturalBlackjack(_dealerHand) && !IsNaturalBlackjack(playerHand);

        private IEnumerator ResolveDealerNaturalBlackjackRound()
        {
            yield return StartCoroutine(RevealDealerHoleForNaturalBlackjack());

            int totalBet = CurrentBet + _doubleDownExtraBet;

            if (_isSplitRound)
            {
                var    results   = new List<string>();
                int    lostAmount = 0;
                Hand[] hands     = { _playerHand, _splitHand };
                string[] labels  = { "Hand 1", "Hand 2" };
                int handBet = CurrentBet / 2;

                for (int i = 0; i < hands.Length; i++)
                {
                    if (IsNaturalBlackjack(hands[i]))
                    {
                        results.Add(ColorizeText($"{labels[i]}: Push", PushColor));
                        ApplyPayout(PayoutResult.Push, handBet);
                    }
                    else
                    {
                        results.Add(ColorizeText($"{labels[i]}: Lose", LoseColor));
                        lostAmount += handBet;
                        ApplyPayout(PayoutResult.Lose, handBet);
                    }
                }

                RecordRoundOutcome(
                    isLoss: lostAmount > 0,
                    lostAmount: lostAmount,
                    scoreDelta: lostAmount > 0 ? -1 : 0,
                    isPush: lostAmount == 0);
                SetStatus(string.Join("  |  ", results));
                yield return StartCoroutine(ApplyDealerNaturalBlackjackPresentationRoutine());
            }
            else if (IsNaturalBlackjack(_playerHand))
            {
                if (AlwaysLose)
                {
                    yield return StartCoroutine(ApplyDealerNaturalBlackjackLossRoutine(totalBet));
                }
                else
                {
                    StartCoroutine(PlayDoubleBJSoundRoutine());
                    RecordRoundOutcome(false, scoreDelta: 0, isPush: true);
                    SetStatus("Push", PushColor);
                    ApplyPayout(PayoutResult.Push, CurrentBet);
                }
            }
            else
            {
                yield return StartCoroutine(ApplyDealerNaturalBlackjackLossRoutine(totalBet));
            }

            yield return StartCoroutine(EndRound());
        }

        /// <summary>
        /// Reveals the hole if needed, plays dealer card bloom + lose/damnit sounds, records the loss, and waits for the presentation to finish.
        /// </summary>
        private IEnumerator ApplyDealerNaturalBlackjackLossRoutine(int lostAmount, bool revealHole = false)
        {
            _dealerNaturalBJPlaying = true;

            if (revealHole)
                yield return StartCoroutine(RevealDealerHoleForNaturalBlackjack());

            ApplyBlackjackGlow(_dealerCardViews);
            float glowDuration = PlayDealerBlackjackLoseSound();
            _dealerNaturalBJEndTime = Time.time + glowDuration;

            RecordRoundOutcome(true, lostAmount: lostAmount, scoreDelta: -1);
            SetStatus("You lose", LoseColor);
            ApplyPayout(PayoutResult.Lose, lostAmount);

            yield return new WaitForSeconds(glowDuration);
            StopBlackjackGlow(_dealerCardViews);
            _dealerNaturalBJPlaying = false;
            _dealerNaturalBJEndTime = 0f;
        }

        /// <summary>Bloom + lose/damnit sounds only; caller handles payout/outcome.</summary>
        private IEnumerator ApplyDealerNaturalBlackjackPresentationRoutine()
        {
            _dealerNaturalBJPlaying = true;

            ApplyBlackjackGlow(_dealerCardViews);
            float glowDuration = PlayDealerBlackjackLoseSound();
            _dealerNaturalBJEndTime = Time.time + glowDuration;

            yield return new WaitForSeconds(glowDuration);
            StopBlackjackGlow(_dealerCardViews);
            _dealerNaturalBJPlaying = false;
            _dealerNaturalBJEndTime = 0f;
        }

        private void ApplyBlackjackGlow() => ApplyBlackjackGlow(_playerCardViews);

        private void ApplyBlackjackGlow(IReadOnlyList<CardView> cardViews)
        {
            foreach (CardView v in cardViews)
                v?.StartGlowPulse();
        }

        private void StopBlackjackGlow(IReadOnlyList<CardView> cardViews)
        {
            foreach (CardView v in cardViews)
                v?.StopGlowPulse();
        }

        /// <summary>Stops fireworks, all audio, and card glow pulses from a blackjack celebration.</summary>
        private void StopBlackjackCelebration()
        {
            _dealerNaturalBJPlaying = false;
            _dealerNaturalBJEndTime = 0f;

            if (audioSource != null)
                audioSource.Stop();

            foreach (CardView v in _playerCardViews)
                v?.StopGlowPulse();
            foreach (CardView v in _dealerCardViews)
                v?.StopGlowPulse();
        }

        // ──────────────────────────────────────────────────────────────────────────
        // UI Helpers
        // ──────────────────────────────────────────────────────────────────────────

        private void UpdateScoreLabels(bool revealDealer)
        {
            SetScoreLabelsVisible(true);

            if (_isSplitRound)
            {
                int p1 = _playerHand.BestValue();
                int p2 = _splitHand.BestValue();
                string s1 = p1 > BlackjackValue ? "Bust" : p1.ToString();
                string s2 = p2 > BlackjackValue ? "Bust" : p2.ToString();

                PositionLabelLeftOfArea(playerScoreLabel, playerCardArea);
                playerScoreLabel.text = s1;

                PositionLabelLeftOfArea(_splitScoreLabel, splitCardArea);
                _splitScoreLabel.text = s2;

                if (_state == GameState.PlayerTurn)
                    UpdateSplitScorePulse();
                else
                    StopAllScorePulses();
            }
            else
            {
                ResetPlayerScoreLabelPosition();
                StopAllScorePulses();
                playerScoreLabel.text = $"{_playerHand.BestValue()}";
            }

            if (revealDealer)
                dealerScoreLabel.text = $"{_dealerHand.BestValue()}";
            else
            {
                int visibleValue = _dealerHand.Cards.Count > 0
                    ? _dealerHand.Cards[0].BlackjackValue
                    : 0;
                dealerScoreLabel.text = $"{visibleValue}";
            }
        }

        /// <summary>
        /// Pins the status label's left edge to the left edge of the dealer card area.
        /// Uses the dealer card area's RectTransform to compute the offset at runtime,
        /// so the alignment stays correct regardless of canvas or layout changes.
        /// </summary>
        private void AlignStatusLabelToCardArea()
        {
            if (dealerCardArea == null) return;

            RectTransform dealerRT = dealerCardArea as RectTransform
                                     ?? dealerCardArea.GetComponent<RectTransform>();
            if (dealerRT == null) return;

            RectTransform statusRT = statusLabel.rectTransform;

            statusLabel.horizontalAlignment = HorizontalAlignmentOptions.Left;
            statusLabel.verticalAlignment   = VerticalAlignmentOptions.Middle;

            // Move pivot to the left edge so anchoredPosition controls the left side.
            Vector2 pivot = statusRT.pivot;
            pivot.x = 0f;
            pivot.y = 0.5f;
            statusRT.pivot = pivot;

            Vector2 size = statusRT.sizeDelta;
            size.y = StatusLabelHeight;
            statusRT.sizeDelta = size;

            // Left edge of dealer card area = its anchoredPosition.x - half its width.
            float leftEdge = dealerRT.anchoredPosition.x - dealerRT.rect.width * 0.5f;
            Vector2 pos = statusRT.anchoredPosition;
            pos.x = leftEdge;
            statusRT.anchoredPosition = pos;
        }

        private void ApplyStatusLabelAlignment()
        {
            if (statusLabel == null) return;

            statusLabel.horizontalAlignment = HorizontalAlignmentOptions.Left;
            statusLabel.verticalAlignment   = VerticalAlignmentOptions.Middle;
        }

        /// <summary>Sets the status label text and resets its color to the default.</summary>
        private void SetStatus(string message)
        {
            if (ShouldBlockStatusUpdate(message)) return;

            statusLabel.text = message;
            statusLabel.color = _defaultStatusColor;
            ApplyStatusLabelAlignment();
        }

        /// <summary>Sets the status label text with a specific color.</summary>
        private void SetStatus(string message, Color color)
        {
            if (ShouldBlockStatusUpdate(message)) return;

            statusLabel.text = message;
            statusLabel.color = color;
            ApplyStatusLabelAlignment();
        }

        private bool ShouldBlockStatusUpdate(string message)
        {
            if (IsLimitPulsing)
                return message != BetLimitStatusMessage && message != string.Empty;

            if (_betLimitStatusLocked)
                return message != BetLimitStatusMessage;

            return false;
        }

        /// <summary>Wraps text in TMP rich text color tags.</summary>
        private static string ColorizeText(string text, Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGBA(color);
            return $"<color=#{hex}>{text}</color>";
        }

        /// <summary>Shows or hides all score labels including the split label.</summary>
        private void SetScoreLabelsVisible(bool visible)
        {
            playerScoreLabel.gameObject.SetActive(visible);
            dealerScoreLabel.gameObject.SetActive(visible);

            if (_splitScoreLabel != null)
                _splitScoreLabel.gameObject.SetActive(visible && _isSplitRound);
        }

        /// <summary>Returns true when the given hand should automatically stand (hard 17+ or soft 19+).</summary>
        private bool ShouldAutoStand(Hand hand)
        {
            int score = hand.BestValue();
            if (hand.IsSoft())
                return score >= AutoStandSoft;
            return score >= AutoStandHard;
        }

        /// <summary>Creates the split score label by cloning the player score label and adds pulse components.</summary>
        private void InitSplitScoreLabel()
        {
            RectTransform playerScoreRT = playerScoreLabel.GetComponent<RectTransform>();
            _defaultPlayerScorePosition = playerScoreRT.anchoredPosition;

            // Instantiate with the source active so TMP's internal material/font
            // state is fully initialized on the clone before we deactivate it.
            // Deactivating immediately after prevents any Canvas rebuild pass from
            // running on the clone before it is intentionally shown.
            GameObject splitLabelObj = Instantiate(playerScoreLabel.gameObject, playerScoreLabel.transform.parent);
            splitLabelObj.name = "SplitScoreLabel";

            _splitScoreLabel = splitLabelObj.GetComponent<TextMeshProUGUI>();
            _splitScoreLabel.text = "";

            splitLabelObj.SetActive(false);

            _playerScorePulse = playerScoreLabel.gameObject.AddComponent<ScoreLabelPulse>();
            _splitScorePulse  = splitLabelObj.AddComponent<ScoreLabelPulse>();
        }

        /// <summary>Positions a score label to the left of the given card area, vertically centered.</summary>
        private void PositionLabelLeftOfArea(TextMeshProUGUI label, Transform cardArea)
        {
            RectTransform labelRT = label.GetComponent<RectTransform>();
            RectTransform areaRT  = cardArea.GetComponent<RectTransform>();

            float areaCenterY = areaRT.anchoredPosition.y + areaRT.sizeDelta.y * 0.5f;
            float labelHalfHeight = labelRT.sizeDelta.y * 0.5f;

            labelRT.anchorMin = areaRT.anchorMin;
            labelRT.anchorMax = areaRT.anchorMax;
            labelRT.anchoredPosition = new Vector2(
                _defaultPlayerScorePosition.x,
                areaCenterY - labelHalfHeight
            );
        }

        /// <summary>Resets the player score label to its original position.</summary>
        private void ResetPlayerScoreLabelPosition()
        {
            RectTransform playerScoreRT = playerScoreLabel.GetComponent<RectTransform>();
            playerScoreRT.anchoredPosition = _defaultPlayerScorePosition;
        }

        /// <summary>Pulses the active hand's score label and dims the inactive one.</summary>
        private void UpdateSplitScorePulse()
        {
            if (_activeHandIndex == 0)
            {
                _playerScorePulse.StartPulse();
                _splitScorePulse.StopPulse();
            }
            else
            {
                _playerScorePulse.StopPulse();
                _splitScorePulse.StartPulse();
            }
        }

        /// <summary>Stops all score label pulses and resets their alpha.</summary>
        private void StopAllScorePulses()
        {
            _playerScorePulse.StopPulse();
            _splitScorePulse.StopPulse();
        }

        /// <summary>Plays the win sound if both clip and source are assigned.</summary>
        private void PlayWinSound()
        {
            winSound.Play(audioSource);
        }

        /// <summary>
        /// Plays the win audio and card glow.
        /// When the current round was entered as a Delayed Martingale, triggers the full
        /// natural-blackjack celebration instead of the regular win sound, then plays
        /// <see cref="resetSound"/> once the celebration has finished.
        /// </summary>
        private void PlayWinRoutine()
        {
            if (_inMartingaleMode)
            {
                ApplyBlackjackGlow();
                float celebrationDuration = PlayNaturalBlackjackSound();
                SpawnFireworks(celebrationDuration);
                StartCoroutine(PlayResetSoundAfterDelay(celebrationDuration));
            }
            else
            {
                PlayWinSound();
            }
        }

        /// <summary>Waits for <paramref name="delay"/> seconds, then plays <see cref="resetSound"/>.</summary>
        private IEnumerator PlayResetSoundAfterDelay(float delay)
        {
            // Always yield at least one frame so RecordRoundOutcome can set _martingaleWin before we read it.
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            else
                yield return null;

            resetSound.Play(audioSource);
        }

        /// <summary>
        /// Instantiates <see cref="fireworksPrefab"/> at the world origin and auto-destroys it
        /// after <see cref="fireworksDuration"/> seconds.
        /// </summary>
        private void SpawnFireworks(float duration)
        {
            if (fireworksPrefab == null) return;
            Vector3 spawnPosition = Vector3.zero;
            if (playerCardArea is RectTransform cardRect)
            {
                Vector3[] corners = new Vector3[4];
                cardRect.GetWorldCorners(corners);
                // corners: 0=bottom-left, 1=top-left, 2=top-right, 3=bottom-right
                // Cards are left-aligned in the area, so bias toward the left side
                float x = Mathf.Lerp(corners[0].x, corners[2].x, 0.2f);
                float y = (corners[0].y + corners[2].y) * 0.5f;
                spawnPosition = new Vector3(x, y, corners[0].z);
            }
            else if (playerCardArea != null)
            {
                spawnPosition = playerCardArea.position;
            }
            GameObject fx = Instantiate(fireworksPrefab, spawnPosition, Quaternion.identity);
            float actualDuration = duration > 0f ? duration : fireworksDuration;
            Destroy(fx, actualDuration);
            _fireworksEndTime = Time.time + actualDuration;
        }

        /// <summary>Plays the natural blackjack sound if assigned, otherwise falls back to win sound.
        /// Also plays the yuhu sound simultaneously. Stops all player card glow pulses once the longest clip finishes.
        /// Returns the duration of the longest clip played, so callers can chain additional sounds.</summary>
        private float PlayNaturalBlackjackSound()
        {
            SoundEntry primary = naturalBlackjackSound.HasClip ? naturalBlackjackSound : winSound;
            float longestDuration = 0f;

            if (primary.HasClip && audioSource != null)
            {
                primary.Play(audioSource);
                longestDuration = primary.Length;
            }

            if (yuhuSound.HasClip && audioSource != null)
            {
                yuhuSound.Play(audioSource);
                if (yuhuSound.Length > longestDuration)
                    longestDuration = yuhuSound.Length;
            }

            if (longestDuration > 0f)
                StartCoroutine(StopGlowAfterClip(longestDuration, _playerCardViews));

            return longestDuration;
        }

        private IEnumerator StopGlowAfterClip(float duration, IReadOnlyList<CardView> cardViews)
        {
            yield return new WaitForSeconds(duration);
            StopBlackjackGlow(cardViews);
        }

        /// <summary>Plays the lose sound if both clip and source are assigned.</summary>
        private void PlayLoseSound()
        {
            loseSound.Play(audioSource);
        }

        /// <summary>Plays lose and damnit sounds when the dealer has a natural blackjack.</summary>
        private float PlayDealerBlackjackLoseSound()
        {
            PlayLoseSound();

            float longestDuration = loseSound.HasClip ? loseSound.Length : 0f;

            if (damnitSound.HasClip && audioSource != null)
            {
                damnitSound.Play(audioSource);
                if (damnitSound.Length > longestDuration)
                    longestDuration = damnitSound.Length;
            }

            return longestDuration > 0f ? longestDuration : 3f;
        }

        /// <summary>Plays the tie sound if both clip and source are assigned.</summary>
        private void PlayTieSound()
        {
            tieSound.Play(audioSource);
        }

        /// <summary>
        /// Triggered on a double natural blackjack push. Plays the tie sound once,
        /// then plays one randomly chosen sound from cheaterSound, damnitSound, and hmhSound.
        /// The same sound is never played twice in a row. Only assigned clips are included.
        /// The deal button stays locked until the random sound has finished playing.
        /// </summary>
        private IEnumerator PlayDoubleBJSoundRoutine()
        {
            _doubleBJSoundPlaying = true;

            tieSound.Play(audioSource);
            yield return new WaitForSeconds(tieSound.Length);

            List<SoundEntry> pool = new List<SoundEntry>();
            if (cheaterSound.HasClip) pool.Add(cheaterSound);
            if (damnitSound.HasClip)  pool.Add(damnitSound);
            if (hmhSound.HasClip)     pool.Add(hmhSound);

            if (_lastDoubleBJSound.HasValue && pool.Count > 1)
                pool.RemoveAll(s => s.clip == _lastDoubleBJSound.Value.clip);

            if (pool.Count == 0)
            {
                _doubleBJSoundPlaying = false;
                if (_state == GameState.RoundOver)
                    SetButtonState(dealEnabled: true, actionEnabled: false, splitEnabled: false);
                yield break;
            }

            SoundEntry chosen = pool[UnityEngine.Random.Range(0, pool.Count)];
            _lastDoubleBJSound = chosen;

            chosen.Play(audioSource);
            yield return new WaitForSeconds(chosen.Length);
            _doubleBJSoundPlaying = false;

            if (_state == GameState.RoundOver)
                SetButtonState(dealEnabled: true, actionEnabled: false, splitEnabled: false);
        }

        private void SetButtonState(bool dealEnabled, bool actionEnabled, bool splitEnabled, bool doubleDownEnabled = false, bool surrenderEnabled = false)
        {
            dealButton.interactable = dealEnabled;
            dealButton.gameObject.SetActive(dealEnabled);

            hitButton.interactable = actionEnabled;
            hitButton.gameObject.SetActive(actionEnabled);

            standButton.interactable = actionEnabled;
            standButton.gameObject.SetActive(actionEnabled);

            if (surrenderButton != null)
            {
                surrenderButton.interactable = surrenderEnabled;
                surrenderButton.gameObject.SetActive(surrenderEnabled);
            }

            if (splitButton != null)
            {
                splitButton.interactable = splitEnabled;
                splitButton.gameObject.SetActive(splitEnabled);

                if (splitEnabled && splitAvailableSprite != null)
                {
                    Image splitImage = splitButton.GetComponent<Image>();
                    if (splitImage != null)
                        splitImage.sprite = splitAvailableSprite;
                }
            }

            if (doubleDownButton != null)
            {
                doubleDownButton.interactable = doubleDownEnabled;
                doubleDownButton.gameObject.SetActive(doubleDownEnabled);
            }
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Table Clear
        // ──────────────────────────────────────────────────────────────────────────

        private void ClearTable()
        {
            StopDoubleDownLayout();
            UnfreezeAreaLayout();

            _playerHand.Clear();
            _splitHand.Clear();
            _dealerHand.Clear();
            _dealerHoleCardView = null;
            _isSplitRound    = false;
            _activeHandIndex = 0;
            StopAllScorePulses();
            ResetPlayerScoreLabelPosition();
            SetScoreLabelsVisible(false);

            DestroyCardViews(_playerCardViews);
            DestroyCardViews(_splitCardViews);
            DestroyCardViews(_dealerCardViews);
        }
    }
}
