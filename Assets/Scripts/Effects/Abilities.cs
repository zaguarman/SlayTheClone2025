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
        public static bool IsActiveAbility(CreatureAbility ability) => ability is CreatureAbility.IdentityTheft or CreatureAbility.Bodyguard
            or CreatureAbility.Wannabe or CreatureAbility.CreditGrab or CreatureAbility.ShellGame;

        /// <summary>Bodyguard can't attack: it only protects. It's printed on the creature, so it holds even while switched off.</summary>
        public static bool CanAttack(ICreature creature) => creature != null && creature.Ability != CreatureAbility.Bodyguard;

        /// <summary>
        /// Abilities that only help a friendly creature land with the spells, before the Day's attacks, as help played this Day
        /// does (GDD 3.7): a Bodyguard's protection is there before the hits it should stop.
        /// </summary>
        public static bool LandsWithSpells(CreatureAbility ability) => ability == CreatureAbility.Bodyguard;

        /// <summary>Whether a plain drag onto this slot uses the ability (no Shift): a Bodyguard dragged onto another friendly creature.</summary>
        public static bool UsedByPlainDrag(ICreature creature, BoardSlot slot) =>
            creature?.Ability == CreatureAbility.Bodyguard && slot?.Owner == creature.Owner && slot.OccupyingCreature != null && slot.OccupyingCreature != creature;

        /// <summary>
        /// Whether its Active ability can be used now: switched on, not resting (Cooldown), and Credit Grab's once per combat
        /// not spent yet.
        /// </summary>
        public static bool CanUseActive(ICreature creature, IModifierManager modifiers) =>
            creature != null && IsActiveAbility(creature.Ability) && IsActive(creature)
            && !(modifiers?.HasStatusEffect(creature, StatusEffectType.Cooldown) ?? false)
            && !(creature.Ability == CreatureAbility.CreditGrab && (creature.Owner?.UsedOncePerCombat.Contains(OnceKey(creature)) ?? false));

        /// <summary>The key a once-per-combat ability is spent under, for this card, until the combat ends.</summary>
        public static string OnceKey(ICreature creature) => $"{creature.CopyId}#ability"; // This copy's, not every copy's

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
            CreatureAbility.PennyStock => "Penny Stock",
            CreatureAbility.CreditGrab => "Credit Grab",
            CreatureAbility.SprayAndPray => "Spray and Pray",
            CreatureAbility.ChainMail => "Chain Mail",
            CreatureAbility.DoubleTap => "Double Tap",
            CreatureAbility.ShellGame => "Shell Game",
            CreatureAbility.SafeSpace => "Safe Space",
            CreatureAbility.Ambush => "Ambush",
            CreatureAbility.ExtremePrejudice => "Extreme Prejudice",
            CreatureAbility.AllergicToLight => "Allergic to Light",
            CreatureAbility.FallGuy => "Fall Guy",
            CreatureAbility.Brink => "On the Brink",
            CreatureAbility.CollateralDamage => "Collateral Damage",
            CreatureAbility.SorryImLate => "Sorry I'm Late",
            CreatureAbility.HeProtec => "He Protec",
            _ => ability.ToString()
        };

        /// <summary>Its reminder text, as the card prints it after the name.</summary>
        public static string ReminderOf(CreatureAbility ability) => ability switch {
            CreatureAbility.Contrarian => "stat changes on it are reversed",
            CreatureAbility.Extremist => "stat changes on it are doubled",
            CreatureAbility.Unbothered => "enemy effects can't lower its stats",
            CreatureAbility.NoU => "enemy stat changes and statuses that land on it bounce to a random enemy creature",
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
            CreatureAbility.ReturnToSender => "enemy statuses bounce back to the creature that sent them; from a spell, to a random enemy creature",
            CreatureAbility.PennyStock => "double damage when it hits for 3 or less",
            CreatureAbility.Undercover => "its plans show as ??? to the enemy",
            CreatureAbility.Ambush => "enters as ???; double damage while the enemy doesn't know what it is, or it's Concealed",
            CreatureAbility.ExtremePrejudice => "its attack destroys the creature it hits, whatever its Health or Armor; then it's gone too",
            CreatureAbility.Handover => "when a summon replaces it, the newcomer takes over its stat changes and statuses",
            CreatureAbility.Recoil => "+2 damage on its attacks; it takes 1 damage after each attack",
            CreatureAbility.Bodyguard => "can't attack. Active: another friendly creature is Untouchable this Day, then a Day of Cooldown (drag it onto that creature)",
            CreatureAbility.Wannabe => "Active: its Attack, Health and Speed become target creature's (hold Shift as you drag it)",
            CreatureAbility.CreditGrab => "Active, once per combat: steals target creature's stat raises (hold Shift as you drag it)",
            CreatureAbility.SprayAndPray => "its attacks also hit the creatures beside its target",
            CreatureAbility.ChainMail => "its attacks jump to the next 2 creatures in line, half the damage each jump",
            CreatureAbility.DoubleTap => "it attacks twice",
            CreatureAbility.ShellGame => "Active: target creature trades places with the one to its right (hold Shift as you drag it)",
            CreatureAbility.SafeSpace => "each Dawn, it and the friendly creatures beside it gain Bless",
            CreatureAbility.AllergicToLight => $"when it's Revealed, or a Reveal shows it, it takes {LightDamage} damage",
            CreatureAbility.FallGuy => "when an enemy effect would Reveal or destroy another friendly creature, it lands on this one instead",
            CreatureAbility.Brink => "at 2 Health or less, everything it deals is doubled",
            CreatureAbility.CollateralDamage => "when its attack is more than a creature's Health, the rest hits that creature's player",
            CreatureAbility.SorryImLate => "played onto a friendly creature, it arrives after the attacks: the creature there takes the Day's hits, then it's replaced",
            CreatureAbility.HeProtec => "when a summon replaces it, it stays through the attacks and takes the Day's hits; the newcomer arrives after them",
            CreatureAbility.Irreplaceable => "no summon can replace it: its slot stays its own until it leaves",
            _ => ""
        };

        /// <summary>
        /// Whether a summon can replace this creature: not an Irreplaceable one (Demagogue), its drawback. Printed, so it
        /// holds even while its ability is switched off.
        /// </summary>
        public static bool CanBeReplaced(ICreature creature) => creature == null || creature.Ability != CreatureAbility.Irreplaceable;

        /// <summary>
        /// A summon onto a friendly creature that waits for the attacks (the Late Arrivals layer, after every other): the
        /// newcomer is Sorry I'm Late, or the creature it replaces says He Protec. The one in the slot stays through the Day's
        /// hits, attacking if it planned to, and is replaced after them; if it died or left meanwhile, the newcomer just
        /// arrives. Decided when the summon is queued, so the plan shows where it lands in the order.
        /// </summary>
        public static bool ArrivesLate(IGameAction action) {
            var (newcomer, slot) = action switch {
                PlayCardAction play when play.GetCard() is ICreature creature => (creature, play.GetTarget() as BoardSlot),
                SummonCreatureAction summon => (summon.GetCreature(), summon.GetTargetSlot() as BoardSlot),
                _ => (null, null)
            };
            var standing = slot?.OccupyingCreature;
            if (newcomer == null || standing == null || standing == newcomer) return false;
            return newcomer.Ability == CreatureAbility.SorryImLate || Has(standing, CreatureAbility.HeProtec);
        }

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
        /// "The creature that caused it", for bounced effects: the source creature if it's still in play, otherwise (a spell)
        /// the caster's creature with the lowest current Health (ties at random), or, <paramref name="atRandom"/>, any of the
        /// caster's creatures picked like a Random effect (the one with the most Heat first). Null if the caster has none
        /// (the effect fails).
        /// </summary>
        public static ICreature BounceTarget(ICreature source, IPlayer caster, bool atRandom = false) {
            if (source != null && !source.IsDead && source.Slot != null) return source;
            var candidates = caster?.Battlefield.Select(s => s.OccupyingCreature).Where(c => c != null && !c.IsDead).ToList();
            if (candidates == null || candidates.Count == 0) return null;
            if (atRandom) return HeatRules.Hottest(source, candidates) ?? candidates[GameRandom.Next(candidates.Count)];
            return candidates.OrderBy(c => c.Health).ThenBy(_ => GameRandom.Next(int.MaxValue)).First();
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

                // 3. Reflect: No U deflects any enemy stat change to a random creature on the enemy's side (Heat first), where
                // it starts again
                if (change.IsEnemy && !change.Bounced && Defends(target, CreatureAbility.NoU, change.Source)) {
                    var bounceTo = BounceTarget(null, change.Controller, atRandom: true);
                    Log($"{target.Name}'s No U deflects the stat change to {bounceTo?.Name ?? "nobody (it fails)"}", LogTag.Effects);
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

        /// <summary>Safe Space: each Dawn, it and the friendly creatures beside it (a step away) gain Bless 1 (a ward against the next status).</summary>
        public static void TrySafeSpace(ICreature creature, IActionsQueue queue) {
            if (!Has(creature, CreatureAbility.SafeSpace) || creature.Slot?.Owner == null) return;
            var row = creature.Slot.Owner.Battlefield;
            foreach (int i in Formation.Neighbours(creature.Slot.Index).Append(creature.Slot.Index)) {
                var ward = i >= 0 && i < row.Count ? row[i].OccupyingCreature : null;
                if (ward != null && !ward.IsDead) queue.AddAction(new ApplyStatusEffectAction(ward, StatusEffectType.Blessed, 1, 1, creature, creature.Owner));
            }
        }
        #endregion

        #region Damage
        /// <summary>
        /// The damage a hit deals before the target's modifiers (Exposed) and Armor: Underground ignores an attacker's
        /// Temporary Stat Modifications, then Penny Stock doubles hits of 3 or less.
        /// </summary>
        public static int OutgoingDamage(DamageCreatureAction hit, IModifierManager modifiers) {
            int damage = hit.GetDamage();
            var attacker = hit.GetAttacker();
            if (hit.DealsAttackersAttack && Defends(hit.GetTarget(), CreatureAbility.Underground, attacker)) {
                damage = attacker.BaseAttack; // Printed Attack and Persistent Stat Modifications only
            }
            // Attacking, it ignores the defender's stat changes too: a Health buff doesn't soak its hit, a Health debuff doesn't add to it
            if (hit.DealsAttackersAttack && Has(attacker, CreatureAbility.Underground) && hit.GetTarget() is ICreature defender) {
                damage = Math.Max(0, damage + (defender.MaxHealth - defender.BaseHealth));
            }
            if (damage > 0 && damage <= 3 && Has(attacker, CreatureAbility.PennyStock)) damage *= 2;
            if (damage > 0 && IsAmbushing(attacker, modifiers)) damage *= 2;
            if (damage > 0 && IsOnTheBrink(attacker)) damage *= 2;
            return damage;
        }

        /// <summary>
        /// Ambush: everything it deals is doubled while the other side doesn't know what it is (it enters as ???, see
        /// <see cref="Intel.IsStealthy"/>) or while it's Concealed. A Reveal or Revealed shows it for good, and ends that.
        /// </summary>
        public static bool IsAmbushing(ICreature creature, IModifierManager modifiers) =>
            Has(creature, CreatureAbility.Ambush)
            && (!Intel.KnowsWhatItIs(creature.Owner?.Opponent, creature) || Intel.IsConcealed(creature, modifiers));

        /// <summary>On the Brink's line: at this much Health or less it deals double.</summary>
        public const int BrinkHealth = 2;

        /// <summary>
        /// On the Brink (Utopia's accelerationism: however bad it gets, the breakthrough pays off): at 2 Health or less, Borrowed
        /// Time's 0 included, everything it deals is doubled.
        /// </summary>
        public static bool IsOnTheBrink(ICreature creature) =>
            Has(creature, CreatureAbility.Brink) && !creature.IsDead && creature.Health <= BrinkHealth;

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

        /// <summary>Allergic to Light: the damage it takes each time it's Revealed or a Reveal shows it.</summary>
        public const int LightDamage = 2;

        /// <summary>Fall Guy: the creature on this one's side that takes an enemy's Reveal or destroy meant for it; null if none.</summary>
        public static ICreature FallGuyFor(ICreature creature) {
            var row = creature?.Owner?.Battlefield;
            return row?.Select(s => s.OccupyingCreature).FirstOrDefault(c => c != null && c != creature && !c.IsDead && Has(c, CreatureAbility.FallGuy));
        }

        /// <summary>Insider Job: the creature's damage ignores Armor.</summary>
        public static bool IgnoresArmor(ICreature attacker) => Has(attacker, CreatureAbility.InsiderJob);
        #endregion
    }
}
