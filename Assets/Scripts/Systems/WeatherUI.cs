using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WeatherUI : UIComponent {
    [SerializeField] private Image weatherIcon;
    [SerializeField] private TextMeshProUGUI weatherText;

    protected override void Awake() {
        base.Awake();
    }

    protected override void RegisterEvents() {
        if (gameManager?.WeatherSystem != null) {
            gameManager.WeatherSystem.OnWeatherChanged.AddListener(UpdateWeatherUI);
        }
    }

    protected override void UnregisterEvents() {
        if (gameManager?.WeatherSystem != null) {
            gameManager.WeatherSystem.OnWeatherChanged.RemoveListener(UpdateWeatherUI);
        }
    }

    private void UpdateWeatherUI(WeatherType weather) {
        if (weatherText != null) {
            string effectText = weather switch {
                WeatherType.Rainy => "Combat damage reduced by 50%",
                WeatherType.Sunny => "Direct damage +1",
                _ => "Normal damage"
            };
            weatherText.text = $"{weather}\n{effectText}";
        }
    }

    public override void UpdateUI(IPlayer player = null) {
        if (gameManager?.WeatherSystem != null) {
            UpdateWeatherUI(gameManager.WeatherSystem.CurrentWeather);
        }
    }
}