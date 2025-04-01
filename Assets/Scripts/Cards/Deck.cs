using System.Collections.Generic;
using System.Linq;
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
    bool RemoveCard(ICard card);
}

public class Deck : IDeck {
    private List<ICard> cards;
    private List<ICard> discardPile;
    private System.Random random;
    private List<CardData> originalCardDataList;

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

        // Store the original card data for restoration purposes
        originalCardDataList = new List<CardData>(cardDataList);

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

        // Check if the card is a creature that needs restoration
        if (card is ICreature creature) {
            // Look for the original card data to restore from using cardId
            ICard restoredCard = RestoreToOriginalValues(creature);

            if (restoredCard != null) {
                discardPile.Add(restoredCard);
                Log($"Added restored creature card to discard pile: {restoredCard.Name}", LogTag.Cards);
                return;
            }
        }

        // If it's not a creature or restoration failed, add the original card
        discardPile.Add(card);
        Log($"Added card to discard pile: {card.Name}", LogTag.Cards);
    }

    private ICard RestoreToOriginalValues(ICreature creature) {
        if (creature == null) return null;

        Log($"Restoring creature {creature.Name} to initial values", LogTag.Cards | LogTag.Creatures);

        // Find the original card data by ID instead of name
        CardData originalData = originalCardDataList?.FirstOrDefault(c => c.cardId == creature.CardId);

        if (originalData != null) {
            // Create a fresh card from the original data
            ICard restoredCard = CardFactory.CreateCard(originalData);

            Log($"Successfully restored {creature.Name} to initial values using cardId: {creature.CardId}", LogTag.Cards | LogTag.Creatures);
            return restoredCard;
        }

        // If original data not found by ID, try to find by name as fallback
        originalData = originalCardDataList?.FirstOrDefault(c => c.cardName == creature.Name);

        if (originalData != null) {
            // Create a fresh card from the original data
            ICard restoredCard = CardFactory.CreateCard(originalData);
            Log($"Successfully restored {creature.Name} to initial values by name (fallback)", LogTag.Cards | LogTag.Creatures);
            return restoredCard;
        }

        // If original data not found, create a generic version
        Log($"Could not find original data for {creature.Name} with ID {creature.CardId}, creating generic version", LogTag.Cards | LogTag.Creatures);
        int health = creature.Health <= 0 ? creature.Attack + 1 : creature.Health;
        var newCreature = new Creature(creature.Name, creature.Attack, health, creature.CardId);
        newCreature.Description = creature.Description;

        // Copy effects
        if (creature.Effects != null && creature.Effects.Count > 0) {
            foreach (var effect in creature.Effects) {
                newCreature.Effects.Add(effect);
            }
        }

        return newCreature;
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

    public bool RemoveCard(ICard card) {
        if (card == null) {
            LogError("Attempted to remove null card from deck", LogTag.Cards);
            return false;
        }

        // First try to find by CardId (more reliable than name)
        var cardToRemove = cards.FirstOrDefault(c => c.CardId == card.CardId);

        if (cardToRemove != null) {
            bool removed = cards.Remove(cardToRemove);
            if (removed) {
                Log($"Removed card {card.Name} from deck by ID: {card.CardId}", LogTag.Cards);
            }
            return removed;
        }

        // If we couldn't find by CardId, fall back to finding by name
        cardToRemove = cards.FirstOrDefault(c => c.Name == card.Name);
        if (cardToRemove != null) {
            bool removed = cards.Remove(cardToRemove);
            if (removed) {
                Log($"Removed card {card.Name} from deck (by name match)", LogTag.Cards);
            }
            return removed;
        }

        LogWarning($"Could not find card {card.Name} (ID: {card.CardId}) in deck to remove", LogTag.Cards);
        return false;
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