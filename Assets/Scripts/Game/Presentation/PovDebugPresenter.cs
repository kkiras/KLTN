using KLTN.Game.Domain;
using KLTN.Game.Networking;
using TMPro;
using UnityEngine;

namespace KLTN.Game.Presentation
{
    public sealed class PovDebugPresenter : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField] private TMP_Text debugLabel;

        #endregion

        #region Runtime State

        private MatchClientProjection projection;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            projection = MatchProjectionRegistry.Current;
            projection.SnapshotChanged += Render;

            if (projection.Current != null) { Render(projection.Current); }
            else
            {
                ShowWaiting();
            }
        }

        private void OnDisable()
        {
            if (projection != null) { projection.SnapshotChanged -= Render; }
        }

        #endregion

        #region Rendering

        private void Render(MatchSnapshotDto snapshot)
        {
            if (debugLabel == null ||
                snapshot == null ||
                snapshot.self == null ||
                snapshot.opponent == null)
            {
                return;
            }

            SeatId viewerSeat = (SeatId)snapshot.viewerSeat;
            SeatId selfSeat = (SeatId)snapshot.self.seat;
            SeatId opponentSeat = (SeatId)snapshot.opponent.seat;
            int revealedOpponentCards = snapshot.opponent.hand?.Length ?? 0;

            debugLabel.text =
                $"POV: {viewerSeat}\n" +
                $"SELF (phía dưới): {selfSeat}\n" +
                $"Hand: {snapshot.self.handCount}\n" +
                $"Deck: {snapshot.self.deckCount}\n" +

                $"OPPONENT (phía trên): {opponentSeat}\n" +
                $"Opponent hand count: {snapshot.opponent.handCount}\n" +
                $"Opponent revealed cards: {revealedOpponentCards}\n" +
                $"Opponent deck: {snapshot.opponent.deckCount}\n\n" +

                $"Revision: {snapshot.revision}\n" +

                $"Round: {snapshot.roundNumber}\n" +

                $"First: {(SeatId)snapshot.firstSeat}\n" +
                $"Active: {(SeatId)snapshot.activeSeat}\n" +
                $"Can act: {snapshot.viewerCanAct}\n" +
                $"Outcome: {(MatchOutcome)snapshot.outcome}\n\n" +

                $"Self board: {BoardText(snapshot.self.board)}\n" +
                $"Opponent board: {BoardText(snapshot.opponent.board)}\n\n" +

                snapshot.status;
        }

        private void ShowWaiting()
        {
            if (debugLabel != null) { debugLabel.text = "Đang chờ snapshot POV từ host..."; }
        }

        #endregion

        #region Helpers

        private static string BoardText(CardViewDto[] cards)
        {
            if (cards == null || cards.Length == 0) { return "trống"; }

            var parts = new string[cards.Length];

            for (int i = 0; i < cards.Length; i++)
            {
                CardViewDto card = cards[i];

                parts[i] =
                    $"[{card.boardSlotIndex + 1}] " +
                    $"{card.displayName} " +
                    $"{card.damage}/{card.health}";
            }

            return string.Join(", ", parts);
        }

        #endregion
    }
}
