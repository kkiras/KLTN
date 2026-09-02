using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public interface IRandomSource
    {
        int Next(int minInclusive, int maxExclusive);
    }

    public sealed class SeededRandomSource : IRandomSource
    {
        #region Fields

        private readonly Random random;

        #endregion

        #region Construction

        public SeededRandomSource(int seed)
        {
            random = new Random(seed);
        }

        #endregion

        #region IRandomSource

        public int Next(int minInclusive, int maxExclusive)
        {
            return random.Next(minInclusive, maxExclusive);
        }

        #endregion
    }

    public sealed class MatchFactory
    {
        #region Fields

        private readonly IRandomSource random;

        #endregion

        #region Construction

        public MatchFactory(IRandomSource random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        #endregion

        #region Public API

        public MatchState Create(
            IReadOnlyList<CardDefinition> definitions,
            int deckSize,
            int openingHandSize,
            SeatId firstSeat = SeatId.Host)
        {
            if (definitions == null || definitions.Count == 0)
            {
                throw new ArgumentException("At least one card definition is required.", nameof(definitions));
            }

            if (deckSize <= 0) { throw new ArgumentOutOfRangeException(nameof(deckSize)); }

            if (openingHandSize < 0 || openingHandSize > deckSize) { throw new ArgumentOutOfRangeException(nameof(openingHandSize)); }

            var state = new MatchState(firstSeat);
            state.Host.InitializeMana(1);
            state.Guest.InitializeMana(1);
            ulong nextInstanceId = 1;
            BuildDeck(state.Host, definitions, deckSize, ref nextInstanceId);
            BuildDeck(state.Guest, definitions, deckSize, ref nextInstanceId);
            Shuffle(state.Host.Deck);
            Shuffle(state.Guest.Deck);
            DrawCards(state.Host, openingHandSize);
            DrawCards(state.Guest, openingHandSize);
            return state;
        }

        #endregion

        #region Deck Construction

        private static void BuildDeck(
            PlayerState player,
            IReadOnlyList<CardDefinition> definitions,
            int deckSize,
            ref ulong nextInstanceId)
        {
            for (int i = 0; i < deckSize; i++)
            {
                CardDefinition definition = definitions[i % definitions.Count];
                player.Deck.Add(new CardInstance(
                    nextInstanceId++,
                    definition.Id,
                    player.Seat,
                    CardZone.Deck,
                    definition.BaseHealth));
            }
        }

        private void Shuffle(List<CardInstance> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int otherIndex = random.Next(0, i + 1);
                CardInstance temporary = cards[i];
                cards[i] = cards[otherIndex];
                cards[otherIndex] = temporary;
            }
        }

        private static void DrawCards(PlayerState player, int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                if (player.Deck.Count == 0) { return; }

                int topIndex = player.Deck.Count - 1;
                CardInstance card = player.Deck[topIndex];
                player.Deck.RemoveAt(topIndex);
                card.MoveTo(CardZone.Hand);
                player.Hand.Add(card);
            }
        }

        #endregion
    }
}
