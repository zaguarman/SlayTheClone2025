using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// Borrowed Time ends (Die Another Day, the user 2026-10-16): at the Dusk of the Day it landed, once the Dusk's other
    /// actions are done. A creature at 0 Health is brought back to 1 and lives on; the status goes.
    /// </summary>
    public class BorrowedTimeEndsAction : IGameAction {
        public ICreature Creature { get; }
        public IModifier Status { get; }

        public BorrowedTimeEndsAction(ICreature creature, IModifier status) {
            Creature = creature;
            Status = status;
        }

        public override string ToString() => $"BorrowedTimeEndsAction: {Creature?.Name}";
    }

    public class BorrowedTimeEndsActionExecutor : ActionExecutor<BorrowedTimeEndsAction> {
        protected override void ExecuteAction(BorrowedTimeEndsAction ends, ActionExecutionContext context) {
            var creature = ends.Creature;
            if (creature == null) return;
            if (!creature.IsDead && creature.Slot != null && creature.Health <= 0) {
                creature.Heal(1 - creature.Health);
                Log($"{creature.Name}'s Borrowed Time ends at 0 Health: it's back to 1", LogTag.Effects | LogTag.Creatures);
            }
            context.ModifierManager.RemoveModifier(creature, ends.Status);
            context.GameMediator.NotifyCreatureStatsChanged(creature);
        }
    }
}
