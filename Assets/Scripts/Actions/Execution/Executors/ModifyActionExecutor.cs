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

            // Losing Health this way isn't damage, but at 0 the creature still goes through the death sequence (GDD 3.3)
            if (targetCreature.Health <= 0 && !targetCreature.IsDead) {
                context.ActionsQueue.AddAction(new CheckLethalAction(targetCreature));
            }

            Log($"Executed ModifyAction via Executor for {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
        }
    }
}
