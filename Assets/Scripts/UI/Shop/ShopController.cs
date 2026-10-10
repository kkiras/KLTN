using System.Collections.Generic;
using CMCMProductions;
using KLTN.Game.Content;
using KLTN.Game.Domain.Economy;
using KLTN.Game.Presentation;
using KLTN.Infrastructure.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KLTN.UI.Shop
{
    /// <summary>
    /// Shop screen: top bar with coins/silver, sort/filter dropdown and a grid of the
    /// 11 buyable cards. Owned cards stay in the list with a dark overlay.
    /// </summary>
    public sealed class ShopController : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private ShopTopBarView topBar;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private ShopCardItemView itemPrefab;
        [SerializeField] private TMP_Dropdown sortDropdown;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button backButton;
        [SerializeField] private ShopConfirmDialog confirmDialog;

        [Header("Card Visuals")]
        [SerializeField] private NetworkCardVisual cardVisualPrefab;
        [SerializeField] private CardPresentationCatalog presentationCatalog;

        private readonly List<ShopCardItemView> items = new List<ShopCardItemView>();
        private ShopSortMode sortMode = ShopSortMode.CoinsAscending;
        private CardAssetCatalog cardAssets;
        private PlayerProfileService profiles;
        private bool purchaseInFlight;

        private async void Start()
        {
            cardAssets = new CardAssetCatalog();
            profiles = PlayerProfileService.Instance;

            if (backButton != null)
            {
                backButton.onClick.AddListener(() => SceneManager.LoadScene(SceneNames.MainMenu));
            }

            if (sortDropdown != null)
            {
                sortDropdown.ClearOptions();
                sortDropdown.AddOptions(new List<string>(ShopSortModeLabels.All));
                sortDropdown.SetValueWithoutNotify((int)sortMode);
                sortDropdown.onValueChanged.AddListener(value =>
                {
                    sortMode = (ShopSortMode)value;
                    Rebuild();
                });
            }

            if (profiles == null)
            {
                SetStatus("Không có dịch vụ hồ sơ người chơi.");
                return;
            }

            profiles.ProfileChanged += HandleProfileChanged;
            SetStatus("Đang tải dữ liệu...");

            EconomyResult result = await profiles.EnsureLoadedAsync();

            if (this == null)
            {
                return;
            }

            SetStatus(result.Success ? string.Empty : "Không tải được hồ sơ: " + result.Message);
            Rebuild();
        }

        private void OnDestroy()
        {
            if (profiles != null)
            {
                profiles.ProfileChanged -= HandleProfileChanged;
            }
        }

        private void HandleProfileChanged(PlayerProfile profile)
        {
            Rebuild();
        }

        private void Rebuild()
        {
            PlayerProfile profile = profiles != null ? profiles.Current : null;
            topBar?.Bind(profile);

            foreach (ShopCardItemView item in items)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            items.Clear();

            ShopCatalog catalog = profiles != null ? profiles.Catalog : ShopCatalog.Load();

            if (catalog == null || itemPrefab == null || listContent == null)
            {
                return;
            }

            Wallet wallet = profile != null ? profile.Wallet : new Wallet(0, 0);
            List<ShopEntry> entries = ShopListQuery.Apply(
                catalog.All,
                sortMode,
                id => profile != null && profile.Owns(id)
            );

            foreach (ShopEntry entry in entries)
            {
                Card asset = cardAssets.Find(entry.CardId);
                CardArtworkView artwork = presentationCatalog != null ? presentationCatalog.Find(entry.CardId) : null;
                bool owned = profile != null && profile.Owns(entry.CardId);
                bool canAfford = profile != null && wallet.Balance(entry.Currency) >= entry.Price;

                ShopCardItemView item = Instantiate(itemPrefab, listContent, false);
                item.name = "ShopItem_" + entry.CardId;
                item.Bind(entry, asset, artwork, cardVisualPrefab, owned, canAfford, HandleBuy);
                item.SetBusy(purchaseInFlight);
                items.Add(item);
            }

            if (entries.Count == 0 && string.IsNullOrEmpty(statusText != null ? statusText.text : null))
            {
                SetStatus("Không có thẻ nào phù hợp bộ lọc.");
            }
        }

        private void HandleBuy(string cardId)
        {
            if (purchaseInFlight || profiles == null)
            {
                return;
            }

            Card asset = cardAssets.Find(cardId);
            string cardName = asset != null && !string.IsNullOrWhiteSpace(asset.cardName) ? asset.cardName : cardId;

            if (confirmDialog == null)
            {
                ExecutePurchase(cardId);
                return;
            }

            confirmDialog.Show($"Bạn có đồng ý mua thẻ {cardName} không?", () => ExecutePurchase(cardId));
        }

        private async void ExecutePurchase(string cardId)
        {
            if (purchaseInFlight || profiles == null)
            {
                return;
            }

            purchaseInFlight = true;
            SetItemsBusy(true);
            SetStatus("Đang mua...");

            EconomyResult result = await profiles.PurchaseAsync(cardId);

            if (this == null)
            {
                return;
            }

            purchaseInFlight = false;
            SetStatus(result.Success ? "Mua thành công!" : result.Message);
            Rebuild();
        }

        private void SetItemsBusy(bool busy)
        {
            foreach (ShopCardItemView item in items)
            {
                item?.SetBusy(busy);
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message ?? string.Empty;
            }
        }

#if UNITY_EDITOR
        public void EditorWire(
            ShopTopBarView bar,
            RectTransform content,
            ShopCardItemView prefab,
            TMP_Dropdown dropdown,
            TextMeshProUGUI status,
            Button back,
            ShopConfirmDialog dialog,
            NetworkCardVisual cardPrefab,
            CardPresentationCatalog presentation
        )
        {
            topBar = bar;
            listContent = content;
            itemPrefab = prefab;
            sortDropdown = dropdown;
            statusText = status;
            backButton = back;
            confirmDialog = dialog;
            cardVisualPrefab = cardPrefab;
            presentationCatalog = presentation;
        }
#endif
    }
}
