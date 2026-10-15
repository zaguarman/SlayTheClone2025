using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.Enums;
using static SlayTheClone.MenuWidgets;

namespace SlayTheClone {
    /// <summary>
    /// The card gallery, opened from the title screen: every card on offer, faction by faction (or all of them at once), shown
    /// as the printed cards; creatures first, then spells. Hover a card to see it enlarged. Built from the <see cref="DeckCatalog"/>.
    /// The mouse wheel scrolls the faction list or the cards, whichever it's over.
    /// </summary>
    public class CardGalleryMenu : MenuScreen {
        public const string EveryCardButton = "Gallery: Every card";
        public const string FactionButton = "Gallery: ";
        public const string BackButtonName = "GalleryBackButton";
        private const int CardColumnCount = 5; // Big enough to read at a glance
        public const float SideColumnWidth = 470f; // The list beside the main area, on the gallery and the glossary alike

        private CardGrid cards;
        private ScrollRect factionList;
        private readonly List<(Button button, Faction? faction)> factionButtons = new List<(Button, Faction?)>();

        /// <summary>The faction whose cards are shown; null when every card is.</summary>
        public Faction? ShownFaction { get; private set; }
        /// <summary>The cards shown, in order.</summary>
        public List<CardController> ShownCards => cards?.Cards ?? new List<CardController>();
        /// <summary>The factions listed, in order, after Every card.</summary>
        public IReadOnlyList<Faction> Factions => factionButtons.Where(b => b.faction != null).Select(b => b.faction.Value).ToList();

        /// <summary>Shows the faction's cards; null shows every card on offer.</summary>
        public void ShowFaction(Faction? faction) {
            ShownFaction = faction;
            var shown = CardGrid.CardsOf(Catalog, faction);
            cards.Show(shown);
            cards.Title.text = faction != null ? $"{CardTextFormatter.FactionName(faction.Value)}  <size=70%>{CardGrid.Count(shown)}</size>"
                : $"Every card  <size=70%>{shown.Count} cards in {Factions.Count} factions</size>";
            foreach (var (button, listed) in factionButtons) {
                ShowChosen(button, listed == faction);
                if (listed == faction) KeepInView(factionList, (RectTransform)button.transform);
            }
        }

        protected override GameObject Build() {
            var screen = AddScreen(transform, "CardGalleryPanel", "Card Gallery");
            var columns = screen.columns;

            var factions = AddColumn(columns, "Factions");
            var factionsElement = factions.gameObject.AddComponent<LayoutElement>();
            factionsElement.minWidth = factionsElement.preferredWidth = SideColumnWidth;
            factionsElement.flexibleWidth = 0;
            var list = AddList(factions, out factionList);
            var every = AddChoice(list, EveryCardButton, "Every card", $"{Catalog.AllCards.Count} cards", () => ShowFaction(null), new List<Faction>(), CoinSize);
            factionButtons.Add((every, null));
            foreach (var faction in CardGrid.FactionsIn(Catalog)) {
                string name = CardTextFormatter.FactionName(faction);
                var button = AddChoice(list, FactionButton + name, name, CardGrid.Count(CardGrid.CardsOf(Catalog, faction)), () => ShowFaction(faction), new[] { faction }, CoinSize);
                factionButtons.Add((button, faction));
            }

            cards = new CardGrid(columns, Catalog.CardPrefab, CardColumnCount);

            AddButton(AddFooter(screen.panel.transform), BackButtonName, "Back", BackWidth, Hide);
            return screen.panel;
        }

        protected override void Opened() => ShowFaction(null);

        protected override void Forget() {
            cards = null;
            factionList = null;
            factionButtons.Clear();
            ShownFaction = null;
        }
    }
}
