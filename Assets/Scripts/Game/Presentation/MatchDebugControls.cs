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

        [SerializeField]
        private NetworkMatchBridge bridge;

        [SerializeField]
        private Button actionButton;

        [SerializeField]
        private TMP_Text actionLabel;

        [SerializeField]
        private TMP_Text localStatusLabel;

        #endregion

        #region Runtime State

        private MatchClientProjection projection;
        private DraggableHandCard pendingSummonCard;
        private string pendingSummonCardInstanceId;

        private readonly DraggableHandCard[] pendingBoardCards = new DraggableHandCard[
            MatchState.BoardSlotCount
        ];
        private int pendingManaCost;
        private bool submitting;
        private string pendingAbilityRequestId;

        private string[] pendingAbilityPrimaryTargets = Array.Empty<string>();

        private string[] pendingAbilitySecondaryTargets = Array.Empty<string>();

        private bool pendingAbilitySelectionComplete;

        #endregion

        private bool HasPendingBoardDraft
        {
            get
            {
                for (int i = 0; i < pendingBoardCards.Length; i++)
                {
                    if (pendingBoardCards[i] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

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

            if (actionButton != null)
            {
                actionButton.onClick.AddListener(CommitOrPass);
            }

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

            if (actionButton != null)
            {
                actionButton.onClick.RemoveListener(CommitOrPass);
            }
        }

        #endregion

        #region Staging API

        public bool TryStageSummon(
            DraggableHandCard card,
            RectTransform reserveContentRoot
        )
        {
            MatchSnapshotDto snapshot = projection?.Current;

            if (card == null || snapshot == null || !snapshot.viewerCanAct || submitting)
            {
                SetLocalStatus("Hiện chưa thể triệu hồi bài.");

                return false;
            }

            if (HasPendingBoardDraft)
            {
                SetLocalStatus("Hãy hủy đội hình Board trước khi Summon.");

                return false;
            }

            if (!IsCardInSelfHand(snapshot, card.Visual.InstanceId))
            {
                SetLocalStatus("Lá bài không còn trong DrawHand.");

                return false;
            }

            if (snapshot.self.activeRosterCount >= MatchState.MaximumActiveRosterSize)
            {
                SetLocalStatus("Reserve đã đầy. Không thể triệu hồi thêm.");

                return false;
            }

            if (card.Visual.Energy > snapshot.self.mana)
            {
                SetLocalStatus("Không đủ mana.");
                return false;
            }

            if (pendingSummonCard != null && pendingSummonCard != card)
            {
                pendingSummonCard.RestoreToHome();
            }

            pendingSummonCard = card;
            pendingSummonCardInstanceId = card.Visual.InstanceId;

            card.PlacePending(reserveContentRoot, CardVisualLocation.Reserve);

            SetPendingManaCost(card.Visual.Energy);

            SetLocalStatus("Đang chọn bài để triệu hồi. Chưa commit.");

            RefreshControls();
            return true;
        }

        public bool TryStageBoardCard(
            DraggableHandCard card,
            int slotIndex,
            RectTransform slotContent
        )
        {
            MatchSnapshotDto snapshot = projection?.Current;

            bool isAttackDraft = snapshot != null && snapshot.viewerCanDeclareAttack;

            bool isBlockDraft = snapshot != null && snapshot.viewerCanDeclareBlock;

            if (
                card == null
                || slotContent == null
                || snapshot == null
                || (!isAttackDraft && !isBlockDraft)
                || submitting
            )
            {
                SetLocalStatus("Hiện chưa thể sắp xếp Board.");

                return false;
            }

            if (pendingSummonCard != null)
            {
                SetLocalStatus("Hãy commit hoặc hủy Summon trước.");

                return false;
            }

            if (slotIndex < 0 || slotIndex >= MatchState.BoardSlotCount)
            {
                return false;
            }

            if (card.HomeLocation != CardVisualLocation.Reserve)
            {
                SetLocalStatus("Lá bài phải nằm trong Reserve.");

                return false;
            }

            if (!IsCardInSelfReserve(snapshot, card.Visual.InstanceId))
            {
                SetLocalStatus("Lá bài không còn trong Reserve.");

                return false;
            }

            if (isBlockDraft && !HasOpponentAttackerAtSlot(snapshot, slotIndex))
            {
                SetLocalStatus($"Slot {slotIndex + 1} không có attacker.");

                return false;
            }

            ClearPendingBoardStatPreviews();

            int sourceSlot = FindPendingBoardSlot(card);

            DraggableHandCard targetCard = pendingBoardCards[slotIndex];

            if (sourceSlot == slotIndex)
            {
                card.PlacePending(slotContent, CardVisualLocation.Board);

                ApplyPendingBoardStatPreviews();

                return true;
            }

            RectTransform sourceContent = card.PendingParent;

            if (sourceSlot >= 0)
            {
                if (targetCard != null && sourceContent != null)
                {
                    pendingBoardCards[sourceSlot] = targetCard;

                    targetCard.MovePending(sourceContent, CardVisualLocation.Board);
                }
                else
                {
                    pendingBoardCards[sourceSlot] = null;

                    if (targetCard != null)
                    {
                        targetCard.RestoreToHome();
                    }
                }
            }
            else if (targetCard != null)
            {
                targetCard.RestoreToHome();
            }

            pendingBoardCards[slotIndex] = card;

            card.PlacePending(slotContent, CardVisualLocation.Board);

            ApplyPendingBoardStatPreviews();

            string roleText = isBlockDraft ? "blocker" : "attacker";

            SetLocalStatus(
                $"Đã đặt {roleText} vào slot {slotIndex + 1}. " + "Chưa commit."
            );

            RefreshControls();
            return true;
        }

        public bool TryReturnBoardCardToReserve(DraggableHandCard card)
        {
            if (
                card == null
                || card.HomeLocation != CardVisualLocation.Reserve
                || submitting
            )
            {
                return false;
            }

            ClearPendingBoardStatPreviews();

            int slotIndex = FindPendingBoardSlot(card);

            if (slotIndex >= 0)
            {
                pendingBoardCards[slotIndex] = null;
            }

            card.RestoreToHome();

            ApplyPendingBoardStatPreviews();

            SetLocalStatus("Đã đưa card trở lại Reserve.");

            RefreshControls();
            return true;
        }

        public void ClearPendingSelection()
        {
            RestorePendingVisuals();
            ClearPendingReferences();

            SetLocalStatus("Đã hủy action đang chọn.");

            RefreshControls();
        }

        public bool TryReturnPendingSummon(DraggableHandCard card)
        {
            if (
                card == null
                || card != pendingSummonCard
                || !card.IsPending
                || submitting
            )
            {
                return false;
            }

            card.RestoreToHome();

            pendingSummonCard = null;
            pendingSummonCardInstanceId = null;

            SetPendingManaCost(0);

            SetLocalStatus("Đã đưa lá bài về DrawHand.");

            RefreshControls();
            return true;
        }

        #endregion

        public void SetAbilityTargetDraft(
            string requestId,
            string[] primaryTargets,
            string[] secondaryTargets,
            bool complete
        )
        {
            pendingAbilityRequestId = requestId;

            pendingAbilityPrimaryTargets =
                primaryTargets == null
                    ? Array.Empty<string>()
                    : (string[])primaryTargets.Clone();

            pendingAbilitySecondaryTargets =
                secondaryTargets == null
                    ? Array.Empty<string>()
                    : (string[])secondaryTargets.Clone();

            pendingAbilitySelectionComplete = complete;

            RefreshControls();
        }

        public void ClearAbilityTargetDraft()
        {
            ClearAbilityTargetDraftInternal();
            RefreshControls();
        }

        public bool CancelPendingAbilitySelection(string requestId)
        {
            MatchSnapshotDto snapshot = projection?.Current;

            bool validRequest =
                snapshot != null
                && snapshot.viewerMustSelectAbilityTargets
                && snapshot.pendingAbilitySelection != null
                && snapshot.pendingAbilitySelection.canCancel
                && snapshot.pendingAbilitySelection.requestId == requestId;

            if (!validRequest || submitting || bridge == null)
            {
                return false;
            }

            // Must be set before sending because host RPC may update the snapshot immediately.
            submitting = true;
            SetLocalStatus("Đang chờ host hủy kỹ năng...");
            RefreshControls();

            bool sent = bridge.RequestCancelAbilitySelection(requestId);

            if (!sent)
            {
                submitting = false;
                SetLocalStatus("Không thể gửi yêu cầu hủy kỹ năng.");
                RefreshControls();
                return false;
            }

            return true;
        }

        #region Command Submission

        private void CommitOrPass()
        {
            MatchSnapshotDto snapshot = projection?.Current;

            if (snapshot != null && snapshot.roundNumber == 0)
            {
                return;
            }

            bool mustSelectAbilityTargets =
                snapshot != null
                && snapshot.viewerMustSelectAbilityTargets
                && snapshot.pendingAbilitySelection != null;

            bool abilityDraftReady =
                mustSelectAbilityTargets
                && pendingAbilitySelectionComplete
                && string.Equals(
                    pendingAbilityRequestId,
                    snapshot.pendingAbilitySelection.requestId,
                    StringComparison.Ordinal
                );

            bool canSubmit =
                snapshot != null
                && !submitting
                && bridge != null
                && (
                    mustSelectAbilityTargets
                        ? abilityDraftReady
                        : snapshot.viewerCanAct || snapshot.viewerCanDeclareBlock
                );

            if (!canSubmit)
            {
                return;
            }

            // Must be set before sending because a host RPC can complete synchronously.
            submitting = true;
            SetLocalStatus("Đang chờ host xác nhận...");
            RefreshControls();

            bool sent;

            if (mustSelectAbilityTargets)
            {
                sent = bridge.RequestSubmitAbilitySelection(
                    pendingAbilityRequestId,
                    pendingAbilityPrimaryTargets,
                    pendingAbilitySecondaryTargets
                );
            }
            else if (pendingSummonCard != null)
            {
                sent = bridge.RequestSummonUnit(pendingSummonCardInstanceId);
            }
            else if (snapshot.viewerCanDeclareBlock)
            {
                sent = bridge.RequestDeclareBlock(BuildBoardCardIdsBySlot());
            }
            else if (HasPendingBoardDraft)
            {
                sent = bridge.RequestDeclareAttack(BuildBoardCardIdsBySlot());
            }
            else
            {
                sent = bridge.RequestPass();
            }

            if (!sent)
            {
                submitting = false;
                SetLocalStatus("Không thể gửi command.");
                RefreshControls();
            }
        }

        #endregion

        #region Projection Events

        private void OnSnapshotChanged(MatchSnapshotDto snapshot)
        {
            bool completedLocalSubmission = submitting;
            submitting = false;

            if (completedLocalSubmission)
            {
                SetLocalStatus(string.Empty);
            }

            string authoritativeRequestId =
                snapshot != null
                && snapshot.viewerMustSelectAbilityTargets
                && snapshot.pendingAbilitySelection != null
                    ? snapshot.pendingAbilitySelection.requestId
                    : null;

            if (
                !string.Equals(
                    pendingAbilityRequestId,
                    authoritativeRequestId,
                    StringComparison.Ordinal
                )
            )
            {
                ClearAbilityTargetDraftInternal();
            }

            ClearPendingReferences();
            RefreshControls();
        }

        private void OnCommandRejected(CommandRejectionReason reason)
        {
            submitting = false;

            RestorePendingVisuals();
            ClearPendingReferences();

            SetLocalStatus($"Host từ chối: {reason}");

            RefreshControls();
        }

        #endregion

        #region Pending State

        private void ClearAbilityTargetDraftInternal()
        {
            pendingAbilityRequestId = null;

            pendingAbilityPrimaryTargets = Array.Empty<string>();

            pendingAbilitySecondaryTargets = Array.Empty<string>();

            pendingAbilitySelectionComplete = false;
        }

        private void ClearPendingReferences()
        {
            ClearPendingBoardStatPreviews();

            pendingSummonCard = null;
            pendingSummonCardInstanceId = null;

            Array.Clear(pendingBoardCards, 0, pendingBoardCards.Length);

            SetPendingManaCost(0);
        }

        private void ClearPendingBoardStatPreviews()
        {
            for (int i = 0; i < pendingBoardCards.Length; i++)
            {
                DraggableHandCard card = pendingBoardCards[i];

                if (card != null && card.Visual != null)
                {
                    card.Visual.ClearStatPreview();
                }
            }
        }

        private void ApplyPendingBoardStatPreviews()
        {
            MatchSnapshotDto snapshot = projection?.Current;

            if (snapshot == null || !snapshot.viewerCanDeclareAttack)
            {
                return;
            }

            for (
                int supporterSlot = 0;
                supporterSlot < pendingBoardCards.Length - 1;
                supporterSlot++
            )
            {
                DraggableHandCard supporter = pendingBoardCards[supporterSlot];
                DraggableHandCard supportedUnit = pendingBoardCards[supporterSlot + 1];

                if (
                    supporter == null
                    || supportedUnit == null
                    || supporter.Visual == null
                    || supportedUnit.Visual == null
                )
                {
                    continue;
                }

                int damageBonus = supporter.Visual.SupportDamageBonus;
                int healthBonus = supporter.Visual.SupportHealthBonus;

                if (damageBonus == 0 && healthBonus == 0)
                {
                    continue;
                }

                supportedUnit.Visual.SetStatPreview(damageBonus, healthBonus);
            }
        }

        private void SetPendingManaCost(int value)
        {
            int sanitizedValue = Mathf.Max(0, value);

            if (pendingManaCost == sanitizedValue)
            {
                return;
            }

            pendingManaCost = sanitizedValue;
            PendingManaCostChanged?.Invoke(pendingManaCost);
        }

        private void RestorePendingVisuals()
        {
            if (pendingSummonCard != null)
            {
                pendingSummonCard.RestoreToHome();
            }

            for (int i = 0; i < pendingBoardCards.Length; i++)
            {
                if (pendingBoardCards[i] != null)
                {
                    pendingBoardCards[i].RestoreToHome();
                }
            }
        }

        #endregion

        #region Control Rendering

        private void RefreshControls()
        {
            MatchSnapshotDto snapshot = projection?.Current;

            if (snapshot != null && snapshot.roundNumber == 0)
            {
                return;
            }

            bool canPriorityAction = snapshot != null && snapshot.viewerCanAct;

            bool canBlock = snapshot != null && snapshot.viewerCanDeclareBlock;

            bool mustSelectAbilityTargets =
                snapshot != null
                && snapshot.viewerMustSelectAbilityTargets
                && snapshot.pendingAbilitySelection != null;

            bool abilityDraftReady =
                mustSelectAbilityTargets
                && pendingAbilitySelectionComplete
                && string.Equals(
                    pendingAbilityRequestId,
                    snapshot.pendingAbilitySelection.requestId,
                    StringComparison.Ordinal
                );

            bool canInteract =
                !submitting
                && (
                    mustSelectAbilityTargets
                        ? abilityDraftReady
                        : canPriorityAction || canBlock
                );

            if (actionButton != null)
            {
                actionButton.interactable = canInteract;
            }

            if (actionLabel == null)
            {
                return;
            }

            if (submitting)
            {
                actionLabel.text = "ĐANG XỬ LÝ";
            }
            else if (mustSelectAbilityTargets)
            {
                actionLabel.text = abilityDraftReady
                    ? "XÁC NHẬN KỸ NĂNG"
                    : "CHỌN MỤC TIÊU";
            }
            else if (canBlock)
            {
                actionLabel.text = "XÁC NHẬN PHÒNG THỦ";
            }
            else if (!canInteract)
            {
                actionLabel.text = "ĐỢI ĐỐI THỦ";
            }
            else if (pendingSummonCard != null)
            {
                actionLabel.text = "TRIỆU HỒI";
            }
            else if (HasPendingBoardDraft)
            {
                actionLabel.text = "TẤN CÔNG";
            }
            else if (snapshot.viewerCanEndRound)
            {
                actionLabel.text = "KẾT THÚC VÒNG";
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
                if (card.instanceId == instanceId)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        private int FindPendingBoardSlot(DraggableHandCard card)
        {
            for (int slotIndex = 0; slotIndex < pendingBoardCards.Length; slotIndex++)
            {
                if (pendingBoardCards[slotIndex] == card)
                {
                    return slotIndex;
                }
            }

            return -1;
        }

        private static bool IsCardInSelfReserve(
            MatchSnapshotDto snapshot,
            string instanceId
        )
        {
            if (snapshot?.self?.reserve == null)
            {
                return false;
            }

            foreach (CardViewDto card in snapshot.self.reserve)
            {
                if (card.instanceId == instanceId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasOpponentAttackerAtSlot(
            MatchSnapshotDto snapshot,
            int slotIndex
        )
        {
            if (snapshot?.opponent?.board == null)
            {
                return false;
            }

            foreach (CardViewDto card in snapshot.opponent.board)
            {
                if (card.boardSlotIndex == slotIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private string[] BuildBoardCardIdsBySlot()
        {
            var result = new string[MatchState.BoardSlotCount];

            for (int slotIndex = 0; slotIndex < pendingBoardCards.Length; slotIndex++)
            {
                DraggableHandCard card = pendingBoardCards[slotIndex];

                result[slotIndex] = card?.Visual?.InstanceId;
            }

            return result;
        }

        #region Status Output

        private void SetLocalStatus(string message)
        {
            if (localStatusLabel != null)
            {
                localStatusLabel.text = message;
            }
        }

        #endregion
    }
}
