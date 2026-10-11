using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

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

        [Test]
        public void Drawing_StopsAtTheHandLimit_AndLeavesTheRestInTheDeck() {
            game.SetDecks(game.Fillers(15), game.Fillers(1));

            game.Run(new DrawCardsAction(p1, 12));

            Assert.AreEqual(Player.MaxHandSize, p1.Hand.Count);
            Assert.AreEqual(15 - Player.MaxHandSize, p1.Deck.CardsRemaining);
            Assert.AreEqual(15, GameAsserts.CountCards(p1), "Cards were lost");
        }

        [Test]
        public void DrawingFromAnEmptyDeck_ShufflesTheDiscardPileBackIn_ThenDrawsWhatThereIs() {
            game.SetDecks(game.Fillers(3), game.Fillers(1));
            game.Run(new DrawCardsAction(p1, 3));
            var firstHand = p1.Hand.ToList();
            game.Run(new DiscardHandAction(p1));

            game.Run(new DrawCardsAction(p1, 2));

            Assert.AreEqual(2, p1.Hand.Count);
            Assert.AreEqual(1, p1.Deck.CardsRemaining);
            Assert.AreEqual(0, p1.Deck.GetDiscardPilePreview().Count);
            CollectionAssert.IsEmpty(p1.Hand.Intersect(firstHand), "Discarded cards come back as fresh copies");
            GameAsserts.AssertEachCardInOneZone(p1);

            game.Run(new DrawCardsAction(p1, 5)); // Only 1 card left, and nothing to reshuffle
            Assert.AreEqual(3, p1.Hand.Count);
            Assert.AreEqual(0, p1.Deck.CardsRemaining);
        }

        [Test]
        public void PlayingACardTwice_OnlyPlaysItOnce() {
            game.SetDecks(game.Fillers(5), game.Fillers(5));
            var wolf = game.GiveCard(p1, game.Creature("Wolf", 2, 3));

            game.ActionsQueue.AddAction(new PlayCardAction(wolf, p1, p1.Battlefield[0]));
            game.ActionsQueue.AddAction(new PlayCardAction(wolf, p1, p1.Battlefield[1]));
            LogAssert.Expect(LogType.Error, new Regex("not in .*hand")); // The second play is refused
            game.Resolve();

            Assert.AreSame(wolf, p1.Battlefield[0].OccupyingCreature);
            Assert.IsFalse(p1.Battlefield[1].IsOccupied(), "The card was played a second time");
            GameAsserts.AssertBoardsConsistent(p1);
        }

        [UnityTest]
        public IEnumerator EndingATurn_DiscardsBothHandsAndDrawsFiveEach() {
            game.SetDecks(game.Fillers(20), game.Fillers(20));
            game.CardDealingService.DealInitialHands(p1, p2, 5);
            var firstHands = new[] { p1, p2 }.ToDictionary(p => p, p => p.Hand.ToList());

            yield return game.EndTurn();

            foreach (var player in new[] { p1, p2 }) {
                Assert.AreEqual(5, player.Hand.Count);
                CollectionAssert.IsEmpty(player.Hand.Intersect(firstHands[player]), "The old hand should have been discarded");
                Assert.AreEqual(5, player.Deck.GetDiscardPilePreview().Count);
                Assert.AreEqual(10, player.Deck.CardsRemaining);
            }
        }

        [Test]
        public void SummonFromDeck_OnlyTakesCreatures_IntoEmptySlots() {
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

        [Test, Category(TestCategories.KnownBug), Description("#44: Deck.RemoveCard matches by CardId, which every copy of a card shares")]
        public void SummonFromDeck_TakesTheSummonedCardOutOfTheDeck() {
            for (int seed = 1; seed <= 10; seed++) {
                using (var g = new TestGame()) {
                    var spider = g.Creature("Spider", 1, 1);
                    g.SetDecks(new List<CardData> { spider, spider, spider, spider }, g.Fillers(1));
                    GameRandom.Reset(seed);

                    SummonFromDeckEffect.QueueRandomSummons(g.Player1, 1, g.ActionsQueue);
                    g.Resolve();

                    var summoned = g.Player1.Battlefield.Single(s => s.IsOccupied()).OccupyingCreature;
                    CollectionAssert.DoesNotContain(g.Player1.Deck.GetDeckPreview(), summoned, $"Seed {seed}: the summoned creature is still in the deck");
                    Assert.AreEqual(3, g.Player1.Deck.CardsRemaining, $"Seed {seed}");
                }
            }
        }

        [Test, Category(TestCategories.KnownBug), Description("#45: summon-from-deck picks slots without counting summons still queued")]
        public void TwoSummonsFromDeckInOneTurn_NeverLandInTheSameSlot() {
            for (int seed = 1; seed <= 20; seed++) {
                using (var g = new TestGame()) {
                    var spider = g.Creature("Spider", 1, 1);
                    var wasp = g.Creature("Wasp", 1, 1);
                    g.SetDecks(new List<CardData> { spider, wasp, spider, wasp }, g.Fillers(1));
                    for (int i = 0; i < 3; i++) g.Summon(g.Player1, g.Creature($"Guard {i}", 1, 1), i);
                    GameRandom.Reset(seed);

                    SummonFromDeckEffect.QueueRandomSummons(g.Player1, 1, g.ActionsQueue);
                    SummonFromDeckEffect.QueueRandomSummons(g.Player1, 1, g.ActionsQueue);
                    g.Resolve();

                    Assert.AreEqual(5, g.Player1.Battlefield.Count(s => s.IsOccupied()), $"Seed {seed}: one summon replaced the other");
                }
            }
        }

        [Test]
        public void DrawAndSummonSpells_WorkForTheirCaster_WhereverTheyAreDropped() {
            game.SetDecks(game.Fillers(5), game.Fillers(5));
            var insight = game.GiveCard(p1, game.Spell("Insight", TargetType.Player, Effect(EffectTrigger.OnPlay, ActionType.Draw, 1, TargetType.Player)));

            game.Run(new PlayCardAction(insight, p1, p2.Battlefield[0]));
            Assert.AreEqual(1, p1.Hand.Count, "The caster should draw");
            Assert.AreEqual(0, p2.Hand.Count, "The enemy shouldn't");

            var call = game.GiveCard(p1, game.Spell("Call", TargetType.Player, Effect(EffectTrigger.OnPlay, ActionType.Summon, 1, TargetType.Player)));
            game.Run(new PlayCardAction(call, p1, p2.Battlefield[1]));
            Assert.AreEqual(1, p1.Battlefield.Count(s => s.IsOccupied()), "The caster's creature should land on the caster's board");
            Assert.AreEqual(0, p2.Battlefield.Count(s => s.IsOccupied()));
        }
    }
}
