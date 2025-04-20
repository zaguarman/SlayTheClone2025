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
        // Example: Applied Turn 1, Duration 1: Should expire end of Turn 2 (check on Turn 3 start / Turn 2 end)
        // Example: Applied Turn 5, Duration 2: Should expire end of Turn 7 (check on Turn 8 start / Turn 7 end)
        // The check happens at the end of the turn X (passed as currentTurn), preparing for turn X+1.
        // So, it expires if the *next* turn (currentTurn + 1) is >= TurnApplied + DurationInTurns.
        // Equivalently: currentTurn >= TurnApplied + DurationInTurns - 1
        // Let's adjust slightly: Expire if currentTurn has REACHED or PASSED the expiry turn.
        // Expiry Turn = TurnApplied + DurationInTurns
        // Expired if currentTurn >= ExpiryTurn
        bool expired = currentTurn >= (TurnApplied + DurationInTurns); // Keep original logic for now, let's confirm intent

        // --- Alternative Logic (Lasts until END of NEXT turn for duration 1) ---
        // This makes a 1-turn buff applied mid-turn last longer.
        // Expired if currentTurn > TurnApplied + DurationInTurns -1
        // bool expired = currentTurn > (TurnApplied + DurationInTurns - 1);
        // Let's stick to the original: Expire AFTER the full duration has passed relative to the application turn.
        // A 1-turn duration applied T13 expires when checking for T14.

        if (expired)
        {
            Log($"TimedStatModifier '{Name}' has expired on turn {currentTurn} (applied on turn {TurnApplied}, duration {DurationInTurns})",
                LogTag.Effects | LogTag.Turns);
        }

        return expired; // Keep original logic, assuming generator fixes data mismatch
    }

    // Override ToString to include duration information
    public override string ToString() =>
        $"{base.ToString()} - Duration: {DurationInTurns} turns (applied on turn {TurnApplied})";
}
