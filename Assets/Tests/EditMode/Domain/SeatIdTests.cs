using KLTN.Game.Domain;
using NUnit.Framework;

namespace KLTN.Game.Domain.Tests
{
    public sealed class SeatIdTests
    {
        #region Opponent Mapping

        [Test]
        public void HostOpponent_IsGuest()
        {
            Assert.AreEqual(SeatId.Guest, SeatId.Host.Opponent());
        }

        [Test]
        public void GuestOpponent_IsHost()
        {
            Assert.AreEqual(SeatId.Host, SeatId.Guest.Opponent());
        }

        #endregion
    }
}
