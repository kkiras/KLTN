using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    /// <summary>
    /// A card's public state at one point in an ability's ordered resolution.
    /// Values are copied before the mutable card changes zone or health.
    /// </summary>
    public sealed class AbilityCardState
    {
        public ulong InstanceId { get; }
        public string DefinitionId { get; }
        public SeatId Owner { get; }
        public CardZone Zone { get; }
        public int BoardSlotIndex { get; }
        public int Damage { get; }
        public int Health { get; }
        public int MaximumHealth { get; }

        private AbilityCardState(CardInstance card, CardDefinition definition)
        {
            InstanceId = card.InstanceId;
            DefinitionId = card.DefinitionId;
            Owner = card.Owner;
            Zone = card.Zone;
            BoardSlotIndex = card.BoardSlotIndex;
            Damage = card.GetDamage(definition);
            Health = card.CurrentHealth;
            MaximumHealth = card.GetMaximumHealth(definition);
        }

        public static AbilityCardState Capture(CardInstance card, CardDefinition definition)
        {
            return card == null ? null : new AbilityCardState(card, definition);
        }
    }

    /// <summary>
    /// One actually applied effect. This is semantic match data, not a Unity VFX command.
    /// </summary>
    public sealed class AbilityResolutionEvent
    {
        public long Sequence { get; }
        public string AbilityId { get; }
        public EffectKind Kind { get; }
        public AbilityCardState Source { get; }
        public AbilityCardState TargetBefore { get; }
        public AbilityCardState TargetAfter { get; }
        public SeatId? NexusOwner { get; }
        public int NexusHealthBefore { get; }
        public int NexusHealthAfter { get; }
        public bool IsRoundStartPassive { get; }

        public AbilityResolutionEvent(
            long sequence,
            string abilityId,
            EffectKind kind,
            AbilityCardState source,
            AbilityCardState targetBefore,
            AbilityCardState targetAfter,
            SeatId? nexusOwner = null,
            int nexusHealthBefore = 0,
            int nexusHealthAfter = 0,
            bool isRoundStartPassive = false
        )
        {
            Sequence = sequence;
            AbilityId = abilityId;
            Kind = kind;
            Source = source;
            TargetBefore = targetBefore;
            TargetAfter = targetAfter;
            NexusOwner = nexusOwner;
            NexusHealthBefore = nexusHealthBefore;
            NexusHealthAfter = nexusHealthAfter;
            IsRoundStartPassive = isRoundStartPassive;
        }
    }

    public sealed class AbilityResolution
    {
        public IReadOnlyList<AbilityResolutionEvent> Events { get; }

        public AbilityResolution(IReadOnlyList<AbilityResolutionEvent> events)
        {
            Events = events ?? throw new ArgumentNullException(nameof(events));
        }
    }
}
