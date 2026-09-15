using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public sealed partial class MatchRulesEngine
    {
        #region Priority Progression

        private static void CompleteNonPassAction(
            MatchState state,
            SeatId actor,
            string actionDescription
        )
        {
            state.ConsecutivePasses = 0;
            state.ActiveSeat = actor.Opponent();

            state.LastEvent =
                $"{actionDescription} "
                + $"Quyền hành động chuyển cho {state.ActiveSeat}.";
        }

        private RoundTransition CompleteRound(MatchState state)
        {
            state.Phase = MatchPhase.RoundEnd;

            state.AttackTokenAvailable = false;

            GameEventBatch ephemeralDeaths = ExpireEphemeralUnits(state);

            EnqueueAndResolveAbilities(state, ephemeralDeaths);

            UpdateOutcome(state);

            if (state.IsFinished)
            {
                state.Phase = MatchPhase.Finished;

                return null;
            }

            if (state.PendingSelection != null)
            {
                return null;
            }

            return FinishRoundTransition(state);
        }

        private RoundTransition FinishRoundTransition(MatchState state)
        {
            int completedRound = state.RoundNumber;

            ClearRoundModifiers(state.Host);

            ClearRoundModifiers(state.Guest);

            state.Host.ReturnBoardCardsToReserve();
            state.Guest.ReturnBoardCardsToReserve();

            if (completedRound % DrawIntervalRounds == 0)
            {
                state.Host.DrawOne();
                state.Guest.DrawOne();
            }

            state.Host.AdvanceAndRefillMana(MaximumMana);

            state.Guest.AdvanceAndRefillMana(MaximumMana);

            state.AttackTokenOwner = state.AttackTokenOwner.Opponent();

            state.RoundNumber = completedRound + 1;

            state.RoundHistory.AdvanceToRound(state.RoundNumber);

            state.ConsecutivePasses = 0;

            state.Phase = MatchPhase.RoundStart;

            state.ActiveSeat = state.AttackTokenOwner;

            state.AttackTokenAvailable = true;

            var transition = new RoundTransition(
                completedRound,
                state.RoundNumber,
                state.AttackTokenOwner
            );

            GameEventBatch passiveReviveEvents =
                unitPassiveRuleResolver.ReviveAtRoundStart(state);

            GameEventBatch roundStartEvents = GameEventBatch.From(
                GameEvent.RoundStarted(state.RoundNumber)
            );

            EnqueueAndResolveAbilities(
                state,
                MergeGameEventBatches(passiveReviveEvents, roundStartEvents)
            );

            UpdateOutcome(state);

            if (state.IsFinished)
            {
                state.Phase = MatchPhase.Finished;

                return transition;
            }

            if (state.PendingSelection == null)
            {
                EnterRoundPriority(state);
            }

            return transition;
        }

        private RoundTransition ContinueAutomaticPhase(MatchState state)
        {
            if (state.IsFinished || state.PendingSelection != null)
            {
                return null;
            }

            if (state.Phase == MatchPhase.RoundEnd)
            {
                return FinishRoundTransition(state);
            }

            if (state.Phase == MatchPhase.RoundStart)
            {
                EnterRoundPriority(state);
            }

            return null;
        }

        private static void EnterRoundPriority(MatchState state)
        {
            state.Phase = MatchPhase.Priority;

            state.LastEvent =
                $"{state.AttackTokenOwner} nhận attack token "
                + $"và hành động đầu tiên ở vòng "
                + $"{state.RoundNumber}.";
        }

        private GameEventBatch ExpireEphemeralUnits(MatchState state)
        {
            var expiredCards = new List<CardInstance>();

            AddEphemeralBoardCards(state.Host, expiredCards);

            AddEphemeralBoardCards(state.Guest, expiredCards);

            expiredCards.Sort(
                (left, right) => left.InstanceId.CompareTo(right.InstanceId)
            );

            var deathEvents = new List<GameEvent>();

            foreach (CardInstance card in expiredCards)
            {
                if (
                    !definitionsById.TryGetValue(
                        card.DefinitionId,
                        out CardDefinition definition
                    )
                )
                {
                    continue;
                }

                card.Kill();

                bool moved = state
                    .Player(card.Owner)
                    .TryMoveDeadActiveCardToGraveyard(card);

                if (!moved)
                {
                    continue;
                }

                int damageAtDeath = card.GetDamage(definition);

                int maximumHealthAtDeath = card.GetMaximumHealth(definition);

                state.RoundHistory.RecordDeath(card, damageAtDeath, maximumHealthAtDeath);

                deathEvents.Add(
                    GameEvent.FromCard(GameEventType.UnitDied, state.RoundNumber, card)
                );
            }

            return new GameEventBatch(deathEvents);
        }

        private void AddEphemeralBoardCards(PlayerState player, List<CardInstance> result)
        {
            foreach (CardInstance card in player.Board)
            {
                if (!card.IsDead && HasKeyword(card, UnitKeyword.Ephemeral))
                {
                    result.Add(card);
                }
            }
        }

        private void ClearRoundModifiers(PlayerState player)
        {
            foreach (CardInstance card in player.DrawHand)
            {
                ClearRoundModifiers(card);
            }
            foreach (CardInstance card in player.Reserve)
            {
                ClearRoundModifiers(card);
            }

            foreach (CardInstance card in player.Board)
            {
                ClearRoundModifiers(card);
            }
        }

        private void ClearRoundModifiers(CardInstance card)
        {
            if (
                !definitionsById.TryGetValue(
                    card.DefinitionId,
                    out CardDefinition definition
                )
            )
            {
                return;
            }

            card.ClearRoundModifiers(definition);
        }

        #endregion
    }
}
