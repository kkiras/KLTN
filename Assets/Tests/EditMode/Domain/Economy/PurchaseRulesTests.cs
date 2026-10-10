using KLTN.Game.Domain.Economy;
using NUnit.Framework;

namespace KLTN.Game.Domain.Tests
{
    public sealed class PurchaseRulesTests
    {
        private static readonly ShopEntry CoinCard = new ShopEntry("MaCo", Currency.Coins, 350, CardTier.Mid, 10.2f);
        private static readonly ShopEntry SilverCard = new ShopEntry("QuyDaXoa", Currency.Silver, 15, CardTier.Strong, 34f);
        private static readonly ShopEntry StarterCard = new ShopEntry("MaDa", Currency.None, 0, CardTier.Weak, 3.6f);

        [Test]
        public void Check_AllowsWhenEnoughBalance()
        {
            Assert.AreEqual(PurchaseRejection.None, PurchaseRules.Check(CoinCard, false, new Wallet(350, 0)));
            Assert.AreEqual(PurchaseRejection.None, PurchaseRules.Check(SilverCard, false, new Wallet(0, 15)));
        }

        [Test]
        public void Check_RejectsWrongCurrencyBalance()
        {
            Assert.AreEqual(PurchaseRejection.InsufficientFunds, PurchaseRules.Check(SilverCard, false, new Wallet(9999, 14)));
        }

        [Test]
        public void Check_RejectsOwnedAndStarterCards()
        {
            Assert.AreEqual(PurchaseRejection.AlreadyOwned, PurchaseRules.Check(CoinCard, true, new Wallet(9999, 99)));
            Assert.AreEqual(PurchaseRejection.NotForSale, PurchaseRules.Check(StarterCard, false, new Wallet(9999, 99)));
        }

        [Test]
        public void Apply_DeductsOnlyMatchingCurrency()
        {
            Wallet after = PurchaseRules.Apply(SilverCard, new Wallet(100, 20));

            Assert.AreEqual(100, after.Coins);
            Assert.AreEqual(5, after.Silver);
        }
    }
}
