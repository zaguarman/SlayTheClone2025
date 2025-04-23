using static DebugLogger;
using static Enums;
using System;

/// <summary>
/// Executor for HealPlayerAction
/// </summary>
public class HealPlayerActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is HealPlayerAction healAction))
        {
            LogError($"HealPlayerActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // --- Get data from Action ---
        var target = healAction.GetTargetPlayer();
        var amount = healAction.GetAmount();

        // --- Validate ---
        if (target == null) {
             LogWarning("HealPlayerActionExecutor: Target player is null.", LogTag.Actions | LogTag.Players);
            return;
        }

        // --- Execute Logic (moved from HealPlayerAction.Execute) ---
        // Actual heal logic should live in Player.Heal() or similar.
        // For now, replicate the placeholder logging.
        int currentHealth = target.Health;
        int maxHealth = 20; // Placeholder max health
        int newHealth = Math.Min(currentHealth + amount, maxHealth);
        int actualHealAmount = newHealth - currentHealth;

        // In a real implementation, we'd call:
        // target.Heal(amount); // which would update currentHealth and trigger events

        // Placeholder logging:
        Log($"Executed HealPlayerAction via Executor: Healing {(target.IsPlayer1() ? "P1" : "P2")} for {amount} (Actual: {actualHealAmount}). Health: {currentHealth} -> {newHealth}/{maxHealth}", LogTag.Actions | LogTag.Players);

        // If player.Heal doesn't notify, notify here
        // context.GameMediator?.NotifyPlayerHealed(target, actualHealAmount);
        // context.GameMediator?.NotifyGameStateChanged();
    }
}
