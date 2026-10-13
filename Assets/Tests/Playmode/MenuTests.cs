using System.Collections;
using System.Collections.Generic;
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
    /// and Start begins a game with exactly that; Cards opens the card gallery, Keywords the keyword glossary. Buttons are clicked the way the menu's own
    /// listeners expect.
    /// </summary>
    public class MenuTests {
        private const string MenuSceneName = "MainMenu";
        private const int Seed = 1234;

        private SceneGame game;
        private MatchSetupMenu setup;
        private DeckCatalog catalog;

        [UnitySetUp]
        public IEnumerator SetUp() {
            MatchSetup.Clear();
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
        public void TearDown() => game.Dispose();

        [UnityTest]
        public IEnumerator TheCardGallery_ShowsEveryDeck_TheGlossaryEveryKeyword_AndPickingAnOpponent_BringsTheirDeck_WhichCanBeSwapped_AndBackKeepsTheChoices() {
            Click(TitleButton(MainMenu.CardsButtonName));
            var gallery = Object.FindFirstObjectByType<CardGalleryMenu>();
            Assert.IsTrue(gallery.IsOpen, "Cards opens the card gallery");
            Assert.IsFalse(TitleButton("PlayButton").gameObject.activeInHierarchy, "...in place of the title screen");
            CollectionAssert.AreEqual(catalog.AllCards.Select(c => c.cardName), Shown(gallery), "It opens on every card on offer");
            foreach (var deck in catalog.AllDecks) {
                Click(GalleryButton(gallery, CardGalleryMenu.DeckButton + deck.GetDeckName()));
                CollectionAssert.AreEquivalent(deck.Cards.Distinct().Select(c => c.cardName), Shown(gallery), $"{deck.GetDeckName()}: each of its cards once");
            }
            Assert.IsNotNull(gallery.GetComponentInChildren<WheelStepper>(), "The mouse wheel steps through the deck list");
            gallery.StepDeck(-99);
            Assert.IsNull(gallery.ShownDeck, "Wheel up to the top: Every card");
            gallery.StepDeck(1);
            Assert.AreSame(catalog.AllDecks[0], gallery.ShownDeck, "One tick down: the first deck");
            Click(GalleryButton(gallery, CardGalleryMenu.BackButtonName));
            Assert.IsFalse(gallery.IsOpen);
            Assert.IsTrue(TitleButton("PlayButton").gameObject.activeInHierarchy, "Back shows the title screen again");

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
            glossary.ShowKeyword(KeyOf(listed[0]));
            glossary.Step(1);
            Assert.AreEqual(KeyOf(listed[1]), glossary.Shown, "A tick of the wheel down goes to the next keyword");
            glossary.Step(-1);
            Assert.AreEqual(KeyOf(listed[0]), glossary.Shown, "...up, to the previous one");
            glossary.Step(-1);
            Assert.AreEqual(KeyOf(listed[0]), glossary.Shown, "...and it stops at the ends");
            glossary.Step(listed.Count);
            Assert.AreEqual(KeyOf(listed.Last()), glossary.Shown);
            yield return null;
            var view = ((RectTransform)list.viewport).rect;
            var last = (RectTransform)listed.Last().transform;
            Vector2 lastCenter = list.viewport.InverseTransformPoint(last.TransformPoint(last.rect.center));
            Assert.IsTrue(view.Contains(lastCenter), "The list follows, so the chosen keyword is always in view");

            Click(listed.Single(b => b.name == KeywordGlossaryMenu.KeywordButton + "Exhausted"));
            var exhausting = glossary.CardsWith("Exhausted").Select(c => c.cardName).ToList();
            CollectionAssert.Contains(exhausting, "DDOS Attack", "The cards that print it are read from the cards themselves");
            CollectionAssert.IsSubsetOf(exhausting.Select(n => "card:" + n), Keywords.LinksIn(glossary.UsedBy.text), "...and listed as links");
            glossary.ShowCardPreview("DDOS Attack", new Vector2(200f, 200f));
            Assert.AreEqual("DDOS Attack", glossary.CardPreview?.GetBaseCardData().cardName, "Hovering a card's name shows the card");
            glossary.ShowCardPreview(null);
            Assert.IsNull(glossary.CardPreview);
            Click(GalleryButton(glossary, KeywordGlossaryMenu.BackButtonName));
            Assert.IsFalse(glossary.IsOpen);

            yield return OpenSetup();

            foreach (var opponent in catalog.Opponents) {
                Click(SetupButton(MatchSetupMenu.OpponentButton + opponent.name));
                Assert.AreSame(opponent, setup.Opponent);
                Assert.AreSame(opponent.deck, setup.OpponentDeck, $"{opponent.name} should bring their own deck");
                AssertOnlyChoicesHighlighted();
            }

            Assert.AreEqual(3, setup.GetComponentsInChildren<WheelStepper>().Length, "The mouse wheel steps through each column");
            setup.StepOpponent(-99);
            Assert.AreSame(catalog.Opponents[0], setup.Opponent, "Wheel up to the top of the opponents");
            setup.StepOpponent(1);
            Assert.AreSame(catalog.Opponents[1], setup.Opponent, "One tick down: the next opponent...");
            Assert.AreSame(catalog.Opponents[1].deck, setup.OpponentDeck, "...who brings their deck");
            setup.StepPlayerDeck(1);
            setup.StepPlayerDeck(-1);
            AssertOnlyChoicesHighlighted();

            var chosenOpponent = setup.Opponent;
            var otherDeck = catalog.Decks.First(d => d != chosenOpponent.deck);
            Click(SetupButton(MatchSetupMenu.OpponentDeckButton + otherDeck.GetDeckName()));
            Assert.AreSame(otherDeck, setup.OpponentDeck);
            Assert.AreSame(chosenOpponent, setup.Opponent, "Swapping their deck keeps the opponent");
            AssertOnlyChoicesHighlighted();

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
            Assert.AreSame(otherDeck, setup.OpponentDeck);
            AssertOnlyChoicesHighlighted();
        }

        [UnityTest]
        public IEnumerator Start_BeginsTheGame_WithYourDeck_TheOpponentAndTheirDeck() {
            yield return OpenSetup();
            var yourDeck = catalog.Decks.Last();
            var opponent = catalog.Opponents.Last();
            var theirDeck = catalog.Decks.First(d => d != opponent.deck && d != yourDeck);
            Click(SetupButton(MatchSetupMenu.PlayerDeckButton + yourDeck.GetDeckName()));
            Click(SetupButton(MatchSetupMenu.OpponentButton + opponent.name));
            Click(SetupButton(MatchSetupMenu.OpponentDeckButton + theirDeck.GetDeckName()));

            yield return game.Start(Seed, () => Click(SetupButton(MatchSetupMenu.StartButtonName)));

            SceneGame.AssertPlays(game.Player1, yourDeck);
            SceneGame.AssertPlays(game.Player2, theirDeck);
            Assert.AreEqual(opponent.name, game.Player2.Name, "Player 2 should be the chosen opponent");
            Assert.AreSame(yourDeck, game.References.Player1Deck);
            Assert.AreSame(theirDeck, game.References.Player2Deck);
            foreach (var player in game.Players) {
                Assert.AreEqual(5, player.Hand.Count, $"{player.Name} should start with a full hand");
                GameAsserts.AssertEachCardInOneZone(player);
            }
            game.AssertViewsMatchRules();
        }

        /// <summary>
        /// Every deck on offer plays all its creatures, spells and attacks for several turns without an error, while
        /// the screen keeps showing what the rules hold and no card is lost, duplicated or in two places.
        /// Each game pits two decks against each other, so half as many games as decks cover them all.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryDeckOnOffer_PlaysSeveralTurns_WithTheScreenInStepWithTheRules() {
            var decks = catalog.AllDecks; // The test decks too
            for (int i = 0; i < decks.Count; i += 2) {
                var yourDeck = decks[i];
                var theirDeck = decks[(i + 1) % decks.Count];
                yield return game.Load(Seed, yourDeck, "Test Opponent", theirDeck);
                SceneGame.AssertPlays(game.Player1, yourDeck);
                SceneGame.AssertPlays(game.Player2, theirDeck);

                var cardCounts = game.Players.ToDictionary(p => p, GameAsserts.CountCards);
                for (int turn = 1; turn <= 3; turn++) {
                    foreach (var player in game.Players) PlayEverything(player);
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
            }
        }

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

        private static List<string> Shown(CardGalleryMenu gallery) => gallery.ShownCards.Select(c => c.GetBaseCardData().cardName).ToList();

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
            AssertHighlighted(MatchSetupMenu.OpponentDeckButton, MatchSetupMenu.OpponentDeckButton + setup.OpponentDeck.GetDeckName());
        }

        private void AssertHighlighted(string column, string chosen) {
            var buttons = setup.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith(column)).ToList();
            var chosenColor = buttons.Single(b => b.name == chosen).image.color;
            foreach (var other in buttons.Where(b => b.name != chosen)) {
                Assert.AreNotEqual(chosenColor, other.image.color, $"{other.name} looks chosen too");
                Assert.AreEqual(buttons.First(b => b.name != chosen).image.color, other.image.color, $"{other.name} looks different from the other unchosen buttons");
            }
        }

        // Creatures go to free slots; spells are dropped on the opponent's first slot, or on your own for spells that help you
        private void PlayEverything(Player player) {
            game.PlayCreatures(player);
            foreach (var spell in player.Hand.OfType<Spell>().ToList()) {
                bool helpsYou = spell.DefaultTargetType is TargetType.FriendlyCreatures or TargetType.Player;
                game.Play(spell, player, (helpsYou ? player : player.Opponent).Battlefield[0]);
            }
            foreach (var slot in player.Battlefield.Where(s => s.IsOccupied())) {
                game.Attack(slot.OccupyingCreature, player.Opponent.Battlefield[slot.Index]);
            }
        }
        #endregion
    }
}
