using System;
using KLTN.Game.Domain;
using KLTN.Game.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KLTN.Game.Presentation
{
    public sealed class MatchDebugControls : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField] private NetworkMatchBridge bridge;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private TMP_Text localStatusLabel;

        #endregion

        #region Runtime State

        private MatchClientProjection projection;
        private DraggableHandCard pendingCard;
        private string pendingCardInstanceId;
        private int pendingSlotIndex = -1;
        private int pendingManaCost;
        private bool submitting;

        #endregion

        #region Public State

        public int PendingManaCost => pendingManaCost;
        public event Action<int> PendingManaCostChanged;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            projection = MatchProjectionRegistry.Current;
            projection.SnapshotChanged += OnSnapshotChanged;
            projection.CommandRejected += OnCommandRejected;

            if (actionButton != null) { actionButton.onClick.AddListener(CommitOrPass); }

            SetLocalStatus(string.Empty);
            SetPendingManaCost(0);
            RefreshControls();
        }

        private void OnDisable()
        {
            if (projection != null)
            {
                projection.SnapshotChanged -= OnSnapshotChanged;
                projection.CommandRejected -= OnCommandRejected;
            }

            if (actionButton != null) { actionButton.onClick.RemoveListener(CommitOrPass); }
        }

        #endregion

        #region Staging API

        public bool TryStageCard(DraggableHandCard card, int slotIndex, RectTransform slotContent)
        {
            MatchSnapshotDto snapshot = projection?.Current;

            if (card == null ||
                snapshot == null ||
                !snapshot.viewerCanAct ||
                submitting)
            {
                SetLocalStatus("Hiện chưa thể đánh bài.");
                return false;
            }

            if (slotIndex < 0 || slotIndex >= 3) { return false; }

            if (!IsCardInSelfHand(snapshot, card.Visual.InstanceId))
            {
                SetLocalStatus("Lá bài không còn trên tay.");
                return false;
            }

            if (card.Visual.Energy > snapshot.self.mana)
            {
                SetLocalStatus("Không đủ mana.");
                return false;
            }

            if (IsSelfSlotOccupied(snapshot, slotIndex))
            {
                SetLocalStatus($"Vị trí {slotIndex + 1} đã có bài.");
                return false;
            }

            if (pendingCard != null &&
                pendingCard != card)
            {
                pendingCard.RestoreToHand();
            }

            pendingCard = card;
            pendingCardInstanceId = card.Visual.InstanceId;
            pendingSlotIndex = slotIndex;
            card.PlacePending(slotContent);
            SetPendingManaCost(card.Visual.Energy);
            SetLocalStatus($"Đang chọn bài ở vị trí {slotIndex + 1}. Chưa commit.");
            RefreshControls();
            return true;
        }

        public void ClearPendingSelection()
        {
            if (pendingCard != null) { pendingCard.RestoreToHand(); }

            ClearPendingReferences();
            SetLocalStatus("Đã bỏ chọn bài.");
            RefreshControls();
        }

        public bool TryReturnPendingCard(DraggableHandCard card)
        {
            if (card == null ||
                card != pendingCard ||
                !card.IsPending ||
                submitting)
            {
                return false;
            }

            card.RestoreToHand();
            ClearPendingReferences();
            SetLocalStatus("Đã đưa lá bài về tay.");
            RefreshControls();
            return true;
        }

        #endregion

        #region Command Submission

        private void CommitOrPass()
        {
            MatchSnapshotDto snapshot = projection?.Current;

            if (snapshot == null ||
                !snapshot.viewerCanAct ||
                submitting ||
                bridge == null)
            {
                return;
            }

            bool sent;

            if (pendingCard == null) { sent = bridge.RequestPass(); }
            else
            {
                sent = bridge.RequestPlayUnit(pendingCardInstanceId, pendingSlotIndex);
            }

            if (!sent)
            {
                SetLocalStatus("Không thể gửi command.");
                return;
            }

            submitting = true;
            SetLocalStatus("Đang chờ host xác nhận...");
            RefreshControls();
        }

        #endregion

        #region Projection Events

        private void OnSnapshotChanged(MatchSnapshotDto snapshot)
        {
            submitting = false;

            // The presenter rebuilds all visuals from the new snapshot.
            ClearPendingReferences();
            RefreshControls();
        }

        private void OnCommandRejected(CommandRejectionReason reason)
        {
            submitting = false;

            if (pendingCard != null) { pendingCard.RestoreToHand(); }

            ClearPendingReferences();
            SetLocalStatus($"Host từ chối: {reason}");
            RefreshControls();
        }

        #endregion

        #region Pending State

        private void ClearPendingReferences()
        {
            pendingCard = null;
            pendingCardInstanceId = null;
            pendingSlotIndex = -1;
            SetPendingManaCost(0);
        }

        private void SetPendingManaCost(int value)
        {
            int sanitizedValue = Mathf.Max(0, value);

            if (pendingManaCost == sanitizedValue) { return; }

            pendingManaCost = sanitizedValue;
            PendingManaCostChanged?.Invoke(pendingManaCost);
        }

        #endregion

        #region Control Rendering

        private void RefreshControls()
        {
            MatchSnapshotDto snapshot = projection?.Current;
            bool canAct = snapshot != null && snapshot.viewerCanAct && !submitting;

            if (actionButton != null) { actionButton.interactable = canAct; }

            if (actionLabel == null) { return; }

            if (submitting) { actionLabel.text = "ĐANG XỬ LÝ"; }
            else if (!canAct)
            {
                actionLabel.text = "ĐỢI ĐỐI THỦ";
            }
            else if (pendingCard != null)
            {
                actionLabel.text = "ĐÁNH BÀI";
            }
            else
            {
                actionLabel.text = "BỎ LƯỢT";
            }
        }

        #endregion

        #region Validation Helpers

        private static bool IsCardInSelfHand(MatchSnapshotDto snapshot, string instanceId)
        {
            foreach (CardViewDto card in snapshot.self.hand)
            {
                if (card.instanceId == instanceId) { return true; }
            }

            return false;
        }

        private static bool IsSelfSlotOccupied(MatchSnapshotDto snapshot, int slotIndex)
        {
            foreach (CardViewDto card in snapshot.self.board)
            {
                if (card.boardSlotIndex == slotIndex) { return true; }
            }

            return false;
        }

        #endregion

        #region Status Output

        private void SetLocalStatus(string message)
        {
            if (localStatusLabel != null) { localStatusLabel.text = message; }
        }

        #endregion
    }
}
