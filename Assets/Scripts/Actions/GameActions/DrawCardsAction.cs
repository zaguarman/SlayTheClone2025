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

    public override string ToString() {
        return $"DrawCardsAction: Player={(player?.IsPlayer1 == true ? "1" : "2")} (TargetID: {player?.TargetId.ToUpper()}), Amount={amount}";
    }
    #endregion
}