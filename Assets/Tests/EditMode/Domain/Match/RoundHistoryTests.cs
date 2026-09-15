using NUnit.Framework;

namespace KLTN.Game.Domain.Tests
{
    public sealed class RoundHistoryTests
    {
        [Test]
        public void RecordDeath_StoresCurrentRoundSnapshot()
        {
            var history =
                new RoundHistory();

            history.BeginFirstRound(1);

            var card =
                new CardInstance(
                    10,
                    "dead_unit",
                    SeatId.Host,
                    CardZone.Board,
                    3);

            card.ApplyDamage(3);

            DeathRecord result =
                history.RecordDeath(
                    card,
                    damageAtDeath: 4,
                    maximumHealthAtDeath: 3);

            Assert.AreEqual(
                1,
                history.CurrentRoundDeaths.Count);

            Assert.AreEqual(
                card.InstanceId,
                result.CardInstanceId);

            Assert.AreEqual(
                4,
                result.DamageAtDeath);

            Assert.AreEqual(
                3,
                result.MaximumHealthAtDeath);
        }

        [Test]
        public void AdvanceRound_MovesCurrentDeathsToPrevious()
        {
            var history =
                new RoundHistory();

            history.BeginFirstRound(1);

            var card =
                new CardInstance(
                    10,
                    "dead_unit",
                    SeatId.Host,
                    CardZone.Graveyard,
                    0);

            history.RecordDeath(card, 2, 3);
            history.AdvanceToRound(2);

            Assert.AreEqual(
                0,
                history.CurrentRoundDeaths.Count);

            Assert.AreEqual(
                1,
                history.PreviousRoundDeaths.Count);

            Assert.IsTrue(
                history.HasDeathPreviousRound(
                    SeatId.Host));
        }

        [Test]
        public void SameCard_CanDieMoreThanOnceInOneRound()
        {
            var history =
                new RoundHistory();

            history.BeginFirstRound(1);

            var card =
                new CardInstance(
                    10,
                    "reviving_unit",
                    SeatId.Host,
                    CardZone.Graveyard,
                    0);

            history.RecordDeath(card, 2, 3);
            history.RecordDeath(card, 3, 4);

            Assert.AreEqual(
                2,
                history.CurrentRoundDeaths.Count);

            Assert.AreNotEqual(
                history.CurrentRoundDeaths[0].Sequence,
                history.CurrentRoundDeaths[1].Sequence);
        }
    }
}
