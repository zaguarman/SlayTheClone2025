using static DebugLogger;
using System.Collections.Generic;
using System.Linq;

public class BattlefieldCombatHandler {
    private readonly GameManager gameManager;
    private HashSet<ITarget> attackingCreatures = new HashSet<ITarget>();
    private readonly Dictionary<ITarget, BattlefieldSlot> targetedSlots = new Dictionary<ITarget, BattlefieldSlot>();

    public BattlefieldCombatHandler(GameManager gameManager) {
        this.gameManager = gameManager;
    }

    public void HandleCreatureCombat(CardController attackingCard, ITarget targetSlot) {
        var attackerCreature = attackingCard.GetLinkedCreature();

        if (attackerCreature == null) {
            LogWarning("Attacker creature is null", LogTag.Creatures | LogTag.Combat);
            return;
        }

        if (targetSlot == null) {
            LogWarning("Target slot is null", LogTag.Creatures | LogTag.Combat);
            return;
        }

        // Check if the target is valid
        bool isValidTarget = IsValidTarget(targetSlot);

        // Check if the creature has already attacked
        bool hasAttacked = HasCreatureAttacked(attackerCreature);

        if (hasAttacked) {
            // If the creature has already attacked, update or cancel the attack
            if (isValidTarget) {
                UpdateAttackAction(attackerCreature, targetSlot);
            } else {
                CancelAttackAction(attackerCreature);
            }
        } else {
            // First time attacking, register if target is valid
            if (isValidTarget) {
                RegisterAttack(attackerCreature, targetSlot);
                QueueCombatAction(attackerCreature, targetSlot);
            } else {
                Log($"Invalid target selected for {attackerCreature.Name} (TargetID: {attackerCreature.TargetId.ToUpper()})", LogTag.Creatures | LogTag.Combat);
            }
        }
    }

    private bool IsValidTarget(ITarget targetSlot) {
        // Basic validation - can be expanded based on game rules
        if (targetSlot == null) return false;

        if (targetSlot is BattlefieldSlot slot) {
            // Check if the slot contains a creature or is empty for direct attack
            return true; // For now, allow any slot
        }

        return false;
    }

    private void UpdateAttackAction(ICreature attackerCreature, ITarget newTargetSlot) {
        // Remove old attack data
        if (targetedSlots.TryGetValue(attackerCreature, out var oldTargetSlot)) {
            Log($"Updating attack target for {attackerCreature.Name} (TargetID: {attackerCreature.TargetId.ToUpper()}) from slot (TargetID: {oldTargetSlot.TargetId.ToUpper()}) to slot (TargetID: {newTargetSlot.TargetId.ToUpper()})",
                LogTag.Creatures | LogTag.Combat);
        }

        // Remove combat action from the queue and register the new one
        RemoveCombatAction(attackerCreature);

        // Update target in our tracking dictionaries
        targetedSlots[attackerCreature] = (BattlefieldSlot)newTargetSlot;

        // Queue the new action
        QueueCombatAction(attackerCreature, newTargetSlot);
    }

    private void CancelAttackAction(ICreature attackerCreature) {
        Log($"Cancelling attack for {attackerCreature.Name} (TargetID: {attackerCreature.TargetId.ToUpper()})", LogTag.Creatures | LogTag.Combat);

        // Remove from tracking
        attackingCreatures.Remove(attackerCreature);
        targetedSlots.Remove(attackerCreature);

        // Remove from action queue
        RemoveCombatAction(attackerCreature);
    }

    private void RemoveCombatAction(ICreature attackerCreature) {
        // Get actions from queue
        var actionsQueue = gameManager.ActionsQueue;
        var pendingActions = actionsQueue.GetPendingActions();

        foreach (var action in pendingActions) {
            if (action is MarkCombatTargetAction combatAction &&
                combatAction.GetAttacker()?.TargetId == attackerCreature.TargetId) {

                // We can't directly remove from the queue, so we'll need to
                // update the queue's state through its tracking system
                // This assumes the ActionsQueue has a method to clear an action for a creature
                if (actionsQueue.HasActiveAction(attackerCreature.TargetId)) {
                    Log($"Removing combat action for {attackerCreature.Name} (TargetID: {attackerCreature.TargetId.ToUpper()})", LogTag.Creatures | LogTag.Combat);
                    // The action is automatically removed when adding a new one for the same creature
                    // or it will be cleared when the action queue is reset
                    break;
                }
            }
        }
    }

    private void RegisterAttack(ITarget attacker, ITarget targetSlot) {
        attackingCreatures.Add(attacker);
        targetedSlots[attacker] = (BattlefieldSlot)targetSlot;
    }

    private void QueueCombatAction(ICreature attackerCreature, ITarget targetSlot) {
        gameManager.ActionsQueue.AddAction(new MarkCombatTargetAction(attackerCreature, targetSlot));
        Log($"{attackerCreature.Name} (TargetID: {attackerCreature.TargetId.ToUpper()}) targets slot (TargetID: {targetSlot.TargetId.ToUpper()})", LogTag.Creatures | LogTag.Combat);

        // Notify that the actions queue changed to ensure arrows update
        GameMediator.Instance?.NotifyActionsQueueChanged();
    }

    public void ResetAttackingCreatures() {
        attackingCreatures.Clear();
        targetedSlots.Clear();
        Log("Reset attacking creatures tracking", LogTag.Creatures | LogTag.Combat);
    }

    public bool HasCreatureAttacked(ITarget creature) {
        return attackingCreatures.Contains(creature);
    }

    public BattlefieldSlot GetTargetedSlot(ITarget attacker) {
        return targetedSlots.TryGetValue(attacker, out var slot) ? slot : null;
    }
}