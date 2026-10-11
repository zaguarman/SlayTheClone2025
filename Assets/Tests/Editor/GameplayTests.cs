using System.Collections.Generic;
using NUnit.Framework;
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
            game.SetDecks(new List<CardData> { wolf, bolt, fish, fish, fish }, new List<CardData> { bear });

            // Dealing draws the shuffled deck into the hand
            game.CardDealingService.DealInitialHands(p1, p2, 5);
            Assert.AreEqual(5, p1.Hand.Count);
            Assert.AreEqual(0, p1.Deck.CardsRemaining);

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

            // A spell dropped on an enemy slot hits the creature there; the dead creature and the spent spell are discarded
            var boltCard = game.InHand(p1, "Bolt");
            game.Run(new PlayCardAction(boltCard, p1, p2.Battlefield[0]));
            Assert.IsTrue(bearCard.IsDead);
            Assert.IsFalse(p2.Battlefield[0].IsOccupied());
            CollectionAssert.Contains(p2.Deck.GetDiscardPilePreview(), bearCard);
            CollectionAssert.Contains(p1.Deck.GetDiscardPilePreview(), boltCard);

            // Discarding empties the hand; drawing from an empty deck shuffles the discard pile back in
            game.Run(new DiscardHandAction(p1));
            Assert.AreEqual(0, p1.Hand.Count);
            Assert.AreEqual(4, p1.Deck.GetDiscardPilePreview().Count); // Bolt and the three Fish

            game.Run(new DrawCardsAction(p1, 2));
            Assert.AreEqual(2, p1.Hand.Count);
            Assert.AreEqual(2, p1.Deck.CardsRemaining);
            Assert.AreEqual(0, p1.Deck.GetDiscardPilePreview().Count);
        }

        [Test]
        public void Combat_HitsWhateverIsInTheTargetSlot() {
            var hound = game.Summon(p1, game.Creature("Hound", 3, 5), 0);
            var sheep = game.Summon(p2, game.Creature("Sheep", 1, 3), 0);
            var porcupine = game.Summon(p2, game.Creature("Porcupine", 0, 10, 1,
                TestGame.Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.Enemy)), 1);

            // Attacking an occupied slot damages its creature; a creature that dies leaves the board for the discard pile
            Attack(hound, p2.Battlefield[0]);
            Assert.IsTrue(sheep.IsDead);
            Assert.IsFalse(p2.Battlefield[0].IsOccupied());
            CollectionAssert.Contains(p2.Deck.GetDiscardPilePreview(), sheep);

            // Attacking an empty slot damages the player who owns it
            int p2Health = p2.Health;
            Attack(hound, p2.Battlefield[0]);
            Assert.AreEqual(p2Health - hound.Attack, p2.Health);

            // An enemy-targeted OnDamage effect strikes back at the attacker
            int houndHealth = hound.Health;
            Attack(hound, p2.Battlefield[1]);
            Assert.AreEqual(porcupine.MaxHealth - hound.Attack, porcupine.Health);
            Assert.AreEqual(houndHealth - 1, hound.Health);

            // Attacks declared in the same turn resolve by speed: a creature killed first never lands its hit
            var falcon = game.Summon(p1, game.Creature("Falcon", 4, 2, speed: 3), 2);
            var boar = game.Summon(p2, game.Creature("Boar", 4, 2, speed: 1), 2);
            game.CombatHandler.HandleCreatureCombat(falcon, p2.Battlefield[2]);
            game.CombatHandler.HandleCreatureCombat(boar, p1.Battlefield[2]);
            game.Resolve();
            Assert.IsTrue(boar.IsDead);
            Assert.IsFalse(falcon.IsDead);
            Assert.AreEqual(falcon.MaxHealth, falcon.Health);
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
            CollectionAssert.Contains(p1.Deck.GetDiscardPilePreview(), eel);

            // A paralyzed creature can't move
            game.Run(new ApplyStatusEffectAction(turtle, StatusEffectType.Paralyzed, 1, 0));
            game.Run(new MoveCreatureAction(turtle, p1.Battlefield[4], p1.Battlefield[0], p1));
            Assert.AreSame(turtle, p1.Battlefield[4].OccupyingCreature);
            Assert.IsFalse(p1.Battlefield[0].IsOccupied());
        }

        // Declares an attack the way dragging a creature onto a slot does, then resolves it
        private void Attack(ICreature attacker, BoardSlot target) {
            game.CombatHandler.HandleCreatureCombat(attacker, target);
            game.Resolve();
        }
    }
}
