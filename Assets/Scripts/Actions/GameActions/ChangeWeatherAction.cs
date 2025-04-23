using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class ChangeWeatherAction : IGameAction {
    private readonly WeatherType targetWeather;
    // Add a getter for the target weather
    public WeatherType GetTargetWeather() => targetWeather;

    public ChangeWeatherAction(WeatherType targetWeather) {
        this.targetWeather = targetWeather;
        Log($"Created ChangeWeatherAction to {targetWeather}", LogTag.Actions | LogTag.Effects);
    }

    // Execute remains simple, the logic is moved to the executor (ActionsQueue)
    public void Execute() {
        // Intentionally left blank or could add a log.
        // The actual weather change logic is now handled by the ActionsQueue.
        Log($"ChangeWeatherAction Execute() called for {targetWeather}. Logic handled by ActionsQueue.", LogTag.Actions | LogTag.Effects);
    }

    public override string ToString() {
        return $"ChangeWeatherAction: TargetWeather={targetWeather}";
    }
}