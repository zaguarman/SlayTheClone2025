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
            return;
        }

        // --- Execute Logic ---
        int actualHealAmount = targetCreature.Heal(amount);

        // --- Notify Mediator ---
        if (actualHealAmount > 0) {
            // Notify AFTER the action is performed
            mediator.NotifyCreatureHealed(targetCreature, actualHealAmount);

            // Since health changed, notify that stats changed so CardController updates
            mediator.NotifyCreatureStatsChanged(targetCreature);
            Log($"HealCreatureActionExecutor: Notified CreatureStatsChanged for {targetCreature.Name} after healing.", LogTag.Actions | LogTag.Creatures | LogTag.Effects);
        }

        Log($"Executed HealCreatureAction via Executor: Healed {targetCreature.Name} for {amount} (Actual: {actualHealAmount}). Health: {targetCreature.Health}/{targetCreature.MaxHealth}", LogTag.Actions | LogTag.Creatures | LogTag.Effects);
    }
}
