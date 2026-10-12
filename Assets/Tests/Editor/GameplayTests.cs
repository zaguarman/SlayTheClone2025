using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static SlayTheClone.Enums;

namespace SlayTheClone.Tests {
    /// <summary>
    /// An overview of how the game plays, one test per area: cards moving between zones, combat, and the board.
    /// Each runs the real rules headless (see <see cref="TestGame"/>); actions are queued, then resolved like an end of turn.
    /// </summary>
    public class GameplayTests {
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
        public void Cards_MoveFromDeckToHandToBoardToDiscardAndBack() {
            var wolf = game.Creature("Wolf", 2, 3, 1, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.Enemy));
            var bolt = game.Spell("Bolt", TargetType.EnemyCreatures, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.EnemyCreatures));
            var fish = game.Creature("Fish", 1, 1);
            var bear = game.Creature("Bear", 1, 3);
            var p1Deck = new List<CardData> { wolf, bolt, fish, fish, fish };
            var p2Deck = new List<CardData> { bear };
            game.SetDecks(p1Deck, p2Deck);

            // Dealing draws the shuffled deck into the hand
            game.CardDealingService.DealInitialHands(p1, p2, 5);
            Assert.AreEqual(5, p1.Hand.Count);
            Assert.AreEqual(0, p1.Deck.CardsRemaining);
            var dealtOrder = p1.Hand.Select(c => c.Name).ToList();

            // A creature played on a slot joins that board
            var bearCard = (ICreature)game.InHand(p2, "Bear");
            game.Run(new PlayCardAction(bearCard, p2, p2.Battlefield[0]));
            Assert.AreSame(bearCard, p2.Battlefield[0].OccupyingCreature);

            // ...leaves the hand, knows its slot, and fires its OnPlay effect (2 damage to the enemy player)
            var wolfCard = (ICreature)game.InHand(p1, "Wolf");
            int p2Health = p2.Health;
            game.Run(new PlayCardAction(wolfCard, p1, p1.Battlefield[1]));
            Assert.AreSame(wolfCard, p1.Battlefield[1].OccupyingCreature);
            Assert.AreSame(p1.Battlefield[1], wolfCard.Slot);
            CollectionAssert.DoesNotContain(p1.Hand, wolfCard);
            Assert.AreEqual(p2Health - 2, p2.Health);

            // A spell dropped on an enemy slot hits the creature there; the dead creature and the spent spell are discarded,
            // as fresh copies (see AssertDiscarded)
            var boltCard = game.InHand(p1, "Bolt");
            game.Run(new PlayCardAction(boltCard, p1, p2.Battlefield[0]));
            Assert.IsTrue(bearCard.IsDead);
            Assert.IsFalse(p2.Battlefield[0].IsOccupied());
            AssertDiscarded(p2, bearCard);
            AssertDiscarded(p1, boltCard);

            // Discarding empties the hand; drawing from an empty deck shuffles the discard pile back in
            game.Run(new DiscardHandAction(p1));
            Assert.AreEqual(0, p1.Hand.Count);
            Assert.AreEqual(4, p1.Deck.GetDiscardPilePreview().Count); // Bolt and the three Fish

            game.Run(new DrawCardsAction(p1, 2));
            Assert.AreEqual(2, p1.Hand.Count);
            Assert.AreEqual(2, p1.Deck.CardsRemaining);
            Assert.AreEqual(0, p1.Deck.GetDiscardPilePreview().Count);

            // The same random seed shuffles the same way, so a game can be replayed
            using (var replay = new TestGame()) {
                replay.SetDecks(p1Deck, p2Deck);
                replay.CardDealingService.DealInitialHands(replay.Player1, replay.Player2, 5);
                CollectionAssert.AreEqual(dealtOrder, replay.Player1.Hand.Select(c => c.Name).ToList());
            }
        }

        [Test]
        public void Combat_HitsWhateverIsInTheTargetSlot() {
            var hound = game.Summon(p1, game.Creature("Hound", 3, 5), 0);
            var sheep = game.Summon(p2, game.Creature("Sheep", 1, 3, 1,
                TestGame.Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.Enemy)), 0);
            var porcupine = game.Summon(p2, game.Creature("Porcupine", 0, 10, 1,
                TestGame.Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.Enemy)), 1);

            // Attacking an occupied slot damages its creature; a creature that dies leaves the board for the discard pile.
            // A killing blow still fires OnDamage effects (GDD 3.9), so the Sheep strikes back as it dies.
            Attack(hound, p2.Battlefield[0]);
            Assert.IsTrue(sheep.IsDead);
            Assert.AreEqual(hound.MaxHealth - 1, hound.Health);
            Assert.IsFalse(p2.Battlefield[0].IsOccupied());
            AssertDiscarded(p2, sheep);

            // Attacking an empty slot damages the player who owns it, announced once
            int p2Health = p2.Health;
            var playerHits = new List<int>();
            game.Mediator.PlayerDamaged += (player, damage) => playerHits.Add(damage);
            Attack(hound, p2.Battlefield[0]);
            Assert.AreEqual(p2Health - hound.Attack, p2.Health);
            CollectionAssert.AreEqual(new[] { hound.Attack }, playerHits);

            // An enemy-targeted OnDamage effect strikes back at the attacker, on every hit
            var jackal = game.Summon(p1, game.Creature("Jackal", 1, 4), 1);
            int houndHealth = hound.Health;
            game.CombatHandler.HandleCreatureCombat(hound, p2.Battlefield[1]);
            game.CombatHandler.HandleCreatureCombat(jackal, p2.Battlefield[1]);
            game.Resolve();
            Assert.AreEqual(porcupine.MaxHealth - hound.Attack - jackal.Attack, porcupine.Health);
            Assert.AreEqual(houndHealth - 1, hound.Health);
            Assert.AreEqual(jackal.MaxHealth - 1, jackal.Health);

            // Effects that change stats go through the queue too, landing before anything else that waits
            var cucumber = game.Summon(p2, game.Creature("Cucumber", 0, 4, 1,
                TestGame.Effect(EffectTrigger.OnDamage, ActionType.ModifyStat, 2, TargetType.FriendlyCreatures)), 3);
            Attack(hound, p2.Battlefield[3]);
            Assert.AreEqual(2, cucumber.Attack);
            Assert.AreEqual(6 - hound.Attack, cucumber.Health); // Max health 4 + 2, minus the hit

            // Attacks declared in the same turn resolve by speed: a creature killed first never lands its hit
            var falcon = game.Summon(p1, game.Creature("Falcon", 4, 2, speed: 3), 2);
            var boar = game.Summon(p2, game.Creature("Boar", 4, 2, speed: 1), 2);
            int falconChanges = 0, boarChanges = 0;
            falcon.Changed += _ => falconChanges++;
            boar.Changed += _ => boarChanges++;
            game.CombatHandler.HandleCreatureCombat(falcon, p2.Battlefield[2]);
            game.CombatHandler.HandleCreatureCombat(boar, p1.Battlefield[2]);
            game.Resolve();
            Assert.IsTrue(boar.IsDead);
            Assert.IsFalse(falcon.IsDead);
            Assert.AreEqual(falcon.MaxHealth, falcon.Health);

            // Each creature announces its own changes (what its card listens to): only the boar was hit and died
            Assert.AreEqual(2, boarChanges);
            Assert.AreEqual(0, falconChanges);

            // Effects can trigger each other forever: two Rams that grow from every hit and strike back.
            // The queue stops after a set number of actions, reports it as an error and drops the rest.
            var ram = game.Creature("Ram", 1, 5, 1,
                TestGame.Effect(EffectTrigger.OnDamage, ActionType.ModifyStat, 1, TargetType.Self),
                TestGame.Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.Enemy));
            var p1Ram = game.Summon(p1, ram, 3);
            game.Summon(p2, ram, 4);
            LogAssert.Expect(LogType.Error, new Regex("Stopped resolving after"));
            Attack(p1Ram, p2.Battlefield[4]);
            Assert.AreEqual(0, game.ActionsQueue.GetPendingActionsCount());
        }

        [Test]
        public void Board_PositionsDecideMovesReplacementsAndChains() {
            var eel = game.Summon(p1, game.Creature("Eel", 3, 4, 1,
                TestGame.Effect(EffectTrigger.OnDamage, ActionType.Damage, 4, TargetType.EnemyCreatures, TargetModifier.Chained)), 0);
            var crabs = new[] {
                game.Summon(p2, game.Creature("Crab A", 0, 10), 1),
                game.Summon(p2, game.Creature("Crab B", 0, 10), 2),
                game.Summon(p2, game.Creature("Crab C", 0, 10), 3)
            };

            // A chain attack jumps from the creature hit along its row of neighbours, halving each jump
            Attack(eel, p2.Battlefield[1]);
            Assert.AreEqual(10 - 3, crabs[0].Health); // The attack itself
            Assert.AreEqual(10 - 4, crabs[1].Health); // First jump
            Assert.AreEqual(10 - 2, crabs[2].Health); // Second jump

            // Moving to an empty slot relocates the creature
            game.Run(new MoveCreatureAction(eel, p1.Battlefield[0], p1.Battlefield[4], p1));
            Assert.IsFalse(p1.Battlefield[0].IsOccupied());
            Assert.AreSame(p1.Battlefield[4], eel.Slot);

            // Moving onto a friendly creature swaps the two
            var turtle = game.Summon(p1, game.Creature("Turtle", 1, 6), 3);
            game.Run(new MoveCreatureAction(eel, p1.Battlefield[4], p1.Battlefield[3], p1));
            Assert.AreSame(eel, p1.Battlefield[3].OccupyingCreature);
            Assert.AreSame(turtle, p1.Battlefield[4].OccupyingCreature);

            // Summoning onto an occupied slot replaces its creature, which is discarded
            var shark = game.Summon(p1, game.Creature("Shark", 4, 4), 3);
            Assert.AreSame(shark, p1.Battlefield[3].OccupyingCreature);
            Assert.IsNull(eel.Slot);
            AssertDiscarded(p1, eel);

            // An exhausted (stunned) creature can't move or attack
            game.Run(new ApplyStatusEffectAction(turtle, StatusEffectType.Exhausted, 1, 0));
            game.Run(new MoveCreatureAction(turtle, p1.Battlefield[4], p1.Battlefield[0], p1));
            Assert.AreSame(turtle, p1.Battlefield[4].OccupyingCreature);
            Assert.IsFalse(p1.Battlefield[0].IsOccupied());
            int healthBeforeStunnedAttack = p2.Health;
            Attack(turtle, p2.Battlefield[4]); // An empty slot: the attack would hit the player
            Assert.AreEqual(healthBeforeStunnedAttack, p2.Health);

            // A creature that needs time to become operative can't attack or move by itself and its effects are off;
            // its on-play effect (2 damage to the enemy player) waits until it is operative
            var golemData = game.Creature("Golem", 3, 5, 1, TestGame.Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.Enemy));
            golemData.turnsUntilOperative = 1;
            int p2Health = p2.Health;
            var golem = game.Summon(p1, golemData, 0);
            Assert.IsFalse(golem.IsOperative);
            Assert.AreEqual(p2Health, p2.Health);

            Attack(golem, p2.Battlefield[0]);
            Assert.AreEqual(p2Health, p2.Health);
            game.Run(new MoveCreatureAction(golem, p1.Battlefield[0], p1.Battlefield[1], p1));
            Assert.AreSame(golem, p1.Battlefield[0].OccupyingCreature);

            // ...but an operative creature can still swap places with it
            game.Run(new MoveCreatureAction(shark, p1.Battlefield[3], p1.Battlefield[0], p1));
            Assert.AreSame(golem, p1.Battlefield[3].OccupyingCreature);
            Assert.AreSame(shark, p1.Battlefield[0].OccupyingCreature);

            // Once it has sat out a full Day after the one it arrived in, it is operative: the on-play effect fires and it can attack
            game.StartTurn();
            Assert.IsFalse(golem.IsOperative, "Deployment Time never counts down at the Dusk of the Day the creature arrived");
            game.StartTurn();
            Assert.IsTrue(golem.IsOperative);
            Assert.AreEqual(p2Health - 2, p2.Health);
            Attack(golem, p2.Battlefield[0]);
            Assert.AreEqual(p2Health - 2 - golem.Attack, p2.Health);
        }

        // A card that leaves play goes to its owner's discard pile as a fresh copy: damage, buffs and death don't carry over
        private static void AssertDiscarded(Player owner, ICard card) {
            var copy = owner.Deck.GetDiscardPilePreview().FirstOrDefault(c => c.CardId == card.CardId);
            Assert.IsNotNull(copy, $"{card.Name} is not in {owner.Name}'s discard pile");
            Assert.AreNotSame(card, copy);
            if (copy is ICreature creature) {
                Assert.IsFalse(creature.IsDead);
                Assert.AreEqual(creature.MaxHealth, creature.Health);
            }
        }

        // Declares an attack the way dragging a creature onto a slot does, then resolves it
        private void Attack(ICreature attacker, BoardSlot target) {
            game.CombatHandler.HandleCreatureCombat(attacker, target);
            game.Resolve();
        }
    }
}
