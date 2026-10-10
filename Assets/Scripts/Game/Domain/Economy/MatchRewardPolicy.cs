using System;

namespace KLTN.Game.Domain.Economy
{
    public enum MatchResultKind : byte
    {
        Win = 0,
        Loss = 1,
        Draw = 2,
        EarlySurrender = 3,
    }

    public readonly struct MatchReward
    {
        public MatchResultKind Kind { get; }
        public int Coins { get; }
        public int Silver { get; }

        public MatchReward(MatchResultKind kind, int coins, int silver)
        {
            Kind = kind;
            Coins = coins;
            Silver = silver;
        }
    }

    /// <summary>
    /// Win: 75 coins + 1 silver. Loss/draw: 25 coins.
    /// Surrendering before round 3: nothing (prevents join-and-surrender farming).
    /// The winner of a surrendered match always receives the full win reward.
    /// </summary>
    public static class MatchRewardPolicy
    {
        public const int WinCoins = 75;
        public const int WinSilver = 1;
        public const int LossCoins = 25;
        public const int EarlySurrenderRoundThreshold = 3;

        public static MatchReward Compute(
            MatchOutcome outcome,
            SeatId viewer,
            SeatId? surrenderedSeat,
            int roundNumber
        )
        {
            if (outcome == MatchOutcome.Running)
            {
                throw new InvalidOperationException("Cannot reward a running match.");
            }

            if (outcome == MatchOutcome.Draw)
            {
                return new MatchReward(MatchResultKind.Draw, LossCoins, 0);
            }

            bool viewerWon =
                (outcome == MatchOutcome.HostWon && viewer == SeatId.Host)
                || (outcome == MatchOutcome.GuestWon && viewer == SeatId.Guest);

            if (viewerWon)
            {
                return new MatchReward(MatchResultKind.Win, WinCoins, WinSilver);
            }

            if (surrenderedSeat == viewer && roundNumber < EarlySurrenderRoundThreshold)
            {
                return new MatchReward(MatchResultKind.EarlySurrender, 0, 0);
            }

            return new MatchReward(MatchResultKind.Loss, LossCoins, 0);
        }
    }
}
