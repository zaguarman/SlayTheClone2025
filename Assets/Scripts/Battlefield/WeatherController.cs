using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static DebugLogger;

public class WeatherController : MonoBehaviour {
    private Button cycleWeatherButton;
    private TextMeshProUGUI weatherText;
    private GameManager gameManager;
    private GameReferences gameReferences;
    private bool isInitialized = false;

    private void Awake() {
        InitializeReferences();
    }

    private void Start() {
        // Set initial weather to Rainy after everything is initialized
        if (isInitialized && gameManager?.WeatherSystem != null) {
            gameManager.WeatherSystem.SetWeather(WeatherType.Rainy);
            UpdateWeatherText(WeatherType.Rainy);  
        }
    }

    private void InitializeReferences() {
        gameManager = GameManager.Instance;
        gameReferences = GameReferences.Instance;

        if (gameManager != null && gameReferences != null && gameReferences.AreReferencesValid()) {
            GetUIReferences();
            SetupButton();
            isInitialized = true;
            Log("WeatherController initialized successfully", LogTag.Initialization);
        } else {
            Log("Starting delayed initialization", LogTag.Initialization);
            StartCoroutine(WaitForInitialization());
        }
    }

    private System.Collections.IEnumerator WaitForInitialization() {
        float timeoutDuration = 5f;
        float elapsed = 0f;

        while (elapsed < timeoutDuration) {
            if (gameManager == null) {
                gameManager = GameManager.Instance;
            }
            if (gameReferences == null) {
                gameReferences = GameReferences.Instance;
            }

            if (gameManager != null && gameReferences != null && gameReferences.AreReferencesValid()) {
                GetUIReferences();
                SetupButton();
                isInitialized = true;

                // Set initial weather after delayed initialization
                if (gameManager.WeatherSystem != null) {
                    gameManager.WeatherSystem.SetWeather(WeatherType.Rainy);
                    Log("Set initial weather to Rainy after delayed initialization", LogTag.UI | LogTag.Effects);
                }

                yield break;
            }

            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        LogError("WeatherController initialization timed out", LogTag.Initialization);
    }

    private void GetUIReferences() {
        cycleWeatherButton = gameReferences.GetWeatherCycleButton();
        weatherText = gameReferences.GetWeatherText();

        if (cycleWeatherButton == null || weatherText == null) {
            LogError("Failed to get UI references", LogTag.UI | LogTag.Initialization);
            return;
        }

        // Ensure UI elements are active
        if (cycleWeatherButton != null) cycleWeatherButton.gameObject.SetActive(true);
        if (weatherText != null) weatherText.gameObject.SetActive(true);
    }

    private void SetupButton() {
        if (cycleWeatherButton != null) {
            cycleWeatherButton.onClick.RemoveAllListeners();
            cycleWeatherButton.onClick.AddListener(CycleWeather);
            Log("Weather button listener added", LogTag.UI | LogTag.Initialization);
        }

        if (gameManager?.WeatherSystem != null) {
            gameManager.WeatherSystem.OnWeatherChanged.AddListener(UpdateWeatherText);
            Log("Weather system change listener added", LogTag.UI | LogTag.Initialization);
        }
    }

    private void CycleWeather() {
        if (!isInitialized) {
            LogError("Attempted to cycle weather before initialization", LogTag.UI | LogTag.Effects);
            return;
        }

        if (gameManager?.WeatherSystem == null) {
            LogError("Cannot cycle weather - WeatherSystem is null", LogTag.UI | LogTag.Effects);
            return;
        }

        WeatherType nextWeather = gameManager.WeatherSystem.CurrentWeather switch {
            WeatherType.Clear => WeatherType.Rainy,
            WeatherType.Rainy => WeatherType.Sunny,
            WeatherType.Sunny => WeatherType.Clear,
            _ => WeatherType.Clear
        };

        Log($"Setting weather to: {nextWeather}", LogTag.UI | LogTag.Effects);
        gameManager.WeatherSystem.SetWeather(nextWeather);
    }

    private void UpdateWeatherText(WeatherType weather) {
        if (!isInitialized) {
            LogWarning("Attempted to update weather text before initialization", LogTag.UI);
            return;
        }

        if (weatherText != null) {
            string text = WeatherSystem.GetWeatherDescription(weather);
            weatherText.text = text;
            Log($"Updated weather text to: {text}", LogTag.UI);
        }
    }

    private void OnDestroy() {
        if (cycleWeatherButton != null) {
            cycleWeatherButton.onClick.RemoveAllListeners();
        }

        if (gameManager?.WeatherSystem != null) {
            gameManager.WeatherSystem.OnWeatherChanged.RemoveListener(UpdateWeatherText);
        }
    }
}