using System;
using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Creature abilities ('Abilities Compendium'): when one is switched on, Zero-Day's bypass, and the stat-change pipeline
    /// (bypass, block, reflect, transform, apply, react). Each rule that an ability changes asks here.
    /// </summary>
    public static class Abilities {
        // The ones Zero-Day ignores
        private static readonly CreatureAbility[] Defensive = {
            CreatureAbility.Contrarian, CreatureAbility.Extremist, CreatureAbility.Unbothered, CreatureAbility.NoU,
            CreatureAbility.Spite, CreatureAbility.Underground, CreatureAbility.TooMainstream, CreatureAbility.ReturnToSender,
            CreatureAbility.HiveMind
        };

        public static bool IsDefensive(CreatureAbility ability) => Defensive.Contains(ability);

        /// <summary>Active abilities are used as the creature's action for the Day (hold Shift while dragging it onto a creature).</summary>
        public static bool IsActiveAbility(CreatureAbility ability) => ability == CreatureAbility.IdentityTheft;

        public static string NameOf(CreatureAbility ability) => ability switch {
            CreatureAbility.NoU => "No U",
            CreatureAbility.TooMainstream => "Too Mainstream",
            CreatureAbility.ZeroDay => "Zero-Day",
            CreatureAbility.SignalJammer => "Signal Jammer",
            CreatureAbility.IdentityTheft => "Identity Theft",
            CreatureAbility.HiveMind => "Hive Mind",
            CreatureAbility.InsiderJob => "Insider Job",
            CreatureAbility.ReturnToSender => "Return to Sender",
            CreatureAbility.Unpatchable => "Cold Storage",
            CreatureAbility.NickelAndDime => "Nickel-and-Dime",
            _ => ability.ToString()
        };

        /// <summary>Its reminder text, as the card prints it after the name.</summary>
        public static string ReminderOf(CreatureAbility ability) => ability switch {
            CreatureAbility.Contrarian => "stat changes on it are reversed",
            CreatureAbility.Extremist => "stat changes on it are doubled",
            CreatureAbility.Unbothered => "enemy effects can't lower its stats",
            CreatureAbility.NoU => "enemy effects that would lower its stats bounce back",
            CreatureAbility.Spite => "when an effect lowers its stats, friendly or enemy, +2 Attack",
            CreatureAbility.Underground => "in combat, ignores the other creature's stat changes",
            CreatureAbility.TooMainstream => "immune to effects that affect more than one creature",
            CreatureAbility.ZeroDay => "its attacks and effects ignore defensive abilities",
            CreatureAbility.Unpatchable => "enemy effects can't lower its stats, not even with Zero-Day",
            CreatureAbility.SignalJammer => "the enemy creature opposite has its ability switched off",
            CreatureAbility.Copypasta => "when ready and each Dawn, copies the ability of the creature opposite",
            CreatureAbility.IdentityTheft => "Active, instead of attacking or moving: swap abilities with target creature (hold Shift as you drag it)",
            CreatureAbility.HiveMind => "a creature that attacks it loses its ability and gains Hive Mind instead",
            CreatureAbility.InsiderJob => "its damage ignores Armor",
            CreatureAbility.ReturnToSender => "enemy statuses bounce back to whoever sent them",
            CreatureAbility.NickelAndDime => "double damage when it hits for 3 or less",
            CreatureAbility.Undercover => "its plans show as ??? to the enemy",
            CreatureAbility.Handover => "when a summon replaces it, the newcomer takes over its stat changes and statuses",
            CreatureAbility.Recoil => "+2 damage on its attacks; it takes 1 damage after each attack",
            _ => ""
        };

        /// <summary>
        /// Whether the creature's ability is switched on: it has it, it's ready (Deployment Time), no status disables its
        /// abilities, and no Signal Jammer faces it. Switched off isn't removed: it still has the ability.
        /// </summary>
        public static bool IsActive(ICreature creature) =>
            creature != null && creature.Ability != CreatureAbility.None && IsSwitchedOn(creature) && !IsJammed(creature);

        // A Signal Jammer's own ability is judged without other jammers, so two facing each other switch each other off.
        // Scorched Earth switches the ability of the creature standing on it off too.
        private static bool IsSwitchedOn(ICreature creature) =>
            creature.IsOperative && !creature.AbilitiesDisabled && !creature.IsDead && !SlotEffectRules.SuppressesTriggeredEffects(creature);

        /// <summary>Signal Jammer: the enemy creature directly opposite it has its ability switched off.</summary>
        public static bool IsJammed(ICreature creature) {
            var slot = creature?.Slot;
            var opposite = slot?.Owner?.Opponent?.Battlefield;
            if (opposite == null || slot.Index >= opposite.Count) return false;
            var jammer = opposite[slot.Index].OccupyingCreature;
            return jammer != null && jammer.Ability == CreatureAbility.SignalJammer && IsSwitchedOn(jammer);
        }

        /// <summary>Whether the creature has this ability switched on.</summary>
        public static bool Has(ICreature creature, CreatureAbility ability) => creature?.Ability == ability && IsActive(creature);

        /// <summary>
        /// Whether the target's defensive ability works against this source: Zero-Day ignores every defensive ability
        /// (Cold Storage, the Unpatchable ability, isn't one, so it still works).
        /// </summary>
        public static bool Defends(ICreature target, CreatureAbility ability, ICreature source) =>
            Has(target, ability) && !(IsDefensive(ability) && Has(source, CreatureAbility.ZeroDay));

        /// <summary>
        /// "The creature that caused it", for bounced effects: the source creature if it's still in play, otherwise the
        /// caster's creature with the lowest current Health (ties at random). Null if the caster has none (the effect fails).
        /// </summary>
        public static ICreature BounceTarget(ICreature source, IPlayer caster) {
            if (source != null && !source.IsDead && source.Slot != null) return source;
            return caster?.Battlefield.Select(s => s.OccupyingCreature).Where(c => c != null && !c.IsDead)
                .OrderBy(c => c.Health).ThenBy(_ => GameRandom.Next(int.MaxValue)).FirstOrDefault();
        }

        #region Stat changes
        /// <summary>A stat change on its way to a creature, as the 'Abilities Compendium' Resolution Order sees it.</summary>
        public struct StatChange {
            public ICreature Target;
            public int Value;
            public ModifierCalculationType CalcType;
            public ICreature Source;   // The creature whose effect it is, if any
            public IPlayer Controller; // Whose effect it is
            public bool Bounced;       // It was sent back once already and can't be again

            public bool IsEnemy => Controller != null && Target?.Owner != null && Controller != Target.Owner;
            public bool Lowers => Value < 0;
        }

        /// <summary>
        /// Runs a stat change through Bypass, Block, Reflect and Transform. Returns the change to apply (maybe on another
        /// creature, maybe reversed or doubled), or null if it's stopped. Applying it and Spite's reaction are the caller's.
        /// </summary>
        public static StatChange? Resolve(StatChange change) {
            for (int bounces = 0; bounces < 2; bounces++) {
                var target = change.Target;
                if (target == null) return null;

                // 2. Block: enemy effects can't lower its stats
                if (change.IsEnemy && change.Lowers) {
                    if (Has(target, CreatureAbility.Unpatchable) || Defends(target, CreatureAbility.Unbothered, change.Source)) {
                        Log($"{target.Name}'s {target.Ability} stops the stat change", LogTag.Effects);
                        return null;
                    }
                }

                // 3. Reflect: No U sends it back to its source, where it starts again
                if (change.IsEnemy && change.Lowers && !change.Bounced && Defends(target, CreatureAbility.NoU, change.Source)) {
                    var bounceTo = BounceTarget(change.Source, change.Controller);
                    Log($"{target.Name}'s No U sends the stat change back to {bounceTo?.Name ?? "nobody (it fails)"}", LogTag.Effects);
                    if (bounceTo == null) return null;
                    change.Target = bounceTo;
                    change.Bounced = true;
                    continue;
                }

                // 4. Transform: Contrarian reverses it, then Extremist doubles it
                if (Defends(target, CreatureAbility.Contrarian, change.Source)) change.Value = Reverse(change.Value, change.CalcType);
                if (Defends(target, CreatureAbility.Extremist, change.Source)) change.Value = Double(change.Value, change.CalcType);
                return change;
            }
            return null;
        }

        // +2 becomes -2; x2 (+100%) becomes /2 (-50%)
        private static int Reverse(int value, ModifierCalculationType calcType) {
            if (calcType == ModifierCalculationType.Flat) return -value;
            double multiplier = 1 + value / 100.0;
            return multiplier <= 0 ? value : (int)Math.Round((1 / multiplier - 1) * 100);
        }

        // +2 becomes +4; x2 (+100%) becomes x4 (+300%)
        private static int Double(int value, ModifierCalculationType calcType) {
            if (calcType == ModifierCalculationType.Flat) return value * 2;
            double multiplier = 1 + value / 100.0;
            return (int)Math.Round((multiplier * multiplier - 1) * 100);
        }
        #endregion

        #region Copying
        /// <summary>
        /// Copypasta: when ready, and each Dawn while it still has Copypasta, it copies the ability of the creature opposite.
        /// Copying replaces Copypasta. Nothing happens if the slot opposite is empty or that creature has no ability.
        /// </summary>
        public static void TryCopypasta(ICreature creature, IGameMediator mediator) {
            if (!Has(creature, CreatureAbility.Copypasta)) return;
            var opposite = creature.Slot?.Owner?.Opponent?.Battlefield;
            if (opposite == null || creature.Slot.Index >= opposite.Count) return;
            var model = opposite[creature.Slot.Index].OccupyingCreature;
            if (model == null || model.Ability == CreatureAbility.None || model.Ability == CreatureAbility.Copypasta) return;

            Log($"{creature.Name}'s Copypasta copies {model.Name}'s {model.Ability}", LogTag.Effects);
            creature.Ability = model.Ability;
            mediator?.NotifyCreatureStatsChanged(creature);
        }
        #endregion

        #region Damage
        /// <summary>
        /// The damage a hit deals before the target's modifiers (Exposed) and Armor: Underground ignores an attacker's
        /// Temporary Stat Modifications, then Nickel-and-Dime doubles hits of 3 or less.
        /// </summary>
        public static int OutgoingDamage(DamageCreatureAction hit) {
            int damage = hit.GetDamage();
            var attacker = hit.GetAttacker();
            if (hit.DealsAttackersAttack && Defends(hit.GetTarget(), CreatureAbility.Underground, attacker)) {
                damage = attacker.BaseAttack; // Printed Attack and Persistent Stat Modifications only
            }
            // Attacking, it ignores the defender's stat changes too: a Health buff doesn't soak its hit, a Health debuff doesn't add to it
            if (hit.DealsAttackersAttack && Has(attacker, CreatureAbility.Underground) && hit.GetTarget() is ICreature defender) {
                damage = Math.Max(0, damage + (defender.MaxHealth - defender.BaseHealth));
            }
            if (damage > 0 && damage <= 3 && Has(attacker, CreatureAbility.NickelAndDime)) damage *= 2;
            return damage;
        }

        /// <summary>Recoil: its attacks hit 2 harder (it takes 1 damage after each, see BattlefieldCombatActionExecutor).</summary>
        public static int AttackBonus(ICreature attacker) => Has(attacker, CreatureAbility.Recoil) ? 2 : 0;

        /// <summary>
        /// Handover: the creature a summon replaces hands its stat changes (Persistent Stat Modifications, timed and
        /// lasting modifiers) and its statuses to the newcomer. Call before the replaced creature
        /// leaves the board; the returned hand-over is given with <see cref="CompleteHandover"/> once the newcomer is in play.
        /// </summary>
        public static (List<IModifier> modifiers, (int attack, int health, int speed) persistent)? StartHandover(ICreature replaced, IModifierManager modifiers) {
            if (!Has(replaced, CreatureAbility.Handover) || modifiers == null) return null;
            var handed = modifiers.GetActiveModifiersFor(replaced).ToList();
            var persistent = replaced.PersistentChanges;
            replaced.ChangeBaseStats(-persistent.attack, -persistent.health, -persistent.speed); // Passed on: its card goes back to printed
            Log($"{replaced.Name} hands over {handed.Count} modifiers and {persistent} in stat changes", LogTag.Effects | LogTag.Creatures);
            return (handed, persistent);
        }

        public static void CompleteHandover(ICreature newcomer, (List<IModifier> modifiers, (int attack, int health, int speed) persistent)? pass, IModifierManager modifiers) {
            if (pass == null || newcomer == null) return;
            var (handed, persistent) = pass.Value;
            modifiers.ChangeBaseStats(newcomer, persistent.attack, persistent.health, persistent.speed);
            foreach (var modifier in handed) modifiers.ApplyModifier(newcomer, modifier);
        }

        /// <summary>Insider Job: the creature's damage ignores Armor.</summary>
        public static bool IgnoresArmor(ICreature attacker) => Has(attacker, CreatureAbility.InsiderJob);
        #endregion
    }
}
