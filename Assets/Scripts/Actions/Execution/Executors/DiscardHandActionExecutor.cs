using static DebugLogger;
using static Enums;

/// <summary>
/// Executor for DiscardHandAction
/// </summary>
public class DiscardHandActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is DiscardHandAction discardAction))
        {
            LogError($"DiscardHandActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // --- Get data from Action ---
        var player = discardAction.GetPlayer();

        // --- Validate ---
        if (player == null) {
            LogError($"DiscardHandActionExecutor: Player is null", LogTag.Actions | LogTag.Cards);
            return;
        }

        // --- Execute Logic (moved from DiscardHandAction.Execute) ---
        int initialHandCount = player.Hand.Count;
        player.DiscardHand(); // Call the player's method

        Log($"Executed DiscardHandAction via Executor for {(player.IsPlayer1 ? "P1" : "P2")} - discarded {initialHandCount} cards", LogTag.Actions | LogTag.Cards);
    }
}
