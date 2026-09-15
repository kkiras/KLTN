using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    /// <summary>
    /// Central policy for enumerating and validating ability targets against authoritative
    /// ownership, relation, zone, count and source-exclusion constraints.
    /// </summary>
    public sealed class AbilityTargetValidator
    {
        #region Validation

        /// <summary>
        /// Determines whether every requirement has enough candidates before a
        /// targeted ability is opened for player input.
        /// </summary>
        public bool HasEnoughValidTargets(
            MatchState state,
            SeatId abilityOwner,
            ulong sourceCardInstanceId,
            IReadOnlyList<AbilityTargetRequirement> requirements
        )
        {
            if (requirements == null)
            {
                return true;
            }

            foreach (AbilityTargetRequirement requirement in requirements)
            {
                IReadOnlyList<CardInstance> validTargets = GetValidTargets(
                    state,
                    abilityOwner,
                    sourceCardInstanceId,
                    requirement
                );

                if (validTargets.Count < requirement.Count)
                {
                    return false;
                }
            }

            return true;
        }

        public bool Validate(
            MatchState state,
            PendingAbilitySelection pending,
            AbilityTargetSelection selection
        )
        {
            if (pending == null || selection == null)
            {
                return false;
            }

            return Validate(
                state,
                pending.ChoosingSeat,
                pending.SourceCardInstanceId,
                pending.Ability.RequiredTargets,
                selection
            );
        }

        public bool Validate(
            MatchState state,
            SeatId abilityOwner,
            ulong sourceCardInstanceId,
            IReadOnlyList<AbilityTargetRequirement> requirements,
            AbilityTargetSelection selection
        )
        {
            if (state == null || requirements == null || selection == null)
            {
                return false;
            }

            var allSelectedIds = new HashSet<ulong>();

            foreach (AbilityTargetRequirement requirement in requirements)
            {
                IReadOnlyList<ulong> selectedIds = selection.GetTargets(requirement.Slot);

                if (selectedIds.Count != requirement.Count)
                {
                    return false;
                }

                IReadOnlyList<CardInstance> validTargets = GetValidTargets(
                    state,
                    abilityOwner,
                    sourceCardInstanceId,
                    requirement
                );

                foreach (ulong selectedId in selectedIds)
                {
                    if (!allSelectedIds.Add(selectedId))
                    {
                        return false;
                    }

                    bool found = false;

                    foreach (CardInstance validTarget in validTargets)
                    {
                        if (validTarget.InstanceId == selectedId)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion

        #region Target Enumeration

        /// <summary>
        /// Returns a deterministic, instance-ID ordered list of legal targets for
        /// one requirement. The authoritative host uses the same enumeration for
        /// availability checks and final selection validation.
        /// </summary>
        public IReadOnlyList<CardInstance> GetValidTargets(
            MatchState state,
            SeatId abilityOwner,
            ulong sourceCardInstanceId,
            AbilityTargetRequirement requirement
        )
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (requirement == null)
            {
                throw new ArgumentNullException(nameof(requirement));
            }

            var result = new List<CardInstance>();

            switch (requirement.Relation)
            {
                case TargetRelation.Ally:
                    AddPlayerTargets(
                        state.Player(abilityOwner),
                        sourceCardInstanceId,
                        requirement,
                        result
                    );
                    break;

                case TargetRelation.Enemy:
                    AddPlayerTargets(
                        state.Player(abilityOwner.Opponent()),
                        sourceCardInstanceId,
                        requirement,
                        result
                    );
                    break;

                case TargetRelation.Any:
                    AddPlayerTargets(
                        state.Host,
                        sourceCardInstanceId,
                        requirement,
                        result
                    );

                    AddPlayerTargets(
                        state.Guest,
                        sourceCardInstanceId,
                        requirement,
                        result
                    );
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            result.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));

            return result.AsReadOnly();
        }

        #endregion

        #region Target Collection Helpers

        private static void AddPlayerTargets(
            PlayerState player,
            ulong sourceCardInstanceId,
            AbilityTargetRequirement requirement,
            List<CardInstance> result
        )
        {
            AddZone(
                player.DrawHand,
                AbilityTargetZone.DrawHand,
                sourceCardInstanceId,
                requirement,
                result
            );

            AddZone(
                player.Reserve,
                AbilityTargetZone.Reserve,
                sourceCardInstanceId,
                requirement,
                result
            );

            AddZone(
                player.Board,
                AbilityTargetZone.Board,
                sourceCardInstanceId,
                requirement,
                result
            );

            AddZone(
                player.Graveyard,
                AbilityTargetZone.Graveyard,
                sourceCardInstanceId,
                requirement,
                result
            );

            AddZone(
                player.Deck,
                AbilityTargetZone.Deck,
                sourceCardInstanceId,
                requirement,
                result
            );
        }

        private static void AddZone(
            IReadOnlyList<CardInstance> cards,
            AbilityTargetZone zone,
            ulong sourceCardInstanceId,
            AbilityTargetRequirement requirement,
            List<CardInstance> result
        )
        {
            if ((requirement.Zones & zone) == 0)
            {
                return;
            }

            foreach (CardInstance card in cards)
            {
                if (requirement.ExcludeSource && card.InstanceId == sourceCardInstanceId)
                {
                    continue;
                }

                if (card.Zone != CardZone.Graveyard && card.IsDead)
                {
                    continue;
                }

                result.Add(card);
            }
        }

        #endregion
    }
}
