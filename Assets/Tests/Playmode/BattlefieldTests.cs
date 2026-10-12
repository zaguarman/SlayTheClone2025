using System.Linq;
using NUnit.Framework;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The board: where creatures land, how they move and swap, and what attacks, spells and effects hit.
    /// Runs the real rules without the scene (see <see cref="TestGame"/>).
    /// </summary>
    public class BattlefieldTests {
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
        public void TearDown() {
            GameAsserts.AssertBoardsConsistent(p1, p2);
            game.Dispose();
        }

        #region Placing and moving
        [Test]
        public void ACreature_HasOneIntentADay_ANewOneReplacesTheOld() {
            var runner = game.Summon(p1, game.Creature("Runner", 2, 5), 0);
            var target = game.Summon(p2, game.Creature("Target", 0, 10), 0);

            game.Declare(runner, p2.Battlefield[0]);
            game.ActionsQueue.AddAction(new MoveCreatureAction(runner, p1.Battlefield[0], p1.Battlefield[2], p1));
            Assert.AreEqual(1, game.ActionsQueue.GetPendingActionsCount(), "Moving after declaring an attack should replace the attack");
            game.Resolve();
            Assert.AreSame(runner, p1.Battlefield[2].OccupyingCreature, "The move happens");
            CollectionAssert.IsEmpty(game.History.DamageTo(target), "The replaced attack doesn't");

            game.ActionsQueue.AddAction(new MoveCreatureAction(runner, p1.Battlefield[2], p1.Battlefield[4], p1));
            game.Declare(runner, p2.Battlefield[0]);
            Assert.AreEqual(1, game.ActionsQueue.GetPendingActionsCount(), "Declaring an attack after moving should replace the move");
            game.Resolve();
            Assert.AreSame(runner, p1.Battlefield[2].OccupyingCreature, "The replaced move doesn't happen");
            CollectionAssert.AreEqual(new[] { 2 }, game.History.DamageTo(target), "The attack does");
        }
        #endregion

        #region What gets hit
        [Test]
        public void ASpell_HitsTheCreatureItIsDroppedOn_OrThatSidesPlayerOnAnEmptySlot() {
            var bolt = game.Spell("Bolt", TargetType.EnemyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Damage, 3, TargetType.EnemyCreatures));
            var oxA = game.Summon(p2, game.Creature("Ox A", 0, 10), 0);
            var oxB = game.Summon(p2, game.Creature("Ox B", 0, 10), 1);

            game.Run(new PlayCardAction(game.GiveCard(p1, bolt), p1, p2.Battlefield[1]));
            Assert.AreEqual(10, oxA.Health, "Only the creature it was dropped on is hit");
            Assert.AreEqual(7, oxB.Health);

            game.Run(new PlayCardAction(game.GiveCard(p1, bolt), p1, p2.Battlefield[3]));
            Assert.AreEqual(Player.MaxHealth - 3, p2.Health);
        }

        [Test]
        public void GroupEffects_HitEveryCreatureOfTheirGroup_Once() {
            var mine = game.Summon(p1, game.Creature("Mine", 0, 5), 0);
            var theirsA = game.Summon(p2, game.Creature("Theirs A", 0, 5), 0);
            var theirsB = game.Summon(p2, game.Creature("Theirs B", 0, 5), 3);

            var storm = game.Summon(p1, game.Creature("Storm", 0, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.EnemyCreatures)), 1);
            Assert.AreEqual(4, theirsA.Health);
            Assert.AreEqual(4, theirsB.Health);
            Assert.AreEqual(5, mine.Health, "Enemy creatures only");

            var quake = game.Summon(p1, game.Creature("Quake", 0, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.AllCreatures)), 2);
            foreach (var creature in new[] { mine, storm, quake }) Assert.AreEqual(4, creature.Health, $"{creature.Name}: all creatures, including itself");
            foreach (var creature in new[] { theirsA, theirsB }) Assert.AreEqual(3, creature.Health, $"{creature.Name}: all creatures");

            game.Summon(p1, game.Creature("Medic", 0, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Heal, 1, TargetType.FriendlyCreatures)), 3);
            foreach (var creature in new[] { mine, storm, quake }) Assert.AreEqual(5, creature.Health, $"{creature.Name}: friendly creatures");
            foreach (var creature in new[] { theirsA, theirsB }) Assert.AreEqual(3, creature.Health, $"{creature.Name}: friendly creatures only");

            foreach (var creature in new[] { theirsA, theirsB }) {
                CollectionAssert.AreEqual(new[] { 1, 1 }, game.History.DamageTo(creature), $"{creature.Name} was hit the wrong number of times");
            }
        }

        [Test]
        public void ASpreadingAttack_AlsoHitsTheTargetsNeighbours_ForTheAttackWhenTheHitsLand() {
            var cleaver = game.Summon(p1, game.Creature("Cleaver", 2, 5, 1,
                Effect(EffectTrigger.OnDamage, ActionType.Damage, 0, TargetType.EnemyCreatures, TargetModifier.WithAdjacent)), 0);
            var enemies = Enumerable.Range(0, 4).Select(i => game.Summon(p2, game.Creature($"Enemy {i}", 0, 10), i)).ToList();

            game.Declare(cleaver, p2.Battlefield[1]);
            game.ActionsQueue.AddAction(new ModifyAction(cleaver, 3, modifyAttack: true, modifyHealth: false, modifySpeed: false)); // Lands before the attack
            game.Resolve();

            CollectionAssert.AreEqual(new[] { 5 }, game.History.DamageTo(enemies[1]), "The target");
            CollectionAssert.AreEqual(new[] { 5 }, game.History.DamageTo(enemies[0]), "Its left neighbour");
            CollectionAssert.AreEqual(new[] { 5 }, game.History.DamageTo(enemies[2]), "Its right neighbour");
            CollectionAssert.IsEmpty(game.History.DamageTo(enemies[3]), "Not a neighbour");
        }
        #endregion
    }
}
