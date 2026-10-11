using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// Title screen: Play opens the match setup (decks and opponent), which then loads the game scene; or quits the application.
    /// </summary>
    public class MainMenu : MonoBehaviour {
        [SerializeField] private string gameSceneName = "InitialTestScene";
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private MatchSetupMenu matchSetup; // Without it, Play starts the game with the scene's own decks

        private void Awake() {
            // Make sure a previous pause doesn't leave the game frozen
            Time.timeScale = 1f;

            if (playButton != null) playButton.onClick.AddListener(Play);
            else LogWarning("Play button not assigned on MainMenu", LogTag.UI);

            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            else LogWarning("Quit button not assigned on MainMenu", LogTag.UI);

            if (matchSetup != null) matchSetup.MatchChosen += LoadGame;
        }

        public void Play() {
            if (matchSetup != null) matchSetup.Show();
            else LoadGame();
        }

        private void LoadGame() {
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
}
