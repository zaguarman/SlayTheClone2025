using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>Makes a Trend the one trending for some Days (0: until another replaces it), replacing the current one.</summary>
    public class SetTrendAction : IGameAction {
        public Trend Trend { get; }
        public int Days { get; }
        public IPlayer Controller { get; }

        public SetTrendAction(Trend trend, int days, IPlayer controller = null) {
            Trend = trend;
            Days = days;
            Controller = controller;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"SetTrendAction: {Trend} for {Days} Days";
    }

    public class SetTrendActionExecutor : ActionExecutor<SetTrendAction> {
        protected override void ExecuteAction(SetTrendAction action, ActionExecutionContext context) =>
            context.TrendSystem.SetTrend(action.Trend, action.Days, context.TurnClock.TurnNumber, action.Controller);
    }
}
