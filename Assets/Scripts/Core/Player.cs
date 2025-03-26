using UnityEngine.Events;
using System;
using System.Collections.Generic;
using static DebugLogger;
using System.Linq;
using UnityEngine;

[Serializable]
public class PlayerDamagedUnityEvent : UnityEvent<int> { }

public interface IPlayer : IEntity, IDamageable {
    bool IsPlayer1();
    IPlayer Opponent { get; set; }
    List<ICard> Hand { get; }
    List<BattlefieldSlot> Battlefield { get; }
    IDeck Deck { get; }  // Added this property
    void AddToHand(ICard card);
    void AddToBattlefield(ICard creature, ITarget slotId = null);
    void RemoveFromBattlefield(ICard creature, bool destroyCard = true);
    PlayerDamagedUnityEvent OnDamaged { get; }
    void InitializeBattlefield(List<BattlefieldSlot> battlefieldSlots);
    void DrawCard();
}

public class Player : Entity, IPlayer {
    public int Health { get; private set; } = 20;
    public IPlayer Opponent { get; set; }
    public List<ICard> Hand { get; private set; }
    public IDeck Deck { get; private set; }  // Added implementation

    public List<BattlefieldSlot> Battlefield { get; private set; }
    public PlayerDamagedUnityEvent OnDamaged { get; } = new PlayerDamagedUnityEvent();

    // is player 1 prop using the isplayer1 method
    public bool Is_Player1 => IsPlayer1();

    private readonly GameMediator gameMediator;

    public Player(string name = "Player") : base(name) {
        Hand = new List<ICard>();
        Battlefield = new List<BattlefieldSlot>();
        Deck = new Deck();  // Initialize deck
        gameMediator = GameMediator.Instance;
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

    public bool IsPlayer1() {
        var gameManager = GameManager.Instance;
        return gameManager != null && gameManager.Player1 == this;
    }

    public void TakeDamage(int amount) {
        Health = Math.Max(0, Health - amount);
        OnDamaged.Invoke(amount);
    }

    public void AddToHand(ICard card) {
        if (card == null) return;
        Hand.Add(card);
        gameMediator?.NotifyHandStateChanged(this);
    }

    public void DrawCard() {
        var cardDealingService = GameManager.Instance?.cardDealingService;
        if (cardDealingService == null) {
            LogError("Cannot draw card - card dealing service not available", LogTag.Cards);
            return;
        }

        if (cardDealingService.CanDrawCard(this)) {
            cardDealingService.DrawCardForPlayer(this);
        } else {
            LogWarning($"No cards left in {(IsPlayer1() ? "Player 1" : "Player 2")}'s deck", LogTag.Cards);
        }
    }

    public void AddToBattlefield(ICard card, ITarget slot = null) {
        if (card == null) return;

        var targetSlot = Battlefield.FirstOrDefault(s => s.TargetId == slot.TargetId);
        if (targetSlot == null) return;

        // Clear existing card if needed
        if (targetSlot.IsOccupied()) {
            var oldCreature = targetSlot.OccupyingCreature;
            RemoveFromBattlefield(oldCreature);
        }

        // Create new card controller
        var cardController = CardFactory.CreateCardController(card, this, targetSlot.transform);
        if (cardController != null) {
            targetSlot.AssignCreature(cardController);
            gameMediator?.NotifyBattlefieldStateChanged(this);
        }
    }

    public void RemoveFromBattlefield(ICard creature, bool destroyCard = true) {
        if (creature == null) return;

        var slot = Battlefield.FirstOrDefault(s => s.OccupyingCreature == creature as ICreature);
        if (slot != null) {
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