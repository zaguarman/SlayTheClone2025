using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// Red Tape (GDD 3.2): adds Days to the Deployment Time of a creature still deploying: it waits that much longer. A
    /// creature already deployed isn't sent back (the user, 2026-10-15): nothing happens to it.
    /// </summary>
    public class DelayDeploymentAction : IGameAction {
        public ICreature Creature { get; }
        public int Days { get; }

        public DelayDeploymentAction(ICreature creature, int days) {
            Creature = creature;
            Days = days;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"DelayDeploymentAction: {Creature?.Name} +{Days} Day(s)";
    }

    public class DelayDeploymentActionExecutor : ActionExecutor<DelayDeploymentAction> {
        protected override void ExecuteAction(DelayDeploymentAction delay, ActionExecutionContext context) {
            var creature = delay.Creature;
            if (creature == null || creature.IsDead || creature.Slot == null || delay.Days <= 0) return;
            if (creature.IsOperative) {
                Log($"{creature.Name} is already deployed: Red Tape can't send it back", LogTag.Actions | LogTag.Effects);
                return;
            }

            creature.AddDeploymentTime(delay.Days, context.TurnClock.TurnNumber);
            context.ActionsQueue.CancelActionsOf(creature); // It can't act until it's ready again (GDD 3.8)
            context.GameMediator.NotifyCreatureStatsChanged(creature);
            Log($"Executed {delay}: {creature.Name} is ready in {creature.TurnsUntilOperative} Day(s)", LogTag.Actions | LogTag.Effects);
        }
    }
}
