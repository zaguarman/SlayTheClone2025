using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Zeitgeist.Enums;
using static Zeitgeist.MenuWidgets;

namespace Zeitgeist {
    /// <summary>
    /// Adventure, from the title screen. With no run under way it asks for your faction, its coins side by side; Embark
    /// starts a run with that faction's starter deck, small and plain (see <see cref="AdventureDecks"/>). Then the map (see
    /// <see cref="AdventureMap"/>), bottom to top: the rooms you can walk into next light up. A fight, an elite or the boss
    /// is a match against the AI (Easy for the first fights, then Medium; Hard for an elite, Very Hard for the boss) with a
    /// deck of another faction made for the room, weak and clumsy at first, better further up; the other rooms are walked
    /// through, nothing in them yet, but a shop. Winning a fight brings you back here to take one of three of its cards, or
    /// skip, and pays EuroYuans; at a shop they buy cards, or have one taken out of your deck. Losing a fight, or beating
    /// the boss, ends the run, and its money with it.
    /// </summary>
    public class AdventureMenu : MenuScreen {
        public const string FactionButton = "Faction: ";
        public const string EmbarkButtonName = "EmbarkButton";
        public const string BackButtonName = "BackButton";
        public const string AbandonButtonName = "AbandonButton";
        public const string RoomButton = "Room ";
        public const string SkipButtonName = "SkipButton";
        public const string PackButton = "RewardPack ";
        public const string ProceedButtonName = "ProceedButton";
        public const string DeckButtonName = "DeckButton";
        public const string PickBackButtonName = "PickBackButton";
        public const string LeaveShopButtonName = "LeaveShopButton";
        public const string RemoveCardButtonName = "RemoveCardButton";
        public const string EventChoiceButton = "EventChoice ";
        public const string EventOnButtonName = "EventOnButton";
        public const string UpgradeBackButtonName = "UpgradeBackButton";
        public const string AutoplayButtonName = "AutoplayButton";
        public const string AscensionButtonName = "AscensionButton";
        public const string RelicOfferButtonName = "RelicOfferButton";
        /// <summary>The Ascension level the next run starts at (see <see cref="Ascension"/>), up to what the chosen faction unlocked.</summary>
        public int AscensionLevel { get; private set; }
        private Button ascensionButton;
        private const float EventWidth = 900f;
        private const int ShopColumns = 5; // The shop's cards on one row
        private const int RewardColumns = 4; // The reward's cards at a readable size, not stretched over the width
        private const int DeckColumns = 6; // The whole deck at a glance
        private const float RewardsWidth = 760f; // The rewards list: a narrow scroll in the middle, as in Slay the Spire
        private const float RowSpacing = 96f, ColumnSpacing = 150f, MapMargin = 70f, SideWidth = 400f, CoinSize = 120f;

        /// <summary>Loads the game scene for a fight (the title screen's own way of starting a game).</summary>
        public Action StartGame;

        /// <summary>The faction picked to start a run with.</summary>
        public Umbrella? Chosen { get; private set; }
        /// <summary>The factions on offer (the four, the user, 2026-10-17): those with cards in the catalog.</summary>
        public IReadOnlyList<Umbrella> Factions => CardGrid.UmbrellasIn(Catalog);
        /// <summary>The map's room buttons, by room.</summary>
        public IReadOnlyDictionary<MapNode, Button> Rooms => rooms;
        /// <summary>What the last room did, or how the run ended.</summary>
        public string Status => status != null ? status.text : null;
        /// <summary>The cards of the pack open on screen; empty when none is.</summary>
        public IReadOnlyList<CardController> RewardCards => reward != null ? reward.Cards : new List<CardController>();
        /// <summary>The deck shown on screen (View Deck); empty when it isn't.</summary>
        public IReadOnlyList<CardController> DeckCards => deckGrid != null ? deckGrid.Cards : new List<CardController>();
        /// <summary>The pack open on screen, if any.</summary>
        public AdventureRun.RewardPack OpenPack { get; private set; }
        /// <summary>Your deck is shown, over the rewards or the map.</summary>
        public bool ViewingDeck { get; private set; }
        /// <summary>At a shop, your deck is up to pick the card to take out.</summary>
        public bool Removing { get; private set; }
        /// <summary>The shop's cards on screen, in its order; empty outside it.</summary>
        public IReadOnlyList<CardController> ShopCards => shopGrid != null ? shopGrid.Cards : new List<CardController>();
        private CardGrid reward, deckGrid, shopGrid;

        private readonly Dictionary<Umbrella, Button> factionButtons = new Dictionary<Umbrella, Button>();
        private readonly Dictionary<MapNode, Button> rooms = new Dictionary<MapNode, Button>();
        private Button embark;
        private TextMeshProUGUI chosenText;
        private TextMeshProUGUI status;
        private ScrollRect mapScroll;
        private string lastWalk; // What the last room walked through said, kept across the rebuild
        private bool abandonArmed;

        protected override GameObject Build() => !AdventureRun.Active ? BuildFactions()
            : ViewingDeck ? BuildDeck()
            : AdventureRun.InShop ? (Removing ? BuildRemoval() : BuildShop())
            : AdventureRun.InEvent ? (AdventureRun.Upgrading ? BuildUpgrade() : BuildEvent())
            : AdventureRun.InRewards ? (OpenPack != null ? BuildPick(OpenPack) : BuildRewards())
            : BuildMap();

        protected override void Opened() {
            if (!AdventureRun.Active) {
                Pick(Chosen ?? Factions.FirstOrDefault());
                return;
            }
            if (mapScroll == null) return; // The card reward is up
            // The map opens on where you are
            Canvas.ForceUpdateCanvases();
            float row = AdventureRun.Current?.Row ?? 0;
            mapScroll.verticalNormalizedPosition = Mathf.Clamp01((row - 2f) / (AdventureMap.Rows - 4f));
        }

        protected override void Forget() {
            factionButtons.Clear();
            rooms.Clear();
            embark = null;
            ascensionButton = null;
            reward = deckGrid = shopGrid = null;
            chosenText = status = null;
            mapScroll = null;
            abandonArmed = false;
        }

        #region Choosing a faction
        public void Pick(Umbrella faction) {
            Chosen = faction;
            foreach (var pair in factionButtons) ShowChosen(pair.Value, pair.Key == faction);
            bool offered = Factions.Contains(faction);
            if (chosenText != null) chosenText.text = offered
                ? $"{Umbrellas.Name(faction)} ({string.Join(", ", Umbrellas.Subfactions(faction).Select(CardTextFormatter.FactionName))}), with <color=#{ColorUtility.ToHtmlStringRGB(AccentColor)}>a starter deck of {AdventureDecks.StarterSize} plain cards</color>" : "";
            if (embark != null) embark.interactable = offered;
            AscensionLevel = Mathf.Min(AscensionLevel, Ascension.Unlocked(faction));
            ShowAscension();
        }

        /// <summary>The next Ascension level for the chosen faction, back to 0 after the highest it unlocked.</summary>
        public void CycleAscension() {
            if (Chosen == null) return;
            int unlocked = Ascension.Unlocked(Chosen.Value);
            AscensionLevel = AscensionLevel >= unlocked ? 0 : AscensionLevel + 1;
            Pick(Chosen.Value); // Its text again, with the level's rules
        }

        private void ShowAscension() {
            if (ascensionButton == null || Chosen == null) return;
            int unlocked = Ascension.Unlocked(Chosen.Value);
            ascensionButton.GetComponentInChildren<TextMeshProUGUI>().text = unlocked == 0 ? "Ascension: locked"
                : $"Ascension {AscensionLevel} <size=70%>of {unlocked}</size>";
            ascensionButton.interactable = unlocked > 0;
            if (chosenText != null && AscensionLevel > 0) chosenText.text += $"\n<size=75%>{string.Join("  ", Ascension.Rules(AscensionLevel))}</size>";
        }

        /// <summary>
        /// The autopilot (see <see cref="AdventureAutopilot"/>): it plays the run on screen, the AI on both seats of the fights,
        /// at the speed you set. From the faction choice it starts a run first; on the map it takes over the run, or gives it back.
        /// </summary>
        public void Autoplay() {
            if (AdventureAutopilot.On) {
                AdventureAutopilot.End();
                Show();
                return;
            }
            if (!AdventureRun.Active) Embark();
            AdventureAutopilot.Begin();
            Show();
        }

        /// <summary>Starts a run with the chosen faction's starter deck, on a new map, and shows it.</summary>
        public void Embark() {
            if (Chosen == null || !Factions.Contains(Chosen.Value)) return;
            AdventureRun.Start(Chosen.Value, AdventureDecks.Starter(Chosen.Value, Catalog.AllCards), Environment.TickCount, Catalog.AllCards, Catalog.Potions,
                Mathf.Min(AscensionLevel, Ascension.Unlocked(Chosen.Value)));
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
            foreach (var faction in Factions) {
                var button = AddButton(grid.transform, FactionButton + Umbrellas.Name(faction), "", -1, () => Pick(faction));
                var coin = UITheme.AddUmbrellaCoin(button.transform, faction, CoinSize).rectTransform;
                coin.anchorMin = coin.anchorMax = coin.pivot = new Vector2(0.5f, 1f);
                coin.anchoredPosition = new Vector2(0f, -20f);
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.text = Umbrellas.Name(faction);
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
            AddButton(footer, AutoplayButtonName, "Autoplay", 260, Autoplay);
            ascensionButton = AddButton(footer, AscensionButtonName, "Ascension", 300, CycleAscension);
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
            if (AdventureRun.Enter(room, Catalog.AllCards)) {
                SetUpFight(room);
                StartGame?.Invoke();
                return;
            }
            lastWalk = AdventureRun.InShop ? "A shop." : AdventureRun.InEvent ? null : WalkThrough(room.Type);
            Show();
        }

        // What a room that does nothing yet says as you pass
        private static string WalkThrough(RoomType type) => type switch {
            RoomType.Rest => "A rest site.",
            RoomType.Shop => "A shop, closed for now. You walk on.",
            RoomType.Treasure => "A chest.",
            RoomType.Unknown => "Something happened here. Or will, one day. You walk on.",
            _ => "You walk on."
        };

        /// <summary>
        /// The match for a fight room: your deck against a deck of another faction made for the room (weak and clumsy at
        /// first, better further up), the AI as strong as the room.
        /// </summary>
        public void SetUpFight(MapNode room) {
            var random = new System.Random(AdventureRun.Map.Seed * 31 + room.Row * 7 + room.Column);
            // The enemy plays a subfaction of another faction than yours
            var rivals = CardGrid.FactionsIn(Catalog).Where(f => Umbrellas.Of(f) != AdventureRun.Umbrella).ToList();
            if (rivals.Count == 0) rivals = CardGrid.FactionsIn(Catalog).ToList();
            var faction = rivals[random.Next(rivals.Count)];
            if (room.Type == RoomType.Boss && AdventureRun.BossFaction != Faction.None) faction = AdventureRun.BossFaction; // The one the map showed
            string name = AdventureDecks.EnemyName(faction, room);
            var deck = AdventureDecks.Enemy(faction, AdventureDecks.StrengthOf(room), Catalog.AllCards, random, name);
            AdventureRun.SetEnemy(deck);
            MatchSetup.Choose(AdventureRun.Deck, name, deck, AISeats.Player2);
            MatchSetup.PlayerHealth = AdventureRun.Health; // Your Health goes from fight to fight
            MatchSetup.OpponentHealth = AdventureRun.EnemyHealthFor(room); // 5, 10, 15... the boss 60
            MatchSetup.PlayerRelics.AddRange(AdventureRun.Relics); // Your kit: the relics, and the potions in your opening hand
            MatchSetup.PlayerPotions.AddRange(AdventureRun.Potions);
            // An elite carries a relic of its own, the boss two (Slay the Spire's elites differ; these are readable)
            AdventureRun.EnemyRelics.Clear();
            int perks = room.Type == RoomType.Boss ? 2 : room.Type == RoomType.Elite ? 1 : 0;
            var perkPool = AdventureRun.EnemyPerks.ToList();
            for (int i = 0; i < perks && perkPool.Count > 0; i++) {
                var perk = perkPool[random.Next(perkPool.Count)];
                perkPool.Remove(perk);
                AdventureRun.EnemyRelics.Add(perk);
            }
            MatchSetup.OpponentRelics.AddRange(AdventureRun.EnemyRelics);
            MatchSetup.AILevel = LevelFor(room);
            MatchSetup.AIStyle = AIStyle.Balanced;
            if (AdventureAutopilot.On) { // The autopilot: the AI plays your seat too
                MatchSetup.AI = AISeats.Both;
                MatchSetup.SetLevel(AISeats.Player1, AdventureAutopilot.Level);
                MatchSetup.SetLevel(AISeats.Player2, LevelFor(room));
            }
        }

        /// <summary>How well the AI plays a fight room: the first fights are easy, an elite hard, the boss very hard.</summary>
        public static AILevel LevelFor(MapNode room, int ascension = -1) {
            var level = room.Type switch {
                RoomType.Boss => AILevel.VeryHard,
                RoomType.Elite => AILevel.Hard,
                _ => room.Row < 3 ? AILevel.Easy : AILevel.Medium
            };
            int bonus = Ascension.AIBonus(ascension < 0 ? AdventureRun.Ascension : ascension); // Ascension 2: a level better
            return (AILevel)Mathf.Min((int)AILevel.VeryHard, (int)level + bonus);
        }

        private GameObject BuildMap() {
            string faction = Umbrellas.Name(AdventureRun.Umbrella);
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
            var coin = UITheme.AddUmbrellaCoin(coinHolder.transform, AdventureRun.Umbrella, CoinSize).rectTransform;
            coin.anchorMin = coin.anchorMax = coin.pivot = new Vector2(0.5f, 0.5f);
            AddText(side, AdventureRun.Deck != null ? $"Your deck: {CardGrid.Count(AdventureRun.Deck.Cards)}" : "", 22, UITheme.Hex(0xC9C0AE), FontStyles.Italic, height: 34);
            AddText(side, $"Health: <b>{AdventureRun.Health}</b>    EuroYuans: {AdventureRun.Money(AdventureRun.EuroYuans)}", 26, AccentColor, FontStyles.Normal, height: 38);
            AddText(side, (AdventureRun.BossFaction != Faction.None ? $"The boss plays <b>{CardTextFormatter.FactionName(AdventureRun.BossFaction)}</b>" : "")
                          + (AdventureRun.Ascension > 0 ? $"    Ascension {AdventureRun.Ascension}" : ""), 22, TextColor, FontStyles.Normal, height: 32);
            status = AddText(side, StatusText(), 24, TextColor, FontStyles.Normal, height: 150);
            status.textWrappingMode = TextWrappingModes.Normal;
            AddKit(side);
            foreach (RoomType type in Enum.GetValues(typeof(RoomType))) AddLegend(side, type);

            var footer = AddFooter(screen.panel.transform);
            AddButton(footer, BackButtonName, "Back", BackWidth, Hide);
            AddButton(footer, DeckButtonName, "View deck", 260, () => ShowDeck(true));
            bool over = AdventureRun.Won || AdventureRun.Lost;
            if (!over) AddButton(footer, AutoplayButtonName, AdventureAutopilot.On ? "Take over" : "Autoplay", 240, Autoplay);
            var abandon = AddButton(footer, AbandonButtonName, over ? "<b>New adventure</b>" : "Abandon run", 360, Abandon);
            if (over) UITheme.StyleButton(abandon, UITheme.ButtonKind.Primary);
            return screen.panel;
        }

        #region Rewards (Slay the Spire's)
        // After a fight won: the packs it dropped, each opened as often as you like until you take a card or skip it; your
        // deck one click away; Proceed goes on to the map
        private GameObject BuildRewards() {
            string from = AdventureRun.EnemyDeck != null ? AdventureRun.EnemyDeck.GetDeckName() : "the deck you beat";
            var screen = AddScreen(transform, "AdventurePanel", "Rewards");
            var column = AddColumn(screen.columns, $"Beaten: {from}");
            var columnSize = column.gameObject.AddComponent<LayoutElement>();
            columnSize.preferredWidth = RewardsWidth;
            columnSize.flexibleWidth = 0;
            if (screen.columns.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row) row.childAlignment = TextAnchor.UpperCenter;
            foreach (var found in AdventureRun.Found) {
                var line = AddText(column, found, 24, TextColor, FontStyles.Normal, height: 64);
                line.textWrappingMode = TextWrappingModes.Normal;
            }
            if (AdventureRun.Earned > 0) AddText(column, $"<b>+{AdventureRun.Money(AdventureRun.Earned)}</b>  <color=#C9C0AE>you have {AdventureRun.Money(AdventureRun.EuroYuans)}</color>",
                30, AccentColor, FontStyles.Normal, height: 56);
            for (int i = 0; i < AdventureRun.Rewards.Count; i++) {
                var pack = AdventureRun.Rewards[i];
                if (pack.Cards.Count == 0) continue;
                string label = pack.Taken != null ? $"<s>Add a card to your deck</s>  <color=#C9C0AE>{pack.Taken.cardName} taken</color>"
                    : pack.Done ? "<s>Add a card to your deck</s>  <color=#C9C0AE>skipped</color>"
                    : "<b>Add a card to your deck</b>";
                var button = AddButton(column, PackButton + i, label, -1, () => OpenRewardPack(pack));
                button.interactable = !pack.Done;
            }
            AddText(column, $"Your deck: {CardGrid.Count(AdventureRun.Deck.Cards)}", 22, UITheme.Hex(0xC9C0AE), FontStyles.Italic, height: 40);

            var footer = AddFooter(screen.panel.transform);
            AddButton(footer, DeckButtonName, "View deck", 260, () => ShowDeck(true));
            var proceed = AddButton(footer, ProceedButtonName, "<b>Proceed</b>", 360, ProceedFromRewards);
            UITheme.StyleButton(proceed, UITheme.ButtonKind.Primary);
            return screen.panel;
        }

        // A pack open: its cards at a readable size; click one to take it, Skip to leave them, Back to decide later
        private GameObject BuildPick(AdventureRun.RewardPack pack) {
            var screen = AddScreen(transform, "AdventurePanel", "Choose a card");
            reward = new CardGrid(screen.columns, Catalog.CardPrefab, RewardColumns);
            reward.Title.text = "Take one into your deck, or skip them";
            reward.Show(pack.Cards, (face, asset) => {
                face.ShowsItself = true; // Read where it is (the user): its keywords explain themselves beside it, a click takes it
                face.OnCardClicked.AddListener(_ => TakeReward(asset));
            });

            var footer = AddFooter(screen.panel.transform);
            AddButton(footer, PickBackButtonName, "Back", BackWidth, () => OpenRewardPack(null));
            AddButton(footer, DeckButtonName, "View deck", 260, () => ShowDeck(true));
            var skip = AddButton(footer, SkipButtonName, "Skip", 300, SkipReward);
            UITheme.StyleButton(skip, UITheme.ButtonKind.Primary);
            return screen.panel;
        }

        #region Events (the ? rooms)
        // The scene, then its choices; once one is made, what happened and a button to go on
        private GameObject BuildEvent() {
            var happening = AdventureRun.Event;
            var screen = AddScreen(transform, "AdventurePanel", happening.Title);
            var column = AddColumn(screen.columns, null);
            var size = column.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = EventWidth;
            size.flexibleWidth = 0;
            if (screen.columns.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup row) row.childAlignment = TextAnchor.UpperCenter;
            var scene = AddText(column, $"<i>{happening.Text}</i>", 28, TextColor, FontStyles.Normal, height: 190);
            scene.textWrappingMode = TextWrappingModes.Normal;
            AddText(column, $"Health {AdventureRun.Health}    {AdventureRun.Money(AdventureRun.EuroYuans)}    {CardGrid.Count(AdventureRun.Deck.Cards)}", 22, UITheme.Hex(0xC9C0AE), FontStyles.Italic, height: 40);
            var footer = AddFooter(screen.panel.transform);
            AddButton(footer, DeckButtonName, "View deck", 260, () => ShowDeck(true));
            if (AdventureRun.EventOutcome == null) {
                for (int i = 0; i < happening.Choices.Count; i++) {
                    var choice = happening.Choices[i];
                    var button = AddButton(column, EventChoiceButton + i, choice.Label, -1, () => ChooseAtEvent(choice));
                    button.interactable = choice.Available();
                }
            }
            else {
                var outcome = AddText(column, AdventureRun.EventOutcome, 28, AccentColor, FontStyles.Normal, height: 120);
                outcome.textWrappingMode = TextWrappingModes.Normal;
                var on = AddButton(footer, EventOnButtonName, "<b>Go on</b>", 300, LeaveEvent);
                UITheme.StyleButton(on, UITheme.ButtonKind.Primary);
            }
            return screen.panel;
        }

        // A rest site's Train: the cards of your deck that can be upgraded; click one to upgrade it, Back to the choices
        private GameObject BuildUpgrade() {
            var screen = AddScreen(transform, "AdventurePanel", "Train");
            deckGrid = new CardGrid(screen.columns, Catalog.CardPrefab, DeckColumns);
            deckGrid.Title.text = "Click the card to upgrade  <size=70%><color=#C9C0AE>a creature +1 Attack and +1 Health; a spell 1 more on each of its numbers</color></size>";
            deckGrid.Show(CardUpgrades.Upgradable(AdventureRun.Deck.Cards).OrderBy(c => c is CreatureCardScriptableObject ? 0 : 1).ThenBy(c => c.cardName),
                (face, asset) => face.OnCardClicked.AddListener(_ => UpgradeCard(asset)));
            var footer = AddFooter(screen.panel.transform);
            AddButton(footer, UpgradeBackButtonName, "Back", BackWidth, () => { AdventureRun.StartUpgrade(false); Show(); });
            return screen.panel;
        }

        /// <summary>Upgrades the card (the rest site's Train), back to the rest site to go on.</summary>
        public void UpgradeCard(CardDataScriptableObject card) {
            if (!AdventureRun.UpgradeCard(card)) return;
            Show();
        }

        // The relics and potions you carry, under the map's status
        private void AddKit(Transform side) {
            string relics = AdventureRun.Relics.Count == 0 ? "<color=#C9C0AE>none yet</color>"
                : string.Join("\n", AdventureRun.Relics.Select(r => $"<b>{Zeitgeist.Relics.NameOf(r)}</b> <size=85%><color=#C9C0AE>{Zeitgeist.Relics.Describe(r)}</color></size>"));
            string potions = AdventureRun.Potions.Count == 0 ? "<color=#C9C0AE>none</color>" : string.Join(", ", AdventureRun.Potions.Select(p => p.cardName));
            var kit = AddText(side, $"Relics:\n{relics}\nPotions ({AdventureRun.Potions.Count}/{AdventureRun.MaxPotions}): {potions}", 20, TextColor, FontStyles.Normal,
                height: 60 + 30 * Math.Max(1, AdventureRun.Relics.Count));
            kit.textWrappingMode = TextWrappingModes.Normal;
        }

        /// <summary>Makes the event's choice: what it says happens, and the screen says so.</summary>
        public void ChooseAtEvent(AdventureEvents.Choice choice) {
            if (!AdventureRun.ChooseAtEvent(choice, Catalog.AllCards)) return;
            Show();
        }

        /// <summary>On from the event to the map.</summary>
        public void LeaveEvent() {
            lastWalk = AdventureRun.EventOutcome;
            AdventureRun.LeaveEvent();
            Show();
        }
        #endregion

        #region The shop
        // A shop: its cards on a row, each with its price; click one to buy it. A card can be taken out of your deck, once;
        // Leave goes on to the map
        private GameObject BuildShop() {
            var screen = AddScreen(transform, "AdventurePanel", "Shop");
            shopGrid = new CardGrid(screen.columns, Catalog.CardPrefab, ShopColumns);
            shopGrid.Title.text = $"You have {AdventureRun.Money(AdventureRun.EuroYuans)}  <size=70%><color=#C9C0AE>Click a card to buy it</color></size>";
            var offers = AdventureRun.Shop.Where(o => !o.IsRelic).ToList(); // Cards and potions; the relic has its button
            var relic = AdventureRun.Shop.FirstOrDefault(o => o.IsRelic);
            if (relic != null && !relic.Sold) shopGrid.Title.text += $"\n<size=60%><color=#C9C0AE>Relic for sale: {Relics.Line(relic.Relic)}</color></size>";
            int at = 0;
            shopGrid.Show(offers.Select(o => o.Card), (face, _) => {
                var offer = offers[at++];
                face.ShowsItself = true;
                AddPriceTag(face, offer);
                face.OnCardClicked.AddListener(__ => Buy(offer));
            });

            var footer = AddFooter(screen.panel.transform);
            AddButton(footer, DeckButtonName, "View deck", 260, () => ShowDeck(true));
            string removal = AdventureRun.RemovedHere ? "<s>Remove a card</s>  <color=#C9C0AE>done</color>"
                : $"Remove a card  {AdventureRun.Money(AdventureRun.RemovalCost)}";
            if (relic != null) {
                var buyRelic = AddButton(footer, RelicOfferButtonName, relic.Sold ? $"<s>{Relics.NameOf(relic.Relic)}</s>  <color=#C9C0AE>sold</color>"
                    : $"Relic: {Relics.NameOf(relic.Relic)}  {AdventureRun.Money(relic.Price)}", 380, () => Buy(relic));
                buyRelic.interactable = !relic.Sold && AdventureRun.EuroYuans >= relic.Price;
            }
            var remove = AddButton(footer, RemoveCardButtonName, removal, 380, () => OpenRemoval(true));
            remove.interactable = AdventureRun.CanRemove;
            var leave = AddButton(footer, LeaveShopButtonName, "<b>Leave</b>", 300, LeaveShop);
            UITheme.StyleButton(leave, UITheme.ButtonKind.Primary);
            return screen.panel;
        }

        // The price under a card for sale (red if you can't pay it), or Sold over a faded one
        private static void AddPriceTag(CardController face, AdventureRun.ShopOffer offer) {
            var tag = UITheme.Round(UITheme.NewImage("Price", face.transform, UITheme.Panel), UITheme.Panel, 1.2f);
            UITheme.AddTrim(tag.transform, offer.Sold ? UITheme.GoldDim : AccentColor, 1.2f);
            var rect = tag.rectTransform;
            rect.anchorMin = new Vector2(0.18f, -0.05f);
            rect.anchorMax = new Vector2(0.82f, 0.05f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            bool afford = AdventureRun.EuroYuans >= offer.Price;
            var text = UITheme.NewText("Amount", tag.transform, offer.Sold ? "Sold" : AdventureRun.Money(offer.Price), 40,
                offer.Sold ? UITheme.TextMuted : afford ? UITheme.Gold : UITheme.HealthLow);
            text.enableAutoSizing = true;
            text.fontSizeMin = 10;
            text.fontSizeMax = 60;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            UITheme.Stretch(text.rectTransform, 4f);
            if (offer.Sold) (face.TryGetComponent(out CanvasGroup group) ? group : face.gameObject.AddComponent<CanvasGroup>()).alpha = 0.35f;
        }

        // Your deck, to click the card to take out of it; Back to the shop
        private GameObject BuildRemoval() {
            var screen = AddScreen(transform, "AdventurePanel", "Remove a card");
            deckGrid = new CardGrid(screen.columns, Catalog.CardPrefab, DeckColumns);
            deckGrid.Title.text = $"Click the card to take out of your deck, for {AdventureRun.Money(AdventureRun.RemovalCost)}";
            deckGrid.Show(AdventureRun.Deck.Cards.OrderBy(c => c is CreatureCardScriptableObject ? 0 : 1).ThenBy(c => c.cardName),
                (face, asset) => face.OnCardClicked.AddListener(_ => RemoveFromDeck(asset)));
            var footer = AddFooter(screen.panel.transform);
            AddButton(footer, BackButtonName, "Back", BackWidth, () => OpenRemoval(false));
            return screen.panel;
        }

        /// <summary>Buys the shop's card, if you can pay it.</summary>
        public void Buy(AdventureRun.ShopOffer offer) {
            if (!AdventureRun.Buy(offer)) return;
            lastWalk = offer.IsRelic ? $"You bought a relic: {Relics.NameOf(offer.Relic)}."
                : offer.IsPotion ? $"You bought a potion: {offer.Card.cardName}." : $"You bought {offer.Card.cardName}.";
            Show();
        }

        /// <summary>Opens your deck to pick the card to take out (false: back to the shop).</summary>
        public void OpenRemoval(bool open) {
            Removing = open && AdventureRun.CanRemove;
            Show();
        }

        /// <summary>Takes the card out of your deck, for the shop's price; back to the shop.</summary>
        public void RemoveFromDeck(CardDataScriptableObject card) {
            if (!AdventureRun.Remove(card)) return;
            lastWalk = $"{card.cardName} is out of your deck.";
            OpenRemoval(false);
        }

        /// <summary>Out of the shop, on to the map.</summary>
        public void LeaveShop() {
            AdventureRun.LeaveShop();
            Removing = false;
            if (lastWalk == "A shop.") lastWalk = "You leave the shop.";
            Show();
        }
        #endregion

        // Your deck, Slay the Spire style: every card, a few to a row; Back returns where you were
        private GameObject BuildDeck() {
            var screen = AddScreen(transform, "AdventurePanel", "Your deck");
            deckGrid = new CardGrid(screen.columns, Catalog.CardPrefab, DeckColumns);
            deckGrid.Title.text = CardGrid.Count(AdventureRun.Deck.Cards);
            deckGrid.Show(AdventureRun.Deck.Cards.OrderBy(c => c is CreatureCardScriptableObject ? 0 : 1).ThenBy(c => c.cardName));
            var footer = AddFooter(screen.panel.transform);
            AddButton(footer, BackButtonName, "Back", BackWidth, () => ShowDeck(false));
            return screen.panel;
        }

        /// <summary>Opens a pack to choose from (null: back to the rewards). It can be opened again until a card is taken or it's skipped.</summary>
        public void OpenRewardPack(AdventureRun.RewardPack pack) {
            OpenPack = pack != null && !pack.Done ? pack : null;
            Show();
        }

        /// <summary>Shows your deck, or goes back from it.</summary>
        public void ShowDeck(bool show) {
            ViewingDeck = show;
            Show();
        }

        /// <summary>Takes the card from the open pack into your deck, back to the rewards.</summary>
        public void TakeReward(CardDataScriptableObject card) {
            if (!AdventureRun.TakeReward(OpenPack, card)) return;
            lastWalk = $"{card.cardName} joins your deck.";
            OpenRewardPack(null);
        }

        /// <summary>Leaves the open pack's cards, back to the rewards.</summary>
        public void SkipReward() {
            AdventureRun.SkipReward(OpenPack);
            OpenRewardPack(null);
        }

        /// <summary>On to the map; packs not opened are left behind.</summary>
        public void ProceedFromRewards() {
            AdventureRun.Proceed();
            OpenPack = null;
            if (lastWalk == null || !lastWalk.EndsWith("joins your deck.")) lastWalk = "You leave their cards behind.";
            Show();
        }
        #endregion

        private string StatusText() {
            if (AdventureRun.Won) return "<b>The boss is beaten.</b> Your adventure is won!"
                + (AdventureRun.AscensionUnlocked > 0 ? $" Ascension {AdventureRun.AscensionUnlocked} is open for {Umbrellas.Name(AdventureRun.Umbrella)}." : "");
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
            OpenPack = null;
            ViewingDeck = Removing = false;
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
            RoomType.Unknown => "an event",
            RoomType.Rest => "heal, or upgrade a card",
            RoomType.Shop => "cards and potions",
            RoomType.Treasure => "a relic",
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
