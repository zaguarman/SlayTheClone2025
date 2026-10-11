using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class BattlefieldCombatActionExecutor : ActionExecutor<BattlefieldCombatAction> {
        protected override void ExecuteAction(BattlefieldCombatAction combatAction, ActionExecutionContext context) {
            var attacker = combatAction.GetAttacker();
            var targetSlot = combatAction.GetTargetSlot();

            if (attacker == null) {
                LogError("BattlefieldCombatActionExecutor: Attacker is null.", LogTag.Actions | LogTag.Combat);
                return;
            }
            if (targetSlot == null) {
                // Treated as a cancelled or invalid attack: nothing is hit
                Log($"BattlefieldCombatActionExecutor: TargetSlot is null for attacker {attacker.Name}. Assuming cancellation or invalid target.", LogTag.Actions | LogTag.Combat);
            }

            var actionsQueue = context.ActionsQueue;
            if (actionsQueue == null) {
                LogError("BattlefieldCombatActionExecutor: ActionsQueue is null in context.", LogTag.Actions | LogTag.Combat);
                return;
            }

            // The attack was declared earlier; re-check the attacker can still act now that it resolves
            if (!context.ModifierManager.CanAct(attacker)) {
                Log($"BattlefieldCombatActionExecutor: {attacker.Name}'s attack cancelled.", LogTag.Actions | LogTag.Combat);
                return;
            }

            // 1. Primary damage: the creature in the slot, or the opponent when the slot is empty
            int primaryDamage = attacker.Attack;
            ITarget primaryTarget = null;
            if (targetSlot != null) {
                primaryTarget = targetSlot.IsOccupied() ? (ITarget)targetSlot.OccupyingCreature : attacker.Owner?.Opponent;
            }
            if (primaryTarget == null) {
                Log($"Executed BattlefieldCombatAction via Executor for {attacker.Name}", LogTag.Actions | LogTag.Combat);
                return;
            }

            Log($"Executor: Creature {attacker.Name} attacking {DescribeTarget(primaryTarget)}", LogTag.Combat);
            QueueCombatDamage(actionsQueue, primaryTarget, primaryDamage, attacker);

            // 2. The attacker's chain and spread add-ons
            EffectResolver.QueueAttackEffects(attacker, primaryTarget, actionsQueue);

            Log($"Executed BattlefieldCombatAction via Executor for {attacker.Name}", LogTag.Actions | LogTag.Combat);
        }

        private static void QueueCombatDamage(IActionsQueue actionsQueue, ITarget target, int damage, ICreature attacker) {
            var damageAction = TargetedActions.Damage(target, damage, attacker, isCombatDamage: true);
            if (damageAction == null) return;

            Log($"Executor: Queueing {damageAction.GetType().Name}: Target={DescribeTarget(target)}, Damage={damage}, Attacker={attacker.Name}", LogTag.Effects | LogTag.Combat | LogTag.Actions);
            actionsQueue.AddAction(damageAction);
        }

        private static string DescribeTarget(ITarget target) => (target as IEntity)?.Name ?? target?.TargetId;
    }
}
