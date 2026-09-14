using System;
using System.Collections;
using System.Collections.Generic;
using CMCMProductions;
using KLTN.Game.Content;
using KLTN.Game.Domain;
using Unity.Netcode;
using UnityEngine;

namespace KLTN.Game.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkMatchBridge : NetworkBehaviour
    {
        #region Constants

        private const int DeckSize = 20;
        private const int OpeningHandSize = 4;
        private const int SnapshotRequestAttempts = 10;
        private const float SnapshotRequestIntervalSeconds = 0.5f;

        #endregion

        #region Server State

        private readonly Dictionary<ulong, SeatId> seatByClient =
            new Dictionary<ulong, SeatId>();
        private readonly Dictionary<string, CardDefinition> definitionsById =
            new Dictionary<string, CardDefinition>();
        private readonly Dictionary<ulong, ulong> lastCommandIdByClient =
            new Dictionary<ulong, ulong>();
        private readonly HashSet<ulong> pendingSnapshotClients = new HashSet<ulong>();
        private ulong revision;
        private MatchState matchState;
        private MatchRulesEngine rulesEngine;
        private MatchSnapshotBuilder snapshotBuilder;
        private RoundResolutionDtoMapper resolutionDtoMapper;

        #endregion

        #region Client State

        private ulong nextLocalCommandId = 1;
        private Coroutine initialSnapshotCoroutine;
        private bool hasReceivedSnapshot;

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            hasReceivedSnapshot = false;

            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

                AssignConnectedClients();
                TryInitializeMatch();
                BroadcastUpdate();
            }

            if (IsClient && !IsServer)
            {
                initialSnapshotCoroutine = StartCoroutine(RequestInitialSnapshot());
            }

            Debug.Log(
                $"NetworkMatchBridge spawned. "
                    + $"ClientId={NetworkManager.LocalClientId}, "
                    + $"IsServer={IsServer}, IsClient={IsClient}, "
                    + $"NetworkObjectId={NetworkObjectId}."
            );
        }

        public override void OnNetworkDespawn()
        {
            if (initialSnapshotCoroutine != null)
            {
                StopCoroutine(initialSnapshotCoroutine);
                initialSnapshotCoroutine = null;
            }

            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            pendingSnapshotClients.Clear();
            MatchProjectionRegistry.Current.Reset();
            MatchUpdateInboxRegistry.Current.Reset();
        }

        #endregion

        #region Connection Handling and Seat Assignment

        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            AssignClient(clientId);
            TryInitializeMatch();
            BroadcastUpdate();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            if (!seatByClient.Remove(clientId))
            {
                return;
            }
            lastCommandIdByClient.Remove(clientId);
            Debug.Log($"Removed seat assignment for client {clientId}.");
            matchState = null;
            rulesEngine = null;
            snapshotBuilder = null;
            resolutionDtoMapper = null;
            definitionsById.Clear();
            BroadcastUpdate();
        }

        private void AssignConnectedClients()
        {
            if (NetworkManager.IsHost)
            {
                AssignClientToSeat(NetworkManager.LocalClientId, SeatId.Host);
            }

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                AssignClient(clientId);
            }
        }

        private void AssignClient(ulong clientId)
        {
            if (seatByClient.ContainsKey(clientId))
            {
                return;
            }

            if (!HasSeat(SeatId.Host))
            {
                AssignClientToSeat(clientId, SeatId.Host);
                return;
            }

            if (!HasSeat(SeatId.Guest))
            {
                AssignClientToSeat(clientId, SeatId.Guest);
                return;
            }

            Debug.LogWarning(
                $"Client {clientId} could not be assigned. " + "Both seats are occupied."
            );
        }

        private void AssignClientToSeat(ulong clientId, SeatId seat)
        {
            if (seatByClient.ContainsKey(clientId) || HasSeat(seat))
            {
                return;
            }

            seatByClient.Add(clientId, seat);
            Debug.Log($"Assigned client {clientId} to {seat}.");
        }

        private bool HasSeat(SeatId seat)
        {
            foreach (SeatId assignedSeat in seatByClient.Values)
            {
                if (assignedSeat == seat)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Snapshot Requests

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestSnapshotRpc(RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            Debug.Log($"Snapshot request received from client {senderClientId}.");

            AssignClient(senderClientId);
            bool matchCreated = TryInitializeMatch();

            if (matchCreated)
            {
                BroadcastUpdate();
                return;
            }

            if (!seatByClient.TryGetValue(senderClientId, out SeatId viewerSeat))
            {
                Debug.LogWarning($"No seat found for client {senderClientId}.");
                return;
            }

            SendUpdate(senderClientId, viewerSeat, null);
        }

        private void QueueSnapshotWhenVisible(ulong clientId)
        {
            if (!IsServer || !pendingSnapshotClients.Add(clientId))
            {
                return;
            }

            StartCoroutine(SendSnapshotWhenVisible(clientId));
        }

        private IEnumerator SendSnapshotWhenVisible(ulong clientId)
        {
            while (
                IsSpawned
                && NetworkManager != null
                && NetworkManager.IsListening
                && NetworkManager.ConnectedClients.ContainsKey(clientId)
                && !NetworkObject.IsNetworkVisibleTo(clientId)
            )
            {
                yield return null;
            }

            pendingSnapshotClients.Remove(clientId);

            if (
                !IsSpawned
                || NetworkManager == null
                || !NetworkManager.ConnectedClients.ContainsKey(clientId)
            )
            {
                yield break;
            }

            if (!seatByClient.TryGetValue(clientId, out SeatId viewerSeat))
            {
                Debug.LogWarning($"Cannot send snapshot: client {clientId} has no seat.");
                yield break;
            }

            Debug.Log($"NetworkMatchBridge is now visible to client {clientId}.");

            SendUpdate(clientId, viewerSeat, null);
        }

        private IEnumerator RequestInitialSnapshot()
        {
            yield return null;

            for (
                int attempt = 1;
                attempt <= SnapshotRequestAttempts && !hasReceivedSnapshot;
                attempt++
            )
            {
                if (
                    !IsSpawned
                    || NetworkManager == null
                    || !NetworkManager.IsConnectedClient
                )
                {
                    yield break;
                }

                Debug.Log(
                    $"Requesting initial snapshot. "
                        + $"Attempt={attempt}/{SnapshotRequestAttempts}, "
                        + $"ClientId={NetworkManager.LocalClientId}."
                );

                RequestSnapshotRpc();

                yield return new WaitForSecondsRealtime(SnapshotRequestIntervalSeconds);
            }

            initialSnapshotCoroutine = null;

            if (!hasReceivedSnapshot)
            {
                Debug.LogError(
                    $"Initial snapshot was not received after "
                        + $"{SnapshotRequestAttempts} attempts."
                );
            }
        }

        #endregion

        #region Match Initialization

        private bool TryInitializeMatch()
        {
            if (matchState != null)
            {
                return false;
            }

            if (!HasSeat(SeatId.Host) || !HasSeat(SeatId.Guest))
            {
                return false;
            }

            Card[] cardAssets = Resources.LoadAll<Card>("Cards");

            if (cardAssets == null || cardAssets.Length == 0)
            {
                Debug.LogError("No Card assets were found in Resources/Cards.");

                return false;
            }

            definitionsById.Clear();

            try
            {
                var definitions = new List<CardDefinition>();

                foreach (Card cardAsset in cardAssets)
                {
                    if (cardAsset == null)
                    {
                        continue;
                    }

                    CardDefinition definition = CardDefinitionMapper.Create(cardAsset);

                    if (definitionsById.ContainsKey(definition.Id))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate card definition ID: {definition.Id}"
                        );
                    }

                    definitionsById.Add(definition.Id, definition);

                    definitions.Add(definition);
                }

                CardContentValidator.Validate(definitions);

                definitions.Sort(
                    (left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id)
                );

                IReadOnlyList<CardDefinition> defaultDeck = DefaultDeckBuilder.Build(
                    definitions
                );

                IRandomSource random = new SeededRandomSource(Environment.TickCount);

                SeatId firstSeat = random.Next(0, 2) == 0 ? SeatId.Host : SeatId.Guest;

                var factory = new MatchFactory(random);

                matchState = factory.Create(
                    defaultDeck,
                    defaultDeck,
                    OpeningHandSize,
                    firstSeat
                );

                rulesEngine = new MatchRulesEngine(definitionsById, random);

                snapshotBuilder = new MatchSnapshotBuilder(definitionsById);

                resolutionDtoMapper = new RoundResolutionDtoMapper(definitionsById);

                Debug.Log(
                    $"Match initialized. "
                        + $"First seat: {matchState.FirstSeat}. "
                        + $"Deck size: {DeckRules.RequiredCardCount}. "
                        + $"Host hand: {matchState.Host.DrawHand.Count}, "
                        + $"Guest hand: {matchState.Guest.DrawHand.Count}."
                );

                return true;
            }
            catch (Exception exception)
            {
                matchState = null;
                rulesEngine = null;
                snapshotBuilder = null;
                resolutionDtoMapper = null;

                definitionsById.Clear();

                Debug.LogError("Match initialization failed: " + exception.Message);

                return false;
            }
        }

        #endregion

        #region Match Update Publication

        private void BroadcastUpdate(
            RoundResolution resolution = null,
            RoundTransition roundTransition = null
        )
        {
            if (!IsServer)
            {
                return;
            }

            revision++;

            RoundResolutionDto resolutionDto =
                resolution == null
                    ? null
                    : resolutionDtoMapper.Build(resolution, matchState);

            RoundTransitionDto roundTransitionDto = BuildRoundTransitionDto(
                roundTransition
            );

            foreach (KeyValuePair<ulong, SeatId> pair in seatByClient)
            {
                SendUpdate(pair.Key, pair.Value, resolutionDto, roundTransitionDto);
            }
        }

        private void SendUpdate(
            ulong targetClientId,
            SeatId viewerSeat,
            RoundResolutionDto resolution,
            RoundTransitionDto roundTransition = null
        )
        {
            bool isVisible = NetworkObject.IsNetworkVisibleTo(targetClientId);

            Debug.Log(
                $"Preparing match update. Target={targetClientId}, "
                    + $"Viewer={viewerSeat}, Visible={isVisible}, Revision={revision}."
            );

            if (!isVisible)
            {
                QueueSnapshotWhenVisible(targetClientId);
                return;
            }

            var update = new MatchUpdateDto
            {
                snapshot = BuildSnapshot(viewerSeat),

                hasResolution = resolution != null,

                resolution = resolution,

                hasRoundTransition = roundTransition != null,

                roundTransition = roundTransition,
            };

            string json = JsonUtility.ToJson(update);

            Debug.Log(
                $"Sending match update through Universal RPC. "
                    + $"Target={targetClientId}, JsonLength={json.Length}."
            );

            ReceiveMatchUpdateRpc(
                json,
                RpcTarget.Single(targetClientId, RpcTargetUse.Temp)
            );
        }

        private MatchSnapshotDto BuildSnapshot(SeatId viewerSeat)
        {
            SeatId opponentSeat = viewerSeat.Opponent();
            bool opponentConnected = HasSeat(opponentSeat);

            if (matchState == null || snapshotBuilder == null)
            {
                return MatchSnapshotBuilder.BuildWaiting(
                    viewerSeat,
                    opponentConnected,
                    revision
                );
            }

            return snapshotBuilder.Build(
                matchState,
                viewerSeat,
                opponentConnected,
                revision
            );
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        private void ReceiveMatchUpdateRpc(string json, RpcParams rpcParams = default)
        {
            Debug.Log(
                $"Universal match update arrived. "
                    + $"ClientId={NetworkManager.LocalClientId}, JsonLength={json?.Length ?? 0}."
            );

            MatchUpdateDto update = JsonUtility.FromJson<MatchUpdateDto>(json);

            if (update?.snapshot == null)
            {
                Debug.LogWarning("Received an invalid match update.");
                return;
            }

            hasReceivedSnapshot = true;
            MatchUpdateInboxRegistry.Current.Enqueue(update);

            Debug.Log(
                $"Received match update. "
                    + $"Viewer={update.snapshot.viewerSeat}, "
                    + $"Self={update.snapshot.self.seat}, "
                    + $"Opponent={update.snapshot.opponent.seat}, "
                    + $"Revision={update.snapshot.revision}, "
                    + $"HasResolution={update.hasResolution}."
            );
        }

        #endregion

        #region Client Command API

        public bool RequestSummonUnit(string instanceId)
        {
            if (!IsSpawned || !IsClient)
            {
                return false;
            }

            if (!ulong.TryParse(instanceId, out ulong parsedId))
            {
                MatchProjectionRegistry.Current.Reject(
                    CommandRejectionReason.CardNotInHand
                );

                return false;
            }

            SubmitSummonUnitRpc(nextLocalCommandId++, parsedId);

            return true;
        }

        public bool RequestDeclareAttack(string[] attackerIdsBySlot)
        {
            if (!IsSpawned || !IsClient)
            {
                return false;
            }

            if (
                attackerIdsBySlot == null
                || attackerIdsBySlot.Length != MatchState.BoardSlotCount
            )
            {
                MatchProjectionRegistry.Current.Reject(
                    CommandRejectionReason.InvalidAttackDeclaration
                );

                return false;
            }

            var parsedIds = new ulong[MatchState.BoardSlotCount];

            for (int slotIndex = 0; slotIndex < attackerIdsBySlot.Length; slotIndex++)
            {
                string instanceId = attackerIdsBySlot[slotIndex];

                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    parsedIds[slotIndex] = 0;
                    continue;
                }

                if (!ulong.TryParse(instanceId, out parsedIds[slotIndex]))
                {
                    MatchProjectionRegistry.Current.Reject(
                        CommandRejectionReason.InvalidAttackDeclaration
                    );

                    return false;
                }
            }

            SubmitDeclareAttackRpc(nextLocalCommandId++, parsedIds);

            return true;
        }

        public bool RequestDeclareBlock(string[] blockerIdsBySlot)
        {
            if (!IsSpawned || !IsClient)
            {
                return false;
            }

            if (
                blockerIdsBySlot == null
                || blockerIdsBySlot.Length != MatchState.BoardSlotCount
            )
            {
                MatchProjectionRegistry.Current.Reject(
                    CommandRejectionReason.InvalidBlockDeclaration
                );

                return false;
            }

            var parsedIds = new ulong[MatchState.BoardSlotCount];

            for (int slotIndex = 0; slotIndex < blockerIdsBySlot.Length; slotIndex++)
            {
                string instanceId = blockerIdsBySlot[slotIndex];

                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    parsedIds[slotIndex] = 0;
                    continue;
                }

                if (!ulong.TryParse(instanceId, out parsedIds[slotIndex]))
                {
                    MatchProjectionRegistry.Current.Reject(
                        CommandRejectionReason.InvalidBlockDeclaration
                    );

                    return false;
                }
            }

            SubmitDeclareBlockRpc(nextLocalCommandId++, parsedIds);

            return true;
        }

        public bool RequestSubmitAbilitySelection(
            string requestId,
            string[] primaryTargetIds,
            string[] secondaryTargetIds
        )
        {
            if (
                !IsSpawned
                || !IsClient
                || !ulong.TryParse(requestId, out ulong parsedRequestId)
            )
            {
                return false;
            }

            if (
                !TryParseOptionalCardIds(
                    primaryTargetIds,
                    out ulong[] parsedPrimaryTargets
                )
                || !TryParseOptionalCardIds(
                    secondaryTargetIds,
                    out ulong[] parsedSecondaryTargets
                )
            )
            {
                MatchProjectionRegistry.Current.Reject(
                    CommandRejectionReason.InvalidAbilityTarget
                );

                return false;
            }

            SubmitAbilitySelectionRpc(
                nextLocalCommandId++,
                parsedRequestId,
                parsedPrimaryTargets,
                parsedSecondaryTargets
            );

            return true;
        }

        public bool RequestCancelAbilitySelection(string requestId)
        {
            if (
                !IsSpawned
                || !IsClient
                || !ulong.TryParse(requestId, out ulong parsedRequestId)
            )
            {
                return false;
            }

            SubmitCancelAbilitySelectionRpc(nextLocalCommandId++, parsedRequestId);

            return true;
        }

        public bool RequestPass()
        {
            if (!IsSpawned || !IsClient)
            {
                return false;
            }

            SubmitPassRpc(nextLocalCommandId++);
            return true;
        }

        public bool RequestMulligan(ulong[] cardIdsToReplace)
        {
            if (!IsSpawned || !IsClient)
            {
                return false;
            }

            SubmitMulliganRpc(nextLocalCommandId++, cardIdsToReplace);
            return true;
        }

        private static bool TryParseOptionalCardIds(
            string[] instanceIds,
            out ulong[] parsedIds
        )
        {
            if (instanceIds == null || instanceIds.Length == 0)
            {
                parsedIds = Array.Empty<ulong>();

                return true;
            }

            parsedIds = new ulong[instanceIds.Length];

            for (int i = 0; i < instanceIds.Length; i++)
            {
                if (!ulong.TryParse(instanceIds[i], out parsedIds[i]))
                {
                    parsedIds = Array.Empty<ulong>();

                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Server Command Pipeline

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitSummonUnitRpc(
            ulong commandId,
            ulong cardInstanceId,
            RpcParams rpcParams = default
        )
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (!TryBeginCommand(senderClientId, commandId, out SeatId actor))
            {
                return;
            }

            CommandResult result = rulesEngine.TrySummonUnit(
                matchState,
                actor,
                cardInstanceId
            );

            FinishCommand(senderClientId, result);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitDeclareAttackRpc(
            ulong commandId,
            ulong[] attackerIdsBySlot,
            RpcParams rpcParams = default
        )
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (!TryBeginCommand(senderClientId, commandId, out SeatId actor))
            {
                return;
            }

            CommandResult result = rulesEngine.TryDeclareAttack(
                matchState,
                actor,
                attackerIdsBySlot
            );

            FinishCommand(senderClientId, result);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitDeclareBlockRpc(
            ulong commandId,
            ulong[] blockerIdsBySlot,
            RpcParams rpcParams = default
        )
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (!TryBeginCommand(senderClientId, commandId, out SeatId actor))
            {
                return;
            }

            CommandResult result = rulesEngine.TryDeclareBlock(
                matchState,
                actor,
                blockerIdsBySlot
            );

            FinishCommand(senderClientId, result);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitAbilitySelectionRpc(
            ulong commandId,
            ulong requestId,
            ulong[] primaryTargetIds,
            ulong[] secondaryTargetIds,
            RpcParams rpcParams = default
        )
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (!TryBeginCommand(senderClientId, commandId, out SeatId actor))
            {
                return;
            }

            var selection = new AbilityTargetSelection(
                primaryTargetIds,
                secondaryTargetIds
            );

            CommandResult result = rulesEngine.TrySubmitAbilitySelection(
                matchState,
                actor,
                requestId,
                selection
            );

            FinishCommand(senderClientId, result);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitCancelAbilitySelectionRpc(
            ulong commandId,
            ulong requestId,
            RpcParams rpcParams = default
        )
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (!TryBeginCommand(senderClientId, commandId, out SeatId actor))
            {
                return;
            }

            CommandResult result = rulesEngine.TryCancelAbilitySelection(
                matchState,
                actor,
                requestId
            );

            FinishCommand(senderClientId, result);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitPassRpc(ulong commandId, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (!TryBeginCommand(senderClientId, commandId, out SeatId actor))
            {
                return;
            }

            CommandResult result = rulesEngine.TryPass(matchState, actor);
            FinishCommand(senderClientId, result);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitMulliganRpc(
            ulong commandId,
            ulong[] cardIdsToReplace,
            RpcParams rpcParams = default
        )
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (!TryBeginCommand(senderClientId, commandId, out SeatId actor))
            {
                return;
            }

            PlayerState player = matchState.Player(actor);

            if (cardIdsToReplace != null && cardIdsToReplace.Length > 0)
            {
                System.Random rng = new System.Random();
                player.PerformMulligan(cardIdsToReplace, rng);
            }

            if (actor == SeatId.Host)
                matchState.HostMulliganDone = true;
            else
                matchState.GuestMulliganDone = true;

            if (matchState.IsMulliganPhaseComplete && matchState.RoundNumber == 0)
            {
                matchState.BeginFirstRound();

                matchState.LastEvent = "Giai đoạn đổi bài kết thúc. Trận đấu bắt đầu!";
            }

            FinishCommand(senderClientId, CommandResult.Success());
        }

        private bool TryBeginCommand(
            ulong senderClientId,
            ulong commandId,
            out SeatId actor
        )
        {
            actor = default;

            if (!seatByClient.TryGetValue(senderClientId, out actor))
            {
                RejectCommand(senderClientId, CommandRejectionReason.InvalidSeat);
                return false;
            }

            if (matchState == null || rulesEngine == null)
            {
                RejectCommand(senderClientId, CommandRejectionReason.MatchNotReady);
                return false;
            }

            if (
                lastCommandIdByClient.TryGetValue(senderClientId, out ulong lastCommandId)
                && commandId <= lastCommandId
            )
            {
                RejectCommand(senderClientId, CommandRejectionReason.DuplicateCommand);
                return false;
            }

            // A rejected domain command still consumes its command ID so a
            // client cannot replay the same request.
            lastCommandIdByClient[senderClientId] = commandId;
            return true;
        }

        private void FinishCommand(ulong senderClientId, CommandResult result)
        {
            if (!result.Accepted)
            {
                RejectCommand(senderClientId, result.RejectionReason);
                return;
            }

            BroadcastUpdate(result.Resolution, result.RoundTransition);
        }

        #endregion

        #region Client Rejection Feedback

        private void RejectCommand(ulong targetClientId, CommandRejectionReason reason)
        {
            ReceiveCommandRejectedRpc(
                (int)reason,
                RpcTarget.Single(targetClientId, RpcTargetUse.Temp)
            );
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        private void ReceiveCommandRejectedRpc(
            int reasonValue,
            RpcParams rpcParams = default
        )
        {
            var reason = (CommandRejectionReason)reasonValue;

            MatchProjectionRegistry.Current.Reject(reason);
            Debug.LogWarning($"Match command rejected: {reason}.");
        }

        #endregion

        private static RoundTransitionDto BuildRoundTransitionDto(
            RoundTransition transition
        )
        {
            if (transition == null)
            {
                return null;
            }

            return new RoundTransitionDto
            {
                completedRoundNumber = transition.CompletedRoundNumber,

                nextRoundNumber = transition.NextRoundNumber,

                nextAttackTokenOwner = (int)transition.NextAttackTokenOwner,
            };
        }
    }
}
