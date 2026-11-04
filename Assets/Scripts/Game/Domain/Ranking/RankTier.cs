using System;

namespace KLTN.Game.Domain.Ranking
{
    /// <summary>Seasonal rank, always derived from season points (never stored).</summary>
    public enum RankTier : byte
    {
        Bronze = 0,
        Silver = 1,
        Gold = 2,
        Platinum = 3,
        Diamond = 4,
        Master = 5,
        Challenger = 6,
    }

    /// <summary>
    /// Point thresholds per tier. Single source of truth for the client, the
    /// leaderboard UI and the matchmaker ticket (rank_tier).
    /// </summary>
    public static class RankPolicy
    {
        #region Thresholds

        private static readonly int[] MinPointsByTier = { 0, 60, 150, 270, 420, 600, 800 };

        public const int TierCount = 7;

        #endregion

        #region Public API

        public static int MinPoints(RankTier tier)
        {
            int index = (int)tier;

            if (index < 0 || index >= TierCount)
            {
                throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown rank tier.");
            }

            return MinPointsByTier[index];
        }

        public static RankTier TierFor(int points)
        {
            for (int index = TierCount - 1; index > 0; index--)
            {
                if (points >= MinPointsByTier[index])
                {
                    return (RankTier)index;
                }
            }

            return RankTier.Bronze;
        }

        /// <summary>Points still needed for the next tier; 0 at Challenger.</summary>
        public static int PointsToNextTier(int points)
        {
            RankTier tier = TierFor(points);

            if (tier == RankTier.Challenger)
            {
                return 0;
            }

            return MinPointsByTier[(int)tier + 1] - Math.Max(0, points);
        }

        #endregion
    }
}
