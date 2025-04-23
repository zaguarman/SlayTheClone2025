using static DebugLogger;
using static Enums;
using System;

/// <summary>
/// Executor for HealCreatureAction
/// </summary>
public class HealCreatureActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is HealCreatureAction healAction))
        {
            LogError($"HealCreatureActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // --- Get data from Action ---
        var target = healAction.GetTarget();
        var amount = healAction.GetAmount();

        // --- Validate ---
        if (target == null) {
            LogWarning("HealCreatureActionExecutor: Target creature is null.", LogTag.Actions | LogTag.Creatures);
            return;
        }

        // --- Execute Logic (moved from HealCreatureAction.Execute) ---
        // Actual heal logic should live in Creature.Heal() or similar.
        // For now, replicate the placeholder logging.
        if (target is Creature creature) {
            int currentHealth = creature.Health;
            // Assuming MaxHealth property exists or we use a placeholder
            int maxHealth = creature.MaxHealth; // Use actual max health
            int newHealth = Math.Min(currentHealth + amount, maxHealth);
            int actualHealAmount = newHealth - currentHealth; // Calculate actual amount healed

            // In a real implementation, we'd call:
            // creature.Heal(amount); // which would update currentHealth and trigger events

            // Placeholder logging:
            Log($"Executed HealCreatureAction via Executor: Healing {creature.Name} for {amount} (Actual: {actualHealAmount}). Health: {currentHealth} -> {newHealth}/{maxHealth}", LogTag.Actions | LogTag.Creatures);

            // If creature.Heal doesn't notify, notify here (but ideally it should)
            // context.GameMediator?.NotifyCreatureHealed(creature, actualHealAmount);
            // context.GameMediator?.NotifyGameStateChanged();
        }
        else {
             LogWarning($"HealCreatureActionExecutor: Target {target.Name} is not a concrete Creature.", LogTag.Actions | LogTag.Creatures);
        }
    }
}
