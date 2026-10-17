using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static Zeitgeist.Enums;
using static Zeitgeist.MenuWidgets;

namespace Zeitgeist {
    /// <summary>
    /// The card gallery, opened from the title screen: every card on offer, faction by faction (each of the four with its
    /// subfactions under it, or all of them at once), shown as the printed cards; creatures first, then spells. The cards
    /// are big enough to read as they are: hovered, one shows only its explanations beside it, no enlarged copy (the user,
    /// 2026-10-17). With a balance study's scores at hand, a row of tier tabs (Any, S to D) filters them, as the deck
    /// builder's does. Built from the <see cref="DeckCatalog"/>. The mouse wheel scrolls the faction list or the cards,
    /// whichever it's over.
    /// </summary>
    public class CardGalleryMenu : MenuScreen {
        public const string EveryCardButton = "Gallery: Every card";
        public const string UmbrellaButton = "Gallery faction: ";
        public const string FactionButton = "Gallery: ";
        public const string TierTab = "GalleryTier: ";
        public const string BackButtonName = "GalleryBackButton";
        private const int CardColumnCount = 5; // Big enough to read at a glance
        public const float SideColumnWidth = 470f; // The list beside the main area, on the gallery and the glossary alike
        private const float SubfactionIndent = 28f; // A subfaction's entry stands in from its faction's

        private CardGrid cards;
        private ScrollRect factionList;
        private readonly List<(Button button, Umbrella umbrella)> umbrellaButtons = new List<(Button, Umbrella)>();
        private readonly List<(Button button, Faction? faction)> factionButtons = new List<(Button, Faction?)>();
        private readonly List<(Button button, string tier)> tierTabs = new List<(Button, string)>();

        /// <summary>The faction (one of the four) whose cards are shown, alone or narrowed to a subfaction; null when every card is.</summary>
        public Umbrella? ShownUmbrella { get; private set; }
        /// <summary>The subfaction whose cards are shown; null when a whole faction or every card is.</summary>
        public Faction? ShownFaction { get; private set; }
        /// <summary>The tier shown (S to D, from the latest balance study); null when every tier is.</summary>
        public string ShownTier { get; private set; }
        /// <summary>The cards shown, in order.</summary>
        public List<CardController> ShownCards => cards?.Cards ?? new List<CardController>();
        /// <summary>The subfactions listed, in order, under their factions.</summary>
        public IReadOnlyList<Faction> Factions => factionButtons.Where(b => b.faction != null).Select(b => b.faction.Value).ToList();

        /// <summary>Shows the subfaction's cards; null shows every card on offer.</summary>
        public void ShowFaction(Faction? faction) {
            ShownFaction = faction;
            ShownUmbrella = faction != null ? Umbrellas.Of(faction.Value) : (Umbrella?)null;
            Show(CardGrid.CardsOf(Catalog, faction), faction != null ? CardTextFormatter.FactionName(faction.Value) : null);
        }

        /// <summary>Shows a faction's cards, all its subfactions.</summary>
        public void ShowUmbrella(Umbrella umbrella) {
            ShownFaction = null;
            ShownUmbrella = umbrella;
            Show(CardGrid.CardsOf(Catalog, umbrella), Umbrellas.Name(umbrella));
        }

        private void Show(List<CardDataScriptableObject> shown, string title) {
            if (ShownTier != null) shown = shown.Where(c => CardScores.TierOf(c.cardName) == ShownTier).ToList();
            cards.Show(shown, (face, _) => face.ShowsItself = true); // Big enough as they are: no enlarged copy on hover
            string tier = ShownTier != null ? $", Tier {ShownTier}" : "";
            cards.Title.text = title != null ? $"{title}{tier}  <size=70%>{CardGrid.Count(shown)}</size>"
                : $"Every card{tier}  <size=70%>{shown.Count} cards in {umbrellaButtons.Count} factions</size>";
            foreach (var (button, listed) in umbrellaButtons) ShowChosen(button, ShownFaction == null && listed == ShownUmbrella);
            foreach (var (button, listed) in factionButtons) {
                bool chosen = listed == ShownFaction && (listed != null || ShownUmbrella == null);
                ShowChosen(button, chosen);
                if (chosen) KeepInView(factionList, (RectTransform)button.transform);
            }
            foreach (var (tab, listed) in tierTabs) ShowChosen(tab, listed == ShownTier);
        }

        /// <summary>Shows only the cards of this tier in the latest balance study (null: any), in the faction shown.</summary>
        public void ShowTier(string tier) {
            ShownTier = tier;
            if (ShownFaction == null && ShownUmbrella != null) ShowUmbrella(ShownUmbrella.Value);
            else ShowFaction(ShownFaction);
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
            // Each of the four factions, its subfactions listed under it (the user, 2026-10-17)
            foreach (var umbrella in CardGrid.UmbrellasIn(Catalog)) {
                string umbrellaName = Umbrellas.Name(umbrella);
                var button = AddUmbrellaChoice(list, UmbrellaButton + umbrellaName, umbrellaName, CardGrid.Count(CardGrid.CardsOf(Catalog, umbrella)), () => ShowUmbrella(umbrella), umbrella);
                umbrellaButtons.Add((button, umbrella));
                foreach (var faction in CardGrid.FactionsIn(Catalog).Where(f => Umbrellas.Of(f) == umbrella)) {
                    string name = CardTextFormatter.FactionName(faction);
                    var indented = NewObject("Subfaction", list);
                    var indent = indented.AddComponent<HorizontalLayoutGroup>();
                    indent.padding.left = (int)SubfactionIndent;
                    SetControls(indent, expandHeight: false);
                    var entry = AddChoice(indented.transform, FactionButton + name, name, CardGrid.Count(CardGrid.CardsOf(Catalog, faction)), () => ShowFaction(faction), new[] { faction }, CoinSize);
                    factionButtons.Add((entry, faction));
                }
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
            umbrellaButtons.Clear();
            factionButtons.Clear();
            tierTabs.Clear();
            ShownUmbrella = null;
            ShownFaction = null;
            ShownTier = null;
        }
    }
}
