using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class ModifyArmorActionExecutor : ActionExecutor<ModifyArmorAction> {
        protected override void ExecuteAction(ModifyArmorAction armorAction, ActionExecutionContext context) {
            var targetCreature = armorAction.GetTargetCreature();
            var amount = armorAction.GetAmount();

            var mediator = context.GameMediator;
            if (mediator == null) {
                LogError("ModifyArmorActionExecutor: GameMediator is null in context.", LogTag.Actions | LogTag.Initialization);
                return;
            }

            if (targetCreature == null) {
                LogWarning("ModifyArmorActionExecutor: Target creature is null.", LogTag.Actions | LogTag.Effects);
                return;
            }

            int previousArmor = targetCreature.CurrentArmorPool;
            targetCreature.ModifyArmorPool(amount);
            if (targetCreature.CurrentArmorPool == previousArmor) return; // e.g. removing armor from a creature that has none

            mediator.NotifyCreatureArmorChanged(targetCreature, targetCreature.CurrentArmorPool);
            Log($"Executed ModifyArmorAction via Executor: Modified armor for {targetCreature.Name} by {amount}. New Pool: {targetCreature.CurrentArmorPool}", LogTag.Actions | LogTag.Effects | LogTag.Creatures);
        }
    }
}
