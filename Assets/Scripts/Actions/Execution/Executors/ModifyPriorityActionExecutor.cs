using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class ModifyPriorityActionExecutor : ActionExecutor<ModifyPriorityAction> {
        protected override void ExecuteAction(ModifyPriorityAction priorityAction, ActionExecutionContext context) {
            if (priorityAction.GetAmount() == 0) return;

            var modifier = new PriorityModifier(priorityAction.GetAmount(), priorityAction.GetDays(), context.TurnClock.TurnNumber);
            switch (priorityAction.GetTarget()) {
                case ICreature creature when !creature.IsDead:
                    context.ModifierManager.ApplyModifier(creature, modifier);
                    break;
                case IPlayer player:
                    context.ModifierManager.ApplySideModifier(player, modifier);
                    break;
                default:
                    LogWarning($"ModifyPriorityActionExecutor: nothing to change the Priority of ({priorityAction}).", LogTag.Actions | LogTag.Effects);
                    return;
            }
            context.GameMediator.NotifyActionsQueueChanged(); // The queue's order may have changed
            Log($"Executed {priorityAction}", LogTag.Actions | LogTag.Effects);
        }
    }
}
