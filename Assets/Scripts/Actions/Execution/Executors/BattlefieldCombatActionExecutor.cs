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

            // The attack was declared earlier; re-check the attacker can still act now that it resolves.
            // Last Rites only needs the attacker alive on the board.
            bool canAttack = combatAction.IsLastRites ? !attacker.IsDead && attacker.Slot != null : context.ModifierManager.CanAct(attacker);
            if (!canAttack) {
                Log($"BattlefieldCombatActionExecutor: {attacker.Name}'s attack cancelled.", LogTag.Actions | LogTag.Combat);
                return;
            }

            // 1. Primary damage: the creature in the slot, or the opponent when the slot is empty
            ITarget primaryTarget = null;
            if (targetSlot != null) {
                primaryTarget = targetSlot.IsOccupied() ? (ITarget)targetSlot.OccupyingCreature : attacker.Owner?.Opponent;
            }
            if (primaryTarget == null) {
                Log($"Executed BattlefieldCombatAction via Executor for {attacker.Name}", LogTag.Actions | LogTag.Combat);
                return;
            }

            Log($"Executor: Creature {attacker.Name} attacking {DescribeTarget(primaryTarget)}", LogTag.Combat);

            // Extreme Prejudice: the hit destroys the creature outright, then the assassin is gone too. What stops a hit
            // (Untouchable, a Temporal Shield) stops it; Health and Armor don't
            if (Abilities.Has(attacker, Enums.CreatureAbility.ExtremePrejudice) && primaryTarget is ICreature victim) {
                if (context.ModifierManager.HasStatusEffect(victim, Enums.StatusEffectType.Untouchable) || SlotEffectRules.BlocksCombatDamage(victim)) {
                    Log($"Executor: {attacker.Name} can't touch {victim.Name}", LogTag.Combat);
                    return;
                }
                Log($"Executor: {attacker.Name} takes {victim.Name} out", LogTag.Combat);
                actionsQueue.AddAction(new DestroyCreatureAction(victim, attacker, unconditional: true, controller: attacker.Owner));
                actionsQueue.AddAction(new DestroyCreatureAction(attacker, unconditional: true));
                return;
            }
            QueueCombatDamage(actionsQueue, primaryTarget, attacker);
            // Double Tap: a second hit right after the first, on the same target
            if (Abilities.Has(attacker, Enums.CreatureAbility.DoubleTap)) QueueCombatDamage(actionsQueue, primaryTarget, attacker);
            // Spray and Pray: the creatures beside the target slot are hit too, each for its attack (a cone of three)
            if (Abilities.Has(attacker, Enums.CreatureAbility.SprayAndPray) && targetSlot?.Owner != null) {
                var row = targetSlot.Owner.Battlefield;
                foreach (int i in new[] { targetSlot.Index - 1, targetSlot.Index + 1 }) {
                    var beside = i >= 0 && i < row.Count ? row[i].OccupyingCreature : null;
                    if (beside != null && !Abilities.Defends(beside, Enums.CreatureAbility.TooMainstream, attacker)) QueueCombatDamage(actionsQueue, beside, attacker);
                }
            }
            // Chain Mail: the hit jumps on to the next creatures in line, at half the damage each jump
            if (Abilities.Has(attacker, Enums.CreatureAbility.ChainMail)) {
                EffectResolver.QueueChain(attacker, primaryTarget, (attacker.Attack + Abilities.AttackBonus(attacker)) / 2, actionsQueue);
            }

            // Hive Mind: whoever attacks it catches it, losing its own ability until its card is reshuffled
            if (primaryTarget is ICreature defender && Abilities.Defends(defender, Enums.CreatureAbility.HiveMind, attacker)
                && attacker.Ability != Enums.CreatureAbility.HiveMind) {
                Log($"{attacker.Name} attacked {defender.Name} and joins the Hive Mind (was {attacker.Ability})", LogTag.Combat | LogTag.Effects);
                attacker.Ability = Enums.CreatureAbility.HiveMind;
                context.GameMediator.NotifyCreatureStatsChanged(attacker);
            }

            // 2. The attacker's chain and spread add-ons
            EffectResolver.QueueAttackEffects(attacker, primaryTarget, actionsQueue);

            // 3. Recoil: the attack costs it 1 damage, after the hit (no attacker: nothing answers it)
            if (Abilities.Has(attacker, Enums.CreatureAbility.Recoil)) {
                actionsQueue.AddAction(new DamageCreatureAction(attacker, 1));
            }

            Log($"Executed BattlefieldCombatAction via Executor for {attacker.Name}", LogTag.Actions | LogTag.Combat);
        }

        // The hit deals the attacker's attack as it is when the hit lands: a buff or debuff that lands in between counts
        private static void QueueCombatDamage(IActionsQueue actionsQueue, ITarget target, ICreature attacker) {
            var damageAction = TargetedActions.AttackersHit(target, attacker);
            if (damageAction == null) return;

            Log($"Executor: Queueing {damageAction}", LogTag.Effects | LogTag.Combat | LogTag.Actions);
            actionsQueue.AddAction(damageAction);
        }

        private static string DescribeTarget(ITarget target) => (target as IEntity)?.Name ?? target?.TargetId;
    }
}
