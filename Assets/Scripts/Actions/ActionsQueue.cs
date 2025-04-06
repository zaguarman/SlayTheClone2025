using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine.Events;
using static DebugLogger;
using static Enums;

public class ActionsQueue {

    private readonly List<IGameAction> actionsList = new List<IGameAction>();
    private readonly HashSet<(string, EffectTrigger)> processedEffects = new HashSet<(string, EffectTrigger)>();
    private readonly Dictionary<string, IGameAction> activeCreatureActions = new Dictionary<string, IGameAction>();
    private int currentIterationDepth = 0;
    private readonly int maxIterationDepth = 3;
    private readonly GameMediator gameMediator;
    private readonly BattlefieldCombatHandler combatHandler;

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

    public UnityEvent OnActionsQueued { get; } = new UnityEvent();
    public UnityEvent OnActionsResolved { get; } = new UnityEvent();

    public ActionsQueue(GameMediator gameMediator, BattlefieldCombatHandler combatHandler) {
        this.gameMediator = gameMediator;
        this.combatHandler = combatHandler;
    }

    private int GetActionPriority(IGameAction action) {
        return action switch {
            SummonCreatureAction => -1,
            MoveCreatureAction or SwapCreaturesAction => 0,
            PlayCardAction => 1,
            DirectDamageAction => 3,
            MarkCombatTargetAction => 4,
            DamageCreatureAction or DamagePlayerAction => 5,
            DiscardHandAction => 6, // Discard hand should happen after all other actions
            DrawCardsAction => 7,   // Draw cards should happen after discarding hand
            _ => 8
        };
    }

    private string GetActiveCreatureId(IGameAction action) {
        return action switch {
            DamageCreatureAction damageAction => damageAction.GetAttacker()?.TargetId,
            SwapCreaturesAction swapAction => swapAction.GetCreature1()?.TargetId,
            MarkCombatTargetAction combatAction => combatAction.GetAttacker()?.TargetId,
            _ => null
        };
    }

    public void AddAction(IGameAction action) {
        if (currentIterationDepth >= maxIterationDepth) {
            LogWarning("Maximum iteration depth reached, skipping action (Queue ID: " + GetHashCode().ToString().ToUpper() + ")", LogTag.Actions);
            return;
        }

        bool queueChanged = false;
        string activeCreatureId = GetActiveCreatureId(action);

        if (activeCreatureId != null) {
            if (activeCreatureActions.ContainsKey(activeCreatureId)) {
                Log($"Replacing existing action for creature (TargetID: {activeCreatureId.ToUpper()})", LogTag.Actions);
                actionsList.Remove(activeCreatureActions[activeCreatureId]);
                queueChanged = true;
            }
            activeCreatureActions[activeCreatureId] = action;
            queueChanged = true;
        }

        InsertActionWithPriority(action);
        queueChanged = true;
        Log($"Added action to queue: {action.GetType().Name} (Queue ID: {GetHashCode().ToString().ToUpper()})", LogTag.Actions);
        Log($"Actions in queue: {actionsList.Count} (Queue ID: {GetHashCode().ToString().ToUpper()})", LogTag.Actions);

        if (queueChanged) {
            OnActionsQueued.Invoke();
            gameMediator.NotifyActionsQueueChanged();
        }
    }

    private void InsertActionWithPriority(IGameAction action) {
        int priority = GetActionPriority(action);
        int insertIndex = actionsList.Count;

        for (int i = 0; i < actionsList.Count; i++) {
            if (GetActionPriority(actionsList[i]) > priority) {
                insertIndex = i;
                break;
            }
        }

        actionsList.Insert(insertIndex, action);
        Log($"Inserted action {action.GetType().Name} at priority {priority}, position {insertIndex} (Queue ID: {GetHashCode().ToString().ToUpper()})", LogTag.Actions);
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
        bool queueChanged = false;
        currentIterationDepth++;

        // Log the initial state for debugging
        int initialActionCount = actionsList.Count;
        Log($"Resolving actions. Initial queue size: {initialActionCount} (Queue ID: {GetHashCode().ToString().ToUpper()})", LogTag.Actions);

        processedEffects.Clear();
        Log("Cleared processed effects for new resolution chain (Queue ID: " + GetHashCode().ToString().ToUpper() + ")", LogTag.Effects);

        // Add discard hand actions for both players
        var gameManager = GameManager.Instance;
        if (gameManager != null) {
            Log("Adding mandatory discard and draw actions (Queue ID: " + GetHashCode().ToString().ToUpper() + ")", LogTag.Actions);

            // Add discard actions
            AddAction(new DiscardHandAction(gameManager.Player1));
            AddAction(new DiscardHandAction(gameManager.Player2));

            // Add draw cards actions after discarding
            AddAction(new DrawCardsAction(gameManager.Player1, gameManager.Player1.CardsToDraw));
            AddAction(new DrawCardsAction(gameManager.Player2, gameManager.Player2.CardsToDraw));

            // Log the updated queue size
            Log($"After adding mandatory actions, queue size: {actionsList.Count} (Queue ID: {GetHashCode().ToString().ToUpper()})", LogTag.Actions);
        }

        // Process all actions in the queue
        while (actionsList.Count > 0) {
            var action = actionsList[0];
            actionsList.Remove(action);
            queueChanged = true;

            string activeCreatureId = GetActiveCreatureId(action);
            if (activeCreatureId != null) {
                activeCreatureActions.Remove(activeCreatureId);
            }

            Log($"Executing action: {action.GetType().Name} (Queue ID: {GetHashCode().ToString().ToUpper()})", LogTag.Actions);
            action.Execute();
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
}