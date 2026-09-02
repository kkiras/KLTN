using System;
using System.Collections.Generic;
using KLTN.Game.Domain;

namespace KLTN.Game.Networking
{
    public sealed class MatchSnapshotBuilder
    {
        #region Fields

        private readonly IReadOnlyDictionary<string, CardDefinition>
            definitionsById;

        #endregion

        #region Construction

        public MatchSnapshotBuilder(IReadOnlyDictionary<string, CardDefinition> definitionsById)
        {
            this.definitionsById = definitionsById ??
                throw new ArgumentNullException(nameof(definitionsById));
        }

        #endregion

        #region Snapshot Construction

        public MatchSnapshotDto Build(MatchState state, SeatId viewerSeat, bool opponentConnected, ulong revision)
        {
            PlayerState self = state.Player(viewerSeat);
            PlayerState opponent = state.Player(viewerSeat.Opponent());

            return new MatchSnapshotDto
            {
                revision = revision,
                viewerSeat = (int)viewerSeat,

                firstSeat = (int)state.FirstSeat,
                activeSeat = (int)state.ActiveSeat,
                roundNumber = state.RoundNumber,
                actionsCompletedInRound = state.ActionsCompletedInRound,
                outcome = (int)state.Outcome,
                viewerCanAct = opponentConnected &&
                               !state.IsFinished &&
                               state.ActiveSeat == viewerSeat,
                self = BuildPlayer(
                    self,
                    revealHand: true,
                    connected: true
                ),
                opponent = BuildPlayer(
                    opponent,
                    revealHand: false,
                    connected: opponentConnected
                ),
                status = state.LastEvent
            };
        }

        public static MatchSnapshotDto BuildWaiting(SeatId viewerSeat, bool opponentConnected, ulong revision)
        {
            SeatId opponentSeat = viewerSeat.Opponent();

            return new MatchSnapshotDto
            {
                revision = revision,
                viewerSeat = (int)viewerSeat,

                firstSeat = -1,
                activeSeat = -1,
                roundNumber = 0,
                actionsCompletedInRound = 0,
                outcome = (int)MatchOutcome.Running,
                viewerCanAct = false,
                self = BuildWaitingPlayer(viewerSeat, connected: true),
                opponent = BuildWaitingPlayer(opponentSeat, opponentConnected),
                status = opponentConnected
                    ? "Đang khởi tạo trận đấu."
                    : "Đang chờ người chơi còn lại."
            };
        }

        #endregion

        #region Player and Card Mapping

        private PlayerViewDto BuildPlayer(PlayerState player, bool revealHand, bool connected)
        {
            return new PlayerViewDto
            {
                seat = (int)player.Seat,
                displayName = SeatName(player.Seat),
                connected = connected,

                nexusHealth = player.NexusHealth,
                mana = player.Mana,
                maxMana = player.MaxMana,
                deckCount = player.Deck.Count,
                handCount = player.Hand.Count,

                hand = revealHand
                    ? ConvertCards(player.Hand)
                    : Array.Empty<CardViewDto>(),

                board = ConvertCards(player.Board)
            };
        }

        private CardViewDto[] ConvertCards(IReadOnlyList<CardInstance> cards)
        {
            var result = new CardViewDto[cards.Count];

            for (int i = 0; i < cards.Count; i++)
            {
                CardInstance instance = cards[i];
                definitionsById.TryGetValue(instance.DefinitionId, out CardDefinition definition);

                result[i] = new CardViewDto
                {
                    instanceId = instance.InstanceId.ToString(),
                    definitionId = instance.DefinitionId,
                    displayName = definition?.DisplayName ??
                                  "Unknown card",

                    health = instance.CurrentHealth,
                    damage = definition?.BaseDamage ?? 0,
                    energy = definition?.Cost ?? 0,

                    boardSlotIndex = instance.BoardSlotIndex
                };
            }

            return result;
        }

        private static PlayerViewDto BuildWaitingPlayer(SeatId seat, bool connected)
        {
            return new PlayerViewDto
            {
                seat = (int)seat,
                displayName = SeatName(seat),
                connected = connected,

                nexusHealth = 20,
                mana = 0,
                maxMana = 0,
                deckCount = 0,
                handCount = 0,

                hand = Array.Empty<CardViewDto>(),
                board = Array.Empty<CardViewDto>()
            };
        }

        #endregion

        #region Helpers

        private static string SeatName(SeatId seat)
        {
            return seat == SeatId.Host ? "Host" : "Guest";
        }

        #endregion
    }
}
