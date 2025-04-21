using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interface for accessing game references and UI components.
/// </summary>
public interface IGameReferences
{
    // Initialization state
    bool IsInitialized { get; }
    bool AreReferencesValid();

    // Player UI References
    PlayerUI GetPlayer1UI();
    PlayerUI GetPlayer2UI();
    HandUI GetPlayer1HandUI();
    HandUI GetPlayer2HandUI();
    BattlefieldUI GetPlayer1BattlefieldUI();
    BattlefieldUI GetPlayer2BattlefieldUI();

    // UI Components
    Button GetCardPrefab();
    Button GetResolveActionsButton();
    Color GetPlayer1CardColor();
    Color GetPlayer2CardColor();
    Button GetWeatherCycleButton();
    TextMeshProUGUI GetWeatherText();
    DeckViewUI GetDeckViewUI();
    Button GetDeckViewButton();
    Button GetDiscardViewButton();
    Tooltip GetTooltip();

    // Deck Data
    List<CardData> GetPlayer1DeckCards();
    List<CardData> GetPlayer2DeckCards();

    // Expose PlayerUIReferences for direct access
    GameReferences.PlayerUIReferences player1References { get; }
    GameReferences.PlayerUIReferences player2References { get; }
}
