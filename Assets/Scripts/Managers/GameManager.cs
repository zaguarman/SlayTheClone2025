using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DebugLogger;

public class GameManager : InitializableComponent {
    private static GameManager instance;

    public static GameManager Instance {
        get {
            if (instance == null) {
                var go = new GameObject("GameManager");
                instance = go.AddComponent<GameManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    public Player Player1 { get; private set; }

    public Player Player2 { get; private set; }

    [ShowInInspector, BoxGroup("Hands"), PropertyOrder]
    [ListDrawerSettings]
    public List<string> Player1Hand => Player1?.Hand
        .Select((card, index) => $"Card {index + 1}: {card.Name}")
        .ToList() ?? new List<string>();

    [ShowInInspector, BoxGroup("Hands"), PropertyOrder]
    [ListDrawerSettings]
    public List<string> Player2Hand => Player2?.Hand
        .Select((card, index) => $"Card {index + 1}: {card.Name}")
        .ToList() ?? new List<string>();

    [ShowInInspector]
    public ActionsQueue ActionsQueue { get; private set; }
    public IWeatherSystem WeatherSystem { get; private set; }
    private BattlefieldCombatHandler combatHandler;

    private GameMediator gameMediator;
    private GameReferences gameReferences;
    public ICardDealingService cardDealingService { get; private set; }
    private System.Random random = new System.Random();

    public BattlefieldCombatHandler CombatHandler => combatHandler;

    private bool weatherSystemInitialized = false;

    protected override void Awake() {
        base.Awake();
        if (instance != null && instance != this) {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void Initialize() {
        if (IsInitialized) return;

        var initManager = InitializationManager.Instance;
        if (!initManager.IsComponentInitialized<GameReferences>() ||
            !initManager.IsComponentInitialized<GameMediator>()) {
            throw new System.InvalidOperationException("Required dependencies not initialized");
        }

        gameMediator = GameMediator.Instance;
        gameReferences = GameReferences.Instance;
        cardDealingService = new CardDealingService(gameMediator);

        InitializeWeatherSystem();
        InitializeCombatSystem();
        InitializeActionsQueue();
        InitializeGameSystem();

        base.Initialize();

        if (WeatherSystem != null) {
            WeatherSystem.SetWeather(WeatherType.Rainy);
        }
    }

    private void InitializeWeatherSystem() {
        if (!weatherSystemInitialized) {
            WeatherSystem = new WeatherSystem(gameMediator);
            weatherSystemInitialized = true;
            Log("Weather system initialized", LogTag.Initialization);
        }
    }

    private void InitializeCombatSystem() {
        combatHandler = new BattlefieldCombatHandler(this);
        Log("Combat system initialized", LogTag.Initialization);
    }

    private void InitializeActionsQueue() {
        ActionsQueue = new ActionsQueue(gameMediator, combatHandler);
        Log("Actions queue initialized", LogTag.Initialization);
    }

    private void InitializeGameSystem() {
        InitializePlayers();
        InitializeCards();

        if (GameUI.Instance.IsInitialized) {
            CompleteGameInitialization();
        } else {
            GameUI.Instance.onInitialized.AddListener(CompleteGameInitialization);
        }
    }

    private void CompleteGameInitialization() {
        SetupInitialGameState();
        PlaceInitialCreatures();
        SetupResolveButton();
        gameMediator.NotifyGameInitialized();
    }

    private void InitializePlayers() {
        Player1 = new Player();
        Player2 = new Player();
        Player1.Opponent = Player2;
        Player2.Opponent = Player1;

        // Set default cards to draw
        Player1.CardsToDraw = 5;
        Player2.CardsToDraw = 5;

        gameMediator.RegisterPlayer(Player1);
        gameMediator.RegisterPlayer(Player2);
    }

    private void InitializeCards() {
        var testSetup = gameObject.AddComponent<TestSetup>();

        // Get test cards without duplication
        var baseCards = testSetup.CreateTestCards();

        // Use just one copy of each card for both players
        var player1Cards = new List<CardData>(baseCards);
        var player2Cards = new List<CardData>(baseCards);

        cardDealingService.InitializeDecks(player1Cards, player2Cards);
        Log($"Decks initialized with {player1Cards.Count} cards per player", LogTag.Cards | LogTag.Initialization);

        // Clean up the test setup component
        Destroy(testSetup);
    }

    private void PlaceInitialCreatures() {
        if (!HasValidBattlefields()) {
            LogError("Cannot place creatures - battlefield not initialized", LogTag.Initialization);
            return;
        }

        var testSetup = gameObject.AddComponent<TestSetup>();
        var availableCreatures = testSetup.CreateTestCards()
            .Where(card => card is CreatureData)
            .Cast<CreatureData>()
            .ToList();

        PlaceCreaturesForPlayer(Player1, availableCreatures);
        PlaceCreaturesForPlayer(Player2, availableCreatures);
    }

    private bool HasValidBattlefields() {
        return Player1?.Battlefield != null && Player1.Battlefield.Any() &&
               Player2?.Battlefield != null && Player2.Battlefield.Any();
    }

    private void PlaceCreaturesForPlayer(IPlayer player, List<CreatureData> availableCreatures) {
        //for (int i = 0; i < 2; i++) {
        //    int randomIndex = random.Next(availableCreatures.Count);
        //    var creatureData = availableCreatures[randomIndex];
        //    var creature = CardFactory.CreateCard(creatureData) as ICreature;

        //    if (creature == null) continue;

        //    var emptySlot = player.Battlefield.FirstOrDefault(s => !s.IsOccupied());
        //    if (emptySlot == null) {
        //        LogWarning($"No empty battlefield slots available for {(player.IsPlayer1() ? "Player 1" : "Player 2")}", LogTag.Creatures);
        //        continue;
        //    }

        //    creature.SetOwner(player);
        //    player.AddToBattlefield(creature, emptySlot);
        //    Log($"Added {creature.Name} to {(player.IsPlayer1() ? "Player 1" : "Player 2")}'s battlefield", LogTag.Creatures | LogTag.Initialization);
        //}
    }

    private void SetupInitialGameState() {
        // Deal initial hands with fewer cards to ensure deck has cards remaining
        cardDealingService.DealInitialHands(Player1, Player2, 5);
        Log("Initial cards dealt to players", LogTag.Initialization);
    }

    private void SetupResolveButton() {
        var resolveButton = gameReferences.GetResolveActionsButton();
        if (resolveButton != null) {
            resolveButton.onClick.AddListener(OnResolveButtonClicked);
        }
    }

    private void OnResolveButtonClicked() {
        Log("Resolve button clicked, processing actions queue", LogTag.UI | LogTag.Actions);

        // Always resolve the actions - even if there are no player actions,
        // this will handle the discard and draw process
        ActionsQueue?.ResolveActions();
    }

    // Methods to handle cards to draw
    public void SetCardsToDraw(IPlayer player, int count) {
        if (player == null) return;

        player.CardsToDraw = count;
        Log($"Set cards to draw for {(player.IsPlayer1() ? "Player 1" : "Player 2")} to {count}", LogTag.Players | LogTag.Cards);
    }

    // Methods to force discard hand
    public void DiscardHand(IPlayer player) {
        if (player == null) return;

        ActionsQueue?.AddAction(new DiscardHandAction(player));
        Log($"Added discard hand action for {(player.IsPlayer1() ? "Player 1" : "Player 2")}", LogTag.Actions | LogTag.Cards);
    }

    // Method to force discard hand for all players
    public void DiscardAllHands() {
        if (Player1 != null) {
            ActionsQueue?.AddAction(new DiscardHandAction(Player1));
        }

        if (Player2 != null) {
            ActionsQueue?.AddAction(new DiscardHandAction(Player2));
        }

        Log("Added discard hand actions for all players", LogTag.Actions | LogTag.Cards);
    }

    // Method to draw cards for a player
    public void DrawCardsForPlayer(IPlayer player, int count = 1) {
        if (player == null) return;

        // Always use DrawCardsAction for consistency
        ActionsQueue?.AddAction(new DrawCardsAction(player, count));
        Log($"Added draw cards action for {(player.IsPlayer1() ? "Player 1" : "Player 2")} to draw {count} cards", LogTag.Actions | LogTag.Cards);
    }

    // Update the existing method to use the new action for consistency
    public void DrawCardForPlayer(IPlayer player) {
        if (player == null) return;
        DrawCardsForPlayer(player, 1);
    }
}