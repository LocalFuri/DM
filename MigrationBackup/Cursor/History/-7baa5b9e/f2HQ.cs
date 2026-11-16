using UnityEngine;

namespace TexasHoldem
{
    /// <summary>Per-action accent colors for confirmation badges and related UI.</summary>
    public static class ActionColors
    {
        public static readonly Color Raise = ButtonLabelStyle.RaiseText;
        public static readonly Color Fold  = new Color32(255, 60, 60, 255);
        public static readonly Color Check = new Color32(40, 220, 90, 255);
        public static readonly Color Call  = new Color32(0, 255, 255, 255);
        public static readonly Color AllIn = new Color32(170, 70, 255, 255);

        public static Color For(BettingAction action) => action switch
        {
            BettingAction.Raise => Raise,
            BettingAction.Fold  => Fold,
            BettingAction.Check => Check,
            BettingAction.Call  => Call,
            BettingAction.AllIn => AllIn,
            _                   => Raise,
        };
    }
}
