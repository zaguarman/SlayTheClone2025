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
        /// <param name="triggeringAttacker">Whoever damaged or killed the creature, for retaliation; null otherwise.</param>
        public static void QueueCreatureEffects(ICreature creature, EffectTrigger trigger, IActionsQueue queue, ICreature triggeringAttacker = null) {
            using var ability = queue.QueueingAbilityOf(creature, trigger); // Decides when they resolve (see ActionsQueue)
            foreach (var action in ActionsFor(creature, trigger)) {
                List<ITarget> targets;
                if (trigger == EffectTrigger.OnDamage && triggeringAttacker != null && action.targetType == TargetType.Enemy) {
                    // Retaliation: an enemy-targeted OnDamage effect hits whoever damaged this creature
                    targets = new List<ITarget> { triggeringAttacker };
                }
                else {
                    targets = SelectTargets(creature, creature.Owner, action, chosenTarget: null);
                }
                QueueAction(creature, creature.Owner, action, targets, queue);
            }
        }

        /// <summary>Queues what the spell does. A spell has a single moment, being played, so every effect it has applies.</summary>
        /// <param name="chosenTarget">What the spell was dropped on: a board slot, creature or player. Null picks its default target.</param>
        public static void QueueSpellEffects(Spell spell, IPlayer caster, ITarget chosenTarget, IActionsQueue queue) {
            chosenTarget ??= DefaultSpellTarget(spell, caster);
            foreach (var action in spell.Effects.SelectMany(e => e.actions)) {
                QueueAction(spell, caster, action, SelectTargets(spell, caster, action, chosenTarget), queue);
            }
        }

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
                    var spread = WithoutTooMainstream(TargetingSystem.GetAdditionalTargets(attacker.Owner, primaryTarget, action.targetModifier).ToList(), attacker);
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

        private static IEnumerable<EffectAction> ActionsFor(ICard card, EffectTrigger trigger) {
            // ToList: queueing can change the card's state while we iterate
            return card.Effects.Where(e => e.trigger == trigger).SelectMany(e => e.actions).ToList();
        }

        #region Targets
        private static List<ITarget> SelectTargets(ICard source, IPlayer controller, EffectAction action, ITarget chosenTarget) {
            if (action.targetType == TargetType.Self) {
                return source is ICreature self ? new List<ITarget> { self } : new List<ITarget>();
            }
            if (controller == null) {
                LogError($"{source.Name}: cannot target {action.targetType} without an owner.", LogTag.Effects);
                return new List<ITarget>();
            }

            if (chosenTarget != null && UsesChosenTarget(action) && IsSlotAction(action.actionType)) {
                return new List<ITarget> { SlotOf(chosenTarget) }.Where(slot => slot != null).ToList();
            }
            if (chosenTarget != null && UsesChosenTarget(action)) {
                var resolved = ResolveChosenTarget(chosenTarget, action.actionType);
                var targets = resolved != null ? new List<ITarget> { resolved } : new List<ITarget>();
                if (action.targetModifier.HasFlag(TargetModifier.WithAdjacent)) targets.AddRange(NeighboursOf(chosenTarget));
                return action.targetModifier.HasFlag(TargetModifier.WithAdjacent) ? WithoutTooMainstream(targets, source) : targets;
            }
            var valid = TargetingSystem.GetValidTargets(controller, action.targetType, action.targetModifier);
            bool area = action.targetType is TargetType.AllCreatures or TargetType.FriendlyCreatures or TargetType.EnemyCreatures
                && !action.targetModifier.HasFlag(TargetModifier.Random);
            return area ? WithoutTooMainstream(valid, source) : valid;
        }

        /// <summary>Too Mainstream: immune to effects that affect more than one creature (area, "each creature", spreads).</summary>
        private static List<ITarget> WithoutTooMainstream(List<ITarget> targets, ICard source) =>
            targets.Where(t => !(t is ICreature c && Abilities.Defends(c, CreatureAbility.TooMainstream, source as ICreature))).ToList();

        /// <summary>
        /// What a card dropped on a slot is aimed at. A spell aimed at one of the caster's own creatures, on the board or
        /// queued to be summoned there, homes on it: there's no fog of war on your own side, so you know where it will be.
        /// Aimed at an enemy slot (or an empty one), it's Fixed: it hits whatever is in that slot when it resolves (GDD 4.6).
        /// </summary>
        public static ITarget AimAt(ICard card, IPlayer caster, BoardSlot slot, IActionsQueue queue) {
            if (!(card is Spell spell) || slot == null || slot.Owner != caster || !IsAimed(spell)) return slot;

            // A creature queued to be summoned here replaces the one standing here before spells resolve
            var creature = IncomingCreature(slot, queue) ?? slot.OccupyingCreature;
            return creature != null ? new HomingTarget(creature, slot) : slot;
        }

        private static ICreature IncomingCreature(BoardSlot slot, IActionsQueue queue) {
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
            foreach (int i in new[] { slot.Index - 1, slot.Index + 1 }) {
                if (i >= 0 && i < row.Count && row[i].OccupyingCreature != null) yield return row[i].OccupyingCreature;
            }
        }

        /// <summary>Whether where the spell is dropped decides what at least one of its effects lands on.</summary>
        public static bool IsAimed(Spell spell) =>
            spell != null && spell.Effects.SelectMany(e => e.actions).Any(a => a.targetType != TargetType.Self && UsesChosenTarget(a));

        // A chosen target is used as is, unless the effect picks its own targets (random or every one of a kind).
        // Only effects that can land on creatures are aimed by it: drawing and summoning always go to the player
        // their target type names, wherever the card was dropped.
        private static bool UsesChosenTarget(EffectAction action) {
            if (IsSideWide(action)) return false;
            bool picksOwnTargets = action.targetModifier.HasFlag(TargetModifier.Random) || action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets);
            return !picksOwnTargets && (CanAffectCreatures(action.actionType) || IsSlotAction(action.actionType));
        }

        // Slot Effects aim at the slot itself (Static, GDD 4.6): they never follow a creature
        private static bool IsSlotAction(ActionType actionType) => actionType is ActionType.PlaceSlotEffect or ActionType.CleanseSlot;

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

        private static bool CanAffectCreatures(ActionType actionType) =>
            actionType is ActionType.Damage or ActionType.Heal or ActionType.ModifyStat
                or ActionType.ApplyStatus or ActionType.Armor or ActionType.Stun or ActionType.ModifyPriority or ActionType.Cleanse
                or ActionType.CreateWatchdog;

        // A Priority change aimed at a player covers every creature that player controls, now and later (GDD 3.7 Global Modifiers)
        private static bool IsSideWide(EffectAction action) =>
            action.actionType is ActionType.ModifyPriority or ActionType.CreateWatchdog && action.targetType is TargetType.Player or TargetType.Enemy;

        private static bool CanAffectPlayers(ActionType actionType) =>
            actionType is ActionType.Damage or ActionType.Heal or ActionType.Draw or ActionType.Summon or ActionType.CreateWatchdog;
        #endregion

        #region Actions
        private static void QueueAction(ICard source, IPlayer controller, EffectAction action, List<ITarget> targets, IActionsQueue queue) {
            // Chain damage only exists as part of an attack (see QueueAttackEffects)
            if (action.actionType == ActionType.Damage && action.targetModifier.HasFlag(TargetModifier.Chained)) return;

            Log($"{source.Name}: {action.actionType} {action.value} on {targets.Count} target(s) ({action.targetType}/{action.targetModifier})", LogTag.Effects);
            foreach (var target in targets) {
                if (action.actionType == ActionType.Summon) {
                    if (target is IPlayer summoner) SummonFromDeckEffect.QueueRandomSummons(summoner, action.value, queue);
                    continue;
                }

                var gameAction = CreateAction(source, controller, action, target);
                if (gameAction == null) continue; // This kind of target can't receive this action

                Log($"{source.Name}: queueing {gameAction}", LogTag.Effects | LogTag.Actions);
                queue.AddAction(gameAction);
            }
        }

        private static IGameAction CreateAction(ICard source, IPlayer controller, EffectAction action, ITarget target) {
            var creature = target as ICreature;
            switch (action.actionType) {
                case ActionType.Damage:
                    // Damage from a creature to a creature is ordered by that creature's speed; other damage has no source
                    return TargetedActions.Damage(target, action.value, creature != null ? source as ICreature : null);
                case ActionType.Heal:
                    return TargetedActions.Heal(target, action.value);
                case ActionType.Draw:
                    return target is IPlayer player ? new DrawCardsAction(player, action.value) : null;
                case ActionType.Armor:
                    return creature != null && action.value != 0 ? new ModifyArmorAction(creature, action.value) : null;
                case ActionType.ModifyStat:
                    return creature != null && action.value != 0
                        ? new ModifyAction(creature, action.value, action.modifyAttack, action.modifyHealth, action.modifySpeed,
                            source: source as ICreature, controller: controller)
                        : null;
                case ActionType.Stun: // A stun exhausts for its value in Days (1 is the usual stun)
                    return Status(creature, StatusEffectType.Exhausted, action.value, 0, source, controller);
                case ActionType.ApplyStatus:
                    return Status(creature, action.statusEffectToApply, action.statusDuration, action.statusPotency, source, controller);
                case ActionType.Cleanse:
                    return creature != null ? new CleanseAction(creature, action.cleanse) : null;
                case ActionType.PlaceSlotEffect: // Static: the slot itself, whoever is in it (GDD 4.6)
                    return SlotOf(target) is BoardSlot slot && action.slotEffect != SlotEffectType.None
                        ? new PlaceSlotEffectAction(slot, action.slotEffect, action.value, action.statusDuration, controller)
                        : null;
                case ActionType.CleanseSlot:
                    return SlotOf(target) is BoardSlot cleansed ? new CleanseSlotAction(cleansed) : null;
                case ActionType.CreateWatchdog: // Aimed at a creature it watches that creature; at a player, that whole side
                    return creature != null || target is IPlayer
                        ? new CreateWatchdogAction(action.watchdog, controller, creature, action.statusDuration)
                        : null;
                case ActionType.ModifyPriority: // For statusDuration Days counting this one; 0 means this Day only
                    return action.value != 0 && (creature != null || target is IPlayer)
                        ? new ModifyPriorityAction(target, action.value, action.statusDuration)
                        : null;
                default:
                    return null;
            }
        }

        private static IGameAction Status(ICreature creature, StatusEffectType type, int duration, int potency, ICard source, IPlayer controller) {
            if (creature == null) return null;
            if (type == StatusEffectType.Blessed) duration = 1; // Persistent: its stacks (potency) count, not Days
            if (type == StatusEffectType.None || duration <= 0) {
                LogWarning($"{source.Name}: invalid status {type} for {duration} turns; skipped.", LogTag.Effects);
                return null;
            }
            return new ApplyStatusEffectAction(creature, type, duration, potency, source as ICreature, controller);
        }

        // Jumps from the creature that was hit to its neighbours, halving the damage on each jump
        private static void QueueChain(ICreature attacker, ITarget primaryTarget, int damage, IActionsQueue queue) {
            if (!(primaryTarget is ICreature hitCreature)) return; // Chains need a creature to jump from

            foreach (var target in WithoutTooMainstream(TargetingSystem.GetDirectionalChainTargets(hitCreature, MaxChainJumps).Cast<ITarget>().ToList(), attacker)) {
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
