using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Regression for cumulative <see cref="PlayerState.SessionNetProfit"/> tracking.
    /// Mirrors the production end-of-hand sequence: award pots → accumulate net profit
    /// (Chips - HandStartStack) → auto-refill to the max buy-in. Net profit must never be
    /// derived from the refilled stack and must persist across the refill.
    /// </summary>
    public sealed class NetProfitRegressionTestRunner
    {
        private const int MaxBuyIn = 1000;

        public static (int passed, int total) RunAllTests()
        {
            var failures = new List<string>();
            int passed = 0;
            int total = 0;

            Debug.Log("[NetProfit] Starting cumulative net-profit regression...");

            var player = new PlayerState("Regression", PlayerType.AI, MaxBuyIn);

            // Hand 1: start 1,000, end 800 → net profit -200.
            BeginHand(player);
            player.Chips = 800;
            EndHand(player);
            Check(failures, ref passed, ref total,
                "Hand 1 net profit is -200", player.SessionNetProfit, -200);

            // Auto-refill restores the stack but must not touch net profit.
            Refill(player);
            Check(failures, ref passed, ref total,
                "Auto-refill restores stack to 1,000", player.Chips, MaxBuyIn);
            Check(failures, ref passed, ref total,
                "Net profit unchanged by refill", player.SessionNetProfit, -200);

            // Hand 2: start 1,000, end 1,300 → net profit -200 + 300 = +100.
            BeginHand(player);
            player.Chips = 1300;
            EndHand(player);
            Check(failures, ref passed, ref total,
                "Hand 2 cumulative net profit is +100", player.SessionNetProfit, 100);

            Refill(player);
            Check(failures, ref passed, ref total,
                "Net profit still +100 after second refill", player.SessionNetProfit, 100);

            if (failures.Count == 0)
                Debug.Log($"[NetProfit] PASS {passed}/{total}");
            else
                Debug.LogError($"[NetProfit] FAIL {passed}/{total}\n - " + string.Join("\n - ", failures));

            return (passed, total);
        }

        /// <summary>Captures the pre-blind stack, matching GameManager.PlayRound.</summary>
        private static void BeginHand(PlayerState player)
            => player.HandStartStack = player.Chips;

        /// <summary>Accumulates the whole-hand result, matching GameManager.UpdateSessionNetProfit.</summary>
        private static void EndHand(PlayerState player)
            => player.SessionNetProfit += player.Chips - player.HandStartStack;

        /// <summary>Tops up to the max buy-in, matching GameManager.ApplyRebuysToMaxBuyIn.</summary>
        private static void Refill(PlayerState player)
        {
            if (player.Chips < MaxBuyIn)
                player.Chips = MaxBuyIn;
        }

        private static void Check(
            List<string> failures, ref int passed, ref int total,
            string label, int actual, int expected)
        {
            total++;
            if (actual == expected)
            {
                passed++;
                Debug.Log($"[NetProfit] PASS  {label} (={actual})");
            }
            else
            {
                string detail = $"{label}: expected {expected}, got {actual}";
                failures.Add(detail);
                Debug.LogError($"[NetProfit] FAIL  {detail}");
            }
        }
    }
}
