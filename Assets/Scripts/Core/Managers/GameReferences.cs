using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
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

        [Header("Decks (used when the main menu didn't choose a match)")]
        [SerializeField] private DeckScriptableObject player1Deck;
        [SerializeField] private DeckScriptableObject player2Deck;

        private bool referencesValidated = false;
        #endregion

        public PlayerUIReferences Player1References => player1References;
        public PlayerUIReferences Player2References => player2References;

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

        // The match chosen in the main menu wins over the scene's own decks; played straight from the game scene (the
        // editor's Play), two different faction decks at random each time (the user: not always Corporate vs Statecraft),
        // never the Mechanics Test decks. The scene's own decks are the fallback
        public DeckScriptableObject Player1Deck => MatchSetup.IsChosen ? MatchSetup.PlayerDeck : RandomDecks().first ?? player1Deck;
        public DeckScriptableObject Player2Deck => MatchSetup.IsChosen ? MatchSetup.OpponentDeck : RandomDecks().second ?? player2Deck;

        private static (DeckScriptableObject first, DeckScriptableObject second)? randomDecks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetRandomDecks() => randomDecks = null;

        private static (DeckScriptableObject first, DeckScriptableObject second) RandomDecks() {
            if (randomDecks.HasValue) return randomDecks.Value;
            DeckScriptableObject first = null, second = null;
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:DeckCatalog");
            var catalog = guids.Length > 0 ? UnityEditor.AssetDatabase.LoadAssetAtPath<DeckCatalog>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0])) : null;
            var decks = catalog != null ? new List<DeckScriptableObject>(catalog.Decks) : new List<DeckScriptableObject>();
            if (decks.Count >= 2) {
                var random = new System.Random();
                first = decks[random.Next(decks.Count)];
                decks.Remove(first);
                second = decks[random.Next(decks.Count)];
                Log($"Random match: {first.GetDeckName()} vs {second.GetDeckName()}", LogTag.Initialization);
            }
#endif
            randomDecks = (first, second);
            return randomDecks.Value;
        }
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
