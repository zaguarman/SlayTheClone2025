using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.Enums;
using static SlayTheClone.MenuWidgets;

namespace SlayTheClone {
    /// <summary>
    /// The card gallery, opened from the title screen: every card on offer, faction by faction (or all of them at once), shown
    /// as the printed cards; creatures first, then spells. The cards are big enough to read as they are: hovered, one shows
    /// only its explanations beside it, no enlarged copy (the user, 2026-10-17). With a balance study's scores at hand, a row
    /// of tier tabs (Any, S to D) filters them, as the deck builder's does. Built from the <see cref="DeckCatalog"/>.
    /// The mouse wheel scrolls the faction list or the cards, whichever it's over.
    /// </summary>
    public class CardGalleryMenu : MenuScreen {
        public const string EveryCardButton = "Gallery: Every card";
        public const string FactionButton = "Gallery: ";
        public const string TierTab = "GalleryTier: ";
        public const string BackButtonName = "GalleryBackButton";
        private const int CardColumnCount = 5; // Big enough to read at a glance
        public const float SideColumnWidth = 470f; // The list beside the main area, on the gallery and the glossary alike

        private CardGrid cards;
        private ScrollRect factionList;
        private readonly List<(Button button, Faction? faction)> factionButtons = new List<(Button, Faction?)>();
        private readonly List<(Button button, string tier)> tierTabs = new List<(Button, string)>();

        /// <summary>The faction whose cards are shown; null when every card is.</summary>
        public Faction? ShownFaction { get; private set; }
        /// <summary>The tier shown (S to D, from the latest balance study); null when every tier is.</summary>
        public string ShownTier { get; private set; }
        /// <summary>The cards shown, in order.</summary>
        public List<CardController> ShownCards => cards?.Cards ?? new List<CardController>();
        /// <summary>The factions listed, in order, after Every card.</summary>
        public IReadOnlyList<Faction> Factions => factionButtons.Where(b => b.faction != null).Select(b => b.faction.Value).ToList();

        /// <summary>Shows the faction's cards; null shows every card on offer.</summary>
        public void ShowFaction(Faction? faction) {
            ShownFaction = faction;
            var shown = CardGrid.CardsOf(Catalog, faction);
            if (ShownTier != null) shown = shown.Where(c => CardScores.TierOf(c.cardName) == ShownTier).ToList();
            cards.Show(shown, (face, _) => face.ShowsItself = true); // Big enough as they are: no enlarged copy on hover
            string tier = ShownTier != null ? $", Tier {ShownTier}" : "";
            cards.Title.text = faction != null ? $"{CardTextFormatter.FactionName(faction.Value)}{tier}  <size=70%>{CardGrid.Count(shown)}</size>"
                : $"Every card{tier}  <size=70%>{shown.Count} cards in {Factions.Count} factions</size>";
            foreach (var (button, listed) in factionButtons) {
                ShowChosen(button, listed == faction);
                if (listed == faction) KeepInView(factionList, (RectTransform)button.transform);
            }
            foreach (var (tab, listed) in tierTabs) ShowChosen(tab, listed == ShownTier);
        }

        /// <summary>Shows only the cards of this tier in the latest balance study (null: any), in the faction shown.</summary>
        public void ShowTier(string tier) {
            ShownTier = tier;
            ShowFaction(ShownFaction);
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
            BuildTierTabs();

            AddButton(AddFooter(screen.panel.transform), BackButtonName, "Back", BackWidth, Hide);
            return screen.panel;
        }

        // The tiers of the latest balance study (the user, 2026-10-17): Any, then S to D, between the title and the cards.
        // Without a study yet, no row
        private void BuildTierTabs() {
            if (!CardScores.HasScores) return;
            var row = NewObject("Tiers", cards.Area);
            row.transform.SetSiblingIndex(1);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            SetControls(layout, expandHeight: false);
            foreach (var tier in new string[] { null }.Concat(CardScores.Tiers)) {
                var tab = AddButton(row.transform, TierTab + (tier ?? "Any"), tier != null ? $"Tier {tier}" : "Any tier", -1, () => ShowTier(tier));
                var element = tab.GetComponent<LayoutElement>();
                element.preferredHeight = 38;
                element.flexibleWidth = 1;
                element.preferredWidth = 0;
                tierTabs.Add((tab, tier));
            }
        }

        protected override void Opened() => ShowFaction(null);

        protected override void Forget() {
            cards = null;
            factionList = null;
            factionButtons.Clear();
            tierTabs.Clear();
            ShownFaction = null;
            ShownTier = null;
        }
    }
}
