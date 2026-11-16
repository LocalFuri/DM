using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem
{
    /// <summary>
    /// Bot decisions use the same preflop chart, position rules, and postflop pot-odds logic as the human HUD.
    /// </summary>
    public class AIController
    {
        private const float FlopValueEquityThreshold   = 65f;
        private const float FlopThinValueEquityMin     = 52f;
        private const int   FlopThinValueMaxOpponents  = 2;
        private const float TurnSecondBarrelEquityMin  = 50f;
        private const float FlopSmallBetPotFraction     = 0.33f;
        private const float FlopLargeBetPotFraction     = 0.67f;

        private static readonly BoardTextureFlags FlopWetTextureFlags =
            BoardTextureFlags.ThreeFlush
            | BoardTextureFlags.FourFlush
            | BoardTextureFlags.Connected
            | BoardTextureFlags.FourStraight;

        private static readonly PostflopDrawFlags FlopSemiBluffDraws =
            PostflopDrawFlags.FlushDraw | PostflopDrawFlags.OpenEndedStraightDraw;

        /// <summary>Player who last bet or raised on the flop (cleared each hand).</summary>
        private PlayerState _flopLastAggressor;

        private readonly HandActionLog _handActionLog = new HandActionLog();

        /// <summary>Hand-scoped action history for bot AI analysis (read-only).</summary>
        public IReadOnlyList<HandActionEntry> HandActions => _handActionLog.Entries;

        /// <summary>Clears per-hand aggression tracking and action log at the start of a new hand.</summary>
        public void ClearHandState()
        {
            _flopLastAggressor = null;
            _handActionLog.Clear();
        }

        /// <summary>Appends a completed table action for bot AI analysis (no effect on decisions).</summary>
        public void RecordHandAction(
            GamePhase street,
            PlayerState player,
            BettingAction action,
            int amount,
            int pot,
            int streetRaiseCount)
        {
            _handActionLog.Record(street, player, action, amount, pot, streetRaiseCount);
        }

        /// <summary>Records the last flop bet/raise aggressor (full or short).</summary>
        public void NoteFlopAggression(PlayerState player)
        {
            if (player != null)
                _flopLastAggressor = player;
        }

        /// <summary>Decides the AI's next betting action using <see cref="BettingAdvisor"/>.</summary>
        public (BettingAction action, int raiseAmount) DecideAction(
            PlayerState                 player,
            IReadOnlyList<Card>         communityCards,
            BettingManager              betting,
            IReadOnlyList<PlayerState>  allPlayers,
            GamePhase                   phase,
            int                         potAmount,
            int                         tableCurrentBet,
            int                         bigBlindAmount,
            int                         streetRaiseCount,
            PreflopSeatBucket           preflopSeat,
            bool                        testMode = false)
        {
            int  callAmount = betting.GetCallAmount(player);
            bool canCheck   = callAmount <= 0;
            bool canCall    = callAmount > 0 && player.Chips > 0;
            bool canRaise   = betting.CanRaise(player);
            bool isPreflop  = phase == GamePhase.PreFlop;
            bool facingRaise = tableCurrentBet > bigBlindAmount;

            if (testMode)
                return canCheck ? (BettingAction.Check, 0) : (BettingAction.Call, 0);

            PreflopHandGroup preflopGroup = PreflopHandGroup.Weak;
            float            equityPercent = 0f;

            if (player.HoleCards != null && player.HoleCards.Count >= 2)
            {
                preflopGroup = PreflopStrategy.ClassifyHand(player.HoleCards);

                if (!isPreflop)
                    equityPercent = EstimateEquityPercent(player, communityCards, allPlayers);
            }

            if (isPreflop)
                PreflopStrategy.LogEffectiveStack(player, allPlayers, tableCurrentBet, bigBlindAmount);

            BettingAdvice advice = BettingAdvisor.Recommend(
                equityPercent,
                potAmount,
                callAmount,
                canCheck,
                canRaise,
                canCall,
                isPreflop,
                preflopGroup,
                preflopSeat,
                facingRaise,
                streetRaiseCount,
                player.Chips,
                player.HoleCards,
                phase);

            BettingAdvice adviceAfterAdvisor = advice;

            int opponentCount = CountActiveOpponents(allPlayers, player);

            advice = ApplyFlopOrTurnSemiBluffIfEligible(
                advice, phase, canCheck, canRaise, player.HoleCards, communityCards);
            BettingAdvice adviceAfterSemiBluff = advice;

            advice = ApplyFlopThinValueIfEligible(
                advice, phase, canCheck, canRaise, equityPercent,
                opponentCount, communityCards);
            BettingAdvice adviceAfterThinValue = advice;

            advice = ApplyTurnSecondBarrelIfEligible(
                advice, phase, canCheck, canRaise, player, equityPercent);
            BettingAdvice adviceAfterBarrel = advice;

            (BettingAction action, int raiseAmount) resolved = BettingAdvisor.ResolveAction(
                advice, betting, player, isPreflop, streetRaiseCount, potAmount, equityPercent, phase);

            resolved = ApplyFlopOpenBetSizing(
                resolved, phase, callAmount, equityPercent, potAmount, betting, player,
                player.HoleCards, communityCards);

            if (!isPreflop)
            {
                LogPostflopDecision(
                    player,
                    phase,
                    communityCards,
                    equityPercent,
                    potAmount,
                    callAmount,
                    tableCurrentBet,
                    opponentCount,
                    canCheck,
                    resolved.action,
                    resolved.raiseAmount,
                    adviceAfterAdvisor,
                    adviceAfterSemiBluff,
                    adviceAfterThinValue,
                    adviceAfterBarrel);
            }

            return resolved;
        }

        /// <summary>
        /// Flop open bets only: ~1/3 pot for semi-bluffs and dry value; ~2/3 pot for value on wetter flops.
        /// </summary>
        private static (BettingAction action, int raiseAmount) ApplyFlopOpenBetSizing(
            (BettingAction action, int raiseAmount) resolved,
            GamePhase phase,
            int callAmount,
            float equityPercent,
            int potAmount,
            BettingManager betting,
            PlayerState player,
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards)
        {
            if (phase != GamePhase.Flop
                || callAmount > 0
                || resolved.action != BettingAction.Raise
                || betting == null
                || player == null)
            {
                return resolved;
            }

            int minIncrement = betting.GetMinRaiseIncrement();
            int maxIncrement = betting.GetMaxRaiseIncrement(player);
            if (maxIncrement < minIncrement || maxIncrement <= 0)
                return resolved;

            float potFraction = ResolveFlopOpenBetPotFraction(equityPercent, holeCards, communityCards);
            int targetTotal = potAmount > 0
                ? Mathf.RoundToInt(potAmount * potFraction)
                : minIncrement;

            int increment = Mathf.Max(targetTotal, minIncrement);
            increment = Mathf.Clamp(increment, minIncrement, maxIncrement);

            if (increment >= player.Chips)
                return (BettingAction.AllIn, 0);

            return (BettingAction.Raise, increment);
        }

        private static float ResolveFlopOpenBetPotFraction(
            float equityPercent,
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards)
        {
            bool isValue = equityPercent >= FlopValueEquityThreshold;
            PostflopDrawFlags draws = PostflopDrawDetector.Detect(holeCards, communityCards);
            bool isSemiBluff = !isValue && (draws & FlopSemiBluffDraws) != 0;

            if (isSemiBluff)
                return FlopSmallBetPotFraction;

            BoardTextureFlags texture = BoardTextureAnalyzer.Analyze(communityCards);
            bool isWet = (texture & FlopWetTextureFlags) != 0;

            if (isValue && isWet)
                return FlopLargeBetPotFraction;

            // Dry / thin value and any other flop open → small size.
            return FlopSmallBetPotFraction;
        }

        /// <summary>
        /// Flop only, when checked to: small (~1/3 pot) thin value bet at 52–65% equity,
        /// only with ≤2 opponents on a dry board. Strong ≥65% value stays in the advisor.
        /// Runs after semi-bluff so FD/OESD stabs are unchanged.
        /// </summary>
        private static BettingAdvice ApplyFlopThinValueIfEligible(
            BettingAdvice advice,
            GamePhase phase,
            bool canCheck,
            bool canRaise,
            float equityPercent,
            int activeOpponents,
            IReadOnlyList<Card> communityCards)
        {
            if (phase != GamePhase.Flop || !canCheck || !canRaise)
                return advice;

            if (advice != BettingAdvice.Check)
                return advice;

            if (activeOpponents > FlopThinValueMaxOpponents)
                return advice;

            if (equityPercent < FlopThinValueEquityMin
                || equityPercent >= FlopValueEquityThreshold)
            {
                return advice;
            }

            BoardTextureFlags texture = BoardTextureAnalyzer.Analyze(communityCards);
            if ((texture & FlopWetTextureFlags) != 0)
                return advice;

            return BettingAdvice.Raise;
        }

        /// <summary>
        /// Turn second barrel: if this bot was the last flop aggressor and is checked to,
        /// continue betting with equity ≥ 50% (existing ~⅔ pot sizing).
        /// </summary>
        private BettingAdvice ApplyTurnSecondBarrelIfEligible(
            BettingAdvice advice,
            GamePhase phase,
            bool canCheck,
            bool canRaise,
            PlayerState player,
            float equityPercent)
        {
            if (phase != GamePhase.Turn || !canCheck || !canRaise)
                return advice;

            if (advice != BettingAdvice.Check)
                return advice;

            if (player == null || player != _flopLastAggressor)
                return advice;

            if (equityPercent >= TurnSecondBarrelEquityMin)
                return BettingAdvice.Raise;

            return advice;
        }

        /// <summary>
        /// Flop/turn only, when checking is free: keep ≥65% value bets from the advisor, and also
        /// bet open-ended straight draws / flush draws (not gutshots). Turn uses existing ~2/3 pot sizing.
        /// </summary>
        private static BettingAdvice ApplyFlopOrTurnSemiBluffIfEligible(
            BettingAdvice advice,
            GamePhase phase,
            bool canCheck,
            bool canRaise,
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards)
        {
            if ((phase != GamePhase.Flop && phase != GamePhase.Turn) || !canCheck || !canRaise)
                return advice;

            if (advice != BettingAdvice.Check)
                return advice;

            PostflopDrawFlags draws = PostflopDrawDetector.Detect(holeCards, communityCards);
            if ((draws & FlopSemiBluffDraws) != 0)
                return BettingAdvice.Raise;

            return advice;
        }

        private static float EstimateEquityPercent(
            PlayerState player,
            IReadOnlyList<Card> communityCards,
            IReadOnlyList<PlayerState> allPlayers)
        {
            int opponents = CountActiveOpponents(allPlayers, player);
            if (opponents <= 0)
                return 100f;

            var board = communityCards ?? System.Array.Empty<Card>();
            MonteCarloResult result = MonteCarloSimulator.Simulate(
                player.HoleCards,
                board,
                opponents,
                MonteCarloSimulator.DefaultSimulationCount);

            return result.EquityPercent;
        }

        private static int CountActiveOpponents(
            IReadOnlyList<PlayerState> allPlayers,
            PlayerState hero)
        {
            if (allPlayers == null || hero == null)
                return 0;

            int count = 0;
            foreach (PlayerState player in allPlayers)
            {
                if (player == null || player == hero || player.HasFolded)
                    continue;

                count++;
            }

            return count;
        }

        // Mirrors BettingAdvisor postflop thresholds (logging only — keep in sync).
        private const float AdvisorCallMargin      = 3f;
        private const float AdvisorRaiseEdge       = 15f;
        private const float AdvisorRiverRaiseEdge  = 25f;
        private const float AdvisorRiverThinValue  = 55f;

        private void LogPostflopDecision(
            PlayerState player,
            GamePhase street,
            IReadOnlyList<Card> communityCards,
            float equityPercent,
            int potAmount,
            int callAmount,
            int tableCurrentBet,
            int opponentCount,
            bool canCheck,
            BettingAction action,
            int raiseAmount,
            BettingAdvice afterAdvisor,
            BettingAdvice afterSemiBluff,
            BettingAdvice afterThinValue,
            BettingAdvice afterBarrel)
        {
            BoardTextureFlags texture = BoardTextureAnalyzer.Analyze(communityCards);

            PostflopDrawFlags draws = PostflopDrawFlags.None;
            if (player?.HoleCards != null && player.HoleCards.Count >= 2)
                draws = PostflopDrawDetector.Detect(player.HoleCards, communityCards);

            float? potOddsPercent = null;
            float? decisionThreshold = null;
            if (!canCheck && callAmount > 0)
            {
                int denominator = potAmount + callAmount;
                if (denominator > 0)
                {
                    float needed = 100f * callAmount / denominator;
                    potOddsPercent = needed;
                    float raiseEdge = street == GamePhase.River ? AdvisorRiverRaiseEdge : AdvisorRaiseEdge;
                    decisionThreshold = ResolveFacingDecisionThreshold(
                        afterAdvisor, needed, raiseEdge);
                }
            }
            else
            {
                decisionThreshold = ResolveCheckedToThreshold(
                    afterAdvisor, afterSemiBluff, afterThinValue, afterBarrel, street, equityPercent);
            }

            string reason = BuildPlainEnglishReason(
                afterAdvisor,
                afterSemiBluff,
                afterThinValue,
                afterBarrel,
                equityPercent,
                potOddsPercent,
                decisionThreshold,
                canCheck,
                street);

            int totalBet = ResolveTotalBetAfterAction(
                action, tableCurrentBet, player, raiseAmount);

            string handSummary = _handActionLog.FormatStreetSummary(street, action);

            Debug.Log(
                "[PostflopAI]\n" +
                $"Player: {player?.Name ?? "(null)"}\n" +
                $"Street: {FormatStreetLabel(street)}\n" +
                $"Hand: {FormatHoleCards(player)}\n" +
                $"Board: {FormatBoard(communityCards)}\n" +
                $"Category: {FormatDetailedHandDescription(player, communityCards)}\n" +
                $"Equity: {equityPercent:F1}%\n" +
                $"Pot Odds: {FormatOptionalPercent(potOddsPercent)}\n" +
                $"Threshold: {FormatOptionalPercent(decisionThreshold)}\n" +
                $"Opponents: {opponentCount}\n" +
                $"Draw: {FormatDrawFlags(draws)}\n" +
                $"Board Texture: {FormatBoardTextureLabel(texture)}\n" +
                $"Decision: {action}\n" +
                $"Reason: {reason}\n" +
                $"Bet Size: {totalBet}\n" +
                "Hand Summary:\n" +
                handSummary);
        }

        private static float? ResolveFacingDecisionThreshold(
            BettingAdvice advisorAdvice,
            float potOddsNeeded,
            float raiseEdge)
        {
            switch (advisorAdvice)
            {
                case BettingAdvice.Raise:
                    return potOddsNeeded + raiseEdge;
                case BettingAdvice.Call:
                case BettingAdvice.Fold:
                    return potOddsNeeded + AdvisorCallMargin;
                default:
                    return potOddsNeeded + raiseEdge;
            }
        }

        private static float? ResolveCheckedToThreshold(
            BettingAdvice afterAdvisor,
            BettingAdvice afterSemiBluff,
            BettingAdvice afterThinValue,
            BettingAdvice afterBarrel,
            GamePhase street,
            float equityPercent)
        {
            if (afterBarrel != afterThinValue)
                return TurnSecondBarrelEquityMin;

            if (afterThinValue != afterSemiBluff)
                return FlopThinValueEquityMin;

            if (afterSemiBluff != afterAdvisor)
                return null; // draw-based; no equity threshold

            if (afterAdvisor == BettingAdvice.Raise)
            {
                if (street == GamePhase.River && equityPercent < FlopValueEquityThreshold)
                    return AdvisorRiverThinValue;

                return FlopValueEquityThreshold;
            }

            return FlopValueEquityThreshold;
        }

        private static string BuildPlainEnglishReason(
            BettingAdvice afterAdvisor,
            BettingAdvice afterSemiBluff,
            BettingAdvice afterThinValue,
            BettingAdvice afterBarrel,
            float equityPercent,
            float? potOddsPercent,
            float? facingThreshold,
            bool canCheck,
            GamePhase street)
        {
            if (afterBarrel != afterThinValue)
                return $"Second barrel as flop aggressor (equity ≥ {TurnSecondBarrelEquityMin:F0}%)";

            if (afterThinValue != afterSemiBluff)
                return $"Thin value bet (equity {FlopThinValueEquityMin:F0}–{FlopValueEquityThreshold:F0}% on dry board)";

            if (afterSemiBluff != afterAdvisor)
                return "Semi-bluff with flush draw or open-ended straight draw";

            if (canCheck)
            {
                if (afterAdvisor == BettingAdvice.Raise)
                {
                    if (street == GamePhase.River && equityPercent < FlopValueEquityThreshold)
                        return $"River thin value (equity ≥ {AdvisorRiverThinValue:F0}%)";

                    return $"Strong value bet (equity ≥ {FlopValueEquityThreshold:F0}%)";
                }

                return $"Equity below value threshold ({FlopValueEquityThreshold:F0}%)";
            }

            if (!potOddsPercent.HasValue || !facingThreshold.HasValue)
                return $"Advisor:{afterAdvisor}";

            float threshold = facingThreshold.Value;
            switch (afterAdvisor)
            {
                case BettingAdvice.Raise:
                    return $"Equity exceeded raise threshold ({threshold:F1}%)";
                case BettingAdvice.Call:
                    return $"Equity exceeded call threshold ({threshold:F1}%)";
                case BettingAdvice.Fold:
                    return $"Equity below call threshold ({threshold:F1}%)";
                default:
                    return $"Advisor:{afterAdvisor}";
            }
        }

        private static int ResolveTotalBetAfterAction(
            BettingAction action,
            int tableCurrentBet,
            PlayerState player,
            int raiseAmount)
        {
            if (player == null)
                return 0;

            switch (action)
            {
                case BettingAction.Raise:
                    return tableCurrentBet + Mathf.Max(0, raiseAmount);
                case BettingAction.Call:
                    return tableCurrentBet;
                case BettingAction.AllIn:
                    return player.CurrentBet + player.Chips;
                case BettingAction.Check:
                case BettingAction.Fold:
                default:
                    return player.CurrentBet;
            }
        }

        private static string FormatOptionalPercent(float? value) =>
            value.HasValue ? $"{value.Value:F1}%" : "n/a";

        private static string FormatStreetLabel(GamePhase street) =>
            street == GamePhase.PreFlop ? "Preflop" : street.ToString();

        private static string FormatHoleCards(PlayerState player)
        {
            if (player?.HoleCards == null || player.HoleCards.Count < 2)
                return "(none)";

            return $"{player.HoleCards[0]} {player.HoleCards[1]}";
        }

        private static string FormatDetailedHandDescription(
            PlayerState player,
            IReadOnlyList<Card> communityCards)
        {
            if (player?.HoleCards == null || player.HoleCards.Count < 2)
                return "Unknown";

            if (communityCards == null || communityCards.Count < 3)
                return "Unknown";

            var cards = new List<Card>(2 + communityCards.Count);
            cards.Add(player.HoleCards[0]);
            cards.Add(player.HoleCards[1]);
            foreach (Card card in communityCards)
            {
                if (card != null)
                    cards.Add(card);
            }

            if (cards.Count < 5)
                return "Unknown";

            HandResult result;
            try
            {
                result = HandEvaluator.Evaluate(cards);
            }
            catch (System.Exception)
            {
                return "Unknown";
            }

            string special = DescribePairRelativeToBoard(player.HoleCards, communityCards, result);
            if (!string.IsNullOrEmpty(special))
                return special;

            return HandDisplayNames.Format(result);
        }

        private static string DescribePairRelativeToBoard(
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards,
            HandResult result)
        {
            if (result == null || result.Rank != HandRank.OnePair)
                return null;

            Rank h0 = holeCards[0].Rank;
            Rank h1 = holeCards[1].Rank;
            bool pocketPair = h0 == h1;

            Rank boardHigh = communityCards[0].Rank;
            for (int i = 1; i < communityCards.Count; i++)
            {
                if (communityCards[i] != null && communityCards[i].Rank > boardHigh)
                    boardHigh = communityCards[i].Rank;
            }

            if (pocketPair && h0 > boardHigh)
                return "Overpair";

            Rank pairRank = (Rank)result.Tiebreakers[0];
            if (!pocketPair && (h0 == pairRank || h1 == pairRank) && pairRank == boardHigh)
            {
                Rank kicker = h0 == pairRank ? h1 : h0;
                if (kicker == Rank.Ace)
                    return "Top Pair, Top Kicker";
                return $"Top Pair, {HandDisplayNames.RankName((int)kicker)} kicker";
            }

            return null;
        }

        private static string FormatBoardTextureLabel(BoardTextureFlags flags) =>
            flags == BoardTextureFlags.None ? "Dry" : flags.ToString();

        private static string FormatDrawFlags(PostflopDrawFlags flags) =>
            flags == PostflopDrawFlags.None ? "None" : flags.ToString();

        private static string FormatBoard(IReadOnlyList<Card> communityCards)
        {
            if (communityCards == null || communityCards.Count == 0)
                return "(none)";

            var parts = new List<string>(communityCards.Count);
            foreach (Card card in communityCards)
            {
                if (card != null)
                    parts.Add(card.ToString());
            }

            return parts.Count > 0 ? string.Join(" ", parts) : "(none)";
        }
    }
}
