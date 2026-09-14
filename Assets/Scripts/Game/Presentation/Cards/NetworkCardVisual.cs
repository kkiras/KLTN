using System;
using System.Collections.Generic;
using KLTN.Game.Domain;
using KLTN.Game.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KLTN.Game.Presentation
{
    public sealed class NetworkCardVisual : MonoBehaviour, IPointerClickHandler
    {
        #region Serialized Fields

        [Header("Artwork")]
        [SerializeField]
        private RectTransform artworkMount;

        [SerializeField]
        private Image cardBackImage;

        [Header("Runtime Text")]
        [SerializeField]
        private TMP_Text fallbackNameText;

        [SerializeField]
        private TMP_Text costText;

        [SerializeField]
        private TMP_Text damageText;

        [SerializeField]
        private TMP_Text healthText;

        [SerializeField]
        private TMP_Text keywordText;

        [Header("Visual State")]
        [SerializeField]
        private GameObject pendingOverlay;

        [SerializeField]
        private Image abilityTargetOverlay;

        [SerializeField]
        private CanvasGroup canvasGroup;

        #endregion

        #region Runtime State

        private CardArtworkView currentArtworkPrefab;
        private CardArtworkView spawnedArtwork;
        private Action<string> abilityTargetClicked;
        private int authoritativeDamage;
        private int authoritativeHealth;

        #endregion

        #region Properties

        public string InstanceId { get; private set; }
        public string DefinitionId { get; private set; }
        public int Energy { get; private set; }
        public int SupportDamageBonus { get; private set; }
        public int SupportHealthBonus { get; private set; }

        #endregion

        #region Binding

        public void BindFaceUp(CardViewDto dto, CardArtworkView artworkPrefab)
        {
            InstanceId = dto.instanceId;
            DefinitionId = dto.definitionId;
            Energy = dto.energy;
            SupportDamageBonus = dto.supportDamageBonus;
            SupportHealthBonus = dto.supportHealthBonus;
            authoritativeDamage = dto.damage;
            authoritativeHealth = dto.health;

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

            SetDisplayedStats(authoritativeDamage, authoritativeHealth);

            SetKeywords((UnitKeyword)dto.keywords);

            SetPending(false);
            ClearAbilityTargetState();
            SetInteractableVisual(true);
        }

        public void BindBack(Sprite cardBack)
        {
            InstanceId = null;
            DefinitionId = null;
            Energy = 0;
            SupportDamageBonus = 0;
            SupportHealthBonus = 0;
            authoritativeDamage = 0;
            authoritativeHealth = 0;

            ClearFaceArtwork();
            SetCardBack(cardBack);

            if (fallbackNameText != null)
            {
                fallbackNameText.gameObject.SetActive(false);
            }

            SetTextActive(costText, false);
            SetTextActive(damageText, false);
            SetTextActive(healthText, false);
            SetKeywords(UnitKeyword.None);
            SetPending(false);
            ClearAbilityTargetState();
            SetInteractableVisual(true);
        }

        #endregion

        #region Artwork

        private void SetFaceArtwork(CardArtworkView artworkPrefab)
        {
            if (artworkPrefab == currentArtworkPrefab && spawnedArtwork != null)
            {
                return;
            }

            ClearFaceArtwork();

            if (artworkPrefab == null || artworkMount == null)
            {
                return;
            }

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

            if (spawnedArtwork == null)
            {
                return;
            }

            spawnedArtwork.gameObject.SetActive(false);
            Destroy(spawnedArtwork.gameObject);
            spawnedArtwork = null;
        }

        private void SetCardBack(Sprite cardBack)
        {
            if (cardBackImage == null)
            {
                return;
            }

            bool visible = cardBack != null;

            cardBackImage.sprite = cardBack;
            cardBackImage.enabled = visible;
            cardBackImage.gameObject.SetActive(visible);
        }

        #endregion

        #region Visual State

        public void SetPending(bool value)
        {
            if (pendingOverlay != null)
            {
                pendingOverlay.SetActive(value);
            }
        }

        public void SetStatPreview(int damageBonus, int healthBonus)
        {
            SetDisplayedStats(
                Math.Max(0, authoritativeDamage + damageBonus),
                Math.Max(0, authoritativeHealth + healthBonus)
            );
        }

        public void ClearStatPreview()
        {
            SetDisplayedStats(authoritativeDamage, authoritativeHealth);
        }

        public void SetAbilityTargetState(Color color, Action<string> clickHandler)
        {
            abilityTargetClicked = clickHandler;

            if (abilityTargetOverlay == null)
            {
                return;
            }

            abilityTargetOverlay.color = color;

            abilityTargetOverlay.raycastTarget = false;

            abilityTargetOverlay.gameObject.SetActive(true);
        }

        public void ClearAbilityTargetState()
        {
            abilityTargetClicked = null;

            if (abilityTargetOverlay != null)
            {
                abilityTargetOverlay.gameObject.SetActive(false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (
                eventData.button != PointerEventData.InputButton.Left
                || string.IsNullOrWhiteSpace(InstanceId)
                || abilityTargetClicked == null
            )
            {
                return;
            }

            abilityTargetClicked.Invoke(InstanceId);
        }

        public void SetInteractableVisual(bool value)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = value ? 1f : 0.55f;
            }
        }

        #endregion

        #region Helpers

        private static void SetTextActive(TMP_Text label, bool value)
        {
            if (label != null)
            {
                label.gameObject.SetActive(value);
            }
        }

        private void SetDisplayedStats(int damage, int health)
        {
            if (damageText != null)
            {
                damageText.text = damage.ToString();
                damageText.gameObject.SetActive(true);
            }

            if (healthText != null)
            {
                healthText.text = health.ToString();
                healthText.gameObject.SetActive(true);
            }
        }

        #endregion

        private void SetKeywords(UnitKeyword keywords)
        {
            if (keywordText == null)
            {
                return;
            }

            var labels = new List<string>();

            if ((keywords & UnitKeyword.Fearsome) != 0)
            {
                labels.Add("F");
            }

            if ((keywords & UnitKeyword.CannotBlock) != 0)
            {
                labels.Add("CB");
            }

            if ((keywords & UnitKeyword.Ephemeral) != 0)
            {
                labels.Add("E");
            }

            if ((keywords & UnitKeyword.Lifesteal) != 0)
            {
                labels.Add("L");
            }

            keywordText.text = string.Join("  ", labels);

            keywordText.gameObject.SetActive(labels.Count > 0);
        }
    }
}
