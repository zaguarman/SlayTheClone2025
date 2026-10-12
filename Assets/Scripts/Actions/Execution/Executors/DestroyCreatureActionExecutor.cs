using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class DestroyCreatureActionExecutor : ActionExecutor<DestroyCreatureAction> {
        protected override void ExecuteAction(DestroyCreatureAction destroy, ActionExecutionContext context) {
            var creature = destroy.GetCreature();
            if (creature == null || creature.IsDead) return;

            if (creature.Health > 0) {
                Log($"Executor: {creature.Name} was saved from destruction ({creature.Health} Health)", LogTag.Creatures | LogTag.Actions);
                return;
            }

            Log($"Executor: {creature.Name} is destroyed", LogTag.Creatures | LogTag.Combat | LogTag.Actions);
            creature.MarkAsDead();
            context.GameMediator.NotifyCreatureDied(creature);

            // OnDied effects wait their turn in the Ability and Attack layer
            creature.HandleEffect(EffectTrigger.OnDeath, context.ActionsQueue, destroy.GetKiller());

            context.ModifierManager.UnregisterCreature(creature);
            creature.Owner?.DiscardCard(creature);
        }
    }
}
