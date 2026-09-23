using System.Collections.Generic;
using NUnit.Framework;

namespace KLTN.Game.Domain.Tests
{
    public sealed class UnitPassiveRuleTests
    {
        [Test]
        public void EffectiveCost_CountsAlliedDeathsThisGame()
        {
            MatchState state = CreateState();

            CardDefinition discountDefinition = Unit(
                "discount",
                cost: 4,
                passiveRules: new UnitPassiveRules(costReductionPerAlliedDeathThisGame: 1)
            );

            CardDefinition deadDefinition = Unit("dead");

            RecordDeadCard(state, deadDefinition, 1);

            RecordDeadCard(state, deadDefinition, 2);

            CardInstance card = new CardInstance(
                10,
                discountDefinition.Id,
                SeatId.Host,
                CardZone.DrawHand,
                discountDefinition.BaseHealth
            );

            state.Host.DrawHand.Add(card);

            Assert.AreEqual(
                2,
                CardCostCalculator.GetEffectiveCost(state, card, discountDefinition)
            );
        }

        [Test]
        public void OncePerMatch_DoesNotResetNextRound()
        {
            AbilityDefinition ability = new AbilityDefinition(
                "once",
                AbilityTrigger.AllyDeath,
                new[]
                {
                    new EffectDefinition(
                        EffectKind.Buff,
                        EffectTarget.Source,
                        amount: 2,
                        secondaryAmount: 2
                    ),
                },
                oncePerMatch: true
            );

            CardDefinition listenerDefinition = Unit(
                "listener",
                abilities: new[] { ability }
            );

            CardDefinition subjectDefinition = Unit("subject");

            var definitions = Definitions(listenerDefinition, subjectDefinition);

            MatchState state = CreateState();

            CardInstance listener = AddReserveCard(state.Host, 1, listenerDefinition);

            CardInstance subject = new CardInstance(
                2,
                subjectDefinition.Id,
                SeatId.Host,
                CardZone.Graveyard,
                0
            );

            var resolver = new AbilityTriggerResolver(definitions);

            IReadOnlyList<TriggeredAbility> first = resolver.Collect(
                state,
                GameEventBatch.From(
                    GameEvent.FromCard(GameEventType.UnitDied, 1, subject)
                )
            );

            Assert.AreEqual(1, first.Count);

            state.RoundHistory.AdvanceToRound(2);
            state.RoundNumber = 2;

            IReadOnlyList<TriggeredAbility> second = resolver.Collect(
                state,
                GameEventBatch.From(
                    GameEvent.FromCard(GameEventType.UnitDied, 2, subject)
                )
            );

            Assert.AreEqual(0, second.Count);
            Assert.AreEqual(CardZone.Reserve, listener.Zone);
        }

        [Test]
        public void RoundStartRevive_GrantsBonusPerDeath()
        {
            CardDefinition definition = Unit(
                "am_binh",
                health: 2,
                damage: 2,
                passiveRules: new UnitPassiveRules(
                    revivesAtRoundStart: true,
                    powerAndHealthPerDeath: 1
                )
            );

            var definitions = Definitions(definition);

            MatchState state = CreateState();

            CardInstance card = RecordDeadCard(state, definition, 1);

            state.RoundHistory.AdvanceToRound(2);
            state.RoundNumber = 2;

            var resolver = new UnitPassiveRuleResolver(definitions);

            GameEventBatch events = resolver.ReviveAtRoundStart(state);

            Assert.AreEqual(CardZone.Reserve, card.Zone);

            Assert.AreEqual(3, card.GetDamage(definition));

            Assert.AreEqual(3, card.CurrentHealth);

            Assert.AreEqual(1, events.Events.Count);

            Assert.AreEqual(GameEventType.UnitSummoned, events.Events[0].Type);
        }

        [Test]
        public void GraveyardJoin_FillsEmptyAttackSlot()
        {
            CardDefinition attackerDefinition = Unit(
                "ephemeral",
                keywords: UnitKeyword.Ephemeral
            );

            CardDefinition maDaDefinition = Unit(
                "ma_da",
                health: 1,
                damage: 3,
                keywords: UnitKeyword.Ephemeral | UnitKeyword.CannotBlock,
                passiveRules: new UnitPassiveRules(
                    joinsAttackFromGraveyard: true,
                    requiresEphemeralAttacker: true
                )
            );

            var definitions = Definitions(attackerDefinition, maDaDefinition);

            MatchState state = CreateState();

            CardInstance attacker = AddReserveCard(state.Host, 1, attackerDefinition);

            Assert.IsTrue(state.Host.TryMoveReserveCardToBoard(attacker, 0));

            CardInstance maDa = RecordDeadCard(state, maDaDefinition, 2);

            var attackers = new CardInstance[] { attacker, null, null };

            var resolver = new UnitPassiveRuleResolver(definitions);

            GameEventBatch events = resolver.JoinAttackFromGraveyard(
                state,
                SeatId.Host,
                attackers
            );

            Assert.AreSame(maDa, attackers[1]);

            Assert.AreEqual(CardZone.Board, maDa.Zone);

            Assert.AreEqual(1, maDa.BoardSlotIndex);

            Assert.AreEqual(1, events.Events.Count);
        }

        [Test]
        public void GraveyardJoin_BypassesRosterCapButStillUsesEmptyBoardSlot()
        {
            CardDefinition attackerDefinition = Unit(
                "ma_lai",
                keywords: UnitKeyword.Ephemeral
            );

            CardDefinition fillerDefinition = Unit("filler");

            CardDefinition maDaDefinition = Unit(
                "ma_da",
                health: 1,
                damage: 3,
                keywords: UnitKeyword.Ephemeral | UnitKeyword.CannotBlock,
                passiveRules: new UnitPassiveRules(
                    joinsAttackFromGraveyard: true,
                    requiresEphemeralAttacker: true
                )
            );

            var definitions = Definitions(
                attackerDefinition,
                fillerDefinition,
                maDaDefinition
            );

            MatchState state = CreateState();

            CardInstance attacker = AddReserveCard(state.Host, 1, attackerDefinition);

            AddReserveCard(state.Host, 2, fillerDefinition);
            AddReserveCard(state.Host, 3, fillerDefinition);
            AddReserveCard(state.Host, 4, fillerDefinition);

            Assert.AreEqual(
                MatchState.MaximumActiveRosterSize,
                state.Host.ActiveRosterCount
            );

            Assert.IsTrue(state.Host.TryMoveReserveCardToBoard(attacker, 0));

            CardInstance maDa = RecordDeadCard(state, maDaDefinition, 10);

            var attackers = new CardInstance[] { attacker, null, null };

            var resolver = new UnitPassiveRuleResolver(definitions);

            GameEventBatch events = resolver.JoinAttackFromGraveyard(
                state,
                SeatId.Host,
                attackers
            );

            Assert.AreSame(maDa, attackers[1]);
            Assert.AreEqual(CardZone.Board, maDa.Zone);
            Assert.AreEqual(1, events.Events.Count);
            Assert.AreEqual(MatchState.MaximumActiveRosterSize + 1, state.Host.ActiveRosterCount);
        }

        [Test]
        public void GraveyardJoin_RequiresEphemeralAttacker()
        {
            CardDefinition attackerDefinition = Unit("normal");

            CardDefinition maDaDefinition = Unit(
                "ma_da",
                passiveRules: new UnitPassiveRules(
                    joinsAttackFromGraveyard: true,
                    requiresEphemeralAttacker: true
                )
            );

            var definitions = Definitions(attackerDefinition, maDaDefinition);

            MatchState state = CreateState();

            CardInstance attacker = AddReserveCard(state.Host, 1, attackerDefinition);

            Assert.IsTrue(state.Host.TryMoveReserveCardToBoard(attacker, 0));

            CardInstance maDa = RecordDeadCard(state, maDaDefinition, 2);

            var attackers = new CardInstance[] { attacker, null, null };

            var resolver = new UnitPassiveRuleResolver(definitions);

            GameEventBatch events = resolver.JoinAttackFromGraveyard(
                state,
                SeatId.Host,
                attackers
            );

            Assert.IsNull(attackers[1]);

            Assert.AreEqual(CardZone.Graveyard, maDa.Zone);

            Assert.AreEqual(0, events.Events.Count);
        }

        [Test]
        public void KillThenRevive_ResolvesInOneAbility()
        {
            CardDefinition sourceDefinition = Unit("source");

            CardDefinition targetDefinition = Unit("target", health: 4);

            var definitions = Definitions(sourceDefinition, targetDefinition);

            MatchState state = CreateState();

            CardInstance source = AddReserveCard(state.Host, 1, sourceDefinition);

            CardInstance target = AddReserveCard(state.Host, 2, targetDefinition);

            var ability = new AbilityDefinition(
                "kill_then_revive",
                AbilityTrigger.Play,
                new[]
                {
                    new EffectDefinition(EffectKind.Kill, EffectTarget.PrimarySelection),
                    new EffectDefinition(
                        EffectKind.Revive,
                        EffectTarget.PrimarySelection
                    ),
                }
            );

            var triggered = new TriggeredAbility(
                source.InstanceId,
                source.Owner,
                ability,
                GameEvent.FromCard(GameEventType.UnitPlayed, state.RoundNumber, source)
            );

            var selection = new AbilityTargetSelection(new[] { target.InstanceId }, null);

            var executor = new AbilityEffectExecutor(definitions);

            GameEventBatch events = executor.Execute(state, triggered, selection);

            Assert.AreEqual(CardZone.Reserve, target.Zone);

            Assert.AreEqual(targetDefinition.BaseHealth, target.CurrentHealth);

            Assert.AreEqual(2, events.Events.Count);

            Assert.AreEqual(GameEventType.UnitDied, events.Events[0].Type);

            Assert.AreEqual(GameEventType.UnitSummoned, events.Events[1].Type);
        }

        [Test]
        public void KillThenRevive_AppliesDeathScalingImmediately()
        {
            CardDefinition sourceDefinition = Unit("thien_linh_cai");

            CardDefinition targetDefinition = Unit(
                "am_binh",
                health: 2,
                damage: 2,
                passiveRules: new UnitPassiveRules(powerAndHealthPerDeath: 1)
            );

            var definitions = Definitions(sourceDefinition, targetDefinition);

            MatchState state = CreateState();

            CardInstance source = AddReserveCard(state.Host, 1, sourceDefinition);

            CardInstance target = AddReserveCard(state.Host, 2, targetDefinition);

            var ability = new AbilityDefinition(
                "kill_then_revive",
                AbilityTrigger.Play,
                new[]
                {
                    new EffectDefinition(EffectKind.Kill, EffectTarget.PrimarySelection),
                    new EffectDefinition(
                        EffectKind.Revive,
                        EffectTarget.PrimarySelection
                    ),
                }
            );

            var triggered = new TriggeredAbility(
                source.InstanceId,
                source.Owner,
                ability,
                GameEvent.FromCard(GameEventType.UnitPlayed, state.RoundNumber, source)
            );

            var selection = new AbilityTargetSelection(new[] { target.InstanceId }, null);

            var executor = new AbilityEffectExecutor(definitions);

            executor.Execute(state, triggered, selection);

            Assert.AreEqual(CardZone.Reserve, target.Zone);
            Assert.AreEqual(3, target.GetDamage(targetDefinition));
            Assert.AreEqual(3, target.CurrentHealth);
            Assert.AreEqual(1, state.RoundHistory.CountDeathsThisGame(target.InstanceId));
        }

        private static MatchState CreateState()
        {
            var state = new MatchState(SeatId.Host);

            state.Host.InitializeMana(10);
            state.Guest.InitializeMana(10);

            state.HostMulliganDone = true;
            state.GuestMulliganDone = true;

            state.BeginFirstRound();

            return state;
        }

        private static CardDefinition Unit(
            string id,
            int health = 3,
            int damage = 2,
            int cost = 1,
            UnitKeyword keywords = UnitKeyword.None,
            IReadOnlyList<AbilityDefinition> abilities = null,
            UnitPassiveRules passiveRules = null
        )
        {
            return new CardDefinition(
                id,
                id,
                health,
                damage,
                cost,
                keywords,
                abilities,
                passiveRules: passiveRules
            );
        }

        private static Dictionary<string, CardDefinition> Definitions(
            params CardDefinition[] definitions
        )
        {
            var result = new Dictionary<string, CardDefinition>();

            foreach (CardDefinition definition in definitions)
            {
                result.Add(definition.Id, definition);
            }

            return result;
        }

        private static CardInstance AddReserveCard(
            PlayerState player,
            ulong id,
            CardDefinition definition
        )
        {
            var card = new CardInstance(
                id,
                definition.Id,
                player.Seat,
                CardZone.Reserve,
                definition.BaseHealth
            );

            player.Reserve.Add(card);
            return card;
        }

        private static CardInstance RecordDeadCard(
            MatchState state,
            CardDefinition definition,
            ulong id
        )
        {
            CardInstance card = AddReserveCard(state.Host, id, definition);

            card.Kill();

            Assert.IsTrue(state.Host.TryMoveDeadActiveCardToGraveyard(card));

            state.RoundHistory.RecordDeath(
                card,
                definition.BaseDamage,
                definition.BaseHealth
            );

            return card;
        }
    }
}
