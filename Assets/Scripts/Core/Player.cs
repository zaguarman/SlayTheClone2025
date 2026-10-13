using System;
using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public interface IPlayer : IEntity {
        bool IsPlayer1 { get; } // Property to identify if this is Player 1
        IPlayer Opponent { get; set; }
        List<ICard> Hand { get; }
        IReadOnlyList<BoardSlot> Battlefield { get; }
        IDeck Deck { get; }
        int CardsToDraw { get; set; }
        /// <summary>Cards owed by effects that resolved after this Day's hand was dealt: drawn with the next Day's hand.</summary>
        int ExtraDraws { get; set; }
        int Health { get; }
        /// <summary>
        /// "Once per combat" effects this player's cards have used (keyed by card and effect). It lasts the whole combat:
        /// leaving play, the discard pile and reshuffles don't give the use back.
        /// </summary>
        ISet<string> UsedOncePerCombat { get; }
        /// <summary>
        /// Cards coming back to this player's hand (Extraction, He Didn't Kill Himself): they join the next Day's hand, after
        /// the old one is discarded, as fresh copies (their Persistent Stat Modifications and ability stay).
        /// </summary>
        List<ICard> ComingBack { get; }
        /// <summary>This player's creatures that are off the board for now (Witness Protection Program), and when they come back.</summary>
        List<GoneDark> GoneDark { get; }
        /// <summary>Puts a card that left play on its way back to the hand: it joins the next Day's hand.</summary>
        void ReturnToHand(ICard card);
        void AddToHand(ICard card);
        void RemoveFromHand(ICard card);
        void RemoveFromBattlefield(ICard creature);
        void DiscardCard(ICard card);
        void DiscardHand();
        // Raised with the new value whenever health changes, for whatever displays it
        event Action<int> HealthChanged;
        void DrawCard();

        /// <summary>Lowers the player's health; the caller announces the damage.</summary>
        /// <returns>The damage actually taken: health doesn't go below 0.</returns>
        int TakeDamage(int amount);

        /// <summary>
        /// Heals the player by the specified amount, up to their maximum health.
        /// </summary>
        /// <param name="amount">The amount to heal.</param>
        /// <returns>The actual amount the player was healed.</returns>
        int Heal(int amount);
    }

    public class Player : Entity, IPlayer {
        public const int MaxHandSize = 10;
        public const int BattlefieldSize = 5;
        public const int MaxHealth = 20; // Players start at full health and can't heal past it

        public int Health { get; private set; } = MaxHealth;
        public IPlayer Opponent { get; set; }
        public List<ICard> Hand { get; private set; }
        public IDeck Deck { get; private set; }
        public int CardsToDraw { get; set; } = 2; // Default to drawing 2 cards
        public int ExtraDraws { get; set; }
        public ISet<string> UsedOncePerCombat { get; } = new HashSet<string>();
        public List<ICard> ComingBack { get; } = new List<ICard>();
        public List<GoneDark> GoneDark { get; } = new List<GoneDark>();

        public IReadOnlyList<BoardSlot> Battlefield { get; }
        public event Action<int> HealthChanged;

        private readonly bool isPlayer1;

        private IGameMediator gameMediator;
        private ICardDealingService cardDealingService;

        public Player(string name = "Player", bool isPlayer1 = false) : base(name) {
            Hand = new List<ICard>();
            Battlefield = Enumerable.Range(0, BattlefieldSize).Select(i => new BoardSlot(this, i)).ToList();
            Deck = new Deck();
            this.isPlayer1 = isPlayer1;
        }

        public bool IsPlayer1 => isPlayer1;

        // The player reports hand and board changes to the mediator and draws through the dealer
        public void Initialize(IGameMediator mediator, ICardDealingService dealer) {
            gameMediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            cardDealingService = dealer ?? throw new ArgumentNullException(nameof(dealer));
            Log($"Player ({Name}) initialized with dependencies.", LogTag.Initialization | LogTag.Players);
        }

        public int TakeDamage(int amount) {
            if (amount <= 0) return 0;

            int previousHealth = Health;
            int actualDamage = Math.Min(Health, amount); // Health doesn't go below 0
            if (actualDamage == 0) return 0;

            Health -= actualDamage;
            Log($"{Name} took {actualDamage} damage. Health: {previousHealth} -> {Health}", LogTag.Players | LogTag.Combat);
            HealthChanged?.Invoke(Health);
            // DamagePlayerActionExecutor notifies the mediator
            return actualDamage;
        }

        /// <summary>
        /// Heals the player by the specified amount, up to their maximum health.
        /// </summary>
        /// <param name="amount">The amount to heal.</param>
        /// <returns>The actual amount the player was healed.</returns>
        public int Heal(int amount) {
            if (amount <= 0) {
                return 0; // Cannot heal with non-positive amount
            }

            int previousHealth = Health;
            int maxPossibleHeal = MaxHealth - Health;
            int actualHealAmount = Math.Min(amount, maxPossibleHeal);

            if (actualHealAmount <= 0) {
                return 0; // Already at full health
            }

            Health += actualHealAmount;

            Log($"{Name} healed for {actualHealAmount} (requested {amount}). Health: {previousHealth} -> {Health}/{MaxHealth}",
                LogTag.Players | LogTag.Effects);

            HealthChanged?.Invoke(Health);
            // HealPlayerActionExecutor notifies the mediator
            return actualHealAmount;
        }

        public void AddToHand(ICard card) {
            if (card == null) return;

            if (Hand.Count >= MaxHandSize) {
                Log($"Hand full ({MaxHandSize} cards), cannot add more cards", LogTag.Cards);
                return;
            }

            Hand.Add(card);
            gameMediator?.NotifyHandStateChanged(this);
        }

        public void RemoveFromHand(ICard card) {
            if (card == null) return;
            if (Hand.Remove(card)) {
                Log($"Removed card {card.Name} from hand", LogTag.Cards);
                gameMediator?.NotifyHandStateChanged(this);
            }
        }

        // Single exit point for a card leaving play, whatever its type: a resolved spell, a creature that
        // died or was replaced, or a card discarded from hand. Pulls it out of its current zone and discards it.
        public void DiscardCard(ICard card) {
            if (card == null) return;

            RemoveFromHand(card);
            RemoveFromBattlefield(card);
            // A Gift goes back to the player whose card it is, not the side that held it
            var home = (card as ICreature)?.DeckOwner ?? this;
            home.Deck.AddToDiscardPile(card);
            Log($"Discarded {card.Name}{(home != this ? $" to {home.Name}'s discard pile" : "")}", LogTag.Cards);
        }

        public void ReturnToHand(ICard card) {
            if (card == null) return;
            RemoveFromBattlefield(card);
            var copy = SlayTheClone.Deck.FreshCopy(card);
            ComingBack.Add(copy);
            Log($"{card.Name} is on its way back to {Name}'s hand", LogTag.Cards);
        }

        public void DiscardHand() {
            Log($"Discarding entire hand for {Name}: {Hand.Count} cards", LogTag.Cards);

            if (Hand.Count == 0) return;

            // Empty the hand in one go so listeners get a single HandStateChanged instead of one per card
            var cardsToDiscard = new List<ICard>(Hand);
            Hand.Clear();

            foreach (var card in cardsToDiscard) {
                Deck.AddToDiscardPile(card);
                Log($"Discarded {card.Name}", LogTag.Cards);
            }

            gameMediator?.NotifyHandStateChanged(this);
            Log($"Hand discarded for {Name}", LogTag.Cards);
        }

        public void DrawCard() {
            // Use injected cardDealingService
            if (cardDealingService == null) {
                LogError("Cannot draw card - card dealing service not available", LogTag.Cards);
                return;
            }

            if (Hand.Count >= MaxHandSize) {
                Log($"Hand full ({MaxHandSize} cards), cannot draw more cards", LogTag.Cards);
                return;
            }

            if (cardDealingService.CanDrawCard(this)) {
                cardDealingService.DrawCardForPlayer(this);
            }
        }

        public void RemoveFromBattlefield(ICard card) {
            // Only creatures occupy slots; without this check a spell (null as ICreature) matched the first empty slot
            if (card is not ICreature creature) return;

            var slot = Battlefield.FirstOrDefault(s => s.OccupyingCreature == creature);
            if (slot != null) {
                // Only frees the slot; DiscardCard is responsible for sending the card to the discard pile.
                // The battlefield view sees the creature leave and plays its exit.
                slot.Clear();
                Log($"Removed creature {creature.Name} from battlefield", LogTag.Creatures);
                gameMediator?.NotifyBattlefieldStateChanged(this);
            }
        }

        public override bool IsValidTarget() => true;

        public override string ToString() {
            return $"{Name} - Health: {Health}, Hand: {Hand.Count}, Battlefield: {Battlefield.Count()}";
        }
    }
}
