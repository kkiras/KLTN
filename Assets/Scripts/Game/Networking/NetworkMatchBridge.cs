using System;
using System.Collections.Generic;
using CMCMProductions;
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

        #endregion

        #region Server State

        private readonly Dictionary<ulong, SeatId> seatByClient = new Dictionary<ulong, SeatId>();
        private readonly Dictionary<string, CardDefinition> definitionsById = new Dictionary<string, CardDefinition>();
        private readonly Dictionary<ulong, ulong> lastCommandIdByClient = new Dictionary<ulong, ulong>();
        private ulong revision;
        private MatchState matchState;
        private MatchRulesEngine rulesEngine;
        private MatchSnapshotBuilder snapshotBuilder;

        #endregion

        #region Client State

        private ulong nextLocalCommandId = 1;

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
                AssignConnectedClients();
                TryInitializeMatch();
                BroadcastSnapshots();
            }

            // A guest requests state only after its NetworkObject has spawned,
            // preventing the RPC from arriving before the object exists.
            if (IsClient && !IsServer) { RequestSnapshotRpc(); }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            MatchProjectionRegistry.Current.Reset();
        }

        #endregion

        #region Connection Handling and Seat Assignment

        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer) { return; }

            AssignClient(clientId);
            TryInitializeMatch();
            BroadcastSnapshots();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) { return; }

            if (!seatByClient.Remove(clientId)) { return; }
            lastCommandIdByClient.Remove(clientId);
            Debug.Log($"Removed seat assignment for client {clientId}.");
            matchState = null;
            rulesEngine = null;
            snapshotBuilder = null;
            definitionsById.Clear();
            BroadcastSnapshots();
        }

        private void AssignConnectedClients()
        {
            // A listen host owns a local gameplay client. A dedicated server
            // does not, so the first remote client receives the Host seat.
            if (NetworkManager.IsHost) { AssignClientToSeat(NetworkManager.LocalClientId, SeatId.Host); }

            foreach (ulong clientId in
                    NetworkManager.ConnectedClientsIds)
            {
                AssignClient(clientId);
            }
        }

        private void AssignClient(ulong clientId)
        {
            if (seatByClient.ContainsKey(clientId)) { return; }

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

            Debug.LogWarning($"Client {clientId} could not be assigned. " + "Both seats are occupied.");
        }

        private void AssignClientToSeat(ulong clientId, SeatId seat)
        {
            if (seatByClient.ContainsKey(clientId) ||
                HasSeat(seat))
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
                if (assignedSeat == seat) { return true; }
            }

            return false;
        }

        #endregion

        #region Snapshot Requests

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestSnapshotRpc(RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            AssignClient(senderClientId);
            bool matchCreated = TryInitializeMatch();

            if (matchCreated)
            {
                BroadcastSnapshots();
                return;
            }

            if (!seatByClient.TryGetValue(senderClientId, out SeatId viewerSeat))
            {
                Debug.LogWarning($"No seat found for client {senderClientId}.");
                return;
            }

            SendSnapshot(senderClientId, viewerSeat);
        }

        #endregion

        #region Match Initialization

        private bool TryInitializeMatch()
        {
            if (matchState != null) { return false; }

            if (!HasSeat(SeatId.Host) || !HasSeat(SeatId.Guest)) { return false; }

            Card[] cardAssets = Resources.LoadAll<Card>("Cards");

            if (cardAssets == null || cardAssets.Length == 0)
            {
                Debug.LogError("No Card assets were found in Resources/Cards.");
                return false;
            }

            definitionsById.Clear();
            var definitions = new List<CardDefinition>();

            foreach (Card cardAsset in cardAssets)
            {
                if (cardAsset == null) { continue; }

                // The asset name is the stable runtime definition ID.
                string definitionId = cardAsset.name;

                if (definitionsById.ContainsKey(definitionId))
                {
                    Debug.LogWarning($"Duplicate card definition ID: {definitionId}");
                    continue;
                }

                var definition = new CardDefinition(
                    definitionId,
                    cardAsset.cardName,
                    cardAsset.health,
                    cardAsset.damage,
                    cardAsset.energy);
                definitionsById.Add(definitionId, definition);
                definitions.Add(definition);
            }

            if (definitions.Count == 0)
            {
                Debug.LogError("No valid card definitions could be loaded.");
                return false;
            }

            IRandomSource random = new SeededRandomSource(Environment.TickCount);

            SeatId firstSeat = random.Next(0, 2) == 0
                ? SeatId.Host
                : SeatId.Guest;
            var factory = new MatchFactory(random);
            matchState = factory.Create(definitions, DeckSize, OpeningHandSize, firstSeat);
            rulesEngine = new MatchRulesEngine(definitionsById);
            snapshotBuilder = new MatchSnapshotBuilder(definitionsById);

            Debug.Log(
                $"Match initialized. " +
                $"First seat: {matchState.FirstSeat}. " +
                $"Host hand: {matchState.Host.Hand.Count}, " +
                $"Guest hand: {matchState.Guest.Hand.Count}.");
            return true;
        }

        #endregion

        #region Snapshot Publication

        private void BroadcastSnapshots()
        {
            if (!IsServer) { return; }

            revision++;

            foreach (KeyValuePair<ulong, SeatId> pair in seatByClient)
            {
                SendSnapshot(pair.Key, pair.Value);
            }
        }

        private void SendSnapshot(ulong targetClientId, SeatId viewerSeat)
        {
            MatchSnapshotDto snapshot = BuildSnapshot(viewerSeat);
            string json = JsonUtility.ToJson(snapshot);

            ReceiveSnapshotClientRpc(json, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId }
                }
            });
        }

        private MatchSnapshotDto BuildSnapshot(SeatId viewerSeat)
        {
            SeatId opponentSeat = viewerSeat.Opponent();
            bool opponentConnected = HasSeat(opponentSeat);

            if (matchState == null || snapshotBuilder == null)
            {
                return MatchSnapshotBuilder.BuildWaiting(viewerSeat, opponentConnected, revision);
            }

            return snapshotBuilder.Build(matchState, viewerSeat, opponentConnected, revision);
        }

        [ClientRpc]
        private void ReceiveSnapshotClientRpc(string json, ClientRpcParams clientRpcParams = default)
        {
            MatchSnapshotDto snapshot = JsonUtility.FromJson<MatchSnapshotDto>(json);

            if (snapshot == null)
            {
                Debug.LogWarning("Received an invalid match snapshot.");
                return;
            }

            MatchProjectionRegistry.Current.Apply(snapshot);

            Debug.Log(
                $"Received POV snapshot. " +
                $"Viewer={snapshot.viewerSeat}, " +
                $"Self={snapshot.self.seat}, " +
                $"Opponent={snapshot.opponent.seat}, " +
                $"Revision={snapshot.revision}");
        }

        #endregion

        #region Client Command API

        public bool RequestPlayUnit(string instanceId, int boardSlotIndex)
        {
            if (!IsSpawned || !IsClient) { return false; }

            if (!ulong.TryParse(instanceId, out ulong parsedId))
            {
                MatchProjectionRegistry.Current.Reject(CommandRejectionReason.CardNotInHand);
                return false;
            }

            SubmitPlayUnitRpc(nextLocalCommandId++, parsedId, boardSlotIndex);
            return true;
        }

        public bool RequestPass()
        {
            if (!IsSpawned || !IsClient) { return false; }

            SubmitPassRpc(nextLocalCommandId++);
            return true;
        }

        #endregion

        #region Server Command Pipeline

        [Rpc(
            SendTo.Server,
            InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitPlayUnitRpc(
            ulong commandId,
            ulong cardInstanceId,
            int boardSlotIndex,
            RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (!TryBeginCommand(senderClientId, commandId, out SeatId actor)) { return; }

            CommandResult result = rulesEngine.TryPlayUnit(matchState, actor, cardInstanceId, boardSlotIndex);
            FinishCommand(senderClientId, result);
        }

        [Rpc(
            SendTo.Server,
            InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitPassRpc(ulong commandId, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (!TryBeginCommand(senderClientId, commandId, out SeatId actor)) { return; }

            CommandResult result = rulesEngine.TryPass(matchState, actor);
            FinishCommand(senderClientId, result);
        }

        private bool TryBeginCommand(ulong senderClientId, ulong commandId, out SeatId actor)
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

            if (lastCommandIdByClient.TryGetValue(senderClientId, out ulong lastCommandId) &&
                commandId <= lastCommandId)
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

            BroadcastSnapshots();
        }

        #endregion

        #region Client Rejection Feedback

        private void RejectCommand(ulong targetClientId, CommandRejectionReason reason)
        {
            ReceiveCommandRejectedClientRpc(
                (int)reason,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { targetClientId } } });
        }

        [ClientRpc]
        private void ReceiveCommandRejectedClientRpc(int reasonValue, ClientRpcParams clientRpcParams = default)
        {
            var reason = (CommandRejectionReason)reasonValue;
            MatchProjectionRegistry.Current.Reject(reason);
            Debug.LogWarning($"Match command rejected: {reason}.");
        }

        #endregion
    }
}
