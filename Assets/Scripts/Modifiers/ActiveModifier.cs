using Enums;

public class ActiveModifier {
    public ModifierData Data { get; private set; }
    public IModifierSource Source { get; internal set; } // Allow internal update on refresh/stack
    public IModifiable Target { get; private set; }
    public int RemainingDuration { get; internal set; } // Allow internal update on refresh/stack
    public int CurrentStacks { get; internal set; } // Allow internal update on stack

    public bool IsExpired => Data.durationType == ModifierDurationType.TurnBased && RemainingDuration <= 0;

    public ActiveModifier(ModifierData data, IModifiable target, IModifierSource source = null) {
        Data = data;
        Target = target;
        Source = source;
        RemainingDuration = data.durationType == ModifierDurationType.TurnBased ? data.baseDuration : int.MaxValue;
        CurrentStacks = 1;
    }

    // Returns true if duration was ticked down (meaning it's turn-based)
    public bool TickTurn() {
        if (Data.durationType == ModifierDurationType.TurnBased) {
            RemainingDuration--;
            // Prevent duration from going below 0 visually/logically
            if (RemainingDuration < 0) RemainingDuration = 0;
            return true;
        }
        return false;
    }

    public override string ToString() {
         string durationStr = Data.durationType == ModifierDurationType.TurnBased ? $" ({RemainingDuration} turns left)" : "";
         string stacksStr = Data.stackingType == ModifierStackingType.Stackable && Data.maxStacks > 1 ? $" [x{CurrentStacks}]" : "";
         return $"{Data.displayName}{stacksStr}{durationStr}";
    }
}
