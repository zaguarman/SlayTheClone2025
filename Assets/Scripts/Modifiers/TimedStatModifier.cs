using System;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    // Represents a modifier that changes a creature's stats for a limited number of turns
    public class TimedStatModifier : StatModifier, ITimedModifier {
        public int DurationInTurns { get; private set; }
        public int TurnApplied { get; private set; }

        public TimedStatModifier(
            string name,
            string description,
            ModifiableStat stat,
            ModifierCalculationType calcType,
            int value,
            int durationInTurns,
            int currentTurn)
            : base(name, description, stat, calcType, value) {
            DurationInTurns = Math.Max(1, durationInTurns);
            TurnApplied = currentTurn;

            Log($"TimedStatModifier '{Name}' created: Modifies {stat} by {value} for {durationInTurns} turns (applied on turn {TurnApplied})",
                LogTag.Effects | LogTag.Turns);
        }

        public bool HasExpired(int currentTurn) {
            bool expired = TimedModifierRules.HasExpired(this, currentTurn);
            if (expired) {
                Log($"TimedStatModifier '{Name}' has expired on turn {currentTurn} (applied on turn {TurnApplied}, duration {DurationInTurns})",
                    LogTag.Effects | LogTag.Turns);
            }
            return expired;
        }

        public override string ToString() =>
            $"{base.ToString()} - Duration: {DurationInTurns} turns (applied on turn {TurnApplied})";
    }
}
