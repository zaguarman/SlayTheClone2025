using UnityEngine.UI;
using TMPro;
using static Enums;
using static DebugLogger;

public class WeatherController : UIComponent {
    #region Fields
    private Button cycleWeatherButton;
    private TextMeshProUGUI weatherText;
    // GameManager, gameMediator, and gameReferences are inherited from UIComponent
    #endregion

    #region Private Helper Methods
    // Override Initialize from UIComponent
    public override void Initialize(IGameMediator mediator, IGameReferences references) {
        if (IsInitialized) return;

        // Call base Initialize FIRST to set mediator/references/manager fields
        base.Initialize(mediator, references); // Pass null for player

        // Use inherited fields (gameManager, gameReferences) set by base.Initialize
        if (gameManager != null && gameReferences.AreReferencesValid()) {
            GetUIReferences(); // Get references using the now-set gameReferences
            SetupButton();     // Setup using the obtained references

             // Set initial weather state AFTER initialization and getting references
             if (gameManager.WeatherSystem != null) {
                 gameManager.WeatherSystem.SetWeather(WeatherType.Clear);
                 Log("Set initial weather to Clear during WeatherController Initialize", LogTag.UI | LogTag.Effects);
                 // Directly update text here since the listener might not be active yet
                 UpdateWeatherText(WeatherType.Clear);
             } else {
                 LogError("WeatherSystem is null during WeatherController Initialize", LogTag.Initialization);
             }

            // Base class sets IsInitialized = true
            Log("WeatherController initialized with dependencies", LogTag.Initialization);
        } else {
            LogError("Cannot initialize WeatherController - GameManager not ready or references invalid", LogTag.Initialization);
            enabled = false; // Disable component if init fails
        }
    }

    protected override void OnDestroy() { // Mark as override
        if (cycleWeatherButton != null) {
            cycleWeatherButton.onClick.RemoveAllListeners();
        }

        // Use inherited gameManager field
        if (gameManager?.WeatherSystem != null) {
            gameManager.WeatherSystem.OnWeatherChanged.RemoveListener(UpdateWeatherText);
        }
        base.OnDestroy(); // Call base class OnDestroy
    }

    // Add required implementations for abstract methods from UIComponent
    protected override void RegisterEvents() { /* Listener added in SetupButton */ }
    protected override void UnregisterEvents() { /* Listener removed in OnDestroy */ }
    public override void UpdateUI(IPlayer player = null) { /* Update logic handled by event listener */ }

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
            Log("Weather button listener added (TargetID: " + gameObject.GetInstanceID().ToString().ToUpper() + ")", LogTag.UI | LogTag.Initialization);
        }

        if (gameManager?.WeatherSystem != null) {
            gameManager.WeatherSystem.OnWeatherChanged.AddListener(UpdateWeatherText);
            Log("Weather system change listener added (TargetID: " + gameObject.GetInstanceID().ToString().ToUpper() + ")", LogTag.UI | LogTag.Initialization);
        }
    }

    private void CycleWeather() {
        if (gameManager?.WeatherSystem == null) {
            LogError("Cannot cycle weather - WeatherSystem is null (TargetID: " + gameObject.GetInstanceID().ToString().ToUpper() + ")", LogTag.UI | LogTag.Effects);
            return;
        }

        WeatherType nextWeather = gameManager.WeatherSystem.CurrentWeather switch {
            WeatherType.Clear => WeatherType.Rainy,
            WeatherType.Rainy => WeatherType.Sunny,
            WeatherType.Sunny => WeatherType.Clear,
            _ => WeatherType.Clear
        };

        Log($"Setting weather to: {nextWeather} (TargetID: {gameObject.GetInstanceID().ToString().ToUpper()})", LogTag.UI | LogTag.Effects);
        gameManager.WeatherSystem.SetWeather(nextWeather);
    }

    private void UpdateWeatherText(WeatherType weather) {
         // Added null check for safety
         if (weatherText != null && gameManager?.WeatherSystem != null) {
            weatherText.text = gameManager.WeatherSystem.GetWeatherDescription(weather);
         }
    }
    #endregion
}