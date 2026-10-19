using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KLTN.Game.Presentation
{
    public sealed class CardDetailModal : MonoBehaviour
    {
        public static CardDetailModal Instance { get; private set; }

        [Header("Modal Root")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropCloseButton;

        [Header("Left Column - Card Preview")]
        [SerializeField] private RectTransform previewArtworkMount;
        [SerializeField] private TMP_Text previewCostText;
        [SerializeField] private TMP_Text previewDamageText;
        [SerializeField] private TMP_Text previewHealthText;
        [SerializeField] private TMP_Text previewFallbackNameText;

        [Header("Right Column - Card Info")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text roleAndStatsText;
        [SerializeField] private TMP_Text skillText;
        [SerializeField] private TMP_Text loreText;

        private CardArtworkView spawnedPreviewArtwork;

        private void Awake()
        {
            Instance = this;
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (backdropCloseButton != null) backdropCloseButton.onClick.AddListener(Hide);
            Hide();
        }

        private void Update()
        {
            if (modalRoot != null && modalRoot.activeSelf && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Hide();
            }
        }

        public static void TryShow(NetworkCardVisual card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.DefinitionId)) return;
            if (Instance == null) Instance = FindAnyObjectByType<CardDetailModal>(FindObjectsInactive.Include);
            
            if (Instance != null) Instance.Show(card);
        }

        public void Show(NetworkCardVisual card)
        {
            if (modalRoot != null) modalRoot.SetActive(true);
            else gameObject.SetActive(true);

            SetupLeftCardPreview(card);

            string finalTitle = !string.IsNullOrWhiteSpace(card.DisplayName) ? card.DisplayName : card.DefinitionId;
            string finalRole = "Không rõ";
            string finalSkill = "Chưa có mô tả kỹ năng.";
            string finalLore = "Chưa có giai thoại dân gian.";

            if (card.CurrentArtworkPrefab != null)
            {
                var loreProfile = card.CurrentArtworkPrefab.GetComponent<CardLoreProfile>();
                if (loreProfile != null)
                {
                    finalRole = loreProfile.role;
                    finalSkill = loreProfile.skillDescription;
                    finalLore = loreProfile.folklore;
                }
            }

            if (titleText != null) 
                titleText.text = $"{finalTitle.ToUpperInvariant()} <size=65%><color=#CCCCCC>[{finalRole}]</color></size>";
            
            if (roleAndStatsText != null) 
                roleAndStatsText.text = $"Mana: <color=#4EA8DE>{card.Energy}</color>   |   ST: <color=#EF476F>{card.CurrentDamage}</color>   |   HP: <color=#06D6A0>{card.CurrentHealth}</color>";
            
            if (skillText != null) skillText.text = finalSkill;
            
            if (loreText != null) loreText.text = $"<i>\"{finalLore}\"</i>";
        }

        public void Hide()
        {
            ClearPreviewArtwork();
            if (modalRoot != null) modalRoot.SetActive(false);
            else gameObject.SetActive(false);
        }

        private void SetupLeftCardPreview(NetworkCardVisual card)
        {
            ClearPreviewArtwork();
            bool hasArtwork = previewArtworkMount != null && card.CurrentArtworkPrefab != null;

            if (hasArtwork)
            {
                spawnedPreviewArtwork = Instantiate(card.CurrentArtworkPrefab, previewArtworkMount, false);
                if (spawnedPreviewArtwork.transform is RectTransform rect)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;
                    rect.SetAsFirstSibling();
                }
            }

            if (previewFallbackNameText != null) { previewFallbackNameText.gameObject.SetActive(!hasArtwork); previewFallbackNameText.text = card.DisplayName; }
            if (previewCostText != null) { previewCostText.text = card.Energy.ToString(); previewCostText.gameObject.SetActive(true); }
            if (previewDamageText != null) { previewDamageText.text = card.CurrentDamage.ToString(); previewDamageText.gameObject.SetActive(true); }
            if (previewHealthText != null) { previewHealthText.text = card.CurrentHealth.ToString(); previewHealthText.gameObject.SetActive(true); }
        }

        private void ClearPreviewArtwork()
        {
            if (spawnedPreviewArtwork != null)
            {
                Destroy(spawnedPreviewArtwork.gameObject);
                spawnedPreviewArtwork = null;
            }
        }
    }
}