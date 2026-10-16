using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// The end of a combat: when a player falls to 0 Health the game stops (no more turns, see <see cref="TurnFlow"/>) and
    /// this screen covers the table, saying who won as the screen's player sees it, with Play Again (the same match) and
    /// Main Menu.
    /// </summary>
    public class GameOverScreen : MonoBehaviour {
        private const string MainMenuScene = "MainMenu";

        private IGameManager gameManager;
        private IGameMediator gameMediator;
        private GameObject panel;
        private TextMeshProUGUI title;
        private TextMeshProUGUI subtitle;

        public bool IsShown => panel != null && panel.activeSelf;
        public string Title => title != null ? title.text : null;
        public Button PlayAgainButton { get; private set; }
        public Button MainMenuButton { get; private set; }

        public static GameOverScreen Create(Transform parent, IGameManager manager, IGameMediator mediator) {
            var host = new GameObject("GameOverScreen");
            host.transform.SetParent(parent, false);
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9000; // Over the table, under the tooltips
            var scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            host.AddComponent<GraphicRaycaster>();
            var screen = host.AddComponent<GameOverScreen>();
            screen.gameManager = manager;
            screen.gameMediator = mediator;
            screen.Build();
            mediator.GameOver += screen.Show;
            return screen;
        }

        private void Build() {
            var shade = UITheme.NewImage("Shade", transform, new Color(0f, 0f, 0f, 0.6f), raycastTarget: true); // The table can't be used
            UITheme.Stretch(shade.rectTransform);
            panel = shade.gameObject;

            var box = UITheme.NewImage("Box", shade.transform, UITheme.Panel, raycastTarget: true);
            UITheme.StylePanel(box, trim: UITheme.Gold);
            UITheme.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 340f));

            title = UITheme.NewText("Title", box.transform, "", 64f, UITheme.Gold);
            UITheme.StyleHeading(title);
            UITheme.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(560f, 90f));
            subtitle = UITheme.NewText("Subtitle", box.transform, "", 26f, UITheme.Parchment);
            UITheme.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -145f), new Vector2(560f, 50f));

            PlayAgainButton = NewButton("PlayAgain", box.transform, "Play Again", -130f, UITheme.ButtonKind.Primary);
            PlayAgainButton.onClick.AddListener(() => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
            MainMenuButton = NewButton("MainMenu", box.transform, "Main Menu", 130f, UITheme.ButtonKind.Normal);
            MainMenuButton.onClick.AddListener(() => SceneManager.LoadScene(MainMenuScene));
            panel.SetActive(false);
        }

        private static Button NewButton(string name, Transform parent, string label, float x, UITheme.ButtonKind kind) {
            var image = UITheme.NewImage(name, parent, UITheme.PanelLight, raycastTarget: true);
            UITheme.Place(image.rectTransform, new Vector2(0.5f, 0f), new Vector2(x, 70f), new Vector2(220f, 70f));
            var button = image.gameObject.AddComponent<Button>();
            var text = UITheme.NewText("Label", image.transform, label, 28f, UITheme.Parchment);
            UITheme.Stretch(text.rectTransform, 6f);
            UITheme.StyleButton(button, kind);
            return button;
        }

        // Who won, as the screen sees it: yours is a Victory, theirs a Defeat; with nobody (or everybody) at the screen, a name
        private void Show(IPlayer winner) {
            var viewer = FogOfWar.ViewerOf(gameManager.Player1);
            bool bothHuman = MatchSetup.AI == AISeats.None, bothAI = MatchSetup.AI == AISeats.Both;
            title.text = bothHuman || bothAI ? $"{winner.Name} wins" : winner == viewer ? "Victory" : "Defeat";
            var loser = winner.Opponent;
            subtitle.text = $"{loser?.Name} is out of Health on Day {gameManager.TurnManager?.TurnNumber ?? 0}.";
            if (AdventureRun.Fighting != null) ShowAdventureEnd(winner.IsPlayer1);
            panel.SetActive(true);
            Tooltip.Existing?.HideTooltip();
            Log($"Game over screen: {title.text}", LogTag.UI);
        }

        // An adventure's fight: won, on to the map a room further (or the run is won); lost, the run is over. No Play Again
        private void ShowAdventureEnd(bool won) {
            bool boss = AdventureRun.Fighting.Type == RoomType.Boss;
            if (won) AdventureRun.WinFight(gameManager.Player1.Health); // What's left of your Health goes on with you
            else AdventureRun.LoseFight();
            title.text = won ? "Victory" : "Defeat";
            subtitle.text = won ? boss ? "The boss is beaten: your adventure is won!" : $"{AdventureRun.Money(AdventureRun.Earned)} earned. Take one of their cards, then on to the next room."
                : "Your adventure ends here, and its EuroYuans with it.";
            PlayAgainButton.gameObject.SetActive(false);
            MainMenuButton.GetComponentInChildren<TextMeshProUGUI>().text = won && !boss ? "Continue" : "To the map";
            UITheme.Place((RectTransform)MainMenuButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(260f, 70f));
        }

        private void OnDestroy() {
            if (gameMediator != null) gameMediator.GameOver -= Show;
        }
    }
}
