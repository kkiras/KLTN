using System;
using System.Collections.Generic;
using KLTN.Game.Domain;

namespace KLTN.Game.Networking
{
    public sealed class RoundResolutionDtoMapper
    {
        #region Fields

        private readonly IReadOnlyDictionary<string, CardDefinition> definitionsById;

        #endregion

        #region Construction

        public RoundResolutionDtoMapper(
            IReadOnlyDictionary<string, CardDefinition> definitionsById)
        {
            this.definitionsById = definitionsById ??
                throw new ArgumentNullException(nameof(definitionsById));
        }

        #endregion

        #region Mapping

        public RoundResolutionDto Build(
            RoundResolution resolution,
            MatchState state)
        {
            if (resolution == null) { return null; }
            if (state == null) { throw new ArgumentNullException(nameof(state)); }

            var steps = new CombatStepDto[resolution.Steps.Count];

            for (int i = 0; i < resolution.Steps.Count; i++)
            {
                CombatStep source = resolution.Steps[i];

                steps[i] = new CombatStepDto
                {
                    slotIndex = source.SlotIndex,
                    hostCard = BuildCard(source.HostCard, state, source.SlotIndex),
                    guestCard = BuildCard(source.GuestCard, state, source.SlotIndex),

                    hostNexusHealthBefore = source.HostNexusHealthBefore,
                    hostNexusHealthAfter = source.HostNexusHealthAfter,
                    guestNexusHealthBefore = source.GuestNexusHealthBefore,
                    guestNexusHealthAfter = source.GuestNexusHealthAfter
                };
            }

            return new RoundResolutionDto
            {
                roundNumber = resolution.RoundNumber,
                steps = steps
            };
        }

        private ResolvedCardDto BuildCard(
            CardCombatResolution resolution,
            MatchState state,
            int slotIndex)
        {
            if (resolution == null) { return null; }

            PlayerState player = state.Player(resolution.Seat);
            CardInstance instance = FindCombatCard(
                player,
                resolution.CardInstanceId);

            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Cannot map combat card {resolution.CardInstanceId}.");
            }

            definitionsById.TryGetValue(
                instance.DefinitionId,
                out CardDefinition definition);

            return new ResolvedCardDto
            {
                seat = (int)resolution.Seat,
                healthAfter = resolution.HealthAfter,

                cardBefore = new CardViewDto
                {
                    instanceId = instance.InstanceId.ToString(),
                    definitionId = instance.DefinitionId,
                    displayName = definition?.DisplayName ?? "Unknown card",

                    health = resolution.HealthBefore,
                    damage = definition?.BaseDamage ?? 0,
                    energy = definition?.Cost ?? 0,
                    boardSlotIndex = slotIndex
                }
            };
        }

        #endregion

        #region Card Lookup

        private static CardInstance FindCombatCard(
            PlayerState player,
            ulong instanceId)
        {
            CardInstance card = FindCard(player.Board, instanceId);

            if (card != null) { return card; }

            return FindCard(player.Graveyard, instanceId);
        }

        private static CardInstance FindCard(
            IReadOnlyList<CardInstance> cards,
            ulong instanceId)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].InstanceId == instanceId) { return cards[i]; }
            }

            return null;
        }

        #endregion
    }
}