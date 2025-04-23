using static DebugLogger;
using static Enums;

/// <summary>
/// Executor for ChangeWeatherAction
/// </summary>
public class ChangeWeatherActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is ChangeWeatherAction weatherAction))
        {
            LogError($"ChangeWeatherActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // Get dependencies from context
        var weatherSystem = context.WeatherSystem;
        if (weatherSystem == null)
        {
            LogError("ChangeWeatherActionExecutor: WeatherSystem is null in context", LogTag.Actions | LogTag.Effects);
            return;
        }

        // Execute logic
        WeatherType targetWeather = weatherAction.GetTargetWeather();
        weatherSystem.SetWeather(targetWeather);
        Log($"Executed ChangeWeatherAction via Executor: Weather set to {targetWeather}", LogTag.Actions | LogTag.Effects);
    }
}
