using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public sealed class PendingAbilitySelection
    {
        public ulong RequestId { get; }

        public SeatId ChoosingSeat { get; }
        public ulong SourceCardInstanceId { get; }

        public AbilityDefinition Ability { get; }
        public GameEvent TriggerEvent { get; }

        public MatchPhase ResumePhase { get; }
        public bool CanCancel { get; }

        public PendingAbilitySelection(
            ulong requestId,
            TriggeredAbility triggeredAbility,
            MatchPhase resumePhase,
            bool canCancel = false
        )
        {
            if (requestId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestId));
            }

            if (triggeredAbility == null)
            {
                throw new ArgumentNullException(nameof(triggeredAbility));
            }

            if (triggeredAbility.Ability.RequiredTargets.Count == 0)
            {
                throw new ArgumentException(
                    "Ability does not require target selection.",
                    nameof(triggeredAbility)
                );
            }

            RequestId = requestId;
            ChoosingSeat = triggeredAbility.ListenerOwner;
            SourceCardInstanceId = triggeredAbility.ListenerCardInstanceId;
            Ability = triggeredAbility.Ability;
            TriggerEvent = triggeredAbility.TriggerEvent;
            ResumePhase = resumePhase;
            CanCancel = canCancel;
        }
    }

    public sealed class AbilityTargetSelection
    {
        public IReadOnlyList<ulong> PrimaryTargets { get; }

        public IReadOnlyList<ulong> SecondaryTargets { get; }

        public AbilityTargetSelection(
            IReadOnlyList<ulong> primaryTargets,
            IReadOnlyList<ulong> secondaryTargets
        )
        {
            PrimaryTargets = Copy(primaryTargets);

            SecondaryTargets = Copy(secondaryTargets);
        }

        public IReadOnlyList<ulong> GetTargets(AbilityTargetSlot slot)
        {
            switch (slot)
            {
                case AbilityTargetSlot.Primary:
                    return PrimaryTargets;

                case AbilityTargetSlot.Secondary:
                    return SecondaryTargets;

                default:
                    throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        private static IReadOnlyList<ulong> Copy(IReadOnlyList<ulong> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<ulong>();
            }

            var copy = new ulong[source.Count];

            for (int i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return Array.AsReadOnly(copy);
        }
    }
}
