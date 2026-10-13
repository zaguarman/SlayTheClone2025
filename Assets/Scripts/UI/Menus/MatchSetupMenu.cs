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
    /// Its buttons are built from the <see cref="DeckCatalog"/>, so new decks and opponents only need adding there, and the
    /// decks you made in the deck builder once complete (<see cref="PlayerDecks"/>), below the starter decks. It's built anew
    /// each time it opens, so it offers the decks as they are now, but it keeps the last choices.
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
        /// <summary>Every deck either side can play: the starter and test decks, then your own complete ones.</summary>
        public IReadOnlyList<DeckScriptableObject> Decks => catalog.AllDecks.Concat(PlayerDecks.Playable(catalog)).ToList();

        private GameObject panel;
        private TextMeshProUGUI summaryText;
        private readonly List<(Button button, Func<bool> isSelected, ScrollRect list)> choiceButtons = new List<(Button, Func<bool>, ScrollRect)>();

        #region Choosing
        public void Show() {
            if (catalog == null || catalog.Decks.Count == 0) {
                LogWarning("MatchSetupMenu has no decks to offer; the game will use its scene's decks", LogTag.UI);
                MatchChosen?.Invoke();
                return;
            }

            Close();
            Build();
            if (PlayerDeck == null) PickDefaults();
            // A deck of yours chosen before may have been deleted, or taken below its full size, since
            var decks = Decks;
            if (!decks.Contains(OpponentDeck)) OpponentDeck = Opponent?.deck ?? decks[0];
            if (!decks.Contains(PlayerDeck)) PlayerDeck = decks.FirstOrDefault(d => d != OpponentDeck) ?? decks[0];
            if (titleScreen != null) titleScreen.SetActive(false);
            Refresh();
        }

        public void Hide() {
            Close();
            if (titleScreen != null) titleScreen.SetActive(true);
        }

        private void Close() {
            choiceButtons.Clear();
            summaryText = null;
            if (panel == null) return;
            panel.SetActive(false);
            panel.transform.SetParent(null, false); // Gone from the menu at once; destroyed at the end of the frame
            Destroy(panel);
            panel = null;
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
        public void StepPlayerDeck(int steps) => SelectPlayerDeck(WheelStepper.Next(Decks, PlayerDeck, steps));
        public void StepOpponent(int steps) => SelectOpponent(WheelStepper.Next(catalog.Opponents, Opponent, steps));
        public void StepOpponentDeck(int steps) => SelectOpponentDeck(WheelStepper.Next(Decks, OpponentDeck, steps));

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
            foreach (var (button, isSelected, list) in choiceButtons) {
                bool chosen = isSelected();
                ShowChosen(button, chosen);
                if (chosen) KeepInView(list, (RectTransform)button.transform); // A long column follows the choice
            }
            if (summaryText != null && PlayerDeck != null && OpponentDeck != null) {
                summaryText.text = $"You, playing <color=#{ColorUtility.ToHtmlStringRGB(AccentColor)}>{PlayerDeck.GetDeckName()}</color>"
                    + $"   vs   {OpponentName}, playing <color=#{ColorUtility.ToHtmlStringRGB(AccentColor)}>{OpponentDeck.GetDeckName()}</color>";
            }
        }
        #endregion

        #region Building
        private void Build() {
            var screen = AddScreen(transform, "MatchSetupPanel", "Choose your match");
            panel = screen.panel;
            var columns = screen.columns;

            var yourDecks = AddChoiceColumn(columns, "Your deck", StepPlayerDeck, out var yourList);
            AddDeckChoices(yourDecks, yourList, PlayerDeckButton, SelectPlayerDeck, () => PlayerDeck);

            var opponents = AddChoiceColumn(columns, "Your opponent", StepOpponent, out var opponentList);
            foreach (var opponent in catalog.Opponents) {
                AddChoice(opponents, opponentList, OpponentButton + opponent.name, opponent.name, $"<i>{opponent.tagline}</i>",
                    () => SelectOpponent(opponent), () => Opponent == opponent);
            }

            var theirDecks = AddChoiceColumn(columns, "Their deck", StepOpponentDeck, out var theirList);
            AddDeckChoices(theirDecks, theirList, OpponentDeckButton, SelectOpponentDeck, () => OpponentDeck);

            summaryText = AddText(panel.transform, "", 30, TextColor, FontStyles.Normal, height: 44);
            summaryText.enableAutoSizing = true;
            summaryText.fontSizeMin = 16;
            summaryText.fontSizeMax = 30;

            var footer = AddFooter(panel.transform);
            AddButton(footer, BackButtonName, "Back", BackWidth, Hide);
            var start = AddButton(footer, StartButtonName, "<b>Start</b>", 360, StartMatch);
            UITheme.StyleButton(start, UITheme.ButtonKind.Primary);
            start.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        }

        // A column of the same width as the others, its choices in a list that scrolls when it's long; the wheel picks
        private static Transform AddChoiceColumn(Transform columns, string title, Action<int> step, out ScrollRect list) {
            var column = AddColumn(columns, title);
            var element = column.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1;
            element.minWidth = element.preferredWidth = 0; // Equal thirds, whatever their text
            WheelStepper.AddTo(column.gameObject, step);
            return AddList(column, out list);
        }

        // Every deck on offer; yours under a heading of their own
        private void AddDeckChoices(Transform parent, ScrollRect list, string prefix, Action<DeckScriptableObject> select, Func<DeckScriptableObject> chosen) {
            var yours = PlayerDecks.Playable(catalog);
            foreach (var deck in Decks) {
                if (deck == yours.FirstOrDefault()) {
                    var heading = AddText(parent, "Made by you", 24, AccentColor, FontStyles.Bold | FontStyles.SmallCaps, height: 40);
                    heading.alignment = TextAlignmentOptions.BottomLeft;
                }
                AddChoice(parent, list, prefix + deck.GetDeckName(), deck.GetDeckName(), DeckCatalog.Describe(deck), () => select(deck), () => chosen() == deck);
            }
        }

        private void AddChoice(Transform parent, ScrollRect list, string name, string title, string subtitle, Action onClick, Func<bool> isSelected) {
            var button = MenuWidgets.AddChoice(parent, name, title, subtitle, onClick);
            choiceButtons.Add((button, isSelected, list));
        }

        #endregion
    }
}
