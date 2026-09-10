using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public sealed class CardCombatResolution
    {
        #region Properties

        public SeatId Seat { get; }
        public ulong CardInstanceId { get; }
        public int HealthBefore { get; }
        public int HealthAfter { get; }

        public int DamageTaken => Math.Max(0, HealthBefore - HealthAfter);
        public bool Died => HealthBefore > 0 && HealthAfter <= 0;

        #endregion

        #region Construction

        public CardCombatResolution(
            SeatId seat,
            ulong cardInstanceId,
            int healthBefore,
            int healthAfter)
        {
            Seat = seat;
            CardInstanceId = cardInstanceId;
            HealthBefore = Math.Max(0, healthBefore);
            HealthAfter = Math.Max(0, healthAfter);
        }

        #endregion
    }

    public sealed class CombatStep
    {
        #region Properties

        public int SlotIndex { get; }

        public CardCombatResolution HostCard { get; }
        public CardCombatResolution GuestCard { get; }

        public int HostNexusHealthBefore { get; }
        public int HostNexusHealthAfter { get; }
        public int GuestNexusHealthBefore { get; }
        public int GuestNexusHealthAfter { get; }

        public int HostNexusDamageTaken =>
            Math.Max(0, HostNexusHealthBefore - HostNexusHealthAfter);

        public int GuestNexusDamageTaken =>
            Math.Max(0, GuestNexusHealthBefore - GuestNexusHealthAfter);

        public bool IsUnitCombat => HostCard != null && GuestCard != null;
        public bool IsDirectAttack => HostCard == null || GuestCard == null;

        #endregion

        #region Construction

        public CombatStep(
            int slotIndex,
            CardCombatResolution hostCard,
            CardCombatResolution guestCard,
            int hostNexusHealthBefore,
            int hostNexusHealthAfter,
            int guestNexusHealthBefore,
            int guestNexusHealthAfter)
        {
            SlotIndex = slotIndex;
            HostCard = hostCard;
            GuestCard = guestCard;
            HostNexusHealthBefore = hostNexusHealthBefore;
            HostNexusHealthAfter = hostNexusHealthAfter;
            GuestNexusHealthBefore = guestNexusHealthBefore;
            GuestNexusHealthAfter = guestNexusHealthAfter;
        }

        #endregion
    }

    public sealed class RoundResolution
    {
        #region Properties

        public int RoundNumber { get; }
        public IReadOnlyList<CombatStep> Steps { get; }

        #endregion

        #region Construction

        public RoundResolution(int roundNumber, IReadOnlyList<CombatStep> steps)
        {
            RoundNumber = roundNumber;
            Steps = new List<CombatStep>(
                steps ?? throw new ArgumentNullException(nameof(steps)));
        }

        #endregion
    }
}