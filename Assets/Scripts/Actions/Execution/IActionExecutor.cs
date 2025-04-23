using System;

/// <summary>
/// Interface for executing a game action using the Strategy pattern
/// </summary>
public interface IActionExecutor
{
    /// <summary>
    /// Executes the action using the provided context
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <param name="context">The execution context providing necessary services</param>
    void Execute(IGameAction action, ActionExecutionContext context);
}
