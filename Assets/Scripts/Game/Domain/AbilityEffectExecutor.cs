using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public sealed class AbilityEffectExecutor
    {
        private readonly IReadOnlyDictionary<string, CardDefinition> definitionsById;
        private readonly IRandomSource random;

        public AbilityEffectExecutor(
            IReadOnlyDictionary<string, CardDefinition> definitionsById,
            IRandomSource random = null
        )
        {
            this.definitionsById =
                definitionsById
                ?? throw new ArgumentNullException(nameof(definitionsById));

            this.random = random ?? new SeededRandomSource(0);
        }

        public GameEventBatch Execute(
            MatchState state,
            TriggeredAbility triggeredAbility,
            AbilityTargetSelection selection
        )
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (triggeredAbility == null)
            {
                throw new ArgumentNullException(nameof(triggeredAbility));
            }

            var emittedEvents = new List<GameEvent>();

            foreach (EffectDefinition effect in triggeredAbility.Ability.Effects)
            {
                ExecuteEffect(state, triggeredAbility, selection, effect, emittedEvents);

                GameEventBatch deathEvents = MoveDeadCardsAndBuildEvents(state);

                foreach (GameEvent deathEvent in deathEvents.Events)
                {
                    emittedEvents.Add(deathEvent);
                }
            }

            return new GameEventBatch(emittedEvents);
        }

        private void ExecuteEffect(
            MatchState state,
            TriggeredAbility triggeredAbility,
            AbilityTargetSelection selection,
            EffectDefinition effect,
            List<GameEvent> emittedEvents
        )
        {
            switch (effect.Kind)
            {
                case EffectKind.Damage:
                    ExecuteDamage(state, triggeredAbility, selection, effect);
                    return;

                case EffectKind.Heal:
                    ExecuteHeal(state, triggeredAbility, selection, effect);
                    return;

                case EffectKind.Kill:
                case EffectKind.Sacrifice:
                    ExecuteKill(state, triggeredAbility, selection, effect);
                    return;

                case EffectKind.Draw:
                    ExecuteDraw(state, triggeredAbility.ListenerOwner, effect.Amount);
                    return;

                case EffectKind.Buff:
                    ExecuteBuff(state, triggeredAbility, selection, effect);
                    return;

                case EffectKind.Revive:
                    ExecuteRevive(
                        state,
                        triggeredAbility,
                        selection,
                        effect,
                        emittedEvents
                    );
                    return;

                case EffectKind.Copy:
                    ExecuteCopy(state, triggeredAbility, selection, effect);
                    return;

                case EffectKind.ReduceCost:
                    ExecuteReduceCost(state, triggeredAbility, selection, effect);
                    return;

                case EffectKind.ReturnToDrawHand:
                    ExecuteReturnToDrawHand(state, triggeredAbility, selection, effect);
                    return;

                case EffectKind.ReturnToReserve:
                    ExecuteReturnToReserve(state, triggeredAbility, selection, effect);
                    return;

                case EffectKind.KillAllUnits:
                    ExecuteKillAllUnits(state);
                    return;

                case EffectKind.HalfNexus:
                    ExecuteHalfNexus(state, triggeredAbility, effect);
                    return;

                case EffectKind.Summon:
                    ExecuteSummon(state, triggeredAbility, effect, emittedEvents);
                    return;

                case EffectKind.CounterSpell:
                    throw new InvalidOperationException(
                        "CounterSpell requires the spell stack, "
                            + "which has not been implemented yet."
                    );

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(effect.Kind),
                        effect.Kind,
                        "Unknown effect kind."
                    );
            }
        }

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

        private void ExecuteSummon(
            MatchState state,
            TriggeredAbility triggeredAbility,
            EffectDefinition effect,
            List<GameEvent> emittedEvents
        )
        {
            if (
                string.IsNullOrWhiteSpace(effect.CardDefinitionId)
                || !definitionsById.TryGetValue(
                    effect.CardDefinitionId,
                    out CardDefinition definition
                )
            )
            {
                return;
            }

            PlayerState owner = state.Player(triggeredAbility.ListenerOwner);

            for (int i = 0; i < effect.Count; i++)
            {
                if (!owner.HasActiveRosterSpace)
                {
                    break;
                }

                var created = new CardInstance(
                    state.AllocateCardInstanceId(),
                    definition.Id,
                    owner.Seat,
                    CardZone.Reserve,
                    definition.BaseHealth
                );

                if (!owner.TryAddCreatedCardToReserve(created))
                {
                    break;
                }

                emittedEvents.Add(
                    GameEvent.FromCard(
                        GameEventType.UnitSummoned,
                        state.RoundNumber,
                        created
                    )
                );
            }
        }

        private void ExecuteCopy(
            MatchState state,
            TriggeredAbility triggeredAbility,
            AbilityTargetSelection selection,
            EffectDefinition effect
        )
        {
            PlayerState owner = state.Player(triggeredAbility.ListenerOwner);

            if (!string.IsNullOrWhiteSpace(effect.CardDefinitionId))
            {
                for (int i = 0; i < effect.Count; i++)
                {
                    TryCreateCopyInDrawHand(state, owner, effect.CardDefinitionId);
                }

                return;
            }

            if (effect.Target == EffectTarget.RandomDeadAlly)
            {
                var candidates = new List<DeathRecord>();

                foreach (DeathRecord death in state.RoundHistory.AllDeaths)
                {
                    if (
                        death.Owner == owner.Seat
                        && definitionsById.ContainsKey(death.DefinitionId)
                    )
                    {
                        candidates.Add(death);
                    }
                }

                for (int i = 0; i < effect.Count && candidates.Count > 0; i++)
                {
                    int index = random.Next(0, candidates.Count);

                    DeathRecord selected = candidates[index];

                    candidates.RemoveAt(index);

                    TryCreateCopyInDrawHand(state, owner, selected.DefinitionId);
                }

                return;
            }

            IReadOnlyList<CardInstance> targets = ResolveCardTargets(
                state,
                triggeredAbility,
                selection,
                effect
            );

            int copyCount = Math.Min(effect.Count, targets.Count);

            for (int i = 0; i < copyCount; i++)
            {
                TryCreateCopyInDrawHand(state, owner, targets[i].DefinitionId);
            }
        }

        private bool TryCreateCopyInDrawHand(
            MatchState state,
            PlayerState owner,
            string definitionId
        )
        {
            if (!definitionsById.TryGetValue(definitionId, out CardDefinition definition))
            {
                return false;
            }

            var copy = new CardInstance(
                state.AllocateCardInstanceId(),
                definition.Id,
                owner.Seat,
                CardZone.DrawHand,
                definition.BaseHealth
            );

            return owner.TryAddCreatedCardToDrawHand(copy);
        }

        private void ExecuteRevive(
            MatchState state,
            TriggeredAbility triggeredAbility,
            AbilityTargetSelection selection,
            EffectDefinition effect,
            List<GameEvent> emittedEvents
        )
        {
            PlayerState owner = state.Player(triggeredAbility.ListenerOwner);

            IReadOnlyList<CardInstance> targets = ResolveReviveTargets(
                state,
                triggeredAbility,
                selection,
                effect
            );

            foreach (CardInstance target in targets)
            {
                if (!owner.HasActiveRosterSpace)
                {
                    break;
                }

                if (
                    target.Owner != owner.Seat
                    || !definitionsById.TryGetValue(
                        target.DefinitionId,
                        out CardDefinition definition
                    )
                )
                {
                    continue;
                }

                if (!owner.TryReviveCardToReserve(target, definition))
                {
                    continue;
                }

                emittedEvents.Add(
                    GameEvent.FromCard(
                        GameEventType.UnitSummoned,
                        state.RoundNumber,
                        target
                    )
                );
            }
        }

        private IReadOnlyList<CardInstance> ResolveReviveTargets(
            MatchState state,
            TriggeredAbility triggeredAbility,
            AbilityTargetSelection selection,
            EffectDefinition effect
        )
        {
            var result = new List<CardInstance>();

            PlayerState owner = state.Player(triggeredAbility.ListenerOwner);

            if (
                effect.Target == EffectTarget.PrimarySelection
                || effect.Target == EffectTarget.SecondarySelection
            )
            {
                IReadOnlyList<CardInstance> selected = ResolveCardTargets(
                    state,
                    triggeredAbility,
                    selection,
                    effect
                );

                foreach (CardInstance card in selected)
                {
                    if (
                        card.Owner == owner.Seat
                        && card.Zone == CardZone.Graveyard
                        && owner.Graveyard.Contains(card)
                    )
                    {
                        AddUnique(card, result);
                    }

                    if (result.Count >= effect.Count)
                    {
                        break;
                    }
                }

                return result.AsReadOnly();
            }

            if (effect.Target == EffectTarget.StrongestDeadAlly)
            {
                var records = new List<DeathRecord>();

                foreach (DeathRecord record in state.RoundHistory.CurrentRoundDeaths)
                {
                    if (record.Owner != owner.Seat)
                    {
                        continue;
                    }

                    CardInstance card = owner.Graveyard.Find(item =>
                        item.InstanceId == record.CardInstanceId
                    );

                    if (card != null)
                    {
                        records.Add(record);
                    }
                }

                records.Sort(CompareStrongestDeath);

                foreach (DeathRecord record in records)
                {
                    CardInstance card = owner.Graveyard.Find(item =>
                        item.InstanceId == record.CardInstanceId
                    );

                    AddUnique(card, result);

                    if (result.Count >= effect.Count)
                    {
                        break;
                    }
                }

                return result.AsReadOnly();
            }

            if (effect.Target == EffectTarget.RandomDeadAlly)
            {
                var candidates = new List<CardInstance>();

                foreach (CardInstance card in owner.Graveyard)
                {
                    if (WasRecordedAsDead(state.RoundHistory.AllDeaths, card.InstanceId))
                    {
                        candidates.Add(card);
                    }
                }

                while (result.Count < effect.Count && candidates.Count > 0)
                {
                    int index = random.Next(0, candidates.Count);

                    AddUnique(candidates[index], result);

                    candidates.RemoveAt(index);
                }
            }

            return result.AsReadOnly();
        }

        private static int CompareStrongestDeath(DeathRecord left, DeathRecord right)
        {
            int damageComparison = right.DamageAtDeath.CompareTo(left.DamageAtDeath);

            if (damageComparison != 0)
            {
                return damageComparison;
            }

            int healthComparison = right.MaximumHealthAtDeath.CompareTo(
                left.MaximumHealthAtDeath
            );

            if (healthComparison != 0)
            {
                return healthComparison;
            }

            return right.Sequence.CompareTo(left.Sequence);
        }

        private static bool WasRecordedAsDead(
            IReadOnlyList<DeathRecord> records,
            ulong instanceId
        )
        {
            foreach (DeathRecord record in records)
            {
                if (record.CardInstanceId == instanceId)
                {
                    return true;
                }
            }

            return false;
        }

        private IReadOnlyList<CardInstance> ResolveCardTargets(
            MatchState state,
            TriggeredAbility triggeredAbility,
            AbilityTargetSelection selection,
            EffectDefinition effect
        )
        {
            var result = new List<CardInstance>();

            switch (effect.Target)
            {
                case EffectTarget.Source:
                    AddCardById(state, triggeredAbility.ListenerCardInstanceId, result);
                    break;

                case EffectTarget.TriggerSubject:
                    AddCardById(
                        state,
                        triggeredAbility.TriggerEvent.SubjectCardInstanceId,
                        result
                    );
                    break;

                case EffectTarget.PrimarySelection:
                    AddSelectedCards(state, selection?.PrimaryTargets, result);
                    break;

                case EffectTarget.SecondarySelection:
                    AddSelectedCards(state, selection?.SecondaryTargets, result);
                    break;

                case EffectTarget.AllUnits:
                    AddAllActiveCards(state.Host, result);

                    AddAllActiveCards(state.Guest, result);
                    break;

                case EffectTarget.WeakestAllies:
                    AddWeakestCards(
                        state.Player(triggeredAbility.ListenerOwner),
                        effect.Count,
                        result
                    );
                    break;

                case EffectTarget.WeakestEnemies:
                    AddWeakestCards(
                        state.Player(triggeredAbility.ListenerOwner.Opponent()),
                        effect.Count,
                        result
                    );
                    break;
            }

            return result.AsReadOnly();
        }

        private void AddWeakestCards(
            PlayerState player,
            int count,
            List<CardInstance> result
        )
        {
            var candidates = new List<CardInstance>();

            AddAllActiveCards(player, candidates);

            candidates.Sort(CompareWeakness);

            int takeCount = Math.Min(Math.Max(0, count), candidates.Count);

            for (int i = 0; i < takeCount; i++)
            {
                AddUnique(candidates[i], result);
            }
        }

        private int CompareWeakness(CardInstance left, CardInstance right)
        {
            definitionsById.TryGetValue(
                left.DefinitionId,
                out CardDefinition leftDefinition
            );

            definitionsById.TryGetValue(
                right.DefinitionId,
                out CardDefinition rightDefinition
            );

            int damageComparison = left.GetDamage(leftDefinition)
                .CompareTo(right.GetDamage(rightDefinition));

            if (damageComparison != 0)
            {
                return damageComparison;
            }

            int healthComparison = left.CurrentHealth.CompareTo(right.CurrentHealth);

            if (healthComparison != 0)
            {
                return healthComparison;
            }

            return left.InstanceId.CompareTo(right.InstanceId);
        }

        private static void AddAllActiveCards(
            PlayerState player,
            List<CardInstance> result
        )
        {
            foreach (CardInstance card in player.Reserve)
            {
                AddUnique(card, result);
            }

            foreach (CardInstance card in player.Board)
            {
                AddUnique(card, result);
            }
        }

        private static void AddSelectedCards(
            MatchState state,
            IReadOnlyList<ulong> instanceIds,
            List<CardInstance> result
        )
        {
            if (instanceIds == null)
            {
                return;
            }

            foreach (ulong instanceId in instanceIds)
            {
                AddCardById(state, instanceId, result);
            }
        }

        private static void AddCardById(
            MatchState state,
            ulong instanceId,
            List<CardInstance> result
        )
        {
            if (instanceId == 0)
            {
                return;
            }

            CardInstance card = state.Host.FindCardAnywhere(instanceId);

            if (card == null)
            {
                card = state.Guest.FindCardAnywhere(instanceId);
            }

            AddUnique(card, result);
        }

        private static void AddUnique(CardInstance card, List<CardInstance> result)
        {
            if (card == null)
            {
                return;
            }

            if (result.Exists(item => item.InstanceId == card.InstanceId))
            {
                return;
            }

            result.Add(card);
        }

        private GameEventBatch MoveDeadCardsAndBuildEvents(MatchState state)
        {
            var deadCards = new List<CardInstance>();

            AddDeadActiveCards(state.Host, deadCards);

            AddDeadActiveCards(state.Guest, deadCards);

            deadCards.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));

            var deathEvents = new List<GameEvent>();

            foreach (CardInstance deadCard in deadCards)
            {
                if (
                    !definitionsById.TryGetValue(
                        deadCard.DefinitionId,
                        out CardDefinition definition
                    )
                )
                {
                    continue;
                }

                bool moved = state
                    .Player(deadCard.Owner)
                    .TryMoveDeadActiveCardToGraveyard(deadCard);

                if (!moved)
                {
                    continue;
                }

                int damageAtDeath = deadCard.GetDamage(definition);

                int maximumHealthAtDeath = deadCard.GetMaximumHealth(definition);

                state.RoundHistory.RecordDeath(
                    deadCard,
                    damageAtDeath,
                    maximumHealthAtDeath
                );

                deathEvents.Add(
                    GameEvent.FromCard(
                        GameEventType.UnitDied,
                        state.RoundNumber,
                        deadCard
                    )
                );
            }

            return new GameEventBatch(deathEvents);
        }

        private static void AddDeadActiveCards(
            PlayerState player,
            List<CardInstance> result
        )
        {
            foreach (CardInstance card in player.Reserve)
            {
                if (card.IsDead)
                {
                    result.Add(card);
                }
            }

            foreach (CardInstance card in player.Board)
            {
                if (card.IsDead)
                {
                    result.Add(card);
                }
            }
        }
    }
}
