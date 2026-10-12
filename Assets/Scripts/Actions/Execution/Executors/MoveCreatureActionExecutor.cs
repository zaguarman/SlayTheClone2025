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

            // Only the creature that moves has to be able to act; one it pushes or swaps with is moved passively
            if (!context.ModifierManager.CanAct(creature)) return;

            // The creature moves from wherever it is now: something earlier this turn may have moved it.
            // Blocked on the way, it stops at the furthest empty slot it can still reach (GDD 2.4)
            var fromSlot = creature.Slot;
            var outcome = MovementRules.Resolve(creature, toSlot, moveAction.Kind);
            if (outcome.End == null || outcome.End == fromSlot) {
                Log($"Executor: {creature.Name} can't move toward {toSlot}", LogTag.Actions | LogTag.Creatures);
                return;
            }

            MovementRules.Apply(creature, moveAction.Kind, outcome);
            Log($"Executor: {creature.Name} {moveAction.Kind} from {fromSlot} to {outcome.End}" +
                (outcome.Partner != null ? $", moving {outcome.Partner.Name} to {outcome.PartnerEnd}" : "") +
                (outcome.ReachesDestination ? "" : $" (blocked short of {toSlot})"), LogTag.Actions | LogTag.Creatures);

            NotifyBattlefieldsChanged(context, fromSlot.Owner ?? player, toSlot.Owner);
            Log($"Executed MoveCreatureAction via Executor for {creature.Name}", LogTag.Actions | LogTag.Creatures);
        }
    }
}
