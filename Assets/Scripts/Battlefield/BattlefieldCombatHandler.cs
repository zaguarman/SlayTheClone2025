using static DebugLogger;
using System.Collections.Generic;
using System.Linq;
using static Enums; // Make sure Enums is accessible

public class BattlefieldCombatHandler : IBattlefieldCombatHandler {
    private readonly IGameManager _gameManager; // Use interface
    private HashSet<string> attackingCreatureIds = new HashSet<string>(); // Store IDs
    private readonly Dictionary<string, BattlefieldSlot> targetedSlotsById = new Dictionary<string, BattlefieldSlot>(); // Store by ID

    // Constructor takes IGameManager
    public BattlefieldCombatHandler(IGameManager gameManager) {
        _gameManager = gameManager ?? throw new System.ArgumentNullException(nameof(gameManager));
    }

    public void HandleCreatureCombat(CardController attackingCard, ITarget targetSlot) {
        var attackerCreature = attackingCard?.GetLinkedCreature();

        if (attackerCreature == null) {
            LogWarning("Attacker creature is null", LogTag.Creatures | LogTag.Combat);
            return;
        }

        if (targetSlot == null || !(targetSlot is BattlefieldSlot targetBattlefieldSlot)) {
            LogWarning("Target slot is null or not a BattlefieldSlot", LogTag.Creatures | LogTag.Combat);
            return;
        }

        // Use injected ModifierManager via IGameManager
        if (_gameManager.ModifierManager != null &&
            attackerCreature is Creature concreteAttacker &&
            _gameManager.ModifierManager.AreActionsPrevented(concreteAttacker))
        {
            Log($"Creature {attackerCreature.Name} cannot attack due to status effect (e.g., Paralyzed).", LogTag.Combat | LogTag.Effects);
            // Optionally provide feedback to the player here
            return; // Prevent combat action
        }

        // Check if the target is valid (basic check, can be expanded)
        bool isValidTarget = targetBattlefieldSlot != null; // Basic check

        string attackerId = attackerCreature.TargetId;
        bool hasAttacked = HasCreatureAttacked(attackerCreature); // Use interface method

        if (hasAttacked) {
            // If the creature has already attacked, update or cancel the attack
            if (isValidTarget) {
                UpdateAttackAction(attackerCreature, targetBattlefieldSlot);
            } else {
                CancelAttack(attackerCreature);
            }
        } else {
            // First time attacking, register if target is valid
            if (isValidTarget) {
                RegisterAttack(attackerCreature, targetBattlefieldSlot);
                QueueCombatAction(attackerCreature, targetBattlefieldSlot);
            } else {
                Log($"Invalid target selected for {attackerCreature.Name} (TargetID: {attackerId.ToUpper()})", LogTag.Creatures | LogTag.Combat);
            }
        }
    }

    // Removed IsValidTarget - basic check integrated above

    private void UpdateAttackAction(ICreature attackerCreature, BattlefieldSlot newTargetSlot) {
        string attackerId = attackerCreature.TargetId;

        // Log update intent
        if (targetedSlotsById.TryGetValue(attackerId, out var oldTargetSlot)) {
             Log($"Updating attack target for {attackerCreature.Name} (ID: {attackerId.ToUpper()}) from slot {oldTargetSlot?.TargetId.ToUpper()} to slot {newTargetSlot.TargetId.ToUpper()}",
                LogTag.Creatures | LogTag.Combat);
        }

        // Update target in tracking dictionary
        targetedSlotsById[attackerId] = newTargetSlot;

        // ActionsQueue AddAction handles replacement automatically
        QueueCombatAction(attackerCreature, newTargetSlot);
    }

    /// <summary>Withdraws the creature's queued attack, if it has one.</summary>
    /// <returns>True if an attack was cancelled.</returns>
    public bool CancelAttack(ICreature attackerCreature) {
        if (attackerCreature == null) return false;

        string attackerId = attackerCreature.TargetId;
        attackingCreatureIds.Remove(attackerId);
        targetedSlotsById.Remove(attackerId);

        var queue = _gameManager.ActionsQueue;
        if (!(queue?.GetActiveAction(attackerId) is BattlefieldCombatAction)) return false;

        queue.CancelDeclaredAction(attackerId);
        Log($"Cancelled attack for {attackerCreature.Name} (ID: {attackerId.ToUpper()})", LogTag.Creatures | LogTag.Combat);
        return true;
    }

    // Removed RemoveCombatAction - ActionsQueue AddAction handles replacement

    private void RegisterAttack(ICreature attacker, BattlefieldSlot targetSlot) {
         string attackerId = attacker.TargetId;
        attackingCreatureIds.Add(attackerId);
        targetedSlotsById[attackerId] = targetSlot;
    }

    private void QueueCombatAction(ICreature attackerCreature, ITarget targetSlot) {
        // Use injected ActionsQueue via IGameManager
        _gameManager.ActionsQueue?.AddAction(new BattlefieldCombatAction(attackerCreature, targetSlot));
        // Log($"Queued BattlefieldCombatAction: {attackerCreature.Name} (ID: {attackerCreature.TargetId.ToUpper()}) targets slot {targetSlot?.TargetId.ToUpper()}", LogTag.Creatures | LogTag.Combat | LogTag.Actions);

        // Mark action attempted if needed (though this might be better in the action execution itself)
        // _gameManager.ActionsQueue?.MarkEffectProcessed(attackerCreature.TargetId, EffectTrigger.ActionAttempted);

        // Mediator notification happens via ActionsQueue events now
        // _gameManager.GameMediator?.NotifyActionsQueueChanged(); // REMOVED
    }

    public void ResetAttackingCreatures() {
        attackingCreatureIds.Clear();
        targetedSlotsById.Clear();
        Log("Reset attacking creatures tracking", LogTag.Creatures | LogTag.Combat);
    }

    // Updated to use ID
    public bool HasCreatureAttacked(ITarget creature) {
        return creature != null && attackingCreatureIds.Contains(creature.TargetId);
    }

    // Updated to use ID
    public BattlefieldSlot GetTargetedSlot(ITarget attacker) {
        if (attacker == null) return null;
        return targetedSlotsById.TryGetValue(attacker.TargetId, out var slot) ? slot : null;
    }
}