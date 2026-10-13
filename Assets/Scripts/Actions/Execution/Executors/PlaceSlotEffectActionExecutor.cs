using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class PlaceSlotEffectActionExecutor : ActionExecutor<PlaceSlotEffectAction> {
        protected override void ExecuteAction(PlaceSlotEffectAction place, ActionExecutionContext context) {
            if (place.GetSlot() == null || place.GetEffectType() == SlotEffectType.None) return;

            var effect = new SlotEffect(place.GetEffectType(), place.GetController(), place.GetValue(), place.GetDays(), context.TurnClock.TurnNumber);
            SlotEffectRules.Place(place.GetSlot(), effect, context.GameMediator);
        }
    }

    public class CleanseSlotActionExecutor : ActionExecutor<CleanseSlotAction> {
        protected override void ExecuteAction(CleanseSlotAction cleanse, ActionExecutionContext context) =>
            SlotEffectRules.Remove(cleanse.GetSlot(), context.GameMediator, context.ActionsQueue);
    }
}
