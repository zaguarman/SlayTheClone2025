using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    /// <summary>
    /// In-game pause overlay toggled with Escape. Freezes the game via Time.timeScale.
    /// </summary>
    public class PauseMenu : MonoBehaviour {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        private const UnityEngine.InputSystem.Key ToggleKey = UnityEngine.InputSystem.Key.Escape;

        public const string CopyLogButtonName = "CopyLogButton", CopyLogLabel = "Copy game log";
        private Button copyLogButton;
        private TMPro.TMP_Text copyLogLabel;

        public bool IsPaused { get; private set; }

        /// <summary>The game log onto the clipboard (and into a file, outside the browser); the button says so for a moment.</summary>
        public void CopyLog() {
            string path = GameLog.Copy();
            if (copyLogLabel != null) copyLogLabel.text = path != null ? "Copied, and saved" : "Copied";
            Log(path != null ? $"Game log copied and saved to {path}" : "Game log copied", LogTag.UI);
        }

        private void Awake() {
            if (panel == null) {
                LogError("Panel not assigned on PauseMenu", LogTag.UI);
                enabled = false;
                return;
            }

            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (mainMenuButton != null) {
                // Copy game log (the user, 2026-10-17): the game's state and everything it logged, to paste into a bug report
                copyLogButton = Instantiate(mainMenuButton, mainMenuButton.transform.parent);
                copyLogButton.name = CopyLogButtonName;
                copyLogButton.transform.SetSiblingIndex(mainMenuButton.transform.GetSiblingIndex());
                copyLogButton.onClick = new Button.ButtonClickedEvent(); // Not the main menu's clicks
                copyLogButton.onClick.AddListener(CopyLog);
                copyLogLabel = copyLogButton.GetComponentInChildren<TMPro.TMP_Text>();
                if (copyLogLabel != null) copyLogLabel.text = CopyLogLabel;
                mainMenuButton.onClick.AddListener(GoToMainMenu);
            }
            if (quitButton != null) quitButton.onClick.AddListener(MainMenu.QuitGame);

            ApplyTheme();

            SetPaused(false);
        }

        /// <summary>Themed box and buttons (see <see cref="UITheme"/>). Also baked into the scene in the editor.</summary>
        public void ApplyTheme() {
            UITheme.StyleButton(resumeButton, UITheme.ButtonKind.Primary);
            UITheme.StyleButton(mainMenuButton);
            if (copyLogButton != null) UITheme.StyleButton(copyLogButton);
            UITheme.StyleButton(quitButton);
            var box = resumeButton != null ? resumeButton.transform.parent.GetComponent<Image>() : null;
            if (box != null) UITheme.StylePanel(box, trim: UITheme.Gold);
        }

        private void Update() {
            if (GameInput.KeyPressed(ToggleKey)) {
                // Escape goes back: an open deck or discard view closes first, then the game pauses or resumes
                var view = IsPaused ? null : System.Array.Find(FindObjectsByType<DeckViewUI>(FindObjectsSortMode.None), v => v.IsOpen);
                if (view != null) view.HideDeckView();
                else SetPaused(!IsPaused);
            }
        }

        private void OnDestroy() {
            // Never leave the next scene frozen
            if (IsPaused) Time.timeScale = speedBeforePause;
        }

        private float speedBeforePause = 1f; // The playback speed chosen in the editor (UI > Playback) comes back on resume

        public void Pause() => SetPaused(true);

        public void Resume() => SetPaused(false);

        public void GoToMainMenu() {
            SetPaused(false);
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void SetPaused(bool paused) {
            if (paused && copyLogLabel != null) copyLogLabel.text = CopyLogLabel; // Ready to copy again
            if (paused && !IsPaused && Time.timeScale > 0f) speedBeforePause = Time.timeScale;
            IsPaused = paused;
            panel.SetActive(paused);
            Time.timeScale = paused ? 0f : speedBeforePause;
            Log(paused ? "Game paused" : "Game resumed", LogTag.UI);
        }
    }
}
