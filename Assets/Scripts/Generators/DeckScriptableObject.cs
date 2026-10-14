using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class DeckScriptableObject : ScriptableObject {
        [SerializeField] private string deckName = "New Deck";
        [SerializeField] private List<CardDataScriptableObject> cards = new List<CardDataScriptableObject>();
        [SerializeField] private string lastUpdated = ""; // When its cards last changed (UTC, round-trip format): menus list the newest first
        [SerializeField] private int focus; // Being worked on now: 1 the most important, then 2...; 0 not (Cards > Generate Faction Decks sets it)

        /// <summary>Its place among the decks being worked on now (1 first), 0 if it isn't one: the menus list those first.</summary>
        public int Focus => focus;

        /// <summary>When its cards last changed; the oldest possible date if it was never recorded.</summary>
        public System.DateTime LastUpdated =>
            System.DateTime.TryParse(lastUpdated, null, System.Globalization.DateTimeStyles.RoundtripKind, out var when) ? when.ToUniversalTime() : System.DateTime.MinValue;

        /// <summary>Its cards changed now (or at this moment, read back from a save).</summary>
        internal void Touch(System.DateTime? when = null) => lastUpdated = (when ?? System.DateTime.UtcNow).ToUniversalTime().ToString("o");

        /// <summary>
        /// Decks as the menus list them: the ones being worked on now first, the most important first (see <see cref="Focus"/>),
        /// then the most recently updated; equally placed ones keep their order.
        /// </summary>
        public static List<DeckScriptableObject> InMenuOrder(IEnumerable<DeckScriptableObject> decks) =>
            decks.Where(d => d != null).OrderBy(d => d.Focus > 0 ? d.Focus : int.MaxValue).ThenByDescending(d => d.LastUpdated).ToList();

        // Convert the scriptable object cards to runtime CardData objects
        public List<CardData> GetCardDataList() {
            List<CardData> cardDataList = new List<CardData>();

            foreach (var card in cards) {
                if (card != null) {
                    var cardData = card.ToCardData();
                    // Ensure the card ID is transferred correctly
                    if (string.IsNullOrEmpty(cardData.cardId) && !string.IsNullOrEmpty(card.cardId)) {
                        Log($"Card '{card.cardName}' had missing cardId in ToCardData(), assigning ID: {card.cardId}", LogTag.Cards);
                        cardData.cardId = card.cardId;
                    }

                    cardDataList.Add(cardData);
                    Log($"Added card '{card.cardName}' with ID {cardData.cardId} to runtime deck", LogTag.Cards | LogTag.Initialization);
                }
                else {
                    LogWarning($"Null card reference found in deck '{deckName}'", LogTag.Cards);
                }
            }

            Log($"Converted {cardDataList.Count} cards from deck '{deckName}'", LogTag.Cards | LogTag.Initialization);
            return cardDataList;
        }

        public string GetDeckName() {
            return deckName;
        }

        public IReadOnlyList<CardDataScriptableObject> Cards => cards;

        /// <summary>A deck made while the game runs, never saved as an asset: the player's own (see <see cref="PlayerDecks"/>).</summary>
        public static DeckScriptableObject Make(string name, IEnumerable<CardDataScriptableObject> cards) {
            var deck = CreateInstance<DeckScriptableObject>();
            deck.hideFlags = HideFlags.DontUnloadUnusedAsset; // Loading the game scene doesn't unload it
            deck.SetName(name);
            deck.SetCards(cards);
            deck.Touch();
            return deck;
        }

        // Only PlayerDecks changes a deck: changing an asset's in play mode would change the asset itself
        internal void SetName(string name) => deckName = this.name = name;
        internal void SetCards(IEnumerable<CardDataScriptableObject> list) => cards = list.Where(c => c != null).ToList();
    }
}
