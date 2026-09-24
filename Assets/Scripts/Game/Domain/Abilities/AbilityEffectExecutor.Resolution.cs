namespace KLTN.Game.Domain
{
    public sealed partial class AbilityEffectExecutor
    {
        private AbilityCardState CaptureCard(CardInstance card)
        {
            if (card == null)
            {
                return null;
            }

            definitionsById.TryGetValue(card.DefinitionId, out CardDefinition definition);
            return AbilityCardState.Capture(card, definition);
        }

        private AbilityCardState CaptureSource(
            MatchState state,
            TriggeredAbility triggeredAbility
        )
        {
            CardInstance source = state
                .Player(triggeredAbility.ListenerOwner)
                .FindCardAnywhere(triggeredAbility.ListenerCardInstanceId);

            return CaptureCard(source);
        }

        private void RecordCardEffect(
            MatchState state,
            TriggeredAbility triggeredAbility,
            EffectKind kind,
            AbilityCardState before,
            AbilityCardState after
        )
        {
            state.RecordAbilityResolution(
                new AbilityResolutionEvent(
                    state.NextAbilityResolutionSequence(),
                    triggeredAbility.Ability.Id,
                    kind,
                    CaptureSource(state, triggeredAbility),
                    before,
                    after
                )
            );
        }

        private void RecordNexusEffect(
            MatchState state,
            TriggeredAbility triggeredAbility,
            EffectKind kind,
            SeatId targetOwner,
            int healthBefore
        )
        {
            int healthAfter = state.Player(targetOwner).NexusHealth;

            if (healthBefore == healthAfter)
            {
                return;
            }

            state.RecordAbilityResolution(
                new AbilityResolutionEvent(
                    state.NextAbilityResolutionSequence(),
                    triggeredAbility.Ability.Id,
                    kind,
                    CaptureSource(state, triggeredAbility),
                    null,
                    null,
                    targetOwner,
                    healthBefore,
                    healthAfter
                )
            );
        }
    }
}
