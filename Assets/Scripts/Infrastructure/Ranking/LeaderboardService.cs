using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KLTN.Game.Domain.Ranking;
using KLTN.Infrastructure.Economy;
using KLTN.Infrastructure.Firestore;
using UnityEngine;

namespace KLTN.Infrastructure.Ranking
{
    public enum RankedReportStatus
    {
        Applied = 0,
        AlreadyApplied,
        NotEligible,
        Failed,
    }

    /// <summary>What happened to one finished match's season points, for the result screen.</summary>
    public sealed class RankedMatchReport
    {
        public RankedReportStatus Status { get; }
        public string Message { get; }
        public RankedResult Result { get; }
        public SeasonStanding Before { get; }
        public SeasonStanding After { get; }

        public RankedMatchReport(
            RankedReportStatus status,
            string message,
            RankedResult result,
            SeasonStanding before = null,
            SeasonStanding after = null
        )
        {
            Status = status;
            Message = message;
            Result = result;
            Before = before;
            After = after;
        }
    }

    /// <summary>
    /// Client-side access to seasons and the leaderboard, plus recording ranked results.
    /// Created automatically; survives scene changes (same lifecycle as PlayerProfileService).
    /// </summary>
    public sealed class LeaderboardService : MonoBehaviour
    {
        public static LeaderboardService Instance { get; private set; }

        public ILeaderboardGateway Gateway { get; private set; }

        /// <summary>Last known running season (refreshed by <see cref="RefreshSeasonAsync"/>).</summary>
        public SeasonWindow? CurrentSeason { get; private set; }

        /// <summary>Last known own row in the current season; null = not played yet.</summary>
        public SeasonStanding MyStanding { get; private set; }

        public event Action<SeasonStanding> MyStandingChanged;

        private readonly HashSet<string> recordedMatches = new HashSet<string>(StringComparer.Ordinal);

        #region Bootstrap

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_SERVER
            if (true)
#else
            if (Application.isBatchMode)
#endif
            {
                // Dedicated server has no signed-in player.
                return;
            }

            var host = new GameObject(nameof(LeaderboardService));
            DontDestroyOnLoad(host);
            host.AddComponent<LeaderboardService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Gateway = new FirestoreLeaderboardGateway(
                new FirestoreRestClient(),
                () => AuthManager.Instance != null ? AuthManager.Instance.CurrentUserId : null,
                () => AuthManager.Instance != null ? AuthManager.Instance.GetValidIdTokenAsync() : Task.FromResult<string>(null)
            );

            AuthManager.OnLoginSuccess += HandleLogin;
            AuthManager.OnLogoutSuccess += HandleLogout;
        }

        private void OnDestroy()
        {
            AuthManager.OnLoginSuccess -= HandleLogin;
            AuthManager.OnLogoutSuccess -= HandleLogout;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Lists the player in the running season as soon as they log in (0 points if new). The season
        /// module also adds every account each hour; this covers players who just signed up.
        /// </summary>
        private async void HandleLogin()
        {
            try
            {
                RankingResult<SeasonWindow?> season = await RefreshSeasonAsync();

                if (season.Success && season.Value.HasValue)
                {
                    await EnsureMyStandingAsync(season.Value.Value.SeasonId);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Leaderboard] Could not add the player to the leaderboard: {exception.Message}");
            }
        }

        private void HandleLogout()
        {
            CurrentSeason = null;
            MyStanding = null;
            recordedMatches.Clear();
            MyStandingChanged?.Invoke(null);
        }

        #endregion

        #region Queries

        public string CurrentUserId => AuthManager.Instance != null ? AuthManager.Instance.CurrentUserId : null;

        public async Task<RankingResult<SeasonWindow?>> RefreshSeasonAsync()
        {
            RankingResult<SeasonWindow?> result = await Gateway.GetCurrentSeasonAsync();

            if (result.Success)
            {
                CurrentSeason = result.Value;
            }

            return result;
        }

        /// <summary>
        /// Own tier in the running season, used for the matchmaker ticket. Falls back to the
        /// cached value (or Bronze) when offline.
        /// </summary>
        public async Task<RankTier> GetMyCurrentTierAsync()
        {
            RankingResult<SeasonWindow?> season = await RefreshSeasonAsync();

            if (!season.Success || !season.Value.HasValue)
            {
                return MyStanding?.Tier ?? RankTier.Bronze;
            }

            RankingResult<SeasonStanding> standing =
                await Gateway.GetStandingAsync(season.Value.Value.SeasonId, CurrentUserId);

            if (standing.Success)
            {
                SetMyStanding(standing.Value);
            }

            return MyStanding?.Tier ?? RankTier.Bronze;
        }

        /// <summary>
        /// Own row in the running season, created empty (0 points, Bronze, 0 %) when missing,
        /// so every player who opens the leaderboard is listed.
        /// </summary>
        public async Task<RankingResult<SeasonStanding>> EnsureMyStandingAsync(int seasonId)
        {
            string username = await ResolveUsernameAsync();
            RankingResult<SeasonStanding> standing = await Gateway.EnsureStandingAsync(seasonId, username);

            if (standing.Success && CurrentSeason.HasValue && CurrentSeason.Value.SeasonId == seasonId)
            {
                SetMyStanding(standing.Value);
            }

            return standing;
        }

        #endregion

        #region Commands

        /// <summary>
        /// Applies one finished ranked match to the running season. Safe to call repeatedly:
        /// the results/{matchId} document makes the write idempotent.
        /// </summary>
        public async Task<RankedMatchReport> RecordRankedMatchAsync(
            string matchId,
            RankedResult result,
            long matchStartedUtcMs
        )
        {
            if (string.IsNullOrEmpty(matchId) || !recordedMatches.Add(matchId))
            {
                return new RankedMatchReport(RankedReportStatus.AlreadyApplied, string.Empty, result);
            }

            RankingResult<SeasonWindow?> season = await RefreshSeasonAsync();

            if (!season.Success)
            {
                recordedMatches.Remove(matchId);
                return new RankedMatchReport(RankedReportStatus.Failed, season.Message, result);
            }

            RankedEligibility eligibility =
                RankedEligibilityPolicy.Check(season.Value, matchStartedUtcMs, ServerClock.UtcNowMs);

            if (eligibility != RankedEligibility.Eligible)
            {
                return new RankedMatchReport(RankedReportStatus.NotEligible, EligibilityMessage(eligibility), result);
            }

            string username = await ResolveUsernameAsync();
            int seasonId = season.Value.Value.SeasonId;
            RankingResult<RankedRecord> record =
                await Gateway.RecordResultAsync(seasonId, username, matchId, result, matchStartedUtcMs);

            if (!record.Success)
            {
                recordedMatches.Remove(matchId);
                return new RankedMatchReport(RankedReportStatus.Failed, record.Message, result);
            }

            SetMyStanding(record.Value.After);
            Debug.Log(
                $"[Leaderboard] Season {seasonId}: {RankPointsPolicy.WireName(result.Kind)} {result.Delta:+#;-#;0} "
                    + $"-> {record.Value.After.Points} điểm ({record.Value.After.Tier})."
            );

            return new RankedMatchReport(
                record.Value.AlreadyApplied ? RankedReportStatus.AlreadyApplied : RankedReportStatus.Applied,
                string.Empty,
                result,
                record.Value.Before,
                record.Value.After
            );
        }

        #endregion

        #region Helpers

        private static async Task<string> ResolveUsernameAsync()
        {
            PlayerProfileService profiles = PlayerProfileService.Instance;

            if (profiles == null)
            {
                return null;
            }

            EconomyResult loaded = await profiles.EnsureLoadedAsync();
            return loaded.Success ? loaded.Profile?.Username : null;
        }

        private void SetMyStanding(SeasonStanding standing)
        {
            MyStanding = standing;
            MyStandingChanged?.Invoke(standing);
        }

        public static string EligibilityMessage(RankedEligibility eligibility)
        {
            switch (eligibility)
            {
                case RankedEligibility.NoActiveSeason:
                    return "Chưa có mùa xếp hạng đang diễn ra.";
                case RankedEligibility.StartedBeforeSeason:
                    return "Trận bắt đầu ở mùa trước: không tính điểm.";
                case RankedEligibility.StartedAfterLock:
                    return "Đã khóa điểm cuối mùa: trận không tính điểm.";
                case RankedEligibility.FinishedAfterSeasonEnd:
                    return "Mùa đã reset trước khi trận kết thúc: không tính điểm.";
                default:
                    return string.Empty;
            }
        }

        #endregion
    }
}
