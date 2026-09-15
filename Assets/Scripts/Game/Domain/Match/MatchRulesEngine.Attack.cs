using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public sealed partial class MatchRulesEngine
    {
        #region Attack Declaration

        /// <summary>
        /// Atomically validates and stages attackers by slot, consumes the attack token,
        /// then emits ordered Attack and Support events before block declaration.
        /// </summary>
        public CommandResult TryDeclareAttack(
            MatchState state,
            SeatId actor,
            ulong[] attackerIdsBySlot
        )
        {
            CommandResult actorValidation = ValidatePriorityActor(state, actor);

            if (!actorValidation.Accepted)
            {
                return actorValidation;
            }

            if (state.AttackTokenOwner != actor)
            {
                return CommandResult.Reject(CommandRejectionReason.NotAttackTokenOwner);
            }

            if (!state.AttackTokenAvailable)
            {
                return CommandResult.Reject(
                    CommandRejectionReason.AttackTokenUnavailable
                );
            }

            PlayerState attacker = state.Player(actor);

            if (attacker.Board.Count > 0)
            {
                return CommandResult.Reject(CommandRejectionReason.BoardNotEmpty);
            }

            if (
                attackerIdsBySlot == null
                || attackerIdsBySlot.Length != MatchState.BoardSlotCount
            )
            {
                return CommandResult.Reject(
                    CommandRejectionReason.InvalidAttackDeclaration
                );
            }

            var selectedCards = new CardInstance[MatchState.BoardSlotCount];

            var uniqueIds = new HashSet<ulong>();

            int attackerCount = 0;

            // Validate the entire declaration before mutating state.
            for (int slotIndex = 0; slotIndex < attackerIdsBySlot.Length; slotIndex++)
            {
                ulong instanceId = attackerIdsBySlot[slotIndex];

                // Zero represents an empty board slot.
                if (instanceId == 0)
                {
                    continue;
                }

                if (!uniqueIds.Add(instanceId))
                {
                    return CommandResult.Reject(
                        CommandRejectionReason.InvalidAttackDeclaration
                    );
                }

                CardInstance card = attacker.FindCardInReserve(instanceId);

                if (card == null)
                {
                    return CommandResult.Reject(CommandRejectionReason.CardNotInReserve);
                }

                selectedCards[slotIndex] = card;
                attackerCount++;
            }

            if (attackerCount == 0)
            {
                return CommandResult.Reject(
                    CommandRejectionReason.InvalidAttackDeclaration
                );
            }

            for (int slotIndex = 0; slotIndex < selectedCards.Length; slotIndex++)
            {
                CardInstance card = selectedCards[slotIndex];

                if (card == null)
                {
                    continue;
                }

                bool moved = attacker.TryMoveReserveCardToBoard(card, slotIndex);

                if (!moved)
                {
                    throw new InvalidOperationException(
                        "Validated attack declaration could not be applied."
                    );
                }
            }

            GameEventBatch joinedAttackEvents =
                unitPassiveRuleResolver.JoinAttackFromGraveyard(
                    state,
                    actor,
                    selectedCards
                );

            attackerCount = 0;

            foreach (CardInstance attackerCard in selectedCards)
            {
                if (attackerCard != null)
                {
                    attackerCount++;
                }
            }

            state.AttackTokenAvailable = false;
            state.ConsecutivePasses = 0;

            state.Phase = MatchPhase.BlockDeclaration;

            state.ActiveSeat = actor.Opponent();

            GameEventBatch attackEvents = BuildAttackEventBatch(
                state.RoundNumber,
                selectedCards
            );

            EnqueueAndResolveAbilities(
                state,
                MergeGameEventBatches(joinedAttackEvents, attackEvents)
            );

            UpdateOutcome(state);

            if (state.IsFinished)
            {
                state.Phase = MatchPhase.Finished;

                state.LastEvent =
                    $"Skill khi tấn công đã kết thúc trận. "
                    + $"Kết quả: {state.Outcome}.";

                return CommandResult.Success();
            }

            if (state.PendingSelection == null)
            {
                state.LastEvent =
                    $"{actor} đã khai báo tấn công bằng "
                    + $"{attackerCount} lá bài. "
                    + $"{state.ActiveSeat} đang chọn blocker.";
            }

            return CommandResult.Success();
        }

        private static GameEventBatch BuildAttackEventBatch(
            int roundNumber,
            CardInstance[] attackersBySlot
        )
        {
            var events = new List<GameEvent>();

            for (int slotIndex = 0; slotIndex < attackersBySlot.Length; slotIndex++)
            {
                CardInstance attacker = attackersBySlot[slotIndex];

                if (attacker == null)
                {
                    continue;
                }

                events.Add(
                    GameEvent.FromCard(
                        GameEventType.AttackDeclared,
                        roundNumber,
                        attacker
                    )
                );

                int supportedSlot = slotIndex + 1;

                if (supportedSlot >= attackersBySlot.Length)
                {
                    continue;
                }

                CardInstance supportedUnit = attackersBySlot[supportedSlot];

                if (supportedUnit == null)
                {
                    continue;
                }

                events.Add(
                    GameEvent.FromCard(
                        GameEventType.UnitSupported,
                        roundNumber,
                        attacker,
                        supportedUnit
                    )
                );
            }

            return new GameEventBatch(events);
        }

        private static GameEventBatch MergeGameEventBatches(
            params GameEventBatch[] batches
        )
        {
            var events = new List<GameEvent>();

            if (batches == null)
            {
                return new GameEventBatch(events);
            }

            foreach (GameEventBatch batch in batches)
            {
                if (batch == null)
                {
                    continue;
                }

                foreach (GameEvent gameEvent in batch.Events)
                {
                    events.Add(gameEvent);
                }
            }

            return new GameEventBatch(events);
        }

        #endregion
    }
}
