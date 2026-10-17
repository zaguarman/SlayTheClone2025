using System.Linq;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    public class BattlefieldCombatActionExecutor : ActionExecutor<BattlefieldCombatAction> {
        protected override void ExecuteAction(BattlefieldCombatAction combatAction, ActionExecutionContext context) {
            var attacker = combatAction.GetAttacker();
            var targetSlot = combatAction.GetTargetSlot();
            if (EffectResolver.IsHoming(attacker) && attacker != null) {
                // Against a Concealed (or Undercover) mark, Homing works like Fixed: the slot it was aimed at
                var locked = combatAction.Mark;
                if (locked != null && (context.ModifierManager.HasStatusEffect(locked, Enums.StatusEffectType.Concealed) || Abilities.Has(locked, Enums.CreatureAbility.Undercover)))
                    targetSlot = combatAction.AimedSlot;
                // Heat Signature pulls a Homing attack to the hottest creature on either side (not itself)
                if (HeatRules.Hottest(attacker, attacker.Owner, new[] { attacker }) is ICreature hottest && hottest.Slot != null) targetSlot = hottest.Slot;
            }

            if (attacker == null) {
                LogError("BattlefieldCombatActionExecutor: Attacker is null.", LogTag.Actions | LogTag.Combat);
                return;
            }
            // Provoked (Taunt, Follow Me): whatever it aimed at, it attacks the slot facing it
            if (context.ModifierManager.HasStatusEffect(attacker, Enums.StatusEffectType.Provoked) && attacker.Slot != null && !combatAction.IsLastRites) {
                var across = attacker.Owner?.Opponent?.Battlefield;
                if (across != null && attacker.Slot.Index < across.Count) targetSlot = across[attacker.Slot.Index];
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
            // Autopilot (Sleepwalker): Exhausted, it still attacks (read off the printed card: Exhausted switches abilities off)
            bool autopilot = attacker.Ability == Enums.CreatureAbility.Autopilot && attacker.IsOperative && !attacker.IsDead && attacker.Slot != null
                && context.ModifierManager.HasStatusEffect(attacker, Enums.StatusEffectType.Exhausted);
            bool canAttack = combatAction.IsLastRites ? !attacker.IsDead && attacker.Slot != null : context.ModifierManager.CanAct(attacker) || autopilot;
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

            // Next Question (the user, 2026-10-17: the politician sidestepping the uncomfortable question): once a Day, as an
            // attack is about to hit it, it steps into an empty slot of its side (one beside it if it can) and the attack hits nothing
            if (primaryTarget is ICreature dodger && !combatAction.IsLastRites && Abilities.Defends(dodger, Enums.CreatureAbility.NextQuestion, attacker)
                && Dodge(dodger, context)) return;

            // Sucker Punch: it went first to beat the creature it faces to the punch; if that creature planned no attack, it
            // holds back (Pokémon's Sucker Punch fails on a target that isn't attacking)
            if (Abilities.Has(attacker, Enums.CreatureAbility.SuckerPunch) && primaryTarget is ICreature punched && !combatAction.IsLastRites
                && !actionsQueue.GetPendingActions().OfType<BattlefieldCombatAction>().Any(a => a.GetAttacker() == punched)) {
                Log($"Executor: {attacker.Name} holds back: {punched.Name} planned no attack (Sucker Punch)", LogTag.Combat);
                return;
            }

            Log($"Executor: Creature {attacker.Name} attacking {DescribeTarget(primaryTarget)}", LogTag.Combat);

            // Licensed (Licence to Kill): this attack destroys the creature it hits, whatever its Health or Armor, and the
            // licence is spent. What stops a hit (Untouchable, a Shield) stops it, and spends it all the same. With
            // Double Tap both hits carry it: a Blessing saves the target from one of them, two Blessings from both
            var licence = primaryTarget is ICreature ? StatusRules.Find(context.ModifierManager, attacker, Enums.StatusEffectType.Licensed) : null;
            if (licence != null && primaryTarget is ICreature mark) {
                context.ModifierManager.RemoveModifier(attacker, licence);
                if (context.ModifierManager.HasStatusEffect(mark, Enums.StatusEffectType.Untouchable) || SlotEffectRules.BlocksCombatDamage(mark, context.ModifierManager)) {
                    Log($"Executor: {attacker.Name}'s licence is spent on {mark.Name}, who can't be touched", LogTag.Combat);
                    return;
                }
                Log($"Executor: {attacker.Name} uses its licence to kill on {mark.Name}", LogTag.Combat);
                actionsQueue.AddAction(new DestroyCreatureAction(mark, attacker, unconditional: true, controller: attacker.Owner));
                if (Abilities.Has(attacker, Enums.CreatureAbility.DoubleTap)) {
                    actionsQueue.AddAction(new DestroyCreatureAction(mark, attacker, unconditional: true, controller: attacker.Owner));
                }
                return;
            }

            // Extreme Prejudice: the hit destroys the creature outright, then the assassin is gone too. What stops a hit
            // (Untouchable, a Shield) stops it; Health and Armor don't
            if (Abilities.Has(attacker, Enums.CreatureAbility.ExtremePrejudice) && primaryTarget is ICreature victim) {
                if (context.ModifierManager.HasStatusEffect(victim, Enums.StatusEffectType.Untouchable) || SlotEffectRules.BlocksCombatDamage(victim, context.ModifierManager)) {
                    Log($"Executor: {attacker.Name} can't touch {victim.Name}", LogTag.Combat);
                    return;
                }
                Log($"Executor: {attacker.Name} takes {victim.Name} out", LogTag.Combat);
                actionsQueue.AddAction(new DestroyCreatureAction(victim, attacker, unconditional: true, controller: attacker.Owner));
                actionsQueue.AddAction(new DestroyCreatureAction(attacker, unconditional: true));
                return;
            }
            // Strip Search: a creature with any status or stat change is Purged first (good and bad alike), and hit 2 harder
            if (Abilities.Has(attacker, Enums.CreatureAbility.StripSearch) && primaryTarget is ICreature searched && IsDressedUp(searched, context.ModifierManager)) {
                Log($"Executor: {attacker.Name} strip-searches {searched.Name}", LogTag.Combat);
                actionsQueue.AddAction(new CleanseAction(searched, Enums.CleanseTarget.Both, purge: true));
                QueueCombatDamage(actionsQueue, primaryTarget, attacker);
                actionsQueue.AddAction(new DamageCreatureAction(searched, Abilities.StripBonus, attacker));
            }
            // Wild Card (the user, 2026-10-17, Pokémon's Metronome: Florida Man): its attack on a creature does any one thing
            else if (Abilities.Has(attacker, Enums.CreatureAbility.WildCard) && primaryTarget is ICreature wild && !combatAction.IsLastRites) WildCardHit(actionsQueue, wild, attacker);
            else QueueCombatDamage(actionsQueue, primaryTarget, attacker);
            // Taser (an adventure's relic): a creature that attacks one of its carrier's takes 1 damage back
            if (primaryTarget is ICreature wired && wired.Owner != null && wired.Owner != attacker.Owner && wired.Owner.Relics.Contains(Relic.Taser)) {
                actionsQueue.AddAction(new DamageCreatureAction(attacker, Relics.TaserDamage));
            }
            // Litigious (Legal Department, Pokémon's Rocky Helmet): whoever attacks it gets hurt
            if (primaryTarget is ICreature sued && Abilities.Defends(sued, Enums.CreatureAbility.Litigious, attacker)) {
                actionsQueue.AddAction(new DamageCreatureAction(attacker, Abilities.LitigiousDamage));
            }
            // Pharaoh's Curse (Mummy, Pokémon's Perish Body): a creature that attacks it is Doomed 3, and so is the mummy
            if (primaryTarget is ICreature tomb && Abilities.Defends(tomb, Enums.CreatureAbility.PharaohsCurse, attacker)) {
                foreach (var cursed in new[] { attacker, tomb }) {
                    if (!context.ModifierManager.HasStatusEffect(cursed, Enums.StatusEffectType.Doomed))
                        actionsQueue.AddAction(new ApplyStatusEffectAction(cursed, Enums.StatusEffectType.Doomed, 3, 0, tomb, tomb.Owner));
                }
            }
            // Backup Dancer: a friendly creature beside the attacker that planned nothing attacks the same slot, once a Day
            if (attacker.Slot?.Owner != null && targetSlot != null && !combatAction.IsLastRites) {
                var row = attacker.Slot.Owner.Battlefield;
                foreach (int i in Formation.Neighbours(attacker.Slot.Index)) {
                    var dancer = i < row.Count ? row[i].OccupyingCreature : null;
                    string key = dancer != null ? $"{dancer.CopyId}#dance#{context.TurnClock.TurnNumber}" : null;
                    if (dancer == null || !Abilities.Has(dancer, Enums.CreatureAbility.BackupDancer) || actionsQueue.HasActiveAction(dancer.TargetId)
                        || !context.ModifierManager.CanAct(dancer) || dancer.Owner.UsedOncePerCombat.Contains(key)) continue;
                    dancer.Owner.UsedOncePerCombat.Add(key);
                    Log($"{dancer.Name} dances along: it attacks slot {targetSlot.Index} with {attacker.Name}", LogTag.Combat | LogTag.Effects);
                    actionsQueue.AddAction(new BattlefieldCombatAction(dancer, targetSlot));
                }
            }
            // Fixated (One-Trick Pony, the Choice items): from its first attack on, it repeats that attack every Day
            if (Abilities.Has(attacker, Enums.CreatureAbility.Fixated) && !combatAction.IsLastRites && !actionsQueue.IsLooping(attacker)) {
                actionsQueue.StartLoop(attacker, combatAction);
            }
            // Double Tap: a second hit right after the first, on the same target
            if (Abilities.Has(attacker, Enums.CreatureAbility.DoubleTap)) QueueCombatDamage(actionsQueue, primaryTarget, attacker);
            // Spray and Pray: the creatures beside the target slot (a step away in the formation) are hit too, each for its attack
            if (Abilities.Has(attacker, Enums.CreatureAbility.SprayAndPray) && targetSlot?.Owner != null) {
                var row = targetSlot.Owner.Battlefield;
                foreach (int i in Formation.Neighbours(targetSlot.Index)) {
                    var beside = i >= 0 && i < row.Count ? row[i].OccupyingCreature : null;
                    if (beside != null && !Abilities.ShrugsOffArea(beside, attacker, context.ModifierManager)) QueueCombatDamage(actionsQueue, beside, attacker);
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

            // Hit and Run: after its attack, back to the hand with the next Day's, fresh (U-turn)
            if (Abilities.Has(attacker, Enums.CreatureAbility.HitAndRun) && !combatAction.IsLastRites) {
                actionsQueue.AddAction(new ReturnToHandAction(attacker));
            }

            Log($"Executed BattlefieldCombatAction via Executor for {attacker.Name}", LogTag.Actions | LogTag.Combat);
        }

        // Next Question: into an empty slot beside it, or any empty slot of its side; once a Day; a Rooted one can't
        private static bool Dodge(ICreature dodger, ActionExecutionContext context) {
            var from = dodger.Slot;
            var row = from?.Owner?.Battlefield;
            if (row == null || dodger.Owner == null || MovementRules.IsRooted(dodger, context.ModifierManager)) return false;
            string key = $"{dodger.CopyId}#dodge#{context.TurnClock.TurnNumber}";
            if (dodger.Owner.UsedOncePerCombat.Contains(key)) return false;
            var to = Formation.Neighbours(from.Index).Where(i => i < row.Count).Select(i => row[i]).FirstOrDefault(s => !s.IsOccupied())
                     ?? row.FirstOrDefault(s => !s.IsOccupied());
            if (to == null) return false;
            dodger.Owner.UsedOncePerCombat.Add(key);
            Log($"Next question: {dodger.Name} sidesteps into slot {to.Index}, and the attack hits nothing", LogTag.Combat | LogTag.Effects);
            Maneuvers.Trade(from, to, context);
            context.GameMediator.NotifyBattlefieldStateChanged(from.Owner);
            return true;
        }

        // Wild Card: twice, a Burn instead, a stun instead, or nothing at all (it got distracted)
        private static void WildCardHit(IActionsQueue actionsQueue, ICreature target, ICreature attacker) {
            int roll = GameRandom.Next(4);
            Log($"Wild card: {attacker.Name} rolls {roll} on {target.Name}", LogTag.Combat | LogTag.Effects);
            switch (roll) {
                case 0:
                    QueueCombatDamage(actionsQueue, target, attacker);
                    QueueCombatDamage(actionsQueue, target, attacker);
                    break;
                case 1:
                    actionsQueue.AddAction(new ApplyStatusEffectAction(target, Enums.StatusEffectType.Burned, 2, 2, attacker, attacker.Owner));
                    break;
                case 2:
                    actionsQueue.AddAction(new ApplyStatusEffectAction(target, Enums.StatusEffectType.Exhausted, 1, 0, attacker, attacker.Owner));
                    break;
            }
        }

        // Strip Search's mark: it has a status or a stat change (Temporary Stat Modifications; the printed stats don't count)
        private static bool IsDressedUp(ICreature creature, IModifierManager modifiers) =>
            modifiers.GetActiveModifiersFor(creature).Any(m => m is StatusEffectModifier || m is StatModifier);

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
