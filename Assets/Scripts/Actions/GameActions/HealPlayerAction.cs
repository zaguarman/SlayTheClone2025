using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class HealPlayerAction : IGameAction {
    #region Fields
    private readonly IPlayer target;
    private readonly int amount;
    #endregion

    #region Constructor
    public HealPlayerAction(IPlayer target, int amount) {
        this.target = target;
        this.amount = amount;
        Log($"Created HealPlayerAction for {(target?.IsPlayer1() == true ? "Player 1" : "Player 2")} (TargetID: {target?.TargetId.ToUpper()}) with amount {amount}",
            LogTag.Actions | LogTag.Players);
    }
    #endregion

    #region Methods
    public void Execute() {
        Log($"HealPlayerAction Execute() called for {(target?.IsPlayer1() == true ? "Player 1" : "Player 2")}. Logic handled by Executor.", LogTag.Actions | LogTag.Players);
    }

    // Getters for executor
    public IPlayer GetTargetPlayer() => target;
    public int GetAmount() => amount;

    public override string ToString() {
        return $"HealPlayerAction: Target={(target?.IsPlayer1() == true ? "Player 1" : "Player 2")} (TargetID: {target?.TargetId.ToUpper()}), Amount={amount}";
    }
    #endregion
}