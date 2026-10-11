using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class DiscardHandActionExecutor : ActionExecutor<DiscardHandAction> {
        protected override void ExecuteAction(DiscardHandAction discardAction, ActionExecutionContext context) {
            var player = discardAction.GetPlayer();
            if (player == null) {
                LogError("DiscardHandActionExecutor: Player is null", LogTag.Actions | LogTag.Cards);
                return;
            }

            int initialHandCount = player.Hand.Count;
            player.DiscardHand();

            Log($"Executed DiscardHandAction via Executor for {player.Name} - discarded {initialHandCount} cards", LogTag.Actions | LogTag.Cards);
        }
    }
}
