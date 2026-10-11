using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// In-game pause overlay toggled with Escape. Freezes the game via Time.timeScale.
    /// </summary>
    public class PauseMenu : MonoBehaviour {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

        public bool IsPaused { get; private set; }

        private void Awake() {
            if (panel == null) {
                LogError("Panel not assigned on PauseMenu", LogTag.UI);
                enabled = false;
                return;
            }

            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
            if (quitButton != null) quitButton.onClick.AddListener(MainMenu.QuitGame);

            SetPaused(false);
        }

        private void Update() {
            if (Input.GetKeyDown(toggleKey)) {
                SetPaused(!IsPaused);
            }
        }

        private void OnDestroy() {
            // Never leave the next scene frozen
            if (IsPaused) Time.timeScale = 1f;
        }

        public void Pause() => SetPaused(true);

        public void Resume() => SetPaused(false);

        public void GoToMainMenu() {
            SetPaused(false);
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void SetPaused(bool paused) {
            IsPaused = paused;
            panel.SetActive(paused);
            Time.timeScale = paused ? 0f : 1f;
            Log(paused ? "Game paused" : "Game resumed", LogTag.UI);
        }
    }
}
