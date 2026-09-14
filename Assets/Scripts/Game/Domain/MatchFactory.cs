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
        private readonly Random random;

        public SeededRandomSource(int seed)
        {
            random = new Random(seed);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            return random.Next(minInclusive, maxExclusive);
        }
    }

    public sealed class MatchFactory
    {
        private readonly IRandomSource random;

        public MatchFactory(IRandomSource random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public MatchState Create(
            IReadOnlyList<CardDefinition> hostDeckDefinitions,
            IReadOnlyList<CardDefinition> guestDeckDefinitions,
            int openingHandSize,
            SeatId firstSeat = SeatId.Host
        )
        {
            DeckRules.Validate(hostDeckDefinitions, "Host deck");

            DeckRules.Validate(guestDeckDefinitions, "Guest deck");

            if (openingHandSize < 0 || openingHandSize > DeckRules.RequiredCardCount)
            {
                throw new ArgumentOutOfRangeException(nameof(openingHandSize));
            }

            var state = new MatchState(firstSeat);

            state.Host.InitializeMana(1);
            state.Guest.InitializeMana(1);

            ulong nextInstanceId = 1;

            BuildDeck(state.Host, hostDeckDefinitions, ref nextInstanceId);

            BuildDeck(state.Guest, guestDeckDefinitions, ref nextInstanceId);

            state.EnsureNextCardInstanceIdAtLeast(nextInstanceId);

            Shuffle(state.Host.Deck);
            Shuffle(state.Guest.Deck);

            DrawCards(state.Host, openingHandSize);
            DrawCards(state.Guest, openingHandSize);

            return state;
        }

        private static void BuildDeck(
            PlayerState player,
            IReadOnlyList<CardDefinition> definitions,
            ref ulong nextInstanceId
        )
        {
            foreach (CardDefinition definition in definitions)
            {
                player.Deck.Add(
                    new CardInstance(
                        nextInstanceId++,
                        definition.Id,
                        player.Seat,
                        CardZone.Deck,
                        definition.BaseHealth
                    )
                );
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
                if (!player.DrawOne())
                {
                    return;
                }
            }
        }
    }
}
