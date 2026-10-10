using KLTN.Game.Domain.Economy;
using NUnit.Framework;

namespace KLTN.Game.Domain.Tests
{
    public sealed class MatchRewardPolicyTests
    {
        [Test]
        public void Winner_Gets75CoinsAnd1Silver()
        {
            MatchReward reward = MatchRewardPolicy.Compute(MatchOutcome.HostWon, SeatId.Host, null, 6);

            Assert.AreEqual(MatchResultKind.Win, reward.Kind);
            Assert.AreEqual(75, reward.Coins);
            Assert.AreEqual(1, reward.Silver);
        }

        [Test]
        public void Loser_Gets25Coins()
        {
            MatchReward reward = MatchRewardPolicy.Compute(MatchOutcome.HostWon, SeatId.Guest, null, 6);

            Assert.AreEqual(MatchResultKind.Loss, reward.Kind);
            Assert.AreEqual(25, reward.Coins);
            Assert.AreEqual(0, reward.Silver);
        }

        [Test]
        public void Draw_CountsAsLoss()
        {
            MatchReward reward = MatchRewardPolicy.Compute(MatchOutcome.Draw, SeatId.Host, null, 8);

            Assert.AreEqual(MatchResultKind.Draw, reward.Kind);
            Assert.AreEqual(25, reward.Coins);
            Assert.AreEqual(0, reward.Silver);
        }

        [Test]
        public void EarlySurrender_GetsNothing()
        {
            MatchReward reward = MatchRewardPolicy.Compute(MatchOutcome.GuestWon, SeatId.Host, SeatId.Host, 2);

            Assert.AreEqual(MatchResultKind.EarlySurrender, reward.Kind);
            Assert.AreEqual(0, reward.Coins);
        }

        [Test]
        public void SurrenderFromRound3_GetsLossReward()
        {
            MatchReward reward = MatchRewardPolicy.Compute(MatchOutcome.GuestWon, SeatId.Host, SeatId.Host, 3);

            Assert.AreEqual(MatchResultKind.Loss, reward.Kind);
            Assert.AreEqual(25, reward.Coins);
        }

        [Test]
        public void OpponentSurrenderedEarly_WinnerStillGetsFullReward()
        {
            MatchReward reward = MatchRewardPolicy.Compute(MatchOutcome.GuestWon, SeatId.Guest, SeatId.Host, 1);

            Assert.AreEqual(MatchResultKind.Win, reward.Kind);
            Assert.AreEqual(75, reward.Coins);
            Assert.AreEqual(1, reward.Silver);
        }

        [Test]
        public void RunningMatch_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(() =>
                MatchRewardPolicy.Compute(MatchOutcome.Running, SeatId.Host, null, 1)
            );
        }
    }
}
