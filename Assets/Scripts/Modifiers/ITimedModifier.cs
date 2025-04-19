// Interface for modifiers that have a duration and can expire
public interface ITimedModifier : IModifier
{
    int DurationInTurns { get; }
    int TurnApplied { get; }
    bool HasExpired(int currentTurn);
}
