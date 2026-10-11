using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class BattlefieldCombatActionExecutor : ActionExecutor<BattlefieldCombatAction> {
        private const int MaxChainJumps = 2;

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
            if (attacker.IsDead) {
                Log($"BattlefieldCombatActionExecutor: {attacker.Name} died before its attack resolved. Attack cancelled.", LogTag.Actions | LogTag.Combat);
                return;
            }
            if (attacker is Creature concreteAttacker && context.ModifierManager != null && context.ModifierManager.AreActionsPrevented(concreteAttacker)) {
                Log($"BattlefieldCombatActionExecutor: {attacker.Name} cannot act due to a status effect. Attack cancelled.", LogTag.Actions | LogTag.Combat | LogTag.Effects);
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

            // 2. The attacker's chain and spread effects, triggered when it deals damage
            if (attacker is Creature creatureAttacker) {
                var onDamageEffects = creatureAttacker.Effects
                    .Where(e => e.trigger == EffectTrigger.OnDamage || e.trigger == EffectTrigger.OnPlay)
                    .SelectMany(e => e.actions)
                    .ToList();
                Log($"Executor: Attacker {attacker.Name} found {onDamageEffects.Count} OnDamage/OnPlay effect actions.", LogTag.Combat | LogTag.Effects);

                foreach (var effectAction in onDamageEffects) {
                    Log($"Executor: Checking effect action -> Type={effectAction.actionType}, Modifier={effectAction.targetModifier}, Value={effectAction.value}", LogTag.Combat | LogTag.Effects);
                    // The effect's value replaces the attack damage when it is set
                    int effectDamage = effectAction.value > 0 ? effectAction.value : primaryDamage;

                    if (effectAction.targetModifier.HasFlag(TargetModifier.Chained)) {
                        QueueChain(actionsQueue, attacker, primaryTarget, effectDamage);
                    }
                    else if (effectAction.targetModifier != TargetModifier.None && effectDamage > 0) {
                        QueueSpread(actionsQueue, attacker, primaryTarget, effectAction.targetModifier, effectDamage);
                    }
                    // Effects without modifiers (like simple bonus damage) are handled as ModifyStat or other effect actions
                }
            }

            Log($"Executed BattlefieldCombatAction via Executor for {attacker.Name}", LogTag.Actions | LogTag.Combat);
        }

        // Jumps from the creature that was hit to its neighbors, halving the damage on each jump
        private static void QueueChain(IActionsQueue actionsQueue, ICreature attacker, ITarget primaryTarget, int chainDamage) {
            if (!(primaryTarget is ICreature hitCreature)) {
                LogWarning($"Executor: Cannot apply Chained effect from {attacker.Name} because primary target was not a creature.", LogTag.Combat | LogTag.Effects);
                return;
            }

            Log($"Executor: Attacker {attacker.Name} applying CHAIN effect (Value: {chainDamage}) starting from {hitCreature.Name}", LogTag.Combat | LogTag.Effects);
            var chainTargets = TargetingSystem.GetDirectionalChainTargets(hitCreature, MaxChainJumps);
            Log($"Executor: Found {chainTargets.Count} chain targets.", LogTag.Combat | LogTag.Effects);

            int currentDamage = chainDamage;
            foreach (var chainTarget in chainTargets) {
                if (currentDamage <= 0) break;
                QueueCombatDamage(actionsQueue, chainTarget, currentDamage, attacker);
                currentDamage /= 2;
            }
        }

        private static void QueueSpread(IActionsQueue actionsQueue, ICreature attacker, ITarget primaryTarget, TargetModifier modifier, int spreadDamage) {
            Log($"Executor: Attacker {attacker.Name} applying SPREAD effect (Modifier: {modifier}, Value: {spreadDamage})", LogTag.Combat | LogTag.Effects);
            var spreadTargets = TargetingSystem.GetAdditionalTargets(attacker.Owner, primaryTarget, modifier);
            Log($"Executor: Found {spreadTargets.Count} spread targets for modifier {modifier}.", LogTag.Combat | LogTag.Effects);

            foreach (var spreadTarget in spreadTargets) {
                QueueCombatDamage(actionsQueue, spreadTarget, spreadDamage, attacker);
            }
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
