using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class ModifyActionExecutor : ActionExecutor<ModifyAction> {
        protected override void ExecuteAction(ModifyAction modifyAction, ActionExecutionContext context) {
            var targetCreature = modifyAction.GetTarget() as ICreature;
            if (targetCreature == null) {
                LogWarning("ModifyActionExecutor: Target creature is null.", LogTag.Actions | LogTag.Effects);
                return;
            }

            StatModifiers.Apply(context.ModifierManager, context.ModifierFactory, targetCreature, modifyAction.GetValue(),
                modifyAction.ShouldModifyAttack(), modifyAction.ShouldModifyHealth(), modifyAction.ShouldModifySpeed(),
                modifyAction.GetCalculationType(), modifyAction.GetDuration(), context.TurnClock.TurnNumber);

            Log($"Executed ModifyAction via Executor for {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
        }
    }
}
