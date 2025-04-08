using UnityEngine;
using System.Collections.Generic;
using static DebugLogger;

[CreateAssetMenu(fileName = "NewDeck", menuName = "Cards/Deck")]
public class DeckScriptableObject : ScriptableObject {
    [SerializeField] private string deckName = "New Deck";
    [SerializeField] private List<CardDataScriptableObject> cards = new List<CardDataScriptableObject>();

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
            } else {
                LogWarning($"Null card reference found in deck '{deckName}'", LogTag.Cards);
            }
        }

        Log($"Converted {cardDataList.Count} cards from deck '{deckName}'", LogTag.Cards | LogTag.Initialization);
        return cardDataList;
    }

    public string GetDeckName() {
        return deckName;
    }
}