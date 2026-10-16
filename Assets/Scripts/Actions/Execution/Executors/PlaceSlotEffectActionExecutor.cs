using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class PlaceSlotEffectActionExecutor : ActionExecutor<PlaceSlotEffectAction> {
        protected override void ExecuteAction(PlaceSlotEffectAction place, ActionExecutionContext context) {
            if (place.GetSlot() == null || place.GetEffectType() == SlotEffectType.None) return;

            var effect = new SlotEffect(place.GetEffectType(), place.GetController(), place.GetValue(), place.GetDays(), context.TurnClock.TurnNumber) {
                Source = place.Source
            };
            SlotEffectRules.Place(place.GetSlot(), effect, context.GameMediator);
        }
    }

    // A trap going off: nothing happens by itself, the screen shows its card while what it does lands (ResolutionPresenter)
    public class TrapSprungActionExecutor : ActionExecutor<TrapSprungAction> {
        protected override void ExecuteAction(TrapSprungAction sprung, ActionExecutionContext context) { }
    }

    public class CleanseSlotActionExecutor : ActionExecutor<CleanseSlotAction> {
        protected override void ExecuteAction(CleanseSlotAction cleanse, ActionExecutionContext context) =>
            SlotEffectRules.Remove(cleanse.GetSlot(), context.GameMediator, context.ActionsQueue);
    }
}
