using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using static DebugLogger;

public class DeckViewUI : UIComponent {
    [SerializeField] public GameObject deckViewPanel;
    [SerializeField] public Transform cardListContent;
    [SerializeField] public Button closeButton;
    [SerializeField] public TextMeshProUGUI deckCountText;
    [SerializeField] public TextMeshProUGUI discardPileCountText;
    [SerializeField] public Button viewDeckButton;
    [SerializeField] public Button viewDiscardButton;
    [SerializeField] public int columnsCount = 3; // Number of columns to display
    [SerializeField] public float cardScale = 0.5f; // Scale of cards in the deck view

    private List<CardController> cardEntries = new List<CardController>();
    private GridLayoutGroup gridLayout;
    private bool viewingDiscardPile = false;

    protected override void Awake() {
        base.Awake();

        Log("DeckViewUI Awake called", LogTag.UI | LogTag.Initialization);

        // Verify essential components
        if (deckViewPanel == null) {
            LogError("DeckViewPanel is not assigned in the inspector", LogTag.UI | LogTag.Initialization);
        }

        if (cardListContent == null) {
            LogError("CardListContent is not assigned in the inspector", LogTag.UI | LogTag.Initialization);
        }

        if (closeButton == null) {
            LogError("CloseButton is not assigned in the inspector", LogTag.UI | LogTag.Initialization);
        } else {
            closeButton.onClick.AddListener(HideDeckView);
            Log("Close button listener attached in Awake", LogTag.UI | LogTag.Initialization);
        }

        if (deckViewPanel != null) {
            deckViewPanel.SetActive(false);
            Log("DeckViewPanel set to inactive on start", LogTag.UI | LogTag.Initialization);
        }

        // Set up toggle buttons if they exist
        if (viewDeckButton != null) {
            viewDeckButton.onClick.AddListener(ShowDeckCards);
        }

        if (viewDiscardButton != null) {
            viewDiscardButton.onClick.AddListener(ShowDiscardPileCards);
        }

        // Setup grid layout for cards
        SetupCardGrid();
    }

    private void SetupCardGrid() {
        if (cardListContent == null) {
            LogError("Cannot setup card grid - cardListContent is null", LogTag.UI | LogTag.Initialization);
            return;
        }

        // Remove any existing layout components
        var existingLayouts = cardListContent.GetComponents<LayoutGroup>();
        foreach (var layout in existingLayouts) {
            DestroyImmediate(layout);
        }

        // Add grid layout
        gridLayout = cardListContent.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(120, 160); // Default card size
        gridLayout.spacing = new Vector2(10, 10);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = columnsCount;

        // Make sure we have a content size fitter
        ContentSizeFitter fitter = cardListContent.GetComponent<ContentSizeFitter>();
        if (fitter == null) {
            fitter = cardListContent.gameObject.AddComponent<ContentSizeFitter>();
        }
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Log("Card grid setup complete", LogTag.UI | LogTag.Initialization);
    }

    public override void Initialize(IPlayer player = null) {
        base.Initialize(player);

        Log("DeckViewUI Initialize called", LogTag.UI | LogTag.Initialization);

        if (IsInitialized) {
            Log("DeckViewUI already initialized, skipping", LogTag.UI | LogTag.Initialization);
            return;
        }

        // Validate all required references
        if (closeButton == null || deckViewPanel == null || cardListContent == null) {
            LogError("Missing references for DeckViewUI", LogTag.UI | LogTag.Initialization);
            return;
        }

        // Ensure the close button works
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(HideDeckView);
        Log("Close button listener attached", LogTag.UI | LogTag.Initialization);

        // Set up view toggle buttons
        if (viewDeckButton != null) {
            viewDeckButton.onClick.RemoveAllListeners();
            viewDeckButton.onClick.AddListener(ShowDeckCards);
        }

        if (viewDiscardButton != null) {
            viewDiscardButton.onClick.RemoveAllListeners();
            viewDiscardButton.onClick.AddListener(ShowDiscardPileCards);
        }

        IsInitialized = true;
        Log("DeckViewUI initialized", LogTag.UI | LogTag.Initialization);
    }

    protected override void RegisterEvents() {
        if (gameMediator != null) {
            // Register for deck changes if needed
            Log("DeckViewUI events registered", LogTag.UI | LogTag.Initialization);
        }
    }

    protected override void UnregisterEvents() {
        if (gameMediator != null) {
            // Unregister events if needed
            Log("DeckViewUI events unregistered", LogTag.UI);
        }
    }

    public override void UpdateUI(IPlayer player = null) {
        if (!IsInitialized) {
            LogWarning("Cannot update UI - DeckViewUI not initialized", LogTag.UI);
            return;
        }

        IPlayer targetPlayer = player ?? Player;
        if (targetPlayer == null) {
            LogError("Cannot update UI - No valid player reference", LogTag.UI);
            return;
        }

        if (viewingDiscardPile) {
            UpdateDiscardPileDisplay(targetPlayer);
        } else {
            UpdateDeckDisplay(targetPlayer);
        }
    }

    public void ShowDeckView() {
        Log("ShowDeckView called", LogTag.UI);

        if (deckViewPanel == null) {
            LogError("Cannot show deck view - deckViewPanel is null", LogTag.UI);
            return;
        }

        if (!IsInitialized) {
            LogWarning("DeckViewUI not initialized yet, attempting to initialize now", LogTag.UI);
            Initialize(Player);
        }

        // Default to showing the deck, not the discard pile
        viewingDiscardPile = false;
        UpdateUI(Player);
        deckViewPanel.SetActive(true);
        Log($"Showing deck view for {(Player?.IsPlayer1() == true ? "Player 1" : "Player 2")}", LogTag.UI);
    }

    public void HideDeckView() {
        Log("HideDeckView called", LogTag.UI);

        if (deckViewPanel != null) {
            deckViewPanel.SetActive(false);
            Log("Hiding deck view", LogTag.UI);
        } else {
            LogError("Cannot hide deck view - deckViewPanel is null", LogTag.UI);
        }
    }

    public void ShowDeckCards() {
        if (viewingDiscardPile) {
            viewingDiscardPile = false;
            UpdateUI(Player);
            Log("Switched to viewing deck", LogTag.UI);
        }
    }

    public void ShowDiscardPileCards() {
        if (!viewingDiscardPile) {
            viewingDiscardPile = true;
            UpdateUI(Player);
            Log("Switched to viewing discard pile", LogTag.UI);
        }
    }

    public void UpdateDeckDisplay(IPlayer player) {
        ClearCardEntries();

        if (player == null || gameManager == null) {
            LogError("Cannot update deck display - references missing", LogTag.UI);
            return;
        }

        var deckCards = GetPlayerDeckCards(player);

        if (deckCountText != null) {
            deckCountText.text = $"Cards in deck: {deckCards.Count}";
        }

        if (discardPileCountText != null) {
            // Display discard pile count even when viewing deck
            var discardCount = gameManager.cardDealingService.GetDiscardPileCount(player);
            discardPileCountText.text = $"Discard pile: {discardCount}";
        }

        foreach (var card in deckCards) {
            CreateCardEntry(card, player);
        }

        Log($"Updated deck display with {deckCards.Count} cards", LogTag.UI | LogTag.Cards);
    }

    public void UpdateDiscardPileDisplay(IPlayer player) {
        ClearCardEntries();

        if (player == null || gameManager == null) {
            LogError("Cannot update discard pile display - references missing", LogTag.UI);
            return;
        }

        // Get the cards from the player's discard pile
        var discardPileCards = GetPlayerDiscardPileCards(player);

        if (deckCountText != null) {
            // Keep showing deck count even when viewing discard pile
            var deckCount = gameManager.cardDealingService.GetDeckPreview(player).Count;
            deckCountText.text = $"Cards in deck: {deckCount}";
        }

        if (discardPileCountText != null) {
            discardPileCountText.text = $"Discard pile: {discardPileCards.Count}";
        }

        foreach (var card in discardPileCards) {
            CreateCardEntry(card, player);
        }

        Log($"Updated discard pile display with {discardPileCards.Count} cards", LogTag.UI | LogTag.Cards);
    }

    private List<ICard> GetPlayerDeckCards(IPlayer player) {
        // Get the cards from the player's deck directly
        if (player != null && player.Deck != null) {
            // Return a copy of the deck so we don't modify the actual deck
            var deckCards = new List<ICard>();

            // Get cards from the player's deck
            if (gameManager?.cardDealingService != null) {
                // Since we can't access the internal cards list directly from IDeck
                // We need to peek at them through the CardDealingService
                var cardDealingService = gameManager.cardDealingService;
                deckCards = cardDealingService.GetDeckPreview(player);

                Log($"Retrieved {deckCards.Count} cards for player deck preview", LogTag.Cards);
                return deckCards;
            }

            // Fallback to demo cards if we can't access the real deck 
            // (This will be removed once the CardDealingService is updated)
            var testSetup = gameObject.AddComponent<TestSetup>();
            var testCards = testSetup.CreateTestCards();
            Destroy(testSetup);

            // Convert CardData to ICard
            foreach (var cardData in testCards) {
                var card = CardFactory.CreateCard(cardData);
                if (card != null) {
                    deckCards.Add(card);
                }
            }

            Log($"Retrieved {deckCards.Count} fallback test cards for player deck", LogTag.Cards);
            return deckCards;
        }

        LogError("Cannot get deck cards - Player or Deck not available", LogTag.Cards | LogTag.UI);
        return new List<ICard>();
    }

    private List<ICard> GetPlayerDiscardPileCards(IPlayer player) {
        if (player != null && player.Deck != null) {
            if (gameManager?.cardDealingService != null) {
                var cardDealingService = gameManager.cardDealingService;
                var discardCards = cardDealingService.GetDiscardPilePreview(player);

                Log($"Retrieved {discardCards.Count} cards for player discard pile preview", LogTag.Cards);
                return discardCards;
            }
        }

        LogError("Cannot get discard pile cards - Player or Deck not available", LogTag.Cards | LogTag.UI);
        return new List<ICard>();
    }

    private void CreateCardEntry(ICard card, IPlayer owner) {
        if (card == null || cardListContent == null) {
            LogError("Cannot create card entry - card or content is null", LogTag.Cards);
            return;
        }

        // Use CardFactory to create the card controller with the existing prefab
        var cardController = CardFactory.CreateCardController(card, owner, cardListContent);

        if (cardController != null) {
            // Disable dragging and other interactive features for the preview
            DisableCardInteractions(cardController);

            // Scale the card
            cardController.transform.localScale = new Vector3(cardScale, cardScale, cardScale);

            // Add to our list for tracking
            cardEntries.Add(cardController);

            Log($"Created card entry for {card.Name}", LogTag.Cards);
        } else {
            LogError($"Failed to create card controller for {card.Name}", LogTag.Cards);
        }
    }

    private void DisableCardInteractions(CardController cardController) {
        if (cardController == null) return;

        // Disable any dragging components
        var components = cardController.GetComponents<MonoBehaviour>();
        foreach (var component in components) {
            string typeName = component.GetType().Name;
            if (typeName.Contains("DragHandler") ||
                typeName.Contains("DropHandler") ||
                typeName.Contains("PointerHandler")) {
                component.enabled = false;
            }
        }
    }

    private void ClearCardEntries() {
        foreach (var entry in cardEntries) {
            if (entry != null) {
                Destroy(entry.gameObject);
            }
        }

        cardEntries.Clear();
    }

    protected override void OnDestroy() {
        if (closeButton != null) {
            closeButton.onClick.RemoveListener(HideDeckView);
        }

        if (viewDeckButton != null) {
            viewDeckButton.onClick.RemoveListener(ShowDeckCards);
        }

        if (viewDiscardButton != null) {
            viewDiscardButton.onClick.RemoveListener(ShowDiscardPileCards);
        }

        ClearCardEntries();
        base.OnDestroy();
    }
}