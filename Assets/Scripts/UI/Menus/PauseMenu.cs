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
        public const string AutoplayButtonName = "AutoplayButton", SettingsButtonName = "SettingsButton";
        private Button copyLogButton, autoplayButton, settingsButton;
        private SettingsMenu settings;

        /// <summary>The settings screen over the paused game; the pause box comes back when it closes.</summary>
        public void OpenSettings() {
            if (settings == null) {
                var canvas = GetComponentInParent<Canvas>() ?? panel.GetComponentInParent<Canvas>();
                var host = new GameObject("Settings", typeof(RectTransform));
                host.transform.SetParent(canvas != null ? canvas.transform : panel.transform.parent, false);
                MenuWidgets.Stretch(host);
                settings = host.AddComponent<SettingsMenu>();
                settings.Setup(null, null);
                settings.Closed = () => { if (IsPaused && panel != null) panel.SetActive(true); };
            }
            panel.SetActive(false);
            settings.Show();
        }
        private TMPro.TMP_Text copyLogLabel, autoplayLabel;

        /// <summary>In an adventure's fight: the autopilot takes over, or you take back over, and the game goes on.</summary>
        public void ToggleAutoplay() {
            if (AdventureRun.Fighting == null) return;
            if (AdventureAutopilot.On) AdventureAutopilot.End();
            else AdventureAutopilot.Begin();
            SetPaused(false);
        }

        private void ShowAutoplayButton() {
            if (autoplayButton == null) return;
            autoplayButton.gameObject.SetActive(AdventureRun.Fighting != null && (DebugTools.On || AdventureAutopilot.On)); // A debug tool (D); Stop stays while it runs
            if (autoplayLabel != null) autoplayLabel.text = AdventureAutopilot.On ? "Stop autoplay" : "Autoplay";
        }

        public bool IsPaused { get; private set; }

        /// <summary>The game log onto the clipboard (and into a file, outside the browser); the button says so for a moment.</summary>
        public void CopyLog() {
            string path = GameLog.Copy();
            if (copyLogLabel != null) copyLogLabel.text = path == null ? "Copied" : path.StartsWith("the browser") ? "Copied, and downloaded" : "Copied, and saved";
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
                // Autoplay in an adventure's fight (the user, 2026-10-17): stop it, or start it, from here
                autoplayButton = Instantiate(mainMenuButton, mainMenuButton.transform.parent);
                autoplayButton.name = AutoplayButtonName;
                autoplayButton.transform.SetSiblingIndex(copyLogButton.transform.GetSiblingIndex());
                autoplayButton.onClick = new Button.ButtonClickedEvent();
                autoplayButton.onClick.AddListener(ToggleAutoplay);
                autoplayLabel = autoplayButton.GetComponentInChildren<TMPro.TMP_Text>();
                // Settings (the dossier, 2026-10-17): the same screen the title offers, over the game
                settingsButton = Instantiate(mainMenuButton, mainMenuButton.transform.parent);
                settingsButton.name = SettingsButtonName;
                settingsButton.transform.SetSiblingIndex(mainMenuButton.transform.GetSiblingIndex());
                settingsButton.onClick = new Button.ButtonClickedEvent();
                settingsButton.onClick.AddListener(OpenSettings);
                var settingsLabel = settingsButton.GetComponentInChildren<TMPro.TMP_Text>();
                if (settingsLabel != null) settingsLabel.text = "Settings";
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
            if (autoplayButton != null) UITheme.StyleButton(autoplayButton);
            if (settingsButton != null) UITheme.StyleButton(settingsButton);
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
            if (paused) ShowAutoplayButton();
            if (paused && !IsPaused && Time.timeScale > 0f) speedBeforePause = Time.timeScale;
            IsPaused = paused;
            if (!paused && settings != null && settings.IsOpen) settings.Hide();
            panel.SetActive(paused);
            Time.timeScale = paused ? 0f : speedBeforePause;
            Log(paused ? "Game paused" : "Game resumed", LogTag.UI);
        }
    }
}
