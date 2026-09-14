using System.Collections.Generic;
using NUnit.Framework;

namespace KLTN.Game.Domain.Tests
{
    public sealed class AbilityTriggerResolverTests
    {
        [Test]
        public void Summon_CollectsSelfBeforeAllyListeners()
        {
            MatchState state =
                CreateReadyState();

            var watcher =
                new CardInstance(
                    1,
                    "watcher",
                    SeatId.Host,
                    CardZone.Reserve,
                    3);

            var summoned =
                new CardInstance(
                    2,
                    "summoned",
                    SeatId.Host,
                    CardZone.Reserve,
                    3);

            state.Host.Reserve.Add(watcher);
            state.Host.Reserve.Add(summoned);

            var definitions =
                new Dictionary<string, CardDefinition>
                {
                    {
                        "watcher",
                        Definition(
                            "watcher",
                            Ability(
                                "watcher_ally_summon",
                                AbilityTrigger.AllySummon))
                    },
                    {
                        "summoned",
                        Definition(
                            "summoned",
                            Ability(
                                "summoned_self",
                                AbilityTrigger.Summon))
                    }
                };

            var resolver =
                new AbilityTriggerResolver(
                    definitions);

            IReadOnlyList<TriggeredAbility> result =
                resolver.Collect(
                    state,
                    GameEventBatch.From(
                        GameEvent.FromCard(
                            GameEventType.UnitSummoned,
                            1,
                            summoned)));

            Assert.AreEqual(2, result.Count);

            Assert.AreEqual(
                summoned.InstanceId,
                result[0].ListenerCardInstanceId);

            Assert.AreEqual(
                watcher.InstanceId,
                result[1].ListenerCardInstanceId);
        }

        [Test]
        public void DrawHandCard_DoesNotListenToAllySummon()
        {
            MatchState state =
                CreateReadyState();

            var hiddenWatcher =
                new CardInstance(
                    1,
                    "watcher",
                    SeatId.Host,
                    CardZone.DrawHand,
                    3);

            var summoned =
                new CardInstance(
                    2,
                    "plain",
                    SeatId.Host,
                    CardZone.Reserve,
                    3);

            state.Host.DrawHand.Add(hiddenWatcher);
            state.Host.Reserve.Add(summoned);

            var definitions =
                new Dictionary<string, CardDefinition>
                {
                    {
                        "watcher",
                        Definition(
                            "watcher",
                            Ability(
                                "watcher_ally_summon",
                                AbilityTrigger.AllySummon))
                    },
                    {
                        "plain",
                        Definition("plain")
                    }
                };

            var resolver =
                new AbilityTriggerResolver(
                    definitions);

            IReadOnlyList<TriggeredAbility> result =
                resolver.Collect(
                    state,
                    GameEventBatch.From(
                        GameEvent.FromCard(
                            GameEventType.UnitSummoned,
                            1,
                            summoned)));

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Death_CollectsLastBreathBeforeAllyDeath()
        {
            MatchState state =
                CreateReadyState();

            var deadCard =
                new CardInstance(
                    1,
                    "last_breath_unit",
                    SeatId.Host,
                    CardZone.Graveyard,
                    0);

            var watcher =
                new CardInstance(
                    2,
                    "death_watcher",
                    SeatId.Host,
                    CardZone.Reserve,
                    3);

            state.Host.Graveyard.Add(deadCard);
            state.Host.Reserve.Add(watcher);

            state.RoundHistory.RecordDeath(
                deadCard,
                2,
                3);

            var definitions =
                new Dictionary<string, CardDefinition>
                {
                    {
                        "last_breath_unit",
                        Definition(
                            "last_breath_unit",
                            Ability(
                                "last_breath",
                                AbilityTrigger.Death))
                    },
                    {
                        "death_watcher",
                        Definition(
                            "death_watcher",
                            Ability(
                                "watch_ally_death",
                                AbilityTrigger.AllyDeath))
                    }
                };

            var resolver =
                new AbilityTriggerResolver(
                    definitions);

            IReadOnlyList<TriggeredAbility> result =
                resolver.Collect(
                    state,
                    GameEventBatch.From(
                        GameEvent.FromCard(
                            GameEventType.UnitDied,
                            1,
                            deadCard)));

            Assert.AreEqual(2, result.Count);

            Assert.AreEqual(
                deadCard.InstanceId,
                result[0].ListenerCardInstanceId);

            Assert.AreEqual(
                watcher.InstanceId,
                result[1].ListenerCardInstanceId);
        }

        [Test]
        public void OncePerRound_IsCollectedOnlyOnce()
        {
            MatchState state =
                CreateReadyState();

            var watcher =
                new CardInstance(
                    1,
                    "watcher",
                    SeatId.Host,
                    CardZone.Reserve,
                    3);

            var firstDead =
                new CardInstance(
                    2,
                    "plain",
                    SeatId.Host,
                    CardZone.Graveyard,
                    0);

            var secondDead =
                new CardInstance(
                    3,
                    "plain",
                    SeatId.Host,
                    CardZone.Graveyard,
                    0);

            state.Host.Reserve.Add(watcher);
            state.Host.Graveyard.Add(firstDead);
            state.Host.Graveyard.Add(secondDead);

            var definitions =
                new Dictionary<string, CardDefinition>
                {
                    {
                        "watcher",
                        Definition(
                            "watcher",
                            Ability(
                                "once_per_round",
                                AbilityTrigger.AllyDeath,
                                oncePerRound: true))
                    },
                    {
                        "plain",
                        Definition("plain")
                    }
                };

            var resolver =
                new AbilityTriggerResolver(
                    definitions);

            IReadOnlyList<TriggeredAbility> first =
                resolver.Collect(
                    state,
                    GameEventBatch.From(
                        GameEvent.FromCard(
                            GameEventType.UnitDied,
                            1,
                            firstDead)));

            IReadOnlyList<TriggeredAbility> second =
                resolver.Collect(
                    state,
                    GameEventBatch.From(
                        GameEvent.FromCard(
                            GameEventType.UnitDied,
                            1,
                            secondDead)));

            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(0, second.Count);

            state.RoundHistory.AdvanceToRound(2);
            state.RoundNumber = 2;

            IReadOnlyList<TriggeredAbility> nextRound =
                resolver.Collect(
                    state,
                    GameEventBatch.From(
                        GameEvent.FromCard(
                            GameEventType.UnitDied,
                            2,
                            secondDead)));

            Assert.AreEqual(1, nextRound.Count);
        }

        [Test]
        public void PreviousRoundCondition_UsesRotatedHistory()
        {
            MatchState state =
                CreateReadyState();

            var deadCard =
                new CardInstance(
                    1,
                    "plain",
                    SeatId.Host,
                    CardZone.Graveyard,
                    0);

            state.Host.Graveyard.Add(deadCard);

            state.RoundHistory.RecordDeath(
                deadCard,
                2,
                3);

            var watcher =
                new CardInstance(
                    2,
                    "watcher",
                    SeatId.Host,
                    CardZone.Reserve,
                    3);

            var summoned =
                new CardInstance(
                    3,
                    "plain",
                    SeatId.Host,
                    CardZone.Reserve,
                    3);

            state.Host.Reserve.Add(watcher);
            state.Host.Reserve.Add(summoned);

            var definitions =
                new Dictionary<string, CardDefinition>
                {
                    {
                        "watcher",
                        Definition(
                            "watcher",
                            Ability(
                                "previous_death",
                                AbilityTrigger.AllySummon,
                                AbilityCondition.AllyDiedPreviousRound))
                    },
                    {
                        "plain",
                        Definition("plain")
                    }
                };

            var resolver =
                new AbilityTriggerResolver(
                    definitions);

            IReadOnlyList<TriggeredAbility> roundOne =
                resolver.Collect(
                    state,
                    GameEventBatch.From(
                        GameEvent.FromCard(
                            GameEventType.UnitSummoned,
                            1,
                            summoned)));

            Assert.AreEqual(0, roundOne.Count);

            state.RoundHistory.AdvanceToRound(2);
            state.RoundNumber = 2;

            IReadOnlyList<TriggeredAbility> roundTwo =
                resolver.Collect(
                    state,
                    GameEventBatch.From(
                        GameEvent.FromCard(
                            GameEventType.UnitSummoned,
                            2,
                            summoned)));

            Assert.AreEqual(1, roundTwo.Count);
        }

        private static MatchState CreateReadyState()
        {
            var state =
                new MatchState(SeatId.Host);

            state.HostMulliganDone = true;
            state.GuestMulliganDone = true;
            state.BeginFirstRound();

            return state;
        }

        private static CardDefinition Definition(
            string id,
            params AbilityDefinition[] abilities)
        {
            return new CardDefinition(
                id,
                id,
                3,
                2,
                1,
                abilities: abilities);
        }

        private static AbilityDefinition Ability(
            string id,
            AbilityTrigger trigger,
            AbilityCondition condition =
                AbilityCondition.None,
            bool oncePerRound = false)
        {
            return new AbilityDefinition(
                id,
                trigger,
                new[]
                {
                    new EffectDefinition(
                        EffectKind.Draw,
                        EffectTarget.SourceOwner,
                        amount: 1)
                },
                condition,
                oncePerRound);
        }
    }
}