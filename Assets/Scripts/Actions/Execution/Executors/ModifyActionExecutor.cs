using static DebugLogger;
using static Enums;
using System;

/// <summary>
/// Executor for ModifyAction
/// </summary>
public class ModifyActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is ModifyAction modifyAction))
        {
            LogError($"ModifyActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // Get data from Action
        var targetCreature = modifyAction.GetTarget() as ICreature;
        var value = modifyAction.GetValue();
        var modifyAttack = modifyAction.ShouldModifyAttack();
        var modifyHealth = modifyAction.ShouldModifyHealth();
        var modifySpeed = modifyAction.ShouldModifySpeed();
        var duration = modifyAction.GetDuration();
        var calcType = modifyAction.GetCalculationType();

        // Validate
        if (targetCreature == null)
        {
            LogWarning($"ModifyActionExecutor: Target creature is null.", LogTag.Actions | LogTag.Effects);
            return;
        }

        // Get Dependencies from Context
        var modifierManager = context.ModifierManager;
        var factory = context.ModifierFactory;

        if (modifierManager == null || factory == null)
        {
            LogError($"ModifyActionExecutor: ModifierManager or Factory is null in context.", LogTag.Actions | LogTag.Effects);
            return;
        }

        // Execute Logic
        // Get current turn number for timed modifiers
        int currentTurn = GameManager.Instance?.TurnManager?.TurnNumber ?? 0;

        // Create and apply modifiers via the ModifierManager
        if (modifyAttack && value != 0)
        {
            string modName = $"Attack Modify ({calcType} {value}){(duration > 0 ? $" [{duration}t]" : "")}";
            string modDesc = $"{(calcType == ModifierCalculationType.Flat && value >= 0 ? "+" : "")}{value}{(calcType == ModifierCalculationType.Percentage ? "%" : "")} Attack{(duration > 0 ? $" ({duration} turns)" : "")}";

            IModifier mod = duration > 0
                ? factory.CreateTimedStatModifier(modName, modDesc, ModifiableStat.Attack, calcType, value, duration, currentTurn)
                : factory.CreateStatModifier(modName, modDesc, ModifiableStat.Attack, calcType, value);

            modifierManager.ApplyModifier(targetCreature, mod);
        }

        if (modifyHealth && value != 0)
        {
            string modName = $"Health Modify ({calcType} {value}){(duration > 0 ? $" [{duration}t]" : "")}";
            string modDesc = $"{(calcType == ModifierCalculationType.Flat && value >= 0 ? "+" : "")}{value}{(calcType == ModifierCalculationType.Percentage ? "%" : "")} Max Health{(duration > 0 ? $" ({duration} turns)" : "")}";

            IModifier mod = duration > 0
                ? factory.CreateTimedStatModifier(modName, modDesc, ModifiableStat.Health, calcType, value, duration, currentTurn)
                : factory.CreateStatModifier(modName, modDesc, ModifiableStat.Health, calcType, value);

            modifierManager.ApplyModifier(targetCreature, mod);
        }

        if (modifySpeed && value != 0)
        {
            string modName = $"Speed Modify ({calcType} {value}){(duration > 0 ? $" [{duration}t]" : "")}";
            string modDesc = $"{(calcType == ModifierCalculationType.Flat && value >= 0 ? "+" : "")}{value}{(calcType == ModifierCalculationType.Percentage ? "%" : "")} Speed{(duration > 0 ? $" ({duration} turns)" : "")}";

            IModifier mod = duration > 0
                ? factory.CreateTimedStatModifier(modName, modDesc, ModifiableStat.Speed, calcType, value, duration, currentTurn)
                : factory.CreateStatModifier(modName, modDesc, ModifiableStat.Speed, calcType, value);

            modifierManager.ApplyModifier(targetCreature, mod);
        }

        Log($"Executed ModifyAction via Executor for {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
    }
}
