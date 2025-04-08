using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class ChangeWeatherAction : IGameAction {
    private readonly WeatherType targetWeather;

    public ChangeWeatherAction(WeatherType targetWeather) {
        this.targetWeather = targetWeather;
        Log($"Created ChangeWeatherAction to {targetWeather}", LogTag.Actions | LogTag.Effects);
    }

    public void Execute() {
        var weatherSystem = GameManager.Instance?.WeatherSystem;
        if (weatherSystem == null) {
            LogError("Cannot execute change weather action - weather system is null", LogTag.Actions);
            return;
        }

        weatherSystem.SetWeather(targetWeather);
        Log($"Changed weather to {targetWeather}", LogTag.Actions | LogTag.Effects);
    }

    public override string ToString() {
        return $"ChangeWeatherAction: TargetWeather={targetWeather}";
    }
} 