using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
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

        public static void Remove(BoardSlot slot, IGameMediator mediator) {
            if (slot?.Effect == null) return;
            Log($"Removed {slot.Effect} from {slot}", LogTag.Effects);
            slot.Effect = null;
            mediator?.NotifyBattlefieldStateChanged(slot.Owner);
        }

        /// <summary>A creature entered the slot: summoned there, or moved in by any means.</summary>
        public static void OnEnter(ICreature creature, BoardSlot slot, ActionExecutionContext context) {
            if (creature == null || slot?.Effect?.Type != SlotEffectType.Landmine) return;
            Trigger(slot, context, $"{creature.Name} steps on a Landmine", new DamageCreatureAction(creature, slot.Effect.Value));
        }

        /// <summary>A creature left the slot by moving: on its own, pushed or swapped.</summary>
        public static void OnLeave(ICreature creature, BoardSlot slot, ActionExecutionContext context) {
            if (creature == null || slot?.Effect?.Type != SlotEffectType.BarbedSnare) return;
            Trigger(slot, context, $"{creature.Name} tears free of a Barbed Snare", new DamageCreatureAction(creature, slot.Effect.Value));
        }

        // The effect is queued as an environment action, and a "1 Trigger" effect is removed (and so revealed) right away
        private static void Trigger(BoardSlot slot, ActionExecutionContext context, string what, IGameAction effect) {
            Log($"{what} ({slot})", LogTag.Effects);
            if (slot.Effect.IsOneTrigger) Remove(slot, context.GameMediator);
            using (context.ActionsQueue.QueueingEnvironment()) {
                context.ActionsQueue.AddAction(effect);
            }
        }

        /// <summary>Temporal Shield: the creature standing on it takes no combat damage.</summary>
        public static bool BlocksCombatDamage(ICreature creature) => creature?.Slot?.Effect?.Type == SlotEffectType.TemporalShield;

        /// <summary>Scorched Earth: the passive abilities of the creature standing on it don't trigger.</summary>
        public static bool SuppressesPassiveAbilities(ICreature creature) => creature?.Slot?.Effect?.Type == SlotEffectType.ScorchedEarth;

        /// <summary>At Dusk: Slot Effects whose Days ran out are removed.</summary>
        public static void ProcessEndOfTurn(IEnumerable<IPlayer> players, int endedTurn, IGameMediator mediator) {
            foreach (var slot in players.Where(p => p?.Battlefield != null).SelectMany(p => p.Battlefield)) {
                if (slot.Effect != null && slot.Effect.HasExpired(endedTurn + 1)) Remove(slot, mediator);
            }
        }
    }
}
