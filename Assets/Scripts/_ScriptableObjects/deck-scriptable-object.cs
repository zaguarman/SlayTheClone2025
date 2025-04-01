using UnityEngine;
using System.Collections.Generic;
using static DebugLogger;

[CreateAssetMenu(fileName = "NewDeck", menuName = "Cards/Deck")]
public class DeckScriptableObject : ScriptableObject
{
    [SerializeField] private string deckName = "New Deck";
    [SerializeField] private List<CardDataScriptableObject> cards = new List<CardDataScriptableObject>();

    // Convert the scriptable object cards to runtime CardData objects
    public List<CardData> GetCardDataList()
    {
        List<CardData> cardDataList = new List<CardData>();
        
        foreach (var card in cards)
        {
            if (card != null)
            {
                cardDataList.Add(card.ToCardData());
            }
            else
            {
                LogWarning($"Null card reference found in deck '{deckName}'", LogTag.Cards);
            }
        }

        Log($"Converted {cardDataList.Count} cards from deck '{deckName}'", LogTag.Cards | LogTag.Initialization);
        return cardDataList;
    }

    public string GetDeckName()
    {
        return deckName;
    }
}
