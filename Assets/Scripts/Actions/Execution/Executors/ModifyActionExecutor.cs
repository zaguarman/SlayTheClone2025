using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class ModifyActionExecutor : ActionExecutor<ModifyAction> {
        protected override void ExecuteAction(ModifyAction modifyAction, ActionExecutionContext context) {
            var targetCreature = modifyAction.GetTarget() as ICreature;
            if (targetCreature == null) {
                LogWarning("ModifyActionExecutor: Target creature is null.", LogTag.Actions | LogTag.Effects);
                return;
            }

            int value = modifyAction.GetValue();
            int duration = modifyAction.GetDuration();

            // Health Loss (-X Health, GDD glossary) is permanent unless the effect says otherwise: a Persistent Stat
            // Modification of base Health, which stays on the card. Maximum and current Health both drop by X.
            // A timed -X Health is a regular stat change instead, and gives the Health back when it ends.
            bool isHealthLoss = modifyAction.ShouldModifyHealth() && value < 0 && duration <= 0
                && modifyAction.GetCalculationType() == ModifierCalculationType.Flat;
            if (isHealthLoss) {
                context.ModifierManager.ChangeBaseStats(targetCreature, 0, value, 0);
            }

            StatModifiers.Apply(context.ModifierManager, context.ModifierFactory, targetCreature, value,
                modifyAction.ShouldModifyAttack(), modifyAction.ShouldModifyHealth() && !isHealthLoss, modifyAction.ShouldModifySpeed(),
                modifyAction.GetCalculationType(), duration, context.TurnClock.TurnNumber);

            // Losing Health this way isn't damage (no Armor, no damage modifiers, no OnDamaged), but at 0 the creature
            // still goes through the death sequence (GDD 3.3)
            if (targetCreature.Health <= 0 && !targetCreature.IsDead) {
                context.ActionsQueue.AddAction(new CheckLethalAction(targetCreature));
            }

            Log($"Executed ModifyAction via Executor for {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
        }
    }
}
