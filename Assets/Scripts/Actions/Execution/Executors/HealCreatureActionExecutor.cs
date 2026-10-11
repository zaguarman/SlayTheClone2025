using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class HealCreatureActionExecutor : ActionExecutor<HealCreatureAction> {
        protected override void ExecuteAction(HealCreatureAction healAction, ActionExecutionContext context) {
            var targetCreature = healAction.GetTarget();
            var amount = healAction.GetAmount();

            var mediator = context.GameMediator;
            if (mediator == null) {
                LogError("HealCreatureActionExecutor: GameMediator is null in context.", LogTag.Actions | LogTag.Initialization);
                return;
            }

            if (targetCreature == null || targetCreature.IsDead) return;

            int actualHealAmount = targetCreature.Heal(amount);
            if (actualHealAmount > 0) {
                mediator.NotifyCreatureHealed(targetCreature, actualHealAmount);
            }

            Log($"Executed HealCreatureAction via Executor: Healed {targetCreature.Name} for {amount} (Actual: {actualHealAmount}). Health: {targetCreature.Health}/{targetCreature.MaxHealth}", LogTag.Actions | LogTag.Creatures | LogTag.Effects);
        }
    }
}
