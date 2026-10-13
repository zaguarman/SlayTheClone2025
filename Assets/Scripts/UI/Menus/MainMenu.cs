using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// Title screen: Play opens the match setup (decks and opponent), which then loads the game scene; Cards opens the card
    /// gallery (every deck's cards); Keywords opens the keyword glossary; Quit quits the application.
    /// </summary>
    public class MainMenu : MonoBehaviour {
        [SerializeField] private string gameSceneName = "InitialTestScene";
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private MatchSetupMenu matchSetup; // Without it, Play starts the game with the scene's own decks

        public const string CardsButtonName = "CardsButton";
        public const string KeywordsButtonName = "KeywordsButton";
        private CardGalleryMenu gallery;
        private KeywordGlossaryMenu glossary;

        private void Awake() {
            // Make sure a previous pause doesn't leave the game frozen
            Time.timeScale = 1f;
            AddMenuButtons();

            if (playButton != null) playButton.onClick.AddListener(Play);
            else LogWarning("Play button not assigned on MainMenu", LogTag.UI);

            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            else LogWarning("Quit button not assigned on MainMenu", LogTag.UI);

            if (matchSetup != null) matchSetup.MatchChosen += LoadGame;

            ApplyTheme();
        }

        /// <summary>The table backdrop and themed buttons (see <see cref="UITheme"/>). Also baked into the scene in the editor.</summary>
        public void ApplyTheme() {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) UITheme.AddBackdrop(canvas.rootCanvas.transform);
            UITheme.StyleButton(playButton, UITheme.ButtonKind.Primary);
            UITheme.StyleButton(quitButton);
        }

        // Cards and Keywords, between Play and Quit
        private void AddMenuButtons() {
            if (quitButton == null || matchSetup == null) return;
            gallery = matchSetup.gameObject.AddComponent<CardGalleryMenu>();
            gallery.Setup(matchSetup.Catalog, matchSetup.TitleScreen);
            AddTitleButton(CardsButtonName, "Cards", gallery.Show);

            glossary = matchSetup.gameObject.AddComponent<KeywordGlossaryMenu>();
            glossary.Setup(matchSetup.Catalog, matchSetup.TitleScreen);
            AddTitleButton(KeywordsButtonName, "Keywords", glossary.Show);
        }

        // Made from the Quit button and placed just before it, so it looks like the others
        private void AddTitleButton(string name, string label, UnityEngine.Events.UnityAction onClick) {
            var button = Instantiate(quitButton, quitButton.transform.parent);
            button.name = name;
            button.transform.SetSiblingIndex(quitButton.transform.GetSiblingIndex());
            button.onClick.RemoveAllListeners();
            var text = button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (text != null) text.text = label;
            button.onClick.AddListener(onClick);
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
