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
        #region Staging API

        /// <summary>
        /// Stages a hand card in Reserve as a summon preview without mutating domain
        /// state. A successful server command later rebuilds the view from a snapshot.
        /// </summary>
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

        /// <summary>
        /// Places or swaps a Reserve card in the local attack/block draft and recalculates
        /// adjacent Support stat previews.
        /// </summary>
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
    }
}
