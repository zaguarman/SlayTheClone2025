using UnityEngine.Events;
using static DebugLogger;

public enum WeatherType {
    Clear,
    Rainy,
    Sunny,
    Cloudy
}

public interface IWeatherSystem {
    WeatherType CurrentWeather { get; }
    void SetWeather(WeatherType weatherType);
    float GetDamageModifier(bool isDirectDamage);
    UnityEvent<WeatherType> OnWeatherChanged { get; }
}

public class WeatherSystem : IWeatherSystem {
    private WeatherType currentWeather = WeatherType.Clear;
    private readonly GameMediator gameMediator;
    private readonly UnityEvent<WeatherType> onWeatherChanged = new UnityEvent<WeatherType>();

    public WeatherType CurrentWeather => currentWeather;
    public UnityEvent<WeatherType> OnWeatherChanged => onWeatherChanged;

    public WeatherSystem(GameMediator gameMediator) {
        this.gameMediator = gameMediator;
    }

    public void SetWeather(WeatherType weatherType) {
        if (currentWeather != weatherType) {
            currentWeather = weatherType;
            OnWeatherChanged.Invoke(currentWeather);
            Log($"Weather changed to {currentWeather}", LogTag.Effects);
            gameMediator?.NotifyGameStateChanged();
        }
    }

    public float GetDamageModifier(bool isDirectDamage) {
        return currentWeather switch {
            WeatherType.Rainy when !isDirectDamage => -1f,    // Combat damage reduced by 1
            WeatherType.Sunny when isDirectDamage => 1.0f,    // Direct damage increased by 1
            WeatherType.Cloudy => 1.0f,                      // All damage reduced by 0.5
            _ => 0f                                           // No modifier
        };
    }

    public static string GetWeatherDescription(WeatherType weather) {
        return weather switch {
            WeatherType.Clear => "Clear: Normal damage",
            WeatherType.Rainy => "Rain: Combat -1",
            WeatherType.Sunny => "Sunny: Direct +1",
            WeatherType.Cloudy => "Cloudy: All damage -1",
            _ => "Unknown weather"
        };
    }
}