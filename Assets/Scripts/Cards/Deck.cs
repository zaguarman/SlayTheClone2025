using System.Collections.Generic;
using static DebugLogger;

public interface IDeck {
    int CardsRemaining { get; }
    void Initialize(List<CardData> cardDataList);
    ICard DrawCard();
    void AddCardToTop(ICard card);
    void AddCardToBottom(ICard card);
    void AddToDiscardPile(ICard card);
    void ClearDiscardPile();
    void Shuffle();
    List<ICard> GetDeckPreview();
    List<ICard> GetDiscardPilePreview();
}

public class Deck : IDeck {
    private List<ICard> cards;
    private List<ICard> discardPile;
    private System.Random random;

    public int CardsRemaining => cards?.Count ?? 0;
    public int DiscardPileCount => discardPile?.Count ?? 0;

    public Deck() {
        cards = new List<ICard>();
        discardPile = new List<ICard>();
        random = new System.Random();
    }

    public void Initialize(List<CardData> cardDataList) {
        if (cardDataList == null || cardDataList.Count == 0) {
            LogError("Attempted to initialize deck with null or empty card list", LogTag.Cards | LogTag.Initialization);
            return;
        }

        cards.Clear();
        discardPile.Clear();
        foreach (var cardData in cardDataList) {
            var card = CardFactory.CreateCard(cardData);
            if (card != null) {
                Log($"Added {card.Name} to deck with {card.Effects.Count} effects", LogTag.Cards | LogTag.Initialization);
                cards.Add(card);
            }
        }

        Shuffle();
        Log($"Initialized with {cards.Count} cards", LogTag.Cards | LogTag.Initialization);
    }

    public ICard DrawCard() {
        if (cards.Count == 0) {
            LogWarning("Attempted to draw from empty deck", LogTag.Cards);
            return null;
        }

        var drawnCard = cards[0];
        cards.RemoveAt(0);
        Log($"Drew card: {drawnCard.Name}", LogTag.Cards);
        return drawnCard;
    }

    public void AddCardToTop(ICard card) {
        if (card == null) {
            LogError("Attempted to add null card to deck", LogTag.Cards);
            return;
        }

        cards.Insert(0, card);
        Log($"Added card to top: {card.Name}", LogTag.Cards);
    }

    public void AddCardToBottom(ICard card) {
        if (card == null) {
            LogError("Attempted to add null card to deck", LogTag.Cards);
            return;
        }

        cards.Add(card);
        Log($"Added card to bottom: {card.Name}", LogTag.Cards);
    }

    public void AddToDiscardPile(ICard card) {
        if (card == null) {
            LogError("Attempted to add null card to discard pile", LogTag.Cards);
            return;
        }

        discardPile.Add(card);
        Log($"Added card to discard pile: {card.Name}", LogTag.Cards);
    }

    public void ClearDiscardPile() {
        int count = discardPile.Count;
        discardPile.Clear();
        Log($"Cleared discard pile ({count} cards)", LogTag.Cards);
    }

    public void Shuffle() {
        if (cards.Count <= 1) return;

        // Fisher-Yates shuffle algorithm
        for (int i = cards.Count - 1; i > 0; i--) {
            int j = random.Next(0, i + 1);
            var temp = cards[i];
            cards[i] = cards[j];
            cards[j] = temp;
        }

        Log($"Shuffled deck ({cards.Count} cards)", LogTag.Cards);
    }

    // Get a copy of the deck for preview purposes
    public List<ICard> GetDeckPreview() {
        // Create a copy of cards to avoid exposing the internal collection
        var previewCards = new List<ICard>(cards);
        Log($"Created deck preview with {previewCards.Count} cards", LogTag.Cards);
        return previewCards;
    }

    // Get a copy of the discard pile for preview purposes
    public List<ICard> GetDiscardPilePreview() {
        // Create a copy of discard pile to avoid exposing the internal collection
        var previewCards = new List<ICard>(discardPile);
        Log($"Created discard pile preview with {previewCards.Count} cards", LogTag.Cards);
        return previewCards;
    }
}