using System.Collections.Generic;
using KLTN.Game.Domain;
using KLTN.Game.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KLTN.Game.Presentation
{
    public sealed class AbilityTargetSelectionController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        private MatchBoardPresenter boardPresenter;

        [SerializeField]
        private MatchDebugControls actionControls;

        [SerializeField]
        private TMP_Text promptLabel;

        [SerializeField]
        private Button cancelButton;

        [Header("Target Colors")]
        [SerializeField]
        private Color validTargetColor = new Color(1f, 0.75f, 0.1f, 0.25f);

        [SerializeField]
        private Color primarySelectedColor = new Color(0.15f, 0.9f, 1f, 0.42f);

        [SerializeField]
        private Color secondarySelectedColor = new Color(1f, 0.2f, 0.55f, 0.42f);

        #endregion

        #region Runtime State

        private MatchClientProjection projection;

        private PendingAbilitySelectionDto pendingSelection;

        private readonly List<string> primaryTargets = new List<string>();

        private readonly List<string> secondaryTargets = new List<string>();

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            projection = MatchProjectionRegistry.Current;

            if (projection != null)
            {
                projection.SnapshotChanged += OnSnapshotChanged;

                projection.CommandRejected += OnCommandRejected;
            }

            if (boardPresenter != null)
            {
                boardPresenter.FaceUpViewsRendered += OnFaceUpViewsRendered;
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(CancelSelection);
            }

            ApplySnapshot(projection != null ? projection.Current : null);
        }

        private void OnDisable()
        {
            if (projection != null)
            {
                projection.SnapshotChanged -= OnSnapshotChanged;

                projection.CommandRejected -= OnCommandRejected;
            }

            if (boardPresenter != null)
            {
                boardPresenter.FaceUpViewsRendered -= OnFaceUpViewsRendered;

                boardPresenter.ClearAbilityTargetVisuals();
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(CancelSelection);
            }

            ClearLocalState();
        }

        #endregion

        #region Projection Events

        private void OnSnapshotChanged(MatchSnapshotDto snapshot)
        {
            ApplySnapshot(snapshot);
        }

        private void OnCommandRejected(CommandRejectionReason reason)
        {
            RefreshPresentation();
        }

        private void OnFaceUpViewsRendered()
        {
            RefreshTargetVisuals();
        }

        #endregion

        #region Snapshot Application

        private void ApplySnapshot(MatchSnapshotDto snapshot)
        {
            PendingAbilitySelectionDto next =
                snapshot != null && snapshot.viewerMustSelectAbilityTargets
                    ? snapshot.pendingAbilitySelection
                    : null;

            if (next == null)
            {
                ClearLocalState();
                return;
            }

            bool changedRequest =
                pendingSelection == null || pendingSelection.requestId != next.requestId;

            pendingSelection = next;

            if (changedRequest)
            {
                primaryTargets.Clear();
                secondaryTargets.Clear();
            }

            SanitizeSelections();
            RefreshPresentation();
        }

        private void ClearLocalState()
        {
            pendingSelection = null;

            primaryTargets.Clear();
            secondaryTargets.Clear();

            if (boardPresenter != null)
            {
                boardPresenter.ClearAbilityTargetVisuals();
            }

            if (actionControls != null)
            {
                actionControls.ClearAbilityTargetDraft();
            }

            SetPrompt(string.Empty);
            SetCancelButtonVisible(false);
        }

        #endregion

        #region Selection

        private void HandleTargetClicked(string instanceId)
        {
            if (pendingSelection == null || string.IsNullOrWhiteSpace(instanceId))
            {
                return;
            }

            if (RemoveSelectedTarget(instanceId))
            {
                RefreshPresentation();
                return;
            }

            AbilityTargetRequirementDto requirement = FindCurrentRequirement();

            if (
                requirement == null
                || !Contains(requirement.validTargetIds, instanceId)
                || IsAlreadySelected(instanceId)
            )
            {
                return;
            }

            List<string> targets = GetTargets(requirement.slot);

            if (targets.Count >= requirement.count)
            {
                return;
            }

            targets.Add(instanceId);
            RefreshPresentation();
        }

        private bool RemoveSelectedTarget(string instanceId)
        {
            bool removedPrimary = primaryTargets.Remove(instanceId);

            bool removedSecondary = secondaryTargets.Remove(instanceId);

            return removedPrimary || removedSecondary;
        }

        private bool IsAlreadySelected(string instanceId)
        {
            return primaryTargets.Contains(instanceId)
                || secondaryTargets.Contains(instanceId);
        }

        #endregion

        #region Rendering

        private void RefreshPresentation()
        {
            RefreshTargetVisuals();

            bool complete = IsSelectionComplete();

            if (actionControls != null && pendingSelection != null)
            {
                actionControls.SetAbilityTargetDraft(
                    pendingSelection.requestId,
                    primaryTargets.ToArray(),
                    secondaryTargets.ToArray(),
                    complete
                );
            }

            SetPrompt(BuildPrompt());

            SetCancelButtonVisible(
                pendingSelection != null && pendingSelection.canCancel
            );
        }

        private void RefreshTargetVisuals()
        {
            if (boardPresenter == null)
            {
                return;
            }

            boardPresenter.ClearAbilityTargetVisuals();

            if (pendingSelection == null)
            {
                return;
            }

            foreach (string targetId in primaryTargets)
            {
                ConfigureVisual(targetId, primarySelectedColor);
            }

            foreach (string targetId in secondaryTargets)
            {
                ConfigureVisual(targetId, secondarySelectedColor);
            }

            AbilityTargetRequirementDto current = FindCurrentRequirement();

            if (current?.validTargetIds == null)
            {
                return;
            }

            foreach (string targetId in current.validTargetIds)
            {
                if (!IsAlreadySelected(targetId))
                {
                    ConfigureVisual(targetId, validTargetColor);
                }
            }
        }

        private void ConfigureVisual(string instanceId, Color color)
        {
            if (boardPresenter.TryGetFaceUpVisual(instanceId, out NetworkCardVisual view))
            {
                view.SetAbilityTargetState(color, HandleTargetClicked);
            }
        }

        private string BuildPrompt()
        {
            if (pendingSelection == null)
            {
                return string.Empty;
            }

            if (IsSelectionComplete())
            {
                return $"Kỹ năng {pendingSelection.abilityId}: " + "đã chọn đủ mục tiêu.";
            }

            AbilityTargetRequirementDto current = FindCurrentRequirement();

            if (current == null)
            {
                return $"Kỹ năng {pendingSelection.abilityId}: " + "hãy chọn mục tiêu.";
            }

            List<string> selected = GetTargets(current.slot);

            return $"Kỹ năng {pendingSelection.abilityId}: "
                + $"chọn {GetRelationText(current.relation)} "
                + $"làm {GetSlotText(current.slot)} "
                + $"({selected.Count}/{current.count}).";
        }

        #endregion

        #region Validation

        private void SanitizeSelections()
        {
            SanitizeSlot((int)AbilityTargetSlot.Primary, primaryTargets);

            SanitizeSlot((int)AbilityTargetSlot.Secondary, secondaryTargets);

            for (int i = secondaryTargets.Count - 1; i >= 0; i--)
            {
                if (primaryTargets.Contains(secondaryTargets[i]))
                {
                    secondaryTargets.RemoveAt(i);
                }
            }
        }

        private void SanitizeSlot(int slot, List<string> targets)
        {
            AbilityTargetRequirementDto requirement = FindRequirement(slot);

            if (requirement == null)
            {
                targets.Clear();
                return;
            }

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (!Contains(requirement.validTargetIds, targets[i]))
                {
                    targets.RemoveAt(i);
                }
            }

            while (targets.Count > requirement.count)
            {
                targets.RemoveAt(targets.Count - 1);
            }
        }

        private bool IsSelectionComplete()
        {
            if (pendingSelection?.requirements == null)
            {
                return false;
            }

            foreach (
                AbilityTargetRequirementDto requirement in pendingSelection.requirements
            )
            {
                if (
                    requirement == null
                    || GetTargets(requirement.slot).Count != requirement.count
                )
                {
                    return false;
                }
            }

            return pendingSelection.requirements.Length > 0;
        }

        private AbilityTargetRequirementDto FindCurrentRequirement()
        {
            if (pendingSelection?.requirements == null)
            {
                return null;
            }

            foreach (
                AbilityTargetRequirementDto requirement in pendingSelection.requirements
            )
            {
                if (
                    requirement != null
                    && GetTargets(requirement.slot).Count < requirement.count
                )
                {
                    return requirement;
                }
            }

            return null;
        }

        private AbilityTargetRequirementDto FindRequirement(int slot)
        {
            if (pendingSelection?.requirements == null)
            {
                return null;
            }

            foreach (
                AbilityTargetRequirementDto requirement in pendingSelection.requirements
            )
            {
                if (requirement != null && requirement.slot == slot)
                {
                    return requirement;
                }
            }

            return null;
        }

        private List<string> GetTargets(int slot)
        {
            return slot == (int)AbilityTargetSlot.Secondary
                ? secondaryTargets
                : primaryTargets;
        }

        private static bool Contains(string[] values, string expected)
        {
            if (values == null)
            {
                return false;
            }

            foreach (string value in values)
            {
                if (value == expected)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region UI Helpers

        private void CancelSelection()
        {
            if (
                pendingSelection == null
                || !pendingSelection.canCancel
                || actionControls == null
            )
            {
                return;
            }

            if (cancelButton != null)
            {
                cancelButton.interactable = false;
            }

            SetPrompt("Đang hủy chọn kỹ năng...");

            bool sent = actionControls.CancelPendingAbilitySelection(
                pendingSelection.requestId
            );

            if (!sent)
            {
                RefreshPresentation();
            }
        }

        private void SetCancelButtonVisible(bool visible)
        {
            if (cancelButton == null)
            {
                return;
            }

            cancelButton.gameObject.SetActive(visible);
            cancelButton.interactable = visible;
        }

        private void SetPrompt(string message)
        {
            if (promptLabel != null)
            {
                promptLabel.text = message ?? string.Empty;
            }
        }

        private static string GetSlotText(int slot)
        {
            return slot == (int)AbilityTargetSlot.Secondary
                ? "mục tiêu phụ"
                : "mục tiêu chính";
        }

        private static string GetRelationText(int relation)
        {
            switch ((TargetRelation)relation)
            {
                case TargetRelation.Ally:
                    return "một đồng minh";

                case TargetRelation.Enemy:
                    return "một kẻ địch";

                default:
                    return "một lá bài";
            }
        }

        #endregion
    }
}
