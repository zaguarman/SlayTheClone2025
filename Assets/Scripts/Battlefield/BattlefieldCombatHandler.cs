using System.Collections.Generic;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    public class BattlefieldCombatHandler : IBattlefieldCombatHandler {
        private readonly IActionsQueue actionsQueue;
        private readonly IModifierManager modifierManager;
        private HashSet<string> attackingCreatureIds = new HashSet<string>(); // Store IDs
        private readonly Dictionary<string, BoardSlot> targetedSlotsById = new Dictionary<string, BoardSlot>(); // Store by ID

        public BattlefieldCombatHandler(IActionsQueue actionsQueue, IModifierManager modifierManager) {
            this.actionsQueue = actionsQueue ?? throw new System.ArgumentNullException(nameof(actionsQueue));
            this.modifierManager = modifierManager ?? throw new System.ArgumentNullException(nameof(modifierManager));
            // Declared attacks are spent once the queue has resolved them
            actionsQueue.OnActionsResolved.AddListener(ResetAttackingCreatures);
        }

        public void HandleCreatureCombat(ICreature attackerCreature, BoardSlot targetSlot) {
            if (attackerCreature == null) {
                LogWarning("Attacker creature is null", LogTag.Creatures | LogTag.Combat);
                return;
            }

            if (targetSlot == null) {
                LogWarning("Target slot is null", LogTag.Creatures | LogTag.Combat);
                return;
            }

            if (!modifierManager.CanAct(attackerCreature)) return;
            if (!Abilities.CanAttack(attackerCreature)) {
                Log($"{attackerCreature.Name} can't attack (Bodyguard)", LogTag.Creatures | LogTag.Combat);
                return;
            }
            // Check if the target is valid (basic check, can be expanded)
            bool isValidTarget = targetSlot != null; // Basic check

            string attackerId = attackerCreature.TargetId;
            bool hasAttacked = HasCreatureAttacked(attackerCreature); // Use interface method

            if (hasAttacked) {
                // If the creature has already attacked, update or cancel the attack
                if (isValidTarget) {
                    UpdateAttackAction(attackerCreature, targetSlot);
                }
                else {
                    CancelAttack(attackerCreature);
                }
            }
            else {
                // First time attacking, register if target is valid
                if (isValidTarget) {
                    RegisterAttack(attackerCreature, targetSlot);
                    QueueCombatAction(attackerCreature, targetSlot);
                }
                else {
                    Log($"Invalid target selected for {attackerCreature.Name} (TargetID: {attackerId.ToUpper()})", LogTag.Creatures | LogTag.Combat);
                }
            }
        }

        private void UpdateAttackAction(ICreature attackerCreature, BoardSlot newTargetSlot) {
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

            if (!(actionsQueue.GetActiveAction(attackerId) is BattlefieldCombatAction)) return false;

            actionsQueue.CancelDeclaredAction(attackerId);
            Log($"Cancelled attack for {attackerCreature.Name} (ID: {attackerId.ToUpper()})", LogTag.Creatures | LogTag.Combat);
            return true;
        }

        private void RegisterAttack(ICreature attacker, BoardSlot targetSlot) {
            string attackerId = attacker.TargetId;
            attackingCreatureIds.Add(attackerId);
            targetedSlotsById[attackerId] = targetSlot;
        }

        private void QueueCombatAction(ICreature attackerCreature, BoardSlot targetSlot) {
            actionsQueue.AddAction(new BattlefieldCombatAction(attackerCreature, targetSlot));
            // Mediator notification happens via ActionsQueue events
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
        public BoardSlot GetTargetedSlot(ITarget attacker) {
            if (attacker == null) return null;
            return targetedSlotsById.TryGetValue(attacker.TargetId, out var slot) ? slot : null;
        }
    }
}
