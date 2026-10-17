using System.Collections.Generic;
using System.Linq;
using static Zeitgeist.DebugLogger;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// What Slot Effects do (GDD 2.4 Slot Effects, 'Slot Effects Compendium'). Triggered ones are environment actions:
    /// they resolve as soon as they're queued, before anything else (GDD 3.7). "1 Trigger" ones are removed the moment their
    /// condition is met. Continuous ones are checked where they apply (combat damage, ability triggers).
    /// </summary>
    public static class SlotEffectRules {
        /// <summary>Puts the effect on the slot. One per slot: an old one is removed at once.</summary>
        public static void Place(BoardSlot slot, SlotEffect effect, IGameMediator mediator) {
            if (slot == null || effect == null) return;
            if (slot.Effect != null) Log($"{effect.Name} replaces {slot.Effect.Name} on {slot}", LogTag.Effects);
            slot.Effect = effect;
            Log($"Placed {effect} on {slot}", LogTag.Effects);
            mediator?.NotifyBattlefieldStateChanged(slot.Owner);
        }

        /// <summary>Takes the effect off. A creature whose entry effects Scorched Earth held back gets them now (with a queue).</summary>
        public static void Remove(BoardSlot slot, IGameMediator mediator, IActionsQueue actionsQueue = null) {
            if (slot?.Effect == null) return;
            Log($"Removed {slot.Effect} from {slot}", LogTag.Effects);
            slot.Effect = null;
            mediator?.NotifyBattlefieldStateChanged(slot.Owner);
            slot.OccupyingCreature?.ReleaseHeldEntryEffects(actionsQueue);
        }

        /// <summary>A creature entered the slot: summoned there, or moved in by any means.</summary>
        public static void OnEnter(ICreature creature, BoardSlot slot, ActionExecutionContext context) {
            creature?.ReleaseHeldEntryEffects(context.ActionsQueue); // Moved off Scorched Earth: its entry effects happen now
            if (creature == null || Abilities.Has(creature, CreatureAbility.Hover)) return; // Hover: traps don't touch it
            // Bomb Disposal (Bomb Squad, Pokémon's Rapid Spin): the Slot Effect on a slot it enters is cleared before it goes off
            if (slot?.Effect != null && Abilities.Has(creature, CreatureAbility.BombDisposal)) {
                Log($"{creature.Name} defuses {slot.Effect.Name} on {slot}", LogTag.Effects);
                Remove(slot, context.GameMediator, context.ActionsQueue);
                return;
            }
            // Checkpoint (a side hazard, Pokémon's Stealth Rock): every creature that enters pays, and it stays
            if (slot?.Effect?.Type == SlotEffectType.Checkpoint) {
                Trigger(slot, context, $"{creature.Name} is stopped at a Checkpoint", new DamageCreatureAction(creature, slot.Effect.Value) { Source = slot.Effect.Source });
                return;
            }
            if (slot?.Effect?.Type != SlotEffectType.Landmine) return;
            Trigger(slot, context, $"{creature.Name} steps on a Landmine", new DamageCreatureAction(creature, slot.Effect.Value) { Source = slot.Effect.Source });
        }

        /// <summary>A creature left the slot by moving: on its own, pushed or swapped. Dying or being replaced isn't leaving.</summary>
        public static void OnLeave(ICreature creature, BoardSlot slot, ActionExecutionContext context) {
            if (creature == null || slot?.Effect?.Type != SlotEffectType.BarbedSnare || Abilities.Has(creature, CreatureAbility.Hover)) return;
            Trigger(slot, context, $"{creature.Name} tears free of a Barbed Snare", new DamageCreatureAction(creature, slot.Effect.Value) { Source = slot.Effect.Source });
        }

        // The effect is queued as an environment action, and a "1 Trigger" effect is removed (and so revealed) right away
        private static void Trigger(BoardSlot slot, ActionExecutionContext context, string what, IGameAction effect) {
            Log($"{what} ({slot})", LogTag.Effects);
            var sprung = slot.Effect;
            if (slot.Effect.IsOneTrigger) Remove(slot, context.GameMediator, context.ActionsQueue);
            using (context.ActionsQueue.QueueingEnvironment()) {
                if (sprung.Source != null) context.ActionsQueue.AddAction(new TrapSprungAction(slot, sprung)); // Its card on the stage first
                context.ActionsQueue.AddAction(effect);
            }
        }

        /// <summary>Shield: the creature standing on it takes no combat damage.</summary>
        public static bool BlocksCombatDamage(ICreature creature) => creature?.Slot?.Effect?.Type == SlotEffectType.Shield;

        /// <summary>Shield (a creature's status, the user: a slot can't have one) or an old Shield slot: no combat damage to it.</summary>
        public static bool BlocksCombatDamage(ICreature creature, IModifierManager modifiers) =>
            creature != null && (BlocksCombatDamage(creature) || (modifiers?.HasStatusEffect(creature, StatusEffectType.Shielded) ?? false));

        /// <summary>Scorched Earth: the triggered effects of the creature standing on it don't happen, and its ability is
        /// switched off (see Abilities). Its entry effects wait until it's off the slot (moved, or the effect ended). It can
        /// still move and attack.</summary>
        public static bool SuppressesTriggeredEffects(ICreature creature) => creature?.Slot?.Effect?.Type == SlotEffectType.ScorchedEarth;

        /// <summary>At Dusk: Slot Effects whose Days ran out are removed.</summary>
        public static void ProcessEndOfTurn(IEnumerable<IPlayer> players, int endedTurn, IGameMediator mediator, IActionsQueue actionsQueue = null) {
            foreach (var slot in players.Where(p => p?.Battlefield != null).SelectMany(p => p.Battlefield)) {
                if (slot.Effect == null || !slot.Effect.HasExpired(endedTurn + 1)) continue;
                // A Prayer answered (Novena, Pokémon's Wish): whoever stands there by now heals
                if (slot.Effect.Type == SlotEffectType.Prayer && slot.OccupyingCreature is { IsDead: false } prayed && actionsQueue != null) {
                    Log($"The prayers on {slot} are answered: {prayed.Name} heals {slot.Effect.Value}", LogTag.Effects);
                    actionsQueue.AddAction(new HealCreatureAction(prayed, slot.Effect.Value));
                }
                Remove(slot, mediator, actionsQueue);
            }
        }
    }
}
