using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    /// <summary>Activates a Watchdog when it resolves (GDD 3.11: Watchdogs are created through the queue).</summary>
    public class CreateWatchdogAction : IGameAction {
        public WatchdogSpec Spec { get; }
        public IPlayer Controller { get; }
        public ICreature WatchedCreature { get; } // Null: it watches a side
        public int Days { get; }                  // 0: until its charges are used
        public Watchdog Created { get; set; }     // Once it resolved: the Watchdog it set (whose condition a Reveal can show)

        public CreateWatchdogAction(WatchdogSpec spec, IPlayer controller, ICreature watchedCreature, int days) {
            Spec = spec;
            Controller = controller;
            WatchedCreature = watchedCreature;
            Days = days;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"CreateWatchdogAction: {Spec.side} {Spec.watches} -> {Spec.effect} for {Controller?.Name}" +
            (WatchedCreature != null ? $", watching {WatchedCreature.Name}" : "");
    }

    public class CreateWatchdogActionExecutor : ActionExecutor<CreateWatchdogAction> {
        protected override void ExecuteAction(CreateWatchdogAction create, ActionExecutionContext context) {
            // Watching a creature that's gone already: nothing to watch
            if (create.WatchedCreature != null && (create.WatchedCreature.IsDead || create.WatchedCreature.Slot == null)) return;
            create.Created = context.ActionsQueue.AddWatchdog(create.Spec, create.Controller, create.WatchedCreature, create.Days);
        }
    }
}
