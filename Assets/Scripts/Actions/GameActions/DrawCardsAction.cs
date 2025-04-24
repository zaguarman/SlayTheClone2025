using static DebugLogger;

public class DrawCardsAction : IGameAction {
    #region Fields
    private readonly IPlayer player;
    private readonly int amount;

    // Add getters for ActionsQueue special handling
    public IPlayer GetPlayer() => player;
    public int GetAmount() => amount;
    #endregion

    #region Constructor
    public DrawCardsAction(IPlayer player, int amount = 1) {
        this.player = player;
        this.amount = amount;
        Log($"Created DrawCardsAction for {(player.IsPlayer1 ? "Player 1" : "Player 2")} (TargetID: {player.TargetId.ToUpper()}) to draw {amount} cards", LogTag.Actions | LogTag.Cards);
    }
    #endregion

    #region Methods
    public void Execute() {
        if (player == null) {
            LogError("Cannot execute draw cards action - player is null", LogTag.Actions | LogTag.Cards);
            return;
        }

        // This Execute method might still be called if the action is somehow
        // executed outside the ActionsQueue's ResolveActions loop, or for testing.
        // The primary execution logic is now handled *by* the ActionsQueue.

        // Log the intent, but don't perform the actual operation
        // This avoids the direct dependency on GameManager.Instance
        Log($"DrawCardsAction: Intent to draw {amount} cards for {(player.IsPlayer1 ? "Player 1" : "Player 2")}. (Actual execution handled by ActionsQueue)", LogTag.Actions | LogTag.Cards);
    }

    public override string ToString() {
        return $"DrawCardsAction: Player={(player?.IsPlayer1 == true ? "1" : "2")} (TargetID: {player?.TargetId.ToUpper()}), Amount={amount}";
    }
    #endregion
}