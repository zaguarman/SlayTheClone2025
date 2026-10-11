using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class SwapCreaturesActionExecutor : ActionExecutor<SwapCreaturesAction> {
        protected override void ExecuteAction(SwapCreaturesAction swapAction, ActionExecutionContext context) {
            var fromSlot = swapAction.GetFromSlot();
            var toSlot = swapAction.GetToSlot();

            if (fromSlot == null || toSlot == null) {
                LogError("SwapCreaturesActionExecutor: One or both slots are null", LogTag.Actions);
                return;
            }

            // Only the creature that starts the swap has to be able to act; the other one is moved passively
            var initiator = swapAction.GetCreature1();
            if (initiator != null && !context.ModifierManager.CanAct(initiator)) return;

            // Either slot may be empty: swapping with an empty slot moves the creature
            BoardSlot.Swap(fromSlot, toSlot);
            NotifyBattlefieldsChanged(context, fromSlot.Owner, toSlot.Owner);

            Log($"Executed SwapCreaturesAction via Executor: {swapAction.GetCreature1()?.Name ?? "Slot"} swapped with {swapAction.GetCreature2()?.Name ?? "Slot"}", LogTag.Actions | LogTag.Creatures);
        }
    }
}
