using CMCMProductions;
using KLTN.Game.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KLTN.Game.Presentation
{
    public sealed class NetworkCardVisual : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField] private Image artworkImage;
        [SerializeField] private TMP_Text fallbackNameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text damageText;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private GameObject pendingOverlay;
        [SerializeField] private CanvasGroup canvasGroup;

        #endregion

        #region Properties

        public string InstanceId { get; private set; }
        public int Energy { get; private set; }

        #endregion

        #region Binding

        public void BindFaceUp(CardViewDto dto, Card asset)
        {
            InstanceId = dto.instanceId;
            Energy = dto.energy;
            bool hasArtwork = asset != null && asset.artwork != null;

            if (artworkImage != null)
            {
                artworkImage.sprite = hasArtwork ? asset.artwork : null;
                artworkImage.enabled = hasArtwork;
            }

            if (fallbackNameText != null)
            {
                fallbackNameText.gameObject.SetActive(!hasArtwork);
                fallbackNameText.text = dto.displayName;
            }

            if (costText != null)
            {
                costText.text = dto.energy.ToString();
                costText.gameObject.SetActive(true);
            }

            if (damageText != null)
            {
                damageText.text = dto.damage.ToString();
                damageText.gameObject.SetActive(true);
            }

            if (healthText != null)
            {
                healthText.text = dto.health.ToString();
                healthText.gameObject.SetActive(true);
            }

            SetPending(false);
            SetInteractableVisual(true);
        }

        #endregion

        #region Visual State

        public void BindBack(Sprite cardBack)
        {
            InstanceId = null;
            Energy = 0;

            if (artworkImage != null)
            {
                artworkImage.sprite = cardBack;
                artworkImage.enabled = cardBack != null;
            }

            if (fallbackNameText != null) { fallbackNameText.gameObject.SetActive(false); }

            SetTextActive(costText, false);
            SetTextActive(damageText, false);
            SetTextActive(healthText, false);
            SetPending(false);
            SetInteractableVisual(true);
        }

        public void SetPending(bool value)
        {
            if (pendingOverlay != null) { pendingOverlay.SetActive(value); }
        }

        public void SetInteractableVisual(bool value)
        {
            if (canvasGroup != null) { canvasGroup.alpha = value ? 1f : 0.55f; }
        }

        #endregion

        #region Helpers

        private static void SetTextActive(TMP_Text label, bool value)
        {
            if (label != null) { label.gameObject.SetActive(value); }
        }

        #endregion
    }
}
