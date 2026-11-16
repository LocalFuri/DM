using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace TexasHoldem
{
    public class GameManager : MonoBehaviour
    {
        [Header("Game Settings")]
        [SerializeField] private int   _startingChips  = 1000;
        [SerializeField] private int   _smallBlind     = 10;
        [SerializeField] private int   _bigBlind       = 20;
        [SerializeField] private int   _aiPlayerCount  = 3;
        [SerializeField] private float _aiActionDelay  = 1.0f;
        [SerializeField] private float _humanThinkTime = 15f;
        [SerializeField] private float _dealDelay      = 0.4f;

        [Header("Table")]
        [SerializeField] private TableLayoutManager _tableLayout;
        [SerializeField] private UIManager          _uiManager;

        [Header("Button Row")]
        [SerializeField] private float _buttonWidth    = 120f;
        [SerializeField] private float _buttonHeight   = 50f;
        [SerializeField] private float _buttonFontSize = 40f;

        [Header("Card Layout")]
        [Tooltip("Pixel gap between the two hole cards (synced to TableLayoutManager on validate).")]
        [SerializeField] private float _cardGap = 16f;

        [Header("Community Card Animation")]
        [SerializeField] private float _communityFlipDuration = 0.35f;
        [SerializeField] private float _communityFlipGap      = 0.1f;

        [Header("Round End")]
        [Tooltip("How long the winner celebration plays before the next round begins.")]
        [SerializeField, Min(0f)] private float _roundEndPauseSecs = 2.5f;
        [Tooltip("Shows on-screen Collect / Next-hand buttons after a win (debug flow).")]
        [SerializeField] private bool _showWinnerDebugOverlay = true;

        [Header("Round Start")]
        [Tooltip("Pause before the dealer button appears at the start of each hand.")]
        [SerializeField, Min(0f)] private float _dealerButtonDelay = 0.4f;
        [Tooltip("Pause between dealer button, small blind, and big blind.")]
        [SerializeField, Min(0f)] private float _blindPostDelay = 0.5f;

        [Header("Rake")]
        [SerializeField] private PokerRakeSettings _rake = new PokerRakeSettings();

        [Header("Events")]
        public UnityEvent<GamePhase>        OnPhaseChanged;
        public UnityEvent<List<PlayerState>> OnPlayersUpdated;
        public UnityEvent                   OnRoundStarting;
        public UnityEvent<int>              OnDealerButtonPlaced;
        public UnityEvent<List<Card>>       OnCommunityCardsUpdated;
        public UnityEvent<string>           OnGameMessage;
        public UnityEvent<PlayerState>      OnPlayerTurn;
        public PlayerActionEvent            OnPlayerAction;
        public UnityEvent<PlayerState>      OnWinnerDetermined;
        public UnityEvent                   OnRoundEnded;

        public List<PlayerState> Players      { get; private set; } = new List<PlayerState>();
        public GamePhase         CurrentPhase { get; private set; } = GamePhase.WaitingToStart;
        public int               DealerIndex  { get; private set; } = 0;
        public int               PotAmount    => _bettingManager?.Pot ?? 0;
        public int               CurrentBet   => _bettingManager?.CurrentBet ?? 0;
        public int               BigBlindAmount => _bigBlind;
        public int               StreetRaiseCount => _bettingManager?.StreetRaiseCount ?? 0;
        public bool              IsAwaitingHumanInput => _awaitingHumanInput;
        public TableSoundManager TableSounds { get; private set; }

        /// <summary>True when the player can make a legal minimum raise (not call-only).</summary>
        public bool CanPlayerRaise(PlayerState player) =>
            player != null && _bettingManager != null && _bettingManager.CanRaise(player);

        public int GetCallAmountFor(PlayerState player) =>
            player != null && _bettingManager != null ? _bettingManager.GetCallAmount(player) : 0;

        public int GetMinRaiseIncrement() =>
            _bettingManager?.GetMinRaiseIncrement() ?? _bigBlind * 2;

        public int GetMaxRaiseIncrement(PlayerState player) =>
            player != null && _bettingManager != null
                ? _bettingManager.GetMaxRaiseIncrement(player)
                : 0;
        public float             ButtonWidth    => _buttonWidth;
        public float             ButtonHeight   => _buttonHeight;
        public float             ButtonFontSize => _buttonFontSize;
        public float             AiActionDelay          => _aiActionDelay;

        /// <summary>Sets bot action delay in seconds (0 = instant).</summary>
        public void SetAiActionDelay(float seconds)
        {
            _aiActionDelay = Mathf.Max(0f, seconds);
        }
        public float             HumanThinkTime         => _humanThinkTime;
        public float             CardGap                 => _cardGap;
        public float             CommunityFlipDuration   => _communityFlipDuration;
        public float             CommunityFlipGap        => _communityFlipGap;
        public float             RoundEndPauseSecs       => _roundEndPauseSecs;
        public bool              AwaitingWinnerDismiss   => _awaitingWinnerDismiss;

        /// <summary>Continues past the showdown result after the human presses Space.</summary>
        public void AcknowledgeWinnerDismiss()
        {
            ApplyPendingPotAward();
            _awaitingWinnerDismiss = false;
            HideWinnerDismissControls();
        }

        /// <summary>
        /// Pays the net pot to winners. Called when pot chips are collected to the winner HUD
        /// (Backspace / B), not at showdown resolution.
        /// </summary>
        public void ApplyPendingPotAward()
        {
            if (!_potAwardPending)
                return;

            _potAwardPending = false;

            if (LastPotAwarded <= 0 || LastRoundWinners == null || LastRoundWinners.Count == 0)
                return;

            PotAward.Split(LastPotAwarded, LastRoundWinners);
            NotifyPlayersUpdated();
        }

        public bool PotAwardPending => _potAwardPending;
        public float             DealerButtonDelay       => _dealerButtonDelay;
        public float             BlindPostDelay          => _blindPostDelay;
        public int               LastPotAwarded          { get; private set; }
        public int               LastGrossPot            { get; private set; }
        public int               LastRakeAmount          { get; private set; }
        public string            LastRakeDisplayText     { get; private set; }
        public IReadOnlyList<PlayerState> LastRoundWinners { get; private set; }
        public WinningHandEvaluation LastWinningHand     { get; private set; }
        public IReadOnlyList<(PlayerState Player, HandResult Result)> LastShowdownHands { get; private set; }
        public IReadOnlyList<(PlayerState Player, WinningHandEvaluation Evaluation)> LastShowdownEvaluations { get; private set; }
        public IReadOnlyList<Card> CommunityCards        => _boardManager?.CommunityCards;

        /// <summary>True when everyone folded to the big blind preflop (no flop, no raises).</summary>
        public bool        LastHandWasBbWalk    { get; private set; }
        /// <summary>Net chips the BB wins on a walk (the small blind).</summary>
        public int         LastBbWalkNetWin     { get; private set; }
        /// <summary>Small-blind player who posted the chip awarded to the BB on a walk.</summary>
        public PlayerState LastBbWalkSbPlayer   { get; private set; }

        /// <summary>Seat index (0-based) of the current dealer among <see cref="Players"/>.</summary>
        public int GetDealerSeatIndex()
        {
            if (Players == null || Players.Count == 0)
                return -1;

            var active = Players.Where(p => p.Chips > 0).ToList();
            if (active.Count == 0)
                return -1;

            int dealerActiveIndex = ((DealerIndex % active.Count) + active.Count) % active.Count;
            return Players.IndexOf(active[dealerActiveIndex]);
        }

        /// <summary>Preflop seat bucket (BTN/SB/BB/UTG/…) for a player still seated with chips.</summary>
        public PreflopSeatBucket GetPreflopSeatBucket(PlayerState player)
        {
            if (player == null || Players == null || Players.Count == 0)
                return PreflopSeatBucket.Early;

            var active = Players.Where(p => p.Chips > 0).ToList();
            if (active.Count == 0)
                return PreflopSeatBucket.Early;

            int dealerSeat = GetDealerSeatIndex();
            if (dealerSeat < 0 || dealerSeat >= Players.Count)
                return PreflopSeatBucket.Early;

            PlayerState dealer = Players[dealerSeat];
            int dealerInActive = active.IndexOf(dealer);
            if (dealerInActive < 0)
                dealerInActive = 0;

            return PreflopStrategy.ResolveSeatBucket(active, dealerInActive, player);
        }

        /// <summary>Wait time after dealing — flip animations removed; only base deal delay when clearing.</summary>
        public float GetCommunityRevealDuration(int newCardCount)
        {
            return newCardCount <= 0 ? _dealDelay : 0f;
        }

        private BettingManager _bettingManager;
        private BoardManager   _boardManager;
        private AIController   _aiController;

        private bool          _awaitingHumanInput;
        private bool          _awaitingWinnerDismiss;
        private bool          _potAwardPending;
        private BettingAction _humanAction;
        private int           _humanRaiseAmount;
        private WinnerDismissControls _winnerDismissControls;

        private void Awake()
        {
            ButtonLabelStyle.RegisterFontSizeProvider(() => _buttonFontSize);
            _bettingManager = new BettingManager(_smallBlind, _bigBlind);
            _boardManager   = new BoardManager();
            _aiController   = new AIController();

            if (_tableLayout == null)
                _tableLayout = FindObjectOfType<TableLayoutManager>(includeInactive: true);

            ResolveUiManagerReference();
            EnsureTableSoundManager();

            OnPhaseChanged          ??= new UnityEvent<GamePhase>();
            OnPlayersUpdated        ??= new UnityEvent<List<PlayerState>>();
            OnCommunityCardsUpdated ??= new UnityEvent<List<Card>>();
            OnGameMessage           ??= new UnityEvent<string>();
            OnPlayerTurn            ??= new UnityEvent<PlayerState>();
            OnPlayerAction          ??= new PlayerActionEvent();
            OnWinnerDetermined      ??= new UnityEvent<PlayerState>();
            OnRoundEnded            ??= new UnityEvent();
            OnRoundStarting         ??= new UnityEvent();
            OnDealerButtonPlaced    ??= new UnityEvent<int>();
        }

        private void Start()
        {
            ResolveUiManagerReference();
            EnsureWinnerDismissControls();
            InitializePlayers();
        }

        private void EnsureWinnerDismissControls()
        {
            _winnerDismissControls = GetComponent<WinnerDismissControls>();
            if (_winnerDismissControls == null)
                _winnerDismissControls = gameObject.AddComponent<WinnerDismissControls>();

            _winnerDismissControls.Bind(this, _uiManager);
        }

        private void ShowWinnerDismissControls()
        {
            EnsureWinnerDismissControls();
            ResolveUiManagerReference();
            _winnerDismissControls.Bind(this, _uiManager);
            _winnerDismissControls.Begin(_showWinnerDebugOverlay);
        }

        private void HideWinnerDismissControls()
        {
            _winnerDismissControls?.End();
        }

        private void ResolveUiManagerReference()
        {
            if (_uiManager != null)
                return;

            _uiManager = UIManager.Instance;

#if UNITY_2023_1_OR_NEWER
            if (_uiManager == null)
                _uiManager = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
#else
            if (_uiManager == null)
                _uiManager = FindObjectOfType<UIManager>(true);
#endif
        }

        private void EnsureTableSoundManager()
        {
            TableSounds = GetComponent<TableSoundManager>();
            if (TableSounds == null)
                TableSounds = gameObject.AddComponent<TableSoundManager>();
        }

        /// <summary>Called by the UI Start button to begin the game loop.</summary>
        public void StartGame()
        {
            StartCoroutine(GameLoop());
        }

        /// <summary>Stops play and returns to the pre-start state (Start button only).</summary>
        public void ResetToStartScreen()
        {
            StopAllCoroutines();
            _awaitingHumanInput    = false;
            _awaitingWinnerDismiss = false;
            _potAwardPending       = false;
            HideWinnerDismissControls();
            _humanAction        = default;
            _humanRaiseAmount   = 0;
            DealerIndex         = 0;
            InitializePlayers();
            _bettingManager.ResetRound();
            _boardManager.NewDeck();
            SetPhase(GamePhase.WaitingToStart);
            OnCommunityCardsUpdated?.Invoke(_boardManager.CommunityCards);
            NotifyPlayersUpdated();
            OnGameMessage?.Invoke(string.Empty);
        }

        private void InitializePlayers()
        {
            _tableLayout?.SyncPlayerDisplayNames();

            Players.Clear();

            int seatCount = _tableLayout != null ? TableLayoutManager.SeatCount : 1 + _aiPlayerCount;
            int playerCount = Mathf.Clamp(1 + _aiPlayerCount, 1, seatCount);

            for (int i = 0; i < playerCount; i++)
            {
                string name = _tableLayout != null
                    ? _tableLayout.GetPlayerName(i)
                    : (i == 0 ? "You" : $"Bot {i}");
                PlayerType type = i == 0 ? PlayerType.Human : PlayerType.AI;
                Players.Add(new PlayerState(name, type, _startingChips));
            }
        }

        private IEnumerator GameLoop()
        {
            while (true)
            {
                var eligible = Players.Where(p => p.Chips > 0).ToList();
                if (eligible.Count < 2)
                {
                    string winner = eligible.Count == 1 ? eligible[0].Name : "Nobody";
                    SetPhase(GamePhase.GameOver);
                    OnGameMessage?.Invoke($"Game Over! {winner} wins the game!");
                    yield break;
                }

                yield return StartCoroutine(PlayRound());
            }
        }

        private IEnumerator PlayRound()
        {
            foreach (var p in Players) p.ResetForNewRound();
            _bettingManager.ResetRound();
            _boardManager.NewDeck();
            LastHandWasBbWalk  = false;
            LastBbWalkNetWin   = 0;
            LastBbWalkSbPlayer = null;

            // Reset the board display at the start of every round.
            OnCommunityCardsUpdated?.Invoke(_boardManager.CommunityCards);

            var active   = Players.Where(p => p.Chips > 0).ToList();
            ApplyGodModeIfEnabled(active);
            int sbIndex  = (DealerIndex + 1) % active.Count;
            int bbIndex  = (DealerIndex + 2) % active.Count;

            SetPhase(GamePhase.RoundOver);
            OnRoundStarting?.Invoke();
            NotifyPlayersUpdated();

            yield return DelaySeconds(_dealerButtonDelay);
            OnDealerButtonPlaced?.Invoke(GetDealerSeatIndex());
            NotifyPlayersUpdated();

            yield return DelaySeconds(_blindPostDelay);
            yield return DealPreflopHoleCards(active, sbIndex);
            NotifyPlayersUpdated();
            yield return DelaySeconds(GetCommunityRevealDuration(2));

            yield return DelaySeconds(_blindPostDelay);
            _bettingManager.PostSmallBlind(active[sbIndex]);
            TableSounds?.PlayBlind();
            OnGameMessage?.Invoke($"{active[sbIndex].Name} posts small blind (${_smallBlind}).");
            NotifyPlayersUpdated();

            yield return DelaySeconds(_blindPostDelay);
            _bettingManager.PostBigBlind(active[bbIndex]);
            TableSounds?.PlayBlind();
            OnGameMessage?.Invoke($"{active[bbIndex].Name} posts big blind (${_bigBlind}).");
            NotifyPlayersUpdated();

            // ── Pre-Flop ──────────────────────────────────────────────────
            SetPhase(GamePhase.PreFlop);
            int utgIndex = (bbIndex + 1) % active.Count;
            yield return StartCoroutine(BettingRound(active, utgIndex));
            if (GetNonFolded(active).Count <= 1) { yield return StartCoroutine(EndRound(active)); yield break; }

            int boardCount = 0;

            // ── Flop ──────────────────────────────────────────────────────
            yield return CollectStreetBetsBeforeNextStreet(active);
            _boardManager.DealFlop();
            var flop = _boardManager.CommunityCards;
            SetPhase(GamePhase.Flop);
            OnCommunityCardsUpdated?.Invoke(flop);
            UIManager flopUi = ResolveUiManager();
            yield return DelaySeconds(flopUi != null ? flopUi.FlopDealTotalDuration : 0.44f);
            yield return null; // extra frame so UI reveal coroutine finishes updating _revealedCommunityCount
            boardCount = flop.Count;
            yield return StartCoroutine(BettingRound(active, sbIndex));
            if (GetNonFolded(active).Count <= 1) { yield return StartCoroutine(EndRound(active)); yield break; }

            // ── Turn ──────────────────────────────────────────────────────
            yield return CollectStreetBetsBeforeNextStreet(active);
            _boardManager.DealTurn();
            var board4 = _boardManager.CommunityCards;
            SetPhase(GamePhase.Turn);
            OnCommunityCardsUpdated?.Invoke(board4);
            yield return DelaySeconds(GetCommunityRevealDuration(board4.Count - boardCount));
            yield return null; // extra frame so UI reveal coroutine finishes updating _revealedCommunityCount
            boardCount = board4.Count;
            yield return StartCoroutine(BettingRound(active, sbIndex));
            if (GetNonFolded(active).Count <= 1) { yield return StartCoroutine(EndRound(active)); yield break; }

            // ── River ─────────────────────────────────────────────────────
            yield return CollectStreetBetsBeforeNextStreet(active);
            _boardManager.DealRiver();
            var board5 = _boardManager.CommunityCards;
            SetPhase(GamePhase.River);
            OnCommunityCardsUpdated?.Invoke(board5);
            yield return DelaySeconds(GetCommunityRevealDuration(board5.Count - boardCount));
            yield return null; // extra frame so UI reveal coroutine finishes updating _revealedCommunityCount
            yield return StartCoroutine(BettingRound(active, sbIndex));

            yield return StartCoroutine(EndRound(active));
        }

        private IEnumerator BettingRound(List<PlayerState> players, int startIndex)
        {
            int n = players.Count;
            var hasActed = new bool[n];
            for (int i = 0; i < n; i++)
                hasActed[i] = players[i].HasFolded || players[i].IsAllIn;

            if (!AnyPlayerMustAct(players, hasActed, _bettingManager.CurrentBet))
                yield break;

            int seatIndex   = startIndex % n;
            int safetyLimit = n * n * 4;
            int iterations  = 0;

            while (iterations++ < safetyLimit)
            {
                if (GetNonFolded(players).Count <= 1)
                    yield break;

                if (IsBettingComplete(players, hasActed, _bettingManager.CurrentBet))
                    yield break;

                int currentIndex = seatIndex % n;
                var player       = players[currentIndex];

                if (player.HasFolded || player.IsAllIn)
                {
                    seatIndex++;
                    continue;
                }

                if (hasActed[currentIndex] && player.CurrentBet >= _bettingManager.CurrentBet)
                {
                    seatIndex++;
                    continue;
                }

                yield return WaitWhileOptionsMenuOpen();

                int  betBeforeAction = _bettingManager.CurrentBet;
                bool actionApplied   = false;
                BettingAction appliedAction = default;

                _awaitingHumanInput = player.Type == PlayerType.Human;
                OnPlayerTurn?.Invoke(player);

                if (player.Type == PlayerType.AI)
                {
                    yield return DelayForAiAction();
                    var (action, raise) = _aiController.DecideAction(
                        player, _boardManager.CommunityCards, _bettingManager, IsTestMode);
                    int playerBetBefore = player.CurrentBet;
                    if (_bettingManager.ProcessAction(player, action, raise))
                    {
                        actionApplied = true;
                        appliedAction = action;
                        int displayAmount = GetActionDisplayAmount(player, action, raise, playerBetBefore);
                        OnPlayerAction?.Invoke(player, action, displayAmount);
                        string detail = action == BettingAction.Raise ? $" +${raise}" : "";
                        OnGameMessage?.Invoke($"{player.Name}: {action}{detail}");
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[GameManager] Rejected {player.Name} action {action} (raise={raise}, " +
                            $"bet={player.CurrentBet}, table={_bettingManager.CurrentBet}).");
                    }
                }
                else
                {
                    yield return new WaitUntil(() => !_awaitingHumanInput && !IsOptionsMenuOpen);

                    int humanBetBefore = player.CurrentBet;
                    if (_bettingManager.ProcessAction(player, _humanAction, _humanRaiseAmount))
                    {
                        actionApplied = true;
                        appliedAction = _humanAction;
                        int displayAmount = GetActionDisplayAmount(player, _humanAction, _humanRaiseAmount, humanBetBefore);
                        OnPlayerAction?.Invoke(player, _humanAction, displayAmount);
                        string detail = _humanAction == BettingAction.Raise ? $" +${_humanRaiseAmount}" : "";
                        OnGameMessage?.Invoke($"You: {_humanAction}{detail}");
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[GameManager] Rejected human action {_humanAction} (raise={_humanRaiseAmount}, " +
                            $"bet={player.CurrentBet}, table={_bettingManager.CurrentBet}).");
                    }
                }

                if (!actionApplied)
                    continue;

                NotifyPlayersUpdated();

                if (appliedAction == BettingAction.Check && TableSounds != null && TableSounds.KnockKnockDuration > 0f)
                    yield return DelaySeconds(TableSounds.KnockKnockDuration);
                else if (player.Type == PlayerType.AI && _aiActionDelay <= 0f)
                    yield return null;

                hasActed[currentIndex] = true;

                if (_bettingManager.CurrentBet > betBeforeAction)
                    ReopenActionForOthers(players, hasActed, currentIndex);

                seatIndex++;

                if (IsBettingComplete(players, hasActed, _bettingManager.CurrentBet))
                    yield break;
            }
        }

        private static bool IsBettingComplete(
            IReadOnlyList<PlayerState> players, bool[] hasActed, int currentBet)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].HasFolded || players[i].IsAllIn)
                    continue;
                if (!hasActed[i])
                    return false;
                if (players[i].CurrentBet < currentBet)
                    return false;
            }

            return true;
        }

        private static bool AnyPlayerMustAct(
            IReadOnlyList<PlayerState> players, bool[] hasActed, int currentBet)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].HasFolded || players[i].IsAllIn)
                    continue;
                if (!hasActed[i] || players[i].CurrentBet < currentBet)
                    return true;
            }

            return false;
        }

        private static void ReopenActionForOthers(
            IReadOnlyList<PlayerState> players, bool[] hasActed, int aggressorIndex)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (i == aggressorIndex)
                    continue;
                if (players[i].HasFolded || players[i].IsAllIn)
                    continue;

                hasActed[i] = false;
            }
        }

        private IEnumerator EndRound(List<PlayerState> active)
        {
            List<PlayerState> remaining = GetNonFolded(active);
            if (remaining.Count == 1)
            {
                PlayerState soleWinner = remaining[0];
                int returned = _bettingManager.ReturnUncalledBet(soleWinner, active);
                if (returned > 0)
                {
                    OnGameMessage?.Invoke(
                        $"{soleWinner.Name}'s uncalled raise (${returned}) is returned.");
                    UIManager ui = ResolveUiManager();
                    if (ui != null)
                        yield return ui.PlayUncalledBetReturn(soleWinner);
                    NotifyPlayersUpdated();
                }

                if (TryDetectBbWalk(active, soleWinner, out PlayerState sbPlayer))
                {
                    LastHandWasBbWalk  = true;
                    LastBbWalkNetWin   = _smallBlind;
                    LastBbWalkSbPlayer = sbPlayer;
                }
            }

            if (LastHandWasBbWalk)
            {
                UIManager walkUi = ResolveUiManager();
                if (walkUi != null)
                    yield return walkUi.CollectBbWalkBlinds(LastBbWalkSbPlayer);
                ResetBetsForNewPhase(active);
            }
            else
            {
                yield return CollectStreetBetsBeforeNextStreet(active);
            }

            SetPhase(GamePhase.Showdown);

            var contenders = GetNonFolded(active);
            if (contenders.Count == 0)
                yield break;

            RoundWinners roundWinners = ResolveRoundWinners(contenders);
            LastWinningHand   = roundWinners.BestEvaluation;
            LastShowdownHands       = roundWinners.ShowdownHands;
            LastShowdownEvaluations = roundWinners.ShowdownEvaluations;

            int dealerInActive = ((DealerIndex % active.Count) + active.Count) % active.Count;
            LastRoundWinners = PotAward.OrderWinnersClockwiseFromDealer(
                roundWinners.Players, active, dealerInActive);

            LastGrossPot   = _bettingManager.Pot;
            bool flopDealt = _boardManager.CommunityCards.Count >= 3;
            RakeResult rakeResult = _rake.Evaluate(LastGrossPot, _bigBlind, flopDealt);
            LastRakeAmount      = rakeResult.Amount;
            LastRakeDisplayText = _rake.FormatDisplay(rakeResult);
            LastPotAwarded = LastGrossPot - LastRakeAmount;
            _potAwardPending = true;

            OnGameMessage?.Invoke(BuildRoundEndMessage(
                LastRoundWinners, LastPotAwarded, LastRakeAmount, LastRakeDisplayText, LastHandWasBbWalk, LastBbWalkNetWin));
            _awaitingWinnerDismiss = true;
            OnWinnerDetermined?.Invoke(LastRoundWinners[0]);
            NotifyPlayersUpdated();
            ShowWinnerDismissControls();

            yield return new WaitUntil(() => !_awaitingWinnerDismiss);

            OnRoundEnded?.Invoke();
            DealerIndex = (DealerIndex + 1) % active.Count;
            SetPhase(GamePhase.RoundOver);
        }

        private struct RoundWinners
        {
            public List<PlayerState> Players;
            public WinningHandEvaluation BestEvaluation;
            public List<(PlayerState Player, HandResult Result)> ShowdownHands;
            public List<(PlayerState Player, WinningHandEvaluation Evaluation)> ShowdownEvaluations;

            public RoundWinners(
                List<PlayerState> players,
                WinningHandEvaluation bestEvaluation,
                List<(PlayerState Player, HandResult Result)> showdownHands,
                List<(PlayerState Player, WinningHandEvaluation Evaluation)> showdownEvaluations)
            {
                Players              = players;
                BestEvaluation       = bestEvaluation;
                ShowdownHands        = showdownHands;
                ShowdownEvaluations  = showdownEvaluations;
            }
        }

        private RoundWinners ResolveRoundWinners(List<PlayerState> contenders)
        {
            WinningHandEvaluation bestEval = null;
            var evaluated = new List<(PlayerState player, WinningHandEvaluation evaluation)>();

            foreach (PlayerState player in contenders)
            {
                var cards = new List<Card>(player.HoleCards);
                cards.AddRange(_boardManager.CommunityCards);
                if (cards.Count < 5)
                    continue;

                WinningHandEvaluation evaluation = HandEvaluator.EvaluateBest(cards);
                evaluated.Add((player, evaluation));

                if (bestEval == null || evaluation.Result.CompareTo(bestEval.Result) > 0)
                    bestEval = evaluation;
            }

            var showdownHands = new List<(PlayerState Player, HandResult Result)>(evaluated.Count);
            var showdownEvaluations = new List<(PlayerState Player, WinningHandEvaluation Evaluation)>(evaluated.Count);
            foreach ((PlayerState player, WinningHandEvaluation evaluation) in evaluated)
            {
                showdownHands.Add((player, evaluation.Result));
                showdownEvaluations.Add((player, evaluation));
            }

            var winners = new List<PlayerState>();
            if (bestEval != null)
            {
                foreach ((PlayerState player, WinningHandEvaluation evaluation) in evaluated)
                {
                    if (evaluation.Result.CompareTo(bestEval.Result) == 0)
                        winners.Add(player);
                }
            }
            else
            {
                winners.Add(contenders[0]);
            }

            return new RoundWinners(winners, bestEval, showdownHands, showdownEvaluations);
        }

        private static string BuildRoundEndMessage(
            IReadOnlyList<PlayerState> winners,
            int netPot,
            int rakeAmount,
            string rakeDisplay,
            bool bbWalk = false,
            int bbWalkNetWin = 0)
        {
            if (winners == null || winners.Count == 0)
                return string.Empty;

            string names = winners.Count == 1
                ? winners[0].Name
                : string.Join(" & ", winners.Select(p => p.Name));

            string rakeNote = rakeAmount > 0 && !string.IsNullOrEmpty(rakeDisplay)
                ? $" (rake {rakeDisplay})"
                : string.Empty;

            if (winners.Count == 1 && bbWalk && bbWalkNetWin > 0)
                return $"{names} wins the blinds (+${bbWalkNetWin}){rakeNote}!";

            if (winners.Count == 1)
                return $"{names} wins the pot of ${netPot}{rakeNote}!";

            return $"{names} split the pot of ${netPot}{rakeNote}!";
        }

        private bool TryDetectBbWalk(List<PlayerState> active, PlayerState winner, out PlayerState sbPlayer)
        {
            sbPlayer = null;
            if (winner == null || active == null || active.Count < 2)
                return false;

            if (_boardManager.CommunityCards.Count >= 3)
                return false;

            if (_bettingManager.StreetRaiseCount > 0)
                return false;

            if (_bettingManager.Pot != _smallBlind + _bigBlind)
                return false;

            int n       = active.Count;
            int bbIndex = (DealerIndex + 2) % n;
            if (active[bbIndex] != winner)
                return false;

            int sbIndex = (DealerIndex + 1) % n;
            sbPlayer    = active[sbIndex];
            return sbPlayer != null;
        }

        /// <summary>Called by the UI to submit the human player's chosen betting action.</summary>
        public void SubmitPlayerAction(BettingAction action, int raiseAmount = 0)
        {
            if (!_awaitingHumanInput) return;
            _humanAction        = action;
            _humanRaiseAmount   = raiseAmount;
            _awaitingHumanInput = false;
        }

        private void ResetBetsForNewPhase(List<PlayerState> players)
        {
            _bettingManager.ResetPhase();
            foreach (var p in players) p.CurrentBet = 0;
        }

        private IEnumerator CollectStreetBetsBeforeNextStreet(List<PlayerState> active)
        {
            UIManager ui = ResolveUiManager();
            if (ui != null)
                yield return ui.CollectStreetBetsToPot();

            ResetBetsForNewPhase(active);
        }

        private UIManager ResolveUiManager()
        {
            if (_uiManager == null)
                _uiManager = FindFirstObjectByType<UIManager>();

            return _uiManager;
        }

        private static int GetActionDisplayAmount(
            PlayerState player, BettingAction action, int raiseAmount, int playerBetBefore)
        {
            switch (action)
            {
                case BettingAction.Call:
                case BettingAction.AllIn:
                    return Mathf.Max(0, player.CurrentBet - playerBetBefore);
                case BettingAction.Raise:
                    return raiseAmount;
                default:
                    return 0;
            }
        }

        private List<PlayerState> GetNonFolded(List<PlayerState> players)
            => players.Where(p => !p.HasFolded).ToList();

        private static bool IsTestMode =>
            OptionsMenu.Instance != null && OptionsMenu.Instance.TestMode;

        private static bool IsOptionsMenuOpen =>
            OptionsMenu.Instance != null && OptionsMenu.Instance.IsOpen;

        private IEnumerator WaitWhileOptionsMenuOpen()
        {
            yield return new WaitUntil(() => !IsOptionsMenuOpen);
        }

        private IEnumerator DelayForAiAction()
        {
            float elapsed = 0f;
            while (true)
            {
                float target = _aiActionDelay;
                if (target <= 0f)
                    yield break;
                if (elapsed >= target)
                    yield break;

                if (!IsOptionsMenuOpen)
                    elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator DelaySeconds(float seconds)
        {
            if (seconds <= 0f)
                yield break;

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (!IsOptionsMenuOpen)
                    elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void ApplyGodModeIfEnabled(List<PlayerState> players)
        {
            if (OptionsMenu.Instance == null || !OptionsMenu.Instance.GodMode)
                return;

            foreach (PlayerState player in players)
            {
                if (player.Type == PlayerType.Human && player.Chips < 100_000)
                    player.Chips = 100_000;
            }
        }

        private IEnumerator DealPreflopHoleCards(List<PlayerState> active, int sbIndex)
        {
            _boardManager.ClearHoleCards(active);

            int playerCount = active.Count;
            if (playerCount == 0)
                yield break;

            UIManager ui = ResolveUiManager();
            PlayerState human = active.Find(p => p.Type == PlayerType.Human);

            sbIndex = ((sbIndex % playerCount) + playerCount) % playerCount;

            if (ui != null)
                ui.BeginPreflopDealAnimation();

            try
            {
                if (ui != null && ui.AnimatePreflopDeal)
                {
                    for (int round = 0; round < 2; round++)
                    {
                        for (int i = 0; i < playerCount; i++)
                        {
                            PlayerState player = active[(sbIndex + i) % playerCount];
                            _boardManager.DealHoleCardTo(player);
                            int seatIndex = Players.IndexOf(player);
                            ui.PlacePreflopHoleCard(seatIndex, round, player);

                            if (ui.HoleCardDealStagger > 0f)
                                yield return DelaySeconds(ui.HoleCardDealStagger);
                        }
                    }
                }
                else
                {
                    _boardManager.DealHoleCards(active, sbIndex);
                    ui?.ShowAllPreflopHoleCardsFaceDown(active);
                }

                if (human != null && ui != null)
                    yield return ui.RevealHumanHoleCardsAfterDeal(human);
            }
            finally
            {
                ui?.EndPreflopDealAnimation();
            }
        }

        private void NotifyPlayersUpdated() => OnPlayersUpdated?.Invoke(Players);

        private void SetPhase(GamePhase phase)
        {
            CurrentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            ButtonLabelStyle.RegisterFontSizeProvider(() => _buttonFontSize);

            var buttonRow = GameObject.Find("ButtonRow")?.GetComponent<ButtonRowFontSize>();
            if (buttonRow != null)
                buttonRow.Apply();
            else
                FindObjectOfType<UIManager>(includeInactive: true)?.ApplyButtonRowSize();

            var tableLayout = FindObjectOfType<TableLayoutManager>(includeInactive: true);
            tableLayout?.SetCardLayout(120f, _cardGap);
        }
#endif
    }
}

