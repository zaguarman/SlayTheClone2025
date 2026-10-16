using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public interface IDeck {
        int CardsRemaining { get; }
        void Initialize(List<CardData> cardDataList);
        ICard DrawCard();
        /// <summary>The top card of this kind (a creature, a spell), or null if the deck has none.</summary>
        ICard DrawCard(System.Func<ICard, bool> which);
        void AddCardToBottom(ICard card);
        void AddToDiscardPile(ICard card);
        void ClearDiscardPile();
        void Shuffle();
        List<ICard> GetDeckPreview();
        List<ICard> GetDiscardPilePreview();
        /// <summary>Takes this card's copy back out of the discard pile (the last one put there), or null if none is there.</summary>
        ICard TakeFromDiscardPile(ICard card);
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

        public ICard DrawCard(System.Func<ICard, bool> which) {
            int index = cards.FindIndex(c => which(c));
            if (index < 0) return null;
            var drawnCard = cards[index];
            cards.RemoveAt(index);
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

            var resetCard = FreshCopy(card);
            discardPile.Add(resetCard);
            Log($"Added {resetCard.Name} to discard pile (DeckID: {deckId.ToUpper()})", LogTag.Cards);
        }

        /// <summary>
        /// A card that left play, as it goes back to a pile or the hand: a fresh copy of its definition, so damage, buffs and
        /// death don't carry over. Its Persistent Stat Modifications and a gained or lost ability stay until it's reshuffled.
        /// </summary>
        public static ICard FreshCopy(ICard card) {
            if (card.SourceData == null) {
                LogError($"{card.Name} has no card definition; using the instance as-is. Cards must be made by CardFactory.CreateCard.", LogTag.Cards);
                return card;
            }
            var copy = CardFactory.CreateCard(card.SourceData);
            if (copy != null) copy.CopyId = card.CopyId; // The same copy: what it spent once per combat stays spent
            if (card is ICreature used && copy is ICreature fresh) {
                var (attack, health, speed) = used.PersistentChanges;
                fresh.ChangeBaseStats(attack, health, speed);
                fresh.Ability = used.Ability;
            }
            return copy;
        }

        public ICard TakeFromDiscardPile(ICard card) {
            if (card == null) return null;
            // The copy itself first: copies of a card share their definition and id, and the one asked for must be the one that
            // leaves (Console Memory Card took one copy while the other left the pile: one card, two places)
            int index = discardPile.LastIndexOf(card);
            if (index < 0) index = discardPile.FindLastIndex(c => (c.SourceData != null && c.SourceData == card.SourceData) || c.CardId == card.CardId);
            if (index < 0) return null;
            var taken = discardPile[index];
            discardPile.RemoveAt(index);
            Log($"Took {taken.Name} back out of the discard pile (DeckID: {deckId.ToUpper()})", LogTag.Cards);
            return taken;
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
