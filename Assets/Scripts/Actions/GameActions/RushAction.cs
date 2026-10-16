using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// Overtime (the user, 2026-10-16): a creature still deploying is rushed in, its Deployment Time cut by up to this many
    /// Days, and pays for it with Burnout: it's destroyed at the Dusk of the first Day it's ready, so it gets that one Day to
    /// act. A creature already deployed isn't touched.
    /// </summary>
    public class RushAction : IGameAction {
        public ICreature Creature { get; }
        public int Days { get; }

        public RushAction(ICreature creature, int days) {
            Creature = creature;
            Days = days;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"RushAction: {Creature?.Name} -{Days} Day(s), then Burnout";
    }

    public class RushActionExecutor : ActionExecutor<RushAction> {
        protected override void ExecuteAction(RushAction rush, ActionExecutionContext context) {
            var creature = rush.Creature;
            if (creature == null || creature.IsDead || creature.Slot == null || rush.Days <= 0) return;
            if (creature.IsOperative) {
                Log($"{creature.Name} is already deployed: Overtime has nothing to rush", LogTag.Actions | LogTag.Effects);
                return;
            }

            int today = context.TurnClock.TurnNumber;
            creature.ReduceDeploymentTime(rush.Days, context.ActionsQueue);
            // Burnout lasts through the first Day it's ready, ending at that Day's Dusk (Status Timing: applied today for N
            // Days, its last Dusk is Day today + N)
            int lastDay = creature.FirstDayReady(today);
            StatusRules.Land(context, creature, StatusEffectType.Burnout, System.Math.Max(1, lastDay - today), 0, today);
            context.GameMediator.NotifyCreatureStatsChanged(creature);
            if (creature.IsOperative) Abilities.TryCopypasta(creature, context.GameMediator); // When ready
            Log($"Executed {rush}: {creature.Name} is ready {(creature.IsOperative ? "now" : $"in {creature.TurnsUntilOperative} Day(s)")}, burns out at the Dusk of Day {lastDay}",
                LogTag.Actions | LogTag.Effects);
        }
    }
}
