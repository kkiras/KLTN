using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public sealed partial class AbilityEffectExecutor
    {
        #region Basic Effects

        private void ExecuteDamage(
            MatchState state,
            TriggeredAbility triggeredAbility,
            AbilityTargetSelection selection,
            EffectDefinition effect
        )
        {
            if (effect.Target == EffectTarget.AlliedNexus)
            {
                state
                    .Player(triggeredAbility.ListenerOwner)
                    .ApplyNexusDamage(effect.Amount);

                return;
            }

            if (effect.Target == EffectTarget.EnemyNexus)
            {
                state
                    .Player(triggeredAbility.ListenerOwner.Opponent())
                    .ApplyNexusDamage(effect.Amount);

                return;
            }

            foreach (
                CardInstance target in ResolveCardTargets(
                    state,
                    triggeredAbility,
                    selection,
                    effect
                )
            )
            {
                target.ApplyDamage(effect.Amount);
            }
        }

        private void ExecuteHeal(
            MatchState state,
            TriggeredAbility triggeredAbility,
            AbilityTargetSelection selection,
            EffectDefinition effect
        )
        {
            if (effect.Target == EffectTarget.AlliedNexus)
            {
                state.Player(triggeredAbility.ListenerOwner).HealNexus(effect.Amount);

                return;
            }

            if (effect.Target == EffectTarget.EnemyNexus)
            {
                state
                    .Player(triggeredAbility.ListenerOwner.Opponent())
                    .HealNexus(effect.Amount);

                return;
            }

            foreach (
                CardInstance target in ResolveCardTargets(
                    state,
                    triggeredAbility,
                    selection,
                    effect
                )
            )
            {
                if (
                    definitionsById.TryGetValue(
                        target.DefinitionId,
                        out CardDefinition definition
                    )
                )
                {
                    target.Heal(definition, effect.Amount);
                }
            }
        }

        private void ExecuteKill(
            MatchState state,
            TriggeredAbility triggeredAbility,
            AbilityTargetSelection selection,
            EffectDefinition effect
        )
        {
            foreach (
                CardInstance target in ResolveCardTargets(
                    state,
                    triggeredAbility,
                    selection,
                    effect
                )
            )
            {
                target.Kill();
            }
        }

        private static void ExecuteDraw(MatchState state, SeatId owner, int amount)
        {
            PlayerState player = state.Player(owner);

            for (int i = 0; i < Math.Max(0, amount); i++)
            {
                if (!player.DrawOne())
                {
                    break;
                }
            }
        }

        private void ExecuteBuff(
            MatchState state,
            TriggeredAbility triggeredAbility,
            AbilityTargetSelection selection,
            EffectDefinition effect
        )
        {
            foreach (
                CardInstance target in ResolveCardTargets(
                    state,
                    triggeredAbility,
                    selection,
                    effect
                )
            )
            {
                if (
                    !definitionsById.TryGetValue(
                        target.DefinitionId,
                        out CardDefinition definition
                    )
                )
                {
                    continue;
                }

                target.ApplyBuff(
                    definition,
                    effect.Amount,
                    effect.SecondaryAmount,
                    effect.Duration
                );
            }
        }

        private void ExecuteReduceCost(
            MatchState state,
            TriggeredAbility triggeredAbility,
            AbilityTargetSelection selection,
            EffectDefinition effect
        )
        {
            foreach (
                CardInstance target in ResolveCardTargets(
                    state,
                    triggeredAbility,
                    selection,
                    effect
                )
            )
            {
                if (
                    !definitionsById.TryGetValue(
                        target.DefinitionId,
                        out CardDefinition definition
                    )
                )
                {
                    continue;
                }

                target.ApplyCostReduction(definition, effect.Amount, effect.Duration);
            }
        }

        private void ExecuteReturnToDrawHand(
            MatchState state,
            TriggeredAbility triggeredAbility,
            AbilityTargetSelection selection,
            EffectDefinition effect
        )
        {
            foreach (
                CardInstance target in ResolveCardTargets(
                    state,
                    triggeredAbility,
                    selection,
                    effect
                )
            )
            {
                if (
                    !definitionsById.TryGetValue(
                        target.DefinitionId,
                        out CardDefinition definition
                    )
                )
                {
                    continue;
                }

                state.Player(target.Owner).TryReturnCardToDrawHand(target, definition);
            }
        }

        private void ExecuteReturnToReserve(
            MatchState state,
            TriggeredAbility triggeredAbility,
            AbilityTargetSelection selection,
            EffectDefinition effect
        )
        {
            foreach (
                CardInstance target in ResolveCardTargets(
                    state,
                    triggeredAbility,
                    selection,
                    effect
                )
            )
            {
                if (target.Zone == CardZone.Board)
                {
                    state.Player(target.Owner).TryMoveBoardCardToReserve(target);
                }
            }
        }

        private static void ExecuteKillAllUnits(MatchState state)
        {
            var targets = new List<CardInstance>();

            AddAllActiveCards(state.Host, targets);

            AddAllActiveCards(state.Guest, targets);

            foreach (CardInstance target in targets)
            {
                target.Kill();
            }
        }

        private static void ExecuteHalfNexus(
            MatchState state,
            TriggeredAbility triggeredAbility,
            EffectDefinition effect
        )
        {
            SeatId targetOwner;

            switch (effect.Target)
            {
                case EffectTarget.AlliedNexus:
                    targetOwner = triggeredAbility.ListenerOwner;
                    break;

                case EffectTarget.EnemyNexus:
                    targetOwner = triggeredAbility.ListenerOwner.Opponent();
                    break;

                default:
                    throw new InvalidOperationException(
                        "HalfNexus must target AlliedNexus " + "or EnemyNexus."
                    );
            }

            PlayerState target = state.Player(targetOwner);

            int damage = target.NexusHealth / 2;

            target.ApplyNexusDamage(damage);
        }

        #endregion
    }
}
