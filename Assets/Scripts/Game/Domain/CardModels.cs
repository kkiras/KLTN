using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public enum CardZone : byte
    {
        Deck = 0,
        DrawHand = 1,
        Reserve = 2,
        Board = 3,
        Graveyard = 4,
    }

    public enum MatchPhase : byte
    {
        Mulligan = 0,
        RoundStart = 1,
        Priority = 2,
        BlockDeclaration = 3,
        CombatResolution = 4,
        RoundEnd = 5,
        Finished = 6,
        AbilitySelection = 7,
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

        public int MaximumCopiesPerDeck { get; }

        public UnitKeyword Keywords { get; }
        public IReadOnlyList<AbilityDefinition> Abilities { get; }

        public UnitPassiveRules PassiveRules { get; }

        public string RulesText { get; }

        #endregion

        #region Construction

        public CardDefinition(
            string id,
            string displayName,
            int baseHealth,
            int baseDamage,
            int cost,
            UnitKeyword keywords = UnitKeyword.None,
            IReadOnlyList<AbilityDefinition> abilities = null,
            string rulesText = "",
            UnitPassiveRules passiveRules = null,
            int maximumCopiesPerDeck = 0
        )
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Card definition ID is required.",
                    nameof(id)
                );
            }

            Id = id;
            DisplayName = displayName ?? id;

            BaseHealth = Math.Max(0, baseHealth);
            BaseDamage = Math.Max(0, baseDamage);
            Cost = Math.Max(0, cost);

            Keywords = keywords;
            Abilities = CopyAbilities(abilities);
            RulesText = rulesText ?? string.Empty;

            if (maximumCopiesPerDeck < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumCopiesPerDeck),
                    "Maximum copies per deck cannot be negative."
                );
            }

            MaximumCopiesPerDeck = maximumCopiesPerDeck;

            PassiveRules = passiveRules ?? UnitPassiveRules.None;
        }

        private static IReadOnlyList<AbilityDefinition> CopyAbilities(
            IReadOnlyList<AbilityDefinition> abilities
        )
        {
            if (abilities == null || abilities.Count == 0)
            {
                return Array.Empty<AbilityDefinition>();
            }

            var copy = new AbilityDefinition[abilities.Count];

            for (int i = 0; i < abilities.Count; i++)
            {
                if (abilities[i] == null)
                {
                    throw new ArgumentException(
                        "Abilities cannot contain null values.",
                        nameof(abilities)
                    );
                }

                copy[i] = abilities[i];
            }

            return Array.AsReadOnly(copy);
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

        public int PermanentDamageModifier { get; private set; }
        public int PermanentHealthModifier { get; private set; }

        public int RoundDamageModifier { get; private set; }
        public int RoundHealthModifier { get; private set; }

        public int PermanentCostModifier { get; private set; }
        public int RoundCostModifier { get; private set; }

        public bool IsDead => CurrentHealth <= 0;

        public bool IsInActiveRoster =>
            Zone == CardZone.Reserve || Zone == CardZone.Board;

        #endregion

        #region Construction

        public CardInstance(
            ulong instanceId,
            string definitionId,
            SeatId owner,
            CardZone zone,
            int startingHealth
        )
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Owner = owner;
            Zone = zone;
            CurrentHealth = Math.Max(0, startingHealth);
        }

        #endregion

        #region Runtime Stats

        public int GetDamage(CardDefinition definition)
        {
            if (definition == null)
            {
                return 0;
            }

            return Math.Max(
                0,
                definition.BaseDamage + PermanentDamageModifier + RoundDamageModifier
            );
        }

        public int GetMaximumHealth(CardDefinition definition)
        {
            if (definition == null)
            {
                return 0;
            }

            return Math.Max(
                0,
                definition.BaseHealth + PermanentHealthModifier + RoundHealthModifier
            );
        }

        public int GetCost(CardDefinition definition)
        {
            if (definition == null)
            {
                return 0;
            }

            return Math.Max(
                0,
                definition.Cost + PermanentCostModifier + RoundCostModifier
            );
        }

        public void ApplyCostReduction(
            CardDefinition definition,
            int amount,
            EffectDuration duration
        )
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            int reduction = Math.Max(0, amount);

            switch (duration)
            {
                case EffectDuration.Permanent:
                    PermanentCostModifier -= reduction;
                    break;

                case EffectDuration.ThisRound:
                    RoundCostModifier -= reduction;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(duration));
            }
        }

        public void ApplyBuff(
            CardDefinition definition,
            int damageAmount,
            int healthAmount,
            EffectDuration duration
        )
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            switch (duration)
            {
                case EffectDuration.Permanent:
                    PermanentDamageModifier += damageAmount;
                    PermanentHealthModifier += healthAmount;
                    break;

                case EffectDuration.ThisRound:
                    RoundDamageModifier += damageAmount;
                    RoundHealthModifier += healthAmount;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(duration));
            }

            CurrentHealth = Math.Max(0, CurrentHealth + healthAmount);

            CurrentHealth = Math.Min(CurrentHealth, GetMaximumHealth(definition));
        }

        public void ClearRoundModifiers(CardDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            RoundDamageModifier = 0;
            RoundHealthModifier = 0;
            RoundCostModifier = 0;

            CurrentHealth = Math.Min(CurrentHealth, GetMaximumHealth(definition));
        }

        public void ClearRoundModifiersOnDeath()
        {
            if (!IsDead)
            {
                throw new InvalidOperationException(
                    "Only dead cards can clear round modifiers on death."
                );
            }

            RoundDamageModifier = 0;
            RoundHealthModifier = 0;
            RoundCostModifier = 0;
        }

        public void ResetToBase(CardDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            PermanentDamageModifier = 0;
            PermanentHealthModifier = 0;
            PermanentCostModifier = 0;

            RoundDamageModifier = 0;
            RoundHealthModifier = 0;
            RoundCostModifier = 0;

            CurrentHealth = definition.BaseHealth;

            BoardSlotIndex = -1;
        }

        #endregion

        #region State Mutations

        public void MoveTo(CardZone zone, int boardSlotIndex = -1)
        {
            Zone = zone;

            BoardSlotIndex = zone == CardZone.Board ? boardSlotIndex : -1;
        }

        public void ApplyDamage(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            CurrentHealth = Math.Max(0, CurrentHealth - amount);
        }

        public void Heal(CardDefinition definition, int amount)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (amount <= 0 || IsDead)
            {
                return;
            }

            CurrentHealth = Math.Min(
                GetMaximumHealth(definition),
                CurrentHealth + amount
            );
        }

        public void Kill()
        {
            CurrentHealth = 0;
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

        public List<CardInstance> DrawHand { get; } = new List<CardInstance>();

        public List<CardInstance> Reserve { get; } = new List<CardInstance>();

        public List<CardInstance> Board { get; } = new List<CardInstance>();

        public List<CardInstance> Graveyard { get; } = new List<CardInstance>();

        public int ActiveRosterCount => Reserve.Count + Board.Count;

        public bool HasActiveRosterSpace =>
            ActiveRosterCount < MatchState.MaximumActiveRosterSize;

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
            if (amount < 0 || Mana < amount)
            {
                return false;
            }

            Mana -= amount;
            return true;
        }

        public void ApplyNexusDamage(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            NexusHealth = Math.Max(0, NexusHealth - amount);
        }

        public void HealNexus(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            NexusHealth = Math.Min(20, NexusHealth + amount);
        }

        #endregion

        #region Card Queries and Zone Transitions

        public CardInstance FindCardInDrawHand(ulong instanceId)
        {
            return DrawHand.Find(card => card.InstanceId == instanceId);
        }

        public CardInstance FindCardInReserve(ulong instanceId)
        {
            return Reserve.Find(card => card.InstanceId == instanceId);
        }

        public CardInstance FindBoardCard(int slotIndex)
        {
            return Board.Find(card => card.BoardSlotIndex == slotIndex);
        }

        public CardInstance FindCardAnywhere(ulong instanceId)
        {
            CardInstance card = Deck.Find(item => item.InstanceId == instanceId);

            if (card != null)
            {
                return card;
            }

            card = DrawHand.Find(item => item.InstanceId == instanceId);

            if (card != null)
            {
                return card;
            }

            card = Reserve.Find(item => item.InstanceId == instanceId);

            if (card != null)
            {
                return card;
            }

            card = Board.Find(item => item.InstanceId == instanceId);

            if (card != null)
            {
                return card;
            }

            return Graveyard.Find(item => item.InstanceId == instanceId);
        }

        public bool CanAddToActiveRoster(int incomingCount = 1)
        {
            if (incomingCount < 0)
            {
                return false;
            }

            return ActiveRosterCount + incomingCount
                <= MatchState.MaximumActiveRosterSize;
        }

        public bool TryMoveDrawHandCardToReserve(CardInstance card)
        {
            if (
                card == null
                || card.Owner != Seat
                || card.Zone != CardZone.DrawHand
                || card.IsDead
                || !DrawHand.Contains(card)
            )
            {
                return false;
            }

            if (!CanAddToActiveRoster())
            {
                return false;
            }

            DrawHand.Remove(card);
            card.MoveTo(CardZone.Reserve);
            Reserve.Add(card);

            return true;
        }

        public bool TrySummonDrawHandCard(CardInstance card, int manaCost)
        {
            if (manaCost < 0 || Mana < manaCost)
            {
                return false;
            }

            if (!TryMoveDrawHandCardToReserve(card))
            {
                return false;
            }

            Mana -= manaCost;
            return true;
        }

        public bool TryAddCreatedCardToDrawHand(CardInstance card)
        {
            if (
                card == null
                || card.Owner != Seat
                || card.Zone != CardZone.DrawHand
                || card.IsDead
                || FindCardAnywhere(card.InstanceId) != null
            )
            {
                return false;
            }

            DrawHand.Add(card);
            return true;
        }

        public bool TryAddCreatedCardToReserve(CardInstance card)
        {
            if (
                card == null
                || card.Owner != Seat
                || card.Zone != CardZone.Reserve
                || card.IsDead
                || FindCardAnywhere(card.InstanceId) != null
                || !HasActiveRosterSpace
            )
            {
                return false;
            }

            Reserve.Add(card);
            return true;
        }

        public bool TryReviveCardToReserve(CardInstance card, CardDefinition definition)
        {
            if (
                card == null
                || definition == null
                || card.Owner != Seat
                || card.Zone != CardZone.Graveyard
                || !card.IsDead
                || !Graveyard.Contains(card)
                || !string.Equals(
                    card.DefinitionId,
                    definition.Id,
                    StringComparison.Ordinal
                )
                || !HasActiveRosterSpace
            )
            {
                return false;
            }

            Graveyard.Remove(card);

            card.ResetToBase(definition);
            card.MoveTo(CardZone.Reserve);

            Reserve.Add(card);
            return true;
        }

        public bool TryReviveCardToBoard(
            CardInstance card,
            CardDefinition definition,
            int slotIndex
        )
        {
            if (
                card == null
                || definition == null
                || card.Owner != Seat
                || card.Zone != CardZone.Graveyard
                || !card.IsDead
                || !Graveyard.Contains(card)
                || !string.Equals(
                    card.DefinitionId,
                    definition.Id,
                    StringComparison.Ordinal
                )
                || !HasActiveRosterSpace
            )
            {
                return false;
            }

            if (
                slotIndex < 0
                || slotIndex >= MatchState.BoardSlotCount
                || Board.Count >= MatchState.BoardSlotCount
                || FindBoardCard(slotIndex) != null
            )
            {
                return false;
            }

            Graveyard.Remove(card);

            card.ResetToBase(definition);
            card.MoveTo(CardZone.Board, slotIndex);

            Board.Add(card);
            return true;
        }

        public bool TryReturnCardToDrawHand(CardInstance card, CardDefinition definition)
        {
            if (
                card == null
                || definition == null
                || card.Owner != Seat
                || !string.Equals(
                    card.DefinitionId,
                    definition.Id,
                    StringComparison.Ordinal
                )
            )
            {
                return false;
            }

            bool removed;

            switch (card.Zone)
            {
                case CardZone.Reserve:
                    removed = Reserve.Remove(card);
                    break;

                case CardZone.Board:
                    removed = Board.Remove(card);
                    break;

                case CardZone.Graveyard:
                    removed = Graveyard.Remove(card);
                    break;

                default:
                    return false;
            }

            if (!removed)
            {
                return false;
            }

            card.ResetToBase(definition);
            card.MoveTo(CardZone.DrawHand);

            DrawHand.Add(card);
            return true;
        }

        public bool TryMoveReserveCardToBoard(CardInstance card, int slotIndex)
        {
            if (
                card == null
                || card.Owner != Seat
                || card.Zone != CardZone.Reserve
                || card.IsDead
                || !Reserve.Contains(card)
            )
            {
                return false;
            }

            if (slotIndex < 0 || slotIndex >= MatchState.BoardSlotCount)
            {
                return false;
            }

            if (
                Board.Count >= MatchState.BoardSlotCount
                || FindBoardCard(slotIndex) != null
            )
            {
                return false;
            }

            Reserve.Remove(card);
            card.MoveTo(CardZone.Board, slotIndex);
            Board.Add(card);

            return true;
        }

        public bool TryMoveBoardCardToReserve(CardInstance card)
        {
            if (
                card == null
                || card.Owner != Seat
                || card.Zone != CardZone.Board
                || card.IsDead
                || !Board.Contains(card)
            )
            {
                return false;
            }

            if (Reserve.Count >= MatchState.MaximumActiveRosterSize)
            {
                return false;
            }

            Board.Remove(card);
            card.MoveTo(CardZone.Reserve);
            Reserve.Add(card);

            return true;
        }

        public void ReturnBoardCardsToReserve()
        {
            // Dead cards must never return to Reserve.
            MoveDeadBoardCardsToGraveyard();

            // Preserve the visual left-to-right board order.
            Board.Sort(
                (left, right) => left.BoardSlotIndex.CompareTo(right.BoardSlotIndex)
            );

            while (Board.Count > 0)
            {
                CardInstance card = Board[0];

                Board.RemoveAt(0);
                card.MoveTo(CardZone.Reserve);
                Reserve.Add(card);
            }
        }

        public bool DrawOne()
        {
            if (Deck.Count == 0)
            {
                return false;
            }

            int topIndex = Deck.Count - 1;
            CardInstance card = Deck[topIndex];

            Deck.RemoveAt(topIndex);
            card.MoveTo(CardZone.DrawHand);
            DrawHand.Add(card);

            return true;
        }

        public IReadOnlyList<CardInstance> MoveDeadBoardCardsToGraveyard()
        {
            var deadCards = new List<CardInstance>();

            foreach (CardInstance card in Board)
            {
                if (card.IsDead)
                {
                    deadCards.Add(card);
                }
            }

            deadCards.Sort(
                (left, right) => left.BoardSlotIndex.CompareTo(right.BoardSlotIndex)
            );

            foreach (CardInstance card in deadCards)
            {
                Board.Remove(card);

                card.ClearRoundModifiersOnDeath();
                card.MoveTo(CardZone.Graveyard);

                Graveyard.Add(card);
            }

            return deadCards;
        }

        public bool TryMoveDeadActiveCardToGraveyard(CardInstance card)
        {
            if (card == null || card.Owner != Seat || !card.IsDead)
            {
                return false;
            }

            bool removed;

            switch (card.Zone)
            {
                case CardZone.Reserve:
                    removed = Reserve.Remove(card);
                    break;

                case CardZone.Board:
                    removed = Board.Remove(card);
                    break;

                default:
                    return false;
            }

            if (!removed)
            {
                return false;
            }

            card.ClearRoundModifiersOnDeath();
            card.MoveTo(CardZone.Graveyard);

            Graveyard.Add(card);

            return true;
        }

        #endregion

        #region Mulligan Logic

        public void PerformMulligan(ulong[] cardIdsToReplace, Random rng)
        {
            if (cardIdsToReplace == null || cardIdsToReplace.Length == 0)
                return;

            int replaceCount = 0;

            for (int i = DrawHand.Count - 1; i >= 0; i--)
            {
                bool shouldReplace = Array.Exists(
                    cardIdsToReplace,
                    id => id == DrawHand[i].InstanceId
                );

                if (!shouldReplace)
                {
                    continue;
                }

                CardInstance card = DrawHand[i];

                DrawHand.RemoveAt(i);
                card.MoveTo(CardZone.Deck);
                Deck.Add(card);
                replaceCount++;
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
        Draw,
    }

    public sealed class MatchState
    {
        #region Constants

        public const int BoardSlotCount = 3;
        public const int MaximumActiveRosterSize = 4;

        #endregion

        #region Match State

        public PlayerState Host { get; } = new PlayerState(SeatId.Host);
        public PlayerState Guest { get; } = new PlayerState(SeatId.Guest);

        public SeatId FirstSeat { get; }
        public SeatId ActiveSeat { get; set; }

        public SeatId AttackTokenOwner { get; set; }
        public bool AttackTokenAvailable { get; set; }

        public MatchPhase Phase { get; set; } = MatchPhase.Mulligan;

        public int ConsecutivePasses { get; set; }

        public bool CanEndRound => Phase == MatchPhase.Priority && ConsecutivePasses == 1;

        public int RoundNumber { get; set; } = 0;
        public RoundHistory RoundHistory { get; } = new RoundHistory();
        public bool HostMulliganDone { get; set; }
        public bool GuestMulliganDone { get; set; }
        public bool IsMulliganPhaseComplete => HostMulliganDone && GuestMulliganDone;

        public MatchOutcome Outcome { get; set; } = MatchOutcome.Running;
        public string LastEvent { get; set; } = "Đang chờ đối thủ đổi bài (Mulligan)...";
        public bool IsFinished => Outcome != MatchOutcome.Running;
        private ulong nextAbilitySelectionRequestId = 1;
        private ulong nextGeneratedCardInstanceId = 1;
        public PendingAbilitySelection PendingSelection { get; private set; }

        #endregion

        #region Construction

        public MatchState()
            : this(SeatId.Host) { }

        public MatchState(SeatId firstSeat)
        {
            if (firstSeat != SeatId.Host && firstSeat != SeatId.Guest)
            {
                throw new ArgumentOutOfRangeException(nameof(firstSeat));
            }

            FirstSeat = firstSeat;
            ActiveSeat = firstSeat;

            AttackTokenOwner = firstSeat;
            AttackTokenAvailable = false;

            Phase = MatchPhase.Mulligan;
            ConsecutivePasses = 0;
        }

        public void BeginFirstRound()
        {
            if (!IsMulliganPhaseComplete)
            {
                throw new InvalidOperationException(
                    "Both players must complete mulligan first."
                );
            }

            if (RoundNumber > 0)
            {
                return;
            }

            RoundNumber = 1;
            RoundHistory.BeginFirstRound(RoundNumber);
            ActiveSeat = AttackTokenOwner;
            AttackTokenAvailable = true;

            ConsecutivePasses = 0;
            Phase = MatchPhase.Priority;
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

        public void EnsureNextCardInstanceIdAtLeast(ulong nextInstanceId)
        {
            if (nextInstanceId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nextInstanceId));
            }

            if (nextInstanceId > nextGeneratedCardInstanceId)
            {
                nextGeneratedCardInstanceId = nextInstanceId;
            }
        }

        public ulong AllocateCardInstanceId()
        {
            while (ContainsCardInstanceId(nextGeneratedCardInstanceId))
            {
                if (nextGeneratedCardInstanceId == ulong.MaxValue)
                {
                    throw new InvalidOperationException(
                        "Card instance ID space was exhausted."
                    );
                }

                nextGeneratedCardInstanceId++;
            }

            if (nextGeneratedCardInstanceId == ulong.MaxValue)
            {
                throw new InvalidOperationException(
                    "Card instance ID space was exhausted."
                );
            }

            return nextGeneratedCardInstanceId++;
        }

        private bool ContainsCardInstanceId(ulong instanceId)
        {
            return Host.FindCardAnywhere(instanceId) != null
                || Guest.FindCardAnywhere(instanceId) != null;
        }

        #endregion

        public bool TryOpenPendingSelection(
            TriggeredAbility triggeredAbility,
            bool canCancel = false
        )
        {
            if (triggeredAbility == null)
            {
                throw new ArgumentNullException(nameof(triggeredAbility));
            }

            if (
                PendingSelection != null
                || triggeredAbility.Ability.RequiredTargets.Count == 0
            )
            {
                return false;
            }

            MatchPhase resumePhase = Phase;

            PendingSelection = new PendingAbilitySelection(
                nextAbilitySelectionRequestId++,
                triggeredAbility,
                resumePhase,
                canCancel
            );

            Phase = MatchPhase.AbilitySelection;

            return true;
        }

        public bool TryClosePendingSelection(ulong requestId)
        {
            if (PendingSelection == null || PendingSelection.RequestId != requestId)
            {
                return false;
            }

            MatchPhase resumePhase = PendingSelection.ResumePhase;

            PendingSelection = null;
            Phase = resumePhase;

            return true;
        }
    }
}
