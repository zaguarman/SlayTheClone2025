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
        turnManager.Initialize(this, gameMediator); // Explicitly initialize with references

        // Initialize systems
        InitializeWeatherSystem(); // Depends on Mediator
        InitializeCombatSystem();  // Depends on this (GameManager)
        InitializeActionsQueue();  // Depends on Mediator, CombatHandler
        InitializeModifierFactory(); // Depends on nothing external yet, but load assets
        InitializeGameSystem();    // Depends on many things (Players, Cards)

        base.Initialize();
        Log("GameManager core initialized.", LogTag.Initialization);

        // Now perform game setup tasks that might depend on core systems being ready
        SetupInitialGameState();    // Deals cards
        PlaceInitialCreatures(); // Places creatures (Requires player battlefields)
        SetupEndTurnButton();   // Requires GameReferences

        gameMediator.NotifyGameInitialized(); // Notify AFTER GameManager setup is complete
        Log("Game Initialized notification sent.", LogTag.Initialization);
        Log("GameManager Initialization sequence completed.", LogTag.Initialization);

        // Initial weather setting (can stay here or move after game fully initialized)
        if (WeatherSystem != null) {
            WeatherSystem.SetWeather(WeatherType.Clear);
        }
    }

    private void InitializeModifierFactory() {
        ModifierFactory = new ModifierFactory();
        // Load all ModifierData assets (adjust path as needed)
        var allModifierDataAssets = Resources.LoadAll<ModifierData>("Modifiers");
        if (allModifierDataAssets == null || allModifierDataAssets.Length == 0) {
            LogWarning("No ModifierData assets found in Resources/Modifiers folder.", LogTag.Initialization | LogTag.Effects);
        }
        ModifierFactory.Initialize(allModifierDataAssets);
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
        Player1 = new Player("Player 1"); // Pass name for clarity
        Player2 = new Player("Player 2");
        Player1.Opponent = Player2;
        Player2.Opponent = Player1;

        // Set default cards to draw (can be adjusted later)
        Player1.CardsToDraw = 5;
        Player2.CardsToDraw = 5;

        gameMediator.RegisterPlayer(Player1);
        gameMediator.RegisterPlayer(Player2);
        Log("Players initialized and registered with Mediator.", LogTag.Initialization | LogTag.Players);
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
            LogError("Cannot place creatures - battlefield not initialized", LogTag.Initialization);
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

        // Create a copy of emptySlots that we can modify
        List<BattlefieldSlot> availableSlots = new List<BattlefieldSlot>(emptySlots);
        List<ICard> creaturesPlaced = new List<ICard>();

        for (int i = 0; i < creaturesToPlace; i++) {
            // Get a random creature from the available ones
            int randomCreatureIndex = random.Next(deckCreatures.Count);
            var creatureCard = deckCreatures[randomCreatureIndex];

            // Remove to avoid duplicates
            deckCreatures.RemoveAt(randomCreatureIndex);

            // Get a random slot from the available slots
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
            player.AddToBattlefield(creature, slot);

            // Get the slot's index for logging purposes
            int slotPosition = player.Battlefield.IndexOf(slot) + 1;

            Log($"Added {creature.Name} to {(player.IsPlayer1() ? "Player 1" : "Player 2")}'s battlefield in slot {slotPosition} from deck",
                LogTag.Creatures | LogTag.Initialization);
        }

        // Remove the placed creatures from the deck
        if (creaturesPlaced.Count > 0) {
            RemoveCardsFromDeck(player, creaturesPlaced);
        }
    }

    private void RemoveCardsFromDeck(IPlayer player, List<ICard> cardsToRemove) {
        if (player == null || cardsToRemove == null || cardsToRemove.Count == 0) return;

        // Access the Deck implementation to remove the cards
        if (cardDealingService != null) {
            foreach (var card in cardsToRemove) {
                cardDealingService.RemoveCardFromDeck(player, card);
            }

            Log($"Removed {cardsToRemove.Count} creatures from {(player.IsPlayer1() ? "Player 1" : "Player 2")}'s deck",
                LogTag.Cards | LogTag.Initialization);
        }
    }

    private bool HasValidBattlefields() {
        return Player1?.Battlefield != null && Player1.Battlefield.Any() &&
               Player2?.Battlefield != null && Player2.Battlefield.Any();
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
        }
    }

    private void OnEndTurnButtonClicked() {
        Log("End Turn button clicked, ending turn", LogTag.UI | LogTag.Actions | LogTag.Turns);

        // Use the TurnManager to end the current turn, which will:
        // 1. Trigger end-of-turn effects
        // 2. Process the actions queue
        // 3. Trigger start-of-turn effects for the next turn
        turnManager.EndTurn();
    }
    #endregion
}