using System.Collections.Generic;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public interface ICardDealingService {
        void InitializeDecks(IPlayer player1, List<CardData> player1Cards, IPlayer player2, List<CardData> player2Cards);
        void DealInitialHands(IPlayer player1, IPlayer player2, int handSize = 6);
        bool CanDrawCard(IPlayer player);
        void DrawCardForPlayer(IPlayer player);
        bool RecycleDiscardPile(IPlayer player);
        void DrawCards(IPlayer player, int count);
        List<ICard> GetDeckPreview(IPlayer player);
        List<ICard> GetDiscardPilePreview(IPlayer player);
        int GetDiscardPileCount(IPlayer player);
    }

    public class CardDealingService : ICardDealingService {
        private readonly Dictionary<IPlayer, IDeck> playerDecks = new Dictionary<IPlayer, IDeck>();
        private readonly IGameMediator gameMediator;

        public CardDealingService(IGameMediator gameMediator) {
            this.gameMediator = gameMediator;
        }

        public void InitializeDecks(IPlayer player1, List<CardData> player1Cards, IPlayer player2, List<CardData> player2Cards) {
            if (player1 == null || player2 == null) {
                LogError("Cannot initialize decks - player instances are null.", LogTag.Cards | LogTag.Initialization);
                return;
            }
            Log($"Initializing decks - Player1 cards: {player1Cards?.Count ?? 0}, Player2 cards: {player2Cards?.Count ?? 0}", LogTag.Cards);

            if (RegisterDeck(player1, player1Cards) && RegisterDeck(player2, player2Cards)) {
                Log("Card dealing service initialized decks successfully", LogTag.Cards | LogTag.Initialization);
            }
        }

        private bool RegisterDeck(IPlayer player, List<CardData> cards) {
            var deck = player.Deck;
            if (deck == null) {
                LogError($"{player.Name} Deck is null", LogTag.Cards | LogTag.Initialization);
                return false;
            }
            deck.Initialize(cards);
            playerDecks[player] = deck;
            return true;
        }

        public void DealInitialHands(IPlayer player1, IPlayer player2, int handSize = 6) {
            // Alternate draws, as if dealt from the top; DrawCardForPlayer skips players whose hand is full
            for (int i = 0; i < handSize; i++) {
                DrawCardForPlayer(player1);
                DrawCardForPlayer(player2);
            }
            Log("Dealt initial hands to both players", LogTag.Cards | LogTag.Initialization);
        }

        public bool CanDrawCard(IPlayer player) {
            if (player == null || !playerDecks.ContainsKey(player)) {
                LogWarning($"Cannot check draw capability - player not found in deck registry", LogTag.Cards);
                return false;
            }

            var deck = playerDecks[player];

            // If the deck is empty, check if we can recycle
            if (deck.CardsRemaining == 0) {
                // Try to recycle discard pile
                if (GetDiscardPileCount(player) > 0) {
                    Log($"Deck empty, checking if discard pile can be recycled for {player.Name} (TargetID: {player.TargetId.ToUpper()})", LogTag.Cards);
                    return true;
                }
                // No cards left in deck or discard
                return false;
            }

            // Check both deck and hand size
            return deck != null && deck.CardsRemaining > 0 && player.Hand.Count < Player.MaxHandSize;
        }

        public void DrawCardForPlayer(IPlayer player) {
            if (player == null) {
                LogError("Cannot draw card - player is null", LogTag.Cards | LogTag.Initialization);
                return;
            }

            if (player.Hand.Count >= Player.MaxHandSize) {
                Log($"{player.Name} (TargetID: {player.TargetId.ToUpper()}) has a full hand ({Player.MaxHandSize} cards), skipping draw", LogTag.Cards);
                return;
            }

            if (!playerDecks.TryGetValue(player, out var deck)) {
                LogError($"Could not find deck for player (TargetID: {player.TargetId.ToUpper()})", LogTag.Cards | LogTag.Initialization);
                return;
            }

            // Check if deck is empty and needs recycling
            if (deck.CardsRemaining == 0) {
                bool recycled = RecycleDiscardPile(player);
                if (!recycled) {
                    Log($"No cards left in deck or discard pile for {player.Name} (TargetID: {player.TargetId.ToUpper()})", LogTag.Cards);
                    return;
                }
            }

            // Draw the card now that we know there are cards available
            var card = deck.DrawCard();
            if (card != null) {
                player.AddToHand(card); // AddToHand notifies HandStateChanged
                Log($"Drew card for {player.Name} (TargetID: {player.TargetId.ToUpper()}): {card.Name} (TargetID: {card.TargetId.ToUpper()})", LogTag.Cards);
            }
        }

        public void DrawCards(IPlayer player, int count) {
            if (player == null) {
                LogError("Cannot draw cards - player is null", LogTag.Cards);
                return;
            }

            Log($"Drawing {count} cards for {player.Name} (TargetID: {player.TargetId.ToUpper()})", LogTag.Cards);

            int drawnCount = 0;
            for (int i = 0; i < count; i++) {
                if (player.Hand.Count >= Player.MaxHandSize) {
                    Log($"Hand full ({Player.MaxHandSize} cards), stopped drawing after {drawnCount} cards for player (TargetID: {player.TargetId.ToUpper()})", LogTag.Cards);
                    break;
                }

                // Check if we need to recycle before drawing
                if (!playerDecks.TryGetValue(player, out var deck) || deck.CardsRemaining == 0) {
                    if (!RecycleDiscardPile(player)) {
                        Log($"No more cards in deck or discard pile after drawing {drawnCount} cards for player (TargetID: {player.TargetId.ToUpper()})", LogTag.Cards);
                        break;
                    }
                }

                DrawCardForPlayer(player);
                drawnCount++;
            }

            Log($"Drew {drawnCount} out of {count} requested cards for {player.Name} (TargetID: {player.TargetId.ToUpper()})", LogTag.Cards);
        }

        public bool RecycleDiscardPile(IPlayer player) {
            if (!TryGetDeck(player, "recycle discard pile", out var deck)) return false;

            var discardPileCards = GetDiscardPilePreview(player);
            if (discardPileCards.Count == 0) {
                Log($"No cards in discard pile to recycle for {player.Name} (TargetID: {player.TargetId.ToUpper()})", LogTag.Cards);
                return false;
            }

            // Add cards from discard to deck and shuffle using IDeck methods. Reshuffling resets them for good:
            // Persistent Stat Modifications are removed here (GDD glossary)
            foreach (var card in discardPileCards) {
                deck.AddCardToBottom(card.SourceData != null ? CardFactory.CreateCard(card.SourceData) : card);
            }

            // Clear the discard pile using IDeck method
            deck.ClearDiscardPile();

            // Shuffle the deck using IDeck method
            deck.Shuffle();

            Log($"Recycled {discardPileCards.Count} cards from discard pile for {player.Name} (TargetID: {player.TargetId.ToUpper()})", LogTag.Cards);
            return true;
        }

        // Copies of the cards, so callers can't change the deck
        public List<ICard> GetDeckPreview(IPlayer player) {
            return TryGetDeck(player, "preview", out var deck) ? deck.GetDeckPreview() : new List<ICard>();
        }

        public List<ICard> GetDiscardPilePreview(IPlayer player) {
            return TryGetDeck(player, "get discard pile", out var deck) ? deck.GetDiscardPilePreview() : new List<ICard>();
        }

        private bool TryGetDeck(IPlayer player, string purpose, out IDeck deck) {
            deck = null;
            if (player == null) {
                LogError($"Cannot {purpose} - player is null", LogTag.Cards);
                return false;
            }
            if (!playerDecks.TryGetValue(player, out deck)) {
                LogError($"Could not find deck for player (TargetID: {player.TargetId.ToUpper()}) to {purpose}", LogTag.Cards);
                return false;
            }
            return true;
        }

        // Get the number of cards in a player's discard pile
        public int GetDiscardPileCount(IPlayer player) {
            if (player == null || !playerDecks.TryGetValue(player, out var deck)) {
                return 0;
            }

            return deck.GetDiscardPilePreview().Count;
        }
    }
}
