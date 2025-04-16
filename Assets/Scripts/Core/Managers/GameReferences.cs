using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DebugLogger;

public class GameReferences : Singleton<GameReferences> {
    [System.Serializable]
    public class PlayerUIReferences {
        // Player UI references as in original implementation
        [Header("Main UI")]
        [SerializeField] public PlayerUI playerUI;
        [SerializeField] public TextMeshProUGUI healthText;

        [Header("Card Containers")]
        [SerializeField] public HandUI handUI;
        [SerializeField] public BattlefieldUI battlefieldUI;

        public PlayerUI PlayerUI => playerUI;
        public TextMeshProUGUI HealthText => healthText;
        public HandUI HandUI => handUI;
        public BattlefieldUI BattlefieldUI => battlefieldUI;

        public bool ValidateReferences(string playerName) {
            bool isValid = true;
            if (playerUI == null) {
                Log($"{playerName} PlayerUI reference missing!", LogTag.Initialization);
                isValid = false;
            }
            if (healthText == null) {
                Log($"{playerName} HealthText reference missing!", LogTag.Initialization);
                isValid = false;
            }
            if (handUI == null) {
                Log($"{playerName} HandUI reference missing!", LogTag.Initialization);
                isValid = false;
            }
            if (battlefieldUI == null) {
                Log($"{playerName} BattlefieldUI reference missing!", LogTag.Initialization);
                isValid = false;
            }
            return isValid;
        }
    }

    // Original game control serialized fields
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
    [SerializeField] public PlayerUIReferences player1References;
    [SerializeField] public PlayerUIReferences player2References;

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

    #region Unity Lifecycle
    private void Start() {
        // Add the DeckViewController to manage deck view interactions
        var deckViewController = GetComponent<DeckViewController>();
        if (deckViewController == null) {
            Log("Adding DeckViewController to GameReferences", LogTag.Initialization);
            deckViewController = gameObject.AddComponent<DeckViewController>();
        }

        // Ensure we have a tooltip instance
        if (tooltip == null) {
            Log("CardTooltip reference missing, creating one", LogTag.Initialization);
            tooltip = Tooltip.Create(transform);
        }
    }

    #region Initialization
    public override void Initialize() {
        if (IsInitialized) return;
        ValidateReferences();
        base.Initialize();

        Log("GameReferences initialized", LogTag.Initialization);
    }
    #endregion

    private void OnEnable() {
        ValidateReferences();
    }
    #endregion

    #region Validation
    private void ValidateReferences() {
        if (referencesValidated) return;

        bool isValid = true;

        // Validate all the original references
        if (resolveActionsButton == null) {
            Log("ResolveActionsButton reference missing!", LogTag.Initialization);
            isValid = false;
        }

        if (weatherCycleButton == null) {
            Log("WeatherCycleButton reference missing!", LogTag.Initialization);
            isValid = false;
        }

        if (weatherText == null) {
            Log("WeatherText reference missing!", LogTag.Initialization);
            isValid = false;
        }

        if (cardPrefab == null) {
            Log("CardPrefab reference missing!", LogTag.Initialization);
            isValid = false;
        }

        if (deckViewUI == null) {
            Log("DeckViewUI reference missing in GameReferences!", LogTag.Initialization);
            isValid = false;
        }

        if (deckViewButton == null) {
            Log("DeckViewButton reference missing in GameReferences!", LogTag.Initialization);
            isValid = false;
        }

        if (discardViewButton == null) {
            Log("DiscardViewButton reference missing in GameReferences!", LogTag.Initialization);
            isValid = false;
        }

        // Validate deck references
        if (player1Deck == null) {
            Log("Player 1 deck reference missing!", LogTag.Initialization);
            isValid = false;
        }

        if (player2Deck == null) {
            Log("Player 2 deck reference missing!", LogTag.Initialization);
            isValid = false;
        }

        // Validate tooltip reference
        if (tooltip == null) {
            Log("CardTooltip reference missing, will create one when needed", LogTag.Initialization);
        }

        isValid &= player1References.ValidateReferences("Player 1");
        isValid &= player2References.ValidateReferences("Player 2");

        referencesValidated = isValid;

        if (isValid) {
            Log("All references validated successfully in GameReferences", LogTag.Initialization);
        } else {
            LogWarning("Some references are missing in GameReferences", LogTag.Initialization);
        }
    }

    public bool AreReferencesValid() {
        ValidateReferences();  // Force revalidation each time
        return referencesValidated;
    }
    #endregion

    #region Methods
    // All the original getter methods
    public PlayerUI GetPlayer1UI() => player1References.PlayerUI;
    public PlayerUI GetPlayer2UI() => player2References.PlayerUI;
    public HandUI GetPlayer1HandUI() => player1References.HandUI;
    public HandUI GetPlayer2HandUI() => player2References.HandUI;
    public BattlefieldUI GetPlayer1BattlefieldUI() => player1References.BattlefieldUI;
    public BattlefieldUI GetPlayer2BattlefieldUI() => player2References.BattlefieldUI;
    public Button GetCardPrefab() => cardPrefab;
    public Button GetEndTurnButton() => resolveActionsButton;
    public Color GetPlayer1CardColor() => player1CardColor;
    public Color GetPlayer2CardColor() => player2CardColor;
    public Button GetWeatherCycleButton() => weatherCycleButton;
    public TextMeshProUGUI GetWeatherText() => weatherText;
    public DeckViewUI GetDeckViewUI() {
        if (deckViewUI == null) {
            LogWarning("DeckViewUI is null when GetDeckViewUI was called", LogTag.UI);
        }
        return deckViewUI;
    }
    public Button GetDeckViewButton() {
        if (deckViewButton == null) {
            LogWarning("DeckViewButton is null when GetDeckViewButton was called", LogTag.UI);
        }
        return deckViewButton;
    }
    public Button GetDiscardViewButton() {
        if (discardViewButton == null) {
            LogWarning("DiscardViewButton is null when GetDiscardViewButton was called", LogTag.UI);
        }
        return discardViewButton;
    }

    // Method to get the tooltip, creating it if needed
    public Tooltip GetTooltip() {
        if (tooltip == null) {
            LogWarning("CardTooltip is null when GetCardTooltip was called, creating one", LogTag.UI);
            tooltip = Tooltip.Create(transform);
        }
        return tooltip;
    }

    // New methods to get deck data
    public List<CardData> GetPlayer1DeckCards() {
        if (player1Deck == null) {
            LogError("Player1Deck is null when GetPlayer1DeckCards was called", LogTag.Cards);
            return new List<CardData>();
        }
        return player1Deck.GetCardDataList();
    }

    public List<CardData> GetPlayer2DeckCards() {
        if (player2Deck == null) {
            LogError("Player2Deck is null when GetPlayer2DeckCards was called", LogTag.Cards);
            return new List<CardData>();
        }
        return player2Deck.GetCardDataList();
    }

    // Add ModifierFactory Access (assuming it's managed by GameManager)
    public ModifierFactory ModifierFactory => GameManager.Instance?.ModifierFactory;

    // --- Optional: Preload Modifier Data ---
    // You could add a field to hold loaded ModifierData if ModifierFactory doesn't load them itself.
    // [Header("Modifier Assets")]
    // [SerializeField] private List<ModifierData> allModifierDatas;
    // public IEnumerable<ModifierData> GetAllModifierData() => allModifierDatas;
    // Ensure this list is populated in the Inspector.
    // Then ModifierFactory.Initialize would take this list instead of loading from Resources.

    #endregion // End of Methods
}