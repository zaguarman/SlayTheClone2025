using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class MoveCreatureActionExecutor : ActionExecutor<MoveCreatureAction> {
        protected override void ExecuteAction(MoveCreatureAction moveAction, ActionExecutionContext context) {
            var creature = moveAction.GetCreature();
            var player = moveAction.GetPlayer();
            var toSlot = moveAction.GetToSlot();

            if (creature == null || toSlot == null || player == null) {
                LogError("MoveCreatureActionExecutor: One or more required components are null", LogTag.Actions);
                return;
            }

            // Only the creature that moves has to be able to act; the one it swaps with is moved passively
            if (!context.ModifierManager.CanAct(creature)) return;

            // The creature moves from wherever it is now: something earlier this turn may have moved it,
            // and whatever took its starting slot must not be carried off in its place
            var fromSlot = creature.Slot;
            if (fromSlot == toSlot) return;

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
