using static DebugLogger;
using static Enums;
using System;

/// <summary>
/// Executor for HealPlayerAction
/// </summary>
public class HealPlayerActionExecutor : IActionExecutor {
    public void Execute(IGameAction action, ActionExecutionContext context) {
        if (!(action is HealPlayerAction healPlayerAction)) {
             LogError($"HealPlayerActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // --- Get data from Action ---
        var targetPlayer = healPlayerAction.GetTargetPlayer();
        var amount = healPlayerAction.GetAmount();

        // --- Validate ---
        if (targetPlayer == null) {
             LogError("HealPlayerActionExecutor: Target player is null", LogTag.Actions | LogTag.Players);
            return;
        }
        if (amount <= 0) {
             LogWarning($"HealPlayerActionExecutor: Heal amount ({amount}) is zero or negative. Skipping.", LogTag.Actions | LogTag.Players);
             return;
        }

        // --- Get Dependencies from Context ---
        var mediator = context.GameMediator;
        if (mediator == null) {
            LogError("HealPlayerActionExecutor: GameMediator is null in context.", LogTag.Actions | LogTag.Initialization);
            return; // Cannot proceed if mediator is somehow null
        }

        // --- Execute Logic ---
        Log($"Executor: Intent to heal {(targetPlayer.IsPlayer1 ? "Player 1" : "Player 2")} for {amount}", LogTag.Actions | LogTag.Players | LogTag.Effects);

        int actualHealAmount = targetPlayer.Heal(amount); // Player.Heal handles health increase and UI update

        // --- Notify Mediator ---
        if (actualHealAmount > 0) {
            // Notify AFTER the action is performed
            mediator.NotifyPlayerHealed(targetPlayer, actualHealAmount);
            // Optionally notify general game state change if needed, but PlayerHealed might be sufficient
            // mediator.NotifyGameStateChanged();
        }

        Log($"Executed HealPlayerAction via Executor for {(targetPlayer.IsPlayer1 ? "P1" : "P2")}. Healed for {amount} (Actual: {actualHealAmount}). Health: {targetPlayer.Health}", LogTag.Actions | LogTag.Players | LogTag.Effects);
    }
}
