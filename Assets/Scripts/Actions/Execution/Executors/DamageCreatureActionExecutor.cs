using System;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class DamageCreatureActionExecutor : ActionExecutor<DamageCreatureAction> {
        protected override void ExecuteAction(DamageCreatureAction damageAction, ActionExecutionContext context) {
            var targetCreature = damageAction.GetTarget();
            var damage = Abilities.OutgoingDamage(damageAction, context.ModifierManager); // Underground, Penny Stock, Ambush
            var attacker = damageAction.GetAttacker(); // Can be null

            if (targetCreature == null || targetCreature.IsDead) return;
            if (targetCreature.Slot == null) {
                Log($"Executor: {targetCreature.Name} isn't in play any more: the damage fizzles", LogTag.Actions | LogTag.Creatures);
                return;
            }

            var mediator = context.GameMediator;
            var modifierManager = context.ModifierManager;
            if (mediator == null || modifierManager == null || context.ActionsQueue == null) {
                LogError($"DamageCreatureActionExecutor: Missing dependencies in context for {targetCreature.Name}.", LogTag.Actions | LogTag.Creatures | LogTag.Initialization);
                return;
            }

            // Shield: the creature on it takes no combat damage
            if (damageAction.IsCombatDamage() && SlotEffectRules.BlocksCombatDamage(targetCreature, context.ModifierManager)) {
                Log($"Executor: {targetCreature.Name} is on a Shield: no combat damage", LogTag.Actions | LogTag.Combat);
                return;
            }

            // Untouchable: no damage at all, from anything
            if (modifierManager.HasStatusEffect(targetCreature, StatusEffectType.Untouchable)) {
                Log($"Executor: {targetCreature.Name} is Untouchable: no damage", LogTag.Actions | LogTag.Combat);
                return;
            }

            // Exposed (part of Exhausted): +1 damage from every source
            if (damage > 0 && modifierManager.IsExposed(targetCreature)) {
                damage += 1;
            }

            Log($"Executor: Intent to deal {damage} damage to {targetCreature.Name} (Armor: {targetCreature.CurrentArmorPool}) from {attacker?.Name ?? "source"}", LogTag.Actions | LogTag.Creatures | LogTag.Combat);

            int damageToArmor = Abilities.IgnoresArmor(attacker) ? 0 : Math.Min(damage, targetCreature.CurrentArmorPool); // Insider Job
            int remainingDamage = damage - damageToArmor;

            // Armor is spent immediately so the next hit in the same resolution sees what is left
            if (damageToArmor > 0) {
                targetCreature.ModifyArmorPool(-damageToArmor);
                mediator.NotifyCreatureArmorChanged(targetCreature, targetCreature.CurrentArmorPool);
            }

            // Survivor (Survivorship Bias): from full Health, a hit that would kill it leaves it at 1, and the status is spent
            if (remainingDamage > 0 && remainingDamage >= targetCreature.Health && targetCreature.Health >= targetCreature.MaxHealth) {
                var survivor = StatusRules.Find(modifierManager, targetCreature, StatusEffectType.Survivor);
                if (survivor != null) {
                    modifierManager.RemoveModifier(targetCreature, survivor);
                    remainingDamage = targetCreature.Health - 1;
                    Log($"Executor: {targetCreature.Name} survives the hit at 1 Health (Survivor)", LogTag.Actions | LogTag.Combat);
                }
            }

            int actualHealthDamage = 0;
            if (remainingDamage > 0 && !targetCreature.IsDead) {
                Log($"Executor: Applying {remainingDamage} remaining damage to health.", LogTag.Actions | LogTag.Creatures | LogTag.Combat);
                actualHealthDamage = targetCreature.TakeHealthDamage(remainingDamage);
            }

            if (actualHealthDamage > 0) {
                mediator.NotifyCreatureDamaged(targetCreature, actualHealthDamage);
            }

            // Collateral Damage: what its attack has left once the creature's Health is gone goes on to that creature's player
            int overflow = remainingDamage - actualHealthDamage;
            if (overflow > 0 && damageAction.IsCombatDamage() && Abilities.Has(attacker, CreatureAbility.CollateralDamage) && targetCreature.Owner != null) {
                Log($"Executor: {overflow} damage goes through {targetCreature.Name} to {targetCreature.Owner.Name}", LogTag.Actions | LogTag.Combat);
                context.ActionsQueue.AddAction(new DamagePlayerAction(targetCreature.Owner, overflow, attacker, isCombatDamage: true));
            }

            // Creature Death Sequence (GDD 3.9): OnDamage fires on every hit that did damage, Armor included, even a killing one,
            // and resolves right away. Then the lethal check, which may lead to the creature's destruction.
            // Only an attack is answered (Counterattack, Retaliate): hitting back isn't an attack, so it's never answered in turn
            if (damageToArmor + actualHealthDamage > 0) {
                TriggerEffects(targetCreature, EffectTrigger.OnDamage, damageAction.IsCombatDamage() ? attacker : null, context);
            }
            if (targetCreature.Health <= 0) {
                context.ActionsQueue.AddAction(new CheckLethalAction(targetCreature, attacker));
            }

            Log($"Executed DamageCreatureAction via Executor for {targetCreature.Name}", LogTag.Actions | LogTag.Creatures | LogTag.Combat);
        }

        // The attacker is passed along so effects can answer its attack
        private static void TriggerEffects(ICreature creature, EffectTrigger trigger, ICreature attacker, ActionExecutionContext context) {
            Log($"Executor: Triggering {trigger} effects for {creature.Name}", LogTag.Effects | LogTag.Actions);
            creature.HandleEffect(trigger, context.ActionsQueue, attacker);
        }
    }
}
