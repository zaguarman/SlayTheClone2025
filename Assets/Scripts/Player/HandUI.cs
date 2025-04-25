using System.Linq;
using UnityEngine;
using static DebugLogger;

public class HandUI : CardContainer {
    public override void Initialize(IPlayer player, IGameMediator mediator, IGameReferences references, IGameManager manager) {
        // Call base CardContainer Initialize FIRST
        base.Initialize(player, mediator, references, manager);
    }

    protected override void RegisterEvents() {
        if (gameMediator != null) {
            gameMediator.AddHandStateChangedListener(UpdateUI);
            gameMediator.AddGameInitializedListener(OnGameInitialized);
        }
    }

    protected override void UnregisterEvents() {
        if (gameMediator != null) {
            gameMediator.RemoveHandStateChangedListener(UpdateUI);
            gameMediator.RemoveGameInitializedListener(OnGameInitialized);
        }
    }

    private void OnGameInitialized() {
        UpdateUI(Player);
    }

    public override void UpdateUI(IPlayer player) {
        if (!IsInitialized || Player == null) return;

        if (player != Player) return;

        // Clear all current cards
        foreach (var card in cards.ToList()) {
            RemoveCard(card);
            if (card != null) {
                Destroy(card.gameObject);
            }
        }
        cards.Clear();

        // Create new card controllers for each card in hand
        foreach (var cardData in player.Hand) {
            // Get the ICard instance from the player's hand
            ICard handCard = cardData; // cardData is actually an ICard instance here

            // Find the original CardData using the player's deck
            CardData originalData = player.Deck?.FindOriginalCardDataById(handCard.CardId);

            if (originalData == null) {
                LogWarning($"HandUI: Could not find original CardData for {handCard.Name} (ID: {handCard.CardId}) in Player {player.Name}'s deck. Display might be inaccurate.", LogTag.UI | LogTag.Cards);
                // As a fallback, you might create temporary data, but ideally, this shouldn't happen for cards dealt from a deck.
                // For now, we'll skip creating a controller if original data is missing to highlight the issue.
                continue;
            }

            // Use CardFactory to create the controller, passing the live ICard instance and the original CardData
            var controller = CardFactory.CreateCardController(handCard, originalData, Player, transform, gameMediator, gameReferences, gameManager);
            if (controller != null) {
                AddCard(controller);
            }
        }

        UpdateLayout();
    }

    protected override void SetupCardEventHandlers(CardController controller) {
        controller.OnBeginDragEvent.AddListener(OnCardBeginDrag);
        controller.OnEndDragEvent.AddListener(OnCardEndDrag);
        controller.OnCardDropped.AddListener(OnCardDropped);
    }

    protected override void OnCardBeginDrag(CardController card) {
        if (card == null) return;
        card.transform.SetAsLastSibling();
        Log($"Begin dragging card from hand: {card.GetBaseCardData()?.cardName} (TargetID: {card.GetBaseCardData()?.cardId.ToUpper()})", LogTag.UI | LogTag.Cards);
    }

    protected override void OnCardEndDrag(CardController card) {
        Log($"End dragging card from hand: {card.GetBaseCardData()?.cardName} (TargetID: {card.GetBaseCardData()?.cardId.ToUpper()})", LogTag.UI | LogTag.Cards);
        UpdateLayout();
    }

    protected override void OnCardDropped(CardController card) {
        Log($"Card dropped from hand: {card.GetBaseCardData()?.cardName} (TargetID: {card.GetBaseCardData()?.cardId.ToUpper()})", LogTag.UI | LogTag.Cards);

        // If the card was successfully dropped onto a valid target,
        // it will be removed from the hand by the PlayCardAction
        // We'll check if it's still in the player's hand after a short delay
        StartCoroutine(CheckCardAfterDrop(card));

        UpdateLayout();
    }

    private System.Collections.IEnumerator CheckCardAfterDrop(CardController card) {
        // Wait a short time for other handlers to process
        yield return new WaitForSeconds(0.1f);

        // If the card controller is null or destroyed, we don't need to do anything
        if (card == null) yield break;

        // Find the matching card in the player's hand by name
        var cardData = card.GetBaseCardData();
        if (cardData == null) yield break;

        // If the card is still in the hand, it wasn't handled by another component
        // Just update layout again to ensure proper positioning
        UpdateLayout();
    }

    protected override void OnCardHoverEnter(CardController card) { }
    protected override void OnCardHoverExit(CardController card) { }
}