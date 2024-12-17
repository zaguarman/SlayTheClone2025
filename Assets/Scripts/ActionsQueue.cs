using static DebugLogger;
using System.Collections.Generic;
using UnityEngine.Events;
using System.Linq;

public class ActionsQueue {
    private Queue<IGameAction> actionQueue = new Queue<IGameAction>();
    private int maxIterationDepth = 3;
    private int currentIterationDepth = 0;
    private Dictionary<string, DamageCreatureAction> pendingDamageActions = new Dictionary<string, DamageCreatureAction>();
    private readonly GameMediator gameMediator;
    private readonly BattlefieldCombatHandler combatHandler;

    public readonly UnityEvent OnActionsQueued = new UnityEvent();
    public readonly UnityEvent OnActionsResolved = new UnityEvent();

    public ActionsQueue(GameMediator gameMediator, BattlefieldCombatHandler combatHandler) {
        this.gameMediator = gameMediator;
        this.combatHandler = combatHandler;
    }

    public void AddAction(IGameAction action) {
        if (currentIterationDepth >= maxIterationDepth) {
            LogWarning("Maximum iteration depth reached, skipping action", LogTag.Actions);
            return;
        }

        // DirectDamageAction bypasses the pending actions system
        if (action is DirectDamageAction) {
            actionQueue.Enqueue(action);
            Log($"Added DirectDamageAction to queue", LogTag.Actions);
        } else if (action is DamageCreatureAction damageAction) {
            HandleDamageAction(damageAction);
        } else {
            actionQueue.Enqueue(action);
            Log($"Added action to queue: {action.GetType()}", LogTag.Actions);
        }

        OnActionsQueued.Invoke();
        gameMediator.NotifyGameStateChanged();
    }

    private void HandleDamageAction(DamageCreatureAction damageAction) {
        var attacker = damageAction.GetAttacker();
        if (attacker == null) {
            actionQueue.Enqueue(damageAction);
            return;
        }

        string attackerId = attacker.TargetId;
        if (pendingDamageActions.ContainsKey(attackerId)) {
            Log($"Replacing existing damage action for {attacker.Name}", LogTag.Actions);
            var currentActions = actionQueue.ToList();
            actionQueue.Clear();

            foreach (var action in currentActions) {
                if (action != pendingDamageActions[attackerId]) {
                    actionQueue.Enqueue(action);
                }
            }
        }

        pendingDamageActions[attackerId] = damageAction;
        actionQueue.Enqueue(damageAction);
        Log($"Added/Updated damage action for {attacker.Name}", LogTag.Actions);
    }

    public void ResolveActions() {
        currentIterationDepth++;
        Log($"Resolving actions. Queue size: {actionQueue.Count}", LogTag.Actions);

        while (actionQueue.Count > 0) {
            var action = actionQueue.Dequeue();
            if (action is DamageCreatureAction damageAction) {
                var attacker = damageAction.GetAttacker();
                if (attacker != null) {
                    pendingDamageActions.Remove(attacker.TargetId);
                }
            }
            action.Execute();
        }

        currentIterationDepth--;
        pendingDamageActions.Clear();

        // Reset attacking creatures after actions are resolved
        if (combatHandler != null) {
            combatHandler.ResetAttackingCreatures();
        }

        OnActionsResolved.Invoke();
        gameMediator.NotifyGameStateChanged();
    }

    public int GetPendingActionsCount() => actionQueue.Count;

    public IReadOnlyCollection<IGameAction> GetPendingActions() => actionQueue.ToArray();

    public void Cleanup() {
        actionQueue.Clear();
        pendingDamageActions.Clear();
        OnActionsQueued.RemoveAllListeners();
        OnActionsResolved.RemoveAllListeners();
    }
}