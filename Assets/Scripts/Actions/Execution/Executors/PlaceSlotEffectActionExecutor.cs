using static Zeitgeist.Enums;

namespace Zeitgeist {
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

    public class SwapSlotEffectsActionExecutor : ActionExecutor<SwapSlotEffectsAction> {
        protected override void ExecuteAction(SwapSlotEffectsAction swap, ActionExecutionContext context) {
            var mine = swap.Controller?.Battlefield;
            var theirs = swap.Controller?.Opponent?.Battlefield;
            if (mine == null || theirs == null) return;
            for (int i = 0; i < System.Math.Min(mine.Count, theirs.Count); i++) {
                (mine[i].Effect, theirs[i].Effect) = (theirs[i].Effect, mine[i].Effect);
            }
            context.GameMediator.NotifyBattlefieldStateChanged(swap.Controller);
            context.GameMediator.NotifyBattlefieldStateChanged(swap.Controller.Opponent);
        }
    }

    public class CleanseSlotActionExecutor : ActionExecutor<CleanseSlotAction> {
        protected override void ExecuteAction(CleanseSlotAction cleanse, ActionExecutionContext context) =>
            SlotEffectRules.Remove(cleanse.GetSlot(), context.GameMediator, context.ActionsQueue);
    }
}
