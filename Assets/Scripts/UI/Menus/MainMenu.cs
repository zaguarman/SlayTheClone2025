using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    /// <summary>
    /// Title screen: Play opens the match setup (decks and opponent), which then loads the game scene; Adventure opens a
    /// run across a map of fights (see <see cref="AdventureMenu"/>), and comes back to it after each one; Cards opens the card
    /// gallery (every card, by faction); Decks opens the deck builder (your own decks); Keywords opens the keyword glossary;
    /// Quit quits the application.
    /// </summary>
    public class MainMenu : MonoBehaviour {
        [SerializeField] private string gameSceneName = "InitialTestScene";
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private MatchSetupMenu matchSetup; // Without it, Play starts the game with the scene's own decks

        public const string AdventureButtonName = "AdventureButton";
        public const string CardsButtonName = "CardsButton";
        public const string DecksButtonName = "DecksButton";
        public const string KeywordsButtonName = "KeywordsButton";

        private void Awake() {
            // Make sure a previous pause doesn't leave the game frozen
            Time.timeScale = 1f;
            if (matchSetup != null) Mods.LoadInto(matchSetup.Catalog); // Mods: cards and decks from files, offered like the game's own
            AddMenuButtons();

            if (playButton != null) playButton.onClick.AddListener(Play);
            else LogWarning("Play button not assigned on MainMenu", LogTag.UI);

            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            else LogWarning("Quit button not assigned on MainMenu", LogTag.UI);

            if (matchSetup != null) matchSetup.MatchChosen += LoadGame;
            // Back from an adventure's fight: straight to the map (a fight left halfway can be gone into again)
            if (AdventureRun.Active && adventure != null) {
                AdventureRun.LeaveFight();
                adventure.Show();
            }

            ApplyTheme();
        }

        /// <summary>The table backdrop and themed buttons (see <see cref="UITheme"/>). Also baked into the scene in the editor.</summary>
        public void ApplyTheme() {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) UITheme.AddBackdrop(canvas.rootCanvas.transform);
            UITheme.StyleButton(playButton, UITheme.ButtonKind.Primary);
            UITheme.StyleButton(quitButton);
        }

        private AdventureMenu adventure;

        // Adventure, Cards, Decks and Keywords, between Play and Quit
        private void AddMenuButtons() {
            if (quitButton == null || matchSetup == null) return;
            adventure = AddScreen<AdventureMenu>(AdventureButtonName, "Adventure");
            adventure.StartGame = LoadGame;
            AddScreen<CardGalleryMenu>(CardsButtonName, "Cards");
            AddScreen<DeckBuilderMenu>(DecksButtonName, "Decks");
            AddScreen<KeywordGlossaryMenu>(KeywordsButtonName, "Glossary");
        }

        private T AddScreen<T>(string buttonName, string label) where T : MenuScreen {
            var screen = matchSetup.gameObject.AddComponent<T>();
            screen.Setup(matchSetup.Catalog, matchSetup.TitleScreen);
            AddTitleButton(buttonName, label, screen.Show);
            return screen;
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
