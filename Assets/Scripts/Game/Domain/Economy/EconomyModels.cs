using System;

namespace KLTN.Game.Domain.Economy
{
    public enum Currency : byte
    {
        /// <summary>Starter card; cannot be bought.</summary>
        None = 0,
        Coins = 1,
        Silver = 2,
    }

    public enum CardTier : byte
    {
        Weak = 0,
        Mid = 1,
        Strong = 2,
    }

    public static class CardTierWeights
    {
        /// <summary>Deck weight per tier: weaker cards appear more often.</summary>
        public static int Of(CardTier tier)
        {
            switch (tier)
            {
                case CardTier.Weak:
                    return 3;
                case CardTier.Mid:
                    return 2;
                default:
                    return 1;
            }
        }
    }

    public sealed class ShopEntry
    {
        public string CardId { get; }
        public Currency Currency { get; }
        public int Price { get; }
        public CardTier Tier { get; }
        public float PowerScore { get; }

        public bool IsStarter => Currency == Currency.None;

        public ShopEntry(string cardId, Currency currency, int price, CardTier tier, float powerScore)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new ArgumentException("Card ID is required.", nameof(cardId));
            }

            if (currency != Currency.None && price <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(price), "Buyable cards need a positive price.");
            }

            CardId = cardId;
            Currency = currency;
            Price = currency == Currency.None ? 0 : price;
            Tier = tier;
            PowerScore = powerScore;
        }
    }

    public readonly struct Wallet
    {
        public int Coins { get; }
        public int Silver { get; }

        public Wallet(int coins, int silver)
        {
            Coins = coins;
            Silver = silver;
        }

        public int Balance(Currency currency)
        {
            switch (currency)
            {
                case Currency.Coins:
                    return Coins;
                case Currency.Silver:
                    return Silver;
                default:
                    return 0;
            }
        }

        public Wallet Add(int coins, int silver)
        {
            return new Wallet(Coins + coins, Silver + silver);
        }
    }
}
