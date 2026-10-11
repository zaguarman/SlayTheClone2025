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
            string effectDescription = $"Applies {statusType} for {duration} turns";
            if (potency > 0 && statusType == StatusEffectType.Burned) {
                effectDescription += $" (Potency: {potency})";
            }

            IModifier statusModifier = context.ModifierFactory.CreateStatusEffectModifier(
                effectName, effectDescription, statusType, duration, potency, context.TurnClock.TurnNumber);

            context.ModifierManager.ApplyModifier(targetCreature, statusModifier);
            Log($"Executed ApplyStatusEffectAction via Executor: Applied {statusType} to {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
        }
    }
}
