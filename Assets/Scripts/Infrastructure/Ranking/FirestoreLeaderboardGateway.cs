using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using KLTN.Game.Domain.Ranking;
using KLTN.Infrastructure.Firestore;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KLTN.Infrastructure.Ranking
{
    /// <summary>
    /// Firestore layout:
    ///   config/season                                  anchorSeasonId, anchorStartMs, durationMs, lockMs,
    ///                                                  pending* (Cloud Code only). The running season is
    ///                                                  computed from it and the server clock.
    ///   seasons/{seasonId}                             seasonId, startAt, endAt, lockAt (history, Cloud Code only)
    ///   seasons/{seasonId}/entries/{uid}               username, points, wins, losses, draws, matches,
    ///                                                  lastMatchId, updatedAt
    ///   seasons/{seasonId}/entries/{uid}/results/{matchId}   result, delta, matchStartedAt, createdAt
    /// A result is one :commit batch (entry + results doc) so Rules can check it with getAfter().
    /// </summary>
    public sealed class FirestoreLeaderboardGateway : ILeaderboardGateway
    {
        #region Constants

        private const string ConfigPath = "config/season";
        private const string SeasonsCollection = "seasons";
        private const string EntriesCollection = "entries";

        /// <summary>Rules require 60 s between two results; wait a little longer to absorb clock skew.</summary>
        private const long RateLimitMs = (RankPointsPolicy.MinSecondsBetweenResults + 2) * 1000L;

        #endregion

        private readonly FirestoreRestClient client;
        private readonly Func<string> userIdProvider;
        private readonly Func<Task<string>> tokenProvider;

        public FirestoreLeaderboardGateway(
            FirestoreRestClient client,
            Func<string> userIdProvider,
            Func<Task<string>> tokenProvider
        )
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.userIdProvider = userIdProvider ?? throw new ArgumentNullException(nameof(userIdProvider));
            this.tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        #region Seasons

        public async Task<RankingResult<SeasonWindow?>> GetCurrentSeasonAsync()
        {
            string token = await tokenProvider();

            if (string.IsNullOrEmpty(token))
            {
                return RankingResult<SeasonWindow?>.Fail(RankingErrorCode.NotSignedIn, "Bạn chưa đăng nhập.");
            }

            FirestoreResponse response = await client.GetDocumentAsync(ConfigPath, token);

            if (response.IsNotFound)
            {
                return RankingResult<SeasonWindow?>.Ok(null);
            }

            if (!response.Success)
            {
                return Fail<SeasonWindow?>(response);
            }

            SeasonConfig? config = ParseConfig(response.Body);

            if (config.HasValue && config.Value.TryGetSeasonAt(ServerClock.UtcNowMs, out SeasonWindow running))
            {
                return RankingResult<SeasonWindow?>.Ok(running);
            }

            return RankingResult<SeasonWindow?>.Ok(null);
        }

        public async Task<RankingResult<IReadOnlyList<SeasonWindow>>> ListSeasonsAsync()
        {
            string token = await tokenProvider();

            if (string.IsNullOrEmpty(token))
            {
                return RankingResult<IReadOnlyList<SeasonWindow>>.Fail(RankingErrorCode.NotSignedIn, "Bạn chưa đăng nhập.");
            }

            (FirestoreResponse response, List<JObject> documents) =
                await client.ListDocumentsAsync(SeasonsCollection, token);

            if (!response.Success)
            {
                return Fail<IReadOnlyList<SeasonWindow>>(response);
            }

            var seasons = new List<SeasonWindow>(documents.Count);

            foreach (JObject document in documents)
            {
                SeasonWindow? window = ParseWindow(document);

                if (window.HasValue)
                {
                    seasons.Add(window.Value);
                }
            }

            seasons.Sort((left, right) => right.SeasonId.CompareTo(left.SeasonId));
            return RankingResult<IReadOnlyList<SeasonWindow>>.Ok(seasons);
        }

        #endregion

        #region Leaderboard Reads

        public async Task<RankingResult<LeaderboardPage>> GetPageAsync(int seasonId, LeaderboardCursor after, int pageSize)
        {
            string token = await tokenProvider();

            if (string.IsNullOrEmpty(token))
            {
                return RankingResult<LeaderboardPage>.Fail(RankingErrorCode.NotSignedIn, "Bạn chưa đăng nhập.");
            }

            pageSize = Math.Max(1, pageSize);

            var query = new JObject
            {
                ["from"] = new JArray { new JObject { ["collectionId"] = EntriesCollection } },
                ["orderBy"] = new JArray
                {
                    // Index: points DESC, winRate DESC, username ASC (__name__ follows the last field).
                    Order("points", "DESCENDING"),
                    Order("winRate", "DESCENDING"),
                    Order("username", "ASCENDING"),
                    Order("__name__", "ASCENDING"),
                },
                // One extra row tells whether another page exists.
                ["limit"] = pageSize + 1,
            };

            if (after != null)
            {
                query["startAt"] = new JObject
                {
                    ["values"] = new JArray
                    {
                        FirestoreValueMapper.ToValue((long)after.Points),
                        FirestoreValueMapper.ToValue((long)after.WinRate),
                        FirestoreValueMapper.ToValue(after.Username),
                        new JObject { ["referenceValue"] = after.DocumentName },
                    },
                    ["before"] = false,
                };
            }

            (FirestoreResponse response, List<JObject> documents) =
                await client.RunQueryAsync(SeasonPath(seasonId), query, token);

            if (!response.Success)
            {
                return Fail<LeaderboardPage>(response);
            }

            bool hasMore = documents.Count > pageSize;
            int count = Math.Min(pageSize, documents.Count);
            var rows = new List<SeasonStanding>(count);
            LeaderboardCursor next = null;

            for (int index = 0; index < count; index++)
            {
                SeasonStanding standing = ParseStanding(documents[index]);
                rows.Add(standing);
                next = new LeaderboardCursor(
                    standing.Points,
                    standing.WinRateBasisPoints,
                    standing.Username,
                    documents[index]["name"]?.ToString()
                );
            }

            return RankingResult<LeaderboardPage>.Ok(new LeaderboardPage(rows, hasMore, next));
        }

        public async Task<RankingResult<SeasonStanding>> GetStandingAsync(int seasonId, string userId)
        {
            string token = await tokenProvider();

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId))
            {
                return RankingResult<SeasonStanding>.Fail(RankingErrorCode.NotSignedIn, "Bạn chưa đăng nhập.");
            }

            FirestoreResponse response = await client.GetDocumentAsync(EntryPath(seasonId, userId), token);

            if (response.IsNotFound)
            {
                return RankingResult<SeasonStanding>.Ok(null);
            }

            return response.Success
                ? RankingResult<SeasonStanding>.Ok(ParseStanding(response.Body))
                : Fail<SeasonStanding>(response);
        }

        public async Task<RankingResult<int>> GetPositionAsync(int seasonId, SeasonStanding standing)
        {
            string token = await tokenProvider();

            if (string.IsNullOrEmpty(token) || standing == null)
            {
                return RankingResult<int>.Fail(RankingErrorCode.NotSignedIn, "Bạn chưa đăng nhập.");
            }

            // Ahead = more points; or same points and a higher win rate; or both equal and a smaller
            // username (unique), matching the page order exactly.
            int winRate = standing.WinRateBasisPoints;
            JObject[] aheadQueries =
            {
                CountQuery(Filter("points", "GREATER_THAN", standing.Points)),
                CountQuery(And(Filter("points", "EQUAL", standing.Points), Filter("winRate", "GREATER_THAN", winRate))),
                CountQuery(And(
                    Filter("points", "EQUAL", standing.Points),
                    Filter("winRate", "EQUAL", winRate),
                    Filter("username", "LESS_THAN", standing.Username)
                )),
            };

            long ahead = 0;

            foreach (JObject aheadQuery in aheadQueries)
            {
                (FirestoreResponse response, long count) = await client.RunCountAsync(SeasonPath(seasonId), aheadQuery, token);

                if (!response.Success)
                {
                    return Fail<int>(response);
                }

                ahead += count;
            }

            return RankingResult<int>.Ok((int)ahead + 1);
        }

        #endregion

        #region Leaderboard Writes

        public async Task<RankingResult<SeasonStanding>> EnsureStandingAsync(int seasonId, string username)
        {
            string uid = userIdProvider();
            RankingResult<SeasonStanding> existing = await GetStandingAsync(seasonId, uid);

            if (!existing.Success || existing.Value != null)
            {
                return existing;
            }

            if (!UsernamePolicy.IsValid(username))
            {
                return RankingResult<SeasonStanding>.Fail(RankingErrorCode.Rejected, "Tài khoản chưa có username.");
            }

            string token = await tokenProvider();
            var empty = new SeasonStanding(uid, username, 0, 0, 0, 0, 0);
            FirestoreResponse response = await client.CommitAsync(
                new[] { client.CreateWrite(EntryPath(seasonId, uid), StandingFields(empty, null), "updatedAt") },
                token
            );

            if (response.Success)
            {
                return RankingResult<SeasonStanding>.Ok(empty);
            }

            // Created meanwhile by the season module or another device: read it back.
            return response.ErrorStatus == "ALREADY_EXISTS"
                ? await GetStandingAsync(seasonId, uid)
                : Fail<SeasonStanding>(response);
        }

        public async Task<RankingResult<RankedRecord>> RecordResultAsync(
            int seasonId,
            string username,
            string matchId,
            RankedResult result,
            long matchStartedUtcMs
        )
        {
            if (string.IsNullOrWhiteSpace(matchId) || matchId.Length > 64 || matchId.Contains("/"))
            {
                return RankingResult<RankedRecord>.Fail(RankingErrorCode.Rejected, "Mã trận không hợp lệ.");
            }

            if (!UsernamePolicy.IsValid(username))
            {
                return RankingResult<RankedRecord>.Fail(RankingErrorCode.Rejected, "Tài khoản chưa có username.");
            }

            string uid = userIdProvider();
            string token = await tokenProvider();

            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(token))
            {
                return RankingResult<RankedRecord>.Fail(RankingErrorCode.NotSignedIn, "Bạn chưa đăng nhập.");
            }

            string entryPath = EntryPath(seasonId, uid);
            string resultPath = $"{entryPath}/results/{matchId}";

            FirestoreResponse entry = await client.GetDocumentAsync(entryPath, token);

            if (!entry.Success && !entry.IsNotFound)
            {
                return Fail<RankedRecord>(entry);
            }

            bool exists = entry.Success;
            SeasonStanding before = exists
                ? ParseStanding(entry.Body)
                : new SeasonStanding(uid, username, 0, 0, 0, 0, 0);

            FirestoreResponse existing = await client.GetDocumentAsync(resultPath, token);

            if (existing.Success)
            {
                return RankingResult<RankedRecord>.Ok(new RankedRecord(before, before, alreadyApplied: true));
            }

            if (exists)
            {
                long sinceLast = ServerClock.UtcNowMs - FirestoreValueMapper.GetTimestampMs(entry.Body, "updatedAt");

                if (sinceLast >= 0 && sinceLast < RateLimitMs)
                {
                    // Rules allow one result per 60 s; a very short match waits instead of being lost.
                    await Task.Delay(TimeSpan.FromMilliseconds(RateLimitMs - sinceLast));
                    token = await tokenProvider();
                }
            }

            SeasonStanding after = Apply(before, uid, username, result);

            Dictionary<string, object> fields = StandingFields(after, matchId);

            var writes = new List<JObject>
            {
                exists
                    ? client.UpdateWrite(entryPath, fields, null, "updatedAt")
                    : client.CreateWrite(entryPath, fields, "updatedAt"),
                client.CreateWrite(
                    resultPath,
                    new Dictionary<string, object>
                    {
                        ["result"] = RankPointsPolicy.WireName(result.Kind),
                        ["delta"] = result.Delta,
                        ["matchStartedAt"] = DateTimeOffset.FromUnixTimeMilliseconds(matchStartedUtcMs).UtcDateTime,
                    },
                    "createdAt"
                ),
            };

            FirestoreResponse response = await client.CommitAsync(writes, token);

            if (!response.Success)
            {
                Debug.LogWarning(
                    $"[Leaderboard] Result {matchId} for season {seasonId} rejected: "
                        + $"{response.ErrorStatus} {response.ErrorMessage}"
                );
                return Fail<RankedRecord>(response);
            }

            return RankingResult<RankedRecord>.Ok(new RankedRecord(before, after, alreadyApplied: false));
        }

        #endregion

        #region Parsing

        private static SeasonStanding Apply(SeasonStanding before, string uid, string username, RankedResult result)
        {
            return new SeasonStanding(
                uid,
                username,
                RankPointsPolicy.ApplyDelta(before.Points, result.Delta),
                before.Wins + (result.Kind == RankedResultKind.Win ? 1 : 0),
                before.Losses + (result.Kind == RankedResultKind.Loss || result.Kind == RankedResultKind.Surrender ? 1 : 0),
                before.Draws + (result.Kind == RankedResultKind.Draw ? 1 : 0),
                before.Matches + 1
            );
        }

        private static Dictionary<string, object> StandingFields(SeasonStanding standing, string lastMatchId)
        {
            var fields = new Dictionary<string, object>
            {
                ["username"] = standing.Username,
                ["points"] = standing.Points,
                ["wins"] = standing.Wins,
                ["losses"] = standing.Losses,
                ["draws"] = standing.Draws,
                ["matches"] = standing.Matches,
                ["winRate"] = standing.WinRateBasisPoints,
            };

            if (lastMatchId != null)
            {
                fields["lastMatchId"] = lastMatchId;
            }

            return fields;
        }

        private static SeasonStanding ParseStanding(JObject document)
        {
            return new SeasonStanding(
                FirestoreValueMapper.DocumentId(document),
                FirestoreValueMapper.GetString(document, "username"),
                (int)FirestoreValueMapper.GetInteger(document, "points"),
                (int)FirestoreValueMapper.GetInteger(document, "wins"),
                (int)FirestoreValueMapper.GetInteger(document, "losses"),
                (int)FirestoreValueMapper.GetInteger(document, "draws"),
                (int)FirestoreValueMapper.GetInteger(document, "matches")
            );
        }

        private static SeasonConfig? ParseConfig(JObject document)
        {
            int anchorId = (int)FirestoreValueMapper.GetInteger(document, "anchorSeasonId");
            long duration = FirestoreValueMapper.GetInteger(document, "durationMs");

            if (anchorId < 1 || duration <= 0)
            {
                Debug.LogWarning("[Leaderboard] config/season is missing its calendar; redeploy SeasonModule.");
                return null;
            }

            var active = new SeasonCalendar(
                anchorId,
                FirestoreValueMapper.GetInteger(document, "anchorStartMs"),
                duration,
                FirestoreValueMapper.GetInteger(document, "lockMs")
            );

            int pendingId = (int)FirestoreValueMapper.GetInteger(document, "pendingSeasonId");
            long pendingDuration = FirestoreValueMapper.GetInteger(document, "pendingDurationMs");

            if (pendingId < 1 || pendingDuration <= 0)
            {
                return new SeasonConfig(active);
            }

            var pending = new SeasonCalendar(
                pendingId,
                FirestoreValueMapper.GetInteger(document, "pendingStartMs"),
                pendingDuration,
                FirestoreValueMapper.GetInteger(document, "pendingLockMs")
            );
            return new SeasonConfig(active, pending);
        }

        private static SeasonWindow? ParseWindow(JObject document)
        {
            int seasonId = (int)FirestoreValueMapper.GetInteger(document, "seasonId");
            long start = FirestoreValueMapper.GetTimestampMs(document, "startAt");
            long end = FirestoreValueMapper.GetTimestampMs(document, "endAt");
            long lockAt = FirestoreValueMapper.GetTimestampMs(document, "lockAt", end);

            if (seasonId < 1 || end <= start)
            {
                Debug.LogWarning($"[Leaderboard] Ignoring malformed season document {document?["name"]}.");
                return null;
            }

            return new SeasonWindow(seasonId, start, end, lockAt);
        }

        #endregion

        #region Helpers

        private static string SeasonPath(int seasonId)
        {
            return $"{SeasonsCollection}/{seasonId.ToString(CultureInfo.InvariantCulture)}";
        }

        private static string EntryPath(int seasonId, string uid)
        {
            return $"{SeasonPath(seasonId)}/{EntriesCollection}/{uid}";
        }

        private static JObject Order(string field, string direction)
        {
            return new JObject
            {
                ["field"] = new JObject { ["fieldPath"] = field },
                ["direction"] = direction,
            };
        }

        private static JObject Filter(string field, string op, int value)
        {
            return Filter(field, op, FirestoreValueMapper.ToValue((long)value));
        }

        private static JObject Filter(string field, string op, string value)
        {
            return Filter(field, op, FirestoreValueMapper.ToValue(value));
        }

        private static JObject Filter(string field, string op, JObject value)
        {
            return new JObject
            {
                ["fieldFilter"] = new JObject
                {
                    ["field"] = new JObject { ["fieldPath"] = field },
                    ["op"] = op,
                    ["value"] = value,
                },
            };
        }

        private static JObject And(params JObject[] filters)
        {
            return new JObject
            {
                ["compositeFilter"] = new JObject { ["op"] = "AND", ["filters"] = new JArray(filters) },
            };
        }

        private static JObject CountQuery(JObject where)
        {
            return new JObject
            {
                ["from"] = new JArray { new JObject { ["collectionId"] = EntriesCollection } },
                ["where"] = where,
            };
        }

        private static RankingResult<T> Fail<T>(FirestoreResponse response)
        {
            switch (response.ErrorStatus)
            {
                case "PERMISSION_DENIED":
                    return RankingResult<T>.Fail(RankingErrorCode.PermissionDenied, "Máy chủ từ chối ghi điểm.");
                case "UNAUTHENTICATED":
                    return RankingResult<T>.Fail(RankingErrorCode.NotSignedIn, "Phiên đăng nhập đã hết hạn.");
                case "FAILED_PRECONDITION":
                    // Usually a missing composite index; the message contains the console link.
                    return RankingResult<T>.Fail(RankingErrorCode.Rejected, response.ErrorMessage);
                case "ALREADY_EXISTS":
                    return RankingResult<T>.Fail(RankingErrorCode.Rejected, "Kết quả trận đã được ghi.");
                case "NETWORK_ERROR":
                case "UNAVAILABLE":
                case "DEADLINE_EXCEEDED":
                    return RankingResult<T>.Fail(RankingErrorCode.Network, "Không thể kết nối máy chủ dữ liệu.");
                default:
                    return RankingResult<T>.Fail(RankingErrorCode.Unknown, response.ErrorMessage ?? "Lỗi không xác định.");
            }
        }

        #endregion
    }
}
