using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KLTN.Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class CombatImpactFlashView : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField] private Image impactFlash;

        [SerializeField] private Color combatColor =
            new Color(1f, 0.3f, 0.12f, 1f);

        [Range(0f, 1f)]
        [SerializeField] private float peakAlpha = 0.75f;

        #endregion

        #region Runtime State

        private Color baseColor;
        private bool baseActive;
        private bool initialized;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Initialize();
            ResetImmediately();
        }

        private void OnDisable()
        {
            if (initialized) { ResetImmediately(); }
        }

        #endregion

        #region Animation API

        public IEnumerator EaseIn(float duration)
        {
            Initialize();

            if (impactFlash == null) { yield break; }

            impactFlash.gameObject.SetActive(true);

            Color targetColor = combatColor;
            targetColor.a = peakAlpha;

            yield return AnimateColor(
                impactFlash.color,
                targetColor,
                duration);

            impactFlash.color = targetColor;
        }

        public IEnumerator FadeOut(float duration)
        {
            Initialize();

            if (impactFlash == null) { yield break; }

            yield return AnimateColor(
                impactFlash.color,
                baseColor,
                duration);

            impactFlash.color = baseColor;
            impactFlash.gameObject.SetActive(baseActive);
        }

        public void ResetImmediately()
        {
            if (!initialized || impactFlash == null) { return; }

            impactFlash.color = baseColor;
            impactFlash.gameObject.SetActive(baseActive);
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            if (initialized) { return; }

            if (impactFlash != null)
            {
                baseColor = impactFlash.color;
                baseActive = impactFlash.gameObject.activeSelf;
            }

            initialized = true;
        }

        #endregion

        #region Animation

        private IEnumerator AnimateColor(
            Color startColor,
            Color endColor,
            float duration)
        {
            if (duration <= 0f)
            {
                impactFlash.color = endColor;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = progress * progress * (3f - 2f * progress);

                impactFlash.color = Color.LerpUnclamped(
                    startColor,
                    endColor,
                    eased);

                yield return null;
            }

            impactFlash.color = endColor;
        }

        #endregion
    }
}