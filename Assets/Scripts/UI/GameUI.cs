using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class GameUI : MonoBehaviour {
        public bool IsInitialized { get; private set; }

        private WeatherController weatherController;
        private TurnUI turnUI;
        private DeckViewController deckViewController;
        private bool weatherSystemInitialized = false;

        private IGameManager gameManager;
        private IGameMediator gameMediator;
        private IGameReferences gameReferences;
        private CardViews cardViews;
        private TableViews tableViews;

        [FormerlySerializedAs("onInitialized")]
        public UnityEvent OnInitialized = new UnityEvent();

        /// <summary>
        /// Puts the UI together: the scene's references are read here, and each view is handed only what it uses.
        /// </summary>
        public void Initialize(IGameMediator mediator, IGameReferences references, IGameManager manager, CardViews views, TableViews table) {
            if (IsInitialized) return;

            gameMediator = mediator ?? throw new System.ArgumentNullException(nameof(mediator));
            gameReferences = references ?? throw new System.ArgumentNullException(nameof(references));
            gameManager = manager ?? throw new System.ArgumentNullException(nameof(manager));
            cardViews = views ?? throw new System.ArgumentNullException(nameof(views));
            tableViews = table ?? throw new System.ArgumentNullException(nameof(table));

            // Also covers each player's PlayerUI, HandUI and BattlefieldUI
            if (!gameReferences.AreReferencesValid()) {
                LogError("Cannot initialize GameUI - GameReferences are invalid", LogTag.UI | LogTag.Initialization);
                return;
            }

            turnUI = FindObjectOfType<TurnUI>() ?? CreateTurnUI();

            InitializeChildUI();
            InitializeControllers();
            SetupResolveButton();

            IsInitialized = true;
            Log("GameUI initialized successfully", LogTag.UI | LogTag.Initialization);

            OnInitialized.Invoke();
        }

        // Fallback for scenes without a TurnUI: a turn counter at the top of the screen
        private TurnUI CreateTurnUI() {
            var turnUIObj = new GameObject("TurnUI");
            turnUIObj.transform.SetParent(transform, false);
            var createdTurnUI = turnUIObj.AddComponent<TurnUI>();

            var textObj = new GameObject("TurnText");
            textObj.transform.SetParent(turnUIObj.transform, false);
            var turnText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            turnText.alignment = TMPro.TextAlignmentOptions.Center;
            turnText.fontSize = 24;
            turnText.color = Color.white;

            var rectTransform = turnUIObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1);
            rectTransform.anchorMax = new Vector2(0.5f, 1);
            rectTransform.pivot = new Vector2(0.5f, 1);
            rectTransform.anchoredPosition = new Vector2(0, -20);
            rectTransform.sizeDelta = new Vector2(200, 40);

            var textRectTransform = textObj.GetComponent<RectTransform>();
            textRectTransform.anchorMin = Vector2.zero;
            textRectTransform.anchorMax = Vector2.one;
            textRectTransform.offsetMin = Vector2.zero;
            textRectTransform.offsetMax = Vector2.zero;

            Log("Created TurnUI component", LogTag.UI | LogTag.Initialization);
            return createdTurnUI;
        }

        private void InitializeChildUI() {
            if (gameManager.Player1 == null || gameManager.Player2 == null) {
                LogError("GameManager or Players not initialized before GameUI.InitializeChildUI!", LogTag.Initialization | LogTag.UI);
                return;
            }

            InitializePlayerUI(gameManager.Player1);
            InitializePlayerUI(gameManager.Player2);
            turnUI.Initialize(gameMediator, gameManager);
            gameReferences.GetDeckViewUI()?.Initialize(gameMediator, gameManager, cardViews);

            Log("All child UI components initialized", LogTag.UI | LogTag.Initialization);
        }

        private void InitializePlayerUI(IPlayer player) {
            var playerReferences = player.IsPlayer1 ? gameReferences.Player1References : gameReferences.Player2References;
            playerReferences.PlayerUI.Initialize(player, gameMediator, gameManager, playerReferences.HealthText);
            playerReferences.BattlefieldUI.Initialize(player, gameMediator, gameManager, cardViews, tableViews);
            playerReferences.HandUI.Initialize(player, gameMediator, gameManager, cardViews);
        }

        private void InitializeControllers() {
            weatherController = GetOrAddComponent<WeatherController>();
            weatherController.Initialize(gameMediator, gameManager, gameReferences.GetWeatherCycleButton(), gameReferences.GetWeatherText());
            weatherSystemInitialized = weatherController.IsInitialized;
            LogControllerInitialized("Weather controller", weatherSystemInitialized);

            deckViewController = GetOrAddComponent<DeckViewController>();
            deckViewController.Initialize(gameMediator, gameManager,
                gameReferences.GetDeckViewButton(), gameReferences.GetDiscardViewButton(), gameReferences.GetDeckViewUI());
            LogControllerInitialized("DeckViewController", deckViewController.IsInitialized);
        }

        private T GetOrAddComponent<T>() where T : Component {
            var component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void LogControllerInitialized(string controllerName, bool initialized) {
            if (initialized) Log($"{controllerName} initialized via GameUI", LogTag.UI | LogTag.Initialization);
            else LogError($"{controllerName} FAILED initialization via GameUI", LogTag.UI | LogTag.Initialization);
        }

        protected void OnDestroy() {
            OnInitialized.RemoveAllListeners();
            Log("GameUI destroyed", LogTag.UI);
            IsInitialized = false;
        }

        public bool IsWeatherSystemInitialized() {
            return weatherSystemInitialized && weatherController != null;
        }

        private void SetupResolveButton() {
            var resolveButton = gameReferences.GetResolveActionsButton();
            var turnManager = gameManager.TurnManager;

            if (resolveButton == null || turnManager == null) {
                LogError("Failed to set up Resolve Actions Button listener in GameUI.", LogTag.UI | LogTag.Initialization);
                return;
            }

            resolveButton.onClick.RemoveAllListeners();
            resolveButton.onClick.AddListener(turnManager.EndTurn);

            var buttonText = resolveButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (buttonText != null) buttonText.text = "End Turn";

            Log("Resolve Actions Button (End Turn) listener set up by GameUI.", LogTag.UI | LogTag.Initialization);
        }
    }
}
