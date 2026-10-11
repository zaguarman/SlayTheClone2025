using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class ChangeWeatherAction : IGameAction {
        private readonly WeatherType targetWeather;

        public WeatherType GetTargetWeather() => targetWeather;

        public ChangeWeatherAction(WeatherType targetWeather) {
            this.targetWeather = targetWeather;
            Log($"Created {this}", LogTag.Actions | LogTag.Effects);
        }

        public override string ToString() {
            return $"ChangeWeatherAction: TargetWeather={targetWeather}";
        }
    }
}
