using static DebugLogger;
using static Enums;

/// <summary>
/// Executor for ModifyArmorAction
/// </summary>
public class ModifyArmorActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is ModifyArmorAction armorAction))
        {
            LogError($"ModifyArmorActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // --- Get data from Action ---
        var targetCreature = armorAction.GetTargetCreature();
        var amount = armorAction.GetAmount();

        // --- Validate ---
        if (targetCreature == null)
        {
            LogWarning($"ModifyArmorActionExecutor: Target creature is null.", LogTag.Actions | LogTag.Effects);
            return;
        }

        // --- Execute Logic (moved from ModifyArmorAction.Execute) ---
        targetCreature.ModifyArmorPool(amount);
        Log($"Executed ModifyArmorAction via Executor: Modified armor for {targetCreature.Name} by {amount}. New Pool: {targetCreature.CurrentArmorPool}", LogTag.Actions | LogTag.Effects | LogTag.Creatures);
    }
}
