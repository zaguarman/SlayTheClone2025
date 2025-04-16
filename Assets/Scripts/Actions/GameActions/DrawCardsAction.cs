using Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class DrawCardsAction : IGameAction {
    #region Fields
    private readonly IPlayer player;
    private readonly int amount;
    #endregion

    #region Constructor
    public DrawCardsAction(IPlayer player, int amount = 1) {
        this.player = player;
        this.amount = amount;
        Log($"Created DrawCardsAction for {(player.IsPlayer1() ? "Player 1" : "Player 2")} (TargetID: {player.TargetId.ToUpper()}) to draw {amount} cards", LogTag.Actions | LogTag.Cards);
    }
    #endregion

    #region Methods
    public void Execute() {
        if (player == null) {
            LogError("Cannot execute draw cards action - player is null", LogTag.Actions | LogTag.Cards);
            return;
        }

        var cardDealingService = GameManager.Instance?.cardDealingService;
        if (cardDealingService == null) {
            LogError("Cannot execute draw cards action - card dealing service not available", LogTag.Actions | LogTag.Cards);
            return;
        }

        // Draw the specified number of cards
        cardDealingService.DrawCards(player, amount);
        Log($"Executed draw cards action for {(player.IsPlayer1() ? "Player 1" : "Player 2")} (TargetID: {player.TargetId.ToUpper()}) - drew up to {amount} cards", LogTag.Actions | LogTag.Cards);
    }

    public override string ToString() {
        return $"DrawCardsAction: Player={(player?.IsPlayer1() == true ? "1" : "2")} (TargetID: {player?.TargetId.ToUpper()}), Amount={amount}";
    }
    #endregion
} 