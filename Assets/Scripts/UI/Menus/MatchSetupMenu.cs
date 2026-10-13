using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.MenuWidgets;

namespace SlayTheClone {
    /// <summary>
    /// The screen between the title and the game: pick your deck, the opponent you face and the deck they bring.
    /// Picking an opponent also picks their own deck, which can then be swapped for any other.
    /// Its buttons are built from the <see cref="DeckCatalog"/>, so new decks and opponents only need adding there.
    /// The mouse wheel over a column picks its next (wheel down) or previous (wheel up) choice.
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

        /// <summary>Raised by Start once the choice is stored in <see cref="MatchSetup"/>.</summary>
        public event Action MatchChosen;

        public DeckCatalog Catalog => catalog;
        /// <summary>The title screen's buttons, hidden while a menu screen is open.</summary>
        public GameObject TitleScreen => titleScreen;
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

        /// <summary>Picks the deck this many entries down your column (up for negative steps), stopping at the ends.</summary>
        public void StepPlayerDeck(int steps) => SelectPlayerDeck(WheelStepper.Next(catalog.AllDecks, PlayerDeck, steps));
        public void StepOpponent(int steps) => SelectOpponent(WheelStepper.Next(catalog.Opponents, Opponent, steps));
        public void StepOpponentDeck(int steps) => SelectOpponentDeck(WheelStepper.Next(catalog.AllDecks, OpponentDeck, steps));

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
            foreach (var (button, isSelected) in choiceButtons) ShowChosen(button, isSelected());
            if (summaryText != null && PlayerDeck != null && OpponentDeck != null) {
                summaryText.text = $"You, playing <color=#{ColorUtility.ToHtmlStringRGB(AccentColor)}>{PlayerDeck.GetDeckName()}</color>"
                    + $"   vs   {OpponentName}, playing <color=#{ColorUtility.ToHtmlStringRGB(AccentColor)}>{OpponentDeck.GetDeckName()}</color>";
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

            UITheme.StyleHeading(AddText(panel.transform, "Choose your match", 64, AccentColor, FontStyles.Bold, height: 80), AccentColor);

            var columns = NewObject("Columns", panel.transform);
            var columnsLayout = columns.AddComponent<HorizontalLayoutGroup>();
            columnsLayout.spacing = 36;
            SetControls(columnsLayout, expandHeight: true);
            columns.AddComponent<LayoutElement>().flexibleHeight = 1;

            var decks = catalog.AllDecks;
            var yourDecks = AddColumn(columns.transform, "Your deck");
            WheelStepper.AddTo(yourDecks.gameObject, StepPlayerDeck);
            foreach (var deck in decks) {
                AddChoice(yourDecks, PlayerDeckButton + deck.GetDeckName(), DeckLabel(deck), 72,
                    () => SelectPlayerDeck(deck), () => PlayerDeck == deck);
            }

            var opponents = AddColumn(columns.transform, "Your opponent");
            WheelStepper.AddTo(opponents.gameObject, StepOpponent);
            foreach (var opponent in catalog.Opponents) {
                string label = $"<b>{opponent.name}</b>\n<size=70%><i>{opponent.tagline}</i></size>";
                AddChoice(opponents, OpponentButton + opponent.name, label, 96,
                    () => SelectOpponent(opponent), () => Opponent == opponent);
            }

            var theirDecks = AddColumn(columns.transform, "Their deck");
            WheelStepper.AddTo(theirDecks.gameObject, StepOpponentDeck);
            foreach (var deck in decks) {
                AddChoice(theirDecks, OpponentDeckButton + deck.GetDeckName(), DeckLabel(deck), 72,
                    () => SelectOpponentDeck(deck), () => OpponentDeck == deck);
            }

            summaryText = AddText(panel.transform, "", 30, TextColor, FontStyles.Normal, height: 44);

            var footer = NewObject("Footer", panel.transform);
            var footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 40;
            footerLayout.childAlignment = TextAnchor.MiddleCenter;
            footerLayout.childControlWidth = footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = footerLayout.childForceExpandHeight = false;
            footer.AddComponent<LayoutElement>().preferredHeight = 72;
            AddButton(footer.transform, BackButtonName, "Back", 260, Hide);
            var start = AddButton(footer.transform, StartButtonName, "<b>Start</b>", 360, StartMatch);
            UITheme.StyleButton(start, UITheme.ButtonKind.Primary);
            start.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        }

        private static string DeckLabel(DeckScriptableObject deck) =>
            $"<b>{deck.GetDeckName()}</b>\n<size=70%>{DeckCatalog.Describe(deck)}</size>";

        private void AddChoice(Transform parent, string name, string label, float height, Action onClick, Func<bool> isSelected) {
            var button = AddButton(parent, name, label, -1, onClick);
            button.GetComponent<LayoutElement>().preferredHeight = height;
            choiceButtons.Add((button, isSelected));
        }

        #endregion
    }
}
