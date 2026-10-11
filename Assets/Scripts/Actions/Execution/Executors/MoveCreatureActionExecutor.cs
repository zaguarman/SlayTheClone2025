using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class MoveCreatureActionExecutor : ActionExecutor<MoveCreatureAction> {
        protected override void ExecuteAction(MoveCreatureAction moveAction, ActionExecutionContext context) {
            var creature = moveAction.GetCreature();
            var player = moveAction.GetPlayer();
            var fromSlot = moveAction.GetFromSlot();
            var toSlot = moveAction.GetToSlot();

            if (creature == null || fromSlot == null || toSlot == null || player == null) {
                LogError("MoveCreatureActionExecutor: One or more required components are null", LogTag.Actions);
                return;
            }

            var modManager = context.ModifierManager;
            if (modManager != null && creature is Creature concreteCreature && modManager.AreActionsPrevented(concreteCreature)) {
                Log($"Executor: Creature {creature.Name} cannot move due to status effect.", LogTag.Actions | LogTag.Effects);
                return;
            }

            // Moving onto an occupied slot swaps the two creatures
            bool isSwap = toSlot.IsOccupied();
            BoardSlot.Swap(fromSlot, toSlot);
            if (isSwap) {
                Log($"Executor: Swapped {creature.Name} with {fromSlot.OccupyingCreature?.Name ?? "Unknown"}", LogTag.Actions | LogTag.Creatures);
            }
            else {
                Log($"Executor: Moved {creature.Name} from slot {fromSlot.TargetId} to {toSlot.TargetId}", LogTag.Actions | LogTag.Creatures);
            }

            NotifyBattlefieldsChanged(context, fromSlot.Owner ?? player, toSlot.Owner);
            Log($"Executed MoveCreatureAction via Executor for {creature.Name}", LogTag.Actions | LogTag.Creatures);
        }
    }
}
