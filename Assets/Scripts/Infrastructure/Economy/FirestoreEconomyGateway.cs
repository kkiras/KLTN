using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KLTN.Game.Domain.Economy;
using KLTN.Infrastructure.Firestore;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KLTN.Infrastructure.Economy
{
    /// <summary>
    /// Firestore layout:
    ///   users/{uid}                         coins, silver, schemaVersion, createdAt, updatedAt,
    ///                                       lastPurchase, lastRewardMatchId
    ///   users/{uid}/ownedCards/{cardId}     acquiredAt, source
    ///   users/{uid}/matchRewards/{matchId}  result, coins, silver, createdAt
    /// Every write is one :commit batch so Security Rules can validate it with getAfter().
    /// </summary>
    public sealed class FirestoreEconomyGateway : IEconomyGateway
    {
        private const int SchemaVersion = 1;

        private readonly FirestoreRestClient client;
        private readonly Func<string> userIdProvider;
        private readonly Func<Task<string>> tokenProvider;

        public FirestoreEconomyGateway(
            FirestoreRestClient client,
            Func<string> userIdProvider,
            Func<Task<string>> tokenProvider
        )
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.userIdProvider = userIdProvider ?? throw new ArgumentNullException(nameof(userIdProvider));
            this.tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        #region IEconomyGateway

        public async Task<EconomyResult> LoadOrCreateProfileAsync(IReadOnlyList<string> starterCardIds)
        {
            (string uid, string token, EconomyResult error) = await SessionAsync();

            if (error != null)
            {
                return error;
            }

            FirestoreResponse user = await client.GetDocumentAsync(UserPath(uid), token);

            if (user.IsNotFound)
            {
                EconomyResult created = await CreateProfileAsync(uid, token, starterCardIds);

                // ALREADY_EXISTS / FAILED_PRECONDITION: another device created it first. Just reload.
                if (!created.Success && created.Error != EconomyErrorCode.Rejected)
                {
                    return created;
                }
            }
            else if (!user.Success)
            {
                return Map(user);
            }

            return await ReadProfileAsync(uid, token);
        }

        public async Task<EconomyResult> PurchaseAsync(ShopEntry entry)
        {
            if (entry == null || entry.IsStarter)
            {
                return EconomyResult.Fail(EconomyErrorCode.Rejected, "Thẻ này không bán.");
            }

            (string uid, string token, EconomyResult error) = await SessionAsync();

            if (error != null)
            {
                return error;
            }

            string source = entry.Currency == Currency.Coins ? "coins" : "silver";
            var increments = new Dictionary<string, long> { [source] = -entry.Price };

            var writes = new List<JObject>
            {
                client.UpdateWrite(
                    UserPath(uid),
                    new Dictionary<string, object> { ["lastPurchase"] = entry.CardId },
                    increments,
                    "updatedAt"
                ),
                client.CreateWrite(
                    $"{UserPath(uid)}/ownedCards/{entry.CardId}",
                    new Dictionary<string, object> { ["source"] = source },
                    "acquiredAt"
                ),
            };

            FirestoreResponse response = await client.CommitAsync(writes, token);

            if (!response.Success)
            {
                EconomyResult failure = Map(response);
                Debug.LogWarning($"[Economy] Purchase {entry.CardId} failed: {failure.Error} {failure.Message}");
                return failure;
            }

            return await ReadProfileAsync(uid, token);
        }

        public async Task<EconomyResult> ClaimMatchRewardAsync(string matchId, MatchReward reward)
        {
            if (string.IsNullOrWhiteSpace(matchId) || matchId.Length > 64 || matchId.Contains("/"))
            {
                return EconomyResult.Fail(EconomyErrorCode.Rejected, "Mã trận không hợp lệ.");
            }

            (string uid, string token, EconomyResult error) = await SessionAsync();

            if (error != null)
            {
                return error;
            }

            string rewardPath = $"{UserPath(uid)}/matchRewards/{matchId}";
            FirestoreResponse existing = await client.GetDocumentAsync(rewardPath, token);

            if (existing.Success)
            {
                EconomyResult current = await ReadProfileAsync(uid, token);
                return current.Success ? EconomyResult.Ok(current.Profile, alreadyApplied: true) : current;
            }

            var writes = new List<JObject>
            {
                client.UpdateWrite(
                    UserPath(uid),
                    new Dictionary<string, object> { ["lastRewardMatchId"] = matchId },
                    new Dictionary<string, long> { ["coins"] = reward.Coins, ["silver"] = reward.Silver },
                    "updatedAt"
                ),
                client.CreateWrite(
                    rewardPath,
                    new Dictionary<string, object>
                    {
                        ["result"] = ResultName(reward.Kind),
                        ["coins"] = reward.Coins,
                        ["silver"] = reward.Silver,
                    },
                    "createdAt"
                ),
            };

            FirestoreResponse response = await client.CommitAsync(writes, token);

            if (!response.Success)
            {
                EconomyResult failure = Map(response);
                Debug.LogWarning($"[Economy] Reward claim {matchId} failed: {failure.Error} {failure.Message}");
                return failure;
            }

            return await ReadProfileAsync(uid, token);
        }

        #endregion

        #region Helpers

        private async Task<EconomyResult> CreateProfileAsync(string uid, string token, IReadOnlyList<string> starterCardIds)
        {
            var writes = new List<JObject>
            {
                client.CreateWrite(
                    UserPath(uid),
                    new Dictionary<string, object>
                    {
                        ["coins"] = 0,
                        ["silver"] = 0,
                        ["schemaVersion"] = SchemaVersion,
                    },
                    "createdAt",
                    "updatedAt"
                ),
            };

            foreach (string cardId in starterCardIds)
            {
                writes.Add(
                    client.CreateWrite(
                        $"{UserPath(uid)}/ownedCards/{cardId}",
                        new Dictionary<string, object> { ["source"] = "starter" },
                        "acquiredAt"
                    )
                );
            }

            FirestoreResponse response = await client.CommitAsync(writes, token);

            if (response.Success)
            {
                Debug.Log($"[Economy] Created profile for {uid} with {starterCardIds.Count} starter cards.");
                return EconomyResult.Ok(null);
            }

            return Map(response);
        }

        private async Task<EconomyResult> ReadProfileAsync(string uid, string token)
        {
            FirestoreResponse user = await client.GetDocumentAsync(UserPath(uid), token);

            if (!user.Success)
            {
                return Map(user);
            }

            (FirestoreResponse listResponse, List<JObject> cards) =
                await client.ListDocumentsAsync($"{UserPath(uid)}/ownedCards", token);

            if (!listResponse.Success)
            {
                return Map(listResponse);
            }

            var owned = new List<string>(cards.Count);

            foreach (JObject card in cards)
            {
                string id = FirestoreValueMapper.DocumentId(card);

                if (!string.IsNullOrEmpty(id))
                {
                    owned.Add(id);
                }
            }

            var wallet = new Wallet(
                (int)FirestoreValueMapper.GetInteger(user.Body, "coins"),
                (int)FirestoreValueMapper.GetInteger(user.Body, "silver")
            );

            return EconomyResult.Ok(new PlayerProfile(uid, wallet, owned));
        }

        private async Task<(string uid, string token, EconomyResult error)> SessionAsync()
        {
            string uid = userIdProvider();
            string token = await tokenProvider();

            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(token))
            {
                return (null, null, EconomyResult.Fail(EconomyErrorCode.NotSignedIn, "Bạn chưa đăng nhập."));
            }

            return (uid, token, null);
        }

        private static string UserPath(string uid)
        {
            return $"users/{uid}";
        }

        private static string ResultName(MatchResultKind kind)
        {
            switch (kind)
            {
                case MatchResultKind.Win:
                    return "win";
                case MatchResultKind.Draw:
                    return "draw";
                case MatchResultKind.EarlySurrender:
                    return "surrender_early";
                default:
                    return "loss";
            }
        }

        private static EconomyResult Map(FirestoreResponse response)
        {
            switch (response.ErrorStatus)
            {
                case "PERMISSION_DENIED":
                    return EconomyResult.Fail(EconomyErrorCode.PermissionDenied, "Giao dịch bị từ chối.");
                case "UNAUTHENTICATED":
                    return EconomyResult.Fail(EconomyErrorCode.NotSignedIn, "Phiên đăng nhập đã hết hạn.");
                case "ALREADY_EXISTS":
                case "FAILED_PRECONDITION":
                    return EconomyResult.Fail(EconomyErrorCode.Rejected, response.ErrorMessage);
                case "NETWORK_ERROR":
                case "UNAVAILABLE":
                case "DEADLINE_EXCEEDED":
                    return EconomyResult.Fail(EconomyErrorCode.Network, "Không thể kết nối máy chủ dữ liệu.");
                default:
                    return EconomyResult.Fail(EconomyErrorCode.Unknown, response.ErrorMessage ?? "Lỗi không xác định.");
            }
        }

        #endregion
    }
}
