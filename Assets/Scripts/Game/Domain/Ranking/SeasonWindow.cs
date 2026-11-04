using System;

namespace KLTN.Game.Domain.Ranking
{
    /// <summary>
    /// One season's time window in Unix milliseconds (UTC).
    /// Points lock at <see cref="LockUtcMs"/>: only matches that STARTED before the lock
    /// and FINISHED before <see cref="EndUtcMs"/> are scored.
    /// </summary>
    public readonly struct SeasonWindow
    {
        public int SeasonId { get; }
        public long StartUtcMs { get; }
        public long EndUtcMs { get; }
        public long LockUtcMs { get; }

        public SeasonWindow(int seasonId, long startUtcMs, long endUtcMs, long lockUtcMs)
        {
            if (seasonId < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(seasonId), seasonId, "Season IDs start at 1.");
            }

            if (endUtcMs <= startUtcMs)
            {
                throw new ArgumentException("A season must end after it starts.", nameof(endUtcMs));
            }

            SeasonId = seasonId;
            StartUtcMs = startUtcMs;
            EndUtcMs = endUtcMs;
            LockUtcMs = Math.Max(startUtcMs, Math.Min(lockUtcMs, endUtcMs));
        }

        public bool IsEnded(long nowUtcMs)
        {
            return nowUtcMs >= EndUtcMs;
        }

        public bool IsLocked(long nowUtcMs)
        {
            return nowUtcMs >= LockUtcMs;
        }

        public long MillisecondsUntilEnd(long nowUtcMs)
        {
            return Math.Max(0, EndUtcMs - nowUtcMs);
        }
    }

    public enum RankedEligibility : byte
    {
        Eligible = 0,
        NoActiveSeason = 1,
        StartedBeforeSeason = 2,
        StartedAfterLock = 3,
        FinishedAfterSeasonEnd = 4,
    }

    /// <summary>
    /// Decides whether a finished match may change season points.
    /// Mirrors the checks in Firestore Security Rules (which use request.time).
    /// </summary>
    public static class RankedEligibilityPolicy
    {
        #region Public API

        public static RankedEligibility Check(SeasonWindow? season, long matchStartedUtcMs, long finishedUtcMs)
        {
            if (!season.HasValue)
            {
                return RankedEligibility.NoActiveSeason;
            }

            SeasonWindow window = season.Value;

            if (finishedUtcMs >= window.EndUtcMs)
            {
                return RankedEligibility.FinishedAfterSeasonEnd;
            }

            if (matchStartedUtcMs < window.StartUtcMs)
            {
                return RankedEligibility.StartedBeforeSeason;
            }

            if (matchStartedUtcMs >= window.LockUtcMs)
            {
                return RankedEligibility.StartedAfterLock;
            }

            return RankedEligibility.Eligible;
        }

        #endregion
    }

    /// <summary>
    /// Seasons as consecutive fixed-length periods counted from an anchor:
    /// season <c>AnchorSeasonId + k</c> starts at <c>AnchorStartUtcMs + k * DurationMs</c>.
    /// The season is a function of the clock, so a season opens the moment its start time
    /// passes; the Cloud Code tick only records history and applies setting changes.
    /// </summary>
    public readonly struct SeasonCalendar : IEquatable<SeasonCalendar>
    {
        public int AnchorSeasonId { get; }
        public long AnchorStartUtcMs { get; }
        public long DurationMs { get; }
        public long LockMs { get; }

        public SeasonCalendar(int anchorSeasonId, long anchorStartUtcMs, long durationMs, long lockMs)
        {
            if (anchorSeasonId < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(anchorSeasonId), anchorSeasonId, "Season IDs start at 1.");
            }

            if (durationMs <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durationMs), durationMs, "Duration must be positive.");
            }

            AnchorSeasonId = anchorSeasonId;
            AnchorStartUtcMs = anchorStartUtcMs;
            DurationMs = durationMs;
            LockMs = Math.Max(0, Math.Min(lockMs, durationMs));
        }

        /// <summary>Window of <paramref name="seasonId"/>; false before the anchor season.</summary>
        public bool TryGetWindow(int seasonId, out SeasonWindow window)
        {
            window = default;

            if (seasonId < AnchorSeasonId)
            {
                return false;
            }

            long start = AnchorStartUtcMs + (seasonId - AnchorSeasonId) * DurationMs;
            long end = start + DurationMs;
            window = new SeasonWindow(seasonId, start, end, end - LockMs);
            return true;
        }

        /// <summary>Season running at <paramref name="nowUtcMs"/>; false before the anchor start.</summary>
        public bool TryGetSeasonAt(long nowUtcMs, out SeasonWindow window)
        {
            window = default;

            if (nowUtcMs < AnchorStartUtcMs)
            {
                return false;
            }

            long index = (nowUtcMs - AnchorStartUtcMs) / DurationMs;
            return TryGetWindow(AnchorSeasonId + (int)index, out window);
        }

        public bool Equals(SeasonCalendar other)
        {
            return AnchorSeasonId == other.AnchorSeasonId
                && AnchorStartUtcMs == other.AnchorStartUtcMs
                && DurationMs == other.DurationMs
                && LockMs == other.LockMs;
        }

        public override bool Equals(object obj)
        {
            return obj is SeasonCalendar other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AnchorSeasonId, AnchorStartUtcMs, DurationMs, LockMs);
        }
    }

    /// <summary>
    /// What config/season stores: the active calendar plus, after a settings change, the
    /// calendar that takes over from the next season (so the running season keeps its length).
    /// </summary>
    public readonly struct SeasonConfig : IEquatable<SeasonConfig>
    {
        public SeasonCalendar Active { get; }
        public SeasonCalendar? Pending { get; }

        public SeasonConfig(SeasonCalendar active, SeasonCalendar? pending = null)
        {
            Active = active;
            Pending = pending;
        }

        public bool TryGetSeasonAt(long nowUtcMs, out SeasonWindow window)
        {
            if (Pending.HasValue && nowUtcMs >= Pending.Value.AnchorStartUtcMs)
            {
                return Pending.Value.TryGetSeasonAt(nowUtcMs, out window);
            }

            return Active.TryGetSeasonAt(nowUtcMs, out window);
        }

        public bool TryGetWindow(int seasonId, out SeasonWindow window)
        {
            if (Pending.HasValue && seasonId >= Pending.Value.AnchorSeasonId)
            {
                return Pending.Value.TryGetWindow(seasonId, out window);
            }

            return Active.TryGetWindow(seasonId, out window);
        }

        public bool Equals(SeasonConfig other)
        {
            return Active.Equals(other.Active) && Nullable.Equals(Pending, other.Pending);
        }

        public override bool Equals(object obj)
        {
            return obj is SeasonConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Active, Pending);
        }
    }

    /// <summary>
    /// Season settings arithmetic used by the Cloud Code season module and tests. Pure:
    /// callers pass the clock.
    /// </summary>
    public static class SeasonSchedule
    {
        /// <summary>
        /// Season boundaries are aligned to 00:00 Vietnam time (17:00 UTC), so 24 h seasons
        /// reset at midnight and shorter ones on a grid that includes midnight.
        /// </summary>
        public const long AlignmentOffsetMs = 17L * 3_600_000L;

        #region Public API

        /// <summary>
        /// Calendar for the first deployment: season 1 is the aligned period containing now.
        /// </summary>
        public static SeasonCalendar First(long nowUtcMs, long durationMs, long lockMs)
        {
            if (durationMs <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durationMs), durationMs, "Duration must be positive.");
            }

            long start = FloorDiv(nowUtcMs - AlignmentOffsetMs, durationMs) * durationMs + AlignmentOffsetMs;
            return new SeasonCalendar(1, start, durationMs, lockMs);
        }

        /// <summary>
        /// Next config/season for a tick at <paramref name="nowUtcMs"/> with the requested
        /// settings. Promotes a pending calendar whose first season has started, and schedules
        /// a settings change from the season after the running one. Idempotent.
        /// </summary>
        public static SeasonConfig Plan(SeasonConfig? current, long nowUtcMs, long durationMs, long lockMs)
        {
            if (!current.HasValue)
            {
                return new SeasonConfig(First(nowUtcMs, durationMs, lockMs));
            }

            SeasonConfig config = current.Value;

            if (config.Pending.HasValue && nowUtcMs >= config.Pending.Value.AnchorStartUtcMs)
            {
                config = new SeasonConfig(config.Pending.Value);
            }

            SeasonCalendar latest = config.Pending ?? config.Active;
            var requested = new SeasonCalendar(latest.AnchorSeasonId, latest.AnchorStartUtcMs, durationMs, lockMs);

            if (requested.DurationMs == latest.DurationMs && requested.LockMs == latest.LockMs)
            {
                return config;
            }

            if (!config.TryGetSeasonAt(nowUtcMs, out SeasonWindow running))
            {
                // Before the first season starts: replace the settings outright.
                return new SeasonConfig(
                    new SeasonCalendar(latest.AnchorSeasonId, latest.AnchorStartUtcMs, durationMs, lockMs)
                );
            }

            var next = new SeasonCalendar(running.SeasonId + 1, running.EndUtcMs, durationMs, lockMs);
            return new SeasonConfig(config.Active, next);
        }

        #endregion

        #region Helpers

        private static long FloorDiv(long value, long divisor)
        {
            long quotient = value / divisor;
            return (value % divisor != 0 && (value < 0) != (divisor < 0)) ? quotient - 1 : quotient;
        }

        #endregion
    }
}
