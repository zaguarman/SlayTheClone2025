using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using static SlayTheClone.Enums;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Cards move between deck, hand, board and discard pile without being lost, duplicated or left in two places:
    /// dealing, drawing, discarding, reshuffling, casting, dying and summoning from the deck.
    /// Runs the real rules without the scene (see <see cref="TestGame"/>).
    /// </summary>
    public class CardCycleTests {
        private TestGame game;
        private Player p1;
        private Player p2;

        [SetUp]
        public void SetUp() {
            game = new TestGame();
            p1 = game.Player1;
            p2 = game.Player2;
        }

        [TearDown]
        public void TearDown() => game.Dispose();

        // An empty board for the next part of a test
        private void Fresh() {
            game.Dispose();
            SetUp();
        }

        [Test]
        public void Drawing_StopsAtTheHandLimit_AndSummoningFromTheDeck_TakesOnlyCreatures_IntoEmptySlots() {
            game.SetDecks(game.Fillers(15), game.Fillers(1));

            game.Run(new DrawCardsAction(p1, 12));

            Assert.AreEqual(Player.MaxHandSize, p1.Hand.Count);
            Assert.AreEqual(15 - Player.MaxHandSize, p1.Deck.CardsRemaining);
            Assert.AreEqual(15, GameAsserts.CountCards(p1), "Cards were lost");

            Fresh();
            var rally = game.Spell("Rally", TargetType.Player);
            var cry = game.Spell("Cry", TargetType.Player);
            var bear = game.Creature("Bear", 2, 2);
            game.SetDecks(new List<CardData> { rally, cry, bear }, game.Fillers(1));
            for (int i = 0; i < 4; i++) game.Summon(p1, game.Creature($"Guard {i}", 1, 1), i);

            SummonFromDeckEffect.QueueRandomSummons(p1, 3, game.ActionsQueue);
            game.Resolve();

            Assert.AreEqual("Bear", p1.Battlefield[4].OccupyingCreature?.Name, "The one creature should fill the one empty slot");
            CollectionAssert.AreEquivalent(new[] { "Rally", "Cry" }, p1.Deck.GetDeckPreview().Select(c => c.Name));
            CollectionAssert.IsEmpty(p1.Deck.GetDiscardPilePreview(), "Nothing on the board was replaced");

            SummonFromDeckEffect.QueueRandomSummons(p1, 1, game.ActionsQueue);
            game.Resolve();
            Assert.AreEqual(2, p1.Deck.CardsRemaining, "With the board full nothing is summoned");
        }

        [UnityTest]
        public IEnumerator EndingATurn_DiscardsBothHandsAndDrawsFiveEach() {
            game.SetDecks(game.Fillers(20), game.Fillers(20));
            game.CardDealingService.DealInitialHands(p1, p2, 5);
            var firstHands = new[] { p1, p2 }.ToDictionary(p => p, p => p.Hand.ToList());
            game.Summon(p1, game.Creature("Crowd", 1, 1), 4);
            var sketchbook = game.GiveCard(p1, game.Spell("Sketchbook", TargetType.Player, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Draw, 2, TargetType.Player)));
            game.Play(sketchbook, p1, p1.Battlefield[0]);
            // The Zeitgeist World: each Day whoever has fewer creatures draws 1 more card. Played on either side, it's everyone's
            var zeitgeistCard = game.Spell("Zeitgeist", TargetType.Player, TestCards.WorldEffect(World.Zeitgeist, 2));
            var zeitgeist = game.GiveCard(p2, zeitgeistCard);
            game.Play(zeitgeist, p2, p1.Battlefield[1]);

            yield return game.EndTurn();

            Assert.AreEqual(World.Zeitgeist, game.WorldSystem.Current, "A World for 2 Days lasts this Day and the next");
            Assert.AreEqual("World", CardTextFormatter.GetTypeLine(zeitgeistCard), "A card that only sets a World is a World");
            foreach (var player in new[] { p1, p2 }) {
                int drawn = player == p1 ? 7 : 6; // The underdog (no creatures) draws 1 more
                Assert.AreEqual(drawn, player.Hand.Count, "A draw resolved during the Day comes with the next Day's hand, not into the hand about to be discarded");
                CollectionAssert.IsEmpty(player.Hand.Intersect(firstHands[player]), "The old hand should have been discarded");
                Assert.AreEqual(20 - 5 - drawn, player.Deck.CardsRemaining);
            }
            Assert.AreEqual(6, p2.Deck.GetDiscardPilePreview().Count, "Its 5 cards and the World it played");
            Assert.AreEqual(0, p1.ExtraDraws, "The owed cards were drawn");

            yield return game.EndTurn();
            Assert.AreEqual(World.None, game.WorldSystem.Current, "Over after its last Day's Dusk");
            Assert.AreEqual(5, p2.Hand.Count, "No more extra card");
        }
    }
}
