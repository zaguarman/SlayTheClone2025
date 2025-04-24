using UnityEngine.Events;
using System;
using System.Collections.Generic;
using static DebugLogger;
using System.Linq;
using UnityEngine;
using TMPro;
using System.Threading.Tasks;
using System.Threading;

[Serializable]
public class PlayerDamagedUnityEvent : UnityEvent<int> { }

public interface IPlayer : IEntity, IDamageable {
    bool IsPlayer1 { get; } // Property to identify if this is Player 1
    IPlayer Opponent { get; set; }
    List<ICard> Hand { get; }
    List<BattlefieldSlot> Battlefield { get; }
    IDeck Deck { get; }
    int CardsToDraw { get; set; }
    void AddToHand(ICard card);
    void RemoveFromHand(ICard card);
    void AddToBattlefield(ICard creature, ITarget slotId = null);
    void RemoveFromBattlefield(ICard creature, bool destroyCard = true);
    void DiscardCard(ICard card);
    void DiscardHand();
    PlayerDamagedUnityEvent OnDamaged { get; }
    void InitializeBattlefield(List<BattlefieldSlot> battlefieldSlots);
    void DrawCard();
    void UpdateHealthUI();
    void SetHealthText(TextMeshProUGUI healthText);
    void SetHealth(int newHealth);
}

public class Player : Entity, IPlayer {
    public const int MAX_HAND_SIZE = 10;

    public int Health { get; private set; } = 20;
    public IPlayer Opponent { get; set; }
    public List<ICard> Hand { get; private set; }
    public IDeck Deck { get; private set; }
    public int CardsToDraw { get; set; } = 2; // Default to drawing 2 cards

    public List<BattlefieldSlot> Battlefield { get; private set; }
    public PlayerDamagedUnityEvent OnDamaged { get; } = new PlayerDamagedUnityEvent();

    private readonly bool _isPlayer1; // Add field to store identity
    // is player 1 prop using the isplayer1 method
    public bool Is_Player1 => IsPlayer1;

    private IGameMediator gameMediator; // Changed to interface
    private IGameReferences gameReferences; // Added reference
    private ICardDealingService cardDealingService; // Added reference
    private TextMeshProUGUI healthText;

    // Modify constructor to accept identity
    public Player(string name = "Player", bool isPlayer1 = false) : base(name) {
        Hand = new List<ICard>(); // Initialize lists
        Battlefield = new List<BattlefieldSlot>(); // Initialize list
        Deck = new Deck();
        gameMediator = GameMediator.Instance;

        // Set up the event listener for our own damage event
        OnDamaged.AddListener(OnPlayerDamaged);
        _isPlayer1 = isPlayer1; // Store the identity
    }

    // Implement IsPlayer1 property from interface
    public bool IsPlayer1 => _isPlayer1;

    // New Initialize method to inject dependencies
    public void Initialize(IGameMediator mediator, IGameReferences references, ICardDealingService dealer) {
        if (mediator == null || references == null || dealer == null) {
             LogError($"Player ({Name}) initialization failed: Dependencies cannot be null.", LogTag.Initialization | LogTag.Players);
             return; // Or throw exception
        }
        this.gameMediator = mediator;
        this.gameReferences = references;
        this.cardDealingService = dealer;
        Log($"Player ({Name}) initialized with dependencies.", LogTag.Initialization | LogTag.Players);
        // Note: OnDamaged listener is already added in constructor
    }

    public void SetHealthText(TextMeshProUGUI healthText) {
        this.healthText = healthText;
        UpdateHealthUI(); // Update UI immediately after setting the reference
    }

    private void OnPlayerDamaged(int damage) {
        // This is called whenever this player takes damage
        // Update our UI here
        UpdateHealthUI();
        Log($"{(IsPlayer1 ? "Player 1" : "Player 2")} took {damage} damage, health: {Health}", LogTag.Players | LogTag.Combat);
    }

    public void UpdateHealthUI() {
        if (healthText != null) {
            healthText.text = $"Health: {Health}";
        }
    }

    public void InitializeBattlefield(List<BattlefieldSlot> slots) {
        if (slots == null || slots.Count == 0) {
            LogError("Cannot initialize battlefield with null or empty slots", LogTag.Initialization);
            return;
        }
        Battlefield.Clear();
        Battlefield.AddRange(slots);
        Log($"Initialized battlefield with {slots.Count} slots", LogTag.Initialization);
    }

    // The IsPlayer1() method is replaced by the IsPlayer1 property

    public void TakeDamage(int amount) {
        Health = Math.Max(0, Health - amount);
        OnDamaged.Invoke(amount);
    }

    public void SetHealth(int newHealth) {
        Health = Math.Min(newHealth, 99);

        // Update UI
        UpdateHealthUI();
    }

    public void AddToHand(ICard card) {
        if (card == null) return;

        if (Hand.Count >= MAX_HAND_SIZE) {
            Log($"Hand full ({MAX_HAND_SIZE} cards), cannot add more cards", LogTag.Cards);
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

    public void DiscardCard(ICard card) {
        if (card == null) return;

        if (Hand.Contains(card)) {
            RemoveFromHand(card);

            if (Deck is Deck deck) {
                deck.AddToDiscardPile(card);
                Log($"Discarded card {card.Name} from hand to discard pile", LogTag.Cards);
            }
        }
    }

    public void DiscardHand() {
        Log($"Discarding entire hand for {(IsPlayer1 ? "Player 1" : "Player 2")}: {Hand.Count} cards", LogTag.Cards);

        // Create a copy of the hand to avoid modification during iteration
        List<ICard> cardsToDiscard = new List<ICard>(Hand);

        // Discard each card
        foreach (var card in cardsToDiscard) {
            DiscardCard(card);
        }

        // Hand should be empty now, but let's ensure that
        Hand.Clear();

        // Notify that the hand has changed
        gameMediator?.NotifyHandStateChanged(this);
        Log($"Hand discarded for {(IsPlayer1 ? "Player 1" : "Player 2")}", LogTag.Cards);
    }

    public void DrawCard() {
        // Use injected cardDealingService
        if (cardDealingService == null) {
            LogError("Cannot draw card - card dealing service not available", LogTag.Cards);
            return;
        }

        if (Hand.Count >= MAX_HAND_SIZE) {
            Log($"Hand full ({MAX_HAND_SIZE} cards), cannot draw more cards", LogTag.Cards);
            return;
        }

        if (cardDealingService.CanDrawCard(this)) {
            cardDealingService.DrawCardForPlayer(this);
        }
    }

    public async Task<bool> DrawCardAsync(CancellationToken cancellationToken = default) {
        // Use injected cardDealingService
        if (cardDealingService == null) {
            LogError("Cannot draw card - card dealing service not available", LogTag.Cards);
            return false;
        }

        if (Hand.Count >= MAX_HAND_SIZE) {
            Log($"Hand full ({MAX_HAND_SIZE} cards), cannot draw more cards", LogTag.Cards);
            return false;
        }

        if (cardDealingService.CanDrawCard(this)) {
            await cardDealingService.DrawCardForPlayerAsync(this, cancellationToken);
            return true;
        }
        return false;
    }

    public void AddToBattlefield(ICard card, ITarget slot = null) {
        if (card == null || !(slot is BattlefieldSlot targetSlot)) return;

        // Use injected dependencies
        var mediator = this.gameMediator;
        var references = this.gameReferences;
        if (mediator == null || references == null) {
            LogError($"Mediator or References null when adding {card.Name} to battlefield.", LogTag.Players);
            return;
        }

        // Clear existing card if needed
        if (targetSlot.IsOccupied()) {
            var oldCreature = targetSlot.OccupyingCreature;
            RemoveFromBattlefield(oldCreature);
        }

        // Get the original CardData for this card
        CardData originalData = null;

        // Try to find the original card data in the deck using the new method
        if (card.CardId != null && Deck != null) {
            originalData = Deck.FindOriginalCardDataById(card.CardId);
        }

        // If we couldn't find the original data, create a temporary one from current state
        if (originalData == null) {
            LogWarning($"Could not find original CardData for {card.Name}, using current state", LogTag.Cards);
            // This is a fallback that should rarely happen
            if (card is ICreature creature) {
                var tempData = ScriptableObject.CreateInstance<CreatureData>();
                tempData.cardId = card.CardId;
                tempData.cardName = card.Name;
                tempData.description = card.Description;
                tempData.attack = creature.BaseAttack;
                tempData.health = creature.BaseHealth;
                tempData.speed = creature.BaseSpeed;
                originalData = tempData;
            }
        }

        // Create controller, passing both the original data and the live instance
        var cardController = CardFactory.CreateCardController(card, originalData, this, targetSlot.transform, mediator, references);
        if (cardController != null) {
            targetSlot.AssignCreature(cardController);
            mediator.NotifyBattlefieldStateChanged(this);
        }
    }

    public async Task<bool> AddToBattlefieldAsync(ICard card, ITarget slot = null, CancellationToken cancellationToken = default) {
        if (card == null || slot is not BattlefieldSlot targetSlot) return false;

        // Use injected dependencies
        var mediator = gameMediator;
        var references = gameReferences;
        if (mediator == null || references == null) {
            LogError($"Mediator or References null when adding {card.Name} to battlefield async.", LogTag.Players);
            return false;
        }

        // Clear existing card if needed
        if (targetSlot.IsOccupied()) {
            var oldCreature = targetSlot.OccupyingCreature;
            RemoveFromBattlefield(oldCreature);
        }

        // Get the original CardData for this card (same logic as in AddToBattlefield)
        CardData originalData = null;

        // Try to find the original card data in the deck using the new method
        if (card.CardId != null && Deck != null) {
            originalData = Deck.FindOriginalCardDataById(card.CardId);
        }

        // If we couldn't find the original data, create a temporary one
        if (originalData == null) {
            LogWarning($"Could not find original CardData for {card.Name}, using current state", LogTag.Cards);
            if (card is ICreature creature) {
                var tempData = ScriptableObject.CreateInstance<CreatureData>();
                tempData.cardId = card.CardId;
                tempData.cardName = card.Name;
                tempData.description = card.Description;
                tempData.attack = creature.BaseAttack;
                tempData.health = creature.BaseHealth;
                tempData.speed = creature.BaseSpeed;
                originalData = tempData;
            }
        }

        // Create new card controller asynchronously with dependencies
        var cardController = await CardFactory.CreateCardControllerAsync(card, originalData, this, targetSlot.transform, mediator, references, cancellationToken);
        if (cardController != null) {
            targetSlot.AssignCreature(cardController);
            mediator.NotifyBattlefieldStateChanged(this);
            return true;
        }
        return false;
    }

    public void RemoveFromBattlefield(ICard creature, bool destroyCard = true) {
        if (creature == null) return;

        var slot = Battlefield.FirstOrDefault(s => s.OccupyingCreature == creature as ICreature);
        if (slot != null) {
            // Note: We no longer need to reset modifiers here as the ModifierManager handles this
            // when the creature is unregistered in Creature.Die()

            // If we're not destroying the card, add it to the discard pile
            if (!destroyCard && Deck is Deck deck && creature is ICreature) {
                // When a creature is removed from battlefield without being destroyed, add it to discard pile
                deck.AddToDiscardPile(creature);
                Log($"Added creature {creature.Name} to discard pile after removing from battlefield", LogTag.Cards | LogTag.Creatures);
            }

            slot.ClearSlot(destroyCard);
            Log($"Removed creature {creature.Name} from battlefield", LogTag.Creatures);
            gameMediator?.NotifyBattlefieldStateChanged(this);
            gameMediator?.NotifyGameStateChanged();
        }
    }

    public override bool IsValidTarget() => true;

    public override string ToString() {
        return $"{Name} - Health: {Health}, Hand: {Hand.Count}, Battlefield: {Battlefield.Count()}";
    }
}