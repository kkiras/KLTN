namespace KLTN.Game.Domain
{
    public sealed class RoundTransition
    {
        public int CompletedRoundNumber { get; }
        public int NextRoundNumber { get; }
        public SeatId NextAttackTokenOwner { get; }

        public RoundTransition(
            int completedRoundNumber,
            int nextRoundNumber,
            SeatId nextAttackTokenOwner)
        {
            CompletedRoundNumber =
                completedRoundNumber;

            NextRoundNumber =
                nextRoundNumber;

            NextAttackTokenOwner =
                nextAttackTokenOwner;
        }
    }
}