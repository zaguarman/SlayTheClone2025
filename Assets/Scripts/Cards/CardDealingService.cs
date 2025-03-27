using System.Collections.Generic;
using static DebugLogger;

public interface ICardDealingService {
    void InitializeDecks(List<CardData> player1Cards, List<CardData> player2Cards);
    void DealInitialHands(IPlayer player1, IPlayer player2, int handSize = 6);
    bool CanDrawCard(IPlayer player);
    void DrawCardForPlayer(IPlayer player);
    void ShuffleDeck(IPlayer player);
    List<ICard> GetDeckPreview(IPlayer player);
    List<ICard> GetDiscardPilePreview(IPlayer player);
    int GetDiscardPileCount(IPlayer player);
}

public class CardDealingService : ICardDealingService {
    private readonly Dictionary<IPlayer, IDeck> playerDecks = new Dictionary<IPlayer, IDeck>();
    private readonly GameMediator gameMediator;

    public CardDealingService(GameMediator gameMediator) {
        this.gameMediator = gameMediator;
    }

    public void InitializeDecks(List<CardData> player1Cards, List<CardData> player2Cards) {
        Log($"Initializing decks - Player1 cards: {player1Cards.Count}, Player2 cards: {player2Cards.Count}", LogTag.Cards);

        var gameManager = GameManager.Instance;
        if (gameManager == null) {
            LogError("GameManager not found when initializing decks", LogTag.Cards | LogTag.Initialization);
            return;
        }

        // Create and initialize deck for Player 1
        var player1Deck = gameManager.Player1.Deck as Deck;
        player1Deck.Initialize(player1Cards);
        playerDecks[gameManager.Player1] = player1Deck;

        // Create and initialize deck for Player 2
        var player2Deck = gameManager.Player2.Deck as Deck;
        player2Deck.Initialize(player2Cards);
        playerDecks[gameManager.Player2] = player2Deck;

        Log("Card dealing service initialized decks successfully", LogTag.Cards | LogTag.Initialization);
    }

    public void DealInitialHands(IPlayer player1, IPlayer player2, int handSize = 7) {
        for (int i = 0; i < handSize; i++) {
            // Check hand size limits before drawing
            if (player1.Hand.Count < Player.MAX_HAND_SIZE) {
                DrawCardForPlayer(player1);
            }

            if (player2.Hand.Count < Player.MAX_HAND_SIZE) {
                DrawCardForPlayer(player2);
            }
        }
        Log($"Dealt initial hands to both players", LogTag.Cards | LogTag.Initialization);
    }

    public bool CanDrawCard(IPlayer player) {
        if (player == null || !playerDecks.ContainsKey(player)) {
            LogWarning($"Cannot check draw capability - player not found in deck registry", LogTag.Cards);
            return false;
        }

        var deck = playerDecks[player];
        // Check both deck and hand size
        return deck != null && deck.CardsRemaining > 0 && player.Hand.Count < Player.MAX_HAND_SIZE;
    }

    public void DrawCardForPlayer(IPlayer player) {
        if (player == null) {
            LogError("Cannot draw card - player is null", LogTag.Cards | LogTag.Initialization);
            return;
        }

        if (player.Hand.Count >= Player.MAX_HAND_SIZE) {
            Log($"Player {(player.IsPlayer1() ? "1" : "2")} has a full hand ({Player.MAX_HAND_SIZE} cards), skipping draw", LogTag.Cards);
            return;
        }

        if (!playerDecks.TryGetValue(player, out var deck)) {
            LogError($"Could not find deck for player", LogTag.Cards | LogTag.Initialization);
            return;
        }

        var card = deck.DrawCard();
        if (card != null) {
            player.AddToHand(card);
            gameMediator.NotifyHandStateChanged(player);
            Log($"Drew card for {(player.IsPlayer1() ? "Player 1" : "Player 2")}: {card.Name}", LogTag.Cards);
        }
    }

    public void ShuffleDeck(IPlayer player) {
        if (!playerDecks.TryGetValue(player, out var deck)) {
            LogError($"Could not find deck for player to shuffle", LogTag.Cards);
            return;
        }

        deck.Shuffle();
        Log($"Shuffled deck for {(player.IsPlayer1() ? "Player 1" : "Player 2")}", LogTag.Cards);
    }

    // Get a preview of the player's deck cards
    public List<ICard> GetDeckPreview(IPlayer player) {
        if (player == null) {
            LogError("Cannot get deck preview - player is null", LogTag.Cards);
            return new List<ICard>();
        }

        if (!playerDecks.TryGetValue(player, out var deck)) {
            LogError($"Could not find deck for player to preview", LogTag.Cards);
            return new List<ICard>();
        }

        // Get a copy of the cards in the deck
        return deck.GetDeckPreview();
    }

    // Get a preview of the player's discard pile
    public List<ICard> GetDiscardPilePreview(IPlayer player) {
        if (player == null) {
            LogError("Cannot get discard pile preview - player is null", LogTag.Cards);
            return new List<ICard>();
        }

        if (!playerDecks.TryGetValue(player, out var deck)) {
            LogError($"Could not find deck for player to get discard pile", LogTag.Cards);
            return new List<ICard>();
        }

        return deck.GetDiscardPilePreview();
    }

    // Get the number of cards in a player's discard pile
    public int GetDiscardPileCount(IPlayer player) {
        if (player == null || !playerDecks.TryGetValue(player, out var deck)) {
            return 0;
        }

        return deck.GetDiscardPilePreview().Count;
    }
}