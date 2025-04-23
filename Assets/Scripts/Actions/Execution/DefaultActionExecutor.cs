using static DebugLogger;
using static Enums;

/// <summary>
/// Default executor for actions that handle their own logic
/// </summary>
public class DefaultActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (action == null)
        {
            LogError("DefaultActionExecutor: Cannot execute null action.", LogTag.Actions);
            return;
        }

        // Simply call the action's original Execute method
        action.Execute();
    }
}
