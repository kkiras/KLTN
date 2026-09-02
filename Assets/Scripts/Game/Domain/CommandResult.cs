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

        #endregion

        #region Construction

        private CommandResult(bool accepted, CommandRejectionReason rejectionReason)
        {
            Accepted = accepted;
            RejectionReason = rejectionReason;
        }

        #endregion

        #region Factory Methods

        public static CommandResult Success()
        {
            return new CommandResult(true, CommandRejectionReason.None);
        }

        public static CommandResult Reject(CommandRejectionReason reason)
        {
            return new CommandResult(false, reason);
        }

        #endregion
    }
}
