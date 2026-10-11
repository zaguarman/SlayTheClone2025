using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlayTheClone {
    /// <summary>
    /// Interface for accessing game references and UI components.
    /// </summary>
    public interface IGameReferences {
        bool IsInitialized { get; }
        bool AreReferencesValid();

        // Player UI references
        GameReferences.PlayerUIReferences Player1References { get; }
        GameReferences.PlayerUIReferences Player2References { get; }
        GameReferences.PlayerUIReferences GetPlayerReferences(IPlayer player);
        PlayerUI GetPlayer1UI();
        PlayerUI GetPlayer2UI();
        HandUI GetPlayer1HandUI();
        HandUI GetPlayer2HandUI();
        BattlefieldUI GetPlayer1BattlefieldUI();
        BattlefieldUI GetPlayer2BattlefieldUI();

        // UI components
        Button GetCardPrefab();
        Button GetResolveActionsButton();
        Color GetCardColor(IPlayer player);
        Button GetWeatherCycleButton();
        TextMeshProUGUI GetWeatherText();
        DeckViewUI GetDeckViewUI();
        Button GetDeckViewButton();
        Button GetDiscardViewButton();
        Tooltip GetTooltip();

        // Deck data
        List<CardData> GetPlayer1DeckCards();
        List<CardData> GetPlayer2DeckCards();
    }
}
