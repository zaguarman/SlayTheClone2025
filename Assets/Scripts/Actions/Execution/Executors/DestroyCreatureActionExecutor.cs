using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class DestroyCreatureActionExecutor : ActionExecutor<DestroyCreatureAction> {
        protected override void ExecuteAction(DestroyCreatureAction destroy, ActionExecutionContext context) {
            var creature = destroy.GetCreature();
            if (creature == null || creature.IsDead) return;
            // Fall Guy: an enemy's destroy meant for one of its side lands on it instead
            if (destroy.IsUnconditional && destroy.Controller != null && destroy.Controller != creature.Owner && creature.Slot != null
                && Abilities.FallGuyFor(creature) is ICreature patsy) {
                Log($"Executor: {patsy.Name} takes the fall for {creature.Name}", LogTag.Creatures | LogTag.Actions);
                creature = patsy;
            }
            if (creature.Slot == null) {
                // It left play before this resolved (replaced by a summon, say): it was discarded then, and can't die now
                Log($"Executor: {creature.Name} isn't in play any more: nothing to destroy", LogTag.Creatures | LogTag.Actions);
                return;
            }

            if (creature.Health > 0 && !destroy.IsUnconditional) {
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
