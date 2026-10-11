using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// The scene's references (see <see cref="IGameReferences"/>). Found and initialized by GameBootstrap.
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

        [Header("Decks (used when the main menu didn't choose a match)")]
        [SerializeField] private DeckScriptableObject player1Deck;
        [SerializeField] private DeckScriptableObject player2Deck;

        private bool referencesValidated = false;
        #endregion

        public PlayerUIReferences Player1References => player1References;
        public PlayerUIReferences Player2References => player2References;
        public Color Player1CardColor => player1CardColor;
        public Color Player2CardColor => player2CardColor;

        #region Unity Lifecycle
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
            isValid &= IsAssigned(Player1Deck, "Player 1 deck reference missing!");
            isValid &= IsAssigned(Player2Deck, "Player 2 deck reference missing!");

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
        public Button GetCardPrefab() => cardPrefab;
        public Button GetResolveActionsButton() => resolveActionsButton;
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

        // The match chosen in the main menu wins over the scene's own decks
        public DeckScriptableObject Player1Deck => MatchSetup.IsChosen ? MatchSetup.PlayerDeck : player1Deck;
        public DeckScriptableObject Player2Deck => MatchSetup.IsChosen ? MatchSetup.OpponentDeck : player2Deck;
        public string Player2Name => MatchSetup.IsChosen ? MatchSetup.OpponentName : MatchSetup.DefaultOpponentName;

        public List<CardData> GetPlayer1DeckCards() => GetDeckCards(Player1Deck, "Player1Deck");
        public List<CardData> GetPlayer2DeckCards() => GetDeckCards(Player2Deck, "Player2Deck");

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
