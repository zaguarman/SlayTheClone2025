using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// What happens when a status would land on a creature ('Status Effects Compendium'): Doomed blocks it, Blessed negates it
    /// and loses a stack, Doomed wipes the others, re-stunning refreshes Exhausted, and a status that stops the creature
    /// from acting cancels what it planned (GDD 3.8). Used by every way a status arrives: applied, or passed on (Heresy).
    /// </summary>
    public static class StatusRules {
        public static StatusEffectModifier Find(IModifierManager manager, ICreature creature, StatusEffectType type) =>
            manager.GetActiveModifiersFor(creature).OfType<StatusEffectModifier>().FirstOrDefault(s => s.EffectType == type);

        /// <param name="turnApplied">The Day its countdown started: today, or the original Day for a status passed on.</param>
        /// <returns>The status now on the creature, or null if it didn't land.</returns>
        public static StatusEffectModifier Land(ActionExecutionContext context, ICreature target, StatusEffectType type,
            int duration, int potency, int turnApplied) {
            var manager = context.ModifierManager;

            // A Doomed creature is immune to new statuses, and Doomed itself ignores re-application
            if (manager.HasStatusEffect(target, StatusEffectType.Doomed)) {
                Log($"{target.Name} is Doomed: {type} doesn't land", LogTag.Actions | LogTag.Effects);
                return null;
            }

            var blessed = Find(manager, target, StatusEffectType.Blessed);
            if (type == StatusEffectType.Blessed && blessed != null) {
                blessed.SetStacks(blessed.Stacks + System.Math.Max(1, potency)); // Re-applying adds stacks, up to 3
                Log($"{target.Name} is now {blessed.Description}", LogTag.Actions | LogTag.Effects);
                context.GameMediator.NotifyCreatureStatusChanged(target, StatusEffectType.Blessed, true);
                return blessed;
            }
            // Blessed wards against the next status, friendly or not, Doomed included
            if (blessed != null) {
                if (blessed.Stacks > 1) blessed.SetStacks(blessed.Stacks - 1);
                else manager.RemoveModifier(target, blessed);
                Log($"{target.Name}'s Blessing negates {type}", LogTag.Actions | LogTag.Effects);
                return null;
            }

            // More Heat on a creature that has some adds to it
            var heat = Find(manager, target, StatusEffectType.HeatSignature);
            if (type == StatusEffectType.HeatSignature && heat != null) {
                heat.SetStacks(heat.Stacks + System.Math.Max(1, potency));
                target.Heat = heat.Stacks;
                Log($"{target.Name} is now at {heat.Description}", LogTag.Actions | LogTag.Effects);
                context.GameMediator.NotifyCreatureStatusChanged(target, StatusEffectType.HeatSignature, true);
                return heat;
            }

            // Doomed wipes every other status on application, cleansable or not
            if (type == StatusEffectType.Doomed) {
                foreach (var other in manager.GetActiveModifiersFor(target).OfType<StatusEffectModifier>().ToList()) {
                    manager.RemoveModifier(target, other);
                }
            }
            // Re-stunning refreshes Exhausted instead of stacking it into a longer stun
            if (type == StatusEffectType.Exhausted) {
                foreach (var old in manager.GetActiveModifiersFor(target).OfType<StatusEffectModifier>()
                             .Where(s => s.EffectType == StatusEffectType.Exhausted).ToList()) {
                    manager.RemoveModifier(target, old);
                }
            }

            var status = (StatusEffectModifier)context.ModifierFactory.CreateStatusEffectModifier(
                $"{type} Effect", Describe(type, duration, potency), type, duration, potency, turnApplied);
            manager.ApplyModifier(target, status);

            // A creature that can't act loses what it planned (GDD 3.8): its attack, move and ability actions
            if (status.PreventsActions()) {
                context.ActionsQueue.CancelActionsOf(target);
            }
            return status;
        }

        private static string Describe(StatusEffectType type, int duration, int potency) {
            string days = duration == 1 ? "1 Day" : $"{duration} Days";
            return type switch {
                StatusEffectType.Exhausted => $"Exhausted ({days}): can't act, abilities off, takes +1 damage",
                StatusEffectType.Doomed => $"Doomed ({days}): +1 Attack each Dawn, then Last Rites and it dies",
                StatusEffectType.Burned => $"Burned ({days})" + (potency > 0 ? $" (Potency: {potency})" : ""),
                StatusEffectType.Blessed or StatusEffectType.HeatSignature => type.ToString(), // Its stacks are added by the status itself
                _ => $"{type} ({days})"
            };
        }
    }
}
