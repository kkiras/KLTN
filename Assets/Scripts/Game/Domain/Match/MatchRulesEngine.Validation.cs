using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public sealed partial class MatchRulesEngine
    {
        #region Priority Validation

        private static CommandResult ValidatePriorityActor(MatchState state, SeatId actor)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (actor != SeatId.Host && actor != SeatId.Guest)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidSeat);
            }

            if (state.IsFinished)
            {
                return CommandResult.Reject(CommandRejectionReason.MatchFinished);
            }

            if (!state.IsMulliganPhaseComplete || state.RoundNumber <= 0)
            {
                return CommandResult.Reject(CommandRejectionReason.MatchNotReady);
            }

            if (state.Phase != MatchPhase.Priority)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidPhase);
            }

            if (state.ActiveSeat != actor)
            {
                return CommandResult.Reject(CommandRejectionReason.NotYourTurn);
            }

            return CommandResult.Success();
        }

        private static CommandResult ValidateBlockActor(MatchState state, SeatId actor)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (actor != SeatId.Host && actor != SeatId.Guest)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidSeat);
            }

            if (state.IsFinished)
            {
                return CommandResult.Reject(CommandRejectionReason.MatchFinished);
            }

            if (!state.IsMulliganPhaseComplete || state.RoundNumber <= 0)
            {
                return CommandResult.Reject(CommandRejectionReason.MatchNotReady);
            }

            if (state.Phase != MatchPhase.BlockDeclaration)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidPhase);
            }

            if (state.ActiveSeat != actor)
            {
                return CommandResult.Reject(CommandRejectionReason.NotYourTurn);
            }

            if (state.AttackTokenOwner == actor)
            {
                return CommandResult.Reject(CommandRejectionReason.NotDefendingPlayer);
            }

            return CommandResult.Success();
        }

        #endregion

        private CommandRejectionReason ValidateBlockKeywords(
            CardInstance attacker,
            CardInstance blocker
        )
        {
            if (attacker == null || blocker == null)
            {
                return CommandRejectionReason.InvalidBlockDeclaration;
            }

            if (
                !definitionsById.TryGetValue(
                    attacker.DefinitionId,
                    out CardDefinition attackerDefinition
                )
                || !definitionsById.TryGetValue(
                    blocker.DefinitionId,
                    out CardDefinition blockerDefinition
                )
            )
            {
                return CommandRejectionReason.MissingCardDefinition;
            }

            bool cannotBlock =
                (blockerDefinition.Keywords & UnitKeyword.CannotBlock) != 0;

            if (cannotBlock)
            {
                return CommandRejectionReason.CardCannotBlock;
            }

            bool attackerIsFearsome =
                (attackerDefinition.Keywords & UnitKeyword.Fearsome) != 0;

            int blockerDamage = blocker.GetDamage(blockerDefinition);

            if (attackerIsFearsome && blockerDamage < FearsomeMinimumBlockerDamage)
            {
                return CommandRejectionReason.FearsomeBlockerTooWeak;
            }

            return CommandRejectionReason.None;
        }
    }
}
