using Enums;
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
        Log($"Created DiscardHandAction for {(player.IsPlayer1() ? "Player 1" : "Player 2")} (PlayerID: {player.TargetId.ToUpper()}) with {player.Hand.Count} cards",
            LogTag.Actions | LogTag.Cards);
    }
    #endregion

    #region Methods
    public void Execute() {
        if (player == null) {
            LogError($"Cannot execute discard hand action - player is null (ActionID: {GetHashCode().ToString().ToUpper()})",
                LogTag.Actions | LogTag.Cards);
            return;
        }

        int initialHandCount = player.Hand.Count;

        // Discard all cards in the player's hand
        player.DiscardHand();

        Log($"Executed discard hand action for {(player.IsPlayer1() ? "Player 1" : "Player 2")} (PlayerID: {player.TargetId.ToUpper()}) - discarded {initialHandCount} cards",
            LogTag.Actions | LogTag.Cards);
    }

    public override string ToString() {
        return $"DiscardHandAction: Player={(player?.IsPlayer1() == true ? "1" : "2")} (PlayerID: {player?.TargetId.ToUpper() ?? "UNKNOWN"}) with {player?.Hand.Count ?? 0} cards";
    }
    #endregion
} 