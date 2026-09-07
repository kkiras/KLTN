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

            RoundResolution resolution = CompleteAction(
                state,
                $"{actor} đã đánh {definition.DisplayName} vào vị trí {boardSlotIndex + 1}.");

            return CommandResult.Success(resolution);
        }

        #endregion

        #region Command Validation and Turn Progression

        public CommandResult TryPass(MatchState state, SeatId actor)
        {
            CommandResult actorValidation = ValidateActor(state, actor);

            if (!actorValidation.Accepted) { return actorValidation; }

            RoundResolution resolution = CompleteAction(
                state,
                $"{actor} đã bỏ lượt.");

            return CommandResult.Success(resolution);
        }

        private static CommandResult ValidateActor(MatchState state, SeatId actor)
        {
            if (state == null) { throw new ArgumentNullException(nameof(state)); }

            if (state.IsFinished) { return CommandResult.Reject(CommandRejectionReason.MatchFinished); }

            if (state.ActiveSeat != actor) { return CommandResult.Reject(CommandRejectionReason.NotYourTurn); }

            return CommandResult.Success();
        }

        private RoundResolution CompleteAction(
            MatchState state,
            string actionDescription)
        {
            state.LastEvent = actionDescription;
            state.ActionsCompletedInRound++;

            if (state.ActionsCompletedInRound < 2)
            {
                state.ActiveSeat = state.ActiveSeat.Opponent();
                return null;
            }

            RoundResolution resolution = ResolveBoard(state);
            UpdateOutcome(state);

            if (state.IsFinished)
            {
                state.LastEvent += " Trận đấu đã kết thúc.";
                return resolution;
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

            return resolution;
        }

        #endregion

        #region Board Resolution

        private RoundResolution ResolveBoard(MatchState state)
        {
            var steps = new List<CombatStep>();

            for (int slot = 0; slot < MatchState.BoardSlotCount; slot++)
            {
                CardInstance hostCard = state.Host.FindBoardCard(slot);
                CardInstance guestCard = state.Guest.FindBoardCard(slot);

                if (hostCard == null && guestCard == null) { continue; }

                int hostCardHealthBefore = hostCard?.CurrentHealth ?? 0;
                int guestCardHealthBefore = guestCard?.CurrentHealth ?? 0;
                int hostNexusHealthBefore = state.Host.NexusHealth;
                int guestNexusHealthBefore = state.Guest.NexusHealth;

                if (hostCard != null && guestCard != null)
                {
                    int hostDamage = DamageOf(hostCard);
                    int guestDamage = DamageOf(guestCard);

                    // Damage trong cùng một slot luôn xảy ra đồng thời.
                    hostCard.ApplyDamage(guestDamage);
                    guestCard.ApplyDamage(hostDamage);
                }
                else if (hostCard != null)
                {
                    state.Guest.ApplyNexusDamage(DamageOf(hostCard));
                }
                else
                {
                    state.Host.ApplyNexusDamage(DamageOf(guestCard));
                }

                CardCombatResolution hostResolution = BuildCardResolution(
                    hostCard,
                    SeatId.Host,
                    hostCardHealthBefore);

                CardCombatResolution guestResolution = BuildCardResolution(
                    guestCard,
                    SeatId.Guest,
                    guestCardHealthBefore);

                steps.Add(new CombatStep(
                    slot,
                    hostResolution,
                    guestResolution,
                    hostNexusHealthBefore,
                    state.Host.NexusHealth,
                    guestNexusHealthBefore,
                    state.Guest.NexusHealth));
            }

            state.Host.MoveDeadBoardCardsToGraveyard();
            state.Guest.MoveDeadBoardCardsToGraveyard();

            return new RoundResolution(state.RoundNumber, steps);
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

        private static CardCombatResolution BuildCardResolution(
            CardInstance card,
            SeatId seat,
            int healthBefore)
        {
            if (card == null) { return null; }

            return new CardCombatResolution(
                seat,
                card.InstanceId,
                healthBefore,
                card.CurrentHealth);
        }

        #endregion
    }
}
