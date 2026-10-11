using System;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class DamageCreatureActionExecutor : ActionExecutor<DamageCreatureAction> {
        protected override void ExecuteAction(DamageCreatureAction damageAction, ActionExecutionContext context) {
            var targetCreature = damageAction.GetTarget();
            var damage = damageAction.GetDamage();
            var attacker = damageAction.GetAttacker(); // Can be null

            if (targetCreature == null || targetCreature.IsDead) return;

            if (damageAction.IsCombatDamage() && context.IsCombatDamageCancelled(attacker)) {
                Log($"Executor: {attacker.Name} was killed by faster damage; its attack on {targetCreature.Name} is cancelled.", LogTag.Actions | LogTag.Combat);
                return;
            }

            var mediator = context.GameMediator;
            var modifierManager = context.ModifierManager;
            if (mediator == null || modifierManager == null || context.ActionsQueue == null) {
                LogError($"DamageCreatureActionExecutor: Missing dependencies in context for {targetCreature.Name}.", LogTag.Actions | LogTag.Creatures | LogTag.Initialization);
                return;
            }

            Log($"Executor: Intent to deal {damage} damage to {targetCreature.Name} (Armor: {targetCreature.CurrentArmorPool}) from {attacker?.Name ?? "source"}", LogTag.Actions | LogTag.Creatures | LogTag.Combat);

            int damageToArmor = Math.Min(damage, targetCreature.CurrentArmorPool);
            int remainingDamage = damage - damageToArmor;

            // Armor is spent immediately so the next hit in the same resolution sees what is left
            if (damageToArmor > 0) {
                targetCreature.ModifyArmorPool(-damageToArmor);
                mediator.NotifyCreatureArmorChanged(targetCreature, targetCreature.CurrentArmorPool);
            }

            int actualHealthDamage = 0;
            if (remainingDamage > 0 && !targetCreature.IsDead) {
                Log($"Executor: Applying {remainingDamage} remaining damage to health.", LogTag.Actions | LogTag.Creatures | LogTag.Combat);
                actualHealthDamage = targetCreature.TakeHealthDamage(remainingDamage);
            }

            if (actualHealthDamage > 0) {
                mediator.NotifyCreatureDamaged(targetCreature, actualHealthDamage);
            }

            // OnDamage effects only fire while the creature survives the hit; a killing blow fires OnDeath instead.
            // (IsDead is only set below, so health is what tells whether this hit was lethal.)
            if (actualHealthDamage > 0 && targetCreature.Health > 0) {
                TriggerEffects(targetCreature, EffectTrigger.OnDamage, attacker, context);
            }

            if (targetCreature.Health <= 0 && !targetCreature.IsDead) {
                Log($"Executor: {targetCreature.Name} died as a result of damage.", LogTag.Creatures | LogTag.Combat | LogTag.Actions);
                targetCreature.MarkAsDead();
                // Same speed the queue used to order this action (see ActionsQueue.GetActionSpeed)
                context.KillingSpeedByCreatureId[targetCreature.TargetId] = attacker?.Speed ?? 1;

                mediator.NotifyCreatureDied(targetCreature);
                TriggerEffects(targetCreature, EffectTrigger.OnDeath, attacker, context);

                modifierManager.UnregisterCreature(targetCreature);
                targetCreature.Owner?.DiscardCard(targetCreature);
            }

            Log($"Executed DamageCreatureAction via Executor for {targetCreature.Name}", LogTag.Actions | LogTag.Creatures | LogTag.Combat);
        }

        // The attacker is passed along so effects can retaliate against it
        private static void TriggerEffects(ICreature creature, EffectTrigger trigger, ICreature attacker, ActionExecutionContext context) {
            Log($"Executor: Triggering {trigger} effects for {creature.Name}", LogTag.Effects | LogTag.Actions);
            creature.HandleEffect(trigger, context.ActionsQueue, attacker);
        }
    }
}
