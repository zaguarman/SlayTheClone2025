using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public interface IDeck {
        int CardsRemaining { get; }
        void Initialize(List<CardData> cardDataList);
        ICard DrawCard();
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
        private string deckId;

        public int CardsRemaining => cards?.Count ?? 0;

        public Deck() {
            cards = new List<ICard>();
            discardPile = new List<ICard>();
            deckId = System.Guid.NewGuid().ToString();
        }

        public void Initialize(List<CardData> cardDataList) {
            if (cardDataList == null || cardDataList.Count == 0) {
                LogError($"Attempted to initialize deck with null or empty card list (DeckID: {deckId.ToUpper()})",
                    LogTag.Cards | LogTag.Initialization);
                return;
            }

            cards.Clear();
            discardPile.Clear();
            foreach (var cardData in cardDataList) {
                var card = CardFactory.CreateCard(cardData);
                if (card != null) {
                    Log($"Added {card.Name} (CardID: {card.TargetId.ToUpper()}) to deck (DeckID: {deckId.ToUpper()}) with {card.Effects.Count} effects",
                        LogTag.Cards | LogTag.Initialization);
                    cards.Add(card);
                }
            }

            Shuffle();
            Log($"Initialized deck (DeckID: {deckId.ToUpper()}) with {cards.Count} cards",
                LogTag.Cards | LogTag.Initialization);
        }

        public ICard DrawCard() {
            if (cards.Count == 0) {
                LogWarning($"Attempted to draw from empty deck (DeckID: {deckId.ToUpper()})", LogTag.Cards);
                return null;
            }

            var drawnCard = cards[0];
            cards.RemoveAt(0);
            Log($"Drew card: {drawnCard.Name} (CardID: {drawnCard.TargetId.ToUpper()}) from deck (DeckID: {deckId.ToUpper()})", LogTag.Cards);
            return drawnCard;
        }

        public void AddCardToBottom(ICard card) {
            if (card == null) {
                LogError($"Attempted to add null card to deck (DeckID: {deckId.ToUpper()})", LogTag.Cards);
                return;
            }

            cards.Add(card);
            Log($"Added card to bottom: {card.Name} (CardID: {card.TargetId.ToUpper()}) to deck (DeckID: {deckId.ToUpper()})", LogTag.Cards);
        }

        // Every card that leaves play (played spell, dead or replaced creature, discarded hand) ends up here.
        // It is stored as a fresh copy of its definition, so damage, buffs and death state never carry over.
        // Only Persistent Stat Modifications stay on the card, until it's reshuffled (see CardDealingService.RecycleDiscardPile).
        public void AddToDiscardPile(ICard card) {
            if (card == null) {
                LogError($"Attempted to add null card to discard pile (DeckID: {deckId.ToUpper()})", LogTag.Cards);
                return;
            }

            var resetCard = CreateFreshCopy(card);
            if (card is ICreature used && resetCard is ICreature copy && copy != used) {
                var (attack, health, speed) = used.PersistentChanges;
                copy.ChangeBaseStats(attack, health, speed);
            }
            discardPile.Add(resetCard);
            Log($"Added {resetCard.Name} to discard pile (DeckID: {deckId.ToUpper()})", LogTag.Cards);
        }

        private ICard CreateFreshCopy(ICard card) {
            if (card.SourceData == null) {
                LogError($"{card.Name} has no card definition; discarding the used instance as-is. Cards must be made by CardFactory.CreateCard.", LogTag.Cards);
                return card;
            }
            return CardFactory.CreateCard(card.SourceData);
        }

        public void ClearDiscardPile() {
            int count = discardPile.Count;
            discardPile.Clear();
            Log($"Cleared discard pile ({count} cards) for deck (DeckID: {deckId.ToUpper()})", LogTag.Cards);
        }

        public void Shuffle() {
            if (cards.Count <= 1) return;

            // Fisher-Yates shuffle algorithm
            for (int i = cards.Count - 1; i > 0; i--) {
                int j = GameRandom.Next(i + 1);
                var temp = cards[i];
                cards[i] = cards[j];
                cards[j] = temp;
            }

            Log($"Shuffled deck ({cards.Count} cards) (DeckID: {deckId.ToUpper()})", LogTag.Cards);
        }

        public bool RemoveCard(ICard card) {
            if (card == null) {
                LogError($"Attempted to remove null card from deck (DeckID: {deckId.ToUpper()})", LogTag.Cards);
                return false;
            }

            // The instance itself first: copies of the same card share a CardId
            if (cards.Remove(card)) {
                Log($"Removed card {card.Name} (CardID: {card.CardId.ToUpper()}) from deck (DeckID: {deckId.ToUpper()})", LogTag.Cards);
                return true;
            }

            // Then by CardId (more reliable than name)
            var cardToRemove = cards.FirstOrDefault(c => c.CardId == card.CardId);

            if (cardToRemove != null) {
                bool removed = cards.Remove(cardToRemove);
                if (removed) {
                    Log($"Removed card {card.Name} (CardID: {card.CardId.ToUpper()}) from deck (DeckID: {deckId.ToUpper()}) by ID", LogTag.Cards);
                }
                return removed;
            }

            // If we couldn't find by CardId, fall back to finding by name
            cardToRemove = cards.FirstOrDefault(c => c.Name == card.Name);
            if (cardToRemove != null) {
                bool removed = cards.Remove(cardToRemove);
                if (removed) {
                    Log($"Removed card {card.Name} (CardID: {card.CardId.ToUpper()}) from deck (DeckID: {deckId.ToUpper()}) by name match", LogTag.Cards);
                }
                return removed;
            }

            LogWarning($"Could not find card {card.Name} (CardID: {card.CardId.ToUpper()}) in deck (DeckID: {deckId.ToUpper()}) to remove", LogTag.Cards);
            return false;
        }

        // Get a copy of the deck for preview purposes
        public List<ICard> GetDeckPreview() {
            // Create a copy of cards to avoid exposing the internal collection
            var previewCards = new List<ICard>(cards);
            Log($"Created deck preview with {previewCards.Count} cards for deck (DeckID: {deckId.ToUpper()})", LogTag.Cards);
            return previewCards;
        }

        // Get a copy of the discard pile for preview purposes
        public List<ICard> GetDiscardPilePreview() {
            // Create a copy of discard pile to avoid exposing the internal collection
            var previewCards = new List<ICard>(discardPile);
            Log($"Created discard pile preview with {previewCards.Count} cards for deck (DeckID: {deckId.ToUpper()})", LogTag.Cards);
            return previewCards;
        }
    }
}
