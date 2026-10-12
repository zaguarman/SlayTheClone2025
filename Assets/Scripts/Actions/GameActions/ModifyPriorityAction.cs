using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// Gives a creature's actions, or the actions of every creature a player controls, extra (or less) Priority (Action)
    /// for a number of Days, counting the Day it resolves (GDD 3.7 Changing Priority).
    /// </summary>
    public class ModifyPriorityAction : IGameAction {
        private readonly ITarget target; // A creature, or a player for a side-wide change
        private readonly int amount;
        private readonly int days;

        public ITarget GetTarget() => target;
        public int GetAmount() => amount;
        public int GetDays() => days;

        public ModifyPriorityAction(ITarget target, int amount, int days) {
            this.target = target;
            this.amount = amount;
            this.days = days;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() {
            return $"ModifyPriorityAction: Target={target?.TargetId.ToUpper()}, Amount={amount}, Days={days}";
        }
    }
}
