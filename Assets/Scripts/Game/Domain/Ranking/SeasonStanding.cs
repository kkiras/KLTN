using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain.Ranking
{
    /// <summary>One player's row in a season leaderboard.</summary>
    public sealed class SeasonStanding
    {
        /// <summary>Win rate is stored in basis points: 10000 = 100 %.</summary>
        public const int WinRateScale = 10000;

        /// <summary>
        /// Leaderboard order: points desc, then win rate desc, then username asc.
        /// Firestore sorts the same way on (points, winRate, username).
        /// </summary>
        public static readonly IComparer<SeasonStanding> LeaderboardOrder = new LeaderboardComparer();

        public string UserId { get; }
        public string Username { get; }
        public int Points { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int Draws { get; }
        public int Matches { get; }

        public RankTier Tier => RankPolicy.TierFor(Points);

        /// <summary>Wins / matches (draws count as played); 0 before the first match.</summary>
        public double WinRate => (double)WinRateBasisPoints / WinRateScale;

        /// <summary>Stored "winRate" field: wins * 10000 / matches rounded down (same formula as the Rules).</summary>
        public int WinRateBasisPoints => WinRateOf(Wins, Matches);

        public SeasonStanding(
            string userId,
            string username,
            int points,
            int wins,
            int losses,
            int draws,
            int matches
        )
        {
            UserId = userId;
            Username = username ?? string.Empty;
            Points = Math.Max(0, points);
            Wins = Math.Max(0, wins);
            Losses = Math.Max(0, losses);
            Draws = Math.Max(0, draws);
            Matches = Math.Max(0, matches);
        }

        public static int WinRateOf(int wins, int matches)
        {
            return matches > 0 ? (int)((long)Math.Max(0, wins) * WinRateScale / matches) : 0;
        }

        private sealed class LeaderboardComparer : IComparer<SeasonStanding>
        {
            public int Compare(SeasonStanding left, SeasonStanding right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (left == null)
                {
                    return 1;
                }

                if (right == null)
                {
                    return -1;
                }

                int byPoints = right.Points.CompareTo(left.Points);

                if (byPoints != 0)
                {
                    return byPoints;
                }

                int byWinRate = right.WinRateBasisPoints.CompareTo(left.WinRateBasisPoints);
                return byWinRate != 0 ? byWinRate : string.CompareOrdinal(left.Username, right.Username);
            }
        }
    }

    /// <summary>Format and validation of the public username "user" + 6 digits.</summary>
    public static class UsernamePolicy
    {
        #region Format

        public const string Prefix = "user";
        public const int DigitCount = 6;
        public const int MaxNumber = 999999;

        #endregion

        #region Public API

        public static string Format(int number)
        {
            if (number < 0 || number > MaxNumber)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "Username number must have 6 digits.");
            }

            return Prefix + number.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool IsValid(string username)
        {
            if (username == null || username.Length != Prefix.Length + DigitCount)
            {
                return false;
            }

            if (!username.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            for (int index = Prefix.Length; index < username.Length; index++)
            {
                if (username[index] < '0' || username[index] > '9')
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }

    /// <summary>
    /// Matchmaking prerequisite: players may only meet opponents within
    /// <see cref="MaxTierDifference"/> tiers (enforced by the matchmaker queue rule on
    /// Players.CustomData.rank_tier). Host/Client sessions bypass the matchmaker.
    /// </summary>
    public static class RankMatchmakingPolicy
    {
        public const int MaxTierDifference = 1;

        public static bool CanMeet(RankTier left, RankTier right)
        {
            return Math.Abs((int)left - (int)right) <= MaxTierDifference;
        }
    }
}
