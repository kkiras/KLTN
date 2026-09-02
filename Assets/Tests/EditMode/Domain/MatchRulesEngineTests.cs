using System.Collections.Generic;
using KLTN.Game.Domain;
using NUnit.Framework;

namespace KLTN.Game.Domain.Tests
{
    public sealed class MatchRulesEngineTests
    {
        #region Test State

        private Dictionary<string, CardDefinition> definitions;
        private MatchRulesEngine engine;

        #endregion

        #region Setup

        [SetUp]
        public void SetUp()
        {
            definitions = new Dictionary<string, CardDefinition>
            {
                ["host_unit"] = new CardDefinition("host_unit", "Host Unit", 4, 3, 1),
                ["guest_unit"] = new CardDefinition("guest_unit", "Guest Unit", 2, 2, 1),
                ["small_unit"] = new CardDefinition("small_unit", "Small Unit", 10, 1, 1),
                ["expensive"] = new CardDefinition("expensive", "Expensive Unit", 5, 5, 2)
            };

            engine = new MatchRulesEngine(definitions);
        }

        #endregion

        #region Command and Resolution Tests

        [Test]
        public void PlayUnit_ConsumesManaAndChangesActiveSeat()
        {
            MatchState state = CreateState();

            CommandResult result = engine.TryPlayUnit(state, SeatId.Host, 1, 0);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(0, state.Host.Mana);
            Assert.AreEqual(0, state.Host.Hand.Count);
            Assert.AreEqual(1, state.Host.Board.Count);
            Assert.AreEqual(0, state.Host.Board[0].BoardSlotIndex);
            Assert.AreEqual(SeatId.Guest, state.ActiveSeat);
        }

        [Test]
        public void InsufficientMana_DoesNotChangeTurn()
        {
            MatchState state = CreateState();

            state.Host.Hand.Clear();
            state.Host.Hand.Add(new CardInstance(5, "expensive", SeatId.Host, CardZone.Hand, 5));

            CommandResult result = engine.TryPlayUnit(state, SeatId.Host, 5, 0);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(CommandRejectionReason.InsufficientMana, result.RejectionReason);

            Assert.AreEqual(SeatId.Host, state.ActiveSeat);
            Assert.AreEqual(1, state.Host.Mana);
            Assert.AreEqual(1, state.Host.Hand.Count);
            Assert.AreEqual(0, state.Host.Board.Count);
        }

        [Test]
        public void DifferentSlots_DamageBothNexuses()
        {
            MatchState state = CreateState();

            engine.TryPlayUnit(state, SeatId.Host, 1, 0);
            engine.TryPlayUnit(state, SeatId.Guest, 10, 1);

            Assert.AreEqual(18, state.Host.NexusHealth);
            Assert.AreEqual(17, state.Guest.NexusHealth);
            Assert.AreEqual(2, state.RoundNumber);
            Assert.AreEqual(SeatId.Host, state.ActiveSeat);
        }

        [Test]
        public void SameSlot_UnitsFightWithoutNexusDamage()
        {
            MatchState state = CreateState();

            engine.TryPlayUnit(state, SeatId.Host, 1, 0);
            engine.TryPlayUnit(state, SeatId.Guest, 10, 0);

            Assert.AreEqual(20, state.Host.NexusHealth);
            Assert.AreEqual(20, state.Guest.NexusHealth);

            Assert.AreEqual(1, state.Host.Board.Count);
            Assert.AreEqual(2, state.Host.Board[0].CurrentHealth);

            Assert.AreEqual(0, state.Guest.Board.Count);
            Assert.AreEqual(1, state.Guest.Graveyard.Count);
        }

        [Test]
        public void ExistingUnit_AttacksAgainWhenBothPass()
        {
            MatchState state = CreateState();

            engine.TryPlayUnit(state, SeatId.Host, 1, 0);
            engine.TryPass(state, SeatId.Guest);

            Assert.AreEqual(17, state.Guest.NexusHealth);

            engine.TryPass(state, SeatId.Host);
            engine.TryPass(state, SeatId.Guest);

            Assert.AreEqual(14, state.Guest.NexusHealth);
        }

        [Test]
        public void EverySecondRound_DrawsAndManaIncreases()
        {
            MatchState state = CreateState();

            int hostDeckBefore = state.Host.Deck.Count;
            int hostHandBefore = state.Host.Hand.Count;

            engine.TryPass(state, SeatId.Host);
            engine.TryPass(state, SeatId.Guest);

            Assert.AreEqual(2, state.Host.Mana);
            Assert.AreEqual(hostHandBefore, state.Host.Hand.Count);

            engine.TryPass(state, SeatId.Host);
            engine.TryPass(state, SeatId.Guest);

            Assert.AreEqual(3, state.Host.Mana);
            Assert.AreEqual(hostHandBefore + 1, state.Host.Hand.Count);
            Assert.AreEqual(hostDeckBefore - 1, state.Host.Deck.Count);
        }

        #endregion

        #region Test Data

        private static MatchState CreateState()
        {
            var state = new MatchState(SeatId.Host);

            state.Host.InitializeMana(1);
            state.Guest.InitializeMana(1);

            state.Host.Hand.Add(new CardInstance(1, "host_unit", SeatId.Host, CardZone.Hand, 4));

            state.Guest.Hand.Add(new CardInstance(10, "guest_unit", SeatId.Guest, CardZone.Hand, 2));

            for (ulong i = 0; i < 3; i++)
            {
                state.Host.Deck.Add(new CardInstance(100 + i, "small_unit", SeatId.Host, CardZone.Deck, 10));

                state.Guest.Deck.Add(new CardInstance(200 + i, "small_unit", SeatId.Guest, CardZone.Deck, 10));
            }

            return state;
        }

        #endregion
    }
}
