using UnityEngine;
using static DebugLogger;

public class GameUI : UIComponent {
    private static GameUI instance;
    public static GameUI Instance {
        get {
            if (instance == null) {
                instance = FindObjectOfType<GameUI>();
                if (instance == null) {
                    Debug.LogError("GameUI not found in scene!");
                }
            }
            return instance;
        }
    }

    private PlayerUI player1UI;
    private PlayerUI player2UI;
    private BattlefieldUI player1BattlefieldUI;
    private BattlefieldUI player2BattlefieldUI;
    private WeatherController weatherController;
    private TurnUI turnUI;
    private bool weatherSystemInitialized = false;

    protected override void Awake() {
        base.Awake();
        if (instance != null && instance != this) {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public override void Initialize() {
        if (IsInitialized) return;

        if (!InitializationManager.Instance.IsComponentInitialized<GameManager>()) {
            LogError("Cannot initialize GameUI - GameManager not initialized", LogTag.UI | LogTag.Initialization);
            return;
        }

        GetReferences();
        if (!ValidateReferences()) {
            LogError("Failed to validate UI references", LogTag.UI | LogTag.Initialization);
            return;
        }

        InitializeUI();
        InitializeWeatherSystem();
        RegisterEvents();

        IsInitialized = true;
        Log("GameUI initialized successfully", LogTag.UI | LogTag.Initialization);
        onInitialized.Invoke();
    }

    private void GetReferences() {
        if (gameReferences == null) {
            LogError("GameReferences not found during UI initialization", LogTag.UI | LogTag.Initialization);
            return;
        }

        player1UI = gameReferences.GetPlayer1UI();
        player2UI = gameReferences.GetPlayer2UI();
        player1BattlefieldUI = gameReferences.GetPlayer1BattlefieldUI();
        player2BattlefieldUI = gameReferences.GetPlayer2BattlefieldUI();

        // Find TurnUI, create it if it doesn't exist
        turnUI = FindObjectOfType<TurnUI>();
        if (turnUI == null) {
            GameObject turnUIObj = new GameObject("TurnUI");
            turnUIObj.transform.SetParent(transform, false);
            turnUI = turnUIObj.AddComponent<TurnUI>();

            // Add TextMeshProUGUI component for displaying turn number
            GameObject textObj = new GameObject("TurnText");
            textObj.transform.SetParent(turnUIObj.transform, false);
            var turnText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            turnText.alignment = TMPro.TextAlignmentOptions.Center;
            turnText.fontSize = 24;
            turnText.color = Color.white;

            // Position the turn UI at the top of the screen
            var rectTransform = turnUIObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1);
            rectTransform.anchorMax = new Vector2(0.5f, 1);
            rectTransform.pivot = new Vector2(0.5f, 1);
            rectTransform.anchoredPosition = new Vector2(0, -20);
            rectTransform.sizeDelta = new Vector2(200, 40);

            // Position the text properly
            var textRectTransform = textObj.GetComponent<RectTransform>();
            textRectTransform.anchorMin = Vector2.zero;
            textRectTransform.anchorMax = Vector2.one;
            textRectTransform.offsetMin = Vector2.zero;
            textRectTransform.offsetMax = Vector2.zero;

            Log("Created TurnUI component", LogTag.UI | LogTag.Initialization);
        }
    }

    private bool ValidateReferences() {
        if (player1UI == null || player2UI == null) {
            LogError("Player UI references missing", LogTag.UI | LogTag.Initialization);
            return false;
        }

        if (player1BattlefieldUI == null || player2BattlefieldUI == null) {
            LogError("Battlefield UI references missing", LogTag.UI | LogTag.Initialization);
            return false;
        }

        if (turnUI == null) {
            LogError("TurnUI reference missing", LogTag.UI | LogTag.Initialization);
            return false;
        }

        return true;
    }

    private void InitializeUI() {
        // Initialize Player UIs
        if (player1UI != null && gameManager.Player1 != null) {
            player1UI.Initialize(gameManager.Player1);
            Log("Player 1 UI initialized", LogTag.UI | LogTag.Initialization);
        }

        if (player2UI != null && gameManager.Player2 != null) {
            player2UI.Initialize(gameManager.Player2);
            Log("Player 2 UI initialized", LogTag.UI | LogTag.Initialization);
        }

        // Initialize Battlefield UIs
        if (player1BattlefieldUI != null && gameManager.Player1 != null) {
            player1BattlefieldUI.Initialize(gameManager.Player1);
            Log("Player 1 Battlefield UI initialized", LogTag.UI | LogTag.Initialization);
        }

        if (player2BattlefieldUI != null && gameManager.Player2 != null) {
            player2BattlefieldUI.Initialize(gameManager.Player2);
            Log("Player 2 Battlefield UI initialized", LogTag.UI | LogTag.Initialization);
        }

        // Initialize Turn UI
        if (turnUI != null) {
            turnUI.Initialize();
            Log("Turn UI initialized", LogTag.UI | LogTag.Initialization | LogTag.Turns);
        }
    }

    private void InitializeWeatherSystem() {
        // Only initialize if GameManager's WeatherSystem is ready
        if (gameManager?.WeatherSystem == null) {
            LogError("Cannot initialize WeatherController - WeatherSystem not ready", LogTag.UI | LogTag.Initialization);
            return;
        }

        if (weatherController == null) {
            weatherController = gameObject.AddComponent<WeatherController>();
            weatherSystemInitialized = true;
            Log("Weather controller initialized", LogTag.UI | LogTag.Initialization);
        }
    }

    protected override void RegisterEvents() {
        if (gameMediator != null) {
            gameMediator.AddGameInitializedListener(OnGameInitialized);
            Log("GameUI events registered", LogTag.UI | LogTag.Initialization);
        }
    }

    protected override void UnregisterEvents() {
        if (gameMediator != null) {
            gameMediator.RemoveGameInitializedListener(OnGameInitialized);
            Log("GameUI events unregistered", LogTag.UI);
        }
    }

    public override void UpdateUI(IPlayer player = null) {
        // do nothing, the ui components are self contained
    }

    private void OnGameInitialized() {
        UpdateUI();
        Log("GameUI updated after game initialization", LogTag.UI);
    }

    protected override void OnDestroy() {
        if (instance == this) {
            if (weatherController != null) {
                Destroy(weatherController);
                weatherSystemInitialized = false;
            }
            instance = null;
            Log("GameUI destroyed", LogTag.UI);
        }
        base.OnDestroy();
    }

    public bool IsWeatherSystemInitialized() {
        return weatherSystemInitialized && weatherController != null;
    }
}