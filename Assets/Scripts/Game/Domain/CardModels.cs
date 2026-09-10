using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public enum CardZone : byte
    {
        Deck,
        Hand,
        Board,
        Graveyard
    }

    /// <summary>
    /// Immutable card definition shared by all card instances.
    /// </summary>
    public sealed class CardDefinition
    {
        #region Properties

        public string Id { get; }
        public string DisplayName { get; }
        public int BaseHealth { get; }
        public int BaseDamage { get; }
        public int Cost { get; }

        #endregion

        #region Construction

        public CardDefinition(string id, string displayName, int baseHealth, int baseDamage, int cost)
        {
            if (string.IsNullOrWhiteSpace(id)) { throw new ArgumentException("Card definition ID is required.", nameof(id)); }

            Id = id;
            DisplayName = displayName ?? id;
            BaseHealth = Math.Max(0, baseHealth);
            BaseDamage = Math.Max(0, baseDamage);
            Cost = Math.Max(0, cost);
        }

        #endregion
    }

    /// <summary>
    /// Mutable state belonging to one physical card in one match.
    /// </summary>
    public sealed class CardInstance
    {
        #region Properties

        public ulong InstanceId { get; }
        public string DefinitionId { get; }
        public SeatId Owner { get; }
        public CardZone Zone { get; private set; }
        public int CurrentHealth { get; private set; }
        public int BoardSlotIndex { get; private set; } = -1;
        public bool IsDead => CurrentHealth <= 0;

        #endregion

        #region Construction

        public CardInstance(ulong instanceId, string definitionId, SeatId owner, CardZone zone, int startingHealth)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Owner = owner;
            Zone = zone;
            CurrentHealth = Math.Max(0, startingHealth);
        }

        #endregion

        #region State Mutations

        public void MoveTo(CardZone zone, int boardSlotIndex = -1)
        {
            Zone = zone;
            BoardSlotIndex = zone == CardZone.Board
                ? boardSlotIndex
                : -1;
        }

        public void ApplyDamage(int amount)
        {
            if (amount <= 0) { return; }

            CurrentHealth = Math.Max(0, CurrentHealth - amount);
        }

        #endregion
    }

    public sealed class PlayerState
    {
        #region Properties and Collections

        public SeatId Seat { get; }

        public int NexusHealth { get; private set; } = 20;
        public int MaxMana { get; private set; }
        public int Mana { get; private set; }

        public List<CardInstance> Deck { get; } = new List<CardInstance>();
        public List<CardInstance> Hand { get; } = new List<CardInstance>();
        public List<CardInstance> Board { get; } = new List<CardInstance>();
        public List<CardInstance> Graveyard { get; } = new List<CardInstance>();

        #endregion

        #region Construction

        public PlayerState(SeatId seat)
        {
            Seat = seat;
        }

        #endregion

        #region Resources and Health

        public void InitializeMana(int amount)
        {
            MaxMana = Math.Max(0, amount);
            Mana = MaxMana;
        }

        public void AdvanceAndRefillMana(int maximumMana)
        {
            MaxMana = Math.Min(maximumMana, MaxMana + 1);
            Mana = MaxMana;
        }

        public bool TrySpendMana(int amount)
        {
            if (amount < 0 || Mana < amount) { return false; }

            Mana -= amount;
            return true;
        }

        public void ApplyNexusDamage(int amount)
        {
            if (amount <= 0) { return; }

            NexusHealth = Math.Max(0, NexusHealth - amount);
        }

        #endregion

        #region Card Queries and Zone Transitions

        public CardInstance FindCardInHand(ulong instanceId)
        {
            return Hand.Find(card => card.InstanceId == instanceId);
        }

        public CardInstance FindBoardCard(int slotIndex)
        {
            return Board.Find(card => card.BoardSlotIndex == slotIndex);
        }

        public void MoveHandCardToBoard(CardInstance card, int slotIndex)
        {
            Hand.Remove(card);
            card.MoveTo(CardZone.Board, slotIndex);
            Board.Add(card);
        }

        public bool DrawOne()
        {
            if (Deck.Count == 0) { return false; }

            int topIndex = Deck.Count - 1;
            CardInstance card = Deck[topIndex];
            Deck.RemoveAt(topIndex);
            card.MoveTo(CardZone.Hand);
            Hand.Add(card);
            return true;
        }

        public void MoveDeadBoardCardsToGraveyard()
        {
            for (int i = Board.Count - 1; i >= 0; i--)
            {
                CardInstance card = Board[i];

                if (!card.IsDead) { continue; }

                Board.RemoveAt(i);
                card.MoveTo(CardZone.Graveyard);
                Graveyard.Add(card);
            }
        }

        #endregion

        #region Mulligan Logic

        public void PerformMulligan(ulong[] cardIdsToReplace, Random rng)
        {
            if (cardIdsToReplace == null || cardIdsToReplace.Length == 0) return;

            int replaceCount = 0;


            for (int i = Hand.Count - 1; i >= 0; i--)
            {
                if (Array.Exists(cardIdsToReplace, id => id == Hand[i].InstanceId))
                {
                    CardInstance card = Hand[i];
                    Hand.RemoveAt(i);
                    card.MoveTo(CardZone.Deck);
                    Deck.Add(card);
                    replaceCount++;
                }
            }


            ShuffleDeck(rng);


            for (int i = 0; i < replaceCount; i++)
            {
                DrawOne();
            }
        }

        private void ShuffleDeck(Random rng)
        {
            int n = Deck.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                CardInstance value = Deck[k];
                Deck[k] = Deck[n];
                Deck[n] = value;
            }
        }

        #endregion
    }

    public enum MatchOutcome : byte
    {
        Running,
        HostWon,
        GuestWon,
        Draw
    }

    public sealed class MatchState
    {
        #region Constants

        public const int BoardSlotCount = 3;

        #endregion

        #region Match State

        public PlayerState Host { get; } = new PlayerState(SeatId.Host);
        public PlayerState Guest { get; } = new PlayerState(SeatId.Guest);

        public SeatId FirstSeat { get; }
        public SeatId ActiveSeat { get; set; }

        public int RoundNumber { get; set; } = 0;

        public bool HostMulliganDone { get; set; }
        public bool GuestMulliganDone { get; set; }
        public bool IsMulliganPhaseComplete => HostMulliganDone && GuestMulliganDone;

        public int ActionsCompletedInRound { get; set; }

        public MatchOutcome Outcome { get; set; } = MatchOutcome.Running;
        public string LastEvent { get; set; } = "Đang chờ đối thủ đổi bài (Mulligan)...";
        public bool IsFinished => Outcome != MatchOutcome.Running;

        #endregion

        #region Construction

        public MatchState() : this(SeatId.Host)
        {
        }

        public MatchState(SeatId firstSeat)
        {
            if (firstSeat != SeatId.Host && firstSeat != SeatId.Guest) { throw new ArgumentOutOfRangeException(nameof(firstSeat)); }

            FirstSeat = firstSeat;
            ActiveSeat = firstSeat;
        }

        #endregion

        #region Player Access

        public PlayerState Player(SeatId seat)
        {
            switch (seat)
            {
                case SeatId.Host:
                    return Host;

                case SeatId.Guest:
                    return Guest;

                default:
                    throw new ArgumentOutOfRangeException(nameof(seat));
            }
        }

        #endregion
    }
}
