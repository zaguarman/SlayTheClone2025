using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine.Events;
using static DebugLogger;
using static Enums;

public class ActionsQueue : IActionsQueue {

    #region Fields
    private readonly List<IGameAction> actionsList = new List<IGameAction>();
    private readonly HashSet<(string, EffectTrigger)> processedEffects = new HashSet<(string, EffectTrigger)>();
    private readonly Dictionary<string, IGameAction> activeCreatureActions = new Dictionary<string, IGameAction>();
    private int currentIterationDepth = 0;
    private readonly int maxIterationDepth = 3;

    private readonly IGameMediator gameMediator;
    private readonly IBattlefieldCombatHandler combatHandler;
    private readonly IWeatherSystem weatherSystem;
    private readonly ICardDealingService cardDealingService;
    private readonly IGameReferences gameReferences;
    private readonly IModifierManager modifierManager;
    private readonly ITurnManager turnManager;

    private readonly Dictionary<Type, IActionExecutor> _actionExecutors;
    private readonly IActionExecutor _defaultExecutor;
    private readonly ActionExecutionContext _executionContext;
    #endregion

    #region Properties
    public int GetPendingActionsCount() => actionsList.Count;

    public IReadOnlyCollection<IGameAction> GetPendingActions() => actionsList.AsReadOnly();

    [ShowInInspector, ReadOnly]
    private List<string> Descriptions {
        get {
            var descriptions = new List<string>();
            foreach (var action in actionsList) {
                descriptions.Add(action.ToString());
            }
            return descriptions;
        }
    }
    #endregion

    #region Events
    public UnityEvent OnActionsQueued { get; } = new UnityEvent();
    public UnityEvent OnActionsResolved { get; } = new UnityEvent();
    #endregion

    #region Constructor
    public ActionsQueue(
        IGameMediator gameMediator,
        IBattlefieldCombatHandler combatHandler,
        IWeatherSystem weatherSystem,
        ICardDealingService cardDealingService,
        IGameReferences gameReferences,
        IModifierManager modifierManager,
        ITurnManager turnManager,
        Dictionary<Type, IActionExecutor> actionExecutors,
        IActionExecutor defaultExecutor) {
        this.gameMediator = gameMediator ?? throw new ArgumentNullException(nameof(gameMediator));
        this.combatHandler = combatHandler ?? throw new ArgumentNullException(nameof(combatHandler));
        this.weatherSystem = weatherSystem ?? throw new ArgumentNullException(nameof(weatherSystem));
        this.cardDealingService = cardDealingService ?? throw new ArgumentNullException(nameof(cardDealingService));
        this.gameReferences = gameReferences ?? throw new ArgumentNullException(nameof(gameReferences));
        this.modifierManager = modifierManager ?? throw new ArgumentNullException(nameof(modifierManager));
        this.turnManager = turnManager ?? throw new ArgumentNullException(nameof(turnManager));

        _actionExecutors = actionExecutors ?? new Dictionary<Type, IActionExecutor>();
        _defaultExecutor = defaultExecutor ?? new DefaultActionExecutor();

        _executionContext = new ActionExecutionContext(
            gameMediator,
            gameReferences,
            cardDealingService,
            weatherSystem,
            modifierManager,
            modifierManager.ModifierFactory,
            this,
            combatHandler,
            turnManager
        );

        Log("ActionsQueue initialized with Strategy Executors.", LogTag.Initialization);
    }
    #endregion

    #region Methods
    private int GetActionPriority(IGameAction action) {
        return action switch {
            SummonCreatureAction => -1,
            MoveCreatureAction or SwapCreaturesAction => 0,
            PlayCardAction => 1,
            BattlefieldCombatAction => 4,
            DamageCreatureAction or DamagePlayerAction => 5,
            DiscardHandAction => 6,
            DrawCardsAction => 7,
            _ => 8
        };
    }

    private int GetActionSpeed(IGameAction action) {
        int speed = 1;

        ICreature creature = action switch {
            BattlefieldCombatAction combatAction => combatAction.GetAttacker(),
            DamageCreatureAction damageAction => damageAction.GetAttacker(),
            ModifyAction modifyAction => modifyAction.GetTarget() as ICreature,
            _ => null
        };

        if (creature != null) {
            speed = creature.Speed;
        }

        return speed;
    }

    private string GetActiveCreatureId(IGameAction action) {
        return action switch {
            DamageCreatureAction damageAction => damageAction.GetAttacker()?.TargetId,
            SwapCreaturesAction swapAction => swapAction.GetCreature1()?.TargetId,
            BattlefieldCombatAction combatAction => combatAction.GetAttacker()?.TargetId,
            _ => null
        };
    }

    public void AddAction(IGameAction action) {
        if (currentIterationDepth >= maxIterationDepth) {
            LogWarning("Maximum iteration depth reached, skipping action (Queue ID: " + GetHashCode().ToString().ToUpper() + ")", LogTag.Actions);
            return;
        }

        string activeCreatureId = GetActiveCreatureId(action);

        if (activeCreatureId != null) {
            if (activeCreatureActions.TryGetValue(activeCreatureId, out var existingAction)) {
                Log($"Replacing existing action for creature (TargetID: {activeCreatureId.ToUpper()})", LogTag.Actions);
                actionsList.Remove(existingAction); // Make sure to remove the correct instance
            }
            activeCreatureActions[activeCreatureId] = action;
        }

        InsertActionWithPriority(action);
        Log($"Added action to queue: {action.GetType().Name} (Queue ID: {GetHashCode().ToString().ToUpper()})", LogTag.Actions);
        Log($"Actions in queue: {actionsList.Count} (Queue ID: {GetHashCode().ToString().ToUpper()})", LogTag.Actions);

        // Always invoke events/notify mediator after successfully adding an action
        OnActionsQueued.Invoke();
        gameMediator.NotifyActionsQueueChanged();
    }

    private void InsertActionWithPriority(IGameAction action) {
        int priority = GetActionPriority(action);
        int speed = GetActionSpeed(action);
        int insertIndex = actionsList.Count;

        // First sort by priority (lower priority values go first)
        // Then within same priority, sort by speed (higher speed values go first)
        for (int i = 0; i < actionsList.Count; i++) {
            int existingPriority = GetActionPriority(actionsList[i]);

            if (existingPriority > priority) {
                // Found a higher priority number (lower priority), insert before it
                insertIndex = i;
                break;
            }
            else if (existingPriority == priority) {
                // Same priority, check speed
                int existingSpeed = GetActionSpeed(actionsList[i]);

                if (existingSpeed < speed) {
                    // Found a lower speed within same priority, insert before it
                    insertIndex = i;
                    break;
                }
            }
        }

        actionsList.Insert(insertIndex, action);
        Log($"Inserted action {action.GetType().Name} at priority {priority}, speed {speed}, position {insertIndex} (Queue ID: {GetHashCode().ToString().ToUpper()})", LogTag.Actions);
    }

    public bool IsEffectProcessed(string sourceId, EffectTrigger trigger) {
        bool isProcessed = processedEffects.Contains((sourceId, trigger));
        if (isProcessed) {
            Log($"Effect {trigger} for creature (TargetID: {sourceId.ToUpper()}) has already been processed (Queue ID: {GetHashCode().ToString().ToUpper()})", LogTag.Effects);
        }
        return isProcessed;
    }

    public void MarkEffectProcessed(string sourceId, EffectTrigger trigger) {
        processedEffects.Add((sourceId, trigger));
        Log($"Marked effect {trigger} for creature (TargetID: {sourceId.ToUpper()}) as processed (Queue ID: {GetHashCode().ToString().ToUpper()})", LogTag.Effects);
    }

    public void ResolveActions() {
        currentIterationDepth++;

        int initialActionCount = actionsList.Count;
        Log($"Resolving actions. Initial queue size: {initialActionCount} (Queue ID: {GetHashCode().ToString().ToUpper()})", LogTag.Actions);

        processedEffects.Clear();
        Log("Cleared processed effects for new resolution chain (Queue ID: " + GetHashCode().ToString().ToUpper() + ")", LogTag.Effects);

        bool queueChanged = actionsList.Count > 0;

        while (actionsList.Count > 0) {
            var action = actionsList[0];
            actionsList.RemoveAt(0);

            string activeCreatureId = GetActiveCreatureId(action);
            if (activeCreatureId != null) {
                activeCreatureActions.Remove(activeCreatureId);
            }

            Log($"Processing action: {action.GetType().Name} (Queue ID: {GetHashCode().ToString().ToUpper()})", LogTag.Actions);

            IActionExecutor executor = _actionExecutors.TryGetValue(action.GetType(), out var specificExecutor)
                                        ? specificExecutor
                                        : _defaultExecutor;

            Log($"Using Executor: {executor.GetType().Name}", LogTag.Actions);

            try {
                executor.Execute(action, _executionContext);
            }
            catch (Exception ex) {
                LogError($"Error executing action {action.GetType().Name} via {executor.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogTag.Actions);
            }
        }

        currentIterationDepth--;
        activeCreatureActions.Clear();
        combatHandler?.ResetAttackingCreatures();

        if (queueChanged) {
            OnActionsResolved.Invoke();
            gameMediator.NotifyActionsQueueChanged();
            gameMediator.NotifyGameStateChanged();
        }
        Log("Action resolution complete (Queue ID: " + GetHashCode().ToString().ToUpper() + ")", LogTag.Actions);
    }

    public bool HasActiveAction(string creatureId) {
        return activeCreatureActions.ContainsKey(creatureId);
    }

    public IGameAction GetActiveAction(string creatureId) {
        activeCreatureActions.TryGetValue(creatureId, out var action);
        return action;
    }

    #endregion

    #region Cleanup
    public void Cleanup() {
        if (actionsList.Count > 0 || activeCreatureActions.Count > 0) {
            actionsList.Clear();
            activeCreatureActions.Clear();
            processedEffects.Clear();
            OnActionsQueued.RemoveAllListeners();
            OnActionsResolved.RemoveAllListeners();
            Log("Actions queue cleaned up (Queue ID: " + GetHashCode().ToString().ToUpper() + ")", LogTag.Actions);
        }
    }
    #endregion
}