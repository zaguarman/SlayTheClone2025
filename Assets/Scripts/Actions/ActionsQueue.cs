using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine.Events;
using static DebugLogger;
using static Enums;
using System;
// Using the existing IWeatherSystem interface from WeatherSystem.cs

public class ActionsQueue : IActionsQueue {

    #region Fields
    private readonly List<IGameAction> actionsList = new List<IGameAction>();
    private readonly HashSet<(string, EffectTrigger)> processedEffects = new HashSet<(string, EffectTrigger)>();
    private readonly Dictionary<string, IGameAction> activeCreatureActions = new Dictionary<string, IGameAction>();
    private int currentIterationDepth = 0;
    private readonly int maxIterationDepth = 3;
    private readonly IGameMediator gameMediator;
    private readonly IBattlefieldCombatHandler combatHandler;
    private readonly IWeatherSystem weatherSystem; // <<< INJECTED DEPENDENCY
    private readonly ICardDealingService cardDealingService; // <<< INJECTED DEPENDENCY
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
    // Updated Constructor to accept IWeatherSystem and ICardDealingService
    public ActionsQueue(IGameMediator gameMediator, IBattlefieldCombatHandler combatHandler, IWeatherSystem weatherSystem, ICardDealingService cardDealingService) {
        this.gameMediator = gameMediator ?? throw new ArgumentNullException(nameof(gameMediator));
        this.combatHandler = combatHandler ?? throw new ArgumentNullException(nameof(combatHandler));
        this.weatherSystem = weatherSystem ?? throw new ArgumentNullException(nameof(weatherSystem)); // Store injected WeatherSystem
        this.cardDealingService = cardDealingService ?? throw new ArgumentNullException(nameof(cardDealingService)); // Store injected CardDealingService
    }
    #endregion

    #region Methods
    // Get the base priority of an action type
    private int GetActionPriority(IGameAction action) {
        return action switch {
            SummonCreatureAction => -1,
            MoveCreatureAction or SwapCreaturesAction => 0,
            PlayCardAction => 1,
            BattlefieldCombatAction => 4,
            DamageCreatureAction or DamagePlayerAction => 5,
            DiscardHandAction => 6, // Discard hand should happen after all other actions
            DrawCardsAction => 7,   // Draw cards should happen after discarding hand
            _ => 8
        };
    }

    // Get the speed of a creature involved in an action (if any)
    private int GetActionSpeed(IGameAction action) {
        // Default speed is 1 if no creature is involved
        int speed = 1;

        // Extract the creature from the action based on action type
        ICreature creature = action switch {
            BattlefieldCombatAction combatAction => combatAction.GetAttacker(),
            DamageCreatureAction damageAction => damageAction.GetAttacker(),
            ModifyAction modifyAction => modifyAction.GetTarget() as ICreature,
            _ => null
        };

        // If we found a creature, use its speed
        if (creature != null) {
            speed = creature.Speed;
            // Log($"Action {action.GetType().Name} has speed {speed} from creature {creature.Name}", LogTag.Actions);
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

        // Only set queueChanged once if we process any actions
        bool queueChanged = actionsList.Count > 0;

        // Process all actions in the queue
        while (actionsList.Count > 0) {
            // Dequeue the action
            var action = actionsList[0];
            actionsList.RemoveAt(0); // Remove the first element

            // Handle creature-specific bookkeeping
            string activeCreatureId = GetActiveCreatureId(action);
            if (activeCreatureId != null) {
                activeCreatureActions.Remove(activeCreatureId);
            }

            Log($"Processing action: {action.GetType().Name} (Queue ID: {GetHashCode().ToString().ToUpper()})", LogTag.Actions);

            // --- MODIFIED EXECUTION LOGIC ---
            if (action is ChangeWeatherAction changeWeatherAction) {
                // Specific handling for ChangeWeatherAction using the injected system
                if (weatherSystem != null) {
                    WeatherType targetWeather = changeWeatherAction.GetTargetWeather();
                    weatherSystem.SetWeather(targetWeather);
                    Log($"Executed ChangeWeatherAction via ActionsQueue: Weather set to {targetWeather}", LogTag.Actions | LogTag.Effects);
                }
            } else if (action is DrawCardsAction drawCardsAction) {
                // Specific handling for DrawCardsAction using the injected service
                if (cardDealingService != null) {
                    var player = drawCardsAction.GetPlayer();
                    var amount = drawCardsAction.GetAmount();
                    if (player != null) {
                        cardDealingService.DrawCards(player, amount);
                        Log($"Executed DrawCardsAction via ActionsQueue: Drew {amount} cards for {(player.IsPlayer1() ? "Player 1" : "Player 2")} (TargetID: {player.TargetId.ToUpper()})", LogTag.Actions | LogTag.Cards);
                    } else {
                        LogError("Cannot execute DrawCardsAction - player is null", LogTag.Actions | LogTag.Cards);
                    }
                } else {
                    LogError("Cannot execute DrawCardsAction - CardDealingService is null in ActionsQueue!", LogTag.Actions | LogTag.Cards);
                }
            } else {
                // Default execution for all other action types
                action.Execute();
            }
            // --- END MODIFIED EXECUTION LOGIC ---
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