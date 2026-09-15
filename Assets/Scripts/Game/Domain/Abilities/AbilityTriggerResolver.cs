using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public sealed class TriggeredAbility
    {
        #region Properties

        public ulong ListenerCardInstanceId { get; }
        public SeatId ListenerOwner { get; }

        public AbilityDefinition Ability { get; }
        public GameEvent TriggerEvent { get; }

        #endregion

        #region Construction

        public TriggeredAbility(
            ulong listenerCardInstanceId,
            SeatId listenerOwner,
            AbilityDefinition ability,
            GameEvent triggerEvent
        )
        {
            Ability = ability ?? throw new ArgumentNullException(nameof(ability));

            TriggerEvent =
                triggerEvent ?? throw new ArgumentNullException(nameof(triggerEvent));

            ListenerCardInstanceId = listenerCardInstanceId;

            ListenerOwner = listenerOwner;
        }

        #endregion
    }

    /// <summary>
    /// Converts ordered domain events into eligible data-authored abilities without
    /// branching on card identity.
    /// </summary>
    public sealed class AbilityTriggerResolver
    {
        #region Dependencies

        private readonly IReadOnlyDictionary<string, CardDefinition> definitionsById;

        #endregion

        #region Construction

        public AbilityTriggerResolver(
            IReadOnlyDictionary<string, CardDefinition> definitionsById
        )
        {
            this.definitionsById =
                definitionsById
                ?? throw new ArgumentNullException(nameof(definitionsById));
        }

        #endregion

        #region Trigger Collection

        public IReadOnlyList<TriggeredAbility> Collect(
            MatchState state,
            GameEventBatch batch
        )
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            var result = new List<TriggeredAbility>();

            foreach (GameEvent gameEvent in batch.Events)
            {
                CollectEvent(state, gameEvent, result);
            }

            return result.AsReadOnly();
        }

        #endregion

        #region Event Routing

        private void CollectEvent(
            MatchState state,
            GameEvent gameEvent,
            List<TriggeredAbility> result
        )
        {
            switch (gameEvent.Type)
            {
                case GameEventType.UnitPlayed:
                    AddSourceAbilities(
                        state,
                        gameEvent,
                        AbilityTrigger.Play,
                        requireActiveRoster: false,
                        result
                    );
                    break;

                case GameEventType.UnitSummoned:
                    AddSourceAbilities(
                        state,
                        gameEvent,
                        AbilityTrigger.Summon,
                        requireActiveRoster: true,
                        result
                    );

                    AddAlliedListeners(
                        state,
                        gameEvent,
                        AbilityTrigger.AllySummon,
                        result
                    );
                    break;

                case GameEventType.AttackDeclared:
                    AddSourceAbilities(
                        state,
                        gameEvent,
                        AbilityTrigger.Attack,
                        requireActiveRoster: true,
                        result
                    );
                    break;

                case GameEventType.UnitSupported:
                    AddSourceAbilities(
                        state,
                        gameEvent,
                        AbilityTrigger.Support,
                        requireActiveRoster: true,
                        result
                    );
                    break;

                case GameEventType.UnitDied:
                    AddSourceAbilities(
                        state,
                        gameEvent,
                        AbilityTrigger.Death,
                        requireActiveRoster: false,
                        result
                    );

                    AddAlliedListeners(
                        state,
                        gameEvent,
                        AbilityTrigger.AllyDeath,
                        result
                    );
                    break;

                case GameEventType.RoundStarted:
                    AddRoundStartListeners(state, gameEvent, result);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        #region Listener Resolution

        private void AddSourceAbilities(
            MatchState state,
            GameEvent gameEvent,
            AbilityTrigger trigger,
            bool requireActiveRoster,
            List<TriggeredAbility> result
        )
        {
            if (!gameEvent.SourceOwner.HasValue || gameEvent.SourceCardInstanceId == 0)
            {
                return;
            }

            CardInstance source = state
                .Player(gameEvent.SourceOwner.Value)
                .FindCardAnywhere(gameEvent.SourceCardInstanceId);

            if (source == null)
            {
                return;
            }

            if (requireActiveRoster && !source.IsInActiveRoster)
            {
                return;
            }

            AddMatchingAbilities(state, source, trigger, gameEvent, result);
        }

        private void AddAlliedListeners(
            MatchState state,
            GameEvent gameEvent,
            AbilityTrigger trigger,
            List<TriggeredAbility> result
        )
        {
            if (!gameEvent.SubjectOwner.HasValue)
            {
                return;
            }

            PlayerState owner = state.Player(gameEvent.SubjectOwner.Value);

            List<CardInstance> activeCards = GetStableActiveCards(owner);

            foreach (CardInstance listener in activeCards)
            {
                // AllySummon/AllyDeath does not mean the subject itself.
                if (listener.InstanceId == gameEvent.SubjectCardInstanceId)
                {
                    continue;
                }

                AddMatchingAbilities(state, listener, trigger, gameEvent, result);
            }
        }

        private void AddRoundStartListeners(
            MatchState state,
            GameEvent gameEvent,
            List<TriggeredAbility> result
        )
        {
            AddRoundStartListenersForPlayer(state, state.Host, gameEvent, result);

            AddRoundStartListenersForPlayer(state, state.Guest, gameEvent, result);
        }

        private void AddRoundStartListenersForPlayer(
            MatchState state,
            PlayerState player,
            GameEvent gameEvent,
            List<TriggeredAbility> result
        )
        {
            foreach (CardInstance listener in GetStableActiveCards(player))
            {
                AddMatchingAbilities(
                    state,
                    listener,
                    AbilityTrigger.RoundStart,
                    gameEvent,
                    result
                );
            }
        }

        private void AddMatchingAbilities(
            MatchState state,
            CardInstance listener,
            AbilityTrigger trigger,
            GameEvent gameEvent,
            List<TriggeredAbility> result
        )
        {
            if (
                !definitionsById.TryGetValue(
                    listener.DefinitionId,
                    out CardDefinition definition
                )
            )
            {
                return;
            }

            foreach (AbilityDefinition ability in definition.Abilities)
            {
                if (ability.Trigger != trigger)
                {
                    continue;
                }

                if (
                    !AbilityConditionEvaluator.IsSatisfied(
                        state,
                        listener.Owner,
                        ability.Condition
                    )
                )
                {
                    continue;
                }

                if (
                    ability.OncePerRound
                    && !state.RoundHistory.TryMarkAbilityUsed(
                        listener.InstanceId,
                        ability.Id
                    )
                )
                {
                    continue;
                }

                if (
                    ability.OncePerMatch
                    && !state.RoundHistory.TryMarkAbilityUsedThisMatch(
                        listener.InstanceId,
                        ability.Id
                    )
                )
                {
                    continue;
                }

                result.Add(
                    new TriggeredAbility(
                        listener.InstanceId,
                        listener.Owner,
                        ability,
                        gameEvent
                    )
                );
            }
        }

        private static List<CardInstance> GetStableActiveCards(PlayerState player)
        {
            var result = new List<CardInstance>();

            result.AddRange(player.Reserve);
            result.AddRange(player.Board);

            result.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));

            return result;
        }

        #endregion
    }

    public static class AbilityConditionEvaluator
    {
        #region Evaluation

        public static bool IsSatisfied(
            MatchState state,
            SeatId abilityOwner,
            AbilityCondition condition
        )
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            switch (condition)
            {
                case AbilityCondition.None:
                    return true;

                case AbilityCondition.AllyDiedThisRound:
                    return state.RoundHistory.HasDeathThisRound(abilityOwner);

                case AbilityCondition.AllyDiedPreviousRound:
                    return state.RoundHistory.HasDeathPreviousRound(abilityOwner);

                default:
                    throw new ArgumentOutOfRangeException(nameof(condition));
            }
        }

        #endregion
    }

    /// <summary>
    /// FIFO queue that preserves deterministic trigger order and supports bounded
    /// resolution by the rules engine.
    /// </summary>
    public sealed class TriggeredAbilityQueue
    {
        #region Dependencies and State

        private readonly AbilityTriggerResolver resolver;

        private readonly Queue<TriggeredAbility> queue = new Queue<TriggeredAbility>();

        public int Count => queue.Count;

        #endregion

        #region Construction

        public TriggeredAbilityQueue(AbilityTriggerResolver resolver)
        {
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        #endregion

        #region Queue Operations

        public IReadOnlyList<TriggeredAbility> Enqueue(
            MatchState state,
            GameEventBatch batch
        )
        {
            IReadOnlyList<TriggeredAbility> triggered = resolver.Collect(state, batch);

            foreach (TriggeredAbility ability in triggered)
            {
                queue.Enqueue(ability);
            }

            return triggered;
        }

        public bool TryPeek(out TriggeredAbility ability)
        {
            if (queue.Count == 0)
            {
                ability = null;
                return false;
            }

            ability = queue.Peek();
            return true;
        }

        public bool TryDequeue(out TriggeredAbility ability)
        {
            if (queue.Count == 0)
            {
                ability = null;
                return false;
            }

            ability = queue.Dequeue();
            return true;
        }

        public void Clear()
        {
            queue.Clear();
        }

        #endregion
    }
}
