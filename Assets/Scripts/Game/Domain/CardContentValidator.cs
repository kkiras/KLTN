using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public static class CardContentValidator
    {
        public static void Validate(IReadOnlyList<CardDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0)
            {
                throw new InvalidOperationException(
                    "At least one card definition is required."
                );
            }

            var definitionIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (CardDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new InvalidOperationException(
                        "Card catalog contains a null definition."
                    );
                }

                if (!IsStableAsciiId(definition.Id))
                {
                    throw new InvalidOperationException(
                        $"Card ID '{definition.Id}' is not a stable ASCII ID."
                    );
                }

                if (!definitionIds.Add(definition.Id))
                {
                    throw new InvalidOperationException(
                        $"Duplicate card definition ID '{definition.Id}'."
                    );
                }
            }

            foreach (CardDefinition definition in definitions)
            {
                ValidateDefinition(definition, definitionIds);
            }
        }

        private static void ValidateDefinition(
            CardDefinition definition,
            HashSet<string> definitionIds
        )
        {
            if (definition.BaseHealth <= 0)
            {
                throw new InvalidOperationException(
                    $"Unit '{definition.Id}' must have positive health."
                );
            }

            if (definition.BaseDamage < 0 || definition.Cost < 0)
            {
                throw new InvalidOperationException(
                    $"Unit '{definition.Id}' contains a negative stat."
                );
            }

            UnitPassiveRules passive = definition.PassiveRules;

            if (passive.PowerAndHealthPerDeath > 0 && !passive.RevivesAtRoundStart)
            {
                throw new InvalidOperationException(
                    $"Unit '{definition.Id}' gains stats per death "
                        + "but does not revive at RoundStart."
                );
            }

            if (passive.RequiresEphemeralAttacker && !passive.JoinsAttackFromGraveyard)
            {
                throw new InvalidOperationException(
                    $"Unit '{definition.Id}' requires an Ephemeral "
                        + "attacker but cannot join from the graveyard."
                );
            }

            var abilityIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (AbilityDefinition ability in definition.Abilities)
            {
                if (!IsStableAsciiId(ability.Id))
                {
                    throw new InvalidOperationException(
                        $"Ability ID '{ability.Id}' on card "
                            + $"'{definition.Id}' is invalid."
                    );
                }

                if (!abilityIds.Add(ability.Id))
                {
                    throw new InvalidOperationException(
                        $"Card '{definition.Id}' contains duplicate "
                            + $"ability ID '{ability.Id}'."
                    );
                }

                ValidateAbility(definition, ability, definitionIds);
            }
        }

        private static void ValidateAbility(
            CardDefinition definition,
            AbilityDefinition ability,
            HashSet<string> definitionIds
        )
        {
            var declaredSlots = new HashSet<AbilityTargetSlot>();

            foreach (AbilityTargetRequirement requirement in ability.RequiredTargets)
            {
                if (!declaredSlots.Add(requirement.Slot))
                {
                    throw new InvalidOperationException(
                        $"Ability '{ability.Id}' on card "
                            + $"'{definition.Id}' declares target slot "
                            + $"'{requirement.Slot}' more than once."
                    );
                }
            }

            foreach (EffectDefinition effect in ability.Effects)
            {
                ValidateEffect(definition, ability, effect, declaredSlots, definitionIds);
            }
        }

        private static void ValidateEffect(
            CardDefinition definition,
            AbilityDefinition ability,
            EffectDefinition effect,
            HashSet<AbilityTargetSlot> declaredSlots,
            HashSet<string> definitionIds
        )
        {
            if (effect.Kind == EffectKind.CounterSpell)
            {
                throw new InvalidOperationException(
                    $"Ability '{ability.Id}' on card '{definition.Id}' "
                        + "uses CounterSpell before the spell stack "
                        + "has been implemented."
                );
            }

            if (effect.Target == EffectTarget.None)
            {
                throw new InvalidOperationException(
                    $"Ability '{ability.Id}' on card '{definition.Id}' "
                        + "contains an effect without a target."
                );
            }

            if (
                effect.Target == EffectTarget.PrimarySelection
                && !declaredSlots.Contains(AbilityTargetSlot.Primary)
            )
            {
                throw new InvalidOperationException(
                    $"Ability '{ability.Id}' uses PrimarySelection "
                        + "without a Primary target requirement."
                );
            }

            if (
                effect.Target == EffectTarget.SecondarySelection
                && !declaredSlots.Contains(AbilityTargetSlot.Secondary)
            )
            {
                throw new InvalidOperationException(
                    $"Ability '{ability.Id}' uses SecondarySelection "
                        + "without a Secondary target requirement."
                );
            }

            switch (effect.Kind)
            {
                case EffectKind.Damage:
                case EffectKind.Heal:
                case EffectKind.Draw:
                case EffectKind.ReduceCost:
                    RequirePositiveAmount(definition, ability, effect);
                    break;

                case EffectKind.Buff:
                    if (effect.Amount == 0 && effect.SecondaryAmount == 0)
                    {
                        throw new InvalidOperationException(
                            $"Buff effect '{ability.Id}' on card "
                                + $"'{definition.Id}' has no stat change."
                        );
                    }

                    break;

                case EffectKind.Summon:
                    ValidateReferencedCard(
                        definition,
                        ability,
                        effect,
                        definitionIds,
                        requireReference: true
                    );
                    break;

                case EffectKind.Copy:
                    ValidateReferencedCard(
                        definition,
                        ability,
                        effect,
                        definitionIds,
                        requireReference: false
                    );
                    break;
            }
        }

        private static void RequirePositiveAmount(
            CardDefinition definition,
            AbilityDefinition ability,
            EffectDefinition effect
        )
        {
            if (effect.Amount > 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Effect '{effect.Kind}' in ability '{ability.Id}' "
                    + $"on card '{definition.Id}' requires "
                    + "a positive amount."
            );
        }

        private static void ValidateReferencedCard(
            CardDefinition definition,
            AbilityDefinition ability,
            EffectDefinition effect,
            HashSet<string> definitionIds,
            bool requireReference
        )
        {
            bool hasReference = !string.IsNullOrWhiteSpace(effect.CardDefinitionId);

            if (requireReference && !hasReference)
            {
                throw new InvalidOperationException(
                    $"Ability '{ability.Id}' on card '{definition.Id}' "
                        + "requires a card definition ID."
                );
            }

            if (hasReference && !definitionIds.Contains(effect.CardDefinitionId))
            {
                throw new InvalidOperationException(
                    $"Ability '{ability.Id}' on card '{definition.Id}' "
                        + $"references unknown card "
                        + $"'{effect.CardDefinitionId}'."
                );
            }
        }

        private static bool IsStableAsciiId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (char character in value)
            {
                bool valid =
                    character >= 'a' && character <= 'z'
                    || character >= 'A' && character <= 'Z'
                    || character >= '0' && character <= '9'
                    || character == '_'
                    || character == '-';

                if (!valid)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
