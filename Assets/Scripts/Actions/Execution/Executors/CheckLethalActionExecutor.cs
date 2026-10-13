using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class CheckLethalActionExecutor : ActionExecutor<CheckLethalAction> {
        protected override void ExecuteAction(CheckLethalAction check, ActionExecutionContext context) {
            var creature = check.GetCreature();
            if (creature == null || creature.IsDead || creature.Health > 0) return; // Already gone, or healed in the meantime
            if (context.ModifierManager.HasStatusEffect(creature, StatusEffectType.BorrowedTime)) {
                Log($"Executor: {creature.Name} is at 0 Health, on Borrowed Time: it doesn't die yet", LogTag.Creatures | LogTag.Actions);
                return;
            }

            Log($"Executor: {creature.Name} is marked for destruction", LogTag.Creatures | LogTag.Actions);

            // Its other pending actions are cancelled; its retaliation still happens
            context.ActionsQueue.CancelActionsOf(creature);

            // OnAboutToDie resolves right away and may save it; the destruction comes after it
            creature.HandleEffect(EffectTrigger.OnAboutToDie, context.ActionsQueue, check.GetKiller());
            context.ActionsQueue.AddAction(new DestroyCreatureAction(creature, check.GetKiller()));
        }
    }
}
