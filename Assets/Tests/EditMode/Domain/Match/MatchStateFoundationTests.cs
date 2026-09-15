using System;
using KLTN.Game.Domain;
using NUnit.Framework;

namespace KLTN.Game.Domain.Tests
{
    public sealed class MatchStateFoundationTests
    {
        #region Zone Tests

        [Test]
        public void MoveDrawHandCardToReserve_UpdatesZoneAndCollections()
        {
            var player = new PlayerState(SeatId.Host);
            var card = new CardInstance(
                1,
                "unit",
                SeatId.Host,
                CardZone.DrawHand,
                3);

            player.DrawHand.Add(card);

            bool moved =
                player.TryMoveDrawHandCardToReserve(card);

            Assert.IsTrue(moved);
            Assert.AreEqual(CardZone.Reserve, card.Zone);
            Assert.AreEqual(0, player.DrawHand.Count);
            Assert.AreEqual(1, player.Reserve.Count);
            Assert.AreEqual(1, player.ActiveRosterCount);
            Assert.AreEqual(-1, card.BoardSlotIndex);
        }

        [Test]
        public void MoveDrawHandCardToReserve_RejectsFifthActiveUnit()
        {
            var player = new PlayerState(SeatId.Host);

            for (ulong id = 1; id <= 5; id++)
            {
                player.DrawHand.Add(
                    new CardInstance(
                        id,
                        "unit",
                        SeatId.Host,
                        CardZone.DrawHand,
                        3));
            }

            for (int i = 0;
                 i < MatchState.MaximumActiveRosterSize;
                 i++)
            {
                CardInstance card = player.DrawHand[0];

                Assert.IsTrue(
                    player.TryMoveDrawHandCardToReserve(card));
            }

            CardInstance fifthCard = player.DrawHand[0];

            bool moved =
                player.TryMoveDrawHandCardToReserve(fifthCard);

            Assert.IsFalse(moved);
            Assert.AreEqual(CardZone.DrawHand, fifthCard.Zone);
            Assert.AreEqual(1, player.DrawHand.Count);
            Assert.AreEqual(4, player.Reserve.Count);
            Assert.AreEqual(4, player.ActiveRosterCount);
        }

        [Test]
        public void MoveReserveCardToBoard_PreservesActiveRosterCount()
        {
            var player = new PlayerState(SeatId.Host);
            var card = new CardInstance(
                1,
                "unit",
                SeatId.Host,
                CardZone.DrawHand,
                3);

            player.DrawHand.Add(card);

            Assert.IsTrue(
                player.TryMoveDrawHandCardToReserve(card));

            Assert.IsTrue(
                player.TryMoveReserveCardToBoard(card, 2));

            Assert.AreEqual(CardZone.Board, card.Zone);
            Assert.AreEqual(2, card.BoardSlotIndex);
            Assert.AreEqual(0, player.Reserve.Count);
            Assert.AreEqual(1, player.Board.Count);
            Assert.AreEqual(1, player.ActiveRosterCount);

            Assert.IsTrue(
                player.TryMoveBoardCardToReserve(card));

            Assert.AreEqual(CardZone.Reserve, card.Zone);
            Assert.AreEqual(-1, card.BoardSlotIndex);
            Assert.AreEqual(1, player.Reserve.Count);
            Assert.AreEqual(0, player.Board.Count);
            Assert.AreEqual(1, player.ActiveRosterCount);
        }

        [Test]
        public void MoveReserveCardToBoard_RejectsOccupiedSlot()
        {
            var player = new PlayerState(SeatId.Host);

            var first = new CardInstance(
                1,
                "first",
                SeatId.Host,
                CardZone.DrawHand,
                3);

            var second = new CardInstance(
                2,
                "second",
                SeatId.Host,
                CardZone.DrawHand,
                3);

            player.DrawHand.Add(first);
            player.DrawHand.Add(second);

            Assert.IsTrue(
                player.TryMoveDrawHandCardToReserve(first));

            Assert.IsTrue(
                player.TryMoveDrawHandCardToReserve(second));

            Assert.IsTrue(
                player.TryMoveReserveCardToBoard(first, 0));

            bool moved =
                player.TryMoveReserveCardToBoard(second, 0);

            Assert.IsFalse(moved);
            Assert.AreEqual(CardZone.Reserve, second.Zone);
            Assert.AreEqual(1, player.Board.Count);
            Assert.AreEqual(1, player.Reserve.Count);
        }

        [Test]
        public void ReturnBoardCardsToReserve_PreservesSlotOrder()
        {
            var player =
                new PlayerState(SeatId.Host);

            var rightCard =
                new CardInstance(
                    1,
                    "right",
                    SeatId.Host,
                    CardZone.DrawHand,
                    3);

            var leftCard =
                new CardInstance(
                    2,
                    "left",
                    SeatId.Host,
                    CardZone.DrawHand,
                    4);

            player.DrawHand.Add(rightCard);
            player.DrawHand.Add(leftCard);

            Assert.IsTrue(
                player.TryMoveDrawHandCardToReserve(
                    rightCard));

            Assert.IsTrue(
                player.TryMoveDrawHandCardToReserve(
                    leftCard));

            Assert.IsTrue(
                player.TryMoveReserveCardToBoard(
                    rightCard,
                    2));

            Assert.IsTrue(
                player.TryMoveReserveCardToBoard(
                    leftCard,
                    0));

            player.ReturnBoardCardsToReserve();

            Assert.AreEqual(0, player.Board.Count);
            Assert.AreEqual(2, player.Reserve.Count);

            Assert.AreSame(leftCard, player.Reserve[0]);
            Assert.AreSame(rightCard, player.Reserve[1]);

            Assert.AreEqual(CardZone.Reserve, leftCard.Zone);
            Assert.AreEqual(CardZone.Reserve, rightCard.Zone);

            Assert.AreEqual(-1, leftCard.BoardSlotIndex);
            Assert.AreEqual(-1, rightCard.BoardSlotIndex);

            // Calling it again must not duplicate cards.
            player.ReturnBoardCardsToReserve();

            Assert.AreEqual(0, player.Board.Count);
            Assert.AreEqual(2, player.Reserve.Count);
        }

        [Test]
        public void ReturnBoardCardsToReserve_MovesDeadCardToGraveyard()
        {
            var player =
                new PlayerState(SeatId.Guest);

            var deadCard =
                new CardInstance(
                    10,
                    "dead",
                    SeatId.Guest,
                    CardZone.Board,
                    0);

            deadCard.MoveTo(
                CardZone.Board,
                boardSlotIndex: 1);

            player.Board.Add(deadCard);

            player.ReturnBoardCardsToReserve();

            Assert.AreEqual(0, player.Board.Count);
            Assert.AreEqual(0, player.Reserve.Count);
            Assert.AreEqual(1, player.Graveyard.Count);

            Assert.AreSame(
                deadCard,
                player.Graveyard[0]);

            Assert.AreEqual(
                CardZone.Graveyard,
                deadCard.Zone);
        }

        #endregion

        #region Match State Tests

        [Test]
        public void MatchState_StartsInMulliganWithoutAvailableToken()
        {
            var state = new MatchState(SeatId.Guest);

            Assert.AreEqual(MatchPhase.Mulligan, state.Phase);
            Assert.AreEqual(SeatId.Guest, state.ActiveSeat);
            Assert.AreEqual(
                SeatId.Guest,
                state.AttackTokenOwner);

            Assert.IsFalse(state.AttackTokenAvailable);
            Assert.AreEqual(0, state.RoundNumber);
            Assert.AreEqual(0, state.ConsecutivePasses);
        }

        [Test]
        public void BeginFirstRound_RequiresCompletedMulligan()
        {
            var state = new MatchState(SeatId.Host);

            Assert.Throws<InvalidOperationException>(
                () => state.BeginFirstRound());
        }

        [Test]
        public void BeginFirstRound_InitializesPriorityAndAttackToken()
        {
            var state = new MatchState(SeatId.Guest)
            {
                HostMulliganDone = true,
                GuestMulliganDone = true
            };

            state.BeginFirstRound();

            Assert.AreEqual(1, state.RoundNumber);
            Assert.AreEqual(MatchPhase.Priority, state.Phase);
            Assert.AreEqual(SeatId.Guest, state.ActiveSeat);
            Assert.AreEqual(
                SeatId.Guest,
                state.AttackTokenOwner);

            Assert.IsTrue(state.AttackTokenAvailable);
            Assert.AreEqual(0, state.ConsecutivePasses);
        }

        [Test]
        public void BeginFirstRound_IsIdempotent()
        {
            var state = new MatchState(SeatId.Host)
            {
                HostMulliganDone = true,
                GuestMulliganDone = true
            };

            state.BeginFirstRound();
            state.BeginFirstRound();

            Assert.AreEqual(1, state.RoundNumber);
            Assert.AreEqual(SeatId.Host, state.ActiveSeat);
            Assert.IsTrue(state.AttackTokenAvailable);
        }

        #endregion
    }
}
