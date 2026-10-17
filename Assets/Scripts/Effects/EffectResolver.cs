using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The one interpreter of card effects. Creatures (when a trigger fires), spells (when played) and attacks
    /// (chain and spread add-ons) all turn their <see cref="EffectAction"/>s into queued game actions here,
    /// so an effect means the same thing whatever card carries it. Nothing is applied directly.
    /// </summary>
    public static class EffectResolver {
        private const int MaxChainJumps = 2;

        /// <summary>Queues what the creature's effects with this trigger do.</summary>
        /// <param name="triggeringAttacker">The creature whose attack hit this one, for Counterattack and Retaliate; null otherwise.</param>
        public static void QueueCreatureEffects(ICreature creature, EffectTrigger trigger, IActionsQueue queue, ICreature triggeringAttacker = null) {
            using var ability = queue.QueueingAbilityOf(creature, trigger); // Decides when they resolve (see ActionsQueue)
            foreach (var action in ActionsFor(creature, trigger, creature.Owner)) {
                var queued = action;
                List<ITarget> targets;
                if (trigger == EffectTrigger.OnRevealed && action.targetType == TargetType.Enemy && action.targetModifier == TargetModifier.None) {
                    targets = new List<ITarget> { triggeringAttacker is { IsDead: false, Slot: not null } revealer ? revealer : creature.Owner?.Opponent };
                }
                else if (trigger == EffectTrigger.OnDamage && action.targetType == TargetType.Enemy && action.targetModifier == TargetModifier.None) {
                    // Counterattack and Retaliate: an enemy-targeted OnDamage effect answers an attack, on the attacker.
                    // Damage that isn't an attack (a spell, an ability) isn't answered
                    if (triggeringAttacker == null) continue;
                    targets = new List<ITarget> { triggeringAttacker };
                    if (action.actionType == ActionType.Damage && action.value <= 0) { // Counterattack: for its Attack
                        if (creature.Attack <= 0) continue;
                        queued = action.Clone();
                        queued.value = creature.Attack;
                    }
                }
                else {
                    targets = SelectTargets(creature, creature.Owner, action, chosenTarget: null, queue);
                }
                QueueAction(creature, creature.Owner, queued, targets, queue);
            }
        }

        /// <summary>Queues what the spell does. A spell has a single moment, being played, so every effect it has applies.</summary>
        /// <param name="chosenTarget">What the spell was dropped on: a board slot, creature or player. Null picks its default target.</param>
        public static void QueueSpellEffects(Spell spell, IPlayer caster, ITarget chosenTarget, IActionsQueue queue) {
            chosenTarget ??= DefaultSpellTarget(spell, caster);
            // Gamble: one of its effects, at random
            var effects = spell.SourceData?.gamble == true && spell.Effects.Count > 0
                ? new List<CardEffect> { spell.Effects[GameRandom.Next(spell.Effects.Count)] }
                : spell.Effects.ToList();
            if (effects.Count == 1 && spell.SourceData?.gamble == true) Log($"{spell.Name}: the wheel stops on effect {spell.Effects.IndexOf(effects[0]) + 1}", LogTag.Effects);
            foreach (var action in effects.SelectMany(e => e.actions)) {
                var targets = SelectTargets(spell, caster, action, chosenTarget, queue);
                // A part that waits (Dead Cat Bounce's crash): aimed now, it lands at a later Dawn, listed with the foretold
                if (action.delayDays > 0) caster.Prophecies.Add(new Prophecy(spell, caster, chosenTarget, queue.Today + action.delayDays, action, targets));
                else QueueAction(spell, caster, action, targets, queue, action.onlyIfTargetDies ? DiesOf(chosenTarget) : null);
            }
        }

        /// <summary>
        /// Queues what a card not in play does on this trigger (Laid Off: discarded from the hand), as its owner's effect:
        /// targets are picked as a spell's are, without one chosen; "itself" is nothing.
        /// </summary>
        public static void QueueCardEffects(ICard card, IPlayer owner, EffectTrigger trigger, IActionsQueue queue) {
            foreach (var action in ActionsFor(card, trigger, owner)) {
                if (action.targetType == TargetType.Self) continue;
                QueueAction(card, owner, action, SelectTargets(card, owner, action, chosenTarget: null, queue), queue);
            }
        }

        /// <summary>A spell's part that waited (see <see cref="EffectAction.delayDays"/>) lands now on what it was aimed at, those still there.</summary>
        public static void QueueDelayed(Spell spell, IPlayer caster, EffectAction action, IEnumerable<ITarget> targets, IActionsQueue queue) =>
            QueueAction(spell, caster, action, targets.Where(t => !(t is ICreature creature) || (!creature.IsDead && creature.Slot != null)).ToList(), queue);

        /// <summary>
        /// Queues the extra hits the attacker's OnDamage effects add to an attack on <paramref name="primaryTarget"/>:
        /// chain damage jumps along the hit creature's neighbours, other target modifiers spread to more targets.
        /// </summary>
        public static void QueueAttackEffects(ICreature attacker, ITarget primaryTarget, IActionsQueue queue) {
            foreach (var action in ActionsFor(attacker, EffectTrigger.OnDamage)) {
                if (action.actionType != ActionType.Damage || action.targetModifier == TargetModifier.None) continue;

                // The effect's value replaces the attack damage when it is set
                int damage = action.value > 0 ? action.value : attacker.Attack;
                if (action.targetModifier.HasFlag(TargetModifier.Chained)) {
                    QueueChain(attacker, primaryTarget, damage, queue);
                }
                else {
                    var spread = WithoutTooMainstream(TargetingSystem.GetAdditionalTargets(attacker.Owner, primaryTarget, action.targetModifier).ToList(), attacker, queue?.Modifiers);
                    foreach (var target in spread) {
                        // Without a value the spread hits like the attack itself, for the attack the attacker has when it lands
                        var hit = action.value > 0
                            ? TargetedActions.Damage(target, action.value, attacker, isCombatDamage: true)
                            : TargetedActions.AttackersHit(target, attacker);
                        if (hit != null) queue.AddAction(hit);
                    }
                }
            }
        }

        private static IEnumerable<EffectAction> ActionsFor(ICard card, EffectTrigger trigger, IPlayer owner = null) {
            // ToList: queueing can change the card's state while we iterate
            var effects = card.Effects.Select((effect, index) => (effect, index)).Where(e => e.effect.trigger == trigger).ToList();
            var actions = new List<EffectAction>();
            foreach (var (effect, index) in effects) {
                if (effect.oncePerCombat && owner != null) {
                    // Once per combat: used up for this copy of the card, wherever it goes, until the combat ends; each copy has
                    // its own (the card's id is its definition's, the same for every copy: a second Living Dead never rose)
                    string key = $"{card.CopyId}#{index}";
                    if (!owner.UsedOncePerCombat.Add(key)) {
                        Log($"{card.Name}: its once-per-combat {trigger} effect is used up", LogTag.Effects);
                        continue;
                    }
                }
                actions.AddRange(effect.actions);
            }
            return actions;
        }

        #region Targets
        private static List<ITarget> SelectTargets(ICard source, IPlayer controller, EffectAction action, ITarget chosenTarget, IActionsQueue queue) {
            var targets = SelectTargetsWithItself(source, controller, action, chosenTarget, queue);
            if (action.targetModifier.HasFlag(TargetModifier.Others) && source is ICreature self) targets.Remove(self); // "Other" creatures: not itself
            return targets;
        }

        private static List<ITarget> SelectTargetsWithItself(ICard source, IPlayer controller, EffectAction action, ITarget chosenTarget, IActionsQueue queue) {
            if (action.targetType == TargetType.Self) {
                return source is ICreature self ? new List<ITarget> { self } : new List<ITarget>();
            }
            if (controller == null) {
                LogError($"{source.Name}: cannot target {action.targetType} without an owner.", LogTag.Effects);
                return new List<ITarget>();
            }

            // A side hazard, or its sweeper: every slot of the side (Checkpoint, Bomb Disposal)
            if (action.actionType is ActionType.PlaceSlotEffect or ActionType.CleanseSlot && action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets)) {
                return SidesOf(action.targetType, controller).SelectMany(side => side.Battlefield).Cast<ITarget>().ToList();
            }
            if (chosenTarget != null && UsesChosenTarget(action) && IsSlotAction(action.actionType)) {
                return new List<ITarget> { SlotOf(chosenTarget) }.Where(slot => slot != null).ToList();
            }
            if (chosenTarget != null && UsesChosenTarget(action) && action.targetModifier.HasFlag(TargetModifier.Beside)) {
                return WithoutTooMainstream(NeighboursOf(chosenTarget).ToList(), source, queue?.Modifiers);
            }
            if (chosenTarget != null && action.targetModifier.HasFlag(TargetModifier.AlliesOfTarget)) {
                return WithoutTooMainstream(AlliesOf(chosenTarget).ToList(), source, queue?.Modifiers);
            }
            // Myth's loophole: a Phoenix Feather still finds its creature when a Sacrifice took it first this Day (one offering
            // pays every rite); the resurrection checks the Day
            if (action.actionType == ActionType.Resurrect && chosenTarget is HomingTarget { Creature: { IsDead: true } offered }
                && controller.Offerings.Any(o => o.Creature == offered)) {
                return new List<ITarget> { offered };
            }
            if (chosenTarget != null && UsesChosenTarget(action)) {
                var resolved = ResolveChosenTarget(Homed(source, controller, chosenTarget, queue), action.actionType);
                // Heat Signature pulls Homing (the user): the hottest creature on either side takes it, even the caster's own
                if (IsHoming(source) && CanAffectCreatures(action.actionType) && !OwnSideOnly(action.actionType)
                    && HeatRules.Hottest(null, controller) is ICreature hottest) resolved = hottest;
                if (OwnSideOnly(action.actionType) && (resolved as ICreature)?.Owner != controller) resolved = null; // A sacrifice is yours to make
                var targets = resolved != null ? new List<ITarget> { resolved } : new List<ITarget>();
                if (action.targetModifier.HasFlag(TargetModifier.WithAdjacent)) targets.AddRange(NeighboursOf(chosenTarget));
                return action.targetModifier.HasFlag(TargetModifier.WithAdjacent) ? WithoutTooMainstream(targets, source, queue?.Modifiers) : targets;
            }
            if (action.targetModifier.HasFlag(TargetModifier.Opposite)) {
                var facing = Opposite(source as ICreature);
                return facing != null && !facing.IsDead ? new List<ITarget> { facing } : new List<ITarget>();
            }
            if (action.actionType is ActionType.Shuffle or ActionType.Spin) { // A move on a whole side: the side it names, or the one it was played on
                var shuffled = action.targetType switch {
                    TargetType.Enemy or TargetType.EnemyCreatures => controller.Opponent,
                    TargetType.Player or TargetType.FriendlyCreatures => controller,
                    _ => chosenTarget as IPlayer ?? (SlotOf(chosenTarget) as BoardSlot)?.Owner ?? (chosenTarget as ICreature)?.Owner ?? controller
                };
                return shuffled != null ? new List<ITarget> { shuffled } : new List<ITarget>();
            }
            if (action.targetModifier.HasFlag(TargetModifier.DroppedSide)) { // The creatures of the side it was played on
                var side = chosenTarget as IPlayer ?? (SlotOf(chosenTarget) as BoardSlot)?.Owner ?? controller;
                var there = TargetingSystem.GetValidTargets(controller, action.targetType, action.targetModifier)
                    .OfType<ICreature>().Where(c => c.Owner == side).Cast<ITarget>().ToList();
                return WithoutTooMainstream(there, source, queue?.Modifiers);
            }
            if (action.targetModifier.HasFlag(TargetModifier.Random)) return PickAtRandom(source, controller, action, queue);
            if (action.targetModifier.HasFlag(TargetModifier.Strongest)) {
                var strongest = Strongest(TargetingSystem.GetValidTargets(controller, action.targetType).OfType<ICreature>()
                    .Where(c => !(action.targetModifier.HasFlag(TargetModifier.Others) && c == source)));
                return strongest != null ? new List<ITarget> { strongest } : new List<ITarget>();
            }
            var valid = TargetingSystem.GetValidTargets(controller, action.targetType, action.targetModifier);
            bool area = action.targetType is TargetType.AllCreatures or TargetType.FriendlyCreatures or TargetType.EnemyCreatures
                && !action.targetModifier.HasFlag(TargetModifier.Random);
            return area ? WithoutTooMainstream(valid, source, queue?.Modifiers) : valid;
        }

        // Random: one of the creatures it can affect, picked when it happens. Heat Signature draws it: if any of them has
        // Heat, it goes to the one with the most (see HeatRules); otherwise to any of them, equally likely. Random damage
        // is drawn from both sides: the hottest creature anywhere takes it, even the caster's own. A random Reveal only
        // picks among the creatures that still hide something from its caster. No creature to pick: it fizzles.
        private static List<ITarget> PickAtRandom(ICard source, IPlayer controller, EffectAction action, IActionsQueue queue) {
            var candidates = TargetingSystem.GetValidTargets(controller, action.targetType).ToList();
            if (action.actionType == ActionType.Attack || action.targetModifier.HasFlag(TargetModifier.Others)) candidates.Remove(source as ICreature); // Never itself
            if (action.actionType == ActionType.Reveal) {
                candidates = candidates.Where(t => Intel.HidesSomethingFrom(controller, t as ICreature, queue)).ToList();
            }
            if (candidates.Count == 0) {
                Log($"{source.Name}: no creature to pick for its random {action.actionType}; it fizzles", LogTag.Effects);
                return candidates;
            }
            var hottest = action.actionType == ActionType.Damage
                ? HeatRules.Hottest(source as ICreature, controller)
                : HeatRules.Hottest(source as ICreature, candidates.OfType<ICreature>());
            return new List<ITarget> { hottest ?? candidates[GameRandom.Next(candidates.Count)] };
        }

        /// <summary>"The strongest": the creature with the highest Attack, ties at random; null if there's none.</summary>
        public static ICreature Strongest(IEnumerable<ICreature> creatures) {
            var list = creatures.Where(c => c != null && !c.IsDead).ToList();
            if (list.Count == 0) return null;
            int top = list.Max(c => c.Attack);
            var tied = list.Where(c => c.Attack == top).ToList();
            return tied[GameRandom.Next(tied.Count)];
        }

        /// <summary>Too Mainstream: immune to effects that affect more than one creature (area, "each creature", spreads).</summary>
        private static List<ITarget> WithoutTooMainstream(List<ITarget> targets, ICard source, IModifierManager modifiers) =>
            targets.Where(t => !(t is ICreature c && Abilities.ShrugsOffArea(c, source as ICreature, modifiers))).ToList(); // Too Mainstream, or Deniable this Day

        /// <summary>
        /// What a card dropped on a slot is aimed at. A spell aimed at one of the caster's own creatures, on the board or
        /// queued to be summoned there, homes on it: there's no fog of war on your own side, so you know where it will be.
        /// Aimed at an enemy slot (or an empty one), it's Fixed: it hits whatever is in that slot when it resolves (GDD 4.6),
        /// unless the card says Homing: then it locks on the enemy creature standing there and follows it.
        /// </summary>
        public static ITarget AimAt(ICard card, IPlayer caster, BoardSlot slot, IActionsQueue queue) {
            if (!(card is Spell spell) || slot == null || !IsAimed(spell)) return slot;
            if (slot.Owner != caster) {
                var mark = slot.OccupyingCreature;
                return IsHoming(spell) && mark != null && !mark.IsDead ? new HomingTarget(mark, slot) : slot;
            }

            // A creature queued to be summoned here replaces the one standing here before spells resolve
            var creature = IncomingCreature(slot, queue) ?? slot.OccupyingCreature;
            return creature != null ? new HomingTarget(creature, slot) : slot;
        }

        /// <summary>The creature queued to be summoned into this slot this Day, if any.</summary>
        public static ICreature IncomingCreature(BoardSlot slot, IActionsQueue queue) {
            if (queue == null) return null;
            foreach (var action in queue.GetPendingActions()) {
                if (action is PlayCardAction play && play.GetTarget() == slot && play.GetCard() is ICreature played) return played;
                if (action is SummonCreatureAction summon && summon.GetTargetSlot() == slot) return summon.GetCreature();
            }
            return null;
        }

        // "...and the creatures next to it": the creatures beside the slot the effect lands on, when it resolves
        private static IEnumerable<ITarget> NeighboursOf(ITarget chosenTarget) {
            var slot = chosenTarget switch {
                HomingTarget homing => homing.CurrentSlot,
                BoardSlot boardSlot => boardSlot,
                ICreature creature => creature.Slot,
                _ => null
            };
            if (slot?.Owner == null) yield break;
            var row = slot.Owner.Battlefield;
            foreach (int i in Formation.Neighbours(slot.Index)) { // A step away in the formation
                if (i >= 0 && i < row.Count && row[i].OccupyingCreature != null) yield return row[i].OccupyingCreature;
            }
        }

        /// <summary>
        /// Information is instant (GDD 2.5): a spell that only changes what its caster can see (Reveal, Revealed) resolves as
        /// soon as it's played, during Planning.
        /// </summary>
        public static bool IsInstant(Spell spell) {
            var actions = spell?.Effects.SelectMany(e => e.actions).ToList();
            return actions != null && actions.Count > 0 && actions.All(a => a.actionType == ActionType.Reveal
                || (a.actionType == ActionType.ApplyStatus && a.statusEffectToApply == StatusEffectType.Revealed));
        }

        /// <summary>A Trend card: all it does is set what's trending (GDD 4.2), so it plays on either side of the table.</summary>
        public static bool IsTrend(Spell spell) => spell != null && IsTrend(spell.Effects);

        public static bool IsTrend(IEnumerable<CardEffect> effects) {
            var actions = effects?.SelectMany(e => e.actions).ToList();
            return actions != null && actions.Count > 0 && actions.All(a => a.actionType == ActionType.SetTrend);
        }

        /// <summary>Whether where the spell is dropped decides what at least one of its effects lands on.</summary>
        public static bool IsAimed(Spell spell) =>
            spell != null && spell.Effects.SelectMany(e => e.actions).Any(a => a.targetType != TargetType.Self && UsesChosenTarget(a));

        /// <summary>
        /// Whether a spell that isn't aimed does something to this side of the table (its player or the creatures there),
        /// so dropping it on that battlefield plays it. One that only does things to itself plays on its caster's side.
        /// </summary>
        public static bool Reaches(Spell spell, IPlayer caster, IPlayer side) {
            if (IsTrend(spell)) return true; // A Trend is the whole board's: it plays on either side
            var sides = spell.Effects.SelectMany(e => e.actions).SelectMany(a => SidesOf(a.targetType, caster)).ToList();
            return sides.Count == 0 ? side == caster : sides.Contains(side);
        }

        private static IEnumerable<IPlayer> SidesOf(TargetType targetType, IPlayer caster) => targetType switch {
            TargetType.Player or TargetType.FriendlyCreatures => new[] { caster },
            TargetType.Enemy or TargetType.EnemyCreatures => new[] { caster.Opponent },
            TargetType.AllCreatures => new[] { caster, caster.Opponent },
            _ => new IPlayer[0]
        };

        // A chosen target is used as is, unless the effect picks its own targets (random or every one of a kind).
        // Only effects that can land on creatures are aimed by it: drawing and summoning always go to the player
        // their target type names, wherever the card was dropped.
        public static bool UsesChosenTarget(EffectAction action) {
            if (IsSideWide(action)) return false;
            bool picksOwnTargets = action.targetModifier.HasFlag(TargetModifier.Random) || action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets)
                || action.targetModifier.HasFlag(TargetModifier.Strongest) || action.targetModifier.HasFlag(TargetModifier.Opposite);
            return !picksOwnTargets && (CanAffectCreatures(action.actionType) || IsSlotAction(action.actionType));
        }

        /// <summary>Effects only for your own creatures, whatever the card is dropped on: a sacrifice (Phoenix Feather), a faked death.</summary>
        public static bool OwnSideOnly(ActionType actionType) => actionType is ActionType.Resurrect or ActionType.FakeDeath;

        /// <summary>
        /// Whether an aimed spell can be played on this slot: one of its aimed effects lands on something there (a creature
        /// it can affect, the player behind an empty slot for damage and the like, the slot itself for a Slot Effect).
        /// The board lights these slots up while the card is carried, and only takes it there.
        /// </summary>
        public static bool CanAimAt(Spell spell, IPlayer caster, BoardSlot slot, IActionsQueue queue) {
            if (spell == null || caster == null || slot == null || !IsAimed(spell)) return false;
            var aim = AimAt(spell, caster, slot, queue);
            foreach (var action in spell.Effects.SelectMany(e => e.actions).Where(a => a.targetType != TargetType.Self && UsesChosenTarget(a))) {
                if (IsSlotAction(action.actionType)) return true;
                var resolved = aim is HomingTarget { CreatureInPlay: null } incoming // A creature on its way here
                    ? (CanAffectCreatures(action.actionType) ? incoming.Creature : null)
                    : ResolveChosenTarget(aim, action.actionType);
                // An empty slot the other side is summoning into this Day: summons land before spells, so the spell finds it
                if (resolved == null && CanAffectCreatures(action.actionType) && IncomingCreature(slot, queue) is ICreature arriving) resolved = arriving;
                if (resolved == null) continue;
                if (OwnSideOnly(action.actionType) && !(resolved is ICreature && slot.Owner == caster)) continue;
                return true;
            }
            return false;
        }

        // "...the other creatures on its side": the creatures on the side of the slot it lands on, but the one there
        private static IEnumerable<ITarget> AlliesOf(ITarget chosenTarget) {
            var slot = chosenTarget switch {
                HomingTarget homing => homing.CurrentSlot ?? homing.AimedSlot,
                BoardSlot boardSlot => boardSlot,
                ICreature creature => creature.Slot,
                _ => null
            };
            var landedOn = (chosenTarget as HomingTarget)?.Creature ?? chosenTarget as ICreature ?? slot?.OccupyingCreature;
            if (slot?.Owner == null) yield break;
            foreach (var other in slot.Owner.Battlefield.Select(s => s.OccupyingCreature)) {
                if (other != null && other != landedOn && !other.IsDead) yield return other;
            }
        }

        /// <summary>The card says Homing (GDD 4.6): aimed at an enemy creature it follows it, and Heat Signature pulls it.</summary>
        public static bool IsHoming(ICard card) => card?.SourceData?.homing == true;

        // Against a Concealed enemy creature, Homing works like Fixed (GDD 4.6): it hits whatever is in the slot it was aimed at
        private static ITarget Homed(ICard source, IPlayer controller, ITarget chosenTarget, IActionsQueue queue) {
            if (!(chosenTarget is HomingTarget homing) || homing.Creature.Owner == controller || queue?.Modifiers == null) return chosenTarget;
            bool hides = queue.Modifiers.HasStatusEffect(homing.Creature, StatusEffectType.Concealed) || Abilities.Has(homing.Creature, CreatureAbility.Undercover);
            return hides && homing.AimedSlot != null ? homing.AimedSlot : chosenTarget;
        }

        // Slot Effects aim at the slot itself (Static, GDD 4.6): they never follow a creature
        public static bool IsSlotAction(ActionType actionType) => actionType is ActionType.PlaceSlotEffect or ActionType.CleanseSlot or ActionType.Reveal;

        private static ITarget SlotOf(ITarget target) => target switch {
            HomingTarget homing => homing.AimedSlot,
            BoardSlot slot => slot,
            ICreature creature => creature.Slot,
            _ => null
        };

        // A board slot stands for its creature when the action can affect creatures, otherwise for the player who owns it.
        // Null when the action applies to neither (it does nothing).
        private static ITarget ResolveChosenTarget(ITarget target, ActionType actionType) {
            switch (target) {
                case HomingTarget homing:
                    if (homing.CreatureInPlay != null) return CanAffectCreatures(actionType) ? homing.CreatureInPlay : null;
                    return homing.CurrentSlot != null ? ResolveChosenTarget(homing.CurrentSlot, actionType) : null;
                case BoardSlot slot:
                    if (slot.OccupyingCreature != null && CanAffectCreatures(actionType)) return slot.OccupyingCreature;
                    return CanAffectPlayers(actionType) ? slot.Owner : null;
                case ICreature:
                    return CanAffectCreatures(actionType) ? target : null;
                case IPlayer:
                    return CanAffectPlayers(actionType) ? target : null;
                default:
                    return null;
            }
        }

        private static ITarget DefaultSpellTarget(Spell spell, IPlayer caster) {
            switch (spell.DefaultTargetType) {
                case TargetType.Player: return caster;
                case TargetType.Enemy: return caster.Opponent;
                default:
                    var modifier = spell.Effects.SelectMany(e => e.actions).FirstOrDefault()?.targetModifier ?? TargetModifier.None;
                    return TargetingSystem.GetValidTargets(caster, spell.DefaultTargetType, modifier).FirstOrDefault();
            }
        }

        /// <summary>Whether the creature is what the effect requires of it, now. <paramref name="value"/>: the effect's number (Attack N or less).</summary>
        public static bool Meets(ICreature creature, TargetRequisite requisite, IModifierManager modifiers, int value = 0) => requisite switch {
            TargetRequisite.Revealed => modifiers != null && modifiers.HasStatusEffect(creature, StatusEffectType.Revealed),
            TargetRequisite.NextToEmptySlot => NextToEmptySlot(creature),
            TargetRequisite.Outnumbered => creature?.Owner?.Opponent != null && CreatureCount(creature.Owner) < CreatureCount(creature.Owner.Opponent),
            TargetRequisite.AttackAtMost => creature != null && creature.Attack <= value,
            TargetRequisite.Deploying => creature != null && !creature.IsOperative,
            TargetRequisite.Online => creature?.SourceData?.faction == Faction.DigitalSociety,
            TargetRequisite.OutsideHeadquarters => creature?.Slot != null && Formation.PositionOf(creature.Slot.Index) != Formation.Position.Headquarters,
            _ => true
        };

        /// <summary>A slot beside it, on its own side, is empty (the board's edge isn't a slot).</summary>
        public static bool NextToEmptySlot(ICreature creature) {
            var slot = creature?.Slot;
            if (slot?.Owner == null) return false;
            var row = slot.Owner.Battlefield;
            return Formation.Neighbours(slot.Index).Any(i => i >= 0 && i < row.Count && !row[i].IsOccupied());
        }

        public static int CreatureCount(IPlayer side) => side?.Battlefield.Count(s => s.OccupyingCreature is { IsDead: false }) ?? 0;

        public static bool CanAffectCreatures(ActionType actionType) =>
            actionType is ActionType.Damage or ActionType.Destroy or ActionType.Heal or ActionType.ModifyStat
                or ActionType.ApplyStatus or ActionType.Armor or ActionType.Stun or ActionType.Cleanse
                or ActionType.CreateWatchdog or ActionType.DelayDeployment or ActionType.Rush or ActionType.Defect
                or ActionType.CopyStats or ActionType.StealStatChanges or ActionType.SwapPositions or ActionType.GrantAbility or ActionType.Equip or ActionType.SwapStats
                or ActionType.Push or ActionType.Pull or ActionType.Mimic or ActionType.TradeStats or ActionType.DoubleDown or ActionType.SplitPain or ActionType.SwapAbilities
                or ActionType.TakeControl or ActionType.Attack or ActionType.Funnel or ActionType.PassHarmfulStatuses
                or ActionType.StealGoodStatuses or ActionType.ReturnToHand or ActionType.FakeDeath or ActionType.Vanish or ActionType.Resurrect
                or ActionType.Consolidate or ActionType.CopyStatuses;

        // A Watchdog aimed at a player watches that whole side; a Reveal aimed at one shows their Watchdog conditions
        private static bool IsSideWide(EffectAction action) =>
            action.actionType is ActionType.CreateWatchdog or ActionType.Reveal && action.targetType is TargetType.Player or TargetType.Enemy;

        /// <summary>Whether this kind of effect can land on a player (aimed at an empty slot, it lands on that side's player).</summary>
        public static bool CanAffectPlayers(ActionType actionType) =>
            actionType is ActionType.Damage or ActionType.Heal or ActionType.Draw or ActionType.Summon or ActionType.CreateWatchdog or ActionType.Reveal
                or ActionType.SummonDecoys or ActionType.WipeWatchdogs or ActionType.CutDraws or ActionType.SendFor or ActionType.Grace;
        #endregion

        #region Actions
        // Whether the creature the spell is aimed at has died (or left play) by the time it's asked
        private static System.Func<bool> DiesOf(ITarget chosenTarget) {
            var victim = ResolveChosenTarget(chosenTarget, ActionType.Destroy) as ICreature;
            return () => victim != null && (victim.IsDead || victim.Slot == null);
        }

        private static void QueueAction(ICard source, IPlayer controller, EffectAction action, List<ITarget> targets, IActionsQueue queue, System.Func<bool> onlyIf = null) {
            // Chain damage only exists as part of an attack (see QueueAttackEffects)
            if (action.actionType == ActionType.Damage && action.targetModifier.HasFlag(TargetModifier.Chained)) return;

            Log($"{source.Name}: {action.actionType} {action.value} on {targets.Count} target(s) ({action.targetType}/{action.targetModifier})", LogTag.Effects);
            foreach (var target in targets) {
                if (action.actionType == ActionType.Summon) {
                    if (target is IPlayer summoner) SummonFromDeckEffect.QueueRandomSummons(summoner, action.value, queue);
                    continue;
                }

                var gameAction = CreateAction(source, controller, action, target, queue.Modifiers);
                if (gameAction == null) continue; // This kind of target can't receive this action
                if (gameAction is ISourcedAction sourced) sourced.Source = source;

                Log($"{source.Name}: queueing {gameAction}", LogTag.Effects | LogTag.Actions);
                if (onlyIf != null) ActionsQueue.OnlyIf(gameAction, onlyIf);
                queue.AddAction(gameAction);
            }
        }

        private static IGameAction CreateAction(ICard source, IPlayer controller, EffectAction action, ITarget target, IModifierManager modifiers) {
            var creature = target as ICreature;
            if (creature != null && !Meets(creature, action.requires, modifiers, action.value)) {
                Log($"{source.Name}: {creature.Name} isn't {action.requires}: its {action.actionType} doesn't land", LogTag.Effects);
                return null;
            }
            switch (action.actionType) {
                case ActionType.Destroy: // Outright, whatever its Health or Armor; Untouchable still saves it
                    if (creature == null) return null;
                    if (modifiers != null && modifiers.HasStatusEffect(creature, StatusEffectType.Untouchable)) {
                        Log($"{source.Name}: {creature.Name} is Untouchable: it isn't destroyed", LogTag.Effects);
                        return null;
                    }
                    return new DestroyCreatureAction(creature, source as ICreature, unconditional: true, controller: controller);
                case ActionType.Damage:
                    // Damage from a creature to a creature is ordered by that creature's speed; other damage has no source
                    return TargetedActions.Damage(target, action.value, creature != null ? source as ICreature : null);
                case ActionType.Heal:
                    return TargetedActions.Heal(target, action.value);
                case ActionType.Draw:
                    return target is IPlayer player ? new DrawCardsAction(player, action.value, fromEffect: true) : null;
                case ActionType.Armor:
                    return creature != null && action.value != 0 ? new ModifyArmorAction(creature, action.value) : null;
                case ActionType.ModifyStat:
                    return creature != null && action.value != 0
                        ? new ModifyAction(creature, action.value, action.modifyAttack, action.modifyHealth, action.modifySpeed,
                            durationInTurns: action.statusDuration, source: source as ICreature, controller: controller) { Origin = source }
                        : null;
                case ActionType.Stun: // A stun exhausts for its value in Days (1 is the usual stun)
                    return Status(creature, StatusEffectType.Exhausted, action.value, 0, source, controller);
                case ActionType.ApplyStatus:
                    return Status(creature, action.statusEffectToApply, action.statusDuration, action.statusPotency, source, controller);
                case ActionType.Cleanse:
                    return creature != null ? new CleanseAction(creature, action.cleanse, action.purge) : null;
                case ActionType.PlaceSlotEffect: // Static: the slot itself, whoever is in it (GDD 4.6)
                    return SlotOf(target) is BoardSlot slot && action.slotEffect != SlotEffectType.None
                        ? new PlaceSlotEffectAction(slot, action.slotEffect, action.value, action.statusDuration, controller)
                        : null;
                case ActionType.CleanseSlot:
                    return SlotOf(target) is BoardSlot cleansed && (cleansed.Effect != null || !action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets))
                        ? new CleanseSlotAction(cleansed) : null;
                case ActionType.SwapSlotEffects: // Projection: the whole board's, whatever it lands on
                    return new SwapSlotEffectsAction(controller);
                case ActionType.CreateWatchdog: // Aimed at a creature it watches that creature; at a player, that whole side
                    return creature != null || target is IPlayer
                        ? new CreateWatchdogAction(action.watchdog, controller, creature, action.statusDuration)
                        : null;
                case ActionType.Reveal: // A slot: its hidden Slot Effect and its creature's plans; a player: their Watchdog conditions
                    return target is IPlayer side ? new RevealAction(side, controller)
                        : SlotOf(target) is BoardSlot revealed ? new RevealAction(revealed, controller, source as ICreature) : null;
                // Two creatures: a creature's own effect works between it and its target; a spell's (or a creature's effect on
                // itself), between the target and the creature facing it
                case ActionType.CopyStats: { // The copier takes the model's stats
                    var (copier, model) = Pair(source, creature);
                    return copier != null && model != null ? new CopyStatsAction(copier, model) : null;
                }
                case ActionType.StealStatChanges: { // The thief takes the victim's stat raises
                    var (thief, victim) = Pair(source, creature);
                    return thief != null && victim != null ? new StealStatChangesAction(thief, victim) : null;
                }
                case ActionType.SwapPositions:
                    return creature != null ? new SwapPositionsAction(creature) : null;
                case ActionType.Push or ActionType.Pull: // Maneuvers: a step back, or forward
                    return creature != null ? new PushAction(creature, action.actionType == ActionType.Pull) : null;
                case ActionType.Spin: // Fidget Spinner: that side's front three slots, clockwise
                    return target is IPlayer spun ? new SpinAction(spun) : null;
                case ActionType.Shuffle: // A Maneuver: that whole side, at random
                    return target is IPlayer shuffled ? new ShuffleAction(shuffled) : null;
                case ActionType.ReturnToHand: // Back to its owner's hand, with the next Day's hand (Extraction)
                    return creature != null ? new ReturnToHandAction(creature) : null;
                case ActionType.FakeDeath: // It "dies" (its Last Will happens), then back to the hand (He Didn't Kill Himself)
                    return creature != null ? new ReturnToHandAction(creature, fakesItsDeath: true) : null;
                case ActionType.Vanish: // Off the board until the next Dawn (Witness Protection Program)
                    return creature != null ? new VanishAction(creature) : null;
                case ActionType.Revive: // Its own Last Will: it rises at the next Dawn, ready, with its stat changes (queued as it dies, while it still has its slot and them)
                    return creature != null && source == creature
                        ? new RevivalAction(creature, creature.Slot?.Index ?? 0, ready: true, kept: RevivalAction.StatChangesOn(creature))
                        : null;
                case ActionType.Grace: // The player's Die Another Day (God's Grace)
                    return target is IPlayer graced ? new GraceAction(graced, action.value) : null;
                case ActionType.Resurrect: // Sacrificed now, it rises at the Dawn so many Days on (Phoenix Feather)
                    return creature != null ? new ResurrectionAction(creature, controller, System.Math.Max(1, action.value)) : null;
                case ActionType.Consolidate: // The target, the strongest, takes Attack from every other creature in its group
                    return creature != null && action.value > 0
                        ? new ConsolidateAction(creature, action.value, wholeBoard: action.targetType == TargetType.AllCreatures, controller, source)
                        : null;
                case ActionType.Funnel: // The target takes the stat raises of every other creature on its side
                    return creature != null ? new FunnelAction(creature) : null;
                // No status is good or bad by itself (the user, 2026-10-15): these move every status
                case ActionType.PassHarmfulStatuses: { // The target's statuses go to the creature facing it (Frame Job)
                    var (from, to) = Pair(source, creature);
                    return from != null && to != null ? new PassStatusesAction(from, to, harmful: null) : null;
                }
                case ActionType.StealGoodStatuses: { // The target takes the statuses of the creature facing it (Devil's Breath)
                    var (thief, victim) = Pair(source, creature);
                    return thief != null && victim != null ? new PassStatusesAction(victim, thief, harmful: null) : null;
                }
                case ActionType.CopyStatuses: { // The target copies every status of the creature facing it (Reflect)
                    var (mirror, model) = Pair(source, creature);
                    return mirror != null && model != null ? new PassStatusesAction(model, mirror, harmful: null, copy: true) : null;
                }
                case ActionType.GrantAbility:
                    return creature != null && action.ability != CreatureAbility.None ? new GrantAbilityAction(creature, action.ability) : null;
                case ActionType.Equip: // Gear: a second ability while it stays (a held item)
                    return creature != null && action.ability != CreatureAbility.None ? new EquipAction(creature, action.ability) : null;
                case ActionType.SetTrend: // The whole board's: it doesn't matter where it lands
                    return new SetTrendAction(action.trend, action.statusDuration, controller);
                case ActionType.EndDay: // Amen: the Day ends here, whatever is still queued is discarded
                    return new EndDayAction(controller, source);
                case ActionType.Earthquake: // The whole board's too: a zigzag from a random slot (HAARP)
                    return action.value > 0 ? new EarthquakeAction(controller, action.value) : null;
                case ActionType.DelayDeployment: // Red Tape: value is the Days added
                    return creature != null && action.value > 0 ? new DelayDeploymentAction(creature, action.value) : null;
                case ActionType.Rush: // Overtime: value is the most Days taken off, then Burnout
                    return creature != null && action.value > 0 ? new RushAction(creature, action.value) : null;
                case ActionType.SummonDecoys: // Tokens into the player's empty slots (Ghost Army)
                    return target is IPlayer decoysSide && action.decoyData != null && action.value > 0 ? new SummonDecoysAction(decoysSide, action.decoyData, action.value) : null;
                case ActionType.SendFor: // A card from the deck joins that player's next hand
                    return target is IPlayer sender ? new SendForAction(sender, source, action.sendFor, action.value, action.fromDiscard) : null;
                case ActionType.CutDraws: // Fewer cards in that player's next hand (Supply Chain Attack)
                    return target is IPlayer cut && action.value > 0 ? new CutDrawsAction(cut, action.value) : null;
                case ActionType.WipeWatchdogs: // Every Watchdog that player controls (GoldenEye)
                    return target is IPlayer wiped ? new WipeWatchdogsAction(wiped) : null;
                case ActionType.Mimic: // Monkey See, Monkey Do: a fresh copy of the target in the slot facing it, on the caster's side
                    return creature?.Slot != null ? new MimicAction(creature, controller) : null;
                case ActionType.TradeStats: { // Freaky Friday: the target and the creature facing it trade Attack and Health
                    var (one, other) = Pair(source, creature);
                    return one != null && other != null ? new TradeStatsAction(one, other) : null;
                }
                case ActionType.DoubleDown: // 3rd Parallel Job: half its Health for double its Attack
                    return creature != null ? new DoubleDownAction(creature) : null;
                case ActionType.SplitPain: { // Blood Transfusion: the target and the creature facing it share their Health
                    var (one, other) = Pair(source, creature);
                    return one != null && other != null ? new SplitPainAction(one, other) : null;
                }
                case ActionType.SwapAbilities: { // Job Rotation: the target and the creature facing it swap abilities
                    var (one, other) = Pair(source, creature);
                    return one != null && other != null ? new SwapAbilitiesAction(one, other) : null;
                }
                case ActionType.Seal: // The DMCA: value is the Days
                    return target is IPlayer claimed && action.value > 0 ? new SealAction(claimed, action.value) : null;
                case ActionType.SwapStats: // Geomagnetic Reversal: its Attack and Health trade places
                    return creature != null ? new SwapStatsAction(creature) : null;
                case ActionType.Defect: // Taqiyya: it goes over to the other side, into the slot opposite (whatever stands there is discarded)
                    return creature?.Slot != null && creature.Owner?.Opponent != null ? new ChangeSidesAction(creature, creature.Owner.Opponent) : null;
                case ActionType.TakeControl: // It changes sides, to the effect's controller (GDD 5.4)
                    return creature != null && creature.Owner != controller ? new ChangeSidesAction(creature, controller) : null;
                case ActionType.Attack: // The creature whose effect it is attacks the target's slot, a real attack
                    return creature?.Slot != null && source is ICreature attacker && attacker != creature
                        ? new BattlefieldCombatAction(attacker, creature.Slot) : null;
                default:
                    return null;
            }
        }

        // Who does it and to whom, for the effects between two creatures. Copy: (copier, model); steal: (thief, victim)
        private static (ICreature doer, ICreature other) Pair(ICard source, ICreature target) {
            if (target == null) return (null, null);
            if (source is ICreature self && self != target) return (self, target);
            return (target, Opposite(target));
        }

        /// <summary>The creature in the slot facing this one, on the other side of the table.</summary>
        public static ICreature Opposite(ICreature creature) {
            var slot = creature?.Slot;
            var row = slot?.Owner?.Opponent?.Battlefield;
            return row != null && slot.Index < row.Count ? row[slot.Index].OccupyingCreature : null;
        }

        private static IGameAction Status(ICreature creature, StatusEffectType type, int duration, int potency, ICard source, IPlayer controller) {
            if (creature == null) return null;
            if (type is StatusEffectType.Blessed or StatusEffectType.HeatSignature) duration = 1; // Persistent: its stacks (potency) count, not Days
            if (type == StatusEffectType.None || duration <= 0) {
                LogWarning($"{source.Name}: invalid status {type} for {duration} turns; skipped.", LogTag.Effects);
                return null;
            }
            return new ApplyStatusEffectAction(creature, type, duration, potency, source as ICreature, controller) { Origin = source };
        }

        /// <summary>Jumps from the creature that was hit to its neighbours, halving the damage on each jump (Chain Mail, chained effects).</summary>
        public static void QueueChain(ICreature attacker, ITarget primaryTarget, int damage, IActionsQueue queue) {
            if (!(primaryTarget is ICreature hitCreature)) return; // Chains need a creature to jump from

            foreach (var target in WithoutTooMainstream(TargetingSystem.GetDirectionalChainTargets(hitCreature, MaxChainJumps).Cast<ITarget>().ToList(), attacker, queue?.Modifiers)) {
                if (damage <= 0) break;
                QueueCombatDamage(target, damage, attacker, queue);
                damage /= 2;
            }
        }

        private static void QueueCombatDamage(ITarget target, int damage, ICreature attacker, IActionsQueue queue) {
            var damageAction = TargetedActions.Damage(target, damage, attacker, isCombatDamage: true);
            if (damageAction != null) queue.AddAction(damageAction);
        }
        #endregion
    }
}
