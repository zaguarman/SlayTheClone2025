using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlayTheClone {
    /// <summary>
    /// What the scene holds: UI objects, the card prefab and colors, and the decks.
    /// Only the startup code (GameBootstrap and GameUI) reads it; everything else is handed what it uses.
    /// </summary>
    public interface IGameReferences {
        bool AreReferencesValid();

        // Each player's side of the table
        GameReferences.PlayerUIReferences Player1References { get; }
        GameReferences.PlayerUIReferences Player2References { get; }

        // Cards
        Button GetCardPrefab();
        Color Player1CardColor { get; }
        Color Player2CardColor { get; }

        // Controls
        Button GetResolveActionsButton();
        Button GetWeatherCycleButton();
        TextMeshProUGUI GetWeatherText();
        DeckViewUI GetDeckViewUI();
        Button GetDeckViewButton();
        Button GetDiscardViewButton();

        // Deck data
        List<CardData> GetPlayer1DeckCards();
        List<CardData> GetPlayer2DeckCards();
    }
}
