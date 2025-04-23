using static DebugLogger;

public class PlayCardAction : IGameAction {
    private readonly ICard card;
    private readonly IPlayer owner;
    private readonly ITarget target;

    // Getters for executor
    public ICard GetCard() => card;
    public IPlayer GetOwner() => owner;
    public ITarget GetTarget() => target;

    public PlayCardAction(ICard card, IPlayer owner, ITarget target) {
        this.card = card;
        this.owner = owner;
        this.target = target;
        Log($"Created PlayCardAction for {card?.Name} (TargetID: {card?.TargetId.ToUpper()}) targeting {target?.TargetId.ToUpper()}",
            LogTag.Actions | LogTag.Cards);
    }

    public void Execute() {
        // Logic moved to PlayCardActionExecutor
        Log($"PlayCardAction Execute() called for {card?.Name}. Logic handled by Executor.", LogTag.Actions | LogTag.Cards);
    }

    public override string ToString() {
        return $"PlayCardAction: Card={card?.Name} (TargetID: {card?.TargetId.ToUpper()}), Owner={(owner?.IsPlayer1() == true ? "Player 1" : "Player 2")} (TargetID: {owner?.TargetId.ToUpper()}), Target={target?.TargetId.ToUpper()}";
    }
}