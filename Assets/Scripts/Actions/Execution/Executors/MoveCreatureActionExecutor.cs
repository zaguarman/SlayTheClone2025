using static DebugLogger;
using static Enums;

/// <summary>
/// Executor for MoveCreatureAction
/// </summary>
public class MoveCreatureActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is MoveCreatureAction moveAction))
        {
            LogError($"MoveCreatureActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // Get data from Action
        var creature = moveAction.GetCreature();
        var fromSlotTarget = moveAction.GetFromSlot();
        var toSlotTarget = moveAction.GetToSlot();
        var player = moveAction.GetPlayer();

        // Validate
        if (creature == null || fromSlotTarget == null || toSlotTarget == null || player == null)
        {
            LogError("MoveCreatureActionExecutor: One or more required components are null", LogTag.Actions);
            return;
        }

        if (!(fromSlotTarget is BattlefieldSlot fromSlot) || !(toSlotTarget is BattlefieldSlot toSlot))
        {
            LogError("MoveCreatureActionExecutor: Invalid slot types", LogTag.Actions);
            return;
        }

        // Get Dependencies from Context
        var modManager = context.ModifierManager;

        // Execute Logic
        if (modManager != null && creature is Creature concreteCreature && modManager.AreActionsPrevented(concreteCreature))
        {
            Log($"Executor: Creature {creature.Name} cannot move due to status effect.", LogTag.Actions | LogTag.Effects);
            return;
        }

        if (toSlot.IsOccupied())
        {
            // Handle Swap
            var fromCard = fromSlot.OccupyingCard;
            var toCard = toSlot.OccupyingCard;
            fromSlot.ClearSlot(false);
            toSlot.ClearSlot(false);
            if (fromCard != null) toSlot.AssignCreature(fromCard);
            if (toCard != null) fromSlot.AssignCreature(toCard);
            Log($"Executor: Swapped {creature.Name} with {toSlot.OccupyingCreature?.Name ?? "Unknown"}", LogTag.Actions | LogTag.Creatures);
        }
        else
        {
            // Handle Move
            var card = fromSlot.OccupyingCard;
            fromSlot.ClearSlot(false);
            if (card != null) toSlot.AssignCreature(card);
            Log($"Executor: Moved {creature.Name} from slot {fromSlot.TargetId} to {toSlot.TargetId}", LogTag.Actions | LogTag.Creatures);
        }

        Log($"Executed MoveCreatureAction via Executor for {creature.Name}", LogTag.Actions | LogTag.Creatures);
    }
}
