using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class ModifyActionExecutor : ActionExecutor<ModifyAction> {
        protected override void ExecuteAction(ModifyAction modifyAction, ActionExecutionContext context) {
            var targetCreature = modifyAction.GetTarget() as ICreature;
            if (targetCreature == null) {
                LogWarning("ModifyActionExecutor: Target creature is null.", LogTag.Actions | LogTag.Effects);
                return;
            }

            var modifierManager = context.ModifierManager;
            var factory = context.ModifierFactory;
            var turnManager = context.TurnManager;

            if (modifierManager == null || factory == null) {
                LogError("ModifyActionExecutor: ModifierManager or Factory is null in context.", LogTag.Actions | LogTag.Effects | LogTag.Initialization);
                return;
            }
            // Timed modifiers need the current turn
            if (turnManager == null) {
                LogError("ModifyActionExecutor: TurnManager is null in context. Cannot process timed modifiers.", LogTag.Actions | LogTag.Effects | LogTag.Initialization);
                return;
            }

            StatModifiers.Apply(modifierManager, factory, targetCreature, modifyAction.GetValue(),
                modifyAction.ShouldModifyAttack(), modifyAction.ShouldModifyHealth(), modifyAction.ShouldModifySpeed(),
                modifyAction.GetCalculationType(), modifyAction.GetDuration(), turnManager.TurnNumber);

            Log($"Executed ModifyAction via Executor for {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
        }
    }
}
