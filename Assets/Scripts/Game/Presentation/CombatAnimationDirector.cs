using System;
using System.Collections;
using System.Collections.Generic;
using KLTN.Game.Networking;
using UnityEngine;

namespace KLTN.Game.Presentation
{
    public sealed class CombatAnimationDirector : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField] private MatchBoardPresenter boardPresenter;
        [SerializeField] private RectTransform animationLayer;
        [SerializeField] private RectTransform selfNexusTarget;
        [SerializeField] private RectTransform opponentNexusTarget;

        [Header("Movement")]
        [Min(0f)]
        [SerializeField] private float retreatDistance = 40f;

        [Min(0f)]
        [SerializeField] private float directAttackDistance = 350f;

        [Min(0f)]
        [SerializeField] private float collisionGap = 12f;

        [Header("Timing")]
        [Min(0f)]
        [SerializeField] private float retreatDuration = 0.18f;

        [Min(0f)]
        [SerializeField] private float strikeDuration = 0.22f;

        [Min(0f)]
        [SerializeField] private float impactHoldDuration = 0.06f;

        [Min(0f)]
        [SerializeField] private float returnDuration = 0.18f;

        [Min(0f)]
        [SerializeField] private float intervalBetweenSlots = 0.08f;

        #endregion

        #region Runtime State

        private readonly HashSet<CombatCardAnimator> preparedAnimators = new HashSet<CombatCardAnimator>();

        private MatchUpdateInbox inbox;
        private MatchClientProjection projection;
        private Coroutine processingCoroutine;
        private MatchUpdateDto currentUpdate;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            inbox = MatchUpdateInboxRegistry.Current;
            projection = MatchProjectionRegistry.Current;

            inbox.UpdateAvailable += HandleUpdateAvailable;
            TryStartProcessing();
        }

        private void OnDisable()
        {
            if (inbox != null)
            {
                inbox.UpdateAvailable -= HandleUpdateAvailable;
            }

            StopAllCoroutines();
            RestorePreparedCards();

            if (currentUpdate?.snapshot != null)
            {
                projection?.Apply(currentUpdate.snapshot);
            }

            currentUpdate = null;
            processingCoroutine = null;
        }

        #endregion

        #region Update Processing

        private void HandleUpdateAvailable()
        {
            TryStartProcessing();
        }

        private void TryStartProcessing()
        {
            if (!isActiveAndEnabled) { return; }
            if (processingCoroutine != null) { return; }

            processingCoroutine = StartCoroutine(ProcessUpdates());
        }

        private IEnumerator ProcessUpdates()
        {
            while (inbox.TryDequeue(out currentUpdate))
            {
                if (HasCombatResolution(currentUpdate))
                {
                    yield return AnimateResolution(currentUpdate);
                }

                projection.Apply(currentUpdate.snapshot);
                currentUpdate = null;
            }

            processingCoroutine = null;
        }

        #endregion

        #region Round Animation

        private IEnumerator AnimateResolution(MatchUpdateDto update)
        {
            if (boardPresenter == null || animationLayer == null)
            {
                Debug.LogWarning("Combat animation references are incomplete.");

                yield break;
            }

            RoundResolutionDto resolution = update.resolution;
            int viewerSeat = update.snapshot.viewerSeat;

            boardPresenter.PrepareCombat(resolution, viewerSeat);

            Canvas.ForceUpdateCanvases();

            // Mỗi step tự wind-up rồi attack
            // Không wind-up cùng lúc toàn bộ board
            for (int i = 0; i < resolution.steps.Length; i++)
            {
                yield return AnimateStep(resolution.steps[i], viewerSeat);

                if (intervalBetweenSlots > 0f && i < resolution.steps.Length - 1)
                {
                    yield return WaitUnscaled(intervalBetweenSlots);
                }
            }

            RestorePreparedCards();
        }

        #endregion

        #region Slot Animation

        private IEnumerator AnimateStep(CombatStepDto step, int viewerSeat)
        {
            bool hasHost = TryGetAnimator(step.hostCard, out CombatCardAnimator hostAnimator);

            bool hasGuest = TryGetAnimator(step.guestCard, out CombatCardAnimator guestAnimator);

            if (!hasHost && !hasGuest) { yield break; }

            if (hasHost)
            {
                PrepareAnimator(step.hostCard, viewerSeat, hostAnimator);
            }

            if (hasGuest)
            {
                PrepareAnimator(step.guestCard, viewerSeat, guestAnimator);
            }

            yield return AnimateRetreat(
                hasHost ? hostAnimator : null,
                hasGuest ? guestAnimator : null
            );

            if (hasHost && hasGuest)
            {
                yield return AnimateUnitCollision(hostAnimator, guestAnimator);

                preparedAnimators.Remove(hostAnimator);
                preparedAnimators.Remove(guestAnimator);
                yield break;
            }

            CombatCardAnimator attackerAnimator = hasHost
                ? hostAnimator
                : guestAnimator;

            yield return AnimateDirectAttack(attackerAnimator);
            preparedAnimators.Remove(attackerAnimator);
        }

        private IEnumerator AnimateRetreat(
            CombatCardAnimator first,
            CombatCardAnimator second)
        {
            if (first != null && second != null)
            {
                yield return RunTogether(
                    first.MoveToRetreat(retreatDuration),
                    second.MoveToRetreat(retreatDuration)
                );

                yield break;
            }

            CombatCardAnimator animator = first ?? second;

            if (animator != null)
            {
                yield return animator.MoveToRetreat(retreatDuration);
            }
        }

        private IEnumerator AnimateUnitCollision(CombatCardAnimator hostAnimator, CombatCardAnimator guestAnimator)
        {
            CalculateCollisionTargets(hostAnimator, guestAnimator, out Vector3 hostTarget, out Vector3 guestTarget);

            yield return RunTogether(
                hostAnimator.Strike(hostTarget, strikeDuration),
                guestAnimator.Strike(guestTarget, strikeDuration)
            );

            yield return WaitUnscaled(impactHoldDuration);

            yield return RunTogether(
                hostAnimator.ReturnHome(returnDuration),
                guestAnimator.ReturnHome(returnDuration)
            );
        }

        private IEnumerator AnimateDirectAttack(CombatCardAnimator attackerAnimator)
        {
            Vector3 targetPosition = CalculateDirectAttackTarget(attackerAnimator);

            yield return attackerAnimator.Strike(targetPosition, strikeDuration);

            yield return WaitUnscaled(impactHoldDuration);

            yield return attackerAnimator.ReturnHome(returnDuration);
        }

        #endregion

        #region Target Calculation

        private void CalculateCollisionTargets(
            CombatCardAnimator hostAnimator,
            CombatCardAnimator guestAnimator,
            out Vector3 hostTarget,
            out Vector3 guestTarget)
        {
            Vector3 hostPosition = hostAnimator.CurrentWorldPosition;

            Vector3 guestPosition = guestAnimator.CurrentWorldPosition;

            Vector3 collisionAxis = guestPosition - hostPosition;

            if (collisionAxis.sqrMagnitude <= 0.001f)
            {
                collisionAxis = Vector3.up;
            }

            collisionAxis.Normalize();

            Vector3 collisionCenter = (hostPosition + guestPosition) * 0.5f;

            float requiredCenterDistance =
                hostAnimator.WorldHeight * 0.5f +
                guestAnimator.WorldHeight * 0.5f +
                collisionGap;

            float halfDistance = requiredCenterDistance * 0.5f;

            hostTarget = collisionCenter - collisionAxis * halfDistance;

            guestTarget = collisionCenter + collisionAxis * halfDistance;
        }

        private Vector3 CalculateDirectAttackTarget(CombatCardAnimator attackerAnimator)
        {
            bool attackerIsSelf = attackerAnimator.IsSelfCard;

            RectTransform nexusTarget = attackerIsSelf
                ? opponentNexusTarget
                : selfNexusTarget;

            Vector3 direction = attackerIsSelf
                ? Vector3.up
                : Vector3.down;

            if (nexusTarget == null)
            {
                return attackerAnimator.HomeWorldPosition + direction * directAttackDistance;
            }

            Vector3 nexusCenter = nexusTarget.TransformPoint(nexusTarget.rect.center);

            Vector3 attackerPosition = attackerAnimator.CurrentWorldPosition;

            return new Vector3(attackerPosition.x, nexusCenter.y, attackerPosition.z);
        }

        #endregion

        #region Animator Preparation and Lookup

        private void PrepareAnimator(ResolvedCardDto card, int viewerSeat, CombatCardAnimator animator)
        {
            bool isSelfCard = card.seat == viewerSeat;

            animator.Prepare(animationLayer, isSelfCard, retreatDistance);

            preparedAnimators.Add(animator);
        }

        private bool TryGetAnimator(ResolvedCardDto card, out CombatCardAnimator animator)
        {
            animator = null;

            if (card?.cardBefore == null) { return false; }

            if (!boardPresenter.TryGetFaceUpVisual(card.cardBefore.instanceId, out NetworkCardVisual visual))
            {
                return false;
            }

            animator = visual.GetComponent<CombatCardAnimator>();

            if (animator != null) { return true; }

            Debug.LogWarning(
                $"Card {card.cardBefore.instanceId} does not have " +
                $"{nameof(CombatCardAnimator)}."
            );

            return false;
        }

        #endregion

        #region Coroutine Utilities

        private IEnumerator RunTogether(IEnumerator first, IEnumerator second)
        {
            int runningCount = 2;

            StartCoroutine(
                RunTracked(
                    first,
                    () => runningCount--
                )
            );

            StartCoroutine(
                RunTracked(
                    second,
                    () => runningCount--
                )
            );

            while (runningCount > 0)
            {
                yield return null;
            }
        }

        private static IEnumerator RunTracked(IEnumerator routine, Action completed)
        {
            yield return routine;
            completed?.Invoke();
        }

        private static IEnumerator WaitUnscaled(float duration)
        {
            if (duration <= 0f) { yield break; }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        #endregion

        #region Cleanup

        private void RestorePreparedCards()
        {
            foreach (CombatCardAnimator animator in preparedAnimators)
            {
                if (animator != null)
                {
                    animator.RestoreImmediately();
                }
            }

            preparedAnimators.Clear();
        }

        private static bool HasCombatResolution(MatchUpdateDto update)
        {
            return update != null &&
                   update.hasResolution &&
                   update.resolution?.steps != null &&
                   update.resolution.steps.Length > 0;
        }

        #endregion
    }
}