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

        /// <summary>Blessed takes a blow for it (a status, or being destroyed): one stack is spent. False: it isn't Blessed.</summary>
        public static bool SpendBlessing(ActionExecutionContext context, ICreature creature) {
            var manager = context.ModifierManager;
            var blessed = Find(manager, creature, StatusEffectType.Blessed);
            if (blessed == null) return false;
            if (blessed.Stacks > 1) blessed.SetStacks(blessed.Stacks - 1);
            else manager.RemoveModifier(creature, blessed);
            context.GameMediator.NotifyCreatureStatusChanged(creature, StatusEffectType.Blessed, blessed.Stacks > 1);
            return true;
        }

        /// <param name="turnApplied">The Day its countdown started: today, or the original Day for a status passed on.</param>
        /// <returns>The status now on the creature, or null if it didn't land.</returns>
        /// <param name="revealer">For Revealed: the creature whose effect it is, if any (If I Told You strikes back at it).</param>
        public static StatusEffectModifier Land(ActionExecutionContext context, ICreature target, StatusEffectType type,
            int duration, int potency, int turnApplied, ICreature revealer = null) {
            var manager = context.ModifierManager;

            // A Doomed creature is immune to new statuses, and Doomed itself ignores re-application. Borrowed Time is the one
            // way in: on it when the sentence is carried out, it survives (Die Another Day)
            if (manager.HasStatusEffect(target, StatusEffectType.Doomed) && type != StatusEffectType.BorrowedTime) {
                Log($"{target.Name} is Doomed: {type} doesn't land", LogTag.Actions | LogTag.Effects);
                return null;
            }

            // Composed (Shaken, Not Stirred): stuns don't land on it
            if (type == StatusEffectType.Exhausted && manager.HasStatusEffect(target, StatusEffectType.Composed)) {
                Log($"{target.Name} is Composed: it isn't Exhausted", LogTag.Actions | LogTag.Effects);
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
            if (blessed != null && SpendBlessing(context, target)) {
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
            // Re-stunning refreshes Exhausted instead of stacking it into a longer stun; the Intelligence statuses also refresh
            // (re-applying sets them to the new amount)
            if (type is StatusEffectType.Exhausted or StatusEffectType.Concealed or StatusEffectType.Revealed or StatusEffectType.Encrypted
                or StatusEffectType.Untouchable or StatusEffectType.Cooldown or StatusEffectType.Brainwashed or StatusEffectType.KillOrder
                or StatusEffectType.BorrowedTime or StatusEffectType.Licensed or StatusEffectType.Composed or StatusEffectType.Blackout
                or StatusEffectType.Survivor or StatusEffectType.Burnout) {
                foreach (var old in manager.GetActiveModifiersFor(target).OfType<StatusEffectModifier>()
                             .Where(s => s.EffectType == type).ToList()) {
                    manager.RemoveModifier(target, old);
                }
            }

            // Composed shakes off a stun it already has
            if (type == StatusEffectType.Composed) {
                foreach (var stun in manager.GetActiveModifiersFor(target).OfType<StatusEffectModifier>()
                             .Where(s => s.EffectType == StatusEffectType.Exhausted).ToList()) {
                    manager.RemoveModifier(target, stun);
                }
            }

            // Revealed shows the creature to the other side for good, whatever it is
            if (type == StatusEffectType.Revealed) {
                Intel.MakeKnown(target, target.Owner?.Opponent);
                Intel.OnRevealed(target, revealer, context.ActionsQueue);
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
                StatusEffectType.Concealed => $"Concealed ({days}): its plans show as ??? to the enemy",
                StatusEffectType.Revealed => $"Revealed ({days}): its plans can be seen",
                StatusEffectType.Encrypted => $"Encrypted ({days}): Watchdogs ignore its actions",
                StatusEffectType.Untouchable => $"Untouchable ({days}): takes no damage",
                StatusEffectType.Cooldown => $"Cooldown ({days}): its Active ability rests",
                StatusEffectType.Brainwashed => $"Brainwashed ({days}): then it changes sides, its ability lost",
                StatusEffectType.KillOrder => $"Kill Order ({days}): then it's destroyed",
                StatusEffectType.Rooted => $"Rooted ({days}): it can't move, and nothing can move it",
                StatusEffectType.Burnout => $"Burnout ({days}): then it's destroyed",
                StatusEffectType.BorrowedTime => $"Borrowed Time ({days}): survives lethal damage",
                StatusEffectType.Composed => $"Composed ({days}): it can't be Exhausted",
                StatusEffectType.Blackout => $"Blackout ({days}): its ability and effects are off",
                StatusEffectType.Licensed => "Licensed: its next attack on a creature destroys it",
                StatusEffectType.Survivor => "Survivor: from full Health, a killing hit leaves it at 1",
                StatusEffectType.Blessed or StatusEffectType.HeatSignature => type.ToString(), // Its stacks are added by the status itself
                _ => $"{type} ({days})"
            };
        }
    }
}
