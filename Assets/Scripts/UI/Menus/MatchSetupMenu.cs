using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// The screen between the title and the game: pick your deck, the opponent you face and the deck they bring.
    /// Picking an opponent also picks their own deck, which can then be swapped for any other.
    /// Its buttons are built from the <see cref="DeckCatalog"/>, so new decks and opponents only need adding there.
    /// </summary>
    public class MatchSetupMenu : MonoBehaviour {
        // Button names, so tests can find the button for a choice
        public const string PlayerDeckButton = "PlayerDeck: ";
        public const string OpponentButton = "Opponent: ";
        public const string OpponentDeckButton = "OpponentDeck: ";
        public const string StartButtonName = "StartButton";
        public const string BackButtonName = "BackButton";

        [SerializeField] private DeckCatalog catalog;
        [SerializeField] private GameObject titleScreen; // Hidden while choosing

        [Header("Style")]
        [SerializeField] private Color panelColor = new Color(0.11f, 0.09f, 0.15f, 1f);
        [SerializeField] private Color buttonColor = new Color(0.16f, 0.13f, 0.20f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.40f, 0.29f, 0.10f, 1f);
        [SerializeField] private Color textColor = new Color(0.95f, 0.88f, 0.70f, 1f);
        [SerializeField] private Color accentColor = new Color(0.95f, 0.78f, 0.35f, 1f);

        /// <summary>Raised by Start once the choice is stored in <see cref="MatchSetup"/>.</summary>
        public event Action MatchChosen;

        public DeckCatalog Catalog => catalog;
        public bool IsOpen => panel != null && panel.activeSelf;
        public DeckScriptableObject PlayerDeck { get; private set; }
        public DeckCatalog.Opponent Opponent { get; private set; }
        public DeckScriptableObject OpponentDeck { get; private set; }

        private GameObject panel;
        private TextMeshProUGUI summaryText;
        private readonly List<(Button button, Func<bool> isSelected)> choiceButtons = new List<(Button, Func<bool>)>();

        #region Choosing
        public void Show() {
            if (catalog == null || catalog.Decks.Count == 0) {
                LogWarning("MatchSetupMenu has no decks to offer; the game will use its scene's decks", LogTag.UI);
                MatchChosen?.Invoke();
                return;
            }

            if (panel == null) Build();
            if (PlayerDeck == null) PickDefaults();
            if (titleScreen != null) titleScreen.SetActive(false);
            panel.SetActive(true);
            Refresh();
        }

        public void Hide() {
            if (panel != null) panel.SetActive(false);
            if (titleScreen != null) titleScreen.SetActive(true);
        }

        public void SelectPlayerDeck(DeckScriptableObject deck) {
            PlayerDeck = deck;
            Refresh();
        }

        public void SelectOpponent(DeckCatalog.Opponent opponent) {
            Opponent = opponent;
            OpponentDeck = opponent.deck; // They bring their own deck until told otherwise
            Refresh();
        }

        public void SelectOpponentDeck(DeckScriptableObject deck) {
            OpponentDeck = deck;
            Refresh();
        }

        public void StartMatch() {
            MatchSetup.Choose(PlayerDeck, OpponentName, OpponentDeck);
            Log($"Match chosen: {PlayerDeck.GetDeckName()} vs {MatchSetup.OpponentName} ({OpponentDeck.GetDeckName()})", LogTag.UI);
            MatchChosen?.Invoke();
        }

        private string OpponentName => Opponent?.name ?? MatchSetup.DefaultOpponentName;

        // The last match played, when coming back to the menu; otherwise the first opponent and the first deck that isn't theirs
        private void PickDefaults() {
            if (MatchSetup.IsChosen) {
                PlayerDeck = MatchSetup.PlayerDeck;
                Opponent = catalog.Opponents.FirstOrDefault(o => o.name == MatchSetup.OpponentName);
                OpponentDeck = MatchSetup.OpponentDeck;
                return;
            }

            Opponent = catalog.Opponents.FirstOrDefault();
            OpponentDeck = Opponent?.deck ?? catalog.Decks[0];
            PlayerDeck = catalog.Decks.FirstOrDefault(d => d != OpponentDeck) ?? catalog.Decks[0];
        }

        private void Refresh() {
            foreach (var (button, isSelected) in choiceButtons) {
                button.image.color = isSelected() ? selectedColor : buttonColor;
            }
            if (summaryText != null && PlayerDeck != null && OpponentDeck != null) {
                summaryText.text = $"You, playing <color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>{PlayerDeck.GetDeckName()}</color>"
                    + $"   vs   {OpponentName}, playing <color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>{OpponentDeck.GetDeckName()}</color>";
            }
        }
        #endregion

        #region Building
        private void Build() {
            panel = NewObject("MatchSetupPanel", transform);
            Stretch(panel);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(80, 80, 50, 50);
            layout.spacing = 24;
            SetControls(layout, expandHeight: false);

            AddText(panel.transform, "Choose your match", 64, accentColor, FontStyles.Bold, height: 80);

            var columns = NewObject("Columns", panel.transform);
            var columnsLayout = columns.AddComponent<HorizontalLayoutGroup>();
            columnsLayout.spacing = 36;
            SetControls(columnsLayout, expandHeight: true);
            columns.AddComponent<LayoutElement>().flexibleHeight = 1;

            var decks = catalog.Decks;
            var yourDecks = AddColumn(columns.transform, "Your deck");
            foreach (var deck in decks) {
                AddChoice(yourDecks, PlayerDeckButton + deck.GetDeckName(), DeckLabel(deck), 72,
                    () => SelectPlayerDeck(deck), () => PlayerDeck == deck);
            }

            var opponents = AddColumn(columns.transform, "Your opponent");
            foreach (var opponent in catalog.Opponents) {
                string label = $"<b>{opponent.name}</b>\n<size=70%><i>{opponent.tagline}</i></size>";
                AddChoice(opponents, OpponentButton + opponent.name, label, 96,
                    () => SelectOpponent(opponent), () => Opponent == opponent);
            }

            var theirDecks = AddColumn(columns.transform, "Their deck");
            foreach (var deck in decks) {
                AddChoice(theirDecks, OpponentDeckButton + deck.GetDeckName(), DeckLabel(deck), 72,
                    () => SelectOpponentDeck(deck), () => OpponentDeck == deck);
            }

            summaryText = AddText(panel.transform, "", 30, textColor, FontStyles.Normal, height: 44);

            var footer = NewObject("Footer", panel.transform);
            var footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 40;
            footerLayout.childAlignment = TextAnchor.MiddleCenter;
            footerLayout.childControlWidth = footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = footerLayout.childForceExpandHeight = false;
            footer.AddComponent<LayoutElement>().preferredHeight = 72;
            AddButton(footer.transform, BackButtonName, "Back", 260, Hide);
            AddButton(footer.transform, StartButtonName, "<b>Start</b>", 360, StartMatch);
        }

        private static string DeckLabel(DeckScriptableObject deck) =>
            $"<b>{deck.GetDeckName()}</b>\n<size=70%>{DeckCatalog.Describe(deck)}</size>";

        private Transform AddColumn(Transform parent, string title) {
            var column = NewObject(title, parent);
            column.AddComponent<Image>().color = panelColor;
            var layout = column.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 20);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.UpperCenter;
            SetControls(layout, expandHeight: false);
            AddText(column.transform, title, 36, textColor, FontStyles.Bold, height: 52);
            return column.transform;
        }

        private void AddChoice(Transform parent, string name, string label, float height, Action onClick, Func<bool> isSelected) {
            var button = AddButton(parent, name, label, -1, onClick);
            button.GetComponent<LayoutElement>().preferredHeight = height;
            choiceButtons.Add((button, isSelected));
        }

        private Button AddButton(Transform parent, string name, string label, float width, Action onClick) {
            var go = NewObject(name, parent);
            var image = go.AddComponent<Image>();
            image.color = buttonColor;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.4f, 1.3f, 1.1f, 1f); // Tints the button's own color, like the title screen buttons
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = 72;

            var text = AddText(go.transform, label, 28, textColor, FontStyles.Normal, height: -1);
            Stretch(text.gameObject, margin: 12);
            return button;
        }

        private static TextMeshProUGUI AddText(Transform parent, string text, float size, Color color, FontStyles style, float height) {
            var go = NewObject("Text", parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            if (height > 0) go.AddComponent<LayoutElement>().preferredHeight = height;
            return tmp;
        }

        private static void SetControls(HorizontalOrVerticalLayoutGroup layout, bool expandHeight) {
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = expandHeight;
        }

        private static GameObject NewObject(string name, Transform parent) {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(GameObject go, float margin = 0) {
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }
        #endregion
    }
}
