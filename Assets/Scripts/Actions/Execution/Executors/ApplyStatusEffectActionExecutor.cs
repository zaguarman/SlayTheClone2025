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

            var modifierManager = context.ModifierManager;
            var factory = context.ModifierFactory;
            var turnManager = context.TurnManager;

            if (turnManager == null) {
                LogError($"ApplyStatusEffectActionExecutor: TurnManager is null in context. Cannot apply {statusType} to {targetCreature.Name}.", LogTag.Actions | LogTag.Effects | LogTag.Initialization);
                return;
            }
            if (modifierManager == null || factory == null) {
                LogError($"ApplyStatusEffectActionExecutor: ModifierManager or Factory is null in context. Cannot apply {statusType} to {targetCreature.Name}.", LogTag.Actions | LogTag.Effects | LogTag.Initialization);
                return;
            }

            string effectName = $"{statusType} Effect";
            string effectDescription = $"Applies {statusType} for {duration} turns";
            if (potency > 0 && (statusType == StatusEffectType.Burned || statusType == StatusEffectType.Poisoned)) {
                effectDescription += $" (Potency: {potency})";
            }

            IModifier statusModifier = factory.CreateStatusEffectModifier(
                effectName, effectDescription, statusType, duration, potency, turnManager.TurnNumber);

            modifierManager.ApplyModifier(targetCreature, statusModifier);
            Log($"Executed ApplyStatusEffectAction via Executor: Applied {statusType} to {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
        }
    }
}
