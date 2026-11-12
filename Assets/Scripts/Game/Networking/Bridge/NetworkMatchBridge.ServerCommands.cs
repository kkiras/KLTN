using System;
using System.Collections;
using System.Collections.Generic;
using CMCMProductions;
using KLTN.Game.ApplicationLayer;
using KLTN.Game.Content;
using KLTN.Game.Domain;
using Unity.Netcode;
using UnityEngine;

namespace KLTN.Game.Networking
{
    public sealed partial class NetworkMatchBridge : NetworkBehaviour
    {
        #region Server Command Pipeline

        // Every command is routed by MatchCommandService from the sender's connection to its own
        // match (seat, started, not paused, growing command ID) before the rules engine runs.

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitSummonUnitRpc(
            ulong commandId,
            ulong cardInstanceId,
            RpcParams rpcParams = default
        )
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            MatchCommandOutcome outcome = commandService.Execute(
                senderClientId,
                commandId,
                (session, actor) => session.Rules.TrySummonUnit(session.State, actor, cardInstanceId)
            );

            FinishCommand(senderClientId, outcome);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitDeclareAttackRpc(
            ulong commandId,
            ulong[] attackerIdsBySlot,
            RpcParams rpcParams = default
        )
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            MatchCommandOutcome outcome = commandService.Execute(
                senderClientId,
                commandId,
                (session, actor) => session.Rules.TryDeclareAttack(session.State, actor, attackerIdsBySlot)
            );

            FinishCommand(senderClientId, outcome);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitDeclareBlockRpc(
            ulong commandId,
            ulong[] blockerIdsBySlot,
            RpcParams rpcParams = default
        )
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            MatchCommandOutcome outcome = commandService.Execute(
                senderClientId,
                commandId,
                (session, actor) => session.Rules.TryDeclareBlock(session.State, actor, blockerIdsBySlot)
            );

            FinishCommand(senderClientId, outcome);
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

            var selection = new AbilityTargetSelection(
                primaryTargetIds,
                secondaryTargetIds
            );

            MatchCommandOutcome outcome = commandService.Execute(
                senderClientId,
                commandId,
                (session, actor) => session.Rules.TrySubmitAbilitySelection(
                    session.State,
                    actor,
                    requestId,
                    selection
                )
            );

            FinishCommand(senderClientId, outcome);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitCancelAbilitySelectionRpc(
            ulong commandId,
            ulong requestId,
            RpcParams rpcParams = default
        )
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            MatchCommandOutcome outcome = commandService.Execute(
                senderClientId,
                commandId,
                (session, actor) => session.Rules.TryCancelAbilitySelection(session.State, actor, requestId)
            );

            FinishCommand(senderClientId, outcome);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitPassRpc(ulong commandId, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            MatchCommandOutcome outcome = commandService.Execute(
                senderClientId,
                commandId,
                (session, actor) => session.Rules.TryPass(session.State, actor)
            );

            FinishCommand(senderClientId, outcome);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitSurrenderRpc(
            ulong commandId,
            RpcParams rpcParams = default
        )
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            MatchCommandOutcome outcome = commandService.Execute(
                senderClientId,
                commandId,
                (session, actor) => session.Rules.TrySurrender(session.State, actor)
            );

            FinishCommand(senderClientId, outcome);
        }

        private float firstPlayerSwapReadyTime;

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SubmitMulliganRpc(
            ulong commandId,
            ulong[] cardIdsToReplace,
            RpcParams rpcParams = default
        )
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            MatchCommandOutcome outcome = commandService.Execute(
                senderClientId,
                commandId,
                (session, actor) => ApplyMulligan(session.State, actor, cardIdsToReplace)
            );

            FinishCommand(senderClientId, outcome);

            if (!outcome.Accepted || outcome.Session == null || outcome.Session.State.RoundNumber != 0)
            {
                return;
            }

            int replaceCount = cardIdsToReplace != null ? cardIdsToReplace.Length : 0;
            float currentPlayerSwapDuration = CalculateSwapAnimationDuration(replaceCount);

            if (!outcome.Session.State.IsMulliganPhaseComplete)
            {
                firstPlayerSwapReadyTime = Time.time + currentPlayerSwapDuration;
            }
            else
            {
                float remainingFirstPlayerSwap = Mathf.Max(0f, firstPlayerSwapReadyTime - Time.time);
                float effectiveSwapDuration = Mathf.Max(currentPlayerSwapDuration, remainingFirstPlayerSwap);

                NotifyBothMulliganConfirmedRpc(effectiveSwapDuration);

                float totalDelayBeforeRound1 = effectiveSwapDuration + 1.5f + 0.65f + 0.25f;
                StartCoroutine(BeginFirstRoundAfterMulliganDelayCoroutine(outcome.Session, totalDelayBeforeRound1));
            }
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void NotifyBothMulliganConfirmedRpc(float swapWaitDuration)
        {
            MulliganPresenter presenter = FindAnyObjectByType<MulliganPresenter>(FindObjectsInactive.Include);
            if (presenter != null)
            {
                presenter.NotifyBothPlayersConfirmed(swapWaitDuration);
            }
        }

        private static CommandResult ApplyMulligan(MatchState matchState, SeatId actor, ulong[] cardIdsToReplace)
        {
            bool alreadyDone = actor == SeatId.Host
                ? matchState.HostMulliganDone
                : matchState.GuestMulliganDone;

            if (alreadyDone)
            {
                return CommandResult.Success();
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

            return CommandResult.Success();
        }

        private static float CalculateSwapAnimationDuration(int replaceCount)
        {
            if (replaceCount <= 0) return 0f;

            float returnTime = 0.45f + (replaceCount - 1) * 0.15f;
            float flyInTime = 0.5f + (replaceCount - 1) * 0.2f;
            return returnTime + flyInTime;
        }

        private IEnumerator BeginFirstRoundAfterMulliganDelayCoroutine(MatchSession session, float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);

            if (!IsServer || session == null || session.State == null)
            {
                yield break;
            }

            if (session.State.IsMulliganPhaseComplete && session.State.RoundNumber == 0)
            {
                session.State.BeginFirstRound();
                session.State.LastEvent = "Giai đoạn đổi bài kết thúc. Trận đấu bắt đầu!";
                BroadcastUpdate(session);
            }
        }

        private void FinishCommand(ulong senderClientId, MatchCommandOutcome outcome)
        {
            CommandResult result = outcome.Result;

            if (!outcome.Accepted)
            {
                RejectCommand(senderClientId, result.RejectionReason);
                return;
            }

            MatchSession session = outcome.Session;

            if (result.Resolution != null)
            {
                Debug.Log(
                    $"[SERVER GRAVEYARD] Match={session.MatchId}, " +
                    $"Host={session.State.Host.Graveyard.Count}, " +
                    $"Guest={session.State.Guest.Graveyard.Count}"
                );
            }

            BroadcastUpdate(session, result.Resolution, result.RoundTransition);
            TrySettleFinishedMatch(session);
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
