using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace KLTN.Game.Domain.Tests
{
    public sealed class DeckRulesTests
    {
        [Test]
        public void Validate_AllowsOneLimitedCard()
        {
            CardDefinition amBinh = Definition("AmBinh", maximumCopies: 1);

            CardDefinition filler = Definition("Filler");

            List<CardDefinition> deck = FilledDeck(filler);

            deck[0] = amBinh;

            Assert.DoesNotThrow(() => DeckRules.Validate(deck, "Test deck"));
        }

        [Test]
        public void Validate_RejectsSecondLimitedCard()
        {
            CardDefinition amBinh = Definition("AmBinh", maximumCopies: 1);

            CardDefinition filler = Definition("Filler");

            List<CardDefinition> deck = FilledDeck(filler);

            deck[0] = amBinh;
            deck[1] = amBinh;

            Assert.Throws<InvalidOperationException>(() =>
                DeckRules.Validate(deck, "Test deck")
            );
        }

        [Test]
        public void Validate_RejectsWrongDeckSize()
        {
            var deck = new List<CardDefinition> { Definition("OnlyCard") };

            Assert.Throws<InvalidOperationException>(() =>
                DeckRules.Validate(deck, "Test deck")
            );
        }

        [Test]
        public void DefaultDeckBuilder_SkipsCopiesBeyondLimit()
        {
            CardDefinition amBinh = Definition("AmBinh", maximumCopies: 1);

            CardDefinition filler = Definition("Filler");

            IReadOnlyList<CardDefinition> deck = DefaultDeckBuilder.Build(
                new[] { amBinh, filler }
            );

            int amBinhCount = 0;

            foreach (CardDefinition definition in deck)
            {
                if (definition.Id == "AmBinh")
                {
                    amBinhCount++;
                }
            }

            Assert.AreEqual(20, deck.Count);
            Assert.AreEqual(1, amBinhCount);
        }

        private static CardDefinition Definition(string id, int maximumCopies = 0)
        {
            return new CardDefinition(
                id,
                id,
                1,
                1,
                1,
                maximumCopiesPerDeck: maximumCopies
            );
        }

        private static List<CardDefinition> FilledDeck(CardDefinition filler)
        {
            var deck = new List<CardDefinition>();

            for (int i = 0; i < DeckRules.RequiredCardCount; i++)
            {
                deck.Add(filler);
            }

            return deck;
        }
    }
}
