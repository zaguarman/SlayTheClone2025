using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DebugLogger;

public class GameReferences : Singleton<GameReferences> {
    [System.Serializable]
    private class PlayerUIReferences {
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

    [Header("Game Control")]
    [SerializeField] private Button resolveActionsButton;

    [Header("Weather Control")]
    [SerializeField] private Button weatherCycleButton;
    [SerializeField] private TextMeshProUGUI weatherText;

    [Header("Player References")]
    [SerializeField] private PlayerUIReferences player1References;
    [SerializeField] private PlayerUIReferences player2References;

    [Header("Card Components")]
    [SerializeField] private Button cardPrefab;

    [Header("Card Style")]
    [SerializeField] private Color player1CardColor = new Color(0.8f, 0.9f, 1f);
    [SerializeField] private Color player2CardColor = new Color(1f, 0.8f, 0.8f);

    private bool referencesValidated = false;

    public override void Initialize() {
        if (IsInitialized) return;
        ValidateReferences();
        base.Initialize();
    }

    private void OnEnable() {
        ValidateReferences();
    }

    private void ValidateReferences() {
        if (referencesValidated) return;

        bool isValid = true;

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

        isValid &= player1References.ValidateReferences("Player 1");
        isValid &= player2References.ValidateReferences("Player 2");

        referencesValidated = isValid;
    }

    // Immutable references getters
    public PlayerUI GetPlayer1UI() => player1References.PlayerUI;
    public PlayerUI GetPlayer2UI() => player2References.PlayerUI;
    public HandUI GetPlayer1HandUI() => player1References.HandUI;
    public HandUI GetPlayer2HandUI() => player2References.HandUI;
    public BattlefieldUI GetPlayer1BattlefieldUI() => player1References.BattlefieldUI;
    public BattlefieldUI GetPlayer2BattlefieldUI() => player2References.BattlefieldUI;
    public Button GetCardPrefab() => cardPrefab;
    public Button GetResolveActionsButton() => resolveActionsButton;
    public Color GetPlayer1CardColor() => player1CardColor;
    public Color GetPlayer2CardColor() => player2CardColor;
    public Button GetWeatherCycleButton() => weatherCycleButton;
    public TextMeshProUGUI GetWeatherText() => weatherText;

    public bool AreReferencesValid() {
        ValidateReferences();  // Force revalidation each time
        return referencesValidated;
    }
}