using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static SlayTheClone.Enums;
using Object = UnityEngine.Object;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The main menu as a player uses it: Play opens the match setup, where you pick your deck, an opponent and their deck,
    /// and Start begins a game with exactly that. Buttons are clicked the way the menu's own listeners expect.
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
        public IEnumerator PickingAnOpponent_BringsTheirDeck_WhichCanBeSwapped_AndBackKeepsTheChoices() {
            yield return OpenSetup();

            foreach (var opponent in catalog.Opponents) {
                Click(SetupButton(MatchSetupMenu.OpponentButton + opponent.name));
                Assert.AreSame(opponent, setup.Opponent);
                Assert.AreSame(opponent.deck, setup.OpponentDeck, $"{opponent.name} should bring their own deck");
                AssertOnlyChoicesHighlighted();
            }

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
            var decks = catalog.Decks;
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
