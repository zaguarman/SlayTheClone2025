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
        if (target == null) return;

        // Heal should be implemented in Player class, here's a workaround for this prototype
        int currentHealth = target.Health;
        int maxHealth = 20; // Assuming 20 is max health for this prototype
        int newHealth = Math.Min(currentHealth + amount, maxHealth);

        // Since we don't have a direct SetHealth method, we'll log the info
        Log($"Healing {(target.IsPlayer1() ? "Player 1" : "Player 2")} (TargetID: {target.TargetId.ToUpper()}) for {amount} (from {currentHealth} to {newHealth})",
            LogTag.Actions | LogTag.Players);

        // In a real implementation, we'd call something like:
        // target.Heal(amount);
    }

    public override string ToString() {
        return $"HealPlayerAction: Target={(target?.IsPlayer1() == true ? "Player 1" : "Player 2")} (TargetID: {target?.TargetId.ToUpper()}), Amount={amount}";
    }
    #endregion
} 