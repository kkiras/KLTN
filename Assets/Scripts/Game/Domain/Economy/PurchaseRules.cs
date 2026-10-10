namespace KLTN.Game.Domain.Economy
{
    public enum PurchaseRejection : byte
    {
        None = 0,
        NotForSale = 1,
        AlreadyOwned = 2,
        InsufficientFunds = 3,
    }

    /// <summary>
    /// Client-side mirror of the purchase checks enforced by Firestore Security Rules.
    /// Used to enable/disable the Buy button; the server-side check is authoritative.
    /// </summary>
    public static class PurchaseRules
    {
        public static PurchaseRejection Check(ShopEntry entry, bool alreadyOwned, Wallet wallet)
        {
            if (entry == null || entry.IsStarter)
            {
                return PurchaseRejection.NotForSale;
            }

            if (alreadyOwned)
            {
                return PurchaseRejection.AlreadyOwned;
            }

            if (wallet.Balance(entry.Currency) < entry.Price)
            {
                return PurchaseRejection.InsufficientFunds;
            }

            return PurchaseRejection.None;
        }

        public static Wallet Apply(ShopEntry entry, Wallet wallet)
        {
            return entry.Currency == Currency.Coins
                ? wallet.Add(-entry.Price, 0)
                : wallet.Add(0, -entry.Price);
        }
    }
}
