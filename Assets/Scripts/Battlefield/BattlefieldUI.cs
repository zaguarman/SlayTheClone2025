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
    private void InitializeManagers(IPlayer player) {
        // Create ArrowManager using injected dependencies from UIComponent base class
        // Get ActionsQueue from the injected gameManager
        var actionsQueue = gameManager?.ActionsQueue;
        if (actionsQueue == null) {
            LogError("Cannot initialize BattlefieldArrowManager - ActionsQueue is null via gameManager", LogTag.Initialization);
            return;
        }
        arrowManager = new BattlefieldArrowManager(transform, gameMediator, gameReferences, actionsQueue);
    }

    private void CreateSlots() {
        for (int i = 0; i < MAX_SLOTS; i++) {
            GameObject slotObj = new GameObject($"Slot_{i}", typeof(RectTransform));
            slotObj.transform.SetParent(transform, false);

            var slot = slotObj.AddComponent<BattlefieldSlot>();
            slot.Initialize(defaultColor, validDropColor, invalidDropColor, hoverColor);

            // Pass the GameReferences to the slot
            if (gameReferences != null) {
                slot.SetGameReferences(gameReferences);
            }

            BattlefieldSlotsList.Add(slot);
        }
        UpdateSlotPositions();
    }

    public override void Initialize(IPlayer player, IGameMediator mediator, IGameReferences references, IGameManager manager) {
        // Call base CardContainer Initialize FIRST
        base.Initialize(player, mediator, references, manager);

        // Now initialize Battlefield specific things
        InitializeManagers(player); // Pass player
        CreateSlots();
        Log($"BattlefieldUI initialized for player (TargetID: {player.TargetId.ToUpper()})", LogTag.Initialization);

        player.InitializeBattlefield(BattlefieldSlotsList);
        Log($"Player Battlefield initialized for player (TargetID: {player.TargetId.ToUpper()})", LogTag.Initialization);

        // Update UI based on initial state
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
    protected override void HandleCardDropped(CardController card) {
        if (card == null || !CanAcceptCard(card)) return;
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
        var cardData = card.GetBaseCardData();
        if (cardData != null) {
            var newCard = CardFactory.CreateCard(cardData);
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
            if (card.IsPlayer1Card() != Player.IsPlayer1) {
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
                Destroy(card.gameObject);
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
            gameMediator.AddCreatureSummonedListener(OnCreatureSummoned);
            gameMediator.AddBattlefieldStateChangedListener(UpdateUI);
            gameMediator.AddCreatureDiedListener(OnCreatureDied);
        }
    }

    protected override void UnregisterEvents() {
        if (gameMediator != null) {
            gameMediator.RemoveCreatureSummonedListener(OnCreatureSummoned);
            gameMediator.RemoveCreatureDiedListener(OnCreatureDied);
        }
    }

    protected override void SetupCardEventHandlers(CardController controller) {
        controller.OnBeginDragEvent.AddListener(OnCardBeginDrag);
        controller.OnEndDragEvent.AddListener(OnCardEndDrag);
        controller.OnCardDropped.AddListener(OnCardDropped);
    }

    private void OnCreatureSummoned(ICreature creature, IPlayer player) {
        if (!IsInitialized || player != Player) return;
        UpdateUI(Player);
    }

    private void OnCreatureDied(ICreature creature) {
        if (!IsInitialized) return;
        var slot = GetSlot(creature);

        if (slot != null) {
            slot.ClearSlot();
            UpdateUI(creature.Owner);
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
        UpdateUI(Player);
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
        var slot = BattlefieldSlotsList.FirstOrDefault(s => s.OccupyingCreature == creature) ??
            GetOpponentBattlefield().BattlefieldSlotsList.FirstOrDefault(s => s.OccupyingCreature == creature);

        return slot;
    }

    public BattlefieldSlot GetSlot(CardController card) {
        return BattlefieldSlotsList.FirstOrDefault(s => s.OccupyingCard == card);
    }

    private BattlefieldUI GetOpponentBattlefield() {
        var player1 = Player.IsPlayer1;
        if (player1) {
            return gameReferences.GetPlayer2BattlefieldUI();
        } else {
            return gameReferences.GetPlayer1BattlefieldUI();
        }
    }

    protected override void OnCardDropped(CardController card) {
        Log($"Card dropped from Battlefield: {card.GetBaseCardData()?.cardName} (TargetID: {card.GetBaseCardData()?.cardId.ToUpper()})", LogTag.UI | LogTag.Cards);
        UpdateLayout();
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