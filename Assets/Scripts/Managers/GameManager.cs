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
        PlaceInitialCreatures();
        SetupInitialGameState();
        SetupResolveButton();
        gameMediator.NotifyGameInitialized();
    }

    private void InitializePlayers() {
        Player1 = new Player();
        Player2 = new Player();
        Player1.Opponent = Player2;
        Player2.Opponent = Player1;

        gameMediator.RegisterPlayer(Player1);
        gameMediator.RegisterPlayer(Player2);
    }

    private void InitializeCards() {
        var testSetup = gameObject.AddComponent<TestSetup>();
        var player1Cards = testSetup.CreateTestCards();
        var player2Cards = testSetup.CreateTestCards();
        cardDealingService.InitializeDecks(player1Cards, player2Cards);
        Log("Decks initialized with test cards", LogTag.Cards | LogTag.Initialization);
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
        cardDealingService.DealInitialHands(Player1, Player2);
        gameMediator.NotifyGameInitialized();
    }

    private void SetupResolveButton() {
        var resolveButton = gameReferences.GetResolveActionsButton();
        if (resolveButton != null) {
            resolveButton.onClick.AddListener(OnResolveButtonClicked);
        }
    }

    private void OnResolveButtonClicked() {
        ActionsQueue?.ResolveActions();
    }

    // Add method to handle draw card actions
    public void DrawCardForPlayer(IPlayer player) {
        if (player == null) return;
        player.DrawCard();
    }
}