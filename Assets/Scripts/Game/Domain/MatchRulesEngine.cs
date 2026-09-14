using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public sealed class MatchRulesEngine
    {
        #region Constants

        private const int MaximumMana = 10;
        private const int DrawIntervalRounds = 2;
        private const int FearsomeMinimumBlockerDamage = 3;

        #endregion

        #region Fields

        private readonly IReadOnlyDictionary<string, CardDefinition> definitionsById;

        private readonly AbilityTargetValidator abilityTargetValidator;

        private readonly TriggeredAbilityQueue triggeredAbilityQueue;

        private readonly AbilityEffectExecutor abilityEffectExecutor;

        private readonly UnitPassiveRuleResolver unitPassiveRuleResolver;

        #endregion

        #region Construction

        public MatchRulesEngine(
            IReadOnlyDictionary<string, CardDefinition> definitionsById,
            IRandomSource random = null
        )
        {
            this.definitionsById =
                definitionsById
                ?? throw new ArgumentNullException(nameof(definitionsById));

            abilityTargetValidator = new AbilityTargetValidator();

            var triggerResolver = new AbilityTriggerResolver(definitionsById);

            triggeredAbilityQueue = new TriggeredAbilityQueue(triggerResolver);

            abilityEffectExecutor = new AbilityEffectExecutor(definitionsById, random);

            unitPassiveRuleResolver = new UnitPassiveRuleResolver(definitionsById);
        }

        #endregion

        #region Commands

        /// <summary>
        /// Temporary compatibility command.
        /// Part 3 will replace this with DrawHand -> Reserve summon.
        /// </summary>
        public CommandResult TrySummonUnit(
            MatchState state,
            SeatId actor,
            ulong cardInstanceId
        )
        {
            CommandResult actorValidation = ValidatePriorityActor(state, actor);

            if (!actorValidation.Accepted)
            {
                return actorValidation;
            }

            PlayerState player = state.Player(actor);

            CardInstance card = player.FindCardInDrawHand(cardInstanceId);

            if (card == null)
            {
                return CommandResult.Reject(CommandRejectionReason.CardNotInHand);
            }

            if (
                !definitionsById.TryGetValue(
                    card.DefinitionId,
                    out CardDefinition definition
                )
            )
            {
                return CommandResult.Reject(CommandRejectionReason.MissingCardDefinition);
            }

            if (!player.HasActiveRosterSpace)
            {
                return CommandResult.Reject(CommandRejectionReason.ActiveRosterFull);
            }

            int manaCost = CardCostCalculator.GetEffectiveCost(state, card, definition);

            if (player.Mana < manaCost)
            {
                return CommandResult.Reject(CommandRejectionReason.InsufficientMana);
            }

            AbilityDefinition targetedPlayAbility = FindTargetedPlayAbility(
                state,
                card,
                definition
            );

            if (targetedPlayAbility != null)
            {
                bool hasTargets = abilityTargetValidator.HasEnoughValidTargets(
                    state,
                    actor,
                    card.InstanceId,
                    targetedPlayAbility.RequiredTargets
                );

                if (!hasTargets)
                {
                    if (
                        targetedPlayAbility.MissingTargetPolicy
                        == MissingTargetPolicy.SkipAbility
                    )
                    {
                        return CommitSummon(state, actor, card, definition, null, null);
                    }

                    return CommandResult.Reject(
                        CommandRejectionReason.NoValidAbilityTargets
                    );
                }

                GameEvent playEvent = GameEvent.FromCard(
                    GameEventType.UnitPlayed,
                    state.RoundNumber,
                    card
                );

                var triggeredAbility = new TriggeredAbility(
                    card.InstanceId,
                    actor,
                    targetedPlayAbility,
                    playEvent
                );

                bool opened = state.TryOpenPendingSelection(
                    triggeredAbility,
                    canCancel: true
                );

                if (!opened)
                {
                    throw new InvalidOperationException(
                        "Validated Play selection could not be opened."
                    );
                }

                state.LastEvent =
                    $"{actor} đang chọn mục tiêu cho " + $"{targetedPlayAbility.Id}.";

                return CommandResult.Success();
            }

            return CommitSummon(state, actor, card, definition, null, null);
        }

        public CommandResult TrySubmitAbilitySelection(
            MatchState state,
            SeatId actor,
            ulong requestId,
            AbilityTargetSelection selection
        )
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            PendingAbilitySelection pending = state.PendingSelection;

            if (pending == null)
            {
                return CommandResult.Reject(
                    CommandRejectionReason.NoPendingAbilitySelection
                );
            }

            if (pending.RequestId != requestId)
            {
                return CommandResult.Reject(
                    CommandRejectionReason.InvalidAbilitySelectionRequest
                );
            }

            if (pending.ChoosingSeat != actor)
            {
                return CommandResult.Reject(CommandRejectionReason.NotAbilitySelector);
            }

            if (!abilityTargetValidator.Validate(state, pending, selection))
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidAbilityTarget);
            }

            CardInstance sourceCard = state
                .Player(actor)
                .FindCardAnywhere(pending.SourceCardInstanceId);

            bool isUncommittedPlay =
                pending.TriggerEvent.Type == GameEventType.UnitPlayed
                && sourceCard != null
                && sourceCard.Zone == CardZone.DrawHand;

            if (isUncommittedPlay)
            {
                if (
                    !definitionsById.TryGetValue(
                        sourceCard.DefinitionId,
                        out CardDefinition definition
                    )
                )
                {
                    return CommandResult.Reject(
                        CommandRejectionReason.MissingCardDefinition
                    );
                }

                PlayerState player = state.Player(actor);

                if (!player.HasActiveRosterSpace)
                {
                    return CommandResult.Reject(CommandRejectionReason.ActiveRosterFull);
                }

                int manaCost = CardCostCalculator.GetEffectiveCost(
                    state,
                    sourceCard,
                    definition
                );

                if (player.Mana < manaCost)
                {
                    return CommandResult.Reject(CommandRejectionReason.InsufficientMana);
                }

                state.TryClosePendingSelection(requestId);

                return CommitSummon(
                    state,
                    actor,
                    sourceCard,
                    definition,
                    pending.Ability,
                    selection
                );
            }

            state.TryClosePendingSelection(requestId);

            ResolveQueuedAbilities(
                state,
                pending.SourceCardInstanceId,
                pending.Ability.Id,
                selection
            );

            RoundTransition automaticTransition = ContinueAutomaticPhase(state);

            return CommandResult.Success(roundTransition: automaticTransition);
        }

        public CommandResult TryCancelAbilitySelection(
            MatchState state,
            SeatId actor,
            ulong requestId
        )
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            PendingAbilitySelection pending = state.PendingSelection;

            if (pending == null)
            {
                return CommandResult.Reject(
                    CommandRejectionReason.NoPendingAbilitySelection
                );
            }

            if (pending.RequestId != requestId)
            {
                return CommandResult.Reject(
                    CommandRejectionReason.InvalidAbilitySelectionRequest
                );
            }

            if (pending.ChoosingSeat != actor)
            {
                return CommandResult.Reject(CommandRejectionReason.NotAbilitySelector);
            }

            if (!pending.CanCancel)
            {
                return CommandResult.Reject(
                    CommandRejectionReason.AbilitySelectionCannotBeCancelled
                );
            }

            CardInstance sourceCard = state
                .Player(actor)
                .FindCardAnywhere(pending.SourceCardInstanceId);

            if (sourceCard == null || sourceCard.Zone != CardZone.DrawHand)
            {
                return CommandResult.Reject(
                    CommandRejectionReason.AbilitySelectionCannotBeCancelled
                );
            }

            bool closed = state.TryClosePendingSelection(requestId);

            if (!closed)
            {
                throw new InvalidOperationException(
                    "Validated ability selection could not be closed."
                );
            }

            state.LastEvent = $"{actor} đã hủy việc sử dụng " + $"{pending.Ability.Id}.";

            return CommandResult.Success();
        }

        private AbilityDefinition FindTargetedPlayAbility(
            MatchState state,
            CardInstance card,
            CardDefinition definition
        )
        {
            foreach (AbilityDefinition ability in definition.Abilities)
            {
                if (
                    ability.Trigger != AbilityTrigger.Play
                    || ability.RequiredTargets.Count == 0
                )
                {
                    continue;
                }

                if (
                    !AbilityConditionEvaluator.IsSatisfied(
                        state,
                        card.Owner,
                        ability.Condition
                    )
                )
                {
                    continue;
                }

                if (
                    ability.OncePerRound
                    && state.RoundHistory.WasAbilityUsedThisRound(
                        card.InstanceId,
                        ability.Id
                    )
                )
                {
                    continue;
                }

                return ability;
            }

            return null;
        }

        private CommandResult CommitSummon(
            MatchState state,
            SeatId actor,
            CardInstance card,
            CardDefinition definition,
            AbilityDefinition selectedAbility,
            AbilityTargetSelection selectedTargets
        )
        {
            PlayerState player = state.Player(actor);

            int manaCost = CardCostCalculator.GetEffectiveCost(state, card, definition);

            bool summoned = player.TrySummonDrawHandCard(card, manaCost);

            if (!summoned)
            {
                return CommandResult.Reject(CommandRejectionReason.ActiveRosterFull);
            }

            CompleteNonPassAction(
                state,
                actor,
                $"{actor} đã triệu hồi " + $"{definition.DisplayName} vào Reserve."
            );

            GameEventBatch eventBatch = GameEventBatch.From(
                GameEvent.FromCard(GameEventType.UnitPlayed, state.RoundNumber, card),
                GameEvent.FromCard(GameEventType.UnitSummoned, state.RoundNumber, card)
            );

            EnqueueAndResolveAbilities(
                state,
                eventBatch,
                card.InstanceId,
                selectedAbility?.Id,
                selectedTargets
            );

            UpdateOutcome(state);

            if (state.IsFinished)
            {
                state.Phase = MatchPhase.Finished;
            }

            return CommandResult.Success();
        }

        private void EnqueueAndResolveAbilities(
            MatchState state,
            GameEventBatch eventBatch,
            ulong selectedSourceCardId = 0,
            string selectedAbilityId = null,
            AbilityTargetSelection selectedTargets = null
        )
        {
            if (eventBatch != null && eventBatch.Events.Count > 0)
            {
                triggeredAbilityQueue.Enqueue(state, eventBatch);
            }

            ResolveQueuedAbilities(
                state,
                selectedSourceCardId,
                selectedAbilityId,
                selectedTargets
            );
        }

        private void ResolveQueuedAbilities(
            MatchState state,
            ulong selectedSourceCardId,
            string selectedAbilityId,
            AbilityTargetSelection selectedTargets
        )
        {
            const int maximumResolutions = 128;

            int resolvedCount = 0;
            bool suppliedSelectionConsumed = false;

            while (triggeredAbilityQueue.TryPeek(out TriggeredAbility next))
            {
                resolvedCount++;

                if (resolvedCount > maximumResolutions)
                {
                    triggeredAbilityQueue.Clear();

                    throw new InvalidOperationException(
                        "Ability resolution exceeded its safety limit."
                    );
                }

                AbilityTargetSelection executionSelection = null;

                if (next.Ability.RequiredTargets.Count > 0)
                {
                    bool hasEnoughTargets = abilityTargetValidator.HasEnoughValidTargets(
                        state,
                        next.ListenerOwner,
                        next.ListenerCardInstanceId,
                        next.Ability.RequiredTargets
                    );

                    if (!hasEnoughTargets)
                    {
                        triggeredAbilityQueue.TryDequeue(out TriggeredAbility skipped);

                        continue;
                    }

                    bool matchesSuppliedSelection =
                        !suppliedSelectionConsumed
                        && selectedTargets != null
                        && next.ListenerCardInstanceId == selectedSourceCardId
                        && string.Equals(
                            next.Ability.Id,
                            selectedAbilityId,
                            StringComparison.Ordinal
                        );

                    if (matchesSuppliedSelection)
                    {
                        executionSelection = selectedTargets;

                        suppliedSelectionConsumed = true;
                    }
                    else
                    {
                        bool opened = state.TryOpenPendingSelection(next);

                        if (!opened)
                        {
                            throw new InvalidOperationException(
                                "Queued ability could not open " + "target selection."
                            );
                        }

                        state.LastEvent =
                            $"{next.ListenerOwner} đang chọn mục tiêu "
                            + $"cho {next.Ability.Id}.";

                        return;
                    }
                }

                triggeredAbilityQueue.TryDequeue(out TriggeredAbility executing);

                GameEventBatch emittedEvents = abilityEffectExecutor.Execute(
                    state,
                    executing,
                    executionSelection
                );

                UpdateOutcome(state);

                if (state.IsFinished)
                {
                    triggeredAbilityQueue.Clear();

                    state.Phase = MatchPhase.Finished;

                    return;
                }

                if (emittedEvents.Events.Count > 0)
                {
                    triggeredAbilityQueue.Enqueue(state, emittedEvents);
                }
            }
        }

        public CommandResult TryDeclareAttack(
            MatchState state,
            SeatId actor,
            ulong[] attackerIdsBySlot
        )
        {
            CommandResult actorValidation = ValidatePriorityActor(state, actor);

            if (!actorValidation.Accepted)
            {
                return actorValidation;
            }

            if (state.AttackTokenOwner != actor)
            {
                return CommandResult.Reject(CommandRejectionReason.NotAttackTokenOwner);
            }

            if (!state.AttackTokenAvailable)
            {
                return CommandResult.Reject(
                    CommandRejectionReason.AttackTokenUnavailable
                );
            }

            PlayerState attacker = state.Player(actor);

            if (attacker.Board.Count > 0)
            {
                return CommandResult.Reject(CommandRejectionReason.BoardNotEmpty);
            }

            if (
                attackerIdsBySlot == null
                || attackerIdsBySlot.Length != MatchState.BoardSlotCount
            )
            {
                return CommandResult.Reject(
                    CommandRejectionReason.InvalidAttackDeclaration
                );
            }

            var selectedCards = new CardInstance[MatchState.BoardSlotCount];

            var uniqueIds = new HashSet<ulong>();

            int attackerCount = 0;

            // Validate the entire declaration before mutating state.
            for (int slotIndex = 0; slotIndex < attackerIdsBySlot.Length; slotIndex++)
            {
                ulong instanceId = attackerIdsBySlot[slotIndex];

                // Zero represents an empty board slot.
                if (instanceId == 0)
                {
                    continue;
                }

                if (!uniqueIds.Add(instanceId))
                {
                    return CommandResult.Reject(
                        CommandRejectionReason.InvalidAttackDeclaration
                    );
                }

                CardInstance card = attacker.FindCardInReserve(instanceId);

                if (card == null)
                {
                    return CommandResult.Reject(CommandRejectionReason.CardNotInReserve);
                }

                selectedCards[slotIndex] = card;
                attackerCount++;
            }

            if (attackerCount == 0)
            {
                return CommandResult.Reject(
                    CommandRejectionReason.InvalidAttackDeclaration
                );
            }

            for (int slotIndex = 0; slotIndex < selectedCards.Length; slotIndex++)
            {
                CardInstance card = selectedCards[slotIndex];

                if (card == null)
                {
                    continue;
                }

                bool moved = attacker.TryMoveReserveCardToBoard(card, slotIndex);

                if (!moved)
                {
                    throw new InvalidOperationException(
                        "Validated attack declaration could not be applied."
                    );
                }
            }

            GameEventBatch joinedAttackEvents =
                unitPassiveRuleResolver.JoinAttackFromGraveyard(
                    state,
                    actor,
                    selectedCards
                );

            attackerCount = 0;

            foreach (CardInstance attackerCard in selectedCards)
            {
                if (attackerCard != null)
                {
                    attackerCount++;
                }
            }

            state.AttackTokenAvailable = false;
            state.ConsecutivePasses = 0;

            state.Phase = MatchPhase.BlockDeclaration;

            state.ActiveSeat = actor.Opponent();

            GameEventBatch attackEvents = BuildAttackEventBatch(
                state.RoundNumber,
                selectedCards
            );

            EnqueueAndResolveAbilities(
                state,
                MergeGameEventBatches(joinedAttackEvents, attackEvents)
            );

            UpdateOutcome(state);

            if (state.IsFinished)
            {
                state.Phase = MatchPhase.Finished;

                state.LastEvent =
                    $"Skill khi tấn công đã kết thúc trận. "
                    + $"Kết quả: {state.Outcome}.";

                return CommandResult.Success();
            }

            if (state.PendingSelection == null)
            {
                state.LastEvent =
                    $"{actor} đã khai báo tấn công bằng "
                    + $"{attackerCount} lá bài. "
                    + $"{state.ActiveSeat} đang chọn blocker.";
            }

            return CommandResult.Success();
        }

        private static GameEventBatch BuildAttackEventBatch(
            int roundNumber,
            CardInstance[] attackersBySlot
        )
        {
            var events = new List<GameEvent>();

            for (int slotIndex = 0; slotIndex < attackersBySlot.Length; slotIndex++)
            {
                CardInstance attacker = attackersBySlot[slotIndex];

                if (attacker == null)
                {
                    continue;
                }

                events.Add(
                    GameEvent.FromCard(
                        GameEventType.AttackDeclared,
                        roundNumber,
                        attacker
                    )
                );

                int supportedSlot = slotIndex + 1;

                if (supportedSlot >= attackersBySlot.Length)
                {
                    continue;
                }

                CardInstance supportedUnit = attackersBySlot[supportedSlot];

                if (supportedUnit == null)
                {
                    continue;
                }

                events.Add(
                    GameEvent.FromCard(
                        GameEventType.UnitSupported,
                        roundNumber,
                        attacker,
                        supportedUnit
                    )
                );
            }

            return new GameEventBatch(events);
        }

        private static GameEventBatch MergeGameEventBatches(
            params GameEventBatch[] batches
        )
        {
            var events = new List<GameEvent>();

            if (batches == null)
            {
                return new GameEventBatch(events);
            }

            foreach (GameEventBatch batch in batches)
            {
                if (batch == null)
                {
                    continue;
                }

                foreach (GameEvent gameEvent in batch.Events)
                {
                    events.Add(gameEvent);
                }
            }

            return new GameEventBatch(events);
        }

        public CommandResult TryDeclareBlock(
            MatchState state,
            SeatId actor,
            ulong[] blockerIdsBySlot
        )
        {
            CommandResult actorValidation = ValidateBlockActor(state, actor);

            if (!actorValidation.Accepted)
            {
                return actorValidation;
            }

            if (
                blockerIdsBySlot == null
                || blockerIdsBySlot.Length != MatchState.BoardSlotCount
            )
            {
                return CommandResult.Reject(
                    CommandRejectionReason.InvalidBlockDeclaration
                );
            }

            PlayerState attacker = state.Player(state.AttackTokenOwner);

            PlayerState defender = state.Player(actor);

            if (defender.Board.Count > 0)
            {
                return CommandResult.Reject(CommandRejectionReason.BoardNotEmpty);
            }

            var selectedBlockers = new CardInstance[MatchState.BoardSlotCount];

            var uniqueIds = new HashSet<ulong>();

            int blockerCount = 0;

            for (int slotIndex = 0; slotIndex < blockerIdsBySlot.Length; slotIndex++)
            {
                ulong blockerId = blockerIdsBySlot[slotIndex];

                if (blockerId == 0)
                {
                    continue;
                }

                if (attacker.FindBoardCard(slotIndex) == null)
                {
                    return CommandResult.Reject(
                        CommandRejectionReason.BlockerHasNoAttacker
                    );
                }

                if (!uniqueIds.Add(blockerId))
                {
                    return CommandResult.Reject(
                        CommandRejectionReason.InvalidBlockDeclaration
                    );
                }

                CardInstance blocker = defender.FindCardInReserve(blockerId);

                if (blocker == null || blocker.IsDead)
                {
                    return CommandResult.Reject(CommandRejectionReason.CardNotInReserve);
                }

                CardInstance attackingCard = attacker.FindBoardCard(slotIndex);

                CommandRejectionReason keywordRejection = ValidateBlockKeywords(
                    attackingCard,
                    blocker
                );

                if (keywordRejection != CommandRejectionReason.None)
                {
                    return CommandResult.Reject(keywordRejection);
                }

                selectedBlockers[slotIndex] = blocker;
                blockerCount++;
            }

            for (int slotIndex = 0; slotIndex < selectedBlockers.Length; slotIndex++)
            {
                CardInstance blocker = selectedBlockers[slotIndex];

                if (blocker == null)
                {
                    continue;
                }

                bool moved = defender.TryMoveReserveCardToBoard(blocker, slotIndex);

                if (!moved)
                {
                    throw new InvalidOperationException(
                        "Validated block declaration could not be applied."
                    );
                }
            }

            state.ConsecutivePasses = 0;
            state.Phase = MatchPhase.CombatResolution;

            GameEventBatch deathEventBatch;

            RoundResolution resolution = ResolveCombat(
                state,
                state.AttackTokenOwner,
                out deathEventBatch
            );

            UpdateOutcome(state);

            if (state.IsFinished)
            {
                state.Phase = MatchPhase.Finished;

                state.LastEvent =
                    $"Combat kết thúc với {blockerCount} blocker. "
                    + $"Kết quả: {state.Outcome}.";

                return CommandResult.Success(resolution);
            }

            state.Host.ReturnBoardCardsToReserve();
            state.Guest.ReturnBoardCardsToReserve();

            state.ActiveSeat = actor;
            state.Phase = MatchPhase.Priority;

            EnqueueAndResolveAbilities(state, deathEventBatch);

            UpdateOutcome(state);

            if (state.IsFinished)
            {
                state.Phase = MatchPhase.Finished;

                state.LastEvent =
                    $"Combat và các skill đã kết thúc. " + $"Kết quả: {state.Outcome}.";

                return CommandResult.Success(resolution);
            }

            if (state.PendingSelection == null)
            {
                state.LastEvent =
                    $"Combat đã kết thúc với {blockerCount} blocker. "
                    + $"{actor} nhận priority sau combat.";
            }

            return CommandResult.Success(resolution);
        }

        public CommandResult TryPass(MatchState state, SeatId actor)
        {
            CommandResult actorValidation = ValidatePriorityActor(state, actor);

            if (!actorValidation.Accepted)
            {
                return actorValidation;
            }

            state.ConsecutivePasses++;

            if (state.ConsecutivePasses < 2)
            {
                state.ActiveSeat = actor.Opponent();

                state.LastEvent =
                    $"{actor} đã pass. "
                    + $"{state.ActiveSeat} có thể hành động "
                    + $"hoặc kết thúc vòng.";

                return CommandResult.Success();
            }

            RoundTransition roundTransition = CompleteRound(state);

            return CommandResult.Success(roundTransition: roundTransition);
        }

        #endregion

        #region Priority Validation

        private static CommandResult ValidatePriorityActor(MatchState state, SeatId actor)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (actor != SeatId.Host && actor != SeatId.Guest)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidSeat);
            }

            if (state.IsFinished)
            {
                return CommandResult.Reject(CommandRejectionReason.MatchFinished);
            }

            if (!state.IsMulliganPhaseComplete || state.RoundNumber <= 0)
            {
                return CommandResult.Reject(CommandRejectionReason.MatchNotReady);
            }

            if (state.Phase != MatchPhase.Priority)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidPhase);
            }

            if (state.ActiveSeat != actor)
            {
                return CommandResult.Reject(CommandRejectionReason.NotYourTurn);
            }

            return CommandResult.Success();
        }

        private static CommandResult ValidateBlockActor(MatchState state, SeatId actor)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (actor != SeatId.Host && actor != SeatId.Guest)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidSeat);
            }

            if (state.IsFinished)
            {
                return CommandResult.Reject(CommandRejectionReason.MatchFinished);
            }

            if (!state.IsMulliganPhaseComplete || state.RoundNumber <= 0)
            {
                return CommandResult.Reject(CommandRejectionReason.MatchNotReady);
            }

            if (state.Phase != MatchPhase.BlockDeclaration)
            {
                return CommandResult.Reject(CommandRejectionReason.InvalidPhase);
            }

            if (state.ActiveSeat != actor)
            {
                return CommandResult.Reject(CommandRejectionReason.NotYourTurn);
            }

            if (state.AttackTokenOwner == actor)
            {
                return CommandResult.Reject(CommandRejectionReason.NotDefendingPlayer);
            }

            return CommandResult.Success();
        }

        #endregion

        private CommandRejectionReason ValidateBlockKeywords(
            CardInstance attacker,
            CardInstance blocker
        )
        {
            if (attacker == null || blocker == null)
            {
                return CommandRejectionReason.InvalidBlockDeclaration;
            }

            if (
                !definitionsById.TryGetValue(
                    attacker.DefinitionId,
                    out CardDefinition attackerDefinition
                )
                || !definitionsById.TryGetValue(
                    blocker.DefinitionId,
                    out CardDefinition blockerDefinition
                )
            )
            {
                return CommandRejectionReason.MissingCardDefinition;
            }

            bool cannotBlock =
                (blockerDefinition.Keywords & UnitKeyword.CannotBlock) != 0;

            if (cannotBlock)
            {
                return CommandRejectionReason.CardCannotBlock;
            }

            bool attackerIsFearsome =
                (attackerDefinition.Keywords & UnitKeyword.Fearsome) != 0;

            int blockerDamage = blocker.GetDamage(blockerDefinition);

            if (attackerIsFearsome && blockerDamage < FearsomeMinimumBlockerDamage)
            {
                return CommandRejectionReason.FearsomeBlockerTooWeak;
            }

            return CommandRejectionReason.None;
        }

        #region Priority Progression

        private static void CompleteNonPassAction(
            MatchState state,
            SeatId actor,
            string actionDescription
        )
        {
            state.ConsecutivePasses = 0;
            state.ActiveSeat = actor.Opponent();

            state.LastEvent =
                $"{actionDescription} "
                + $"Quyền hành động chuyển cho {state.ActiveSeat}.";
        }

        private RoundTransition CompleteRound(MatchState state)
        {
            state.Phase = MatchPhase.RoundEnd;

            state.AttackTokenAvailable = false;

            GameEventBatch ephemeralDeaths = ExpireEphemeralUnits(state);

            EnqueueAndResolveAbilities(state, ephemeralDeaths);

            UpdateOutcome(state);

            if (state.IsFinished)
            {
                state.Phase = MatchPhase.Finished;

                return null;
            }

            if (state.PendingSelection != null)
            {
                return null;
            }

            return FinishRoundTransition(state);
        }

        private RoundTransition FinishRoundTransition(MatchState state)
        {
            int completedRound = state.RoundNumber;

            ClearRoundModifiers(state.Host);

            ClearRoundModifiers(state.Guest);

            state.Host.ReturnBoardCardsToReserve();
            state.Guest.ReturnBoardCardsToReserve();

            if (completedRound % DrawIntervalRounds == 0)
            {
                state.Host.DrawOne();
                state.Guest.DrawOne();
            }

            state.Host.AdvanceAndRefillMana(MaximumMana);

            state.Guest.AdvanceAndRefillMana(MaximumMana);

            state.AttackTokenOwner = state.AttackTokenOwner.Opponent();

            state.RoundNumber = completedRound + 1;

            state.RoundHistory.AdvanceToRound(state.RoundNumber);

            state.ConsecutivePasses = 0;

            state.Phase = MatchPhase.RoundStart;

            state.ActiveSeat = state.AttackTokenOwner;

            state.AttackTokenAvailable = true;

            var transition = new RoundTransition(
                completedRound,
                state.RoundNumber,
                state.AttackTokenOwner
            );

            GameEventBatch passiveReviveEvents =
                unitPassiveRuleResolver.ReviveAtRoundStart(state);

            GameEventBatch roundStartEvents = GameEventBatch.From(
                GameEvent.RoundStarted(state.RoundNumber)
            );

            EnqueueAndResolveAbilities(
                state,
                MergeGameEventBatches(passiveReviveEvents, roundStartEvents)
            );

            UpdateOutcome(state);

            if (state.IsFinished)
            {
                state.Phase = MatchPhase.Finished;

                return transition;
            }

            if (state.PendingSelection == null)
            {
                EnterRoundPriority(state);
            }

            return transition;
        }

        private RoundTransition ContinueAutomaticPhase(MatchState state)
        {
            if (state.IsFinished || state.PendingSelection != null)
            {
                return null;
            }

            if (state.Phase == MatchPhase.RoundEnd)
            {
                return FinishRoundTransition(state);
            }

            if (state.Phase == MatchPhase.RoundStart)
            {
                EnterRoundPriority(state);
            }

            return null;
        }

        private static void EnterRoundPriority(MatchState state)
        {
            state.Phase = MatchPhase.Priority;

            state.LastEvent =
                $"{state.AttackTokenOwner} nhận attack token "
                + $"và hành động đầu tiên ở vòng "
                + $"{state.RoundNumber}.";
        }

        private GameEventBatch ExpireEphemeralUnits(MatchState state)
        {
            var expiredCards = new List<CardInstance>();

            AddEphemeralBoardCards(state.Host, expiredCards);

            AddEphemeralBoardCards(state.Guest, expiredCards);

            expiredCards.Sort(
                (left, right) => left.InstanceId.CompareTo(right.InstanceId)
            );

            var deathEvents = new List<GameEvent>();

            foreach (CardInstance card in expiredCards)
            {
                if (
                    !definitionsById.TryGetValue(
                        card.DefinitionId,
                        out CardDefinition definition
                    )
                )
                {
                    continue;
                }

                card.Kill();

                bool moved = state
                    .Player(card.Owner)
                    .TryMoveDeadActiveCardToGraveyard(card);

                if (!moved)
                {
                    continue;
                }

                int damageAtDeath = card.GetDamage(definition);

                int maximumHealthAtDeath = card.GetMaximumHealth(definition);

                state.RoundHistory.RecordDeath(card, damageAtDeath, maximumHealthAtDeath);

                deathEvents.Add(
                    GameEvent.FromCard(GameEventType.UnitDied, state.RoundNumber, card)
                );
            }

            return new GameEventBatch(deathEvents);
        }

        private void AddEphemeralBoardCards(PlayerState player, List<CardInstance> result)
        {
            foreach (CardInstance card in player.Board)
            {
                if (!card.IsDead && HasKeyword(card, UnitKeyword.Ephemeral))
                {
                    result.Add(card);
                }
            }
        }

        private void ClearRoundModifiers(PlayerState player)
        {
            foreach (CardInstance card in player.DrawHand)
            {
                ClearRoundModifiers(card);
            }
            foreach (CardInstance card in player.Reserve)
            {
                ClearRoundModifiers(card);
            }

            foreach (CardInstance card in player.Board)
            {
                ClearRoundModifiers(card);
            }
        }

        private void ClearRoundModifiers(CardInstance card)
        {
            if (
                !definitionsById.TryGetValue(
                    card.DefinitionId,
                    out CardDefinition definition
                )
            )
            {
                return;
            }

            card.ClearRoundModifiers(definition);
        }

        #endregion

        #region Combat Resolution

        private RoundResolution ResolveCombat(
            MatchState state,
            SeatId attackerSeat,
            out GameEventBatch deathEventBatch
        )
        {
            SeatId defenderSeat = attackerSeat.Opponent();

            PlayerState defender = state.Player(defenderSeat);

            var steps = new List<CombatStep>();

            for (int slotIndex = 0; slotIndex < MatchState.BoardSlotCount; slotIndex++)
            {
                CardInstance hostCard = state.Host.FindBoardCard(slotIndex);

                CardInstance guestCard = state.Guest.FindBoardCard(slotIndex);

                CardInstance attackerCard =
                    attackerSeat == SeatId.Host ? hostCard : guestCard;

                CardInstance blockerCard =
                    attackerSeat == SeatId.Host ? guestCard : hostCard;

                if (attackerCard == null)
                {
                    continue;
                }

                int hostCardHealthBefore = hostCard?.CurrentHealth ?? 0;

                int guestCardHealthBefore = guestCard?.CurrentHealth ?? 0;

                int hostNexusHealthBefore = state.Host.NexusHealth;

                int guestNexusHealthBefore = state.Guest.NexusHealth;

                int attackerDamageTaken = 0;
                int blockerDamageTaken = 0;

                bool attackerDiedFromEphemeral = false;

                bool blockerDiedFromEphemeral = false;

                if (blockerCard != null)
                {
                    int attackerDamage = DamageOf(attackerCard);

                    int blockerDamage = DamageOf(blockerCard);

                    attackerDamageTaken = CalculateActualDamage(
                        blockerDamage,
                        attackerCard.CurrentHealth
                    );

                    blockerDamageTaken = CalculateActualDamage(
                        attackerDamage,
                        blockerCard.CurrentHealth
                    );

                    attackerCard.ApplyDamage(blockerDamage);

                    blockerCard.ApplyDamage(attackerDamage);

                    ApplyLifesteal(state, attackerCard, blockerDamageTaken);

                    ApplyLifesteal(state, blockerCard, attackerDamageTaken);

                    if (
                        HasKeyword(attackerCard, UnitKeyword.Ephemeral)
                        && !attackerCard.IsDead
                    )
                    {
                        attackerCard.Kill();

                        attackerDiedFromEphemeral = true;
                    }

                    if (
                        HasKeyword(blockerCard, UnitKeyword.Ephemeral)
                        && !blockerCard.IsDead
                    )
                    {
                        blockerCard.Kill();

                        blockerDiedFromEphemeral = true;
                    }
                }
                else
                {
                    int nexusHealthBefore = defender.NexusHealth;

                    defender.ApplyNexusDamage(DamageOf(attackerCard));

                    int actualNexusDamage = Math.Max(
                        0,
                        nexusHealthBefore - defender.NexusHealth
                    );

                    ApplyLifesteal(state, attackerCard, actualNexusDamage);

                    if (
                        HasKeyword(attackerCard, UnitKeyword.Ephemeral)
                        && !attackerCard.IsDead
                    )
                    {
                        attackerCard.Kill();

                        attackerDiedFromEphemeral = true;
                    }
                }

                bool hostIsAttacker = attackerSeat == SeatId.Host;

                CardCombatResolution hostResolution = BuildCardResolution(
                    hostCard,
                    SeatId.Host,
                    hostCardHealthBefore,
                    hostIsAttacker ? attackerDamageTaken : blockerDamageTaken,
                    hostIsAttacker ? attackerDiedFromEphemeral : blockerDiedFromEphemeral
                );

                CardCombatResolution guestResolution = BuildCardResolution(
                    guestCard,
                    SeatId.Guest,
                    guestCardHealthBefore,
                    hostIsAttacker ? blockerDamageTaken : attackerDamageTaken,
                    hostIsAttacker ? blockerDiedFromEphemeral : attackerDiedFromEphemeral
                );

                steps.Add(
                    new CombatStep(
                        slotIndex,
                        hostResolution,
                        guestResolution,
                        hostNexusHealthBefore,
                        state.Host.NexusHealth,
                        guestNexusHealthBefore,
                        state.Guest.NexusHealth
                    )
                );
            }

            List<CardInstance> deadCards = CollectDeadCardsInCombatOrder(
                state,
                attackerSeat
            );

            state.Host.MoveDeadBoardCardsToGraveyard();
            state.Guest.MoveDeadBoardCardsToGraveyard();

            var deathEvents = new List<GameEvent>();

            foreach (CardInstance deadCard in deadCards)
            {
                int damageAtDeath = 0;
                int maximumHealthAtDeath = 0;

                if (
                    definitionsById.TryGetValue(
                        deadCard.DefinitionId,
                        out CardDefinition definition
                    )
                )
                {
                    damageAtDeath = deadCard.GetDamage(definition);

                    maximumHealthAtDeath = deadCard.GetMaximumHealth(definition);
                }

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

            deathEventBatch = new GameEventBatch(deathEvents);

            return new RoundResolution(state.RoundNumber, steps);
        }

        private static List<CardInstance> CollectDeadCardsInCombatOrder(
            MatchState state,
            SeatId attackerSeat
        )
        {
            var result = new List<CardInstance>();

            PlayerState attacker = state.Player(attackerSeat);

            PlayerState defender = state.Player(attackerSeat.Opponent());

            for (int slotIndex = 0; slotIndex < MatchState.BoardSlotCount; slotIndex++)
            {
                CardInstance attackerCard = attacker.FindBoardCard(slotIndex);

                CardInstance defenderCard = defender.FindBoardCard(slotIndex);

                if (attackerCard != null && attackerCard.IsDead)
                {
                    result.Add(attackerCard);
                }

                if (defenderCard != null && defenderCard.IsDead)
                {
                    result.Add(defenderCard);
                }
            }

            return result;
        }

        private int DamageOf(CardInstance card)
        {
            if (card == null)
            {
                return 0;
            }

            if (
                !definitionsById.TryGetValue(
                    card.DefinitionId,
                    out CardDefinition definition
                )
            )
            {
                return 0;
            }

            return card.GetDamage(definition);
        }

        private bool HasKeyword(CardInstance card, UnitKeyword keyword)
        {
            if (
                card == null
                || !definitionsById.TryGetValue(
                    card.DefinitionId,
                    out CardDefinition definition
                )
            )
            {
                return false;
            }

            return (definition.Keywords & keyword) != 0;
        }

        private static int CalculateActualDamage(int attemptedDamage, int healthBefore)
        {
            return Math.Min(Math.Max(0, attemptedDamage), Math.Max(0, healthBefore));
        }

        private void ApplyLifesteal(
            MatchState state,
            CardInstance source,
            int actualDamage
        )
        {
            if (actualDamage <= 0 || !HasKeyword(source, UnitKeyword.Lifesteal))
            {
                return;
            }

            state.Player(source.Owner).HealNexus(actualDamage);
        }

        private CardCombatResolution BuildCardResolution(
            CardInstance card,
            SeatId seat,
            int healthBefore,
            int damageTaken,
            bool diedFromEphemeral
        )
        {
            if (card == null)
            {
                return null;
            }

            return new CardCombatResolution(
                seat,
                card.InstanceId,
                healthBefore,
                card.CurrentHealth,
                DamageOf(card),
                damageTaken,
                diedFromEphemeral
            );
        }

        private static void UpdateOutcome(MatchState state)
        {
            bool hostDead = state.Host.NexusHealth <= 0;

            bool guestDead = state.Guest.NexusHealth <= 0;

            if (hostDead && guestDead)
            {
                state.Outcome = MatchOutcome.Draw;
            }
            else if (hostDead)
            {
                state.Outcome = MatchOutcome.GuestWon;
            }
            else if (guestDead)
            {
                state.Outcome = MatchOutcome.HostWon;
            }
        }

        #endregion
    }
}
