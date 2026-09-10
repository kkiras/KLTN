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
        [SerializeField] private MatchTransitionBanner transitionBanner;

        [Header("Movement")]
        [Min(0f)]
        [SerializeField] private float retreatDistance = 40f;

        [Min(0f)]
        [SerializeField] private float directAttackDistance = 350f;

        [Min(0f)]
        [SerializeField] private float collisionGap = 12f;

        [Min(0f)]
        [SerializeField] private float nexusContactGap = 4f;

        [Header("VFX")]
        [SerializeField] private CombatVfxPresenter vfxPresenter;

        [Header("Damage Feedback")]
        [SerializeField] private DamageFeedbackView selfNexusFeedback;
        [SerializeField] private DamageFeedbackView opponentNexusFeedback;

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

        [Min(0f)]
        [SerializeField] private float combatPreviewDuration = 1.5f;

        [Min(0f)]
        [SerializeField] private float deathDelayAfterCombat = 0.5f;

        [SerializeField, Min(0f)] private float roundEndDelayBeforeBanner = 0.75f;
        [SerializeField, Min(0f)] private float roundEndDelayAfterBanner = 0.75f;

        #endregion

        #region Runtime State

        private readonly HashSet<CombatCardAnimator> preparedAnimators = new HashSet<CombatCardAnimator>();
        private readonly HashSet<DamageFeedbackView> activeFeedbackViews = new HashSet<DamageFeedbackView>();
        private readonly HashSet<CardDeathFeedback> activeDeathFeedbacks = new HashSet<CardDeathFeedback>();
        private readonly Dictionary<string, CardDeathFeedback> pendingDeathsById = new Dictionary<string, CardDeathFeedback>();
        private readonly Vector3[] nexusTargetCorners = new Vector3[4];

        private MatchUpdateInbox inbox;
        private MatchClientProjection projection;
        private Coroutine processingCoroutine;
        private MatchUpdateDto currentUpdate;
        private int activeDamageFeedbackCount;

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

            activeDamageFeedbackCount = 0;
            RestorePreparedCards();
            ResetFeedbackViews();
            ResetDeathFeedbacks();
            pendingDeathsById.Clear();

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

            pendingDeathsById.Clear();

            boardPresenter.PrepareCombat(resolution, viewerSeat);
            Canvas.ForceUpdateCanvases();

            yield return null;

            yield return WaitUnscaled(combatPreviewDuration);

            for (int i = 0; i < resolution.steps.Length; i++)
            {
                yield return AnimateStep(resolution.steps[i], viewerSeat);

                if (intervalBetweenSlots > 0f && i < resolution.steps.Length - 1)
                {
                    yield return WaitUnscaled(intervalBetweenSlots);
                }
            }

            bool hasPendingDeaths = pendingDeathsById.Count > 0;

            if (hasPendingDeaths)
            {
                yield return WaitUnscaled(deathDelayAfterCombat);
                yield return AnimatePendingDeaths();
            }

            yield return WaitForDamageFeedback();

            RestorePreparedCards();
            ResetFeedbackViews();

            activeDeathFeedbacks.Clear();
            pendingDeathsById.Clear();

            yield return WaitUnscaled(roundEndDelayBeforeBanner);

            if (transitionBanner != null)
            {
                yield return transitionBanner.PlayRoundEnd(resolution.roundNumber);
            }
            else
            {
                Debug.LogError("CombatAnimationDirector has no MatchTransitionBanner reference.", this);
            }

            yield return WaitUnscaled(roundEndDelayAfterBanner);
        }

        #endregion

        #region Slot Animation

        private IEnumerator AnimateStep(CombatStepDto step, int viewerSeat)
        {
            bool hasHostCard = HasResolvedCard(step.hostCard);
            bool hasGuestCard = HasResolvedCard(step.guestCard);

            CombatCardAnimator hostAnimator = null;
            CombatCardAnimator guestAnimator = null;

            bool hasHostVisual = hasHostCard &&
                                 TryGetAnimator(step.hostCard, out hostAnimator);

            bool hasGuestVisual = hasGuestCard &&
                                  TryGetAnimator(step.guestCard, out guestAnimator);

            Debug.Log(
                $"Combat step: Slot={step.slotIndex}, " +
                $"HostCard={CardIdOrNone(step.hostCard)}, " +
                $"GuestCard={CardIdOrNone(step.guestCard)}, " +
                $"HostVisual={hasHostVisual}, GuestVisual={hasGuestVisual}."
            );

            if (hasHostCard && hasGuestCard)
            {
                if (!hasHostVisual || !hasGuestVisual)
                {
                    Debug.LogWarning(
                        $"Không tìm thấy đủ visual cho combat card-vs-card. " +
                        $"Slot={step.slotIndex}, HostVisual={hasHostVisual}, " +
                        $"GuestVisual={hasGuestVisual}."
                    );

                    yield break;
                }

                PrepareAnimator(step.hostCard, viewerSeat, hostAnimator);
                PrepareAnimator(step.guestCard, viewerSeat, guestAnimator);

                yield return AnimateRetreat(hostAnimator, guestAnimator);

                yield return AnimateUnitCollision(step, hostAnimator, guestAnimator);

                preparedAnimators.Remove(hostAnimator);
                preparedAnimators.Remove(guestAnimator);
                yield break;
            }

            CombatCardAnimator attackerAnimator =
                hasHostCard ? hostAnimator : guestAnimator;

            bool hasAttackerVisual =
                hasHostCard ? hasHostVisual : hasGuestVisual;

            if (!hasAttackerVisual || attackerAnimator == null)
            {
                Debug.LogWarning(
                    $"Không tìm thấy attacker visual cho direct attack. " +
                    $"Slot={step.slotIndex}."
                );

                yield break;
            }

            ResolvedCardDto attackerCard = hasHostCard
                ? step.hostCard
                : step.guestCard;

            PrepareAnimator(attackerCard, viewerSeat, attackerAnimator);

            yield return AnimateRetreat(attackerAnimator, null);

            yield return AnimateDirectAttack(step, viewerSeat, attackerAnimator);
            preparedAnimators.Remove(attackerAnimator);
        }

        private IEnumerator AnimateRetreat(CombatCardAnimator first, CombatCardAnimator second)
        {
            var routines = new List<IEnumerator>(4);

            AddRetreatRoutines(first, routines);
            AddRetreatRoutines(second, routines);

            yield return RunMany(routines);
        }

        private void AddRetreatRoutines(CombatCardAnimator animator, List<IEnumerator> routines)
        {
            if (animator == null) { return; }

            routines.Add(animator.MoveToRetreat(retreatDuration));

            CombatImpactFlashView flash = animator.GetComponent<CombatImpactFlashView>();

            if (flash != null) { routines.Add(flash.EaseIn(retreatDuration)); }
        }

        private IEnumerator AnimateUnitCollision(
            CombatStepDto step,
            CombatCardAnimator hostAnimator,
            CombatCardAnimator guestAnimator)
        {
            DamageFeedbackView hostFeedback =
                hostAnimator.GetComponent<DamageFeedbackView>();

            DamageFeedbackView guestFeedback =
                guestAnimator.GetComponent<DamageFeedbackView>();

            BeginFeedback(hostFeedback);
            BeginFeedback(guestFeedback);

            CalculateCollisionTargets(
                hostAnimator,
                guestAnimator,
                out Vector3 hostTarget,
                out Vector3 guestTarget);

            PlaySlide(hostAnimator);
            PlaySlide(guestAnimator);

            yield return RunTogether(
                hostAnimator.Strike(
                    hostTarget,
                    strikeDuration),

                guestAnimator.Strike(
                    guestTarget,
                    strikeDuration));

            if (vfxPresenter != null)
            {
                Vector3 impactPosition = (hostTarget + guestTarget) * 0.5f;
                vfxPresenter.PlayCardHit(impactPosition);
            }

            StartDamageFeedback(
                PlayCardDamage(
                    hostFeedback,
                    step.hostCard));

            StartDamageFeedback(
                PlayCardDamage(
                    guestFeedback,
                    step.guestCard));

            yield return WaitUnscaled(impactHoldDuration);

            yield return ShowStepLethalHighlights(step);

            yield return ReturnAfterCollision(hostAnimator, guestAnimator);
        }

        private IEnumerator AnimateDirectAttack(
            CombatStepDto step,
            int viewerSeat,
            CombatCardAnimator attackerAnimator)
        {
            if (!TryGetNexusImpact(
                    step,
                    viewerSeat,
                    out bool targetIsSelf,
                    out int damagedSeat,
                    out int healthBefore,
                    out int healthAfter,
                    out int damage
                ))
            {
                Debug.LogWarning(
                    $"Combat step tại slot {step.slotIndex} " +
                    "không phải direct Nexus attack."
                );

                yield break;
            }

            DamageFeedbackView targetFeedback =
                targetIsSelf ? selfNexusFeedback : opponentNexusFeedback;

            RectTransform nexusTarget =
                targetIsSelf ? selfNexusTarget : opponentNexusTarget;

            Vector3 attackPosition = CalculateDirectAttackTarget(
                attackerAnimator,
                nexusTarget,
                targetIsSelf,
                out Vector3 nexusImpactPosition);

            Debug.Log(
                $"Nexus impact: Viewer={viewerSeat}, DamagedSeat={damagedSeat}, " +
                $"Target={(targetIsSelf ? "Self" : "Opponent")}, " +
                $"HP={healthBefore}->{healthAfter}, Damage={damage}."
            );

            BeginFeedback(targetFeedback);
            PlaySlide(attackerAnimator);

            yield return attackerAnimator.Strike(attackPosition, strikeDuration);

            if (vfxPresenter != null)
            {
                vfxPresenter.PlayNexusHit(nexusImpactPosition, targetIsSelf);
            }

            if (targetFeedback != null)
            {
                StartDamageFeedback(targetFeedback.PlayDamage(damage, healthAfter));
            }

            yield return WaitUnscaled(impactHoldDuration);

            yield return ReturnCard(attackerAnimator);
        }

        private IEnumerator ReturnAfterCollision(
            CombatCardAnimator hostAnimator,
            CombatCardAnimator guestAnimator)
        {
            yield return RunTogether(
                ReturnCard(hostAnimator),
                ReturnCard(guestAnimator));
        }

        private IEnumerator ReturnCard(CombatCardAnimator animator)
        {
            if (animator == null) { yield break; }

            CombatImpactFlashView flash = animator.GetComponent<CombatImpactFlashView>();

            if (flash == null)
            {
                yield return animator.ReturnHome(returnDuration);
                yield break;
            }

            yield return RunTogether(
                animator.ReturnHome(returnDuration),
                flash.FadeOut(returnDuration));
        }

        #endregion

        #region Combat VFX

        private void PlaySlide(CombatCardAnimator animator)
        {
            if (vfxPresenter == null || animator == null) { return; }

            Vector3 trailingDirection = animator.IsSelfCard
                ? Vector3.down
                : Vector3.up;

            Vector3 effectPosition =
                animator.CurrentWorldPosition +
                trailingDirection * animator.WorldHeight * 0.42f;

            bool flip = !animator.IsSelfCard;
            vfxPresenter.PlaySlide(effectPosition, flip);
        }

        private void PlayDeathDust(Vector3 worldPosition)
        {
            if (vfxPresenter != null)
            {
                vfxPresenter.PlayDeathDust(worldPosition);
            }
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

        private Vector3 CalculateDirectAttackTarget(
            CombatCardAnimator attackerAnimator,
            RectTransform nexusTarget,
            bool targetIsSelf,
            out Vector3 impactWorldPosition)
        {
            Vector3 attackerPosition = attackerAnimator.CurrentWorldPosition;
            Vector3 fallbackDirection = targetIsSelf ? Vector3.down : Vector3.up;

            if (nexusTarget == null)
            {
                Vector3 fallbackTarget =
                    attackerAnimator.HomeWorldPosition +
                    fallbackDirection * directAttackDistance;

                impactWorldPosition = fallbackTarget;
                return fallbackTarget;
            }

            nexusTarget.GetWorldCorners(nexusTargetCorners);

            float contactEdgeY = targetIsSelf
                ? Mathf.Max(nexusTargetCorners[1].y, nexusTargetCorners[2].y)
                : Mathf.Min(nexusTargetCorners[0].y, nexusTargetCorners[3].y);

            float distanceFromCardCenter =
                attackerAnimator.WorldHeight * 0.5f +
                nexusContactGap;

            float cardCenterY = targetIsSelf
                ? contactEdgeY + distanceFromCardCenter
                : contactEdgeY - distanceFromCardCenter;

            impactWorldPosition = new Vector3(
                attackerPosition.x,
                contactEdgeY,
                attackerPosition.z);

            return new Vector3(
                attackerPosition.x,
                cardCenterY,
                attackerPosition.z);
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

            if (!HasResolvedCard(card)) { return false; }

            if (!boardPresenter.TryGetFaceUpVisual(
                    card.cardBefore.instanceId,
                    out NetworkCardVisual visual))
            {
                Debug.LogWarning(
                    $"Không tìm thấy combat visual. " +
                    $"Card={card.cardBefore.instanceId}, Seat={card.seat}."
                );

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

        #region Damage Feedback

        private void BeginFeedback(DamageFeedbackView feedback)
        {
            if (feedback == null) { return; }

            activeFeedbackViews.Add(feedback);
            feedback.BeginAnticipation();
        }

        private static IEnumerator PlayCardDamage(
            DamageFeedbackView feedback,
            ResolvedCardDto card)
        {
            if (feedback == null || !HasResolvedCard(card))
            {
                yield break;
            }

            int damage = Mathf.Max(
                0,
                card.cardBefore.health -
                card.healthAfter);

            yield return feedback.PlayDamage(
                damage,
                card.healthAfter);
        }

        private static bool TryGetNexusImpact(
            CombatStepDto step,
            int viewerSeat,
            out bool targetIsSelf,
            out int damagedSeat,
            out int healthBefore,
            out int healthAfter,
            out int damage)
        {
            const int hostSeat = 0;
            const int guestSeat = 1;

            bool hasHostCard = HasResolvedCard(step.hostCard);
            bool hasGuestCard = HasResolvedCard(step.guestCard);

            bool hostAttacksGuest = hasHostCard && !hasGuestCard;
            bool guestAttacksHost = hasGuestCard && !hasHostCard;

            if (hostAttacksGuest)
            {
                damagedSeat = guestSeat;
                healthBefore = step.guestNexusHealthBefore;
                healthAfter = step.guestNexusHealthAfter;
            }
            else if (guestAttacksHost)
            {
                damagedSeat = hostSeat;
                healthBefore = step.hostNexusHealthBefore;
                healthAfter = step.hostNexusHealthAfter;
            }
            else
            {
                targetIsSelf = false;
                damagedSeat = -1;
                healthBefore = 0;
                healthAfter = 0;
                damage = 0;
                return false;
            }

            targetIsSelf = damagedSeat == viewerSeat;
            damage = Mathf.Max(0, healthBefore - healthAfter);
            return true;
        }

        private static bool HasResolvedCard(ResolvedCardDto card)
        {
            return card?.cardBefore != null &&
                   !string.IsNullOrWhiteSpace(card.cardBefore.instanceId);
        }

        private static string CardIdOrNone(ResolvedCardDto card)
        {
            return HasResolvedCard(card)
                ? card.cardBefore.instanceId
                : "none";
        }

        private void ResetFeedbackViews()
        {
            foreach (DamageFeedbackView feedback in activeFeedbackViews)
            {
                if (feedback != null)
                {
                    feedback.ResetImmediately();
                }
            }

            activeFeedbackViews.Clear();
        }

        #endregion

        #region Death Feedback

        private IEnumerator ShowStepLethalHighlights(CombatStepDto step)
        {
            var routines = new List<IEnumerator>(2);

            RegisterLethalHighlight(step.hostCard, routines);
            RegisterLethalHighlight(step.guestCard, routines);

            yield return RunMany(routines);
        }

        private void RegisterLethalHighlight(
            ResolvedCardDto card,
            List<IEnumerator> routines)
        {
            if (!IsDead(card)) { return; }

            string instanceId = card.cardBefore.instanceId;

            if (pendingDeathsById.ContainsKey(instanceId)) { return; }

            if (!boardPresenter.TryGetFaceUpVisual(
                    instanceId,
                    out NetworkCardVisual visual))
            {
                Debug.LogWarning(
                    $"Cannot prepare dead card {instanceId}: visual not found.");

                return;
            }

            CardDeathFeedback deathFeedback =
                visual.GetComponent<CardDeathFeedback>();

            if (deathFeedback == null)
            {
                Debug.LogWarning(
                    $"Card {instanceId} does not have " +
                    $"{nameof(CardDeathFeedback)}.");

                return;
            }

            pendingDeathsById.Add(instanceId, deathFeedback);
            activeDeathFeedbacks.Add(deathFeedback);
            routines.Add(deathFeedback.ShowLethalHighlight(instanceId));
        }

        private IEnumerator AnimatePendingDeaths()
        {
            var routines = new List<IEnumerator>(pendingDeathsById.Count);

            foreach (KeyValuePair<string, CardDeathFeedback> pair in pendingDeathsById)
            {
                if (pair.Value == null) { continue; }

                routines.Add(pair.Value.PlayShatter(pair.Key, PlayDeathDust));
            }

            yield return RunMany(routines);
        }

        private void ResetDeathFeedbacks()
        {
            foreach (CardDeathFeedback feedback in activeDeathFeedbacks)
            {
                if (feedback != null)
                {
                    feedback.ResetImmediately();
                }
            }

            activeDeathFeedbacks.Clear();
        }

        private static bool IsDead(ResolvedCardDto card)
        {
            return HasResolvedCard(card) && card.died;
        }

        #endregion

        #region Coroutine Utilities

        private IEnumerator RunTogether(IEnumerator first, IEnumerator second)
        {
            var routines = new List<IEnumerator>(2) { first, second };
            yield return RunMany(routines);
        }

        private IEnumerator RunMany(IReadOnlyList<IEnumerator> routines)
        {
            if (routines == null || routines.Count == 0) { yield break; }

            int runningCount = 0;

            for (int i = 0; i < routines.Count; i++)
            {
                IEnumerator routine = routines[i];

                if (routine == null) { continue; }

                runningCount++;
                StartCoroutine(RunTracked(routine, () => runningCount--));
            }

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

        private void StartDamageFeedback(
            IEnumerator feedbackRoutine)
        {
            if (feedbackRoutine == null) { return; }

            activeDamageFeedbackCount++;
            StartCoroutine(
                RunDamageFeedback(feedbackRoutine));
        }

        private IEnumerator RunDamageFeedback(
            IEnumerator feedbackRoutine)
        {
            yield return feedbackRoutine;

            activeDamageFeedbackCount =
                Mathf.Max(
                    0,
                    activeDamageFeedbackCount - 1);
        }

        private IEnumerator WaitForDamageFeedback()
        {
            while (activeDamageFeedbackCount > 0)
            {
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
                    animator.GetComponent<CombatImpactFlashView>()?.ResetImmediately();
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
