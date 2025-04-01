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
    private TurnManager turnManager;

    private GameMediator gameMediator;
    private GameReferences gameReferences;
    public ICardDealingService cardDealingService { get; private set; }
    private System.Random random = new System.Random();

    public BattlefieldCombatHandler CombatHandler => combatHandler;
    public TurnManager TurnManager => turnManager;

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

        // Initialize turn manager
        turnManager = TurnManager.Instance;

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
        // Get the empty slots from the player's battlefield
        var emptySlots = player.Battlefield.Where(s => !s.IsOccupied()).ToList();

        // If there are no empty slots, we can't place any creatures
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

    private void PlaceCreaturesForPlayer(IPlayer player, List<CreatureData> availableCreatures, int count) {
        // Get the empty slots from the player's battlefield
        var emptySlots = player.Battlefield.Where(s => !s.IsOccupied()).ToList();

        // Place up to 'count' creatures, or as many as we have empty slots for
        int creaturesToPlace = Mathf.Min(count, emptySlots.Count, availableCreatures.Count);

        // Create a copy of emptySlots that we can modify
        List<BattlefieldSlot> availableSlots = new List<BattlefieldSlot>(emptySlots);

        for (int i = 0; i < creaturesToPlace; i++) {
            // Get a random creature from the available ones
            int randomCreatureIndex = random.Next(availableCreatures.Count);
            var creatureData = availableCreatures[randomCreatureIndex];

            // Get a random slot from the available slots
            int randomSlotIndex = random.Next(availableSlots.Count);
            var slot = availableSlots[randomSlotIndex];

            // Remove the selected slot from available slots to prevent duplicates
            availableSlots.RemoveAt(randomSlotIndex);

            // Create the creature
            var creature = CardFactory.CreateCard(creatureData) as ICreature;
            if (creature == null) continue;

            // Set the owner and add to battlefield
            creature.SetOwner(player);
            player.AddToBattlefield(creature, slot);

            // Get the slot's index for logging purposes
            int slotPosition = player.Battlefield.IndexOf(slot) + 1;

            Log($"Added {creature.Name} to {(player.IsPlayer1() ? "Player 1" : "Player 2")}'s battlefield in slot {slotPosition}",
                LogTag.Creatures | LogTag.Initialization);
        }
    }

    private void SetupInitialGameState() {
        // Deal initial hands with fewer cards to ensure deck has cards remaining
        cardDealingService.DealInitialHands(Player1, Player2, 5);
        Log("Initial cards dealt to players", LogTag.Initialization);
    }

    private void SetupResolveButton() {
        var resolveButton = gameReferences.GetResolveActionsButton();
        if (resolveButton != null) {
            resolveButton.onClick.RemoveAllListeners();
            resolveButton.onClick.AddListener(OnResolveButtonClicked);
        }
    }

    private void OnResolveButtonClicked() {
        Log("Resolve button clicked, ending turn", LogTag.UI | LogTag.Actions | LogTag.Turns);

        // Use the TurnManager to end the current turn, which will:
        // 1. Trigger end-of-turn effects
        // 2. Process the actions queue
        // 3. Trigger start-of-turn effects for the next turn
        turnManager.EndTurn();
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