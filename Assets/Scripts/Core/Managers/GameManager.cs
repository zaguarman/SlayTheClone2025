using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DebugLogger;

public class GameManager : InitializableComponent {
    #region Singleton
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
    #endregion

    #region Fields & Properties
    [ShowInInspector]
    public ActionsQueue ActionsQueue { get; private set; }
    public IWeatherSystem WeatherSystem { get; private set; }
    private BattlefieldCombatHandler combatHandler;
    private TurnManager turnManager;

    private GameMediator gameMediator;
    private GameReferences gameReferences;
    public ICardDealingService cardDealingService { get; private set; }
    private System.Random random = new System.Random();
    public CardFactory CardFactory { get; private set; } // Add CardFactory instance
    private bool weatherSystemInitialized = false;
    public Player Player1 { get; private set; }
    public Player Player2 { get; private set; }
    public ModifierFactory ModifierFactory { get; private set; } // Add ModifierFactory property

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

    public ICardDealingService CardDealingService => cardDealingService;
    public BattlefieldCombatHandler CombatHandler => combatHandler;
    public TurnManager TurnManager => turnManager;
    #endregion

    #region Unity Lifecycle
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
        // Ensure dependencies are met BEFORE initializing this component
        if (!initManager.IsComponentInitialized<GameReferences>() ||
            !initManager.IsComponentInitialized<GameMediator>()) {
            LogError("GameManager Initialization Error: Dependencies (GameReferences/GameMediator) not ready.", LogTag.Initialization);
            return;
        }

        Log("Initializing GameManager...", LogTag.Initialization);
        gameMediator = GameMediator.Instance;
        gameReferences = GameReferences.Instance;
        cardDealingService = new CardDealingService(gameMediator);

        // Initialize turn manager instance
        turnManager = TurnManager.Instance; // Ensure it exists
        if (turnManager == null) {
            LogError("TurnManager instance is null during GameManager Initialize!", LogTag.Initialization);
            // Attempt to find or create TurnManager again if necessary
            var tmGo = FindObjectOfType<TurnManager>()?.gameObject;
            if (tmGo == null) tmGo = new GameObject("TurnManager");
            turnManager = tmGo.GetComponent<TurnManager>();
             if (turnManager == null) turnManager = tmGo.AddComponent<TurnManager>();
             DontDestroyOnLoad(tmGo);
             LogWarning("Created missing TurnManager instance.", LogTag.Initialization);
        }
        turnManager.Initialize(this, gameMediator); // Explicitly initialize with references

        // Initialize systems
        InitializeWeatherSystem(); // Depends on Mediator
        InitializeCombatSystem();  // Depends on this (GameManager)
        InitializeActionsQueue();  // Depends on Mediator, CombatHandler
        InitializeModifierFactory(); // Depends on nothing external yet, but load assets
        CardFactory = new CardFactory(); // Instantiate CardFactory HERE, before InitializeGameSystem
        Log("CardFactory initialized", LogTag.Initialization | LogTag.Cards);
        InitializeGameSystem();    // Depends on many things (Players, Cards)

        // --- Moved initial game state setup to CompleteGameSetup ---

        // Initialize things that DON'T depend on populated battlefields
        SetupEndTurnButton();   // Requires GameReferences

        // --- GameInitialized notification is moved to CompleteGameSetup ---

        // *** Mark as initialized AFTER core setup but BEFORE CompleteGameSetup ***
        base.Initialize();
        Log("GameManager core initialized.", LogTag.Initialization);
    }

    // New method to be called AFTER GameUI is initialized
    public void CompleteGameSetup()
    {
        if (!IsInitialized)
        {
            LogError("Attempted to complete game setup before GameManager core was initialized.", LogTag.Initialization);
            return;
        }

        Log("Completing GameManager setup (post-UI)...", LogTag.Initialization);

        SetupInitialGameState();    // Deals cards
        PlaceInitialCreatures(); // Places creatures (Requires player battlefields from UI)

        // Initial weather setting
        if (WeatherSystem != null) {
            WeatherSystem.SetWeather(WeatherType.Clear);
             Log("Initial weather set to Clear.", LogTag.Initialization | LogTag.Effects);
        } else {
            LogWarning("WeatherSystem is null during CompleteGameSetup.", LogTag.Initialization | LogTag.Effects);
        }

        // Now notify that the game is fully ready
        gameMediator.NotifyGameInitialized();
        Log("Game Initialized notification sent.", LogTag.Initialization);

        // *** REMOVED base.Initialize(); from here ***
        Log("GameManager Initialization sequence completed.", LogTag.Initialization);
    }

    // --- MODIFIED: InitializeModifierFactory ---
    private void InitializeModifierFactory() {
        ModifierFactory = new ModifierFactory();

        // Load all ModifierData ScriptableObject assets
        var allModifierDataAssets = Resources.LoadAll<ModifierData>("Modifiers");
        if (allModifierDataAssets == null || allModifierDataAssets.Length == 0) {
            LogWarning("No ModifierData assets found in Resources/Modifiers folder.", LogTag.Initialization | LogTag.Effects);
        }

        // Convert ScriptableObjects to runtime ModifierDefinition
        List<ModifierDefinition> definitions = new List<ModifierDefinition>();
        foreach (var dataAsset in allModifierDataAssets) {
            if (dataAsset != null) {
                definitions.Add(dataAsset.ToModifierDefinition());
            }
        }

        // Initialize the factory with the runtime definitions
        ModifierFactory.Initialize(definitions);
        // Log moved inside Factory.Initialize
    }
    #endregion

    #region Methods
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

    // Ensure Player initialization happens early in InitializeGameSystem
    private void InitializeGameSystem() {
        InitializePlayers();
        InitializeCards();
    }

    private void InitializePlayers() {
        // Ensure players are only created once
        if (Player1 == null)
        {
            Player1 = new Player("Player 1"); // Pass name for clarity
            Player1.CardsToDraw = 5; // Set default draw count
            gameMediator.RegisterPlayer(Player1);
        }
        if (Player2 == null)
        {
            Player2 = new Player("Player 2");
            Player2.CardsToDraw = 5; // Set default draw count
            gameMediator.RegisterPlayer(Player2);
        }

        // Ensure opponents are set
        if (Player1 != null && Player2 != null)
        {
            Player1.Opponent = Player2;
            Player2.Opponent = Player1;
            Log("Players initialized and registered with Mediator.", LogTag.Initialization | LogTag.Players);
        } else {
            LogError("Failed to initialize players completely.", LogTag.Initialization | LogTag.Players);
        }
    }

    private void InitializeCards() {
        // Get cards from GameReferences instead of TestSetup
        var player1Cards = gameReferences.GetPlayer1DeckCards();
        var player2Cards = gameReferences.GetPlayer2DeckCards();

        cardDealingService.InitializeDecks(player1Cards, player2Cards);
        Log($"Decks initialized with {player1Cards.Count} cards for Player 1 and {player2Cards.Count} cards for Player 2",
            LogTag.Cards | LogTag.Initialization);
    }

    private void PlaceInitialCreatures() {
        if (!HasValidBattlefields()) {
            LogError("Cannot place creatures - battlefield not initialized or populated correctly", LogTag.Initialization);
            // Add more specific logs:
            if (Player1 == null) LogError("Player1 is null.", LogTag.Initialization);
            else if (Player1.Battlefield == null) LogError("Player1.Battlefield is null.", LogTag.Initialization);
            else if (!Player1.Battlefield.Any()) LogError("Player1.Battlefield is empty.", LogTag.Initialization);

            if (Player2 == null) LogError("Player2 is null.", LogTag.Initialization);
            else if (Player2.Battlefield == null) LogError("Player2.Battlefield is null.", LogTag.Initialization);
            else if (!Player2.Battlefield.Any()) LogError("Player2.Battlefield is empty.", LogTag.Initialization);
            return;
        }

        // Place creatures for each player from their own deck
        PlaceCreaturesForPlayerFromDeck(Player1, 3);
        PlaceCreaturesForPlayerFromDeck(Player2, 3);
    }

    private void PlaceCreaturesForPlayerFromDeck(IPlayer player, int count) {
        var emptySlots = player.Battlefield.Where(s => !s.IsOccupied()).ToList();

        if (emptySlots.Count == 0) {
            LogWarning($"No empty slots available for {(player.IsPlayer1() ? "Player 1" : "Player 2")}", LogTag.Initialization);
            return;
        }

        // Get a preview of the player's deck to find creatures
        var deckPreview = cardDealingService.GetDeckPreview(player);
        List<ICard> deckCreatures = deckPreview.Where(card => card is ICreature).ToList();

        // Place up to 'count' creatures, or as many as we have empty slots/creatures for
        int creaturesToPlace = Mathf.Min(count, emptySlots.Count, deckCreatures.Count);
         Log($"Attempting to place {creaturesToPlace} creatures for {(player.IsPlayer1() ? "Player 1" : "Player 2")}. (Deck Creatures: {deckCreatures.Count}, Empty Slots: {emptySlots.Count})", LogTag.Initialization);


        // Create a copy of emptySlots that we can modify
        List<BattlefieldSlot> availableSlots = new List<BattlefieldSlot>(emptySlots);
        List<ICard> creaturesPlaced = new List<ICard>();

        for (int i = 0; i < creaturesToPlace; i++) {
            // Get a random creature from the available ones
            if (deckCreatures.Count == 0) break; // No more creatures in deck
            int randomCreatureIndex = random.Next(deckCreatures.Count);
            var creatureCard = deckCreatures[randomCreatureIndex];

            // Remove to avoid duplicates
            deckCreatures.RemoveAt(randomCreatureIndex);

            // Get a random slot from the available slots
            if (availableSlots.Count == 0) break; // No more slots available
            int randomSlotIndex = random.Next(availableSlots.Count);
            var slot = availableSlots[randomSlotIndex];

            // Remove the selected slot from available slots to prevent duplicates
            availableSlots.RemoveAt(randomSlotIndex);

            // Track the creature we're placing so we can remove it from the deck later
            creaturesPlaced.Add(creatureCard);

            // Set the owner and add to battlefield
            ICreature creature = creatureCard as ICreature;
            if (creature == null) continue;

            creature.SetOwner(player);
            //player.AddToBattlefield(creature, slot); // AddToBattlefield creates controller, we need SummonAction

            // Use SummonCreatureAction to place the creature (triggers OnPlay effects)
            ActionsQueue.AddAction(new SummonCreatureAction(creature, player, slot, true)); // true because it's from deck

            // Get the slot's index for logging purposes
            int slotPosition = player.Battlefield.IndexOf(slot) + 1;

            Log($"Queued SummonAction for {creature.Name} to {(player.IsPlayer1() ? "Player 1" : "Player 2")}'s slot {slotPosition} from deck",
                LogTag.Creatures | LogTag.Initialization | LogTag.Actions);
        }

        // Remove the placed creatures from the deck
        if (creaturesPlaced.Count > 0) {
            RemoveCardsFromDeck(player, creaturesPlaced);
        }

        // Resolve the summon actions immediately
        ActionsQueue.ResolveActions();
        Log($"Resolved initial creature placement actions for {(player.IsPlayer1() ? "Player 1" : "Player 2")}", LogTag.Initialization | LogTag.Actions);
    }

    private void RemoveCardsFromDeck(IPlayer player, List<ICard> cardsToRemove) {
        if (player == null || cardsToRemove == null || cardsToRemove.Count == 0) return;

        // Access the Deck implementation to remove the cards
        if (cardDealingService != null) {
            int removedCount = 0;
            foreach (var card in cardsToRemove) {
                if (cardDealingService.RemoveCardFromDeck(player, card))
                {
                    removedCount++;
                }
            }

            Log($"Removed {removedCount} creatures from {(player.IsPlayer1() ? "Player 1" : "Player 2")}'s deck",
                LogTag.Cards | LogTag.Initialization);
        }
    }

    private bool HasValidBattlefields() {
        bool p1Valid = Player1 != null && Player1.Battlefield != null && Player1.Battlefield.Any();
        bool p2Valid = Player2 != null && Player2.Battlefield != null && Player2.Battlefield.Any();
        return p1Valid && p2Valid;
    }

    private void SetupInitialGameState() {
        // Deal initial hands with fewer cards to ensure deck has cards remaining
        cardDealingService.DealInitialHands(Player1, Player2, 5);
        Log("Initial cards dealt to players", LogTag.Initialization);
    }

    private void SetupEndTurnButton() {
        var endTurnButton = gameReferences.GetEndTurnButton();
        if (endTurnButton != null) {
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(OnEndTurnButtonClicked);
        } else {
            LogError("EndTurnButton reference is null in GameReferences.", LogTag.Initialization);
        }
    }

    private void OnEndTurnButtonClicked() {
        Log("End Turn button clicked, ending turn", LogTag.UI | LogTag.Actions | LogTag.Turns);

        // Use the TurnManager to end the current turn, which will:
        // 1. Trigger end-of-turn effects
        // 2. Process the actions queue
        // 3. Trigger start-of-turn effects for the next turn
        if (turnManager != null) {
             turnManager.EndTurn();
        } else {
            LogError("TurnManager is null. Cannot end turn.", LogTag.Turns);
        }
    }
    #endregion
}