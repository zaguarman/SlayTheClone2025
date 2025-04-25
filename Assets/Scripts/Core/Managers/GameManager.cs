using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DebugLogger;
using static Enums;

public interface IGameManager {
    // Add IsInitialized property from former IInitializable interface
    bool IsInitialized { get; }
    void Initialize(IGameMediator mediator, IGameReferences references, ITurnManager turnManager, IModifierFactory modFactory);
    // Properties for accessing core systems via interfaces
    IActionsQueue ActionsQueue { get; }
    IWeatherSystem WeatherSystem { get; }
    ITurnManager TurnManager { get; }
    ICardDealingService CardDealingService { get; }
    IModifierManager ModifierManager { get; }
    IBattlefieldCombatHandler CombatHandler { get; }
    // Expose GameReferences for dependency access
    IGameReferences GameReferences { get; }
    // Expose players via IPlayer interface
    IPlayer Player1 { get; }
    IPlayer Player2 { get; }

    // Add any essential methods needed publicly
    void SetCardsToDraw(IPlayer player, int count);
    void DiscardHand(IPlayer player);
    void DiscardAllHands();
    void DrawCardsForPlayer(IPlayer player, int count);
    void DrawCardForPlayer(IPlayer player);
    int TurnNumber { get; }
    void EndTurn();
}

public interface ITurnManager {
    bool IsInitialized { get; }
    int TurnNumber { get; }
    void EndTurn();
}

public interface IModifierManager {
    // Add essential public methods needed by other systems
    void RegisterCreature(Creature creature); // Register still takes concrete for simplicity if needed internally
    void UnregisterCreature(ICreature creature); // Changed from Creature to ICreature
    void ApplyModifier(object target, IModifier modifier);
    void RemoveModifier(object target, IModifier modifier);
    IEnumerable<IModifier> GetActiveModifiersFor(Creature creature);
    bool HasStatusEffect(Creature creature, StatusEffectType statusType);
    bool AreActionsPrevented(Creature creature);
    void RecalculateStats(Creature creature);
    void ProcessEndOfTurn(int endedTurnNumber, IActionsQueue actionsQueue);
    void ProcessStartOfTurn(int startingTurnNumber, IActionsQueue actionsQueue);
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

public class GameManager : MonoBehaviour, IGameManager {
    // Implement IsInitialized property from IGameManager interface
    public bool IsInitialized { get; private set; }

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
    public Player Player1 { get; private set; } // Keep concrete Player for internal use
    public Player Player2 { get; private set; } // Keep concrete Player for internal use

    // Interface Property Implementations (Exposing Players as IPlayer)
    IPlayer IGameManager.Player1 => Player1;
    IPlayer IGameManager.Player2 => Player2;

    [ShowInInspector, BoxGroup("Systems")]
    public IModifierManager ModifierManager { get; private set; }

    [ShowInInspector, BoxGroup("Systems")]
    public IModifierFactory ModifierFactory { get; private set; }

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
    public IGameReferences GameReferences => gameReferences;

    // Implement ITurnManager methods through delegation
    public int TurnNumber => turnManager.TurnNumber;
    public void EndTurn() => turnManager.EndTurn();
    #endregion



    #region Initialization

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

        // Create WeatherSystem first (needed for ActionsQueue)
        WeatherSystem = new WeatherSystem(gameMediator);

        // Create executors for ActionsQueue
        var executors = new Dictionary<Type, IActionExecutor> {
            // Assign specific executors for each action type
            [typeof(DrawCardsAction)] = new DrawCardsActionExecutor(),
            [typeof(ChangeWeatherAction)] = new ChangeWeatherActionExecutor(),
            [typeof(SummonCreatureAction)] = new SummonCreatureActionExecutor(),
            [typeof(PlayCardAction)] = new PlayCardActionExecutor(),
            [typeof(PlaySpellAction)] = new PlaySpellActionExecutor(),
            [typeof(BattlefieldCombatAction)] = new BattlefieldCombatActionExecutor(),
            [typeof(ModifyAction)] = new ModifyActionExecutor(),
            [typeof(ApplyStatusEffectAction)] = new ApplyStatusEffectActionExecutor(),
            [typeof(MoveCreatureAction)] = new MoveCreatureActionExecutor(),
            [typeof(DamageCreatureAction)] = new DamageCreatureActionExecutor(),
            [typeof(ModifyArmorAction)] = new ModifyArmorActionExecutor(),
            [typeof(DamagePlayerAction)] = new DamagePlayerActionExecutor(),
            [typeof(DiscardHandAction)] = new DiscardHandActionExecutor(),
            [typeof(HealCreatureAction)] = new HealCreatureActionExecutor(),
            [typeof(HealPlayerAction)] = new HealPlayerActionExecutor(),
            [typeof(SwapCreaturesAction)] = new SwapCreaturesActionExecutor()
            // Add any other specific action types and their executors here
        };

        // Create ModifierManager first (no longer needs ActionsQueue)
        ModifierFactory = modifierFactory;
        ModifierManager = new ModifierManager(gameMediator, modifierFactory, turnManager);

        // Now create ActionsQueue with the ModifierManager
        ActionsQueue = new ActionsQueue(
            gameMediator,
            combatHandler,
            WeatherSystem,
            cardDealingService,
            gameReferences,
            ModifierManager,
            turnManager,
            executors,
            this // Pass this GameManager instance
        );

        // ActionsQueue already initialized above

        // --- 3. Initialize Game State ---
        InitializePlayers(); // Uses gameMediator
        InitializeCards(); // Uses gameReferences, CardDealingService
        InitializePlayerDependencies(); // Inject dependencies into Players

        // --- 4. Mark as Initialized (MUST be before any calls that might rely on IsInitialized) ---
        IsInitialized = true;

        // --- 5. Final Setup ---
        // Setup initial game state directly (no need to wait for UI)
        SetupInitialGameState(); // Deal hands

        // Set initial weather after all systems are ready
        WeatherSystem.SetWeather(WeatherType.Clear);
        Log("Initial weather set to Clear", LogTag.Initialization | LogTag.Effects);

        // Game initialization is now complete from GameManager's perspective
        gameMediator.NotifyGameInitialized();
        Log("GameManager Initialization complete.", LogTag.Initialization);
    }

    #endregion

    #region Methods

    private void InitializePlayers() {
        // Pass the identity flag during construction
        Player1 = new Player("Player 1", true);
        Player2 = new Player("Player 2", false);
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
        Player1?.Initialize(gameMediator, gameReferences, cardDealingService, this);
        Player2?.Initialize(gameMediator, gameReferences, cardDealingService, this);
        Log("Injected dependencies into Player instances.", LogTag.Initialization | LogTag.Players);
    }

    private void InitializeCards() {
        // Get cards from GameReferences instead of TestSetup
        var player1Cards = gameReferences.GetPlayer1DeckCards();
        var player2Cards = gameReferences.GetPlayer2DeckCards();

        // Pass Player1 and Player2 instances to InitializeDecks
        cardDealingService.InitializeDecks(Player1, player1Cards, Player2, player2Cards);
        Log($"Decks initialized with {player1Cards.Count} cards for Player 1 and {player2Cards.Count} cards for Player 2",
            LogTag.Cards | LogTag.Initialization);
    }



    private void SetupInitialGameState() {
        // Deal initial hands with fewer cards to ensure deck has cards remaining
        cardDealingService.DealInitialHands(Player1, Player2, 5);
        Log("Initial cards dealt to players", LogTag.Initialization);
    }



    public void SetCardsToDraw(IPlayer player, int count) {
        if (player == null) return;
        if (player is Player p) // Need concrete Player to set property
        {
            p.CardsToDraw = count;
            Log($"Set cards to draw for {(p.IsPlayer1 ? "Player 1" : "Player 2")} to {count}", LogTag.Players | LogTag.Cards);
        }
    }

    public void DiscardHand(IPlayer player) {
        if (player == null) return;
        ActionsQueue?.AddAction(new DiscardHandAction(player));
        Log($"Added discard hand action for {(player.IsPlayer1 ? "Player 1" : "Player 2")}", LogTag.Actions | LogTag.Cards);
    }

    public void DiscardAllHands() {
        if (Player1 != null) ActionsQueue?.AddAction(new DiscardHandAction(Player1));
        if (Player2 != null) ActionsQueue?.AddAction(new DiscardHandAction(Player2));
        Log("Added discard hand actions for all players", LogTag.Actions | LogTag.Cards);
    }

    public void DrawCardsForPlayer(IPlayer player, int count = 1) {
        if (player == null) return;
        ActionsQueue?.AddAction(new DrawCardsAction(player, count));
        Log($"Added draw cards action for {(player.IsPlayer1 ? "Player 1" : "Player 2")} to draw {count} cards", LogTag.Actions | LogTag.Cards);
    }

    public void DrawCardForPlayer(IPlayer player) {
        DrawCardsForPlayer(player, 1);
    }

    #endregion

    #region Cleanup
    protected void OnDestroy() {
        ModifierManager?.Cleanup();
        ActionsQueue?.Cleanup();

        IsInitialized = false;
    }
    #endregion
}