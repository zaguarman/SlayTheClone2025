using UnityEngine;
using static DebugLogger;
using UnityEngine.Events;

public class GameUI : InitializableComponent {
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
    private HandUI player1HandUI;
    private HandUI player2HandUI;
    private WeatherController weatherController;
    private TurnUI turnUI;
    private DeckViewController deckViewController;
    private bool weatherSystemInitialized = false;

    // Keep fields to store dependencies needed by children
    private GameManager _gameManager; // Change to IGameManager later
    private IGameMediator _gameMediator;
    private IGameReferences _gameReferences;

    // UnityEvent for initialization completion
    public UnityEvent onInitialized = new UnityEvent();

    protected override void Awake() {
        base.Awake();
        if (instance != null && instance != this) {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    // New Initialize method with explicit dependency injection
    public void Initialize(IGameMediator mediator, IGameReferences references)
    {
        if (IsInitialized) return;

        // Store injected dependencies
        _gameMediator = mediator ?? throw new System.ArgumentNullException(nameof(mediator));
        _gameReferences = references ?? throw new System.ArgumentNullException(nameof(references));

        // Still using GameManager singleton for now (will be refactored later)
        _gameManager = GameManager.Instance;

        if (_gameManager == null) {
            LogError("Cannot initialize GameUI - GameManager not ready", LogTag.UI | LogTag.Initialization);
            return;
        }
        if (!_gameReferences.AreReferencesValid()) {
             LogError("Cannot initialize GameUI - GameReferences are invalid", LogTag.UI | LogTag.Initialization);
             return;
        }

        // Call base.Initialize() from InitializableComponent to set the flag
        base.Initialize(); // Sets IsInitialized = true

        GetChildReferences();
        if (!ValidateChildReferences()) {
            LogError("Failed to validate child UI references in GameUI", LogTag.UI | LogTag.Initialization);
            return;
        }

        InitializeChildUI(); // Initialize children, passing dependencies
        InitializeControllers(); // Initialize WeatherController, DeckViewController

        // Update "Resolve Actions" button text
        var resolveButton = _gameReferences.GetResolveActionsButton();
        if (resolveButton != null) {
            var buttonText = resolveButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (buttonText != null) buttonText.text = "End Turn";
        }

        // IsInitialized is set by base.Initialize()
        Log("GameUI initialized successfully", LogTag.UI | LogTag.Initialization);
        onInitialized.Invoke(); // Notify that GameUI itself is ready
    }

    private void GetChildReferences() {
        if (_gameReferences == null) return;

        player1UI = _gameReferences.GetPlayer1UI();
        player2UI = _gameReferences.GetPlayer2UI();
        player1BattlefieldUI = _gameReferences.GetPlayer1BattlefieldUI();
        player2BattlefieldUI = _gameReferences.GetPlayer2BattlefieldUI();
        player1HandUI = _gameReferences.GetPlayer1HandUI();
        player2HandUI = _gameReferences.GetPlayer2HandUI();

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

    private bool ValidateChildReferences() {
        bool isValid = true;
        if (player1UI == null) { LogError("Player 1 UI reference missing in GameReferences", LogTag.UI); isValid = false; }
        if (player2UI == null) { LogError("Player 2 UI reference missing in GameReferences", LogTag.UI); isValid = false; }
        if (player1BattlefieldUI == null) { LogError("Player 1 Battlefield UI reference missing in GameReferences", LogTag.UI); isValid = false; }
        if (player2BattlefieldUI == null) { LogError("Player 2 Battlefield UI reference missing in GameReferences", LogTag.UI); isValid = false; }
        if (player1HandUI == null) { LogError("Player 1 Hand UI reference missing in GameReferences", LogTag.UI); isValid = false; }
        if (player2HandUI == null) { LogError("Player 2 Hand UI reference missing in GameReferences", LogTag.UI); isValid = false; }
        if (turnUI == null) { LogError("TurnUI reference missing", LogTag.UI); isValid = false; }
        return isValid;
    }

    private void InitializeChildUI() {
        if (_gameManager.Player1 == null || _gameManager.Player2 == null) {
             LogError("Players not initialized in GameManager before GameUI initialization!", LogTag.Initialization | LogTag.UI);
             return;
        }

        player1UI?.Initialize(_gameManager.Player1, _gameMediator, _gameReferences);
        player2UI?.Initialize(_gameManager.Player2, _gameMediator, _gameReferences);
        player1BattlefieldUI?.Initialize(_gameManager.Player1, _gameMediator, _gameReferences);
        player2BattlefieldUI?.Initialize(_gameManager.Player2, _gameMediator, _gameReferences);
        player1HandUI?.Initialize(_gameManager.Player1, _gameMediator, _gameReferences);
        player2HandUI?.Initialize(_gameManager.Player2, _gameMediator, _gameReferences);
        turnUI?.Initialize(_gameMediator, _gameReferences);
        _gameReferences.GetDeckViewUI()?.Initialize(_gameMediator, _gameReferences); // DeckViewUI doesn't need player

        Log("All child UI components initialized", LogTag.UI | LogTag.Initialization);
    }

    private void InitializeControllers() {
        // Initialize WeatherController
        if (_gameManager?.WeatherSystem == null) {
            LogError("Cannot initialize WeatherController - WeatherSystem not ready", LogTag.UI | LogTag.Initialization);
        } else {
            weatherController = GetComponent<WeatherController>();
            if (weatherController == null) weatherController = gameObject.AddComponent<WeatherController>();
            // Ensure WeatherController is initialized with dependencies
            weatherController.Initialize(_gameMediator, _gameReferences); // <<< ADDED CALL
            weatherSystemInitialized = true;
            Log("Weather controller initialized via GameUI", LogTag.UI | LogTag.Initialization);
        }

        // Initialize DeckViewController
        deckViewController = GetComponent<DeckViewController>();
        if (deckViewController == null) deckViewController = gameObject.AddComponent<DeckViewController>();
        // Ensure DeckViewController is initialized with dependencies
        deckViewController.Initialize(_gameMediator, _gameReferences); // <<< ADDED CALL
        Log("DeckViewController initialized via GameUI", LogTag.UI | LogTag.Initialization);
    }

    private void OnEnable() {
        if (IsInitialized && _gameMediator != null) {
            _gameMediator.AddGameInitializedListener(OnGameInitialized);
            Log("GameUI events registered", LogTag.UI);
        }
    }

    private void OnDisable() {
        if (_gameMediator != null) {
            _gameMediator.RemoveGameInitializedListener(OnGameInitialized);
            Log("GameUI events unregistered", LogTag.UI);
        }
    }

    private void OnGameInitialized() {
        Log("GameUI notified of game initialization", LogTag.UI);
        // Notify child components if needed
    }

    protected override void OnDestroy() {
        if (instance == this) {
            // No longer need to destroy weatherController here if it's a component
            // if (weatherController != null) {
            //     Destroy(weatherController);
            //     weatherSystemInitialized = false;
            // }
            instance = null;
            onInitialized.RemoveAllListeners(); // Clean up listeners
            Log("GameUI destroyed", LogTag.UI);
        }
        base.OnDestroy();
    }

    public bool IsWeatherSystemInitialized() {
        return weatherSystemInitialized && weatherController != null;
    }


}