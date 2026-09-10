namespace KLTN.Game.Domain
{
    public enum CommandRejectionReason : byte
    {
        None,
        MatchFinished,
        NotYourTurn,
        CardNotInHand,
        InsufficientMana,
        InvalidBoardSlot,
        BoardSlotOccupied,
        BoardFull,
        MissingCardDefinition,
        MatchNotReady,
        InvalidSeat,
        DuplicateCommand
    }

    public sealed class CommandResult
    {
        #region Properties

        public bool Accepted { get; }
        public CommandRejectionReason RejectionReason { get; }
        public RoundResolution Resolution { get; }

        #endregion

        #region Construction

        private CommandResult(bool accepted, CommandRejectionReason rejectionReason, RoundResolution resolution)
        {
            Accepted = accepted;
            RejectionReason = rejectionReason;
            Resolution = resolution;
        }

        #endregion

        #region Factory Methods

        public static CommandResult Success(RoundResolution resolution = null)
        {
            return new CommandResult(true, CommandRejectionReason.None, resolution);
        }

        public static CommandResult Reject(CommandRejectionReason reason)
        {
            return new CommandResult(false, reason, null);
        }

        #endregion
    }
}
