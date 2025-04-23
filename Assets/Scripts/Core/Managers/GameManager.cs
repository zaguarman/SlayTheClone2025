using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DebugLogger;
using static Enums;

public interface IGameManager : IInitializable {
    // Properties for accessing core systems via interfaces
    IActionsQueue ActionsQueue { get; }
    IWeatherSystem WeatherSystem { get; }
    ITurnManager TurnManager { get; }
    ICardDealingService CardDealingService { get; }
    IModifierManager ModifierManager { get; }
    IBattlefieldCombatHandler CombatHandler { get; }
    // Expose players via IPlayer interface
    IPlayer Player1 { get; }
    IPlayer Player2 { get; }

    // Add any essential methods needed publicly
    void SetCardsToDraw(IPlayer player, int count);
    void DiscardHand(IPlayer player);
    void DiscardAllHands();
    void DrawCardsForPlayer(IPlayer player, int count);
    void DrawCardForPlayer(IPlayer player);
}

public interface ITurnManager {
    int TurnNumber { get; }
    void EndTurn();
}

public interface IModifierManager {
    // Add essential public methods needed by other systems
    void RegisterCreature(Creature creature);
    void UnregisterCreature(Creature creature);
    void ApplyModifier(object target, IModifier modifier);
    void RemoveModifier(object target, IModifier modifier);
    IEnumerable<IModifier> GetActiveModifiersFor(Creature creature);
    bool HasStatusEffect(Creature creature, StatusEffectType statusType);
    bool AreActionsPrevented(Creature creature);
    void RecalculateStats(Creature creature);
    void ProcessEndOfTurn(int endedTurnNumber);
    void Cleanup();

    // Add missing methods needed by other classes
    bool HasModifier(Creature creature, Predicate<IModifier> predicate);

    // Expose the ModifierFactory for creating modifiers
    IModifierFactory ModifierFactory { get; }
}

public interface IActionsQueue {
    void AddAction(IGameAction action);
    void ResolveActions();
    int GetPendingActionsCount();
    IReadOnlyCollection<IGameAction> GetPendingActions();
    bool IsEffectProcessed(string sourceId, EffectTrigger trigger);
    void MarkEffectProcessed(string sourceId, EffectTrigger trigger);
    bool HasActiveAction(string creatureId);
    IGameAction GetActiveAction(string creatureId);
    void Cleanup();
    // Events
    UnityEngine.Events.UnityEvent OnActionsQueued { get; }
    UnityEngine.Events.UnityEvent OnActionsResolved { get; }
}

public interface IBattlefieldCombatHandler {
    void HandleCreatureCombat(CardController attackingCard, ITarget targetSlot);
    void ResetAttackingCreatures();
    bool HasCreatureAttacked(ITarget creature);
    BattlefieldSlot GetTargetedSlot(ITarget attacker);
}

public class GameManager : InitializableComponent, IGameManager {
    #region Singleton (Modified)
    // Keep singleton for access, but initialization will be controlled externally
    private static GameManager instance;
    public static GameManager Instance // Keep static Instance for now
    {
        get {
            // Don't auto-create, just return if exists
            if (instance == null && Application.isPlaying) {
                // LogError instead of creating, GameBootstrap should handle creation/finding
                Debug.LogError($"GameManager instance accessed before it was initialized or assigned!");
            }
            return instance;
        }
    }
    #endregion

    #region Fields & Properties
    // System References (using Interface Types)
    [ShowInInspector]
    public IActionsQueue ActionsQueue { get; private set; }
    public IWeatherSystem WeatherSystem { get; private set; }
    private IBattlefieldCombatHandler combatHandler;
    private ITurnManager turnManager;

    // Dependencies (to be injected)
    private IGameMediator gameMediator;
    private IGameReferences gameReferences;
    public ICardDealingService cardDealingService; // Made public to fix access issues
    private System.Random random = new System.Random();
    private bool weatherSystemInitialized = false;
    public Player Player1 { get; private set; } // Keep concrete Player for internal use
    public Player Player2 { get; private set; } // Keep concrete Player for internal use

    // Interface Property Implementations (Exposing Players as IPlayer)
    IPlayer IGameManager.Player1 => Player1;
    IPlayer IGameManager.Player2 => Player2;

    [ShowInInspector, BoxGroup("Systems")]
    public IModifierManager ModifierManager { get; private set; }

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

    // Interface implementations
    public ICardDealingService CardDealingService => cardDealingService;
    public IBattlefieldCombatHandler CombatHandler => combatHandler;
    public ITurnManager TurnManager => turnManager;
    #endregion

    #region Unity Lifecycle
    protected override void Awake() {
        // Base awake does nothing specific here, but good practice to call
        base.Awake();

        // Singleton Registration Logic
        if (instance == null) {
            instance = this;
            // DontDestroyOnLoad(gameObject); // Let GameBootstrap manage persistence
        } else if (instance != this) {
            LogWarning($"Duplicate GameManager instance found on {gameObject.name}. Destroying self.", LogTag.Initialization);
            Destroy(gameObject);
            return;
        }
        // DO NOT Initialize here. GameBootstrap will call Initialize externally
    }

    protected override void OnDestroy() {
        // Cleanup systems
        ModifierManager?.Cleanup();
        ActionsQueue?.Cleanup();
        // Add cleanup for other systems if they need it

        if (instance == this) {
            instance = null; // Clear static reference if this was the instance
        }
        base.OnDestroy();
    }

    #region Initialization (Refactored for Dependency Injection)

    // NEW Initialize method accepting dependencies
    public void Initialize(
        IGameMediator mediator,
        IGameReferences references,
        ITurnManager turnManager,
        IModifierFactory modFactory) {
        if (IsInitialized) {
            LogWarning("GameManager Initialize called but already initialized.", LogTag.Initialization);
            return;
        }

        // --- 1. Store Injected Dependencies ---
        Log("GameManager Initializing with injected dependencies...", LogTag.Initialization);
        gameMediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        gameReferences = references ?? throw new ArgumentNullException(nameof(references));
        this.turnManager = turnManager ?? throw new ArgumentNullException(nameof(turnManager)); // Store injected TurnManager
        IModifierFactory modifierFactory = modFactory ?? throw new ArgumentNullException(nameof(modFactory)); // Store injected factory

        if (!gameReferences.AreReferencesValid()) {
            LogError("GameReferences are invalid during GameManager Initialize!", LogTag.Initialization);
            // Optionally handle this more gracefully, e.g., disable the component
            enabled = false;
            return;
        }

        // --- 2. Initialize Internal Systems (using stored dependencies) ---
        cardDealingService = new CardDealingService(gameMediator); // Pass mediator
        combatHandler = new BattlefieldCombatHandler(this); // Pass IGameManager (this)
        ModifierManager = new ModifierManager(gameMediator, modifierFactory); // Pass mediator and factory
        WeatherSystem = new WeatherSystem(gameMediator); // Pass mediator

        // --- Create Action Executors ---
        var defaultExecutor = new DefaultActionExecutor();
        var executors = new Dictionary<Type, IActionExecutor>
        {
            // Previous executors
            [typeof(DrawCardsAction)] = new DrawCardsActionExecutor(),
            [typeof(ChangeWeatherAction)] = new ChangeWeatherActionExecutor(),
            [typeof(SummonCreatureAction)] = new SummonCreatureActionExecutor(),
            [typeof(PlayCardAction)] = new PlayCardActionExecutor(),
            [typeof(PlaySpellAction)] = new PlaySpellActionExecutor(),
            [typeof(BattlefieldCombatAction)] = new BattlefieldCombatActionExecutor(),
            [typeof(ModifyAction)] = new ModifyActionExecutor(),
            [typeof(ApplyStatusEffectAction)] = new ApplyStatusEffectActionExecutor(),
            [typeof(MoveCreatureAction)] = new MoveCreatureActionExecutor(),
            [typeof(DamageCreatureAction)] = new DamageCreatureActionExecutor()

            // Newly added executors will be registered after they're compiled
            // [typeof(ModifyArmorAction)] = new ModifyArmorActionExecutor(),
            // [typeof(DamagePlayerAction)] = new DamagePlayerActionExecutor(),
            // [typeof(DiscardHandAction)] = new DiscardHandActionExecutor(),
            // [typeof(HealCreatureAction)] = new HealCreatureActionExecutor(),
            // [typeof(HealPlayerAction)] = new HealPlayerActionExecutor(),
            // [typeof(SwapCreaturesAction)] = new SwapCreaturesActionExecutor()
        };

        // Initialize ActionsQueue with all dependencies and executors
        ActionsQueue = new ActionsQueue(
            gameMediator,
            combatHandler,
            WeatherSystem,
            cardDealingService,
            gameReferences,
            ModifierManager,
            executors,
            defaultExecutor
        );

        // --- 3. Initialize Game State ---
        InitializePlayers(); // Uses gameMediator
        InitializeCards(); // Uses gameReferences, CardDealingService
        InitializePlayerDependencies(); // Inject dependencies into Players

        // --- 4. Mark as Initialized (MUST be before any calls that might rely on IsInitialized) ---
        base.Initialize(); // Sets IsInitialized = true

        // --- 5. Final Setup ---
        // Setup initial game state (like placing creatures) *after* main initialization
        // Using a Coroutine to ensure UI might be ready
        StartCoroutine(CompleteGameInitialization());

        // Set initial weather after all systems are ready
        WeatherSystem.SetWeather(WeatherType.Clear);
        Log("Initial weather set to Clear", LogTag.Initialization | LogTag.Effects);

        Log("GameManager Initialization complete.", LogTag.Initialization);
    }

    // Overload for convenience if TurnManager or ModFactory are provided elsewhere (e.g., singletons for now)
    // Mark as obsolete to encourage using the main injection method
    [Obsolete("Use Initialize with all dependencies injected.")]
    public void Initialize(IGameMediator mediator, IGameReferences references) {
        if (IsInitialized) return;
        LogWarning("Using obsolete GameManager Initialize method. Dependencies should be injected.", LogTag.Initialization);
        // Provide default dependencies (using singletons temporarily)
        var turnManager = FindObjectOfType<TurnManager>(); // Find TurnManager in scene
        if (turnManager == null) {
            LogError("Cannot find TurnManager in scene. Initialization failed.", LogTag.Initialization);
            return;
        }
        IModifierFactory modifierFactory = new SimpleModifierFactory(); // Create default factory
        Initialize(mediator, references, turnManager, modifierFactory);
    }

    // Hide the base Initialize method with a new implementation
    // This is safer than using Obsolete with error=true which can cause compiler errors
    // when the base class method is called through the interface
    public new void Initialize() {
        LogError("GameManager.Initialize() without parameters should not be called. Use Initialize with dependencies.", LogTag.Initialization);
        // This method should not be called directly, but we don't want to break the interface
        // If called through IInitializable, provide a fallback implementation
        if (!IsInitialized) {
            // Fallback to using singletons and direct find
            var mediator = GameMediator.Instance;
            var references = FindObjectOfType<GameReferences>(); // Direct find since GameReferences is no longer a singleton
            if (mediator != null && references != null) {
                // Use the full Initialize method with dependencies
                var tm = FindObjectOfType<TurnManager>();
                if (tm != null) {
                    IModifierFactory mf = new SimpleModifierFactory();
                    Initialize(mediator, references, tm, mf);
                } else {
                    LogError("Cannot find TurnManager in scene. Initialization failed.", LogTag.Initialization);
                }
            } else {
                LogError("Cannot initialize GameManager - dependencies not available", LogTag.Initialization);
            }
        }
    }

    #endregion
    #endregion

    #region Methods
    private void InitializeModifierSystem() {
        if (ModifierManager == null) {
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
        // Make sure WeatherSystem is initialized first if needed
        if (WeatherSystem == null) {
            InitializeWeatherSystem();
        }

        // Create executors
        var defaultExecutor = new DefaultActionExecutor();
        var executors = new Dictionary<Type, IActionExecutor>
        {
            // Previous executors
            [typeof(DrawCardsAction)] = new DrawCardsActionExecutor(),
            [typeof(ChangeWeatherAction)] = new ChangeWeatherActionExecutor(),
            [typeof(SummonCreatureAction)] = new SummonCreatureActionExecutor(),
            [typeof(PlayCardAction)] = new PlayCardActionExecutor(),
            [typeof(PlaySpellAction)] = new PlaySpellActionExecutor(),
            [typeof(BattlefieldCombatAction)] = new BattlefieldCombatActionExecutor(),
            [typeof(ModifyAction)] = new ModifyActionExecutor(),
            [typeof(ApplyStatusEffectAction)] = new ApplyStatusEffectActionExecutor(),
            [typeof(MoveCreatureAction)] = new MoveCreatureActionExecutor(),
            [typeof(DamageCreatureAction)] = new DamageCreatureActionExecutor()

            // Newly added executors will be registered after they're compiled
            // [typeof(ModifyArmorAction)] = new ModifyArmorActionExecutor(),
            // [typeof(DamagePlayerAction)] = new DamagePlayerActionExecutor(),
            // [typeof(DiscardHandAction)] = new DiscardHandActionExecutor(),
            // [typeof(HealCreatureAction)] = new HealCreatureActionExecutor(),
            // [typeof(HealPlayerAction)] = new HealPlayerActionExecutor(),
            // [typeof(SwapCreaturesAction)] = new SwapCreaturesActionExecutor()
        };

        // Initialize ActionsQueue with all dependencies and executors
        ActionsQueue = new ActionsQueue(
            gameMediator,
            combatHandler,
            WeatherSystem,
            cardDealingService,
            gameReferences,
            ModifierManager,
            executors,
            defaultExecutor
        );

        Log("Actions queue initialized with Strategy Executors", LogTag.Initialization);
    }

    private void InitializeGameSystem() {
        InitializePlayers();
        InitializeCards();

        if (GameUI.Instance != null) {
            if (GameUI.Instance.IsInitialized) {
                StartCoroutine(CompleteGameInitialization());
            } else {
                // Use UnityEvent.AddListener which takes an Action (no parameters)
                // This requires a wrapper method that calls StartCoroutine
                GameUI.Instance.onInitialized.AddListener(OnGameUIInitialized);
            }
        } else {
            LogWarning("GameUI.Instance is null in InitializeGameSystem", LogTag.Initialization);
            // Proceed anyway
            StartCoroutine(CompleteGameInitialization());
        }
    }

    private void OnGameUIInitialized() {
        StartCoroutine(CompleteGameInitialization());
    }

    // Coroutine for setup steps that might need UI or happen after initial init
    private IEnumerator CompleteGameInitialization() {
        // Wait a frame to allow UI potentially initialize if needed
        yield return null;

        // Wait until the GameUI signals it's ready (optional, but safer)
        // This assumes GameUI.onInitialized event exists and is fired
        if (GameUI.Instance != null && !GameUI.Instance.IsInitialized) {
            Log("GameManager waiting for GameUI initialization...", LogTag.Initialization);
            yield return new WaitUntil(() => GameUI.Instance.IsInitialized);
            Log("GameUI initialization detected by GameManager.", LogTag.Initialization);
        }

        // Now perform setup that might depend on UI or full initialization
        SetupInitialGameState(); // Deal hands
        PlaceInitialCreatures(); // Place creatures (uses SummonAction -> affects UI/Modifiers)
        SetupResolveButton(); // Setup UI button listener

        gameMediator.NotifyGameInitialized(); // Notify game is fully ready
        Log("GameManager final setup complete.", LogTag.Initialization);
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

    private void InitializePlayerDependencies() {
         // Pass dependencies to players *after* GameManager has them
         Player1?.Initialize(gameMediator, gameReferences, cardDealingService);
         Player2?.Initialize(gameMediator, gameReferences, cardDealingService);
         Log("Injected dependencies into Player instances.", LogTag.Initialization | LogTag.Players);
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
        PlaceCreaturesForPlayerFromDeck(Player1, 3);
        PlaceCreaturesForPlayerFromDeck(Player2, 3);
        // Resolve actions immediately after placing initial creatures
        // This ensures OnPlay effects trigger and creatures are registered properly
        ActionsQueue.ResolveActions();
        Log("Initial creatures placed and actions resolved.", LogTag.Initialization | LogTag.Creatures);
    }

    private void PlaceCreaturesForPlayerFromDeck(IPlayer player, int count) {
        var emptySlots = player.Battlefield.Where(s => !s.IsOccupied()).ToList();
        if (emptySlots.Count == 0) {
            LogWarning($"No empty slots available for {(player.IsPlayer1() ? "Player 1" : "Player 2")} during initial placement", LogTag.Initialization);
            return;
        }

        var deckPreview = CardDealingService.GetDeckPreview(player);
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

            // Queue the Summon Action
            var summonAction = new SummonCreatureAction(creature, player, slot, true); // fromDeck = true
            ActionsQueue.AddAction(summonAction); // Queue the action

            // Log that the action was QUEUED, not executed yet.
            Log($"Queued initial placement action for {creature.Name} into slot {player.Battlefield.IndexOf(slot) + 1} for {(player.IsPlayer1() ? "Player 1" : "Player 2")}.", LogTag.Creatures | LogTag.Initialization | LogTag.Actions);
        }

        if (creaturesPlaced.Count > 0) {
            RemoveCardsFromDeck(player, creaturesPlaced); // Remove the cards from the deck data
        }
    }

    private void RemoveCardsFromDeck(IPlayer player, List<ICard> cardsToRemove) {
        if (player == null || cardsToRemove == null || cardsToRemove.Count == 0) return;
        if (CardDealingService != null) {
            foreach (var card in cardsToRemove) {
                CardDealingService.RemoveCardFromDeck(player, card);
            }
            Log($"Removed {cardsToRemove.Count} creatures from {(player.IsPlayer1() ? "Player 1" : "Player 2")}'s deck", LogTag.Cards | LogTag.Initialization);
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
        Log("End Turn button clicked", LogTag.UI | LogTag.Actions | LogTag.Turns);
        turnManager.EndTurn(); // Use the stored ITurnManager
    }

    // Methods to handle cards to draw
    public void SetCardsToDraw(IPlayer player, int count) {
        if (player == null) return;
        if (player is Player p) // Need concrete Player to set property
        {
            p.CardsToDraw = count;
            Log($"Set cards to draw for {(p.IsPlayer1() ? "Player 1" : "Player 2")} to {count}", LogTag.Players | LogTag.Cards);
        }
    }

    public void DiscardHand(IPlayer player) {
        if (player == null) return;
        ActionsQueue?.AddAction(new DiscardHandAction(player));
        Log($"Added discard hand action for {(player.IsPlayer1() ? "Player 1" : "Player 2")}", LogTag.Actions | LogTag.Cards);
    }

    public void DiscardAllHands() {
        if (Player1 != null) ActionsQueue?.AddAction(new DiscardHandAction(Player1));
        if (Player2 != null) ActionsQueue?.AddAction(new DiscardHandAction(Player2));
        Log("Added discard hand actions for all players", LogTag.Actions | LogTag.Cards);
    }

    public void DrawCardsForPlayer(IPlayer player, int count = 1) {
        if (player == null) return;
        ActionsQueue?.AddAction(new DrawCardsAction(player, count));
        Log($"Added draw cards action for {(player.IsPlayer1() ? "Player 1" : "Player 2")} to draw {count} cards", LogTag.Actions | LogTag.Cards);
    }

    public void DrawCardForPlayer(IPlayer player) {
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