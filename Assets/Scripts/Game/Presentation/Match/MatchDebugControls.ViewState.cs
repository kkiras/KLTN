using System;
using KLTN.Game.Domain;
using KLTN.Game.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KLTN.Game.Presentation
{
    public sealed partial class MatchDebugControls : MonoBehaviour
    {
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
