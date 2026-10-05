using KLTN.Infrastructure.Economy;
using TMPro;
using UnityEngine;

namespace KLTN.UI.Shop
{
    public sealed class ShopTopBarView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private TextMeshProUGUI silverText;

        public void Bind(PlayerProfile profile)
        {
            if (coinsText != null)
            {
                coinsText.text = profile != null ? $"{profile.Wallet.Coins:N0} xu" : "— xu";
            }

            if (silverText != null)
            {
                silverText.text = profile != null ? $"{profile.Wallet.Silver:N0} bạc" : "— bạc";
            }
        }

#if UNITY_EDITOR
        public void EditorWire(TextMeshProUGUI coins, TextMeshProUGUI silver)
        {
            coinsText = coins;
            silverText = silver;
        }
#endif
    }
}
