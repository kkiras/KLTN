using KLTN.Game.Domain.Ranking;
using NUnit.Framework;

namespace KLTN.Game.Domain.Tests
{
    public sealed class SeasonScheduleTests
    {
        private const long Minute = 60_000L;
        private const long Hour = 60 * Minute;
        private const long Day = 24 * Hour;
        private const long Lock = 30 * Minute;
        private const long T0 = 1_790_000_000_000L;

        private static SeasonConfig FirstConfig(long now = T0)
        {
            return SeasonSchedule.Plan(null, now, Day, Lock);
        }

        private static SeasonWindow SeasonAt(SeasonConfig config, long now)
        {
            Assert.IsTrue(config.TryGetSeasonAt(now, out SeasonWindow window));
            return window;
        }

        [Test]
        public void First_AlignsToVietnamMidnight_AndContainsNow()
        {
            SeasonCalendar calendar = FirstConfig().Active;
            SeasonWindow season = SeasonAt(FirstConfig(), T0);

            Assert.AreEqual(1, season.SeasonId);
            Assert.AreEqual(0, (calendar.AnchorStartUtcMs - SeasonSchedule.AlignmentOffsetMs) % Day);
            Assert.LessOrEqual(season.StartUtcMs, T0);
            Assert.Greater(season.EndUtcMs, T0);
            Assert.AreEqual(season.EndUtcMs - Lock, season.LockUtcMs);
        }

        [Test]
        public void SeasonAt_OpensNextSeasonExactlyAtEnd_WithoutAnyTick()
        {
            SeasonConfig config = FirstConfig();
            SeasonWindow first = SeasonAt(config, T0);

            Assert.AreEqual(1, SeasonAt(config, first.EndUtcMs - 1).SeasonId);
            SeasonWindow second = SeasonAt(config, first.EndUtcMs);
            Assert.AreEqual(2, second.SeasonId);
            Assert.AreEqual(first.EndUtcMs, second.StartUtcMs);
            Assert.AreEqual(5, SeasonAt(config, first.StartUtcMs + 4 * Day + Hour).SeasonId);
        }

        [Test]
        public void Plan_SameSettings_IsIdempotent()
        {
            SeasonConfig config = FirstConfig();

            Assert.AreEqual(config, SeasonSchedule.Plan(config, T0 + 3 * Day, Day, Lock));
        }

        [Test]
        public void Plan_NewDuration_StartsFromNextSeason()
        {
            SeasonConfig config = FirstConfig();
            SeasonWindow running = SeasonAt(config, T0);

            SeasonConfig changed = SeasonSchedule.Plan(config, T0, Hour, 10 * Minute);

            Assert.IsTrue(changed.Pending.HasValue);
            Assert.AreEqual(running, SeasonAt(changed, T0));
            SeasonWindow next = SeasonAt(changed, running.EndUtcMs);
            Assert.AreEqual(2, next.SeasonId);
            Assert.AreEqual(running.EndUtcMs + Hour, next.EndUtcMs);
            Assert.AreEqual(next.EndUtcMs - 10 * Minute, next.LockUtcMs);
            Assert.AreEqual(4, SeasonAt(changed, running.EndUtcMs + 2 * Hour).SeasonId);
        }

        [Test]
        public void Plan_PromotesPendingOnceItStarts()
        {
            SeasonConfig config = FirstConfig();
            SeasonWindow running = SeasonAt(config, T0);
            SeasonConfig changed = SeasonSchedule.Plan(config, T0, Hour, 10 * Minute);

            SeasonConfig promoted = SeasonSchedule.Plan(changed, running.EndUtcMs + 5 * Hour, Hour, 10 * Minute);

            Assert.IsFalse(promoted.Pending.HasValue);
            Assert.AreEqual(2, promoted.Active.AnchorSeasonId);
            Assert.AreEqual(7, SeasonAt(promoted, running.EndUtcMs + 5 * Hour).SeasonId);
        }

        [Test]
        public void Plan_ChangeBackBeforePendingStarts_KeepsCalendarConsistent()
        {
            SeasonConfig config = FirstConfig();
            SeasonWindow running = SeasonAt(config, T0);
            SeasonConfig changed = SeasonSchedule.Plan(config, T0, Hour, 10 * Minute);

            SeasonConfig reverted = SeasonSchedule.Plan(changed, T0 + Minute, Day, Lock);

            Assert.AreEqual(running, SeasonAt(reverted, T0 + Minute));
            Assert.AreEqual(running.EndUtcMs + Day, SeasonAt(reverted, running.EndUtcMs).EndUtcMs);
        }

        [Test]
        public void TryGetWindow_PastSeason_UsesCalendarItBelongsTo()
        {
            SeasonConfig config = FirstConfig();
            SeasonWindow running = SeasonAt(config, T0);
            SeasonConfig changed = SeasonSchedule.Plan(config, T0, Hour, 10 * Minute);

            Assert.IsTrue(changed.TryGetWindow(1, out SeasonWindow first));
            Assert.AreEqual(running, first);
            Assert.IsTrue(changed.TryGetWindow(3, out SeasonWindow third));
            Assert.AreEqual(running.EndUtcMs + Hour, third.StartUtcMs);
            Assert.IsFalse(config.TryGetWindow(0, out _));
        }

        [Test]
        public void LockLongerThanSeason_ClampsToStart()
        {
            var calendar = new SeasonCalendar(1, T0, 10 * Minute, Lock);

            Assert.IsTrue(calendar.TryGetWindow(1, out SeasonWindow season));
            Assert.AreEqual(T0, season.LockUtcMs);
        }

        [Test]
        public void Eligibility_MatchStartedBeforeLockAndFinishedInSeason_IsScored()
        {
            SeasonWindow season = SeasonAt(FirstConfig(), T0);
            long start = season.LockUtcMs - Minute;

            Assert.AreEqual(
                RankedEligibility.Eligible,
                RankedEligibilityPolicy.Check(season, start, season.EndUtcMs - 1)
            );
        }

        [Test]
        public void Eligibility_MatchStartedAfterLock_IsNotScored()
        {
            SeasonWindow season = SeasonAt(FirstConfig(), T0);

            Assert.AreEqual(
                RankedEligibility.StartedAfterLock,
                RankedEligibilityPolicy.Check(season, season.LockUtcMs, season.LockUtcMs + Minute)
            );
        }

        [Test]
        public void Eligibility_MatchCrossingReset_IsNotScored()
        {
            SeasonWindow season = SeasonAt(FirstConfig(), T0);

            Assert.AreEqual(
                RankedEligibility.FinishedAfterSeasonEnd,
                RankedEligibilityPolicy.Check(season, season.LockUtcMs - Minute, season.EndUtcMs)
            );
        }

        [Test]
        public void Eligibility_MatchFromPreviousSeason_IsNotScored()
        {
            SeasonWindow season = SeasonAt(FirstConfig(), T0);

            Assert.AreEqual(
                RankedEligibility.StartedBeforeSeason,
                RankedEligibilityPolicy.Check(season, season.StartUtcMs - Minute, season.StartUtcMs + Minute)
            );
            Assert.AreEqual(
                RankedEligibility.NoActiveSeason,
                RankedEligibilityPolicy.Check(null, T0, T0 + Minute)
            );
        }
    }
}
