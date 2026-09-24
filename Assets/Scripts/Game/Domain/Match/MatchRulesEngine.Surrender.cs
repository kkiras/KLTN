namespace KLTN.Game.Domain
{
    public sealed partial class MatchRulesEngine
    {
        /// <summary>
        /// Ends the match immediately when a player surrenders.
        /// The opponent is declared the winner.
        /// </summary>
        public CommandResult TrySurrender(
            MatchState state,
            SeatId actor
        )
        {
            if (state == null)
            {
                return CommandResult.Reject(
                    CommandRejectionReason.MatchNotReady
                );
            }

            if (state.IsFinished)
            {
                return CommandResult.Reject(
                    CommandRejectionReason.MatchFinished
                );
            }

            if (actor != SeatId.Host && actor != SeatId.Guest)
            {
                return CommandResult.Reject(
                    CommandRejectionReason.InvalidSeat
                );
            }

            if (actor == SeatId.Host)
            {
                state.Outcome = MatchOutcome.GuestWon;
            }
            else
            {
                state.Outcome = MatchOutcome.HostWon;
            }

            state.Phase = MatchPhase.Finished;
            state.ConsecutivePasses = 0;

            state.LastEvent =
                $"{actor} đã đầu hàng. {actor.Opponent()} chiến thắng.";

            return CommandResult.Success();
        }
    }
}