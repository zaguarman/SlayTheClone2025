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

            // A discard cost (Social Engineering): random other cards from the hand pay for the card; taken back, they return
            Fresh();
            game.SetDecks(game.Fillers(10), game.Fillers(1));
            var tradeData = game.Spell("Trade", TargetType.Player, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Draw, 3, TargetType.Player));
            tradeData.discardCost = 2;
            var trade = game.GiveCard(p1, tradeData);
            game.ActionsQueue.AddAction(new PlayCardAction(trade, p1, p1));
            Assert.AreEqual(0, game.ActionsQueue.GetPendingActionsCount(), "Nothing else in the hand to pay with: it can't be played");
            game.Run(new DrawCardsAction(p1, 3));
            int cards = GameAsserts.CountCards(p1);
            game.ActionsQueue.AddAction(new PlayCardAction(trade, p1, p1));
            Assert.AreEqual((2, 2), (p1.Hand.Count, p1.SetAside.Count), "Two of the three others are set aside to pay for it");
            Assert.AreEqual(cards, GameAsserts.CountCards(p1));
            game.ActionsQueue.CancelPlay(trade);
            Assert.AreEqual((4, 0), (p1.Hand.Count, p1.SetAside.Count), "Taken back: they come back");
            game.ActionsQueue.AddAction(new PlayCardAction(trade, p1, p1));
            game.Resolve();
            Assert.AreEqual(3, p1.Deck.GetDiscardPilePreview().Count, "Played: the two it cost and the spell itself are discarded...");
            Assert.AreEqual(4, p1.Hand.Count, "...and it draws 3 (the one card left and the 3 drawn)");
            Assert.AreEqual(cards, GameAsserts.CountCards(p1));

            // Single Use (The Golden Gun): once played, it's gone for the rest of the combat, but still the player's card
            var gunData = game.Spell("Gun", TargetType.Player, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Draw, 1, TargetType.Player));
            gunData.singleUse = true;
            var gun = game.GiveCard(p1, gunData);
            cards = GameAsserts.CountCards(p1);
            game.ActionsQueue.AddAction(new PlayCardAction(gun, p1, p1));
            game.Resolve();
            CollectionAssert.Contains(p1.Spent, gun);
            CollectionAssert.DoesNotContain(p1.Deck.GetDiscardPilePreview(), gun);
            Assert.AreEqual(cards, GameAsserts.CountCards(p1));
        }

        [UnityTest]
        public IEnumerator EndingATurn_DiscardsBothHandsAndDrawsFiveEach() {
            game.SetDecks(game.Fillers(20), game.Fillers(20));
            game.CardDealingService.DealInitialHands(p1, p2, 5);
            var firstHands = new[] { p1, p2 }.ToDictionary(p => p, p => p.Hand.ToList());
            game.Summon(p1, game.Creature("Crowd", 1, 1), 4);
            var sketchbook = game.GiveCard(p1, game.Spell("Sketchbook", TargetType.Player, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Draw, 2, TargetType.Player)));
            game.Play(sketchbook, p1, p1.Battlefield[0]);
            // The Zeitgeist Trend: each Day whoever has fewer creatures draws 1 more card. Played on either side, it's everyone's
            var zeitgeistCard = game.Spell("Zeitgeist", TargetType.Player, TestCards.TrendEffect(Trend.Zeitgeist, 2));
            var zeitgeist = game.GiveCard(p2, zeitgeistCard);
            game.Play(zeitgeist, p2, p1.Battlefield[1]);

            yield return game.EndTurn();

            Assert.AreEqual(Trend.Zeitgeist, game.TrendSystem.Current, "A Trend for 2 Days lasts this Day and the next");
            Assert.AreEqual("Trend", CardTextFormatter.GetTypeLine(zeitgeistCard), "A card that only sets a Trend is a Trend");
            foreach (var player in new[] { p1, p2 }) {
                int drawn = player == p1 ? 7 : 6; // The underdog (no creatures) draws 1 more
                Assert.AreEqual(drawn, player.Hand.Count, "A draw resolved during the Day comes with the next Day's hand, not into the hand about to be discarded");
                CollectionAssert.IsEmpty(player.Hand.Intersect(firstHands[player]), "The old hand should have been discarded");
                Assert.AreEqual(20 - 5 - drawn, player.Deck.CardsRemaining);
            }
            Assert.AreEqual(6, p2.Deck.GetDiscardPilePreview().Count, "Its 5 cards and the Trend it played");
            Assert.AreEqual(0, p1.ExtraDraws, "The owed cards were drawn");

            yield return game.EndTurn();
            Assert.AreEqual(Trend.None, game.TrendSystem.Current, "Over after its last Day's Dusk");
            Assert.AreEqual(5, p2.Hand.Count, "No more extra card");
        }
    }
}
