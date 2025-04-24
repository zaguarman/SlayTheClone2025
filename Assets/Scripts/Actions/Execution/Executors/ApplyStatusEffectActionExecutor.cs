using static DebugLogger;
using static Enums;

/// <summary>
/// Executor for ApplyStatusEffectAction
/// </summary>
public class ApplyStatusEffectActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is ApplyStatusEffectAction statusAction))
        {
            LogError($"ApplyStatusEffectActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // Get data from Action
        var targetCreature = statusAction.GetTargetCreature();
        var statusType = statusAction.GetStatusType();
        var duration = statusAction.GetDuration();
        var potency = statusAction.GetPotency();

        // Validate
        if (targetCreature == null || statusType == StatusEffectType.None)
        {
            LogWarning("ApplyStatusEffectActionExecutor: Target creature null or status type is None.", LogTag.Actions | LogTag.Effects);
            return;
        }

        // Get Dependencies from Context
        var modifierManager = context.ModifierManager;
        var factory = context.ModifierFactory;
        var turnManager = context.TurnManager;

        // Need current turn number
        if (turnManager == null)
        {
            LogError($"ApplyStatusEffectActionExecutor: TurnManager is null in context. Cannot apply {statusType} to {targetCreature.Name}.", LogTag.Actions | LogTag.Effects | LogTag.Initialization);
            return;
        }
        int currentTurn = turnManager.TurnNumber;

        if (modifierManager == null || factory == null)
        {
            LogError($"ApplyStatusEffectActionExecutor: ModifierManager or Factory is null in context. Cannot apply {statusType} to {targetCreature.Name}.", LogTag.Actions | LogTag.Effects | LogTag.Initialization);
            return;
        }

        // Execute Logic
        string effectName = $"{statusType} Effect";
        string effectDescription = $"Applies {statusType} for {duration} turns";
        if (potency > 0 && (statusType == StatusEffectType.Burned || statusType == StatusEffectType.Poisoned))
        {
            effectDescription += $" (Potency: {potency})";
        }

        IModifier statusModifier = factory.CreateStatusEffectModifier(
            effectName, effectDescription, statusType, duration, potency, currentTurn
        );

        modifierManager.ApplyModifier(targetCreature, statusModifier);
        Log($"Executed ApplyStatusEffectAction via Executor: Applied {statusType} to {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
    }
}
