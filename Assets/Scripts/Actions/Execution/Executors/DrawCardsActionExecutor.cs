using static DebugLogger;
using static Enums;

/// <summary>
/// Executor for DrawCardsAction
/// </summary>
public class DrawCardsActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is DrawCardsAction drawAction))
        {
            LogError($"DrawCardsActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // Get data from Action
        var player = drawAction.GetPlayer();
        var amount = drawAction.GetAmount();

        // Validate
        if (player == null)
        {
            LogError("DrawCardsActionExecutor: Player is null", LogTag.Actions | LogTag.Cards);
            return;
        }

        // Get dependencies from context
        var cardDealingService = context.CardDealingService;
        if (cardDealingService == null)
        {
            LogError("DrawCardsActionExecutor: CardDealingService is null in context", LogTag.Actions | LogTag.Cards);
            return;
        }

        // Execute logic
        cardDealingService.DrawCards(player, amount);
        Log($"Executed DrawCardsAction via Executor: Drew {amount} cards for {(player.IsPlayer1 ? "P1" : "P2")}", LogTag.Actions | LogTag.Cards);
    }
}
