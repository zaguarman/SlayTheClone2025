using static DebugLogger;
using static Enums;

/// <summary>
/// Executor for SwapCreaturesAction
/// </summary>
public class SwapCreaturesActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is SwapCreaturesAction swapAction))
        {
             LogError($"SwapCreaturesActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // --- Get data from Action ---
        var fromCreature = swapAction.GetCreature1();
        var toCreature = swapAction.GetCreature2();
        var fromSlot = swapAction.GetFromSlot();
        var toSlot = swapAction.GetToSlot();

        // --- Validate ---
        if (fromSlot == null || toSlot == null) {
            LogError("SwapCreaturesActionExecutor: One or both slots are null", LogTag.Actions);
            return;
        }
        // It's okay if creatures are null (e.g., swapping with an empty slot)

        // --- Execute Logic (moved from SwapCreaturesAction.Execute) ---
        var fromCard = fromSlot.OccupyingCard;
        var toCard = toSlot.OccupyingCard;

        fromSlot.ClearSlot(false);
        toSlot.ClearSlot(false);

        if (fromCard != null) toSlot.AssignCreature(fromCard);
        if (toCard != null) fromSlot.AssignCreature(toCard);

        Log($"Executed SwapCreaturesAction via Executor: {fromCreature?.Name ?? "Slot"} swapped with {toCreature?.Name ?? "Slot"}", LogTag.Actions | LogTag.Creatures);
    }
}
