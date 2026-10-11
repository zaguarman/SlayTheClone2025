using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// Provides access to game references and UI components.
    /// Found and initialized by GameBootstrap.
    /// </summary>
    public class GameReferences : MonoBehaviour, IGameReferences {
        public bool IsInitialized { get; private set; }

        [System.Serializable]
        public class PlayerUIReferences {
            [Header("Main UI")]
            [SerializeField] private PlayerUI playerUI;
            [SerializeField] private TextMeshProUGUI healthText;

            [Header("Card Containers")]
            [SerializeField] private HandUI handUI;
            [SerializeField] private BattlefieldUI battlefieldUI;

            public PlayerUI PlayerUI => playerUI;
            public TextMeshProUGUI HealthText => healthText;
            public HandUI HandUI => handUI;
            public BattlefieldUI BattlefieldUI => battlefieldUI;

            public bool ValidateReferences(string playerName) {
                bool isValid = true;
                isValid &= IsAssigned(playerUI, $"{playerName} PlayerUI reference missing!");
                isValid &= IsAssigned(healthText, $"{playerName} HealthText reference missing!");
                isValid &= IsAssigned(handUI, $"{playerName} HandUI reference missing!");
                isValid &= IsAssigned(battlefieldUI, $"{playerName} BattlefieldUI reference missing!");
                return isValid;
            }
        }

        #region Fields
        [Header("Game Control")]
        [SerializeField] private Button resolveActionsButton;

        [Header("Weather Control")]
        [SerializeField] private Button weatherCycleButton;
        [SerializeField] private TextMeshProUGUI weatherText;

        [Header("Deck View")]
        [SerializeField] private DeckViewUI deckViewUI;
        [SerializeField] private Button deckViewButton;
        [SerializeField] private Button discardViewButton;
        [SerializeField] private TextMeshProUGUI deckViewTitleText;

        [Header("Tooltip")]
        [SerializeField] private Tooltip tooltip;

        [Header("Player References")]
        [FormerlySerializedAs("_player1References")]
        [SerializeField] private PlayerUIReferences player1References;
        [FormerlySerializedAs("_player2References")]
        [SerializeField] private PlayerUIReferences player2References;

        [Header("Card Components")]
        [SerializeField] private Button cardPrefab;

        [Header("Card Style")]
        [SerializeField] private Color player1CardColor = new Color(0.8f, 0.9f, 1f);
        [SerializeField] private Color player2CardColor = new Color(1f, 0.8f, 0.8f);

        [Header("Decks")]
        [SerializeField] private DeckScriptableObject player1Deck;
        [SerializeField] private DeckScriptableObject player2Deck;

        private bool referencesValidated = false;
        #endregion

        public PlayerUIReferences Player1References => player1References;
        public PlayerUIReferences Player2References => player2References;

        #region Unity Lifecycle
        private void Start() {
            if (GetComponent<DeckViewController>() == null) {
                Log("Adding DeckViewController to GameReferences", LogTag.Initialization);
                gameObject.AddComponent<DeckViewController>();
            }

            if (tooltip == null) {
                Log("CardTooltip reference missing, creating one", LogTag.Initialization);
                tooltip = Tooltip.Create(transform);
            }
        }

        private void OnEnable() {
            ValidateReferences();
        }
        #endregion

        #region Initialization
        public void Initialize() {
            if (IsInitialized) return;
            ValidateReferences();
            IsInitialized = true;

            Log("GameReferences initialized", LogTag.Initialization);
        }
        #endregion

        #region Validation
        private void ValidateReferences() {
            if (referencesValidated) return;

            bool isValid = true;
            isValid &= IsAssigned(resolveActionsButton, "ResolveActionsButton reference missing!");
            isValid &= IsAssigned(weatherCycleButton, "WeatherCycleButton reference missing!");
            isValid &= IsAssigned(weatherText, "WeatherText reference missing!");
            isValid &= IsAssigned(cardPrefab, "CardPrefab reference missing!");
            isValid &= IsAssigned(deckViewUI, "DeckViewUI reference missing in GameReferences!");
            isValid &= IsAssigned(deckViewButton, "DeckViewButton reference missing in GameReferences!");
            isValid &= IsAssigned(discardViewButton, "DiscardViewButton reference missing in GameReferences!");
            isValid &= IsAssigned(player1Deck, "Player 1 deck reference missing!");
            isValid &= IsAssigned(player2Deck, "Player 2 deck reference missing!");

            // Optional: created on demand
            if (tooltip == null) {
                Log("CardTooltip reference missing, will create one when needed", LogTag.Initialization);
            }

            isValid &= player1References.ValidateReferences("Player 1");
            isValid &= player2References.ValidateReferences("Player 2");

            referencesValidated = isValid;

            if (isValid) {
                Log("All references validated successfully in GameReferences", LogTag.Initialization);
            }
            else {
                LogWarning("Some references are missing in GameReferences", LogTag.Initialization);
            }
        }

        private static bool IsAssigned(Object reference, string missingMessage) {
            if (reference != null) return true;
            Log(missingMessage, LogTag.Initialization);
            return false;
        }

        public bool AreReferencesValid() {
            ValidateReferences();
            return referencesValidated;
        }
        #endregion

        #region Methods
        public PlayerUIReferences GetPlayerReferences(IPlayer player) => player.IsPlayer1 ? player1References : player2References;

        public PlayerUI GetPlayer1UI() => player1References.PlayerUI;
        public PlayerUI GetPlayer2UI() => player2References.PlayerUI;
        public HandUI GetPlayer1HandUI() => player1References.HandUI;
        public HandUI GetPlayer2HandUI() => player2References.HandUI;
        public BattlefieldUI GetPlayer1BattlefieldUI() => player1References.BattlefieldUI;
        public BattlefieldUI GetPlayer2BattlefieldUI() => player2References.BattlefieldUI;
        public Button GetCardPrefab() => cardPrefab;
        public Button GetResolveActionsButton() => resolveActionsButton;
        public Color GetCardColor(IPlayer player) => player.IsPlayer1 ? player1CardColor : player2CardColor;
        public Button GetWeatherCycleButton() => weatherCycleButton;
        public TextMeshProUGUI GetWeatherText() => weatherText;
        public DeckViewUI GetDeckViewUI() => WarnIfMissing(deckViewUI, nameof(GetDeckViewUI));
        public Button GetDeckViewButton() => WarnIfMissing(deckViewButton, nameof(GetDeckViewButton));
        public Button GetDiscardViewButton() => WarnIfMissing(discardViewButton, nameof(GetDiscardViewButton));

        private static T WarnIfMissing<T>(T reference, string getterName) where T : Object {
            if (reference == null) {
                LogWarning($"Reference is null when {getterName} was called", LogTag.UI);
            }
            return reference;
        }

        // Created on demand, and again if the cached tooltip was destroyed
        public Tooltip GetTooltip() {
            if (this == null) return null; // GameReferences itself was destroyed

            if (tooltip == null) {
                LogWarning("CardTooltip is missing or was destroyed when GetTooltip was called, creating one", LogTag.UI);
                tooltip = Tooltip.Create(transform);
            }
            return tooltip;
        }

        public List<CardData> GetPlayer1DeckCards() => GetDeckCards(player1Deck, "Player1Deck");
        public List<CardData> GetPlayer2DeckCards() => GetDeckCards(player2Deck, "Player2Deck");

        private static List<CardData> GetDeckCards(DeckScriptableObject deck, string deckName) {
            if (deck == null) {
                LogError($"{deckName} is null when its cards were requested", LogTag.Cards);
                return new List<CardData>();
            }
            return deck.GetCardDataList();
        }
        #endregion
    }
}
