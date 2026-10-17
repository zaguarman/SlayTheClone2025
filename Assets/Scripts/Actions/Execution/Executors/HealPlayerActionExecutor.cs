using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    public class HealPlayerActionExecutor : ActionExecutor<HealPlayerAction> {
        protected override void ExecuteAction(HealPlayerAction healPlayerAction, ActionExecutionContext context) {
            var targetPlayer = healPlayerAction.GetTargetPlayer();
            var amount = healPlayerAction.GetAmount();

            if (targetPlayer == null) {
                LogError("HealPlayerActionExecutor: Target player is null", LogTag.Actions | LogTag.Players);
                return;
            }
            if (amount <= 0) {
                LogWarning($"HealPlayerActionExecutor: Heal amount ({amount}) is zero or negative. Skipping.", LogTag.Actions | LogTag.Players);
                return;
            }

            var mediator = context.GameMediator;
            if (mediator == null) {
                LogError("HealPlayerActionExecutor: GameMediator is null in context.", LogTag.Actions | LogTag.Initialization);
                return;
            }

            Log($"Executor: Intent to heal {targetPlayer.Name} for {amount}", LogTag.Actions | LogTag.Players | LogTag.Effects);

            int actualHealAmount = targetPlayer.Heal(amount); // Player.Heal handles health increase and UI update
            if (actualHealAmount > 0) {
                mediator.NotifyPlayerHealed(targetPlayer, actualHealAmount);
            }

            Log($"Executed HealPlayerAction via Executor for {targetPlayer.Name}. Healed for {amount} (Actual: {actualHealAmount}). Health: {targetPlayer.Health}", LogTag.Actions | LogTag.Players | LogTag.Effects);
        }
    }
}
