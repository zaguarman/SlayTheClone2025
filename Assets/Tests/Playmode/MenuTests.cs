using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static SlayTheClone.Enums;
using Object = UnityEngine.Object;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The main menu as a player uses it: Play opens the match setup, where you pick your deck, an opponent and their deck,
    /// and Start begins a game with exactly that; Cards opens the card gallery, Decks the deck builder, Keywords the keyword
    /// glossary. Buttons are clicked the way the menu's own listeners expect. Your decks go to a file of the tests' own.
    /// </summary>
    public class MenuTests {
        private const string MenuSceneName = "MainMenu";
        private const int Seed = 1234;

        private SceneGame game;
        private MatchSetupMenu setup;
        private DeckCatalog catalog;

        private static string DecksFile => Path.Combine(Application.temporaryCachePath, "test-decks.json");

        [UnitySetUp]
        public IEnumerator SetUp() {
            MatchSetup.Clear();
            AdventureRun.Clear();
            if (File.Exists(DecksFile)) File.Delete(DecksFile);
            PlayerDecks.FilePath = DecksFile;
            yield return SceneManager.LoadSceneAsync(MenuSceneName, LoadSceneMode.Single);
            setup = Object.FindFirstObjectByType<MatchSetupMenu>();
            Assert.IsNotNull(setup, "The main menu has no match setup");
            catalog = setup.Catalog;
            Assert.IsNotNull(catalog, "The match setup has no deck catalog");
            Assert.GreaterOrEqual(catalog.Decks.Count, 2, "The catalog should offer a choice of decks");
            Assert.GreaterOrEqual(catalog.Opponents.Count, 2, "The catalog should offer a choice of opponents");
            game = new SceneGame();
        }

        [TearDown]
        public void TearDown() {
            game.Dispose();
            if (File.Exists(DecksFile)) File.Delete(DecksFile);
            PlayerDecks.Forget();
        }

        [UnityTest]
        public IEnumerator TheCardGallery_ShowsEveryFaction_TheGlossaryEveryKeyword_BothOpeningAsNew_AndPickingAnOpponent_BringsTheirDeck_EveryListScrolls_AndBackKeepsTheChoices_AnAdventureWalksAMap() {
            Click(TitleButton(MainMenu.CardsButtonName));
            var gallery = Object.FindFirstObjectByType<CardGalleryMenu>();
            Assert.IsTrue(gallery.IsOpen, "Cards opens the card gallery");
            Assert.IsFalse(TitleButton("PlayButton").gameObject.activeInHierarchy, "...in place of the title screen");
            CollectionAssert.AreEquivalent(catalog.AllCards.Select(c => c.cardName), Shown(gallery), "It opens on every card on offer");
            CollectionAssert.AreEquivalent(catalog.AllCards.Select(c => c.faction).Distinct(), gallery.Factions, "...and lists their factions");
            foreach (var faction in gallery.Factions) {
                Click(GalleryButton(gallery, CardGalleryMenu.FactionButton + CardTextFormatter.FactionName(faction)));
                var cards = catalog.AllCards.Where(c => c.faction == faction).ToList();
                CollectionAssert.AreEquivalent(cards.Select(c => c.cardName), Shown(gallery), $"{faction}: each of its cards once");
                CollectionAssert.AreEqual(cards.Select(c => c is CreatureCardScriptableObject).OrderByDescending(isCreature => isCreature),
                    gallery.ShownCards.Select(c => c.GetBaseCardData() is CreatureData), "...creatures first, then spells");
            }
            AssertTheWheelScrollsEveryList(gallery);
            yield return null;
            CardsScroll(gallery).verticalNormalizedPosition = 0f;
            Click(GalleryButton(gallery, CardGalleryMenu.BackButtonName));
            Assert.IsFalse(gallery.IsOpen);
            Assert.IsTrue(TitleButton("PlayButton").gameObject.activeInHierarchy, "Back shows the title screen again");
            Click(TitleButton(MainMenu.CardsButtonName));
            yield return null;
            Assert.IsNull(gallery.ShownFaction, "It opens as new each time: on every card...");
            Assert.AreEqual(1f, CardsScroll(gallery).verticalNormalizedPosition, 0.01f, "...scrolled to the top");
            Click(GalleryButton(gallery, CardGalleryMenu.BackButtonName));

            Click(TitleButton(MainMenu.KeywordsButtonName));
            var glossary = Object.FindFirstObjectByType<KeywordGlossaryMenu>();
            Assert.IsTrue(glossary.IsOpen, "Keywords opens the glossary");
            var listed = glossary.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith(KeywordGlossaryMenu.KeywordButton)).ToList();
            Assert.AreEqual(Keywords.All.Count(), listed.Count, "It lists every keyword");
            Click(listed.Single(b => b.name == KeywordGlossaryMenu.KeywordButton + "Exhausted"));
            Assert.AreEqual("Exhausted", glossary.Shown);
            yield return null;
            var point = SceneGame.KeywordPoint(glossary.Body, "Exposed");
            ExecuteEvents.Execute(glossary.Body.gameObject, new PointerEventData(EventSystem.current) { position = point }, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual("Exposed", glossary.Shown, "Clicking a keyword in an explanation shows that keyword");

            var list = glossary.GetComponentsInChildren<ScrollRect>().Single();
            float width = ((RectTransform)list.transform).rect.width;
            Click(listed.Single(b => b.name == KeywordGlossaryMenu.KeywordButton + "Random"));
            yield return null;
            Assert.AreEqual(width, ((RectTransform)list.transform).rect.width, 0.5f, "The keyword list keeps its width whatever is shown");
            string KeyOf(Button b) => b.name.Substring(KeywordGlossaryMenu.KeywordButton.Length);
            AssertTheWheelScrollsEveryList(glossary);
            glossary.ShowKeyword(KeyOf(listed.Last()));
            yield return null;
            var view = ((RectTransform)list.viewport).rect;
            var last = (RectTransform)listed.Last().transform;
            Vector2 lastCenter = list.viewport.InverseTransformPoint(last.TransformPoint(last.rect.center));
            Assert.IsTrue(view.Contains(lastCenter), "The list follows, so the chosen keyword is always in view");

            glossary.Filter.text = "rEvEaL";
            CollectionAssert.AreEquivalent(Keywords.All.Where(e => e.Title.ToLower().Contains("reveal")).Select(e => e.Key), glossary.Listed,
                "Typing in the filter lists only the keywords whose name has it, any case");
            Assert.AreEqual(glossary.Listed[0], glossary.Shown, "...and shows the first one, the one shown being filtered out");
            glossary.Filter.text = "";
            Assert.AreEqual(listed.Count, glossary.Listed.Count, "Empty, it lists them all again");

            Click(listed.Single(b => b.name == KeywordGlossaryMenu.KeywordButton + "Exhausted"));
            var exhausting = glossary.CardsWith("Exhausted").Select(c => c.cardName).ToList();
            CollectionAssert.Contains(exhausting, "DDOS Attack", "The cards that print it are read from the cards themselves");
            CollectionAssert.IsSubsetOf(exhausting.Select(n => "card:" + n), Keywords.LinksIn(glossary.UsedBy.text), "...and listed as links");
            glossary.ShowCardPreview("DDOS Attack", new Vector2(200f, 200f));
            Assert.AreEqual("DDOS Attack", glossary.CardPreview?.GetBaseCardData().cardName, "Hovering a card's name shows the card");
            Canvas.ForceUpdateCanvases(); // Laid out now: the next frame, the pointer isn't on the name any more
            var preview = (RectTransform)glossary.CardPreview.transform;
            Assert.Greater(preview.rect.height, preview.rect.width * 1.2f, "...as a card, not stretched across the menu");
            var corners = new Vector3[4];
            preview.GetWorldCorners(corners);
            Assert.IsTrue(corners.All(c => c.x >= 0 && c.y >= 0 && c.x <= Screen.width && c.y <= Screen.height), "...and all of it on screen");
            glossary.ShowCardPreview(null);
            Assert.IsNull(glossary.CardPreview);
            Click(GalleryButton(glossary, KeywordGlossaryMenu.BackButtonName));
            Assert.IsFalse(glossary.IsOpen);
            Click(TitleButton(MainMenu.KeywordsButtonName));
            Assert.AreEqual(Keywords.All.First().Key, glossary.Shown, "It opens as new each time: on the first keyword");
            Click(GalleryButton(glossary, KeywordGlossaryMenu.BackButtonName));

            yield return OpenSetup();
            Assert.AreSame(setup.Decks[0], setup.PlayerDeck, "By default you play the deck being worked on (the Workbench)...");
            Assert.AreSame(setup.Decks.Skip(1).First(d => catalog.Opponents.Any(o => o.deck == d)), setup.OpponentDeck, "...against the next one in focus");
            Assert.AreSame(setup.OpponentDeck, setup.Opponent.deck, "...brought by its opponent");
            var firstDeck = SetupButton(MatchSetupMenu.PlayerDeckButton + catalog.Decks[0].GetDeckName());
            Assert.IsNotNull(firstDeck.transform.Find("Factions/FactionCoin"), "A deck shows its faction's coin at its left");

            foreach (var opponent in catalog.Opponents) {
                Click(SetupButton(MatchSetupMenu.OpponentButton + opponent.name));
                Assert.AreSame(opponent, setup.Opponent);
                Assert.AreSame(opponent.deck, setup.OpponentDeck, $"{opponent.name} should bring their own deck");
                AssertOnlyChoicesHighlighted();
            }

            Assert.AreEqual(2, setup.GetComponentsInChildren<ScrollRect>().Length, "Two columns: your deck and your opponent, who brings theirs");
            AssertTheWheelScrollsEveryList(setup);
            var decksList = setup.GetComponentsInChildren<ScrollRect>()[0];
            decksList.verticalNormalizedPosition = 1f;
            decksList.GetComponent<WheelScroll>().ScrollBy(-1f);
            Assert.Less(decksList.verticalNormalizedPosition, 1f, "...the long list of decks moves down a tick of the wheel");
            var chosenOpponent = catalog.Opponents[1];
            Click(SetupButton(MatchSetupMenu.OpponentButton + chosenOpponent.name));

            var yourDeck = catalog.Decks.Last();
            Click(SetupButton(MatchSetupMenu.PlayerDeckButton + yourDeck.GetDeckName()));
            Click(SetupButton(MatchSetupMenu.BackButtonName));
            yield return null;
            Assert.IsFalse(setup.IsOpen);
            Assert.IsTrue(TitleButton("PlayButton").gameObject.activeInHierarchy, "Back should show the title screen again");
            Assert.IsFalse(MatchSetup.IsChosen, "Going back doesn't choose a match");

            yield return OpenSetup();
            Assert.AreSame(yourDeck, setup.PlayerDeck, "Play should reopen the setup with the same choices");
            Assert.AreSame(chosenOpponent, setup.Opponent);
            AssertOnlyChoicesHighlighted();

            // Adventure: a faction picked by its coin, then a map built like Slay the Spire 2's, walked room by room
            Click(SetupButton(MatchSetupMenu.BackButtonName));
            yield return null;
            Click(TitleButton(MainMenu.AdventureButtonName));
            var adventure = Object.FindFirstObjectByType<AdventureMenu>();
            Assert.IsTrue(adventure.IsOpen, "Adventure asks for a faction first");
            CollectionAssert.AreEquivalent(catalog.Opponents.Select(o => DeckCatalog.FactionOf(o.deck)).Distinct(), adventure.Factions.Select(f => f.faction), "...each once");
            var yours = adventure.Factions[1].faction;
            Click(GalleryButton(adventure, AdventureMenu.FactionButton + CardTextFormatter.FactionName(yours)));
            Click(GalleryButton(adventure, AdventureMenu.EmbarkButtonName));
            Assert.IsTrue(AdventureRun.Active && AdventureRun.Faction == yours, "Embark starts a run with it...");
            Assert.AreEqual(AdventureDecks.StarterSize, AdventureRun.Deck.Cards.Count, "...and a small starter deck...");
            Assert.IsTrue(AdventureRun.Deck.Cards.All(c => c.faction == yours) && AdventureRun.Deck.Cards.Any(c => c is CreatureCardScriptableObject)
                && AdventureRun.Deck.Cards.Any(c => c is not CreatureCardScriptableObject), "...of its faction, creatures and spells");
            var map = AdventureRun.Map;
            Assert.IsTrue(map.Row(0).All(n => n.Type == RoomType.Fight) && map.Row(AdventureMap.TreasureRow).All(n => n.Type == RoomType.Treasure)
                && map.Row(AdventureMap.Rows - 1).All(n => n.Type == RoomType.Rest), "The first row all fights, the one 7 from the end all treasure, the last all rest sites");
            Assert.GreaterOrEqual(map.Row(0).Count(), 2, "At least two ways in...");
            Assert.IsTrue(map.Nodes.All(n => n.Next.Count > 0) && map.Boss.Previous.Count == map.Row(AdventureMap.Rows - 1).Count(), "...and every path leads up to the boss");
            Assert.IsFalse(map.Nodes.Any(n => n.Row < AdventureMap.SafeRows && n.Type is RoomType.Elite or RoomType.Rest), "No elite or rest site in the first rows...");
            Assert.IsTrue(map.Nodes.Where(n => n.Row is > 0 and < AdventureMap.Rows - 1 && n.Row != AdventureMap.TreasureRow).All(n => AdventureMap.Allowed(n, n.Type)),
                "...nor two elites, shops or rest sites in a row, nor a fork into the same kind of room");
            Assert.IsFalse(map.Nodes.Any(a => map.Nodes.Any(b => a.Row == b.Row && a.Column < b.Column && a.Next.Any(x => b.Next.Any(y => x.Column > y.Column)))), "Paths never cross");
            Assert.IsTrue(adventure.Rooms.Where(r => r.Value.interactable).All(r => r.Key.Row == 0), "At first only the bottom row can be walked into");

            var first = map.Row(0).First();
            Assert.IsTrue(AdventureRun.Enter(first), "A fight is a match...");
            adventure.SetUpFight(first);
            Assert.AreSame(AdventureRun.Deck, MatchSetup.PlayerDeck, "...your faction's deck...");
            Assert.AreNotEqual(yours, DeckCatalog.FactionOf(MatchSetup.OpponentDeck), "...against another faction's...");
            Assert.AreEqual(AILevel.Easy, MatchSetup.AILevel, "...the first ones easy...");
            var tiers = AdventureDecks.Tiers(catalog.AllCards);
            double TierOf(DeckScriptableObject deck) => deck.Cards.Average(c => (int)tiers[c]);
            var beaten = MatchSetup.OpponentDeck;
            Assert.AreSame(beaten, AdventureRun.EnemyDeck);
            Assert.LessOrEqual(TierOf(beaten), (double)AdventureDecks.Tier.C + 0.5, "...with weak cards (Tier C and D)");
            var bossDeck = AdventureDecks.Enemy(DeckCatalog.FactionOf(beaten), AdventureDecks.StrengthOf(map.Boss), catalog.AllCards, new System.Random(1), "Boss");
            Assert.Greater(TierOf(bossDeck), TierOf(beaten) + 1, "The boss's cards are much better");
            Assert.LessOrEqual(bossDeck.Cards.Count, PlayerDecks.MaxDeckSize);
            AdventureRun.WinFight(); // As the game's end does, before the title screen opens the map again
            adventure.Show();
            Assert.AreEqual(AdventureRun.FightPurse, AdventureRun.EuroYuans, "A fight won pays EuroYuans");
            Assert.AreSame(first, AdventureRun.Current, "Won, you're in its room...");
            Assert.AreEqual(1, AdventureRun.Rewards.Count, "...and it drops a card pack (Slay the Spire's rewards)");
            Assert.IsEmpty(AdventureRun.Reachable(), "No walking on before proceeding");
            Click(GalleryButton(adventure, AdventureMenu.DeckButtonName));
            Assert.AreEqual(AdventureDecks.StarterSize, adventure.DeckCards.Count, "Your deck is a click away");
            Click(GalleryButton(adventure, AdventureMenu.BackButtonName));
            Click(GalleryButton(adventure, AdventureMenu.PackButton + 0));
            Assert.AreEqual(Mathf.Min(3, beaten.Cards.Distinct().Count()), adventure.RewardCards.Count, "Opened, the pack shows three of their cards...");
            Click(GalleryButton(adventure, AdventureMenu.PickBackButtonName));
            Assert.IsEmpty(adventure.RewardCards, "...it can be closed to think...");
            Click(GalleryButton(adventure, AdventureMenu.PackButton + 0));
            var taken = adventure.RewardCards[0].GetBaseCardData().cardName;
            ClickCard(adventure.RewardCards[0]);
            Assert.AreEqual(AdventureDecks.StarterSize + 1, AdventureRun.Deck.Cards.Count, "...and opened again: clicking one takes it into your deck...");
            Assert.IsTrue(AdventureRun.Deck.Cards.Any(c => c.cardName == taken) && beaten.Cards.Any(c => c.cardName == taken), "...a card of the deck you beat");
            Assert.IsFalse(GalleryButton(adventure, AdventureMenu.PackButton + 0).interactable, "The pack is used up");
            Click(GalleryButton(adventure, AdventureMenu.ProceedButtonName));
            Assert.IsFalse(AdventureRun.InRewards, "Proceed: on to the map");
            var walk = first.Next.FirstOrDefault(n => !n.IsCombat);
            if (walk != null) {
                Click(adventure.Rooms[walk]);
                Assert.AreSame(walk, AdventureRun.Current, "Any other room is walked through");
            }
            Assert.IsTrue(adventure.Rooms.Where(r => r.Value.interactable).All(r => AdventureRun.Current.Next.Contains(r.Key)), "Then only the rooms its paths lead to");

            // On to a shop, the fights on the way won
            var way = new Dictionary<MapNode, MapNode>();
            var search = new Queue<MapNode>(new[] { AdventureRun.Current });
            MapNode shop = null;
            while (search.Count > 0 && shop == null) {
                var node = search.Dequeue();
                foreach (var next in node.Next.Where(n => !way.ContainsKey(n))) {
                    way[next] = node;
                    if (next.Type == RoomType.Shop) shop = next;
                    search.Enqueue(next);
                }
            }
            if (shop != null) {
                var steps = new List<MapNode>();
                for (var node = shop; node != AdventureRun.Current; node = way[node]) steps.Insert(0, node);
                foreach (var step in steps) {
                    if (step == shop) break;
                    if (AdventureRun.Enter(step, catalog.AllCards)) {
                        AdventureRun.WinFight();
                        AdventureRun.Proceed();
                    }
                    if (AdventureRun.InShop) AdventureRun.LeaveShop();
                }
                int purse = AdventureRun.EuroYuans + 30; // Enough to buy and remove
                typeof(AdventureRun).GetProperty(nameof(AdventureRun.EuroYuans)).SetValue(null, purse);
                adventure.Show();
                Click(adventure.Rooms[shop]);
                Assert.IsTrue(AdventureRun.InShop, "A shop opens...");
                Assert.AreEqual(AdventureRun.ShopCards, adventure.ShopCards.Count, "...its cards on show...");
                Assert.AreEqual(AdventureRun.ShopOwnFaction, AdventureRun.Shop.Count(o => o.Card.faction == yours), "...most of your faction");
                Assert.IsEmpty(AdventureRun.Reachable(), "No walking on from a shop before leaving it");
                var cheapest = AdventureRun.Shop.OrderBy(o => o.Price).First();
                int size = AdventureRun.Deck.Cards.Count;
                ClickCard(adventure.ShopCards[AdventureRun.Shop.IndexOf(cheapest)]);
                Assert.IsTrue(cheapest.Sold && AdventureRun.Deck.Cards.Count == size + 1 && AdventureRun.EuroYuans == purse - cheapest.Price, "Clicking a card buys it, paid");
                int cost = AdventureRun.RemovalCost;
                purse = AdventureRun.EuroYuans;
                Click(GalleryButton(adventure, AdventureMenu.RemoveCardButtonName));
                Assert.IsTrue(adventure.Removing && adventure.DeckCards.Count == size + 1, "Remove a card shows your deck...");
                ClickCard(adventure.DeckCards[0]);
                Assert.IsTrue(AdventureRun.Deck.Cards.Count == size && AdventureRun.EuroYuans == purse - cost && !adventure.Removing, "...a click takes that one out, paid...");
                Assert.IsFalse(GalleryButton(adventure, AdventureMenu.RemoveCardButtonName).interactable, "...once a shop");
                Click(GalleryButton(adventure, AdventureMenu.LeaveShopButtonName));
                Assert.IsFalse(AdventureRun.InShop, "Leave: on to the map");
                Assert.IsNotEmpty(AdventureRun.Reachable());
            }
            Click(GalleryButton(adventure, AdventureMenu.AbandonButtonName));
            Assert.IsTrue(AdventureRun.Active, "Abandon asks to be sure first...");
            Click(GalleryButton(adventure, AdventureMenu.AbandonButtonName));
            Assert.IsFalse(AdventureRun.Active, "...then ends the run");
            Assert.AreEqual(0, AdventureRun.EuroYuans, "...and its money goes with it");
            MatchSetup.Clear();
        }

        [UnityTest]
        public IEnumerator YourOwnDeck_MixingFactions_IsKept_OfferedOnceComplete_AndStartBeginsTheGame_WithIt_TheOpponentAndTheirDeck_TheAIPlayingTheSeatsChosen() {
            Click(TitleButton(MainMenu.DecksButtonName));
            var builder = Object.FindFirstObjectByType<DeckBuilderMenu>();
            Assert.IsTrue(builder.IsOpen, "Decks opens the deck builder");
            AssertTheWheelScrollsEveryList(builder);
            Assert.IsNull(builder.OpenDeck, "No decks of yours yet");
            var factions = catalog.AllCards.Select(c => c.faction).Distinct().ToList();
            Click(GalleryButton(builder, DeckBuilderMenu.FactionTab + CardTextFormatter.FactionName(factions[0])));
            var first = builder.ShownCards[0];
            string firstName = first.GetBaseCardData().cardName;
            for (int i = 0; i < 3; i++) ClickCard(first);
            var yourDeck = builder.OpenDeck;
            Assert.IsNotNull(yourDeck, "Clicking a card starts a deck with it");
            Assert.AreEqual(3, yourDeck.Cards.Count, "...and adds a copy at each click: copies aren't limited");
            Click(GalleryButton(builder, DeckBuilderMenu.FactionTab + CardTextFormatter.FactionName(factions[1])));
            foreach (var card in builder.ShownCards.ToList()) ClickCard(card);
            Click(GalleryButton(builder, DeckBuilderMenu.EveryFactionTab));
            foreach (var card in builder.ShownCards.ToList()) ClickCard(card);
            Assert.AreEqual(PlayerDecks.MaxDeckSize, yourDeck.Cards.Count, "Cards are added until the deck has 40, not beyond");
            Assert.GreaterOrEqual(DeckCatalog.FactionsOf(yourDeck).Count, 2, "...whatever their faction");
            var name = builder.NameField;
            Assert.AreEqual(PlayerDecks.MaxNameLength, name.characterLimit, "A deck's name is 20 characters at most...");
            name.text = "The Longest Deck Name Ever Typed";
            name.onEndEdit.Invoke(name.text);
            Assert.AreEqual("The Longest Deck Nam", yourDeck.GetDeckName(), "...even pasted");
            name.text = "Mixtape";
            name.onEndEdit.Invoke(name.text);
            Assert.AreEqual("Mixtape", yourDeck.GetDeckName(), "Typing a name renames it");
            Click(GalleryButton(builder, DeckBuilderMenu.CardRow + firstName));
            Assert.AreEqual(PlayerDecks.MaxDeckSize - 1, yourDeck.Cards.Count, "Clicking a card in the deck takes a copy out");

            Click(GalleryButton(builder, DeckBuilderMenu.CopyButtonName));
            Assert.AreEqual("Mixtape 2", builder.OpenDeck.GetDeckName(), "Copy makes another deck, opened, with a name of its own");
            Click(GalleryButton(builder, DeckBuilderMenu.DeleteButtonName));
            Assert.AreEqual(2, builder.Decks.Count, "Delete asks to be sure first...");
            Click(GalleryButton(builder, DeckBuilderMenu.DeleteButtonName));
            Assert.AreEqual(1, builder.Decks.Count, "...then deletes it");
            var starter = catalog.Decks[0];
            Click(GalleryButton(builder, DeckBuilderMenu.StarterDeckButton + starter.GetDeckName()));
            Assert.AreSame(starter, builder.OpenDeck, "A starter deck can be looked at...");
            Assert.IsFalse(builder.NameField.interactable, "...not changed...");
            Click(GalleryButton(builder, DeckBuilderMenu.CopyButtonName));
            Assert.AreEqual(2, builder.Decks.Count, "...and copied, to start a deck of your own from it");
            CollectionAssert.AreEqual(starter.Cards, builder.OpenDeck.Cards);
            Click(GalleryButton(builder, DeckBuilderMenu.DeleteButtonName));
            Click(GalleryButton(builder, DeckBuilderMenu.DeleteButtonName));
            Assert.AreSame(yourDeck, builder.OpenDeck);
            while (yourDeck.Cards.Count >= PlayerDecks.MinDeckSize) builder.RemoveCard(yourDeck.Cards.Last(c => c.cardName != firstName));
            Click(GalleryButton(builder, DeckBuilderMenu.BackButtonName));

            yield return OpenSetup();
            Assert.IsFalse(setup.Decks.Contains(yourDeck), "A deck under 20 cards isn't offered");
            Click(SetupButton(MatchSetupMenu.BackButtonName));
            Click(TitleButton(MainMenu.DecksButtonName));
            Assert.AreSame(yourDeck, builder.OpenDeck, "The builder opens on your first deck");
            ClickCard(builder.ShownCards.First(c => c.GetBaseCardData().cardName == firstName));
            Assert.IsTrue(PlayerDecks.IsComplete(yourDeck));
            Click(GalleryButton(builder, DeckBuilderMenu.BackButtonName));

            yield return OpenSetup();
            Assert.AreEqual(1, setup.Decks[0].Focus, "The decks being worked on lead, the Workbench first...");
            Assert.AreSame(yourDeck, setup.Decks.First(d => d.Focus == 0), "...then the newest: yours, just finished");
            var opponent = catalog.Opponents.Last();
            var theirDeck = opponent.deck;
            Click(SetupButton(MatchSetupMenu.PlayerDeckButton + yourDeck.GetDeckName()));
            Click(SetupButton(MatchSetupMenu.OpponentButton + opponent.name));
            Assert.AreEqual(AISeats.Player2, setup.AI, "You play against the AI unless you choose otherwise");
            Click(SetupButton(MatchSetupMenu.SeatsButtonName));
            Assert.AreEqual(AISeats.None, setup.AI, "...then hot seat...");
            Click(SetupButton(MatchSetupMenu.SeatsButtonName));
            Assert.AreEqual(AISeats.Both, setup.AI, "...then the AI against itself");
            StringAssert.Contains("AI vs AI", SetupButton(MatchSetupMenu.SeatsButtonName).GetComponentInChildren<TMPro.TMP_Text>().text);

            AIController.TurnPause = 0.1f;
            yield return game.Start(Seed, () => Click(SetupButton(MatchSetupMenu.StartButtonName)));
            Assert.AreEqual(AISeats.Both, MatchSetup.AI);

            SceneGame.AssertPlays(game.Player1, yourDeck);
            SceneGame.AssertPlays(game.Player2, theirDeck);
            Assert.AreEqual(opponent.name, game.Player2.Name, "Player 2 should be the chosen opponent");
            Assert.AreSame(yourDeck, game.References.Player1Deck);
            Assert.AreSame(theirDeck, game.References.Player2Deck);
            foreach (var player in game.Players) {
                // A spell that only shows something resolves as soon as the AI plays it
                Assert.AreEqual(5, player.Hand.Count + player.Deck.GetDiscardPilePreview().Count, $"{player.Name} should start with a full hand");
                GameAsserts.AssertEachCardInOneZone(player);
                Assert.IsFalse(FogOfWar.CanControl(player), "The AI's cards aren't yours to use");
            }

            int day = game.Turns.TurnNumber;
            var cardCounts = game.Players.ToDictionary(p => p, GameAsserts.CountCards);
            yield return Wait.For(() => game.Turns.TurnNumber >= day + 2 && !game.Turns.IsResolving, "the AI to play two Days by itself");

            AIController.SwitchSeats(); // F7: you take Player 2 back
            Assert.AreEqual(AISeats.Player1, MatchSetup.AI);
            Assert.IsFalse(FogOfWar.PlaysAsPlayer1, "The screen shows your seat");
            Assert.IsTrue(FogOfWar.CanControl(game.Player2) && !FogOfWar.CanControl(game.Player1));
            day = game.Turns.TurnNumber;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(day, game.Turns.TurnNumber, "With a seat of yours, you end the turn");
            foreach (var player in game.Players) Assert.AreEqual(cardCounts[player], GameAsserts.CountCards(player), $"{player.Name} lost or gained cards");
            GameAsserts.AssertBoardsConsistent(game.Player1, game.Player2);
            game.AssertViewsMatchRules();

            var cards = yourDeck.Cards.Select(c => c.cardName).ToList();
            PlayerDecks.FilePath = DecksFile; // Read again, as in the next session
            var kept = PlayerDecks.All(catalog).Single();
            Assert.AreEqual("Mixtape", kept.GetDeckName(), "Your decks are kept between sessions");
            CollectionAssert.AreEquivalent(cards, kept.Cards.Select(c => c.cardName));
        }

        /// <summary>
        /// Every deck on offer is played by the AI on both sides for several turns without an error, while the screen keeps
        /// showing what the rules hold and no card is lost, duplicated or in two places; the AI plays cards and attacks.
        /// Each game pits two decks against each other, so half as many games as decks cover them all.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryDeckOnOffer_PlayedByTheAI_ForSeveralTurns_WithTheScreenInStepWithTheRules() {
            var decks = catalog.AllDecks; // The test decks too
            for (int i = 0; i < decks.Count; i += 2) {
                var yourDeck = decks[i];
                var theirDeck = decks[(i + 1) % decks.Count];
                yield return game.Load(Seed, yourDeck, "Test Opponent", theirDeck);
                SceneGame.AssertPlays(game.Player1, yourDeck);
                SceneGame.AssertPlays(game.Player2, theirDeck);

                var cardCounts = game.Players.ToDictionary(p => p, GameAsserts.CountCards);
                // Each game two levels, so every level plays several decks
                var ai = new Dictionary<Player, AIPlanner> {
                    [game.Player1] = new AIPlanner(game.Queue, game.Manager.CombatHandler, (AILevel)(i / 2 % 4), i),
                    [game.Player2] = new AIPlanner(game.Queue, game.Manager.CombatHandler, (AILevel)((i / 2 + 1) % 4), i + 1)
                };
                var played = game.Players.ToDictionary(p => p, p => 0);
                var attacked = game.Players.ToDictionary(p => p, p => 0);
                var couldAttack = game.Players.ToDictionary(p => p, p => 0);
                for (int turn = 1; turn <= Turns; turn++) {
                    foreach (var player in game.Players) {
                        int discarded = player.Deck.GetDiscardPilePreview().Count;
                        couldAttack[player] += player.Battlefield.Count(s => s.OccupyingCreature is ICreature c && c.Attack > 0
                            && Abilities.CanAttack(c) && game.Manager.ModifierManager.CanAct(c));
                        ai[player].PlanDay(player);
                        var plans = game.Queue.GetPendingActions();
                        played[player] += plans.OfType<PlayCardAction>().Count(p => p.GetOwner() == player)
                            + player.Deck.GetDiscardPilePreview().Count - discarded; // Spells that only show something resolve at once
                        attacked[player] += plans.OfType<BattlefieldCombatAction>().Count(a => a.GetAttacker().Owner == player);
                    }
                    yield return null;
                    game.AssertViewsMatchRules(); // Queued plays: ghosts on the boards, spells marked in the hands

                    yield return game.EndTurn();

                    string match = $"{yourDeck.GetDeckName()} vs {theirDeck.GetDeckName()}, turn {turn}";
                    foreach (var player in game.Players) {
                        Assert.AreEqual(cardCounts[player], GameAsserts.CountCards(player), $"{match}: {player.Name} lost or gained cards");
                        GameAsserts.AssertEachCardInOneZone(player);
                    }
                    GameAsserts.AssertBoardsConsistent(game.Player1, game.Player2);
                    game.AssertViewsMatchRules();
                    if (game.Players.Any(p => p.Health == 0)) break;
                }
                foreach (var player in game.Players) {
                    Assert.Greater(played[player], 0, $"{yourDeck.GetDeckName()} vs {theirDeck.GetDeckName()}: the AI played none of {player.Name}'s cards");
                    // Given a couple of chances: with a single one, holding back can be the right call
                    if (couldAttack[player] > 1) Assert.Greater(attacked[player], 0, $"{yourDeck.GetDeckName()} vs {theirDeck.GetDeckName()}: the AI never attacked with {player.Name}'s creatures");
                }
            }
        }

        private const int Turns = 4;

        #region Helpers
        private IEnumerator OpenSetup() {
            Click(TitleButton("PlayButton"));
            yield return null;
            Assert.IsTrue(setup.IsOpen, "Play should open the match setup");
        }

        private static Button TitleButton(string name) {
            var button = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).SingleOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"The main menu has no {name}");
            return button;
        }

        // Every list on the screen scrolls with the wheel when the pointer is over it, whatever is under the pointer
        private static void AssertTheWheelScrollsEveryList(Component screen) {
            var lists = screen.GetComponentsInChildren<ScrollRect>();
            Assert.IsNotEmpty(lists);
            foreach (var list in lists) Assert.IsNotNull(list.GetComponent<WheelScroll>(), $"{screen.name}: the wheel doesn't scroll {list.name}");
        }

        private static List<string> Shown(CardGalleryMenu gallery) => gallery.ShownCards.Select(c => c.GetBaseCardData().cardName).ToList();

        private static ScrollRect CardsScroll(Component screen) => screen.GetComponentsInChildren<ScrollRect>().Single(s => s.name == "Cards");

        // A left click, the way the card itself hears it
        private static void ClickCard(CardController card) =>
            ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);

        private static Button GalleryButton(Component gallery, string name) {
            var button = gallery.GetComponentsInChildren<Button>(includeInactive: true).SingleOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"The card gallery has no button '{name}'");
            return button;
        }

        private Button SetupButton(string name) {
            var button = setup.GetComponentsInChildren<Button>(includeInactive: true).SingleOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"The match setup has no button '{name}'");
            return button;
        }

        // Clicking only works on what the player can see and press
        private static void Click(Button button) {
            Assert.IsTrue(button.gameObject.activeInHierarchy && button.interactable, $"{button.name} can't be clicked");
            button.onClick.Invoke();
        }

        // In each column, the chosen button looks different from all the others, which look alike
        private void AssertOnlyChoicesHighlighted() {
            AssertHighlighted(MatchSetupMenu.PlayerDeckButton, MatchSetupMenu.PlayerDeckButton + setup.PlayerDeck.GetDeckName());
            AssertHighlighted(MatchSetupMenu.OpponentButton, MatchSetupMenu.OpponentButton + setup.Opponent.name);
        }

        private void AssertHighlighted(string column, string chosen) {
            var buttons = setup.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith(column)).ToList();
            var chosenColor = buttons.Single(b => b.name == chosen).image.color;
            foreach (var other in buttons.Where(b => b.name != chosen)) {
                Assert.AreNotEqual(chosenColor, other.image.color, $"{other.name} looks chosen too");
                Assert.AreEqual(buttons.First(b => b.name != chosen).image.color, other.image.color, $"{other.name} looks different from the other unchosen buttons");
            }
        }

        #endregion
    }
}
