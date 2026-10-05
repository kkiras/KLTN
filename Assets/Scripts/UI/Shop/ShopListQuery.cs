using System;
using System.Collections.Generic;
using KLTN.Game.Domain.Economy;

namespace KLTN.UI.Shop
{
    /// <summary>Pure filter/sort logic for the shop list (no Unity types, easy to test).</summary>
    public static class ShopListQuery
    {
        public static List<ShopEntry> Apply(IEnumerable<ShopEntry> entries, ShopSortMode mode, Func<string, bool> owns)
        {
            var result = new List<ShopEntry>();

            foreach (ShopEntry entry in entries)
            {
                if (entry == null || entry.IsStarter)
                {
                    continue;
                }

                bool keep = mode switch
                {
                    ShopSortMode.SilverOnly => entry.Currency == Currency.Silver,
                    ShopSortMode.CoinsOnly => entry.Currency == Currency.Coins,
                    ShopSortMode.NotOwnedOnly => !owns(entry.CardId),
                    _ => true,
                };

                if (keep)
                {
                    result.Add(entry);
                }
            }

            result.Sort((a, b) => Compare(a, b, mode));
            return result;
        }

        private static int Compare(ShopEntry a, ShopEntry b, ShopSortMode mode)
        {
            int byMode = mode switch
            {
                // "Bạc tăng/giảm": silver cards first, ordered by silver price; coin cards follow.
                ShopSortMode.SilverAscending => ByCurrency(a, b, Currency.Silver, ascending: true),
                ShopSortMode.SilverDescending => ByCurrency(a, b, Currency.Silver, ascending: false),
                ShopSortMode.CoinsAscending => ByCurrency(a, b, Currency.Coins, ascending: true),
                ShopSortMode.CoinsDescending => ByCurrency(a, b, Currency.Coins, ascending: false),
                _ => ByCurrency(a, b, Currency.Coins, ascending: true),
            };

            return byMode != 0 ? byMode : string.CompareOrdinal(a.CardId, b.CardId);
        }

        private static int ByCurrency(ShopEntry a, ShopEntry b, Currency primary, bool ascending)
        {
            bool aPrimary = a.Currency == primary;
            bool bPrimary = b.Currency == primary;

            if (aPrimary != bPrimary)
            {
                return aPrimary ? -1 : 1;
            }

            int price = a.Price.CompareTo(b.Price);
            return aPrimary && !ascending ? -price : price;
        }
    }
}
