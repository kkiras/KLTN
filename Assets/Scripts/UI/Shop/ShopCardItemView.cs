using System;
using CMCMProductions;
using KLTN.Game.Domain.Economy;
using KLTN.Game.Networking;
using KLTN.Game.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KLTN.UI.Shop
{
    /// <summary>
    /// One card in the shop list: card face, price below it, a dark overlay with a
    /// centred Buy button on hover, and a permanent dark overlay when already owned.
    /// </summary>
    public sealed class ShopCardItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform cardMount;
        [SerializeField] private GameObject hoverOverlay;
        [SerializeField] private Button buyButton;
        [SerializeField] private GameObject ownedOverlay;
        [SerializeField] private TextMeshProUGUI priceText;

        private static readonly Color AffordableColor = new Color(1f, 0.93f, 0.7f);
        private static readonly Color UnaffordableColor = new Color(0.9f, 0.3f, 0.3f);

        private ShopEntry entry;
        private Action<string> onBuy;
        private bool owned;
        private bool busy;
        private NetworkCardVisual spawnedCard;

        public string CardId => entry?.CardId;

        private void Awake()
        {
            if (buyButton != null)
            {
                buyButton.onClick.AddListener(HandleBuyClicked);
            }

            SetHover(false);
        }

        public void Bind(
            ShopEntry shopEntry,
            Card cardAsset,
            CardArtworkView artworkPrefab,
            NetworkCardVisual cardPrefab,
            bool isOwned,
            bool canAfford,
            Action<string> buyCallback
        )
        {
            entry = shopEntry;
            onBuy = buyCallback;
            owned = isOwned;

            SpawnCard(cardAsset, artworkPrefab, cardPrefab);

            string currency = shopEntry.Currency == Currency.Silver ? "bạc" : "xu";

            if (priceText != null)
            {
                priceText.text = isOwned ? "Đã sở hữu" : $"{shopEntry.Price:N0} {currency}";
                priceText.color = isOwned || canAfford ? AffordableColor : UnaffordableColor;
            }

            if (ownedOverlay != null)
            {
                ownedOverlay.SetActive(isOwned);
            }

            if (buyButton != null)
            {
                buyButton.interactable = !isOwned && canAfford && !busy;
            }

            SetHover(false);
        }

        public void SetBusy(bool value)
        {
            busy = value;

            if (buyButton != null)
            {
                buyButton.interactable = !value && buyButton.interactable;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHover(!owned);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHover(false);
        }

        private void SetHover(bool visible)
        {
            if (hoverOverlay != null)
            {
                hoverOverlay.SetActive(visible);
            }
        }

        private void HandleBuyClicked()
        {
            if (entry != null && !owned && !busy)
            {
                onBuy?.Invoke(entry.CardId);
            }
        }

        private void SpawnCard(Card cardAsset, CardArtworkView artworkPrefab, NetworkCardVisual cardPrefab)
        {
            if (spawnedCard != null)
            {
                Destroy(spawnedCard.gameObject);
                spawnedCard = null;
            }

            if (cardPrefab == null || cardMount == null || cardAsset == null)
            {
                return;
            }

            spawnedCard = Instantiate(cardPrefab, cardMount, false);

            // Shop cards are display-only: disable match interactions such as dragging.
            foreach (DraggableHandCard drag in spawnedCard.GetComponentsInChildren<DraggableHandCard>(true))
            {
                drag.enabled = false;
            }

            CanvasGroup group = spawnedCard.GetComponent<CanvasGroup>();

            if (group == null)
            {
                group = spawnedCard.gameObject.AddComponent<CanvasGroup>();
            }

            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 1f;

            spawnedCard.BindFaceUp(
                new CardViewDto
                {
                    instanceId = string.Empty,
                    definitionId = cardAsset.name,
                    displayName = cardAsset.cardName,
                    health = cardAsset.health,
                    damage = cardAsset.damage,
                    energy = cardAsset.energy,
                    keywords = (int)cardAsset.keywords,
                },
                artworkPrefab
            );

            FitIntoMount(spawnedCard.transform as RectTransform);
        }

        private void FitIntoMount(RectTransform card)
        {
            if (card == null)
            {
                return;
            }

            // Bottom-aligned so the price label sits right under the card.
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0f);
            card.anchoredPosition = Vector2.zero;
            card.localRotation = Quaternion.identity;

            Vector2 cardSize = card.rect.size;
            Vector2 mountSize = cardMount.rect.size;

            if (cardSize.x <= 0f || cardSize.y <= 0f || mountSize.x <= 0f || mountSize.y <= 0f)
            {
                card.localScale = Vector3.one;
                return;
            }

            float scale = Mathf.Min(mountSize.x / cardSize.x, mountSize.y / cardSize.y);
            card.localScale = new Vector3(scale, scale, 1f);
        }

#if UNITY_EDITOR
        public void EditorWire(RectTransform mount, GameObject hover, Button buy, GameObject ownedLayer, TextMeshProUGUI price)
        {
            cardMount = mount;
            hoverOverlay = hover;
            buyButton = buy;
            ownedOverlay = ownedLayer;
            priceText = price;
        }
#endif
    }
}
