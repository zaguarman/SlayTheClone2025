using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>Puts a World in play for some Days (0: until another replaces it), replacing the one in play.</summary>
    public class SetWorldAction : IGameAction {
        public World World { get; }
        public int Days { get; }
        public IPlayer Controller { get; }

        public SetWorldAction(World world, int days, IPlayer controller = null) {
            World = world;
            Days = days;
            Controller = controller;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() => $"SetWorldAction: {World} for {Days} Days";
    }

    public class SetWorldActionExecutor : ActionExecutor<SetWorldAction> {
        protected override void ExecuteAction(SetWorldAction action, ActionExecutionContext context) =>
            context.WorldSystem.SetWorld(action.World, action.Days, context.TurnClock.TurnNumber);
    }
}
