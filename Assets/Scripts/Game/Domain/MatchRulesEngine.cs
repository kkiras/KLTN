using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public sealed class MatchRulesEngine
    {
        #region Constants

        private const int MaximumMana = 10;
        private const int DrawIntervalRounds = 2;

        #endregion

        #region Fields

        private readonly IReadOnlyDictionary<string, CardDefinition>
            definitionsById;

        #endregion

        #region Construction

        public MatchRulesEngine(IReadOnlyDictionary<string, CardDefinition> definitionsById)
        {
            this.definitionsById = definitionsById ??
                throw new ArgumentNullException(nameof(definitionsById));
        }

        #endregion

        #region Commands

        public CommandResult TryPlayUnit(MatchState state, SeatId actor, ulong cardInstanceId, int boardSlotIndex)
        {
            CommandResult actorValidation = ValidateActor(state, actor);

            if (!actorValidation.Accepted) { return actorValidation; }

            if (boardSlotIndex < 0 ||
                boardSlotIndex >= MatchState.BoardSlotCount)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidBoardSlot);
            }

            PlayerState player = state.Player(actor);

            if (player.Board.Count >= MatchState.BoardSlotCount) { return CommandResult.Reject(CommandRejectionReason.BoardFull); }

            if (player.FindBoardCard(boardSlotIndex) != null) { return CommandResult.Reject(CommandRejectionReason.BoardSlotOccupied); }

            CardInstance card = player.FindCardInHand(cardInstanceId);

            if (card == null) { return CommandResult.Reject(CommandRejectionReason.CardNotInHand); }

            if (!definitionsById.TryGetValue(card.DefinitionId, out CardDefinition definition))
            {
                return CommandResult.Reject(CommandRejectionReason.MissingCardDefinition);
            }

            // The server always reads cost from the trusted definition.
            // A client never supplies its own cost value.
            if (!player.TrySpendMana(definition.Cost)) { return CommandResult.Reject(CommandRejectionReason.InsufficientMana); }

            player.MoveHandCardToBoard(card, boardSlotIndex);
            CompleteAction(state, $"{actor} đã đánh {definition.DisplayName} vào vị trí {boardSlotIndex + 1}.");
            return CommandResult.Success();
        }

        #endregion

        #region Command Validation and Turn Progression

        public CommandResult TryPass(MatchState state, SeatId actor)
        {
            CommandResult actorValidation = ValidateActor(state, actor);

            if (!actorValidation.Accepted) { return actorValidation; }

            CompleteAction(state, $"{actor} đã bỏ lượt.");
            return CommandResult.Success();
        }

        private static CommandResult ValidateActor(MatchState state, SeatId actor)
        {
            if (state == null) { throw new ArgumentNullException(nameof(state)); }

            if (state.IsFinished) { return CommandResult.Reject(CommandRejectionReason.MatchFinished); }

            if (state.ActiveSeat != actor) { return CommandResult.Reject(CommandRejectionReason.NotYourTurn); }

            return CommandResult.Success();
        }

        private void CompleteAction(MatchState state, string actionDescription)
        {
            state.LastEvent = actionDescription;
            state.ActionsCompletedInRound++;

            if (state.ActionsCompletedInRound < 2)
            {
                state.ActiveSeat = state.ActiveSeat.Opponent();
                return;
            }

            ResolveBoard(state);
            UpdateOutcome(state);

            if (state.IsFinished)
            {
                state.LastEvent += " Trận đấu đã kết thúc.";
                return;
            }

            int completedRound = state.RoundNumber;

            if (completedRound % DrawIntervalRounds == 0)
            {
                state.Host.DrawOne();
                state.Guest.DrawOne();
            }

            state.Host.AdvanceAndRefillMana(MaximumMana);
            state.Guest.AdvanceAndRefillMana(MaximumMana);
            state.RoundNumber = completedRound + 1;
            state.ActionsCompletedInRound = 0;
            state.ActiveSeat = state.FirstSeat;
        }

        #endregion

        #region Board Resolution

        private void ResolveBoard(MatchState state)
        {
            for (int slot = 0;
                 slot < MatchState.BoardSlotCount;
                 slot++)
            {
                CardInstance hostCard = state.Host.FindBoardCard(slot);
                CardInstance guestCard = state.Guest.FindBoardCard(slot);

                if (hostCard != null && guestCard != null)
                {
                    int hostDamage = DamageOf(hostCard);
                    int guestDamage = DamageOf(guestCard);

                    // Both units deal damage simultaneously.
                    hostCard.ApplyDamage(guestDamage);
                    guestCard.ApplyDamage(hostDamage);
                    continue;
                }

                if (hostCard != null) { state.Guest.ApplyNexusDamage(DamageOf(hostCard)); }

                if (guestCard != null) { state.Host.ApplyNexusDamage(DamageOf(guestCard)); }
            }

            state.Host.MoveDeadBoardCardsToGraveyard();
            state.Guest.MoveDeadBoardCardsToGraveyard();
        }

        #endregion

        #region Helpers

        private int DamageOf(CardInstance card)
        {
            if (!definitionsById.TryGetValue(card.DefinitionId, out CardDefinition definition)) { return 0; }

            return definition.BaseDamage;
        }

        private static void UpdateOutcome(MatchState state)
        {
            bool hostDead = state.Host.NexusHealth <= 0;
            bool guestDead = state.Guest.NexusHealth <= 0;

            if (hostDead && guestDead) { state.Outcome = MatchOutcome.Draw; }
            else if (hostDead)
            {
                state.Outcome = MatchOutcome.GuestWon;
            }
            else if (guestDead)
            {
                state.Outcome = MatchOutcome.HostWon;
            }
        }

        #endregion
    }
}
