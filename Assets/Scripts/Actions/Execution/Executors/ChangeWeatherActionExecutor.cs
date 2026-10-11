using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class ChangeWeatherActionExecutor : ActionExecutor<ChangeWeatherAction> {
        protected override void ExecuteAction(ChangeWeatherAction weatherAction, ActionExecutionContext context) {
            var weatherSystem = context.WeatherSystem;
            if (weatherSystem == null) {
                LogError("ChangeWeatherActionExecutor: WeatherSystem is null in context", LogTag.Actions | LogTag.Effects);
                return;
            }

            WeatherType targetWeather = weatherAction.GetTargetWeather();
            weatherSystem.SetWeather(targetWeather);
            Log($"Executed ChangeWeatherAction via Executor: Weather set to {targetWeather}", LogTag.Actions | LogTag.Effects);
        }
    }
}
