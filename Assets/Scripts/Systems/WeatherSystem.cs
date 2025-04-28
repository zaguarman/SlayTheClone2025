using UnityEngine.Events;
using static DebugLogger;
using static Enums;

public interface IWeatherSystem {
    WeatherType CurrentWeather { get; }
    void SetWeather(WeatherType weatherType);
    // Removed bool isDirectDamage parameter
    float GetDamageModifier();
    UnityEvent<WeatherType> OnWeatherChanged { get; }
    string GetWeatherDescription(WeatherType weatherType);
}

public class WeatherSystem : IWeatherSystem {
    #region Fields & Properties
    private WeatherType currentWeather = WeatherType.Clear;
    private readonly IGameMediator gameMediator;
    private readonly UnityEvent<WeatherType> onWeatherChanged = new UnityEvent<WeatherType>();
    public WeatherType CurrentWeather => currentWeather;
    public UnityEvent<WeatherType> OnWeatherChanged => onWeatherChanged;
    #endregion

    #region Constructor
    public WeatherSystem(IGameMediator gameMediator) {
        this.gameMediator = gameMediator;
    }
    #endregion

    #region Methods
    public void SetWeather(WeatherType weatherType) {
        if (currentWeather != weatherType) {
            currentWeather = weatherType;
            OnWeatherChanged.Invoke(currentWeather);
            Log($"Weather changed to {currentWeather}", LogTag.Effects);
            gameMediator?.NotifyGameStateChanged();
        }
    }

    // Updated GetDamageModifier
    public float GetDamageModifier() {
        // Simplification: Rainy affects combat (MarkCombatTargetAction processing), Sunny affects all damage actions.
        // A more complex system could check action type if needed.
        return currentWeather switch {
            // Example: Maybe Rainy only affects damage queued by MarkCombatTargetAction
            // Example: Sunny could boost all DamageCreature/DamagePlayer actions
            WeatherType.Rainy => -1f, // Let's assume this affects combat damage logic elsewhere for now
            WeatherType.Sunny => 1.0f, // Assume this boosts all damage actions
            _ => 0f
        };
        // Note: The actual application of this modifier needs to be implemented
        // where damage is calculated or applied (e.g., in DamageCreatureAction.Execute or Creature.TakeDamage)
        // For now, this just returns the value.
    }

    public string GetWeatherDescription(WeatherType weather) {
        return weather switch {
            WeatherType.Clear => "Clear: Normal damage",
            WeatherType.Rainy => "Rain: Combat -1", // Description remains
            WeatherType.Sunny => "Sunny: Damage +1", // Description remains
            _ => "Unknown weather"
        };
    }
    #endregion
}