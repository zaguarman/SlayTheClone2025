using UnityEngine;
using static DebugLogger;
using UnityEngine.Events;

public class GameUI : MonoBehaviour {
    public bool IsInitialized { get; private set; }

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
    private IGameManager _gameManager; // Changed to IGameManager
    private IGameMediator _gameMediator;
    private IGameReferences _gameReferences;

    // UnityEvent for initialization completion
    public UnityEvent onInitialized = new UnityEvent();

    protected void Awake() {
        // No singleton initialization needed
    }

    // Initialize method with explicit dependency injection including IGameManager
    public void Initialize(IGameMediator mediator, IGameReferences references, IGameManager manager)
    {
        if (IsInitialized) return;

        // Store injected dependencies
        _gameMediator = mediator ?? throw new System.ArgumentNullException(nameof(mediator));
        _gameReferences = references ?? throw new System.ArgumentNullException(nameof(references));
        _gameManager = manager ?? throw new System.ArgumentNullException(nameof(manager));

        if (!_gameReferences.AreReferencesValid()) {
             LogError("Cannot initialize GameUI - GameReferences are invalid", LogTag.UI | LogTag.Initialization);
             return;
        }

        GetChildReferences();
        if (!ValidateChildReferences()) {
            LogError("Failed to validate child UI references in GameUI", LogTag.UI | LogTag.Initialization);
            return;
        }

        InitializeChildUI(); // Initialize children, passing dependencies
        InitializeControllers(); // Initialize WeatherController, DeckViewController

        // Setup the resolve button (End Turn)
        SetupResolveButton();

        // Set flag AFTER setup
        IsInitialized = true;
        Log("GameUI initialized successfully", LogTag.UI | LogTag.Initialization);

        // Now that UI and battlefields are initialized, place initial creatures
        if (_gameManager != null && _gameManager.IsInitialized) {
            Log("Calling GameManager to place initial creatures now that battlefields are initialized", LogTag.UI | LogTag.Initialization);
            _gameManager.PlaceInitialCreatures();
        } else {
            LogError("Cannot place initial creatures - GameManager is null or not initialized", LogTag.UI | LogTag.Initialization);
        }

        onInitialized.Invoke();
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
        if (_gameManager == null || _gameManager.Player1 == null || _gameManager.Player2 == null) { // Check gameManager itself too
             LogError("GameManager or Players not initialized before GameUI.InitializeChildUI!", LogTag.Initialization | LogTag.UI);
             return;
        }

        // Pass _gameManager (IGameManager) to Initialize methods
        player1UI?.Initialize(_gameManager.Player1, _gameMediator, _gameReferences, _gameManager);
        player2UI?.Initialize(_gameManager.Player2, _gameMediator, _gameReferences, _gameManager);
        player1BattlefieldUI?.Initialize(_gameManager.Player1, _gameMediator, _gameReferences, _gameManager);
        player2BattlefieldUI?.Initialize(_gameManager.Player2, _gameMediator, _gameReferences, _gameManager);
        player1HandUI?.Initialize(_gameManager.Player1, _gameMediator, _gameReferences, _gameManager);
        player2HandUI?.Initialize(_gameManager.Player2, _gameMediator, _gameReferences, _gameManager);
        turnUI?.Initialize(_gameMediator, _gameReferences, _gameManager); // TurnUI also needs GameManager
        _gameReferences.GetDeckViewUI()?.Initialize(_gameMediator, _gameReferences, _gameManager); // DeckViewUI also needs GameManager

        Log("All child UI components initialized", LogTag.UI | LogTag.Initialization);
    }

    private void InitializeControllers() {
        // Initialize WeatherController
        weatherController = GetComponent<WeatherController>();
        if (weatherController == null) weatherController = gameObject.AddComponent<WeatherController>();
        weatherController.Initialize(_gameMediator, _gameReferences, _gameManager); // Pass manager
        weatherSystemInitialized = (weatherController != null && weatherController.IsInitialized); // Check IsInitialized
        if (weatherSystemInitialized) Log("Weather controller initialized via GameUI", LogTag.UI | LogTag.Initialization);
        else LogError("Weather controller FAILED initialization via GameUI", LogTag.UI | LogTag.Initialization);

        // Initialize DeckViewController
        deckViewController = GetComponent<DeckViewController>();
        if (deckViewController == null) deckViewController = gameObject.AddComponent<DeckViewController>();
        deckViewController.Initialize(_gameMediator, _gameReferences, _gameManager); // Pass manager
        if(deckViewController.IsInitialized) Log("DeckViewController initialized via GameUI", LogTag.UI | LogTag.Initialization);
        else LogError("DeckViewController FAILED initialization via GameUI", LogTag.UI | LogTag.Initialization);
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

    protected void OnDestroy() {
        // Clean up resources
        onInitialized.RemoveAllListeners();
        Log("GameUI destroyed", LogTag.UI);
        IsInitialized = false;
    }

    public bool IsWeatherSystemInitialized() {
        return weatherSystemInitialized && weatherController != null;
    }

    private void SetupResolveButton() {
        var resolveButton = _gameReferences.GetResolveActionsButton();
        var turnManager = _gameManager?.TurnManager; // Get TurnManager via injected GameManager

        if (resolveButton != null && turnManager != null) {
            resolveButton.onClick.RemoveAllListeners(); // Clear existing
            resolveButton.onClick.AddListener(turnManager.EndTurn); // Hook directly to TurnManager

            // Update button text
            var buttonText = resolveButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (buttonText != null) buttonText.text = "End Turn";

            Log("Resolve Actions Button (End Turn) listener set up by GameUI.", LogTag.UI | LogTag.Initialization);
        } else {
            LogError("Failed to set up Resolve Actions Button listener in GameUI.", LogTag.UI | LogTag.Initialization);
        }
    }
}