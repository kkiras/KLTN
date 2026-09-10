using KLTN.Game.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KLTN.Game.Presentation
{
    public sealed class NetworkCardVisual : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Artwork")]
        [SerializeField] private RectTransform artworkMount;
        [SerializeField] private Image cardBackImage;

        [Header("Runtime Text")]
        [SerializeField] private TMP_Text fallbackNameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text damageText;
        [SerializeField] private TMP_Text healthText;

        [Header("Visual State")]
        [SerializeField] private GameObject pendingOverlay;
        [SerializeField] private CanvasGroup canvasGroup;

        #endregion

        #region Runtime State

        private CardArtworkView currentArtworkPrefab;
        private CardArtworkView spawnedArtwork;

        #endregion

        #region Properties

        public string InstanceId { get; private set; }
        public int Energy { get; private set; }

        #endregion

        #region Binding

        public void BindFaceUp(CardViewDto dto, CardArtworkView artworkPrefab)
        {
            InstanceId = dto.instanceId;
            Energy = dto.energy;

            bool hasArtwork = artworkMount != null && artworkPrefab != null;

            SetCardBack(null);
            SetFaceArtwork(hasArtwork ? artworkPrefab : null);

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

        public void BindBack(Sprite cardBack)
        {
            InstanceId = null;
            Energy = 0;

            ClearFaceArtwork();
            SetCardBack(cardBack);

            if (fallbackNameText != null) { fallbackNameText.gameObject.SetActive(false); }

            SetTextActive(costText, false);
            SetTextActive(damageText, false);
            SetTextActive(healthText, false);
            SetPending(false);
            SetInteractableVisual(true);
        }

        #endregion

        #region Artwork

        private void SetFaceArtwork(CardArtworkView artworkPrefab)
        {
            if (artworkPrefab == currentArtworkPrefab && spawnedArtwork != null) { return; }

            ClearFaceArtwork();

            if (artworkPrefab == null || artworkMount == null) { return; }

            currentArtworkPrefab = artworkPrefab;
            spawnedArtwork = Instantiate(artworkPrefab, artworkMount, false);

            RectTransform artworkRect = spawnedArtwork.transform as RectTransform;

            if (artworkRect == null)
            {
                Debug.LogError(
                    $"Artwork prefab {artworkPrefab.name} requires a RectTransform.",
                    artworkPrefab
                );

                ClearFaceArtwork();
                return;
            }

            artworkRect.anchorMin = Vector2.zero;
            artworkRect.anchorMax = Vector2.one;
            artworkRect.pivot = new Vector2(0.5f, 0.5f);
            artworkRect.offsetMin = Vector2.zero;
            artworkRect.offsetMax = Vector2.zero;
            artworkRect.localScale = Vector3.one;
            artworkRect.localRotation = Quaternion.identity;
            artworkRect.SetAsFirstSibling();
        }

        private void ClearFaceArtwork()
        {
            currentArtworkPrefab = null;

            if (spawnedArtwork == null) { return; }

            spawnedArtwork.gameObject.SetActive(false);
            Destroy(spawnedArtwork.gameObject);
            spawnedArtwork = null;
        }

        private void SetCardBack(Sprite cardBack)
        {
            if (cardBackImage == null) { return; }

            bool visible = cardBack != null;

            cardBackImage.sprite = cardBack;
            cardBackImage.enabled = visible;
            cardBackImage.gameObject.SetActive(visible);
        }

        #endregion

        #region Visual State

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