using System.Linq;
using static Zeitgeist.DebugLogger;
using static Zeitgeist.Enums;

namespace Zeitgeist {
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
        protected override void ExecuteAction(SetTrendAction action, ActionExecutionContext context) {
            var trends = context.TrendSystem;
            bool wasLiminal = trends.Current == Trend.LiminalSpace;
            trends.SetTrend(action.Trend, action.Days, context.TurnClock.TurnNumber, action.Controller);
            // Liminal Space starting, or replaced (the user, 2026-10-17): everyone's Attack and Health trade places, or back
            if (wasLiminal != (trends.Current == Trend.LiminalSpace) && action.Controller?.Opponent != null) {
                var everyone = action.Controller.Battlefield.Concat(action.Controller.Opponent.Battlefield)
                    .Select(s => s.OccupyingCreature).Where(c => c != null && !c.IsDead).ToList();
                foreach (var creature in everyone) context.ActionsQueue.AddAction(new SwapStatsAction(creature));
            }
            trends.QueueDayEffects(context.ActionsQueue); // Its first Day is this one
        }
    }
}
