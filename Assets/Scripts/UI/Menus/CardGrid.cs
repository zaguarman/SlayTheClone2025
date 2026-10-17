using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Zeitgeist.Enums;
using static Zeitgeist.MenuWidgets;

namespace Zeitgeist {
    /// <summary>
    /// A panel of printed cards in a scrolling grid, with a title above them: the card gallery's and the deck builder's.
    /// The cards fill the width in a set number of columns, as big as that allows (see <see cref="CardColumns"/>). Hover a
    /// card to see it enlarged; the mouse wheel over the grid scrolls it.
    /// </summary>
    public class CardGrid {
        private readonly CardViews cardViews;
        private readonly CardColumns columns;
        private readonly Transform grid;
        private readonly ScrollRect scroll;

        /// <summary>The panel: the title, then the grid. Something can go between them (the deck builder's faction tabs).</summary>
        public Transform Area { get; }
        public TextMeshProUGUI Title { get; }
        /// <summary>The cards shown, in order.</summary>
        public List<CardController> Cards { get; } = new List<CardController>();

        public CardGrid(Transform parent, CardController cardPrefab, int columnCount) {
            cardViews = CardViews.ForDisplay(cardPrefab);

            var area = NewObject("Cards", parent);
            Area = area.transform;
            UITheme.StylePanel(area.AddComponent<Image>(), PanelColor);
            var element = area.AddComponent<LayoutElement>();
            element.flexibleWidth = 1;
            element.minWidth = element.preferredWidth = 0; // Takes the width left by the other columns
            var areaLayout = area.AddComponent<VerticalLayoutGroup>();
            areaLayout.padding = new RectOffset(20, 20, 14, 20);
            areaLayout.spacing = 8;
            SetControls(areaLayout, expandHeight: false);

            Title = AddText(area.transform, "", 36, TextColor, FontStyles.Bold, height: 52);

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
            gridLayout.spacing = new Vector2(16f, 18f);
            gridLayout.padding = new RectOffset(8, 8, 8, 8);
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            grid = content.transform;
            columns = CardColumns.AddTo(gridLayout, columnCount);

            scroll = area.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            WheelScroll.AddTo(scroll).PixelsPerNotch = 140f;
        }

        /// <summary>Shows these cards in place of the ones shown, scrolled to the top; each face can be dressed as it's made.</summary>
        public void Show(IEnumerable<CardDataScriptableObject> cards, System.Action<CardController, CardDataScriptableObject> dress = null) {
            Tooltip.Existing?.HideTooltip();
            foreach (var card in Cards) Object.Destroy(card.transform.parent.gameObject);
            Cards.Clear();

            foreach (var data in cards) {
                var cell = NewObject(data.cardName, grid);
                var face = cardViews.Create(CardFactory.CreateCard(data.ToCardData()), null, cell.transform);
                if (face == null) continue;
                var rect = (RectTransform)face.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                Cards.Add(face);
                dress?.Invoke(face, data);
            }
            columns.Fit();
            scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>The factions that have cards on offer, in the Faction Compendium's order.</summary>
        public static List<Faction> FactionsIn(DeckCatalog catalog) => catalog.AllCards.Select(c => c.faction).Distinct().OrderBy(Umbrellas.Order).ToList();

        /// <summary>The factions (the four) with cards on offer, in their order.</summary>
        public static List<Umbrella> UmbrellasIn(DeckCatalog catalog) => catalog.AllCards.Select(c => Umbrellas.Of(c.faction)).Distinct().OrderBy(u => Umbrellas.All.ToList().IndexOf(u)).ToList();

        /// <summary>A subfaction's cards on offer (null: every card, faction by faction): creatures, then spells, each by name.</summary>
        public static List<CardDataScriptableObject> CardsOf(DeckCatalog catalog, Faction? faction) =>
            Sorted(catalog.AllCards.Where(c => faction == null || c.faction == faction));

        /// <summary>A faction's cards on offer, all its subfactions: creatures, then spells, each by name.</summary>
        public static List<CardDataScriptableObject> CardsOf(DeckCatalog catalog, Umbrella umbrella) =>
            Sorted(catalog.AllCards.Where(c => Umbrellas.Of(c.faction) == umbrella));

        private static List<CardDataScriptableObject> Sorted(IEnumerable<CardDataScriptableObject> cards) => cards
            .OrderBy(c => Umbrellas.Order(c.faction)).ThenBy(c => c is CreatureCardScriptableObject ? 0 : 1).ThenBy(c => c.cardName).ToList();

        /// <summary>"12 cards: 8 creatures, 4 spells".</summary>
        public static string Count(IReadOnlyCollection<CardDataScriptableObject> cards) {
            int creatures = cards.Count(c => c is CreatureCardScriptableObject);
            return $"{Plural(cards.Count, "card")}: {Plural(creatures, "creature")}, {Plural(cards.Count - creatures, "spell")}";
        }

        public static string Plural(int count, string thing) => $"{count} {thing}{(count == 1 ? "" : "s")}";
    }
}
