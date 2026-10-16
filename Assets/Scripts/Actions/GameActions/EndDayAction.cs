using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// Amen (the user, 2026-10-16): the Day ends here. Every other action still in the queue is discarded, both sides': the
    /// cards played for them go to their owners' discard piles, and the plans are gone. Dusk comes as on any Day.
    /// </summary>
    public class EndDayAction : IGameAction {
        public IPlayer Controller { get; }
        public ICard Source { get; }

        public EndDayAction(IPlayer controller, ICard source) {
            Controller = controller;
            Source = source;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"EndDayAction: {Source?.Name} ends the Day for {Controller?.Name}";
    }

    public class EndDayActionExecutor : ActionExecutor<EndDayAction> {
        protected override void ExecuteAction(EndDayAction amen, ActionExecutionContext context) {
            int discarded = context.ActionsQueue.DiscardPending();
            Log($"{amen.Source?.Name}: the Day ends, {discarded} action(s) discarded", LogTag.Actions | LogTag.Effects);
        }
    }
}
