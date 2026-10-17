namespace Zeitgeist {
    // Interface for modifiers that have a duration and can expire
    public interface ITimedModifier : IModifier {
        int DurationInTurns { get; }
        int TurnApplied { get; }
        bool HasExpired(int currentTurn);
    }

    public static class TimedModifierRules {
        /// <summary>
        /// A timed modifier expires once its whole duration has passed since the turn it was applied:
        /// applied on turn 13 for 1 turn, it has expired when turn 14 is checked.
        /// </summary>
        public static bool HasExpired(ITimedModifier modifier, int currentTurn) {
            return currentTurn >= modifier.TurnApplied + modifier.DurationInTurns;
        }
    }
}
