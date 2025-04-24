using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class DiscardHandAction : IGameAction {
    #region Fields
    private readonly IPlayer player;
    #endregion

    #region Constructor
    public DiscardHandAction(IPlayer player) {
        this.player = player;
        Log($"Created DiscardHandAction for {(player.IsPlayer1 ? "Player 1" : "Player 2")} (PlayerID: {player.TargetId.ToUpper()}) with {player.Hand.Count} cards",
            LogTag.Actions | LogTag.Cards);
    }
    #endregion

    #region Methods
    public void Execute() {
        Log($"DiscardHandAction Execute() called for {(player?.IsPlayer1 == true ? "Player 1" : "Player 2")}. Logic handled by Executor.", LogTag.Actions | LogTag.Cards);
    }

    // Getter for executor
    public IPlayer GetPlayer() => player;

    public override string ToString() {
        return $"DiscardHandAction: Player={(player?.IsPlayer1 == true ? "1" : "2")} (PlayerID: {player?.TargetId.ToUpper() ?? "UNKNOWN"}) with {player?.Hand.Count ?? 0} cards";
    }
    #endregion
}