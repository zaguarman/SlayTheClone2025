using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DebugLogger;
using static Enums;

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

    [ShowInInspector, BoxGroup("Systems")]
    public ModifierManager ModifierManager { get; private set; } // Add ModifierManager property

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

    protected override void OnDestroy() {
        ModifierManager?.Cleanup(); // Call the cleanup method we added earlier
        // ... other cleanup ...
        base.OnDestroy(); // If inheriting from MonoBehaviour/Singleton
    }

    public override void Initialize() {
        if (IsInitialized) return;

        var initManager = InitializationManager.Instance;
        if (!initManager.IsComponentInitialized<GameReferences>() ||
            !initManager.IsComponentInitialized<GameMediator>()) {
            // LogError instead of throwing exception to potentially recover
            LogError("Required dependencies (GameReferences or GameMediator) not initialized for GameManager", LogTag.Initialization);
            return;
        }

        gameMediator = GameMediator.Instance;
        gameReferences = GameReferences.Instance;
        cardDealingService = new CardDealingService(gameMediator);

        // Initialize turn manager
        turnManager = TurnManager.Instance; // Assuming TurnManager is a Singleton or handled elsewhere

        // Initialize Modifier System FIRST
        InitializeModifierSystem(); // Add this call

        // Then other systems
        InitializeWeatherSystem();
        InitializeCombatSystem();
        InitializeActionsQueue();
        InitializeGameSystem(); // This initializes players, cards, etc.

        base.Initialize(); // Mark GameManager as initialized

        // Set initial weather after all systems are ready
        if (WeatherSystem != null) {
            WeatherSystem.SetWeather(WeatherType.Clear);
             Log("Initial weather set to Clear", LogTag.Initialization | LogTag.Effects);
        }
         Log("GameManager Initialization complete.", LogTag.Initialization);
    }
    #endregion

    #region Methods
    private void InitializeModifierSystem()
    {
        if (ModifierManager == null)
        {
            IModifierFactory factory = new SimpleModifierFactory();
            ModifierManager = new ModifierManager(gameMediator, factory);
            Log("Modifier system initialized", LogTag.Initialization | LogTag.Effects);
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
        var emptySlots = player.Battlefield.Where(s => !s.IsOccupied()).ToList();
        if (emptySlots.Count == 0) {
             LogWarning($"No empty slots available for {(player.IsPlayer1() ? "Player 1" : "Player 2")} during initial placement", LogTag.Initialization);
            return;
        }

        var deckPreview = cardDealingService.GetDeckPreview(player);
        List<ICard> deckCreatures = deckPreview.Where(card => card is ICreature).ToList();
        int creaturesToPlace = Mathf.Min(count, emptySlots.Count, deckCreatures.Count);
        List<BattlefieldSlot> availableSlots = new List<BattlefieldSlot>(emptySlots);
        List<ICard> creaturesPlaced = new List<ICard>();

        for (int i = 0; i < creaturesToPlace; i++) {
            int randomCreatureIndex = random.Next(deckCreatures.Count);
            var creatureCard = deckCreatures[randomCreatureIndex];
            deckCreatures.RemoveAt(randomCreatureIndex);

            int randomSlotIndex = random.Next(availableSlots.Count);
            var slot = availableSlots[randomSlotIndex];
            availableSlots.RemoveAt(randomSlotIndex);

            creaturesPlaced.Add(creatureCard);

            ICreature creature = creatureCard as ICreature;
            if (creature == null) continue;

            creature.SetOwner(player);

            // --- CRITICAL CHANGE: Use Summon Action to place and register ---
            // This ensures the creature is registered with the ModifierManager
            // Use the target slot provided by placement logic
            // Use fromDeck = true
            var summonAction = new SummonCreatureAction(creature, player, slot, true);
            // Execute immediately for initial setup? Or queue? Queuing might be safer.
            // ActionsQueue.AddAction(summonAction); // Add to queue
            summonAction.Execute(); // Execute directly for initial placement simplicity

            int slotPosition = player.Battlefield.IndexOf(slot) + 1;
            // Log within SummonCreatureAction will now cover this
            // Log($"Placed {creature.Name} into slot {slotPosition} for {(player.IsPlayer1() ? "Player 1" : "Player 2")} via initial setup.", LogTag.Creatures | LogTag.Initialization);
        }

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

    // Method to create and add the Electric Eel card to Player 1's hand
    public void AddElectricEelCardToPlayer1() {
        if (Player1 == null) return;

        // Create the Electric Eel creature with chained damage effect
        var electricEel = ScriptableObject.CreateInstance<CreatureData>();
        electricEel.cardName = "Electric Eel";
        electricEel.description = "When this creature attacks, it also deals damage to 2 additional targets in a chain.";
        electricEel.attack = 3;
        electricEel.health = 4;

        var effect = new CardEffect {
            effectType = EffectType.Continuous,
            trigger = EffectTrigger.OnPlay,
            actions = new List<EffectAction> {
                new EffectAction {
                    actionType = ActionType.Damage,
                    value = 3, // Same damage as the attack value
                    targetType = TargetType.EnemyCreatures,
                    targetModifier = TargetModifier.Chained
                }
            }
        };

        electricEel.effects.Add(effect);

        // Create the card and add to player's hand
        var creature = CardFactory.CreateCard(electricEel);
        Player1.AddToHand(creature);

        Log("Added Electric Eel card to Player 1's hand", LogTag.Cards | LogTag.Initialization);
    }
    #endregion
}