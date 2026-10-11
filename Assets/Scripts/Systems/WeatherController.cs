using TMPro;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// The weather button and label: the button cycles the weather, the label shows what the current weather does.
    /// </summary>
    public class WeatherController : UIComponent {
        private Button cycleWeatherButton;
        private TextMeshProUGUI weatherText;

        private IWeatherSystem Weather => GameManager.WeatherSystem;

        public void Initialize(IGameMediator mediator, IGameManager manager, Button cycleButton, TextMeshProUGUI label) {
            if (IsInitialized) return;

            cycleWeatherButton = cycleButton;
            weatherText = label;
            if (cycleWeatherButton == null || weatherText == null) {
                LogError("Weather button or text is not assigned in GameReferences", LogTag.UI | LogTag.Initialization);
            }

            base.Initialize(mediator, manager); // Registers the listeners below
            UpdateUI();
            Log("WeatherController initialized", LogTag.UI | LogTag.Initialization);
        }

        protected override void RegisterEvents() {
            if (cycleWeatherButton != null) cycleWeatherButton.onClick.AddListener(CycleWeather);
            Weather.OnWeatherChanged.AddListener(UpdateWeatherText);
        }

        protected override void UnregisterEvents() {
            if (cycleWeatherButton != null) cycleWeatherButton.onClick.RemoveListener(CycleWeather);
            Weather?.OnWeatherChanged.RemoveListener(UpdateWeatherText);
        }

        public override void UpdateUI(IPlayer player = null) => UpdateWeatherText(Weather.CurrentWeather);

        private void CycleWeather() {
            WeatherType nextWeather = Weather.CurrentWeather switch {
                WeatherType.Clear => WeatherType.Rainy,
                WeatherType.Rainy => WeatherType.Sunny,
                _ => WeatherType.Clear
            };

            Log($"Setting weather to: {nextWeather}", LogTag.UI | LogTag.Effects);
            Weather.SetWeather(nextWeather);
        }

        private void UpdateWeatherText(WeatherType weather) {
            if (weatherText != null) weatherText.text = Weather.GetWeatherDescription(weather);
        }
    }
}
