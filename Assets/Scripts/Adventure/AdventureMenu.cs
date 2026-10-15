using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SlayTheClone.Enums;
using static SlayTheClone.MenuWidgets;

namespace SlayTheClone {
    /// <summary>
    /// Adventure, from the title screen. With no run under way it asks for your faction, its coins side by side; Embark
    /// starts a run with that faction's deck. Then the map (see <see cref="AdventureMap"/>), bottom to top: the rooms you
    /// can walk into next light up. A fight, an elite or the boss is a match against the AI (Easy for the first fights,
    /// then Medium; Hard for an elite, Very Hard for the boss) with a deck of another faction; the other rooms are walked
    /// through, nothing in them yet. Winning a fight brings you back here; losing one, or beating the boss, ends the run.
    /// </summary>
    public class AdventureMenu : MenuScreen {
        public const string FactionButton = "Faction: ";
        public const string EmbarkButtonName = "EmbarkButton";
        public const string BackButtonName = "BackButton";
        public const string AbandonButtonName = "AbandonButton";
        public const string RoomButton = "Room ";
        private const float RowSpacing = 96f, ColumnSpacing = 150f, MapMargin = 70f, SideWidth = 400f, CoinSize = 120f;

        /// <summary>Loads the game scene for a fight (the title screen's own way of starting a game).</summary>
        public Action StartGame;

        /// <summary>The faction picked to start a run with.</summary>
        public Faction? Chosen { get; private set; }
        /// <summary>The factions on offer, each with its deck.</summary>
        public IReadOnlyList<(Faction faction, DeckScriptableObject deck)> Factions =>
            Catalog.Opponents.Select(o => o.deck).Where(d => DeckCatalog.FactionsOf(d).Count == 1).Select(d => (DeckCatalog.FactionOf(d), d))
                .Where(f => f.Item1 != Faction.None).GroupBy(f => f.Item1).Select(g => g.First()).ToList();
        /// <summary>The map's room buttons, by room.</summary>
        public IReadOnlyDictionary<MapNode, Button> Rooms => rooms;
        /// <summary>What the last room did, or how the run ended.</summary>
        public string Status => status != null ? status.text : null;

        private readonly Dictionary<Faction, Button> factionButtons = new Dictionary<Faction, Button>();
        private readonly Dictionary<MapNode, Button> rooms = new Dictionary<MapNode, Button>();
        private Button embark;
        private TextMeshProUGUI chosenText;
        private TextMeshProUGUI status;
        private ScrollRect mapScroll;
        private string lastWalk; // What the last room walked through said, kept across the rebuild
        private bool abandonArmed;

        protected override GameObject Build() => AdventureRun.Active ? BuildMap() : BuildFactions();

        protected override void Opened() {
            if (!AdventureRun.Active) {
                Pick(Chosen ?? Factions.FirstOrDefault().faction);
                return;
            }
            // The map opens on where you are
            Canvas.ForceUpdateCanvases();
            float row = AdventureRun.Current?.Row ?? 0;
            mapScroll.verticalNormalizedPosition = Mathf.Clamp01((row - 2f) / (AdventureMap.Rows - 4f));
        }

        protected override void Forget() {
            factionButtons.Clear();
            rooms.Clear();
            embark = null;
            chosenText = status = null;
            mapScroll = null;
            abandonArmed = false;
        }

        #region Choosing a faction
        public void Pick(Faction faction) {
            Chosen = faction;
            foreach (var pair in factionButtons) ShowChosen(pair.Value, pair.Key == faction);
            var deck = Factions.FirstOrDefault(f => f.faction == faction).deck;
            if (chosenText != null) chosenText.text = deck != null ? $"{CardTextFormatter.FactionName(faction)}, with <color=#{ColorUtility.ToHtmlStringRGB(AccentColor)}>{deck.GetDeckName()}</color>" : "";
            if (embark != null) embark.interactable = deck != null;
        }

        /// <summary>Starts a run with the chosen faction's deck, on a new map, and shows it.</summary>
        public void Embark() {
            var deck = Factions.FirstOrDefault(f => f.faction == Chosen).deck;
            if (deck == null || Chosen == null) return;
            AdventureRun.Start(Chosen.Value, deck, Environment.TickCount);
            lastWalk = "Your journey begins. Pick a room on the bottom row.";
            Show();
        }

        private GameObject BuildFactions() {
            var screen = AddScreen(transform, "AdventurePanel", "Adventure");
            var column = AddColumn(screen.columns, "Choose your faction");
            column.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            var grid = NewObject("Factions", column);
            var layout = grid.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(230f, 210f);
            layout.spacing = new Vector2(24f, 24f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            grid.AddComponent<LayoutElement>().flexibleHeight = 1;
            foreach (var (faction, _) in Factions) {
                var button = AddButton(grid.transform, FactionButton + CardTextFormatter.FactionName(faction), "", -1, () => Pick(faction));
                var coin = UITheme.AddFactionCoin(button.transform, faction, CoinSize).rectTransform;
                coin.anchorMin = coin.anchorMax = coin.pivot = new Vector2(0.5f, 1f);
                coin.anchoredPosition = new Vector2(0f, -20f);
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.text = CardTextFormatter.FactionName(faction);
                label.alignment = TextAlignmentOptions.Bottom;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16;
                label.fontSizeMax = 28;
                factionButtons[faction] = button;
            }
            chosenText = AddText(column, "", 30, TextColor, FontStyles.Normal, height: 50);

            var footer = AddFooter(screen.panel.transform);
            AddButton(footer, BackButtonName, "Back", BackWidth, Hide);
            embark = AddButton(footer, EmbarkButtonName, "<b>Embark</b>", 360, Embark);
            UITheme.StyleButton(embark, UITheme.ButtonKind.Primary);
            return screen.panel;
        }
        #endregion

        #region The map
        /// <summary>
        /// Walks into the room, if it's one you can reach: a fight starts a match against the AI (the game scene loads); any
        /// other room is walked through, and the map shows where you are now.
        /// </summary>
        public void Enter(MapNode room) {
            if (!AdventureRun.CanEnter(room)) return;
            if (AdventureRun.Enter(room)) {
                SetUpFight(room);
                StartGame?.Invoke();
                return;
            }
            lastWalk = WalkThrough(room.Type);
            Show();
        }

        // What a room that does nothing yet says as you pass
        private static string WalkThrough(RoomType type) => type switch {
            RoomType.Rest => "A rest site. Nothing to do here yet: you walk on.",
            RoomType.Shop => "A shop, closed for now. You walk on.",
            RoomType.Treasure => "A chest, empty for now. You walk on.",
            RoomType.Unknown => "Something happened here. Or will, one day. You walk on.",
            _ => "You walk on."
        };

        /// <summary>The match for a fight room: your deck against another faction's, the AI as strong as the room.</summary>
        public void SetUpFight(MapNode room) {
            var rivals = Catalog.Opponents.Where(o => DeckCatalog.FactionOf(o.deck) != AdventureRun.Faction).ToList();
            if (rivals.Count == 0) rivals = Catalog.Opponents.ToList();
            var rival = rivals[new System.Random(AdventureRun.Map.Seed * 31 + room.Row * 7 + room.Column).Next(rivals.Count)];
            MatchSetup.Choose(AdventureRun.Deck, rival.name, rival.deck, AISeats.Player2);
            MatchSetup.AILevel = LevelFor(room);
            MatchSetup.AIStyle = AIStyle.Balanced;
        }

        /// <summary>How well the AI plays a fight room: the first fights are easy, an elite hard, the boss very hard.</summary>
        public static AILevel LevelFor(MapNode room) => room.Type switch {
            RoomType.Boss => AILevel.VeryHard,
            RoomType.Elite => AILevel.Hard,
            _ => room.Row < 3 ? AILevel.Easy : AILevel.Medium
        };

        private GameObject BuildMap() {
            string faction = CardTextFormatter.FactionName(AdventureRun.Faction);
            var screen = AddScreen(transform, "AdventurePanel", $"Adventure: {faction}");

            var mapColumn = AddColumn(screen.columns, null);
            mapColumn.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var content = AddList(mapColumn, out mapScroll);
            var contentRect = (RectTransform)content;
            DestroyImmediate(content.GetComponent<VerticalLayoutGroup>()); // Rooms are placed by hand, by row and column
            DestroyImmediate(content.GetComponent<ContentSizeFitter>());
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = new Vector2(1f, 0f);
            contentRect.pivot = new Vector2(0.5f, 0f);
            contentRect.sizeDelta = new Vector2(0f, MapMargin * 2 + AdventureMap.Rows * RowSpacing);
            DrawMap(contentRect);

            var side = AddColumn(screen.columns, faction);
            var sideElement = side.gameObject.AddComponent<LayoutElement>();
            sideElement.minWidth = sideElement.preferredWidth = SideWidth;
            sideElement.flexibleWidth = 0;
            var coinHolder = NewObject("Coin", side);
            coinHolder.AddComponent<LayoutElement>().preferredHeight = CoinSize + 10;
            var coin = UITheme.AddFactionCoin(coinHolder.transform, AdventureRun.Faction, CoinSize).rectTransform;
            coin.anchorMin = coin.anchorMax = coin.pivot = new Vector2(0.5f, 0.5f);
            AddText(side, AdventureRun.Deck != null ? AdventureRun.Deck.GetDeckName() : "", 22, UITheme.Hex(0xC9C0AE), FontStyles.Italic, height: 34);
            status = AddText(side, StatusText(), 24, TextColor, FontStyles.Normal, height: 150);
            status.textWrappingMode = TextWrappingModes.Normal;
            foreach (RoomType type in Enum.GetValues(typeof(RoomType))) AddLegend(side, type);

            var footer = AddFooter(screen.panel.transform);
            AddButton(footer, BackButtonName, "Back", BackWidth, Hide);
            bool over = AdventureRun.Won || AdventureRun.Lost;
            var abandon = AddButton(footer, AbandonButtonName, over ? "<b>New adventure</b>" : "Abandon run", 360, Abandon);
            if (over) UITheme.StyleButton(abandon, UITheme.ButtonKind.Primary);
            return screen.panel;
        }

        private string StatusText() {
            if (AdventureRun.Won) return "<b>The boss is beaten.</b> Your adventure is won!";
            if (AdventureRun.Lost) return $"<b>Defeated</b> after {AdventureRun.Path.Count} rooms. The run is over.";
            return lastWalk ?? (AdventureRun.Current == null ? "Pick a room on the bottom row." : "Pick the next room along your path.");
        }

        /// <summary>Ends the run: armed by the first click, done by the second (a run that's over ends at once). Back to the faction choice.</summary>
        public void Abandon() {
            bool over = AdventureRun.Won || AdventureRun.Lost;
            if (!over && !abandonArmed) {
                abandonArmed = true;
                var label = Panel.GetComponentsInChildren<Button>().First(b => b.name == AbandonButtonName).GetComponentInChildren<TextMeshProUGUI>();
                label.text = "Abandon? Click again";
                return;
            }
            AdventureRun.Clear();
            lastWalk = null;
            Show();
        }

        private void DrawMap(RectTransform area) {
            var reachable = AdventureRun.Reachable();
            var walked = new HashSet<MapNode>(AdventureRun.Path);
            var all = AdventureRun.Map.Nodes.Append(AdventureRun.Map.Boss).ToList();
            // The paths first, under the rooms; the ones you took in brass
            foreach (var node in all) {
                foreach (var next in node.Next) {
                    bool taken = walked.Contains(next) && (walked.Contains(node));
                    AddLine(area, Position(node), Position(next), taken ? AccentColor : UITheme.WithAlpha(TextColor, 0.25f), taken ? 5f : 3f);
                }
            }
            foreach (var node in all) {
                bool canGo = reachable.Contains(node);
                bool here = node == AdventureRun.Current;
                var button = AddRoom(area, node, canGo, walked.Contains(node), here);
                rooms[node] = button;
            }
        }

        // Where a room sits: its row up from the bottom, its column across, nudged a little (the same each time) so the map
        // doesn't look like a grid. The boss sits above the last row, in the middle
        private Vector2 Position(MapNode node) {
            if (node.Type == RoomType.Boss) return new Vector2(0f, MapMargin + node.Row * RowSpacing + 10f);
            var jitter = new System.Random(AdventureRun.Map.Seed + node.Row * 101 + node.Column * 13);
            float x = (node.Column - (AdventureMap.Columns - 1) / 2f) * ColumnSpacing + (float)(jitter.NextDouble() - 0.5) * 40f;
            float y = MapMargin + node.Row * RowSpacing + (float)(jitter.NextDouble() - 0.5) * 24f;
            return new Vector2(x, y);
        }

        private Button AddRoom(RectTransform area, MapNode node, bool canGo, bool walked, bool here) {
            bool boss = node.Type == RoomType.Boss;
            var go = NewObject(RoomButton + node.Id, area);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = boss ? new Vector2(220f, 84f) : new Vector2(120f, 48f);
            rect.anchoredPosition = Position(node);
            var color = RoomColor(node.Type);
            var image = UITheme.Round(go.AddComponent<Image>(), canGo || walked || boss ? color : Color.Lerp(color, UITheme.Panel, 0.6f), 2f);
            UITheme.AddTrim(go.transform, here ? UITheme.Parchment : canGo ? AccentColor : walked ? UITheme.GoldDim : UITheme.WithAlpha(UITheme.GoldDim, 0.3f), here ? 3f : 2f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.4f, 1.3f, 1.1f, 1f);
            colors.disabledColor = Color.white; // Dimmed by its own color instead
            button.colors = colors;
            button.interactable = canGo;
            button.onClick.AddListener(() => Enter(node));
            var label = AddText(go.transform, RoomName(node.Type), boss ? 34 : 20, canGo || walked || boss ? UITheme.Parchment : UITheme.WithAlpha(UITheme.Parchment, 0.5f),
                boss || here ? FontStyles.Bold : FontStyles.Normal, height: -1);
            Stretch(label.gameObject, margin: 4);
            label.enableAutoSizing = true;
            label.fontSizeMin = 12;
            label.fontSizeMax = boss ? 34 : 20;
            return button;
        }

        private static void AddLine(RectTransform area, Vector2 from, Vector2 to, Color color, float width) {
            var line = UITheme.NewImage("Path", area, color);
            var rect = line.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var delta = to - from;
            rect.sizeDelta = new Vector2(delta.magnitude, width);
            rect.anchoredPosition = (from + to) / 2f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static void AddLegend(Transform parent, RoomType type) {
            var row = NewObject("Legend " + type, parent);
            row.AddComponent<LayoutElement>().preferredHeight = 34;
            var swatch = UITheme.Round(UITheme.NewImage("Swatch", row.transform, RoomColor(type)), RoomColor(type), 1.5f);
            UITheme.Place(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(56f, 26f));
            var text = AddText(row.transform, $"{RoomName(type)}  <color=#{ColorUtility.ToHtmlStringRGB(UITheme.Hex(0xC9C0AE))}>{RoomMeaning(type)}</color>", 20, TextColor, FontStyles.Normal, height: -1);
            Stretch(text.gameObject);
            text.rectTransform.offsetMin = new Vector2(72f, 0f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
        }

        public static string RoomName(RoomType type) => type switch {
            RoomType.Unknown => "?",
            _ => type.ToString()
        };

        private static string RoomMeaning(RoomType type) => type switch {
            RoomType.Fight => "a match against the AI",
            RoomType.Elite => "a tougher one",
            RoomType.Boss => "the last fight",
            RoomType.Unknown => "an event (soon)",
            RoomType.Rest => "rest (soon)",
            RoomType.Shop => "a shop (soon)",
            RoomType.Treasure => "a chest (soon)",
            _ => ""
        };

        private static Color RoomColor(RoomType type) => type switch {
            RoomType.Fight => UITheme.Hex(0x8A4B3C),
            RoomType.Elite => UITheme.Hex(0xB0302A),
            RoomType.Unknown => UITheme.Hex(0x4E5D8C),
            RoomType.Rest => UITheme.Hex(0x3E7D4F),
            RoomType.Shop => UITheme.Hex(0xA8842C),
            RoomType.Treasure => UITheme.Hex(0x7A5A2A),
            _ => UITheme.Hex(0x5A1E1E)
        };
        #endregion
    }
}
