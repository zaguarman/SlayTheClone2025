using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.MenuWidgets;

namespace SlayTheClone {
    /// <summary>
    /// The card gallery, opened from the title screen: every card on offer, deck by deck (or all of them at once), shown as the
    /// printed cards. Hover a card to see it enlarged. Built from the <see cref="DeckCatalog"/>, like the match setup.
    /// </summary>
    public class CardGalleryMenu : MonoBehaviour {
        public const string EveryCardButton = "Gallery: Every card";
        public const string DeckButton = "Gallery: ";
        public const string BackButtonName = "GalleryBackButton";
        private const float CardScale = 1.25f;
        private static readonly Vector2 CellSize = new Vector2(215f, 268f);

        private DeckCatalog catalog;
        private GameObject titleScreen;
        private CardViews cardViews;
        private GameObject panel;
        private TextMeshProUGUI shownTitle;
        private Transform grid;
        private ScrollRect scroll;
        private readonly List<(Button button, DeckScriptableObject deck)> deckButtons = new List<(Button, DeckScriptableObject)>();

        public bool IsOpen => panel != null && panel.activeSelf;
        /// <summary>The deck being shown; null when every card is.</summary>
        public DeckScriptableObject ShownDeck { get; private set; }
        /// <summary>The cards shown, in order.</summary>
        public List<CardController> ShownCards { get; } = new List<CardController>();

        public void Setup(DeckCatalog deckCatalog, GameObject title) {
            catalog = deckCatalog;
            titleScreen = title;
        }

        public void Show() {
            if (catalog == null || catalog.CardPrefab == null) {
                Debug.LogWarning("The card gallery needs a deck catalog with a card prefab (Cards > Generate Faction Decks sets it)");
                return;
            }
            if (panel == null) Build();
            if (titleScreen != null) titleScreen.SetActive(false);
            panel.SetActive(true);
            ShowDeck(ShownDeck);
        }

        public void Hide() {
            Tooltip.Existing?.HideTooltip();
            if (panel != null) panel.SetActive(false);
            if (titleScreen != null) titleScreen.SetActive(true);
        }

        /// <summary>Shows the deck's cards, each different card once; null shows every card on offer.</summary>
        public void ShowDeck(DeckScriptableObject deck) {
            ShownDeck = deck;
            Tooltip.Existing?.HideTooltip();
            foreach (var card in ShownCards) Destroy(card.transform.parent.gameObject);
            ShownCards.Clear();

            var cards = deck != null ? deck.Cards.Where(c => c != null).Distinct().ToList() : catalog.AllCards.ToList();
            foreach (var data in cards) {
                var cell = NewObject(data.cardName, grid);
                var face = cardViews.Create(CardFactory.CreateCard(data.ToCardData()), null, cell.transform);
                if (face == null) continue;
                var rect = (RectTransform)face.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one * CardScale;
                ShownCards.Add(face);
            }

            shownTitle.text = deck != null ? $"{deck.GetDeckName()}  <size=70%>{DeckCatalog.Describe(deck)}</size>"
                : $"Every card  <size=70%>{cards.Count} cards in {catalog.AllDecks.Count} decks</size>";
            foreach (var (button, shown) in deckButtons) ShowChosen(button, shown == deck);
            scroll.verticalNormalizedPosition = 1f;
        }

        private void Build() {
            cardViews = CardViews.ForDisplay(catalog.CardPrefab);

            panel = NewObject("CardGalleryPanel", transform);
            Stretch(panel);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(60, 60, 40, 40);
            layout.spacing = 20;
            SetControls(layout, expandHeight: false);
            UITheme.StyleHeading(AddText(panel.transform, "Card Gallery", 64, AccentColor, FontStyles.Bold, height: 80), AccentColor);

            var columns = NewObject("Columns", panel.transform);
            var columnsLayout = columns.AddComponent<HorizontalLayoutGroup>();
            columnsLayout.spacing = 30;
            columnsLayout.childControlWidth = columnsLayout.childControlHeight = true;
            columnsLayout.childForceExpandWidth = false;
            columnsLayout.childForceExpandHeight = true;
            columns.AddComponent<LayoutElement>().flexibleHeight = 1;

            var decks = AddColumn(columns.transform, "Decks");
            var decksElement = decks.gameObject.AddComponent<LayoutElement>();
            decksElement.preferredWidth = 470;
            decksElement.flexibleWidth = 0;
            var every = AddButton(decks, EveryCardButton, "<b>Every card</b>", -1, () => ShowDeck(null));
            every.GetComponent<LayoutElement>().preferredHeight = 60;
            deckButtons.Add((every, null));
            foreach (var deck in catalog.AllDecks) {
                var button = AddButton(decks, DeckButton + deck.GetDeckName(),
                    $"<b>{deck.GetDeckName()}</b>\n<size=70%>{DeckCatalog.Describe(deck)}</size>", -1, () => ShowDeck(deck));
                button.GetComponent<LayoutElement>().preferredHeight = 72;
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16;
                label.fontSizeMax = 26;
                deckButtons.Add((button, deck));
            }

            BuildCardsArea(columns.transform);

            var footer = NewObject("Footer", panel.transform);
            var footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
            footerLayout.childAlignment = TextAnchor.MiddleCenter;
            footerLayout.childControlWidth = footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = footerLayout.childForceExpandHeight = false;
            footer.AddComponent<LayoutElement>().preferredHeight = 72;
            AddButton(footer.transform, BackButtonName, "Back", 260, Hide);
        }

        // A scrolling grid of cards, with the name of what's shown above it
        private void BuildCardsArea(Transform parent) {
            var area = NewObject("Cards", parent);
            UITheme.StylePanel(area.AddComponent<Image>(), PanelColor);
            area.AddComponent<LayoutElement>().flexibleWidth = 1;
            var areaLayout = area.AddComponent<VerticalLayoutGroup>();
            areaLayout.padding = new RectOffset(20, 20, 14, 20);
            areaLayout.spacing = 8;
            SetControls(areaLayout, expandHeight: false);

            shownTitle = AddText(area.transform, "", 36, TextColor, FontStyles.Bold, height: 52);

            var viewport = NewObject("Viewport", area.transform);
            viewport.AddComponent<RectMask2D>();
            viewport.AddComponent<Image>().color = Color.clear; // Catches the scroll wheel between cards
            viewport.AddComponent<LayoutElement>().flexibleHeight = 1;

            var content = NewObject("Grid", viewport.transform);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            var gridLayout = content.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = CellSize;
            gridLayout.spacing = new Vector2(12f, 14f);
            gridLayout.padding = new RectOffset(8, 8, 8, 8);
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            grid = content.transform;

            scroll = area.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
        }
    }
}
