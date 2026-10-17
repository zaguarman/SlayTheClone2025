using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using static Zeitgeist.Enums;

namespace Zeitgeist.Tests {
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

            // A chosen discard cost (Headhunter, a calculated play): the cards picked pay, and can be picked again while queued
            Fresh();
            game.SetDecks(game.Fillers(10), game.Fillers(1));
            var hireData = game.Spell("Hire", TargetType.Player, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Draw, 1, TargetType.Player));
            hireData.discardCost = 1;
            hireData.discardChosen = true;
            var hire = game.GiveCard(p1, hireData);
            game.Run(new DrawCardsAction(p1, 3));
            var others = p1.Hand.Where(c => c != hire).ToList();
            p1.DiscardPicks[hire] = new List<ICard> { others[1] };
            var hiring = new PlayCardAction(hire, p1, p1);
            game.ActionsQueue.AddAction(hiring);
            CollectionAssert.AreEqual(new[] { others[1] }, p1.SetAside, "Chosen: the card picked pays");
            Assert.IsTrue(DiscardCost.Repay(hiring, new List<ICard> { others[2] }, game.ActionsQueue));
            CollectionAssert.AreEqual(new[] { others[2] }, p1.SetAside, "...and picked again while the play is queued, the new one pays instead");
            CollectionAssert.Contains(p1.Hand, others[1]);
            StringAssert.Contains("of your choice", Keywords.Plain(CardTextFormatter.GetRulesText(hireData)));

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

            // Sacrifice (Myth): the weakest creature of its side dies first; with none on the board it can't be played
            var ritualData = game.Spell("Ritual", TargetType.Player, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Draw, 1, TargetType.Player));
            ritualData.sacrifice = 1;
            var ritual = game.GiveCard(p1, ritualData);
            game.ActionsQueue.AddAction(new PlayCardAction(ritual, p1, p1));
            Assert.AreEqual(0, game.ActionsQueue.GetPendingActionsCount(), "Nobody to sacrifice: it can't be played");
            var lamb = game.Summon(p1, game.Creature("Lamb", 1, 2), 0);
            var bull = game.Summon(p1, game.Creature("Bull", 3, 5), 1);
            game.ActionsQueue.AddAction(new PlayCardAction(ritual, p1, p1));
            game.Resolve();
            Assert.IsTrue(lamb.IsDead, "The weakest is sacrificed...");
            Assert.IsFalse(bull.IsDead, "...and only it");
            StringAssert.Contains("Sacrifice 1", Keywords.Plain(CardTextFormatter.GetRulesText(ritualData)));

            // Doom as a cost (Doomscrolling): the weakest creature not yet Doomed is Doomed first; with none it can't be played
            var scrollData = game.Spell("Scroll", TargetType.Player, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Draw, 1, TargetType.Player));
            scrollData.doomCost = 3;
            var scroll = game.GiveCard(p1, scrollData);
            game.ActionsQueue.AddAction(new PlayCardAction(scroll, p1, p1));
            game.Resolve();
            Assert.IsTrue(game.ModifierManager.HasStatusEffect(bull, StatusEffectType.Doomed), "The feed takes the only one left");
            scroll = game.GiveCard(p1, scrollData);
            game.ActionsQueue.AddAction(new PlayCardAction(scroll, p1, p1));
            Assert.AreEqual(0, game.ActionsQueue.GetPendingActionsCount(), "Everyone's Doomed already: it can't be played");
            StringAssert.Contains("Doom 3", Keywords.Plain(CardTextFormatter.GetRulesText(scrollData)));

            // Prophecy (Myth): played now, it comes true at the Dawn so many Days later, and the queue lists it meanwhile
            var floodData = game.Spell("Flood", TargetType.Enemy, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.EnemyCreatures, TargetModifier.AllSameTypeTargets));
            floodData.prophecy = 2;
            var foe = game.Summon(p2, game.Creature("Foe", 1, 5), 0); // The Vanguard (a Flank would face the Doomed bull's last strike)
            var flood = game.GiveCard(p1, floodData);
            game.ActionsQueue.AddAction(new PlayCardAction(flood, p1, p2));
            game.Resolve();
            Assert.AreEqual(5, foe.Health, "Foretold: nothing happens yet");
            StringAssert.Contains("foretold", string.Join("\n", QueueView.Lines(p1, game.ActionsQueue, game.ModifierManager)), "Everyone hears it");
            game.StartTurn();
            Assert.AreEqual(5, foe.Health, "Not at the next Dawn...");
            game.StartTurn();
            Assert.AreEqual(3, foe.Health, "...but at the Dawn two Days on");
            CollectionAssert.IsEmpty(p1.Prophecies);

            // Prophecy 1-3 (Margin Call): it comes on a Dawn in that window, rolled in secret; the queue only tells the window
            floodData.prophecy = 1;
            floodData.prophecyLatest = 3;
            StringAssert.Contains("Prophecy 1-3", Keywords.Plain(CardTextFormatter.GetRulesText(floodData)));
            game.ActionsQueue.AddAction(new PlayCardAction(game.GiveCard(p1, floodData), p1, p2));
            game.Resolve();
            var due = p1.Prophecies.Single();
            Assert.That(due.DueOnTurn, Is.InRange(due.EarliestTurn, due.LatestTurn));
            Assert.AreEqual(2, due.LatestTurn - due.EarliestTurn, "A window of three Days");
            string foretoldLine = string.Join("\n", QueueView.Lines(p1, game.ActionsQueue, game.ModifierManager));
            StringAssert.Contains($"from Day {due.EarliestTurn} to Day {due.LatestTurn}", foretoldLine, "Everyone knows the window, not the Day");
            for (int day = 0; day < 3 && p1.Prophecies.Count > 0; day++) game.StartTurn();
            Assert.AreEqual(1, foe.Health, $"It came within three Dawns (hits on the foe: {string.Join(", ", game.History.DamageTo(foe))})");

            // A part that waits (Dead Cat Bounce): it heals now, and the crash lands on the same creature at the next Dawn
            var bounce = TestGame.Effect(EffectTrigger.OnPlay, ActionType.Heal, 2, TargetType.AllCreatures);
            var crash = TestGame.Effect(EffectTrigger.OnPlay, ActionType.Damage, 5, TargetType.AllCreatures).actions[0];
            crash.delayDays = 1;
            bounce.actions.Add(crash);
            var catData = game.Spell("Cat", TargetType.AllCreatures, bounce);
            StringAssert.Contains("at the next Dawn, 5 damage", Keywords.Plain(CardTextFormatter.GetRulesText(catData)));
            var cat = game.Summon(p2, game.Creature("Stock", 1, 9), 1);
            game.ActionsQueue.AddAction(new PlayCardAction(game.GiveCard(p1, game.Spell("Hit", TargetType.AllCreatures,
                TestGame.Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.AllCreatures))), p1, cat));
            game.Resolve();
            game.ActionsQueue.AddAction(new PlayCardAction(game.GiveCard(p1, catData), p1, cat));
            game.Resolve();
            Assert.AreEqual(8, cat.Health, "The bounce: healed 2, on either side");
            StringAssert.Contains("the rest of it lands", string.Join("\n", QueueView.Lines(p1, game.ActionsQueue, game.ModifierManager)));
            game.StartTurn();
            Assert.AreEqual(3, cat.Health, "The crash, at the next Dawn");

            // Send For (Multipass, Omen Reading...): the card picked from the deck joins the next hand; scouting only looks at its top
            Fresh();
            game.SetDecks(new List<CardData> { game.Creature("Scout", 1, 1), game.Creature("Giant", 5, 5), game.Creature("Runt", 1, 1) }, game.Fillers(3));
            var send = game.GiveCard(p1, game.Spell("Send", TargetType.Player, TestGame.Effect(EffectTrigger.OnPlay, ActionType.SendFor, 0, TargetType.Player)));
            cards = GameAsserts.CountCards(p1);
            var giant = p1.Deck.GetDeckPreview().First(c => c.Name == "Giant");
            p1.SendForPicks[send] = giant;
            game.ActionsQueue.AddAction(new PlayCardAction(send, p1, p1));
            game.Resolve();
            CollectionAssert.Contains(p1.ComingBack, giant, "The picked card is on its way to the next hand");
            CollectionAssert.DoesNotContain(p1.Deck.GetDeckPreview(), giant, "...out of the deck");
            Assert.AreEqual(cards, GameAsserts.CountCards(p1));
            var peek = game.GiveCard(p1, game.Spell("Peek", TargetType.Player, TestGame.Effect(EffectTrigger.OnPlay, ActionType.SendFor, 1, TargetType.Player)));
            var top = p1.Deck.GetDeckPreview()[0];
            game.ActionsQueue.AddAction(new PlayCardAction(peek, p1, p1));
            game.Resolve();
            CollectionAssert.Contains(p1.ComingBack, top, "Nothing picked: the best it can reach, and scouting only reaches the top card");
            var loadData = game.Spell("Load", TargetType.Player, TestGame.Effect(EffectTrigger.OnPlay, ActionType.SendFor, 0, TargetType.Player));
            loadData.effects[0].actions[0].fromDiscard = true;
            StringAssert.Contains("from your discard pile", Keywords.Plain(CardTextFormatter.GetRulesText(loadData)));
            var load = game.GiveCard(p1, loadData);
            var saved = p1.Deck.GetDiscardPilePreview().FirstOrDefault() ?? p1.Deck.GetDeckPreview()[0];
            if (!p1.Deck.GetDiscardPilePreview().Contains(saved)) { p1.Deck.RemoveCard(saved); p1.Deck.AddToDiscardPile(saved); }
            p1.SendForPicks[load] = saved;
            game.ActionsQueue.AddAction(new PlayCardAction(load, p1, p1));
            game.Resolve();
            CollectionAssert.Contains(p1.ComingBack, saved, "Console Memory Card: the save loads from the discard pile...");
            CollectionAssert.DoesNotContain(p1.Deck.GetDiscardPilePreview(), saved, "...and leaves it");

            // Gamble (Casino): only one of its effects happens, at random
            for (int spin = 0; spin < 6; spin++) {
                var coinData = game.Spell("Coin", TargetType.Player, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.Player),
                    TestGame.Effect(EffectTrigger.OnPlay, ActionType.Heal, 0, TargetType.Player), TestGame.Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.Player));
                coinData.gamble = true;
                var coin = game.GiveCard(p1, coinData);
                int health = p1.Health;
                game.ActionsQueue.AddAction(new PlayCardAction(coin, p1, p1));
                game.Resolve();
                CollectionAssert.Contains(new[] { health, health - 1, health - 2 }, p1.Health, "One outcome, never two at once");
            }
        }

        // The hand (the user, 2026-10-16): each Day the creatures left over go and up to 3 new ones come; the spells stay until
        // played, 2 more a Day, 5 at most; extra draws are spells while there's room for them, creatures past that
        [UnityTest]
        public IEnumerator EndingATurn_DiscardsTheCreaturesLeft_KeepsTheSpells_AndDealsTheDay() {
            List<CardData> Mixed() => game.Fillers(12).Concat(Enumerable.Range(0, 12).Select(i => game.Spell("Memo", TargetType.Player))).ToList();
            game.SetDecks(Mixed(), Mixed());
            game.CardDealingService.DealInitialHands(p1, p2, 5);
            foreach (var player in new[] { p1, p2 })
                Assert.AreEqual((3, 2), (Player.CreaturesIn(player.Hand), Player.SpellsIn(player.Hand)), "The first hand: 3 creatures and 2 spells");
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
                CollectionAssert.IsEmpty(player.Hand.OfType<ICreature>().Intersect(firstHands[player]), "The creatures left over were discarded...");
                CollectionAssert.IsSubsetOf(firstHands[player].Where(c => !(c is ICreature)), player.Hand, "...the spells stay");
            }
            // P1: 3 new creatures, 2 spells kept and 2 more, and Sketchbook's 2 extra: a spell (the 5th), then a creature
            Assert.AreEqual((4, 5), (Player.CreaturesIn(p1.Hand), Player.SpellsIn(p1.Hand)), "A draw resolved during the Day comes with the next Day's hand; spells stop at 5");
            // P2: 3 new creatures, 2 + 2 spells, and the Trend's 1 more (it has fewer creatures): a spell
            Assert.AreEqual((3, 5), (Player.CreaturesIn(p2.Hand), Player.SpellsIn(p2.Hand)), "The underdog draws 1 more");
            Assert.AreEqual(4, p2.Deck.GetDiscardPilePreview().Count, "Its 3 creatures and the Trend it played");
            Assert.AreEqual(0, p1.ExtraDraws, "The owed cards were drawn");

            yield return game.EndTurn();
            Assert.AreEqual(Trend.None, game.TrendSystem.Current, "Over after its last Day's Dusk");
            Assert.AreEqual((3, 5), (Player.CreaturesIn(p2.Hand), Player.SpellsIn(p2.Hand)), "No more extra card, and no spell past 5");

            // Supply Chain Attack: every enemy creature still deploying waits 2 more Days, and their next hand is 2 cards smaller
            var slowData = game.Creature("Slow", 2, 2);
            slowData.turnsUntilOperative = 2;
            var slow = game.Summon(p2, slowData, 0);
            var ready = game.Summon(p2, game.Creature("Ready", 2, 2), 1);
            var delay = TestGame.Effect(EffectTrigger.OnPlay, ActionType.DelayDeployment, 2, TargetType.EnemyCreatures, TargetModifier.AllSameTypeTargets);
            delay.actions[0].requires = TargetRequisite.Deploying;
            var supplyChain = game.GiveCard(p1, game.Spell("Supply Chain", TargetType.Enemy, delay,
                TestGame.Effect(EffectTrigger.OnPlay, ActionType.CutDraws, 2, TargetType.Enemy)));
            game.Run(new PlayCardAction(supplyChain, p1, p2));
            Assert.AreEqual((4, 0), (slow.TurnsUntilOperative, ready.TurnsUntilOperative), "Only the one still deploying waits longer");
            yield return game.EndTurn();
            Assert.AreEqual((1, 5), (Player.CreaturesIn(p2.Hand), Player.SpellsIn(p2.Hand)), "Their hand is 2 cards smaller (no spell was coming: 2 creatures fewer)");

            // Casino: a Trend whose wheel spins the Day's weather each Dawn, the same for both sides, never twice running
            var casino = game.GiveCard(p1, game.Spell("Casino", TargetType.Player, TestCards.TrendEffect(Trend.Casino, 3)));
            game.Run(new PlayCardAction(casino, p1, p1));
            var firstDay = game.TrendSystem.Roll;
            Assert.AreNotEqual(CasinoRoll.None, firstDay, "Set, the wheel spins at once for its first Day");
            yield return game.EndTurn();
            Assert.AreEqual(Trend.Casino, game.TrendSystem.Current);
            Assert.AreNotEqual(firstDay, game.TrendSystem.Roll, "...and again at each Dawn, never the same twice running");
            Assert.AreEqual(game.TrendSystem.Roll == CasinoRoll.RushHour, game.TrendSystem.SlowestFirst, "Its Rush Hour is the real one");

            // The meta picks (2026-10-16): a Ripening spell kept in the hand ripens at each Dawn; a creature discarded at Dawn is
            // Laid Off (what it does happens); a Policy happens when played and again at each Dawn, out of the discard pile
            p1.DiscardHand(); // Room in the hand
            var interestData = game.Spell("Interest", TargetType.Player, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Draw, 1, TargetType.Player));
            interestData.ripening = true;
            var interest = (Spell)game.GiveCard(p1, interestData);
            game.GiveCard(p1, game.Creature("Quitter", 1, 1, 1,
                TestGame.Effect(EffectTrigger.OnDiscarded, ActionType.Damage, 2, TargetType.EnemyCreatures, TargetModifier.AllSameTypeTargets)));
            var mark = game.Summon(p2, game.Creature("Mark", 0, 10), 3);
            var wall = game.Summon(p1, game.Creature("Wall", 0, 5), 3);
            var policyData = game.Spell("Daily Armor", TargetType.Player,
                TestGame.Effect(EffectTrigger.OnPlay, ActionType.Armor, 1, TargetType.FriendlyCreatures, TargetModifier.AllSameTypeTargets));
            policyData.policy = true;
            var policy = game.GiveCard(p1, policyData);
            game.Run(new PlayCardAction(policy, p1, p1));
            Assert.AreEqual(1, wall.CurrentArmorPool, "A Policy happens when it's played...");
            CollectionAssert.Contains(p1.Policies, policy);
            yield return game.EndTurn();
            Assert.AreEqual(1, interest.Ripeness, "Kept through a Dawn, it ripened");
            Assert.AreEqual(2, interest.Effects[0].actions[0].value, "...it draws 1 more");
            Assert.AreEqual(8, mark.Health, "The Quitter, discarded at Dawn, was Laid Off: 2 damage");
            Assert.AreEqual(1, wall.CurrentArmorPool, "...and the Policy again at Dawn (the first Armor faded at Dusk)");
            Assert.IsFalse(p1.Deck.GetDiscardPilePreview().Any(c => c.Name == "Daily Armor"), "In force, not in the discard pile");
        }
    }
}
