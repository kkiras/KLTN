using System;
using System.Collections;
using System.Collections.Generic;
using KLTN.Game.Networking;
using UnityEngine;

namespace KLTN.Game.Presentation
{
    public sealed partial class CombatAnimationDirector : MonoBehaviour
    {
        #region Coroutine Utilities

        private IEnumerator RunTogether(IEnumerator first, IEnumerator second)
        {
            var routines = new List<IEnumerator>(2) { first, second };
            yield return RunMany(routines);
        }

        private IEnumerator RunMany(IReadOnlyList<IEnumerator> routines)
        {
            if (routines == null || routines.Count == 0)
            {
                yield break;
            }

            int runningCount = 0;

            for (int i = 0; i < routines.Count; i++)
            {
                IEnumerator routine = routines[i];

                if (routine == null)
                {
                    continue;
                }

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
            if (duration <= 0f)
            {
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void StartDamageFeedback(IEnumerator feedbackRoutine)
        {
            if (feedbackRoutine == null)
            {
                return;
            }

            activeDamageFeedbackCount++;
            StartCoroutine(RunDamageFeedback(feedbackRoutine));
        }

        private IEnumerator RunDamageFeedback(IEnumerator feedbackRoutine)
        {
            yield return feedbackRoutine;

            activeDamageFeedbackCount = Mathf.Max(0, activeDamageFeedbackCount - 1);
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
            return update != null
                && update.hasResolution
                && update.resolution?.steps != null
                && update.resolution.steps.Length > 0;
        }

        private static bool HasRoundTransition(MatchUpdateDto update)
        {
            return update != null
                && update.hasRoundTransition
                && update.roundTransition != null;
        }

        #endregion
    }
}
