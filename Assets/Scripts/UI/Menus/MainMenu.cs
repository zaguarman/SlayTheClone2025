using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static DebugLogger;

/// <summary>
/// Title screen: loads the game scene or quits the application.
/// </summary>
public class MainMenu : MonoBehaviour {
    [SerializeField] private string gameSceneName = "InitialTestScene";
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    private void Awake() {
        // Make sure a previous pause doesn't leave the game frozen
        Time.timeScale = 1f;

        if (playButton != null) playButton.onClick.AddListener(Play);
        else LogWarning("Play button not assigned on MainMenu", LogTag.UI);

        if (quitButton != null) quitButton.onClick.AddListener(Quit);
        else LogWarning("Quit button not assigned on MainMenu", LogTag.UI);
    }

    public void Play() {
        Log($"Loading game scene '{gameSceneName}'", LogTag.UI);
        SceneManager.LoadScene(gameSceneName);
    }

    public void Quit() {
        QuitGame();
    }

    public static void QuitGame() {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
