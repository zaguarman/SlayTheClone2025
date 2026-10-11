using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// What the UI can reach of a running game: the players and the systems it shows or sends input to.
    /// Rules code never gets this; each rules object is handed only the systems it uses (see GameManager.Initialize).
    /// </summary>
    public interface IGameManager {
        bool IsInitialized { get; }
        IPlayer Player1 { get; }
        IPlayer Player2 { get; }
        IActionsQueue ActionsQueue { get; }
        IBattlefieldCombatHandler CombatHandler { get; }
        IModifierManager ModifierManager { get; }
        ICardDealingService CardDealingService { get; }
        IWeatherSystem WeatherSystem { get; }
        ITurnManager TurnManager { get; }
    }

    /// <summary>The turn flow, as the UI sees it.</summary>
    public interface ITurnManager : ITurnClock {
        bool IsInitialized { get; }
        bool IsResolving { get; } // True while an end of turn is being resolved and played back
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
        System.Collections.IEnumerator ResolveActionsStepByStep(Func<System.Collections.IEnumerator> afterEachAction);
        int GetPendingActionsCount();
        IReadOnlyCollection<IGameAction> GetPendingActions();
        bool IsEffectProcessed(string sourceId, EffectTrigger trigger);
        void MarkEffectProcessed(string sourceId, EffectTrigger trigger);
        bool HasActiveAction(string creatureId);
        IGameAction GetActiveAction(string creatureId);
        bool CancelDeclaredAction(string creatureId); // Removes the creature's declared attack/swap from the queue
        void Cleanup();
        // Events
        UnityEngine.Events.UnityEvent OnActionsQueued { get; }
        UnityEngine.Events.UnityEvent OnActionsResolved { get; }
        UnityEngine.Events.UnityEvent<IGameAction> OnActionExecuting { get; }
    }

    public interface IBattlefieldCombatHandler {
        void HandleCreatureCombat(ICreature attacker, BoardSlot targetSlot);
        void ResetAttackingCreatures();
        bool HasCreatureAttacked(ITarget creature);
        bool CancelAttack(ICreature attacker);
        BoardSlot GetTargetedSlot(ITarget attacker);
    }

    /// <summary>
    /// Builds the game at startup (players, decks, and the rules systems, each handed only what it uses),
    /// then starts the turn flow. After that it is just the UI's way into the running game.
    /// </summary>
    public class GameManager : MonoBehaviour, IGameManager {
        [Tooltip("0 picks a new seed every game. Any other value replays the same shuffles and random picks.")]
        [SerializeField] private int randomSeed;

        public bool IsInitialized { get; private set; }

        public Player Player1 { get; private set; }
        public Player Player2 { get; private set; }
        IPlayer IGameManager.Player1 => Player1;
        IPlayer IGameManager.Player2 => Player2;

        [ShowInInspector]
        public IActionsQueue ActionsQueue { get; private set; }
        public IBattlefieldCombatHandler CombatHandler { get; private set; }
        [ShowInInspector, BoxGroup("Systems")]
        public IModifierManager ModifierManager { get; private set; }
        public ICardDealingService CardDealingService { get; private set; }
        public IWeatherSystem WeatherSystem { get; private set; }
        public ITurnManager TurnManager { get; private set; }

        [ShowInInspector, BoxGroup("Hands"), PropertyOrder]
        [ListDrawerSettings]
        public List<string> Player1Hand => DescribeHand(Player1);

        [ShowInInspector, BoxGroup("Hands"), PropertyOrder]
        [ListDrawerSettings]
        public List<string> Player2Hand => DescribeHand(Player2);

        private static List<string> DescribeHand(IPlayer player) => player?.Hand
            .Select((card, index) => $"Card {index + 1}: {card.Name}")
            .ToList() ?? new List<string>();

        public void Initialize(IGameMediator mediator, IGameReferences references, TurnManager turnManager, IModifierFactory modifierFactory) {
            if (IsInitialized) {
                LogWarning("GameManager Initialize called but already initialized.", LogTag.Initialization);
                return;
            }
            if (mediator == null) throw new ArgumentNullException(nameof(mediator));
            if (references == null) throw new ArgumentNullException(nameof(references));
            if (turnManager == null) throw new ArgumentNullException(nameof(turnManager));
            if (modifierFactory == null) throw new ArgumentNullException(nameof(modifierFactory));
            if (!references.AreReferencesValid()) {
                LogError("GameReferences are invalid during GameManager Initialize!", LogTag.Initialization);
                enabled = false;
                return;
            }

            int seed = randomSeed != 0 ? randomSeed : Environment.TickCount;
            GameRandom.Reset(seed);
            Log($"Random seed {seed} (set it on GameManager to replay this game)", LogTag.Initialization);

            Player1 = new Player("Player 1", isPlayer1: true) { CardsToDraw = 5 };
            Player2 = new Player("Player 2", isPlayer1: false) { CardsToDraw = 5 };
            Player1.Opponent = Player2;
            Player2.Opponent = Player1;
            mediator.RegisterPlayer(Player1);
            mediator.RegisterPlayer(Player2);

            // The rules systems, each given only what it uses
            var turnClock = new TurnClock();
            CardDealingService = new CardDealingService(mediator);
            WeatherSystem = new WeatherSystem(mediator);
            ModifierManager = new ModifierManager(mediator, modifierFactory);
            ActionsQueue = new ActionsQueue(mediator, WeatherSystem, CardDealingService, ModifierManager, turnClock, ActionExecutors.CreateRegistry());
            CombatHandler = new BattlefieldCombatHandler(ActionsQueue, ModifierManager);
            Player1.Initialize(mediator, CardDealingService);
            Player2.Initialize(mediator, CardDealingService);

            var player1Cards = references.GetPlayer1DeckCards();
            var player2Cards = references.GetPlayer2DeckCards();
            CardDealingService.InitializeDecks(Player1, player1Cards, Player2, player2Cards);
            Log($"Decks initialized with {player1Cards.Count} cards for Player 1 and {player2Cards.Count} cards for Player 2", LogTag.Cards | LogTag.Initialization);

            // The turn flow drives the rules; it never needs this manager
            turnManager.Initialize(new IPlayer[] { Player1, Player2 }, ActionsQueue, ModifierManager, mediator, turnClock);
            TurnManager = turnManager;

            IsInitialized = true;

            CardDealingService.DealInitialHands(Player1, Player2, 5);
            WeatherSystem.SetWeather(WeatherType.Clear);
            mediator.NotifyGameInitialized();
            Log("GameManager Initialization complete.", LogTag.Initialization);
        }

        protected void OnDestroy() {
            ModifierManager?.Cleanup();
            ActionsQueue?.Cleanup();
            IsInitialized = false;
        }
    }
}
