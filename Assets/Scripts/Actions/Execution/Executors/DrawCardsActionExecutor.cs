using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class DrawCardsActionExecutor : ActionExecutor<DrawCardsAction> {
        protected override void ExecuteAction(DrawCardsAction drawAction, ActionExecutionContext context) {
            var player = drawAction.GetPlayer();
            var amount = drawAction.GetAmount();

            if (player == null) {
                LogError("DrawCardsActionExecutor: Player is null", LogTag.Actions | LogTag.Cards);
                return;
            }

            var cardDealingService = context.CardDealingService;
            if (cardDealingService == null) {
                LogError("DrawCardsActionExecutor: CardDealingService is null in context", LogTag.Actions | LogTag.Cards);
                return;
            }

            // The hand is discarded at the end of the Day: cards drawn while it resolves come with the next Day's hand
            if (drawAction.FromEffect && context.TurnClock.DrawsWaitForNextDay) {
                player.ExtraDraws += amount;
                Log($"{player.Name} will draw {amount} more next Day", LogTag.Actions | LogTag.Cards);
                return;
            }
            if (drawAction.DealsDay) cardDealingService.DealDay(player, amount);
            else cardDealingService.DrawCards(player, amount);
            Log($"Executed DrawCardsAction via Executor: Drew {amount} cards for {player.Name}", LogTag.Actions | LogTag.Cards);
        }
    }
}
