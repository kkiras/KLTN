using System;

namespace KLTN.Game.Domain
{
    /// <summary>
    /// Identifies one of the two logical player positions in a match.
    /// This is not a network client ID and is not a Unity GameObject.
    /// </summary>
    public enum SeatId : byte
    {
        Host = 0,
        Guest = 1,
    }

    public static class SeatIdExtensions
    {
        #region Public API

        /// <summary>
        /// Returns the other seat in a two-player match.
        /// </summary>
        public static SeatId Opponent(this SeatId seat)
        {
            switch (seat)
            {
                case SeatId.Host:
                    return SeatId.Guest;

                case SeatId.Guest:
                    return SeatId.Host;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(seat),
                        seat,
                        "The seat is not valid for a two-player match.");
            }
        }

        #endregion
    }
}
