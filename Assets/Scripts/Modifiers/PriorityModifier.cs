using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// A Priority Boost or Penalty (GDD 3.7 Changing Priority): every action of the creature it's on (or, side-wide, of every
    /// creature its player controls) gets this much Priority (Action) while it lasts. It isn't a status, so it works from
    /// the moment it's applied: for 1 Day it lasts until that Day's Dusk.
    /// </summary>
    public class PriorityModifier : BaseModifier, ITimedModifier {
        public int Value { get; }
        public int DurationInTurns { get; }
        public int TurnApplied { get; }

        public PriorityModifier(int value, int days, int currentTurn)
            : base($"Priority {(value >= 0 ? "+" : "")}{value}", $"{(value >= 0 ? "+" : "")}{value} Priority for {System.Math.Max(1, days)} Day(s)") {
            Value = value;
            DurationInTurns = System.Math.Max(1, days);
            TurnApplied = currentTurn;
            Log($"PriorityModifier created: {Value} for {DurationInTurns} Day(s) from Day {TurnApplied}", LogTag.Effects);
        }

        // Checked with the Day about to start: lasting 1 Day, it ends at the Dusk of the Day it was applied
        public bool HasExpired(int currentTurn) => TimedModifierRules.HasExpired(this, currentTurn);

        public override void Apply(ICreature target, IGameMediator mediator) { }
        public override void Remove(ICreature target, IGameMediator mediator) { }
    }
}
