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

    // Add reference for card tooltip
    [Header("Card Tooltip")]
    [SerializeField] private CardTooltip cardTooltip;

    [Header("Player References")]
    [SerializeField] public PlayerUIReferences player1References;
    [SerializeField] public PlayerUIReferences player2References;

    [Header("Card Components")]
    [SerializeField] private Button cardPrefab;

    [Header("Card Style")]
    [SerializeField] private Color player1CardColor = new Color(0.8f, 0.9f, 1f);
    [SerializeField] private Color player2CardColor = new Color(1f, 0.8f, 0.8f);

    private bool referencesValidated = false;

    private void Start() {
        // Add the DeckViewController to manage deck view interactions
        var deckViewController = GetComponent<DeckViewController>();
        if (deckViewController == null) {
            Log("Adding DeckViewController to GameReferences", LogTag.Initialization);
            deckViewController = gameObject.AddComponent<DeckViewController>();
        }

        // Ensure we have a tooltip instance
        if (cardTooltip == null) {
            Log("CardTooltip reference missing, creating one", LogTag.Initialization);
            CreateCardTooltip();
        }
    }

    // Method to create a tooltip if one doesn't exist
    private void CreateCardTooltip() {
        // Create tooltip GameObject
        GameObject tooltipObj = new GameObject("CardTooltip");
        tooltipObj.transform.SetParent(transform, false);

        // Add Canvas component for UI rendering
        Canvas tooltipCanvas = tooltipObj.AddComponent<Canvas>();
        tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tooltipCanvas.sortingOrder = 10000; // Make sure it's always on top

        // Add CanvasScaler for consistent UI sizing
        CanvasScaler scaler = tooltipObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Add CanvasGroup for fade control
        CanvasGroup canvasGroup = tooltipObj.AddComponent<CanvasGroup>();

        // Create background panel
        GameObject bgPanel = new GameObject("Background");
        bgPanel.transform.SetParent(tooltipObj.transform, false);

        // Add Image component to background
        Image background = bgPanel.AddComponent<Image>();
        background.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        // Configure the RectTransform for proper sizing and positioning
        RectTransform bgRect = background.rectTransform;
        bgRect.anchorMin = new Vector2(0.5f, 0);
        bgRect.anchorMax = new Vector2(0.5f, 0);
        bgRect.pivot = new Vector2(0.5f, 0);
        bgRect.sizeDelta = new Vector2(300, 150);

        // Add text for tooltip content
        GameObject textObj = new GameObject("TooltipText");
        textObj.transform.SetParent(bgPanel.transform, false);

        // Add TextMeshProUGUI component
        TextMeshProUGUI tooltipText = textObj.AddComponent<TextMeshProUGUI>();
        tooltipText.alignment = TextAlignmentOptions.Center;
        tooltipText.fontSize = 16;
        tooltipText.color = Color.white;
        tooltipText.enableWordWrapping = true;

        // Configure text RectTransform
        RectTransform textRect = tooltipText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);

        // Add shadow to make text more readable
        Shadow shadow = bgPanel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.5f);
        shadow.effectDistance = new Vector2(2, -2);

        // Add CardTooltip component
        cardTooltip = tooltipObj.AddComponent<CardTooltip>();

        // Set references
        cardTooltip.SetupReferences(tooltipText, bgRect, canvasGroup);

        // Hide initially
        tooltipObj.SetActive(false);

        Log("Card tooltip created successfully", LogTag.Initialization);
    }

    public override void Initialize() {
        if (IsInitialized) return;
        ValidateReferences();
        base.Initialize();

        Log("GameReferences initialized", LogTag.Initialization);
    }

    private void OnEnable() {
        ValidateReferences();
    }

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

        // Validate tooltip reference
        if (cardTooltip == null) {
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

    // All the original getter methods
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
    public CardTooltip GetCardTooltip() {
        if (cardTooltip == null) {
            LogWarning("CardTooltip is null when GetCardTooltip was called, creating one", LogTag.UI);
            CreateCardTooltip();
        }
        return cardTooltip;
    }

    public bool AreReferencesValid() {
        ValidateReferences();  // Force revalidation each time
        return referencesValidated;
    }
}