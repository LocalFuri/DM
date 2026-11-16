using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem
{
    /// <summary>One completed betting action in the current hand (bot AI analysis only).</summary>
    public readonly struct HandActionEntry
    {
        public GamePhase     Street           { get; }
        public string        PlayerName       { get; }
        public BettingAction Action           { get; }
        public int           Amount           { get; }
        public int           Pot              { get; }
        public int           StreetRaiseCount { get; }

        public HandActionEntry(
            GamePhase street,
            string playerName,
            BettingAction action,
            int amount,
            int pot,
            int streetRaiseCount)
        {
            Street           = street;
            PlayerName       = playerName ?? string.Empty;
            Action           = action;
            Amount           = amount;
            Pot              = pot;
            StreetRaiseCount = streetRaiseCount;
        }
    }

    /// <summary>
    /// Hand-scoped action history for bot AI analysis. Cleared each hand; does not affect play.
    /// </summary>
    public sealed class HandActionLog
    {
        private readonly List<HandActionEntry> _entries = new List<HandActionEntry>(32);

        public IReadOnlyList<HandActionEntry> Entries => _entries;

        public void Clear() => _entries.Clear();

        /// <summary>Compact one-line summary of actions recorded so far this hand.</summary>
        public string FormatSummary()
        {
            if (_entries.Count == 0)
                return "(none)";

            var parts = new List<string>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
            {
                HandActionEntry e = _entries[i];
                parts.Add($"{e.Street}:{e.PlayerName}:{e.Action}:{e.Amount}");
            }

            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Short per-street summary (last action on each street), optionally appending the pending bot decision.
        /// </summary>
        public string FormatStreetSummary(GamePhase? pendingStreet = null, BettingAction? pendingAction = null)
        {
            var lastByStreet = new Dictionary<GamePhase, BettingAction>();
            for (int i = 0; i < _entries.Count; i++)
            {
                HandActionEntry e = _entries[i];
                lastByStreet[NormalizeStreet(e.Street)] = e.Action;
            }

            if (pendingStreet.HasValue && pendingAction.HasValue)
                lastByStreet[NormalizeStreet(pendingStreet.Value)] = pendingAction.Value;

            if (lastByStreet.Count == 0)
                return "(none)";

            var lines = new List<string>(4);
            AppendStreetLine(lines, lastByStreet, GamePhase.PreFlop, "Preflop");
            AppendStreetLine(lines, lastByStreet, GamePhase.Flop, "Flop");
            AppendStreetLine(lines, lastByStreet, GamePhase.Turn, "Turn");
            AppendStreetLine(lines, lastByStreet, GamePhase.River, "River");
            return lines.Count > 0 ? string.Join("\n", lines) : "(none)";
        }

        private static GamePhase NormalizeStreet(GamePhase street) => street;

        private static void AppendStreetLine(
            List<string> lines,
            Dictionary<GamePhase, BettingAction> lastByStreet,
            GamePhase street,
            string label)
        {
            if (lastByStreet.TryGetValue(street, out BettingAction action))
                lines.Add($"{label}: {action}");
        }

        public void Record(
            GamePhase street,
            PlayerState player,
            BettingAction action,
            int amount,
            int pot,
            int streetRaiseCount)
        {
            string name = player != null ? player.Name : "(null)";
            var entry = new HandActionEntry(street, name, action, amount, pot, streetRaiseCount);
            _entries.Add(entry);

            Debug.Log(
                $"[HandActionLog] street={entry.Street} player={entry.PlayerName} " +
                $"action={entry.Action} amount={entry.Amount} pot={entry.Pot} " +
                $"streetRaiseCount={entry.StreetRaiseCount}");
        }
    }
}
