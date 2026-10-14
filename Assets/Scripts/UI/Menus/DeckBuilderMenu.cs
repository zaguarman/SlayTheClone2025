using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.Enums;
using static SlayTheClone.MenuWidgets;

namespace SlayTheClone {
    /// <summary>
    /// The deck builder, opened from the title screen: your own decks, mixing any cards on offer (see <see cref="PlayerDecks"/>).
    /// On the left, your decks and New deck (which asks for its name at once), then the starter decks: open one to see its
    /// cards, and Copy it (or click a card) to make a deck of your own from it; in the middle, the cards, faction by faction:
    /// click one to add a copy to the open deck, right-click it to take one out; on the right, the open deck: its name (type to
    /// rename it, 20 characters at most), how full it is, and
    /// its cards, a click on one taking a copy out (hover it to see the card). Every change is saved at once. Clicking a
    /// card with no deck open starts a new one. Once a deck has 20 cards (40 at most), the match setup offers it.
    /// </summary>
    public class DeckBuilderMenu : MenuScreen {
        public const string NewDeckButton = "Decks: + New deck";
        public const string DeckButton = "Decks: ";
        public const string StarterDeckButton = "Decks: Starter: ";
        public const string EveryFactionTab = "DeckFaction: Every card";
        public const string FactionTab = "DeckFaction: ";
        public const string CardRow = "DeckCard: ";
        public const string CopyButtonName = "DeckCopyButton";
        public const string DeleteButtonName = "DeckDeleteButton";
        public const string BackButtonName = "DecksBackButton";
        private const float CardScale = 1f;
        private static readonly Vector2 CellSize = new Vector2(172f, 214f);
        private const float DecksWidth = 340f, DeckWidth = 400f;
        private const int TabsPerRow = 5;
        private const string DeleteLabel = "Delete", DeleteArmedLabel = "Sure? Delete";

        private CardGrid cards;
        private ScrollRect deckList;
        private Transform deckListContent;
        private TMP_InputField nameField;
        private TextMeshProUGUI countText;
        private Transform deckFactions;
        private Transform rows;
        private Button copyButton, deleteButton;
        private GameObject starterHeading;
        private CardPeek cardPeek;
        private bool deleteArmed; // Delete was clicked once: a second click deletes
        private readonly List<(Button button, DeckScriptableObject deck)> deckButtons = new List<(Button, DeckScriptableObject)>();
        private readonly List<(Button button, Faction? faction)> factionTabs = new List<(Button, Faction?)>();
        private readonly Dictionary<CardController, CardDataScriptableObject> shownAssets = new Dictionary<CardController, CardDataScriptableObject>();

        /// <summary>The deck being built, or a starter deck being looked at; null when none is open.</summary>
        public DeckScriptableObject OpenDeck { get; private set; }
        /// <summary>The open deck is a starter deck: it can't be changed, only copied.</summary>
        public bool ShowsStarterDeck => OpenDeck != null && !PlayerDecks.IsPlayers(OpenDeck);
        /// <summary>The faction whose cards are offered; null when every card is.</summary>
        public Faction? ShownFaction { get; private set; }
        /// <summary>The cards offered, in order.</summary>
        public List<CardController> ShownCards => cards?.Cards ?? new List<CardController>();
        /// <summary>Your decks, as listed.</summary>
        public IReadOnlyList<DeckScriptableObject> Decks => DeckScriptableObject.InMenuOrder(PlayerDecks.All(Catalog)); // The most recently updated first
        public TMP_InputField NameField => nameField;

        #region Building decks
        /// <summary>A new, empty deck, opened, its name ready to be typed over.</summary>
        public void NewDeck() {
            Open(PlayerDecks.Create(Catalog));
            nameField.Select();
            nameField.ActivateInputField(); // Selects the whole name: typing replaces it
        }

        public void Open(DeckScriptableObject deck) {
            OpenDeck = PlayerDecks.IsPlayers(deck) || Catalog.Decks.Contains(deck) ? deck : null;
            Refresh();
        }

        /// <summary>Opens the deck this many entries down your list (up for negative steps), stopping at the ends.</summary>
        public void StepDeck(int steps) {
            if (Decks.Count > 0) Open(WheelStepper.Next(Decks, OpenDeck, steps));
        }

        /// <summary>Adds a copy of the card to the open deck, starting a new deck if none is open (a copy, if a starter deck is).</summary>
        public bool AddCard(CardDataScriptableObject card) {
            if (OpenDeck == null) OpenDeck = PlayerDecks.Create(Catalog);
            else if (ShowsStarterDeck) OpenDeck = PlayerDecks.Create(Catalog, OpenDeck.GetDeckName(), OpenDeck.Cards);
            bool added = PlayerDecks.Add(OpenDeck, card);
            Refresh();
            return added;
        }

        /// <summary>Takes a copy of the card out of the open deck.</summary>
        public bool RemoveCard(CardDataScriptableObject card) {
            bool removed = PlayerDecks.Remove(OpenDeck, card);
            Refresh();
            return removed;
        }

        /// <summary>A copy of the open deck, opened.</summary>
        public void CopyDeck() {
            if (OpenDeck != null) Open(PlayerDecks.Create(Catalog, OpenDeck.GetDeckName(), OpenDeck.Cards));
        }

        /// <summary>The first call asks to be sure; the second deletes the open deck and opens the next one.</summary>
        public void DeleteDeck() {
            if (OpenDeck == null) return;
            if (!deleteArmed) {
                deleteArmed = true;
                ShowDeleteButton();
                return;
            }
            int index = Decks.ToList().IndexOf(OpenDeck);
            PlayerDecks.Delete(OpenDeck);
            Open(Decks.Count > 0 ? Decks[Mathf.Clamp(index, 0, Decks.Count - 1)] : null);
        }

        // Typing a name with no deck open starts a new deck with that name
        private void Rename(string name) {
            if (OpenDeck == null && !string.IsNullOrWhiteSpace(name)) OpenDeck = PlayerDecks.Create(Catalog, name);
            else PlayerDecks.Rename(OpenDeck, name);
            Refresh();
        }
        #endregion

        #region Showing
        /// <summary>Offers the faction's cards; null offers every card.</summary>
        public void ShowFaction(Faction? faction) {
            ShownFaction = faction;
            var offered = CardGrid.CardsOf(Catalog, faction);
            shownAssets.Clear();
            cards.Show(offered, (face, asset) => {
                shownAssets[face] = asset;
                face.OnCardClicked.AddListener(_ => AddCard(asset));
                face.OnCardRightClicked.AddListener(_ => RemoveCard(asset));
            });
            string title = faction != null ? CardTextFormatter.FactionName(faction.Value) : "Every card";
            cards.Title.text = $"{title}  <size=70%>{CardGrid.Count(offered)}</size>";
            foreach (var (tab, listed) in factionTabs) ShowChosen(tab, listed == faction);
            Refresh();
        }

        // Everything that shows the open deck: the list of decks, its name, how full it is, its cards, and the copies of each offered card
        private void Refresh() {
            if (Panel == null) return;
            deleteArmed = false; // Doing anything else takes back a first click on Delete
            RefreshDeckList();
            RefreshDeck();
            foreach (var (face, asset) in shownAssets) {
                int copies = PlayerDecks.CopiesOf(OpenDeck, asset);
                ShowCopies(face, copies, copies >= PlayerDecks.MaxCopies);
            }
        }

        private void RefreshDeckList() {
            foreach (var (button, _) in deckButtons) Discard(button.gameObject);
            deckButtons.Clear();
            if (starterHeading != null) Discard(starterHeading);
            foreach (var deck in Decks) {
                var button = AddChoice(deckListContent, DeckButton + deck.GetDeckName(), deck.GetDeckName(), PlayerDecks.SizeText(deck), () => Open(deck), DeckCatalog.CoinsFor(deck));
                ShowChosen(button, deck == OpenDeck);
                deckButtons.Add((button, deck));
            }
            // The starter decks, to look at and copy
            var heading = AddText(deckListContent, "Starter decks", 24, AccentColor, FontStyles.Bold | FontStyles.SmallCaps, height: 40);
            heading.alignment = TextAlignmentOptions.BottomLeft;
            starterHeading = heading.gameObject;
            foreach (var deck in DeckScriptableObject.InMenuOrder(Catalog.Decks)) {
                var button = AddChoice(deckListContent, StarterDeckButton + deck.GetDeckName(), deck.GetDeckName(), $"{deck.Cards.Count} cards · copy it to make it yours",
                    () => Open(deck), DeckCatalog.CoinsFor(deck));
                ShowChosen(button, deck == OpenDeck);
                deckButtons.Add((button, deck));
            }
            var open = deckButtons.FirstOrDefault(b => b.deck == OpenDeck).button;
            if (open != null) KeepInView(deckList, (RectTransform)open.transform);
        }

        private void RefreshDeck() {
            bool open = OpenDeck != null;
            copyButton.interactable = open;
            deleteButton.interactable = nameField.interactable = open && !ShowsStarterDeck;
            nameField.SetTextWithoutNotify(open ? OpenDeck.GetDeckName() : "");
            ShowDeleteButton();

            string accent = ColorUtility.ToHtmlStringRGB(AccentColor);
            if (!open) countText.text = "<i>Name a deck or click a card to start one.</i>";
            else if (ShowsStarterDeck) countText.text = $"Starter deck, {OpenDeck.Cards.Count} cards · <i>Copy it to make it yours</i>";
            else if (PlayerDecks.IsComplete(OpenDeck)) countText.text = $"{PlayerDecks.SizeText(OpenDeck)} · <color=#{accent}>ready to play</color>" +
                (OpenDeck.Cards.Count < PlayerDecks.MaxDeckSize ? $" <i>(up to {PlayerDecks.MaxDeckSize})</i>" : "");
            else countText.text = $"{PlayerDecks.SizeText(OpenDeck)} · <i>{CardGrid.Plural(PlayerDecks.MinDeckSize - OpenDeck.Cards.Count, "more card")} to play</i>";
            foreach (Transform coin in deckFactions.Cast<Transform>().ToList()) Discard(coin.gameObject);
            if (open) {
                var coins = AddCoins(deckFactions, DeckCatalog.CoinsFor(OpenDeck), 30f, DeckWidth - 60f);
                coins.sizeDelta = new Vector2(DeckCatalog.CoinsFor(OpenDeck).Count * 34f, 30f); // Centered, side by side
            }

            cardPeek.Hide();
            foreach (Transform row in rows.Cast<Transform>().ToList()) Discard(row.gameObject);
            if (!open) return;
            var inDeck = OpenDeck.Cards.Distinct().OrderBy(c => c is CreatureCardScriptableObject ? 0 : 1).ThenBy(c => c.faction).ThenBy(c => c.cardName).ToList();
            foreach (var group in inDeck.GroupBy(c => c is CreatureCardScriptableObject)) {
                int count = group.Sum(c => PlayerDecks.CopiesOf(OpenDeck, c));
                var heading = AddText(rows, CardGrid.Plural(count, group.Key ? "Creature" : "Spell"), 24, AccentColor, FontStyles.Bold | FontStyles.SmallCaps, height: 34);
                heading.alignment = TextAlignmentOptions.BottomLeft;
                foreach (var card in group) AddCardRow(card);
            }
        }

        // A card in the open deck: its faction's emblem, its name and copies. Clicking takes a copy out; hovering shows the card
        private void AddCardRow(CardDataScriptableObject card) {
            var row = AddButton(rows, CardRow + card.cardName, card.cardName, -1, () => RemoveCard(card));
            row.GetComponent<LayoutElement>().preferredHeight = 40;
            var label = row.GetComponentInChildren<TextMeshProUGUI>();
            var coin = UITheme.AddFactionCoin(row.transform, card.faction, 26f).rectTransform;
            coin.anchorMin = coin.anchorMax = coin.pivot = new Vector2(0f, 0.5f);
            coin.anchoredPosition = new Vector2(8f, 0f);
            label.fontSize = 22;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.rectTransform.offsetMin = new Vector2(42f, 0f);
            label.rectTransform.offsetMax = new Vector2(-48f, 0f);
            var copies = AddText(row.transform, $"×{PlayerDecks.CopiesOf(OpenDeck, card)}", 22, AccentColor, FontStyles.Bold, height: -1);
            var copiesRect = copies.rectTransform;
            copiesRect.anchorMin = new Vector2(1f, 0f);
            copiesRect.anchorMax = Vector2.one;
            copiesRect.pivot = new Vector2(1f, 0.5f);
            copiesRect.sizeDelta = new Vector2(44f, 0f);
            copiesRect.anchoredPosition = new Vector2(-8f, 0f);
            CardPeekOnHover.AddTo(row.gameObject, cardPeek, card);
        }

        // A badge with the copies in the open deck at the card's corner; the card dims when no more can be added
        private static void ShowCopies(CardController face, int copies, bool atMost) {
            var cell = face.transform.parent;
            var badge = cell.Find("Copies");
            if (badge == null && copies > 0) {
                var go = NewObject("Copies", cell);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
                rect.sizeDelta = new Vector2(52f, 34f);
                rect.anchoredPosition = new Vector2(2f, 2f);
                UITheme.Round(go.AddComponent<Image>(), UITheme.Gold, 1f).raycastTarget = false;
                var text = AddText(go.transform, "", 24, UITheme.Ink, FontStyles.Bold, height: -1);
                Stretch(text.gameObject);
                badge = go.transform;
            }
            if (badge != null) {
                badge.gameObject.SetActive(copies > 0);
                badge.GetComponentInChildren<TextMeshProUGUI>().text = $"×{copies}";
                badge.SetAsLastSibling();
            }
            face.GetComponent<CanvasGroup>().alpha = atMost ? 0.5f : 1f;
        }

        private void ShowDeleteButton() {
            deleteButton.GetComponentInChildren<TextMeshProUGUI>().text = deleteArmed ? DeleteArmedLabel : DeleteLabel;
            deleteButton.image.color = deleteArmed ? UITheme.HealthLow : ButtonColor;
        }
        #endregion

        #region Building the screen
        protected override GameObject Build() {
            var screen = AddScreen(transform, "DeckBuilderPanel", "Deck Builder");
            var columns = screen.columns;
            BuildDeckList(columns);
            BuildCards(columns);
            BuildDeck(columns);
            AddButton(AddFooter(screen.panel.transform), BackButtonName, "Back", BackWidth, Hide);
            cardPeek = new CardPeek(screen.panel.transform, Catalog.CardPrefab);
            return screen.panel;
        }

        protected override void Opened() {
            OpenDeck = Decks.FirstOrDefault();
            ShowFaction(null);
        }

        protected override void Forget() {
            cardPeek?.Hide();
            cards = null;
            deckList = null;
            deckListContent = rows = null;
            nameField = null;
            countText = null;
            deckFactions = null;
            copyButton = deleteButton = null;
            starterHeading = null;
            cardPeek = null;
            deleteArmed = false;
            deckButtons.Clear();
            factionTabs.Clear();
            shownAssets.Clear();
            OpenDeck = null;
            ShownFaction = null;
        }

        // Your decks, with New deck first; the wheel goes through them
        private void BuildDeckList(Transform columns) {
            var column = AddColumn(columns, "Your decks");
            var element = column.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = DecksWidth;
            element.flexibleWidth = 0;
            WheelStepper.AddTo(column.gameObject, StepDeck);
            deckListContent = AddList(column, out deckList);
            AddChoice(deckListContent, NewDeckButton, "+ New deck", "An empty deck to fill", NewDeck);
        }

        // The cards on offer, with a tab for each faction above them
        private void BuildCards(Transform columns) {
            cards = new CardGrid(columns, Catalog.CardPrefab, CellSize, CardScale);
            var tabs = NewObject("Factions", cards.Area);
            tabs.transform.SetSiblingIndex(1); // Between the title and the cards
            var tabsLayout = tabs.AddComponent<VerticalLayoutGroup>();
            tabsLayout.spacing = 8;
            SetControls(tabsLayout, expandHeight: false);

            var factions = new List<Faction?> { null }.Concat(CardGrid.FactionsIn(Catalog).Select(f => (Faction?)f)).ToList();
            Transform row = null;
            for (int i = 0; i < factions.Count; i++) {
                if (i % TabsPerRow == 0) {
                    row = NewObject("Row", tabs.transform).transform;
                    var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                    rowLayout.spacing = 8;
                    SetControls(rowLayout, expandHeight: false);
                }
                var faction = factions[i];
                string label = faction != null ? CardTextFormatter.FactionName(faction.Value) : "Every card";
                string name = faction != null ? FactionTab + CardTextFormatter.FactionName(faction.Value) : EveryFactionTab;
                var tab = AddButton(row, name, label, -1, () => ShowFaction(faction));
                var tabElement = tab.GetComponent<LayoutElement>();
                tabElement.preferredHeight = 44;
                tabElement.flexibleWidth = 1;
                tabElement.preferredWidth = 0;
                var text = tab.GetComponentInChildren<TextMeshProUGUI>();
                text.enableAutoSizing = true;
                text.fontSizeMin = 14;
                text.fontSizeMax = 22;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.rectTransform.offsetMin = new Vector2(faction != null ? 40f : 8f, 4f);
                text.rectTransform.offsetMax = new Vector2(-8f, -4f);
                if (faction != null) {
                    var coin = UITheme.AddFactionCoin(tab.transform, faction.Value, 28f).rectTransform;
                    coin.anchorMin = coin.anchorMax = coin.pivot = new Vector2(0f, 0.5f);
                    coin.anchoredPosition = new Vector2(8f, 0f);
                }
                factionTabs.Add((tab, faction));
            }
        }

        // The open deck: its name, how full it is, its cards, and Copy and Delete
        private void BuildDeck(Transform columns) {
            var column = AddColumn(columns, null);
            var element = column.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = DeckWidth;
            element.flexibleWidth = 0;

            nameField = AddInputField(column, "DeckName", "Name your deck", 56f);
            nameField.characterLimit = PlayerDecks.MaxNameLength;
            nameField.onEndEdit.AddListener(Rename);
            deckFactions = NewObject("Factions", column).transform; // The open deck's factions, as coins: most cards first
            deckFactions.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
            countText = AddText(column, "", 22, TextColor, FontStyles.Normal, height: 36);
            countText.textWrappingMode = TextWrappingModes.Normal;
            countText.enableAutoSizing = true; // Three factions' names on two lines
            countText.fontSizeMin = 14;
            countText.fontSizeMax = 22;

            var list = NewObject("Cards", column);
            list.AddComponent<LayoutElement>().flexibleHeight = 1;
            var viewport = NewObject("Viewport", list.transform);
            Stretch(viewport);
            viewport.AddComponent<RectMask2D>();
            viewport.AddComponent<Image>().color = Color.clear;
            var content = NewObject("Content", viewport.transform);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.padding = new RectOffset(2, 2, 0, 8);
            SetControls(layout, expandHeight: false);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = list.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            WheelScroll.AddTo(scroll);
            rows = content.transform;

            var hint = AddText(column, "Click a card to add a copy (two at most, 20 to 40 cards); click it here, or right-click it, to take one out.", 18, UITheme.Hex(0xC9C0AE), FontStyles.Italic, height: 48);
            hint.textWrappingMode = TextWrappingModes.Normal;

            var buttons = NewObject("Buttons", column);
            var buttonsLayout = buttons.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = 12;
            SetControls(buttonsLayout, expandHeight: true);
            var buttonsElement = buttons.AddComponent<LayoutElement>();
            buttonsElement.minHeight = buttonsElement.preferredHeight = 56;
            buttonsElement.flexibleHeight = 0; // The deck's cards take the height left
            copyButton = AddButton(buttons.transform, CopyButtonName, "Copy", -1, CopyDeck);
            deleteButton = AddButton(buttons.transform, DeleteButtonName, DeleteLabel, -1, DeleteDeck);
        }
        #endregion

        // Gone from the screen at once (so it's never found again), destroyed at the end of the frame
        private static void Discard(GameObject go) {
            go.SetActive(false);
            go.transform.SetParent(null, false);
            Destroy(go);
        }
    }
}
