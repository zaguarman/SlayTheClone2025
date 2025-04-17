using Enums; // Assuming StatAdjustment uses Enums namespace
using static DebugLogger; // If logging is needed

public class ActiveModifier {
    // Now holds ModifierDefinition instead of ModifierData
    public ModifierDefinition Data { get; private set; }
    public IModifierSource Source { get; internal set; }
    public IModifiable Target { get; private set; }
    public int RemainingDuration { get; internal set; }
    public int CurrentStacks { get; internal set; }

    // Use helper property from ModifierDefinition
    public bool IsExpired => Data.IsTurnBased && RemainingDuration <= 0;

    // Constructor now takes ModifierDefinition
    public ActiveModifier(ModifierDefinition definition, IModifiable target, IModifierSource source = null) {
        Data = definition;
        Target = target;
        Source = source;
        // Initialize duration based on definition rules
        RemainingDuration = Data.IsTurnBased ? Data.duration : int.MaxValue; // Use MaxValue for permanent internally
        CurrentStacks = 1; // Always start with 1 stack
    }

    // Tick duration if it's turn-based
    public bool TickTurn() {
        if (Data.IsTurnBased) {
            RemainingDuration--;
            // Prevent duration from going below 0 visually/logically
            if (RemainingDuration < 0) RemainingDuration = 0;
            return true;
        }
        return false; // Not turn-based, duration didn't change
    }

    public override string ToString() {
        string durationStr = Data.IsTurnBased ? $" ({RemainingDuration} turns left)" : " (Permanent)";
        // Adjust stack display based on rules
        string stacksStr = "";
        if (Data.maxStacks > 1 || Data.HasInfiniteStacks) // Show stacks if limit > 1 or infinite
        {
             stacksStr = $" [x{CurrentStacks}]";
             if (Data.maxStacks > 1) // Add max stacks info only if there's a limit > 1
             {
                 stacksStr += $"/{Data.maxStacks}";
             }
        }
        // Don't show stacks for NonStackable (0) or RefreshOnly (1)

        return $"{Data.displayName}{stacksStr}{durationStr}";
    }
}
