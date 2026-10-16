using KLTN.Game.Domain.Ranking;
using NUnit.Framework;

namespace KLTN.Game.Domain.Tests
{
    public sealed class RankPointsPolicyTests
    {
        [Test]
        public void Winner_Gets30()
        {
            RankedResult result = RankPointsPolicy.Compute(MatchOutcome.HostWon, SeatId.Host, null);

            Assert.AreEqual(RankedResultKind.Win, result.Kind);
            Assert.AreEqual(30, result.Delta);
        }

        [Test]
        public void Loser_Loses20()
        {
            RankedResult result = RankPointsPolicy.Compute(MatchOutcome.HostWon, SeatId.Guest, null);

            Assert.AreEqual(RankedResultKind.Loss, result.Kind);
            Assert.AreEqual(-20, result.Delta);
        }

        [Test]
        public void Draw_IsZero()
        {
            RankedResult result = RankPointsPolicy.Compute(MatchOutcome.Draw, SeatId.Guest, null);

            Assert.AreEqual(RankedResultKind.Draw, result.Kind);
            Assert.AreEqual(0, result.Delta);
        }

        [Test]
        public void OwnSurrender_Loses20_AndOpponentGetsFullWin()
        {
            RankedResult loser = RankPointsPolicy.Compute(MatchOutcome.GuestWon, SeatId.Host, SeatId.Host);
            RankedResult winner = RankPointsPolicy.Compute(MatchOutcome.GuestWon, SeatId.Guest, SeatId.Host);

            Assert.AreEqual(RankedResultKind.Surrender, loser.Kind);
            Assert.AreEqual(-20, loser.Delta);
            Assert.AreEqual(RankedResultKind.Win, winner.Kind);
            Assert.AreEqual(30, winner.Delta);
        }

        [Test]
        public void RunningMatch_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => RankPointsPolicy.Compute(MatchOutcome.Running, SeatId.Host, null)
            );
        }

        [TestCase(0, -20, 0)]
        [TestCase(10, -20, 0)]
        [TestCase(20, -20, 0)]
        [TestCase(50, -20, 30)]
        [TestCase(0, 30, 30)]
        public void ApplyDelta_FloorsAtZero(int points, int delta, int expected)
        {
            Assert.AreEqual(expected, RankPointsPolicy.ApplyDelta(points, delta));
        }

        [Test]
        public void DeltaFor_MatchesCompute()
        {
            foreach (RankedResultKind kind in System.Enum.GetValues(typeof(RankedResultKind)))
            {
                Assert.IsNotEmpty(RankPointsPolicy.WireName(kind));
            }

            Assert.AreEqual(RankPointsPolicy.WinPoints, RankPointsPolicy.DeltaFor(RankedResultKind.Win));
            Assert.AreEqual(RankPointsPolicy.LossPoints, RankPointsPolicy.DeltaFor(RankedResultKind.Loss));
            Assert.AreEqual(RankPointsPolicy.DrawPoints, RankPointsPolicy.DeltaFor(RankedResultKind.Draw));
            Assert.AreEqual(RankPointsPolicy.SurrenderPoints, RankPointsPolicy.DeltaFor(RankedResultKind.Surrender));
        }
    }
}
