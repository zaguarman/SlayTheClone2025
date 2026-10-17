using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
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

            turnUI = FindFirstObjectByType<TurnUI>() ?? CreateTurnUI();

            InitializeChildUI();
            InitializeControllers();
            SetupResolveButton();
            var skin = GetOrAddComponent<BoardSkin>();
            skin.Apply(gameReferences, gameManager, gameMediator, cardViews);
            skin.PutAtBottom(FogOfWar.PlaysAsPlayer1);
            CreateQueuePanel(skin);
            CreateEnemyPileButtons(skin);
            // The deck window covers every control made just now (the Queue button, the enemy's piles), as it does the scene's
            var deckWindow = gameReferences.GetDeckViewUI();
            if (deckWindow != null && deckWindow.DeckViewPanel != null) deckWindow.DeckViewPanel.transform.SetAsLastSibling();
            GameOver = GameOverScreen.Create(transform, gameManager, gameMediator);
            SendForPicker = GetOrAddComponent<SendForPicker>();
            SendForPicker.Initialize(gameManager, gameMediator, gameReferences.GetDeckViewUI());
            DiscardPicker = GetOrAddComponent<DiscardPicker>();
            DiscardPicker.Initialize(gameManager, gameMediator, gameReferences.GetDeckViewUI());
            gameMediator.GameOver += OnGameOver;
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
            // The bottom hand's discards fly into the discard pile's button; the top one's off the screen
            playerReferences.HandUI.DiscardPile = gameReferences.GetDiscardViewButton()?.transform;
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

        /// <summary>Asks the screen's player which card a Send For fetches.</summary>
        public SendForPicker SendForPicker { get; private set; }

        /// <summary>Asks the screen's player which cards pay for a chosen discard cost.</summary>
        public DiscardPicker DiscardPicker { get; private set; }

        /// <summary>What covers the table when the combat is over.</summary>
        public GameOverScreen GameOver { get; private set; }

        /// <summary>
        /// Closes the windows that cover the table (the deck and discard piles, the queue), so the board can be watched: when
        /// a turn starts resolving, and when a test or a game asks for a clear view.
        /// </summary>
        public void CloseMenus() {
            if (QueuePanel != null && QueuePanel.IsOpen) QueuePanel.SetOpen(false);
            var deckWindow = gameReferences?.GetDeckViewUI();
            if (deckWindow != null && deckWindow.IsOpen) deckWindow.HideDeckView();
        }

        private bool wasResolving;
        private int lastTurn;

        // No more turns: End Turn goes grey
        private void OnGameOver(IPlayer winner) {
            var endTurn = gameReferences.GetResolveActionsButton();
            if (endTurn != null) endTurn.interactable = false;
        }

        /// <summary>The Queue button and panel: what will resolve, in order, as the screen's player sees it.</summary>
        public QueuePanel QueuePanel { get; private set; }

        private void CreateQueuePanel(BoardSkin skin) {
            var endTurn = gameReferences.GetResolveActionsButton();
            if (endTurn == null) return;
            QueuePanel = QueuePanel.Create(endTurn.transform, gameManager, gameMediator);
            skin.PlaceQueue(QueuePanel.Toggle, QueuePanel.Panel);
        }

        // The other side's deck and discard pile, by its hand: copies of the screen player's buttons, shown while Fog of War is off
        private void CreateEnemyPileButtons(BoardSkin skin) {
            var deck = gameReferences.GetDeckViewButton();
            var discard = gameReferences.GetDiscardViewButton();
            if (deck == null || discard == null || deckViewController == null) return;
            var enemyDeck = Instantiate(deck, deck.transform.parent);
            enemyDeck.name = "EnemyDeckViewButton";
            var enemyDiscard = Instantiate(discard, discard.transform.parent);
            enemyDiscard.name = "EnemyDiscardViewButton";
            // Drawn with the other piles, under the panels that open over the table (the queue's)
            enemyDeck.transform.SetSiblingIndex(deck.transform.GetSiblingIndex() + 1);
            enemyDiscard.transform.SetSiblingIndex(discard.transform.GetSiblingIndex() + 1);
            skin.PlaceEnemyPiles((RectTransform)enemyDeck.transform, (RectTransform)enemyDiscard.transform);
            deckViewController.AddEnemyButtons(enemyDeck, enemyDiscard);
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
        private TMPro.TextMeshProUGUI shortcutsLabel;

        private void Update() {
            if (!IsInitialized) return;
            // The turn plays out (or already has, in one go): let the player watch it
            var turns = gameManager.TurnManager;
            bool resolving = turns?.IsResolving ?? false;
            int turn = turns?.TurnNumber ?? 0;
            if ((resolving && !wasResolving) || turn != lastTurn) CloseMenus();
            wasResolving = resolving;
            lastTurn = turn;
            if (GameInput.KeyPressed(FogOfWar.ToggleKey)) FogOfWar.Toggle();
            if (GameInput.KeyPressed(FogOfWar.SwitchSidesKey)) FogOfWar.SwitchSides();
            if (GameInput.KeyPressed(AIController.Player1Key)) {
                AIController.CycleSeat(AISeats.Player1);
                ShowFogOfWarState();
            }
            if (GameInput.KeyPressed(AIController.Player2Key)) {
                AIController.CycleSeat(AISeats.Player2);
                ShowFogOfWarState();
            }
        }

        // Every view redraws what it may show, and the screen's player sits at the bottom
        private void OnFogOfWarChanged() {
            ShowFogOfWarState();
            GetComponent<BoardSkin>()?.PutAtBottom(FogOfWar.PlaysAsPlayer1);
            gameMediator.NotifyBattlefieldStateChanged(gameManager.Player1);
            gameMediator.NotifyBattlefieldStateChanged(gameManager.Player2);
            gameMediator.NotifyActionsQueueChanged();
        }

        /// <summary>The note under the End Turn button: the debug switches' state, the seats the AI plays, and the keyboard shortcuts.</summary>
        /// Every line reads the same way: the key, then what it switches and how it's set now.
        public string ShortcutsText =>
            Shortcut(KeyName(FogOfWar.ToggleKey), $"Fog of War: {(FogOfWar.Enabled ? "On" : "Off")}") + "\n" +
            Shortcut(KeyName(FogOfWar.SwitchSidesKey), $"Playing as: Player {(FogOfWar.PlaysAsPlayer1 ? 1 : 2)}") + "\n" +
            Shortcut(KeyName(AIController.Player1Key), $"AI Player 1: {AIController.SeatText(AISeats.Player1)}") + "\n" +
            Shortcut(KeyName(AIController.Player2Key), $"AI Player 2: {AIController.SeatText(AISeats.Player2)}") + "\n" +
            $"<size=85%>{Shortcut("Esc", "Menu")}    {Shortcut("Shift + drag", "Use an ability")}\n{Shortcut("Right-drag", "Attack your own")}</size>";

        private static string KeyName(UnityEngine.InputSystem.Key key) => key.ToString().Replace("Digit", "");

        private static string Shortcut(string key, string what) => $"<color=#B8B0A0>{key}</color>  {what}";

        // Always shown under the End Turn button: whether the fog is on, whose view it is, and the other shortcuts
        private void ShowFogOfWarState() {
            if (shortcutsLabel == null) {
                var endTurn = gameReferences.GetResolveActionsButton();
                if (endTurn == null) return;
                shortcutsLabel = UITheme.NewText("Shortcuts", endTurn.transform, "", 16f, UITheme.Gold, TMPro.TextAlignmentOptions.TopRight);
                var rect = shortcutsLabel.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f); // Lined up with the button's right edge: it stays on screen
                rect.pivot = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(420f, 124f);
                rect.anchoredPosition = new Vector2(0f, -6f);
                shortcutsLabel.raycastTarget = false;
            }
            shortcutsLabel.text = ShortcutsText;
            shortcutsLabel.gameObject.SetActive(true);
        }
        #endregion

        protected void OnDestroy() {
            FogOfWar.Changed -= OnFogOfWarChanged;
            if (gameMediator != null) gameMediator.GameOver -= OnGameOver;
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
            resolveButton.onClick.AddListener(() => {
                if (!DeckViewUI.PickPending) turnManager.EndTurn(); // A card to pick first (Send For)
            });

            var buttonText = resolveButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (buttonText != null) buttonText.text = "End Turn";

            Log("Resolve Actions Button (End Turn) listener set up by GameUI.", LogTag.UI | LogTag.Initialization);
        }
    }
}
