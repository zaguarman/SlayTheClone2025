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

            // Borrowed Time (Die Another Day): it can't die while it lasts, not even outright
            if (context.ModifierManager.HasStatusEffect(creature, StatusEffectType.BorrowedTime)) {
                Log($"Executor: {creature.Name} is on Borrowed Time: it doesn't die today", LogTag.Creatures | LogTag.Actions);
                return;
            }

            if (creature.Health > 0 && !destroy.IsUnconditional) {
                Log($"Executor: {creature.Name} was saved from destruction ({creature.Health} Health)", LogTag.Creatures | LogTag.Actions);
                return;
            }

            // Blessed: an outright destroy (a spell, an ability, a licence to kill) spends one stack instead. A creature with no
            // Health left still dies, and a Sacrifice is an offering, not an attack
            if (destroy.IsUnconditional && !destroy.IsSacrifice && creature.Health > 0 && StatusRules.SpendBlessing(context, creature)) {
                Log($"Executor: {creature.Name}'s Blessing saves it from being destroyed", LogTag.Creatures | LogTag.Actions);
                return;
            }

            Log($"Executor: {creature.Name} is destroyed", LogTag.Creatures | LogTag.Combat | LogTag.Actions);
            creature.MarkAsDead();
            if (creature.Owner != null) creature.Owner.Fallen++; // Grudge counts the fallen of its side
            context.GameMediator.NotifyCreatureDied(creature);

            // OnDied effects wait their turn in the Ability and Attack layer
            creature.HandleEffect(EffectTrigger.OnDeath, context.ActionsQueue, destroy.GetKiller());

            context.ModifierManager.UnregisterCreature(creature);
            creature.Owner?.DiscardCard(creature);
        }
    }
}
