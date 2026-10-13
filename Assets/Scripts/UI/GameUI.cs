using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class GameUI : MonoBehaviour {
        public bool IsInitialized { get; private set; }

        private TrendController trendController;
        private TurnUI turnUI;
        private DeckViewController deckViewController;

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
            var skin = GetOrAddComponent<BoardSkin>();
            skin.Apply(gameReferences, gameManager, gameMediator);
            skin.PutAtBottom(FogOfWar.PlaysAsPlayer1);
            CreateQueuePanel(skin);
            FogOfWar.Changed += OnFogOfWarChanged;
            ShowFogOfWarState();

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
            trendController = GetOrAddComponent<TrendController>();
            trendController.Initialize(gameMediator, gameManager, gameReferences.GetWeatherCycleButton(), gameReferences.GetWeatherText());
            LogControllerInitialized("Trend controller", trendController.IsInitialized);

            deckViewController = GetOrAddComponent<DeckViewController>();
            deckViewController.Initialize(gameMediator, gameManager,
                gameReferences.GetDeckViewButton(), gameReferences.GetDiscardViewButton(), gameReferences.GetDeckViewUI());
            LogControllerInitialized("DeckViewController", deckViewController.IsInitialized);
        }

        /// <summary>The Queue button and panel: what will resolve, in order, as the screen's player sees it.</summary>
        public QueuePanel QueuePanel { get; private set; }

        private void CreateQueuePanel(BoardSkin skin) {
            var endTurn = gameReferences.GetResolveActionsButton();
            if (endTurn == null) return;
            QueuePanel = QueuePanel.Create(endTurn.transform, gameManager, gameMediator);
            skin.PlaceQueue(QueuePanel.Toggle, QueuePanel.Panel);
        }

        private T GetOrAddComponent<T>() where T : Component {
            var component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void LogControllerInitialized(string controllerName, bool initialized) {
            if (initialized) Log($"{controllerName} initialized via GameUI", LogTag.UI | LogTag.Initialization);
            else LogError($"{controllerName} FAILED initialization via GameUI", LogTag.UI | LogTag.Initialization);
        }

        #region Fog of War (debug switch)
        private TMPro.TextMeshProUGUI fogOffLabel;

        private void Update() {
            if (!IsInitialized) return;
            if (GameInput.KeyPressed(FogOfWar.ToggleKey)) FogOfWar.Toggle();
            if (GameInput.KeyPressed(FogOfWar.SwitchSidesKey)) FogOfWar.SwitchSides();
        }

        // Every view redraws what it may show, and the screen's player sits at the bottom
        private void OnFogOfWarChanged() {
            ShowFogOfWarState();
            GetComponent<BoardSkin>()?.PutAtBottom(FogOfWar.PlaysAsPlayer1);
            gameMediator.NotifyBattlefieldStateChanged(gameManager.Player1);
            gameMediator.NotifyBattlefieldStateChanged(gameManager.Player2);
            gameMediator.NotifyActionsQueueChanged();
        }

        // While the fog is off, or the screen shows player 2's view, a reminder under the End Turn button says so
        private void ShowFogOfWarState() {
            if (FogOfWar.Enabled && FogOfWar.PlaysAsPlayer1) {
                if (fogOffLabel != null) fogOffLabel.gameObject.SetActive(false);
                return;
            }
            if (fogOffLabel == null) {
                var endTurn = gameReferences.GetResolveActionsButton();
                if (endTurn == null) return;
                fogOffLabel = UITheme.NewText("FogOfWarOff", endTurn.transform, $"FOG OF WAR OFF (debug, {FogOfWar.ToggleKey})", 18f, UITheme.Gold,
                    TMPro.TextAlignmentOptions.Top);
                var rect = fogOffLabel.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(320f, 28f);
                rect.anchoredPosition = new Vector2(0f, -6f);
                fogOffLabel.raycastTarget = false;
            }
            var notes = new System.Collections.Generic.List<string>();
            if (!FogOfWar.Enabled) notes.Add($"FOG OF WAR OFF ({FogOfWar.ToggleKey})");
            if (!FogOfWar.PlaysAsPlayer1) notes.Add($"PLAYING AS PLAYER 2 ({FogOfWar.SwitchSidesKey})");
            fogOffLabel.text = string.Join("  ·  ", notes) + " (debug)";
            fogOffLabel.gameObject.SetActive(true);
        }
        #endregion

        protected void OnDestroy() {
            FogOfWar.Changed -= OnFogOfWarChanged;
            OnInitialized.RemoveAllListeners();
            Log("GameUI destroyed", LogTag.UI);
            IsInitialized = false;
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
