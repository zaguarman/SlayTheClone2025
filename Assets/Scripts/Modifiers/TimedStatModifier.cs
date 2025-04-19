using System;
using static DebugLogger;
using static Enums;

// Represents a modifier that changes a creature's stats for a limited number of turns
public class TimedStatModifier : StatModifier
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
        DurationInTurns = durationInTurns;
        TurnApplied = currentTurn;
        
        Log($"TimedStatModifier '{Name}' created: Modifies {stat} by {value} for {durationInTurns} turns (applied on turn {TurnApplied})", 
            LogTag.Effects | LogTag.Turns);
    }
    
    // Check if the modifier has expired based on the current turn
    public bool HasExpired(int currentTurn)
    {
        // Expires after the turn it was applied + duration
        // For example, if applied on turn 1 with duration 2, it expires after turn 3
        bool expired = currentTurn > (TurnApplied + DurationInTurns);
        
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
