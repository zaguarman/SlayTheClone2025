using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class ApplyStatusEffectActionExecutor : ActionExecutor<ApplyStatusEffectAction> {
        protected override void ExecuteAction(ApplyStatusEffectAction statusAction, ActionExecutionContext context) {
            var targetCreature = statusAction.GetTargetCreature();
            var statusType = statusAction.GetStatusType();
            var duration = statusAction.GetDuration();
            var potency = statusAction.GetPotency();

            if (targetCreature == null || statusType == StatusEffectType.None) {
                LogWarning("ApplyStatusEffectActionExecutor: Target creature null or status type is None.", LogTag.Actions | LogTag.Effects);
                return;
            }


            string effectName = $"{statusType} Effect";
            string days = duration == 1 ? "1 Day" : $"{duration} Days";
            string effectDescription = statusType == StatusEffectType.Exhausted
                ? $"Exhausted ({days}): can't act, abilities off, takes +1 damage"
                : $"{statusType} ({days})";
            if (potency > 0 && statusType == StatusEffectType.Burned) {
                effectDescription += $" (Potency: {potency})";
            }

            // Re-stunning refreshes Exhausted instead of stacking it into a longer stun (Status Effects Compendium)
            if (statusType == StatusEffectType.Exhausted) {
                foreach (var old in context.ModifierManager.GetActiveModifiersFor(targetCreature).OfType<StatusEffectModifier>()
                             .Where(s => s.EffectType == StatusEffectType.Exhausted).ToList()) {
                    context.ModifierManager.RemoveModifier(targetCreature, old);
                }
            }

            var statusModifier = (StatusEffectModifier)context.ModifierFactory.CreateStatusEffectModifier(
                effectName, effectDescription, statusType, duration, potency, context.TurnClock.TurnNumber);

            context.ModifierManager.ApplyModifier(targetCreature, statusModifier);

            // A creature that can't act loses what it planned (GDD 3.8): its attack, move and ability actions
            if (statusModifier.PreventsActions()) {
                context.ActionsQueue.CancelActionsOf(targetCreature);
            }
            Log($"Executed ApplyStatusEffectAction via Executor: Applied {statusType} to {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
        }
    }
}
