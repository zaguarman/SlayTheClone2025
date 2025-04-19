using System;
using static DebugLogger;
using static Enums;

// Represents a modifier that changes a creature's stats for a limited number of turns
public class TimedStatModifier : StatModifier, ITimedModifier // Implement the new interface
{
    public int DurationInTurns { get; private set; }
    public int TurnApplied { get; private set; }

    // Constructor for timed stat modifiers
    public TimedStatModifier(
        string name,
        string description,
        ModifiableStat stat,
        ModifierCalculationType calcType,
        int value,
        int durationInTurns,
        int currentTurn)
        : base(name, description, stat, calcType, value)
    {
        // Ensure duration is at least 1 turn
        DurationInTurns = Math.Max(1, durationInTurns);
        TurnApplied = currentTurn;

        Log($"TimedStatModifier '{Name}' created: Modifies {stat} by {value} for {durationInTurns} turns (applied on turn {TurnApplied})",
            LogTag.Effects | LogTag.Turns);
    }

    // Check if the modifier has expired based on the current turn
    public bool HasExpired(int currentTurn)
    {
        // Expires *after* the turn it was applied + duration has fully passed.
        // e.g., Applied Turn 1, Duration 1: Expires after Turn 2 ends (so check on Turn 3 start/Turn 2 end)
        // e.g., Applied Turn 5, Duration 2: Expires after Turn 7 ends (so check on Turn 8 start/Turn 7 end)
        // The check happens *at the end* of the turn, so we check if the current turn number
        // is strictly greater than the turn it should last until.
        bool expired = currentTurn >= (TurnApplied + DurationInTurns);

        if (expired)
        {
            Log($"TimedStatModifier '{Name}' has expired on turn {currentTurn} (applied on turn {TurnApplied}, duration {DurationInTurns})",
                LogTag.Effects | LogTag.Turns);
        }

        return expired;
    }

    // Override ToString to include duration information
    public override string ToString() =>
        $"{base.ToString()} - Duration: {DurationInTurns} turns (applied on turn {TurnApplied})";
}
