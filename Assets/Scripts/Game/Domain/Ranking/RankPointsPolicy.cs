using System;

namespace KLTN.Game.Domain.Ranking
{
    public enum RankedResultKind : byte
    {
        Win = 0,
        Loss = 1,
        Draw = 2,
        Surrender = 3,
    }

    public readonly struct RankedResult
    {
        public RankedResultKind Kind { get; }
        public int Delta { get; }

        public RankedResult(RankedResultKind kind, int delta)
        {
            Kind = kind;
            Delta = delta;
        }
    }

    /// <summary>
    /// Fixed points per ranked match (no Elo, no demotion protection).
    /// Win +30, loss -20, draw 0, own surrender -20. Season points never go below 0.
    /// The same table is embedded in Firestore Security Rules by the rules exporter.
    /// </summary>
    public static class RankPointsPolicy
    {
        #region Points Table

        public const int WinPoints = 30;
        public const int LossPoints = -20;
        public const int DrawPoints = 0;
        public const int SurrenderPoints = -20;

        /// <summary>Security Rules reject a second result within this many seconds (anti-farming).</summary>
        public const int MinSecondsBetweenResults = 60;

        /// <summary>Allowed difference between the match host's clock and Google's clock.</summary>
        public const int MaxHostClockSkewSeconds = 120;

        #endregion

        #region Public API

        public static RankedResult Compute(MatchOutcome outcome, SeatId viewer, SeatId? surrenderedSeat)
        {
            if (outcome == MatchOutcome.Running)
            {
                throw new InvalidOperationException("Cannot score a running match.");
            }

            if (outcome == MatchOutcome.Draw)
            {
                return new RankedResult(RankedResultKind.Draw, DrawPoints);
            }

            bool viewerWon =
                (outcome == MatchOutcome.HostWon && viewer == SeatId.Host)
                || (outcome == MatchOutcome.GuestWon && viewer == SeatId.Guest);

            if (viewerWon)
            {
                return new RankedResult(RankedResultKind.Win, WinPoints);
            }

            return surrenderedSeat == viewer
                ? new RankedResult(RankedResultKind.Surrender, SurrenderPoints)
                : new RankedResult(RankedResultKind.Loss, LossPoints);
        }

        public static int DeltaFor(RankedResultKind kind)
        {
            switch (kind)
            {
                case RankedResultKind.Win:
                    return WinPoints;
                case RankedResultKind.Draw:
                    return DrawPoints;
                case RankedResultKind.Surrender:
                    return SurrenderPoints;
                default:
                    return LossPoints;
            }
        }

        /// <summary>Season points after one result, floored at 0.</summary>
        public static int ApplyDelta(int points, int delta)
        {
            return Math.Max(0, points + delta);
        }

        /// <summary>Wire name stored in Firestore (results/{matchId}.result).</summary>
        public static string WireName(RankedResultKind kind)
        {
            switch (kind)
            {
                case RankedResultKind.Win:
                    return "win";
                case RankedResultKind.Draw:
                    return "draw";
                case RankedResultKind.Surrender:
                    return "surrender";
                default:
                    return "loss";
            }
        }

        #endregion
    }
}
