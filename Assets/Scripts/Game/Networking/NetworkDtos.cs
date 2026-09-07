using System;

namespace KLTN.Game.Networking
{
    [Serializable]
    public sealed class CardViewDto
    {
        #region Identity

        public string instanceId;
        public string definitionId;
        public string displayName;

        #endregion

        #region Public Card State

        public int health;
        public int damage;
        public int energy;

        // -1 while in hand, 0-2 while on the board.
        public int boardSlotIndex = -1;

        #endregion
    }

    [Serializable]
    public sealed class PlayerViewDto
    {
        #region Identity and Connection

        public int seat;
        public string displayName;
        public bool connected;

        #endregion

        #region Resources and Counts

        public int nexusHealth;
        public int mana;
        public int maxMana;
        public int deckCount;
        public int handCount;

        #endregion

        #region Viewer-Visible Cards

        public CardViewDto[] hand;
        public CardViewDto[] board;

        #endregion
    }

    [Serializable]
    public sealed class MatchSnapshotDto
    {
        #region Snapshot Identity

        public ulong revision;
        public int viewerSeat;

        #endregion

        #region Public Match State

        public int firstSeat;
        public int activeSeat;
        public int roundNumber;
        public int actionsCompletedInRound;
        public int outcome;
        public bool viewerCanAct;

        #endregion

        #region Viewer Projection

        public PlayerViewDto self;
        public PlayerViewDto opponent;
        public string status;

        #endregion
    }

    [Serializable]
    public sealed class ResolvedCardDto
    {
        #region Card Identity and State

        public int seat;
        public CardViewDto cardBefore;
        public int healthAfter;

        #endregion
    }

    [Serializable]
    public sealed class CombatStepDto
    {
        #region Slot

        public int slotIndex;

        #endregion

        #region Cards

        public ResolvedCardDto hostCard;
        public ResolvedCardDto guestCard;

        #endregion

        #region Nexus Health

        public int hostNexusHealthBefore;
        public int hostNexusHealthAfter;
        public int guestNexusHealthBefore;
        public int guestNexusHealthAfter;

        #endregion
    }

    [Serializable]
    public sealed class RoundResolutionDto
    {
        #region Round Resolution

        public int roundNumber;
        public CombatStepDto[] steps;

        #endregion
    }

    [Serializable]
    public sealed class MatchUpdateDto
    {
        #region Authoritative Update

        public MatchSnapshotDto snapshot;
        public bool hasResolution;
        public RoundResolutionDto resolution;

        #endregion
    }
}
