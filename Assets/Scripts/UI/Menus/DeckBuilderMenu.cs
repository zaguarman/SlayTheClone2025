using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Zeitgeist.Enums;
using static Zeitgeist.MenuWidgets;

namespace Zeitgeist {
    /// <summary>
    /// The deck builder, opened from the title screen: your own decks, mixing any cards on offer (see <see cref="PlayerDecks"/>).
    /// On the left, your decks and New deck (which asks for its name at once), then the starter decks: open one to see its
    /// cards, and Copy it (or click a card) to make a deck of your own from it; in the middle, the cards, faction by faction:
    /// click one to add a copy to the open deck, right-click it to take one out; on the right, the open deck: its name (type to
    /// rename it, 20 characters at most), how full it is, and its cards: a click on one finds it in the middle, a right click
    /// takes a copy out, hovering it shows the card, whose keywords can be read and which can be right-clicked too (the
    /// menus' key language, the user, 2026-10-17: a left click decides, a right click takes back, Escape goes back). Every
    /// change is saved at once. Clicking a card with no deck open starts a new one. Once a deck has 20 cards (40 at most),
    /// the match setup offers it.
    /// </summary>
    public class DeckBuilderMenu : MenuScreen {
        public const string NewDeckButton = "Decks: + New deck";
        public const string DeckButton = "Decks: ";
        public const string StarterDeckButton = "Decks: Starter: ";
        public const string EveryFactionTab = "DeckFaction: Every card";
        public const string FactionTab = "DeckFaction: ";
        public const string UmbrellaTab = "DeckFactionGroup: ";
        public const string TierTab = "DeckTier: ";
        public const string CardRow = "DeckCard: ";
        public const string CopyButtonName = "DeckCopyButton";
        public const string DeleteButtonName = "DeckDeleteButton";
        public const string BackButtonName = "DecksBackButton";
        private const int CardColumnCount = 4; // Big enough to read at a glance
        private const float DecksWidth = 340f, DeckWidth = 400f;
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
        private readonly List<(Button button, Umbrella? umbrella)> umbrellaTabs = new List<(Button, Umbrella?)>();
        private readonly List<(Button button, Faction faction)> factionTabs = new List<(Button, Faction)>();
        private readonly List<(Button button, string tier)> tierTabs = new List<(Button, string)>();
        private readonly Dictionary<CardController, CardDataScriptableObject> shownAssets = new Dictionary<CardController, CardDataScriptableObject>();

        /// <summary>The deck being built, or a starter deck being looked at; null when none is open.</summary>
        public DeckScriptableObject OpenDeck { get; private set; }
        /// <summary>The open deck is a starter deck: it can't be changed, only copied.</summary>
        public bool ShowsStarterDeck => OpenDeck != null && !PlayerDecks.IsPlayers(OpenDeck);
        /// <summary>The faction whose cards are offered; null when every card is.</summary>
        public Faction? ShownFaction { get; private set; }
        /// <summary>The faction (one of the four) whose cards are offered, whole or narrowed to <see cref="ShownFaction"/>; null when every card is.</summary>
        public Umbrella? ShownUmbrella { get; private set; }
        /// <summary>The tier offered, "S" to "D" (the latest balance study's, see <see cref="CardScores"/>); null: any.</summary>
        public string ShownTier { get; private set; }
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
            ShownUmbrella = faction != null ? Umbrellas.Of(faction.Value) : (Umbrella?)null;
            Show(CardGrid.CardsOf(Catalog, faction), faction != null ? CardTextFormatter.FactionName(faction.Value) : "Every card");
        }

        /// <summary>Offers a faction's cards, all its subfactions (the user, 2026-10-17); null offers every card.</summary>
        public void ShowUmbrella(Umbrella? umbrella) {
            if (umbrella == null) { ShowFaction(null); return; }
            ShownFaction = null;
            ShownUmbrella = umbrella;
            Show(CardGrid.CardsOf(Catalog, umbrella.Value), Umbrellas.Name(umbrella.Value));
        }

        private void Show(List<CardDataScriptableObject> offered, string title) {
            if (ShownTier != null) offered = offered.Where(c => CardScores.TierOf(c.cardName) == ShownTier).ToList();
            shownAssets.Clear();
            cards.Show(offered, (face, asset) => {
                shownAssets[face] = asset;
                face.ShowsItself = true; // Big enough to read as they are (the user): no enlarged copy on hover, its keywords explain themselves on it
                face.OnCardClicked.AddListener(_ => AddCard(asset));
                face.OnCardRightClicked.AddListener(_ => RemoveCard(asset));
            });
            cards.Title.text = $"{title}{(ShownTier != null ? $", Tier {ShownTier}" : "")}  <size=70%>{CardGrid.Count(offered)}</size>";
            foreach (var (tab, listed) in umbrellaTabs) ShowChosen(tab, listed == ShownUmbrella);
            // The chosen faction's subfactions; every card: all of them
            foreach (var (tab, listed) in factionTabs) {
                tab.gameObject.SetActive(ShownUmbrella == null || Umbrellas.Of(listed) == ShownUmbrella);
                ShowChosen(tab, listed == ShownFaction);
            }
            foreach (var (tab, listed) in tierTabs) ShowChosen(tab, listed == ShownTier);
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
                ShowCopies(face, copies, OpenDeck != null && OpenDeck.Cards.Count >= PlayerDecks.MaxDeckSize);
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
            // The deck's own name on the title line, its factions and size under it (the user, 2026-10-17: the rows were cut and wrapping)
            foreach (var deck in DeckScriptableObject.InMenuOrder(Catalog.Decks)) {
                string factions = string.Join(" + ", DeckCatalog.FactionsOf(deck).Select(CardTextFormatter.FactionName));
                var button = AddChoice(deckListContent, StarterDeckButton + deck.GetDeckName(), DeckScriptableObject.ShortName(deck.GetDeckName()),
                    $"{factions} · {deck.Cards.Count} cards", () => Open(deck), DeckCatalog.CoinsFor(deck));
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

        /// <summary>Shows the card among its subfaction's in the middle, scrolled into view (the user, 2026-10-17: to see the cards around it).</summary>
        public void ShowCard(CardDataScriptableObject card) {
            if (card == null) return;
            if (ShownFaction != card.faction || (ShownTier != null && CardScores.TierOf(card.cardName) != ShownTier)) {
                ShownTier = null;
                ShowFaction(card.faction);
            }
            cards.KeepInView(shownAssets.FirstOrDefault(p => p.Value == card).Key);
        }

        // A card in the open deck: its faction's emblem, its name and copies. A click finds it in the middle, a right click
        // takes a copy out (the menus' key language: left decides, right takes back); hovering shows the card
        private void AddCardRow(CardDataScriptableObject card) {
            var row = AddButton(rows, CardRow + card.cardName, card.cardName, -1, () => ShowCard(card));
            OnRightClick(row.gameObject, () => RemoveCard(card));
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
            cardPeek = new CardPeek(screen.panel.transform, Catalog.CardPrefab) { Readable = true }; // Its keywords can be read on it
            // A right click on the shown card takes a copy out; the card stays up while there are copies left
            cardPeek.RightClicked = card => {
                var at = cardPeek.ShownAt;
                RemoveCard(card);
                if (PlayerDecks.CopiesOf(OpenDeck, card) > 0) cardPeek.Show(card, at);
            };
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
            umbrellaTabs.Clear();
            factionTabs.Clear();
            tierTabs.Clear(); // Rebuilt with the screen
            shownAssets.Clear();
            OpenDeck = null;
            ShownFaction = null;
            ShownUmbrella = null;
        }

        // Your decks, with New deck first
        private void BuildDeckList(Transform columns) {
            var column = AddColumn(columns, "Your decks");
            var element = column.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = DecksWidth;
            element.flexibleWidth = 0;
            deckListContent = AddList(column, out deckList);
            AddChoice(deckListContent, NewDeckButton, "+ New deck", "An empty deck to fill", NewDeck);
        }

        // The cards on offer, with a tab for each faction above them
        private void BuildCards(Transform columns) {
            cards = new CardGrid(columns, Catalog.CardPrefab, CardColumnCount);
            var tabs = NewObject("Factions", cards.Area);
            tabs.transform.SetSiblingIndex(1); // Between the title and the cards
            var tabsLayout = tabs.AddComponent<VerticalLayoutGroup>();
            tabsLayout.spacing = 8;
            SetControls(tabsLayout, expandHeight: false);

            // The four factions in a row (Every card first), their subfactions in the row under it: the chosen faction's, or
            // all of them under Every card (the user, 2026-10-17)
            var umbrellaRow = AddTabRow(tabs.transform);
            foreach (var umbrella in new List<Umbrella?> { null }.Concat(CardGrid.UmbrellasIn(Catalog).Select(u => (Umbrella?)u))) {
                string label = umbrella != null ? Umbrellas.Name(umbrella.Value) : "Every card";
                var tab = AddTab(umbrellaRow, umbrella != null ? UmbrellaTab + label : EveryFactionTab, label, () => ShowUmbrella(umbrella), umbrella != null);
                if (umbrella != null) PlaceCoin(UITheme.AddUmbrellaCoin(tab.transform, umbrella.Value, 28f).rectTransform);
                umbrellaTabs.Add((tab, umbrella));
            }
            var factionRow = AddTabRow(tabs.transform);
            foreach (var faction in CardGrid.FactionsIn(Catalog)) {
                string label = CardTextFormatter.FactionName(faction);
                var tab = AddTab(factionRow, FactionTab + label, label, () => ShowFaction(faction), true);
                PlaceCoin(UITheme.AddFactionCoin(tab.transform, faction, 28f).rectTransform);
                factionTabs.Add((tab, faction));
            }

            // The tiers of the latest balance study (the user, 2026-10-17): any, then S to D. Without a study yet, none
            if (!CardScores.HasScores) return;
            var tierRow = NewObject("Tiers", tabs.transform).transform;
            var tierLayout = tierRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tierLayout.spacing = 8;
            SetControls(tierLayout, expandHeight: false);
            foreach (var tier in new string[] { null }.Concat(CardScores.Tiers)) {
                var tab = AddButton(tierRow, TierTab + (tier ?? "Any"), tier != null ? $"Tier {tier}" : "Any tier", -1, () => ShowTier(tier));
                var tabElement = tab.GetComponent<LayoutElement>();
                tabElement.preferredHeight = 38;
                tabElement.flexibleWidth = 1;
                tabElement.preferredWidth = 0;
                tierTabs.Add((tab, tier));
            }
        }

        private static Transform AddTabRow(Transform tabs) {
            var row = NewObject("Row", tabs).transform;
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8;
            SetControls(rowLayout, expandHeight: false);
            return row;
        }

        private static Button AddTab(Transform row, string name, string label, System.Action onClick, bool withCoin) {
            var tab = AddButton(row, name, label, -1, onClick);
            var tabElement = tab.GetComponent<LayoutElement>();
            tabElement.preferredHeight = 44;
            tabElement.flexibleWidth = 1;
            tabElement.preferredWidth = 0;
            var text = tab.GetComponentInChildren<TextMeshProUGUI>();
            text.enableAutoSizing = true;
            text.fontSizeMin = 14;
            text.fontSizeMax = 22;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.offsetMin = new Vector2(withCoin ? 40f : 8f, 4f);
            text.rectTransform.offsetMax = new Vector2(-8f, -4f);
            return tab;
        }

        private static void PlaceCoin(RectTransform coin) {
            coin.anchorMin = coin.anchorMax = coin.pivot = new Vector2(0f, 0.5f);
            coin.anchoredPosition = new Vector2(8f, 0f);
        }

        /// <summary>Offers only the cards of this tier in the latest balance study (null: any), in the faction shown.</summary>
        public void ShowTier(string tier) {
            ShownTier = tier;
            if (ShownFaction == null && ShownUmbrella != null) ShowUmbrella(ShownUmbrella);
            else ShowFaction(ShownFaction);
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

            var hint = AddText(column, "Click a card in the middle to add a copy, right-click it to take one out. Here, click a card to find it among its subfaction's; right-click it, or the card shown, to take one out.", 18, UITheme.Hex(0xC9C0AE), FontStyles.Italic, height: 48);
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
