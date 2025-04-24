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
        var targetCreature = healAction.GetTarget();
        var amount = healAction.GetAmount();

        // --- Get Dependencies from Context ---
        var mediator = context.GameMediator;
        if (mediator == null)
        {
            LogError("HealCreatureActionExecutor: GameMediator is null in context.", LogTag.Actions | LogTag.Initialization);
            return;
        }

        // --- Validate Target ---
        if (targetCreature == null || targetCreature.IsDead) {
            // LogWarning($"HealCreatureActionExecutor: Target creature {(targetCreature?.Name ?? "null")} is null or dead. Skipping heal.", LogTag.Actions | LogTag.Creatures);
            return;
        }

        // --- Execute Logic ---
        int actualHealAmount = targetCreature.Heal(amount);

        // --- Notify Mediator ---
        if (actualHealAmount > 0) {
            // Notify AFTER the action is performed
            mediator.NotifyCreatureHealed(targetCreature, actualHealAmount);
            // Optionally notify general game state change if needed, but CreatureHealed might be sufficient
            // mediator.NotifyGameStateChanged();
        }

        Log($"Executed HealCreatureAction via Executor: Healed {targetCreature.Name} for {amount} (Actual: {actualHealAmount}). Health: {targetCreature.Health}/{targetCreature.MaxHealth}", LogTag.Actions | LogTag.Creatures | LogTag.Effects);
    }
}
