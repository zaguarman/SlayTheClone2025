using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using static DebugLogger;

public class BattlefieldUI : CardContainer {
    private const int MAX_SLOTS = 5;
    private readonly List<BattlefieldSlot> BattlefieldSlotsList = new List<BattlefieldSlot>();

    private BattlefieldArrowManager arrowManager;

    #region Initialization
    private void InitializeManagers() {
        arrowManager = new BattlefieldArrowManager(transform, gameManager, gameMediator);
    }

    private void CreateSlots() {
        for (int i = 0; i < MAX_SLOTS; i++) {
            GameObject slotObj = new GameObject($"Slot_{i}", typeof(RectTransform));
            slotObj.transform.SetParent(transform, false);

            var slot = slotObj.AddComponent<BattlefieldSlot>();
            slot.Initialize(defaultColor, validDropColor, invalidDropColor, hoverColor);
            BattlefieldSlotsList.Add(slot);
        }
        UpdateSlotPositions();
    }

    public override void Initialize(IPlayer player) {
        base.Initialize(player);

        InitializeManagers();
        CreateSlots();
        Log($"BattlefieldUI initialized for player (TargetID: {player.TargetId.ToUpper()})", LogTag.Initialization);

        player.InitializeBattlefield(BattlefieldSlotsList);
        Log($"Player Battlefield initialized for player (TargetID: {player.TargetId.ToUpper()})", LogTag.Initialization);

        UpdateUI(Player);
    }

    private void UpdateSlotPositions() {
        float totalWidth = (MAX_SLOTS - 1) * settings.spacing;
        float startX = -totalWidth / 2;

        for (int i = 0; i < BattlefieldSlotsList.Count; i++) {
            float xPos = startX + (settings.spacing * i);
            BattlefieldSlotsList[i].SetPosition(new Vector2(xPos, 0));
        }
    }
    #endregion

    #region Card Handling
    // Override CreateCard to use the instance factory
    protected override CardController CreateCard(ICard cardData) {
        return gameManager?.CardFactory?.CreateCardController(cardData, Player, transform);
    }

    protected override void HandleCardDropped(CardController card) {
        if (gameManager == null) return;

        var targetSlot = GetTargetSlot();
        if (targetSlot == null) return;

        if (IsCardFromHand(card)) {
            HandleCardFromHand(card, targetSlot);
        } else {
            HandleCardFromBattlefield(card, targetSlot);
        }

        gameMediator?.NotifyGameStateChanged();
    }

    private bool IsCardFromHand(CardController card) {
        if (card.OriginalParent == null) return false;

        Transform parent = card.OriginalParent;
        while (parent != null) {
            if (parent.GetComponent<HandUI>() != null) return true;
            parent = parent.parent;
        }
        return false;
    }

    private void HandleCardFromHand(CardController card, ITarget target) {
        var cardData = card.GetCardData();
        if (cardData != null && gameManager?.CardFactory != null) {
            var newCard = gameManager.CardFactory.CreateCard(cardData);
            if (newCard != null) {
                // Determine if the target is valid based on card type
                ITarget validTarget = target;

                // For creatures, we need a slot
                if (cardData is CreatureData) {
                    Log($"Adding PlayCardAction for creature {cardData.cardName} (TargetID: {cardData.cardId.ToUpper()}) to slot (TargetID: {target.TargetId.ToUpper()})",
                        LogTag.Actions | LogTag.Cards);
                }
                // For spells, we might need a different target (creature in slot or player)
                else if (cardData is SpellData) {
                    // If the slot is occupied, target the creature
                    if (target is BattlefieldSlot slot && slot.IsOccupied()) {
                        validTarget = slot.OccupyingCreature;
                        Log($"Adding PlayCardAction for spell {cardData.cardName} (TargetID: {cardData.cardId.ToUpper()}) targeting creature (TargetID: {validTarget.TargetId.ToUpper()})",
                            LogTag.Actions | LogTag.Cards);
                    }
                    // Otherwise target the opponent player
                    else {
                        validTarget = Player.Opponent;
                        Log($"Adding PlayCardAction for spell {cardData.cardName} (TargetID: {cardData.cardId.ToUpper()}) targeting player (TargetID: {validTarget.TargetId.ToUpper()})",
                            LogTag.Actions | LogTag.Cards);
                    }
                }

                // Find the card in the player's hand that matches the card data
                ICard cardToPlay = null;
                foreach (var handCard in Player.Hand) {
                    if (handCard.Name == cardData.cardName) {
                        cardToPlay = handCard;
                        break;
                    }
                }

                // If we found the card, use it; otherwise use the newly created one
                if (cardToPlay != null) {
                    gameManager.ActionsQueue.AddAction(new PlayCardAction(cardToPlay, Player, validTarget));
                } else {
                    gameManager.ActionsQueue.AddAction(new PlayCardAction(newCard, Player, validTarget));
                }
            }
        }
    }

    private void HandleCardFromBattlefield(CardController card, ITarget target) {
        if (target != null) {
            if (card.IsPlayer1Card() != Player.IsPlayer1()) {
                gameManager.CombatHandler.HandleCreatureCombat(card, target);
            } else {
                HandleCreatureMove(card, target);
            }
        }
    }

    private void HandleCreatureMove(CardController card, ITarget targetSlot) {
        var creature = card.GetLinkedCreature();
        if (creature == null) return;

        var sourceSlot = BattlefieldSlotsList.FirstOrDefault(s => s.OccupyingCard == card);
        if (sourceSlot != null) {
            var moveAction = new MoveCreatureAction(creature, (ITarget)sourceSlot, targetSlot, Player);
            gameManager.ActionsQueue.AddAction(moveAction);
        }
    }

    private ITarget GetTargetSlot() {
        var pointerEventData = new PointerEventData(EventSystem.current) {
            position = Input.mousePosition
        };

        var raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, raycastResults);

        foreach (var result in raycastResults) {
            var slot = result.gameObject.GetComponent<BattlefieldSlot>();
            if (slot != null) return (ITarget)slot;
        }

        return null;
    }
    #endregion

    #region UI Updates
    public override void UpdateUI(IPlayer player) {
        if (!IsInitialized || Player == null) return;
        if (player != Player) return;

        // Clear orphaned cards
        foreach (var card in cards.ToList()) {
            bool existsInSlot = player.Battlefield.Any(s => s.OccupyingCard == card);
            if (!existsInSlot) {
                RemoveCard(card);
                if (card != null) Destroy(card.gameObject); // Check for null before destroy
            }
        }

        // Add and position cards from slots
        foreach (var slot in player.Battlefield) {
            if (slot.OccupyingCard != null) {
                // Add card to the list if not already present
                if (!cards.Contains(slot.OccupyingCard)) {
                    AddCard(slot.OccupyingCard);
                }
                // Reposition the card to the slot's position
                PositionCardInSlot(slot.OccupyingCard, slot);
            }
        }

        // --- Explicitly update the UI of cards already in slots ---
        foreach (var slot in BattlefieldSlotsList) { // Use the internal list
            if (slot.OccupyingCard != null) {
                slot.OccupyingCard.UpdateUI(); // Tell the card controller to refresh its display
            }
        }
        // --- End added section ---

        UpdateLayout();
    }

    private void PositionCardInSlot(CardController card, BattlefieldSlot slot) {
        if (card == null) return;

        // Parent to slot and reset position
        card.transform.SetParent(slot.transform, false);
        card.transform.localPosition = Vector3.zero;
        card.transform.localRotation = Quaternion.identity;
    }

    // Check if it is necessary
    private void UpdateCreatureCards() {
        foreach (var battlefieldSlot in Player.Battlefield) {
            var creature = battlefieldSlot.OccupyingCreature;
            if (creature == null) continue;

            var creatureCard = battlefieldSlot.OccupyingCard;

            if (creatureCard == null) {
                battlefieldSlot.OccupySlot(creatureCard);
                creatureCard.UpdateUI();
            }
        }
    }

    protected override void UpdateLayout() {
        // Intentionally left empty to prevent CardContainer from repositioning cards
        // Slots handle card positioning instead
    }
    #endregion

    #region Event Handling
    protected override void RegisterEvents() {
        if (gameMediator != null) {
            // Note: Base CardContainer now handles Setup/Cleanup calls via Add/RemoveCard
            gameMediator.AddCreatureSummonedListener(OnCreatureSummoned);
            gameMediator.AddBattlefieldStateChangedListener(UpdateUI);
            gameMediator.AddCreatureDiedListener(OnCreatureDied);
            // --- ADD THIS LISTENER ---
            gameMediator.AddGameStateChangedListener(OnGameStateChanged);
        }
    }

    protected override void UnregisterEvents() {
        if (gameMediator != null) {
            // Note: Base CardContainer now handles Setup/Cleanup calls via Add/RemoveCard
            gameMediator.RemoveCreatureSummonedListener(OnCreatureSummoned);
            // BattlefieldStateChanged listener is already handled by UpdateUI in base Initialize
            gameMediator.RemoveCreatureDiedListener(OnCreatureDied);
            // --- REMOVE THIS LISTENER ---
            gameMediator.RemoveGameStateChangedListener(OnGameStateChanged);
        }
    }

    // --- ADD THIS METHOD ---
    private void OnGameStateChanged() {
        // We need to update the UI for the player associated with this battlefield
        // Check IsInitialized and Player validity
        if (IsInitialized && Player != null) {
            UpdateUI(Player);
        }
    }
    // --- END ADDED METHOD ---



    private void OnCreatureSummoned(ICreature creature, IPlayer player) {
        if (!IsInitialized || player != Player) return;
        UpdateUI(Player);
    }

    private void OnCreatureDied(ICreature creature) {
        if (!IsInitialized || creature.Owner != Player) return; // Only handle if the creature belonged to this player
        var slot = GetSlot(creature);

        if (slot != null) {
            // ClearSlot now handles destroying the card object if requested (default: true)
            slot.ClearSlot(true);
            UpdateUI(Player); // Refresh UI after removal
        }
    }
    #endregion

    #region Drag Handling
    protected override void OnCardBeginDrag(CardController card) {
        if (card == null) return;

        var startSlot = BattlefieldSlotsList.FirstOrDefault(s => s.OccupyingCard == card);
        if (startSlot != null) {
            arrowManager.ShowDragArrow(startSlot.transform.position);
        } else {
            arrowManager.ShowDragArrow(card.transform.position);
        }
        card.transform.SetAsLastSibling();
    }

    public void OnCardDrag(PointerEventData eventData) {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(eventData.position);
        arrowManager.UpdateDragArrow(worldPos);
    }

    protected override void OnCardEndDrag(CardController card) {
        arrowManager.HideDragArrow();
        // No need to call UpdateUI here, OnGameStateChanged will handle it if a move/attack happened
        // If the card is just dropped back, it should snap back (handled by CardController or Container)
        // Let's ensure layout updates if nothing else handles it.
        UpdateLayout();
    }
    #endregion

    public CardController GetCardController(ICreature creature) {
        foreach (var slot in BattlefieldSlotsList) {
            if (slot.OccupyingCreature == creature) {
                return slot.OccupyingCard;
            }
        }

        return null;
    }

    public BattlefieldSlot GetSlot(ICreature creature) {
        // Check own battlefield first
        var slot = BattlefieldSlotsList.FirstOrDefault(s => s.OccupyingCreature == creature);
        if (slot != null) return slot;

        // If not found, check opponent's battlefield (needed for targeting)
        var opponentBattlefield = GetOpponentBattlefield();
        if (opponentBattlefield != null)
        {
            slot = opponentBattlefield.BattlefieldSlotsList.FirstOrDefault(s => s.OccupyingCreature == creature);
            if (slot != null) return slot;
        }

        // Check if the creature has a direct reference to its slot
        if (creature?.Slot != null) {
            return creature.Slot;
        }


        return null; // Return null if not found anywhere
    }

    public BattlefieldSlot GetSlot(CardController card) {
        return BattlefieldSlotsList.FirstOrDefault(s => s.OccupyingCard == card);
    }

    private BattlefieldUI GetOpponentBattlefield() {
        if (gameReferences == null || Player == null) return null;
        var player1 = Player.IsPlayer1();
        return player1 ? gameReferences.GetPlayer2BattlefieldUI() : gameReferences.GetPlayer1BattlefieldUI();
    }

    protected override void OnCardDropped(CardController card) {
        Log($"Card dropped from Battlefield: {card.GetCardData()?.cardName} (TargetID: {card.GetCardData()?.cardId.ToUpper()})", LogTag.UI | LogTag.Cards);
        // Dropping a card onto the battlefield is handled by HandleCardFromBattlefield
        // UpdateLayout(); // No need to call UpdateLayout, state changes will trigger it
    }

    #region Cleanup
    protected override void OnDestroy() {
        base.OnDestroy();
        foreach (var slot in BattlefieldSlotsList) {
            if (slot != null) {
                var cardController = slot.OccupyingCard;

                if (cardController != null) {
                    Destroy(cardController.gameObject);
                }

                Destroy(slot.gameObject);
            }
        }
        BattlefieldSlotsList.Clear();

        if (arrowManager != null) {
            arrowManager.Cleanup();
        }
    }
    #endregion

    protected override void OnCardHoverEnter(CardController card) { }
    protected override void OnCardHoverExit(CardController card) { }
}