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



    public override string ToString() {
        return $"ChangeWeatherAction: TargetWeather={targetWeather}";
    }
}