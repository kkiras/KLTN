using System;
using System.Collections.Generic;
using KLTN.Game.Domain.Ranking;
using UnityEngine;

namespace KLTN.Game.Content
{
    [Serializable]
    public sealed class RankCatalogEntry
    {
        public RankTier tier;
        public string displayName;

        [Tooltip("Assigned by reference: the sheet slices are not numbered contiguously.")]
        public Sprite icon;
    }

    /// <summary>Display name + icon per rank tier (Resources/RankCatalog.asset).</summary>
    [CreateAssetMenu(fileName = "RankCatalog", menuName = "KLTN/Rank Catalog")]
    public sealed class RankCatalog : ScriptableObject
    {
        public const string ResourcesPath = "RankCatalog";

        private static readonly string[] FallbackNames =
        {
            "Đồng", "Bạc", "Vàng", "Bạch Kim", "Kim Cương", "Cao Thủ", "Thách Đấu",
        };

        [SerializeField]
        private List<RankCatalogEntry> entries = new List<RankCatalogEntry>();

        public static RankCatalog Load()
        {
            RankCatalog catalog = Resources.Load<RankCatalog>(ResourcesPath);

            if (catalog == null)
            {
                Debug.LogWarning("[Ranking] Missing Resources/RankCatalog.asset; rank icons will be empty.");
            }

            return catalog;
        }

        public static string FallbackName(RankTier tier)
        {
            int index = (int)tier;
            return index >= 0 && index < FallbackNames.Length ? FallbackNames[index] : tier.ToString();
        }

        public string NameOf(RankTier tier)
        {
            RankCatalogEntry entry = Find(tier);
            return entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : FallbackName(tier);
        }

        public Sprite IconOf(RankTier tier)
        {
            return Find(tier)?.icon;
        }

        private RankCatalogEntry Find(RankTier tier)
        {
            foreach (RankCatalogEntry entry in entries)
            {
                if (entry != null && entry.tier == tier)
                {
                    return entry;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        public void EditorReplace(IReadOnlyList<Sprite> iconsInTierOrder)
        {
            entries.Clear();

            for (int index = 0; index < RankPolicy.TierCount; index++)
            {
                entries.Add(new RankCatalogEntry
                {
                    tier = (RankTier)index,
                    displayName = FallbackNames[index],
                    icon = iconsInTierOrder != null && index < iconsInTierOrder.Count ? iconsInTierOrder[index] : null,
                });
            }
        }
#endif
    }
}
