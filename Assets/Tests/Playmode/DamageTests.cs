using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using static SlayTheClone.Enums;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Damage and healing land once, on the right target, in the right amount, and never go past their limits.
    /// Runs the real rules without the scene (see <see cref="TestGame"/>).
    /// </summary>
    public class DamageTests {
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
        public void Armor_TakesHitsFirst_AndIsUsedUpAcrossTheTurnsHits() {
            var wall = game.Summon(p2, game.Creature("Wall", 0, 5), 0);
            game.Run(new ModifyArmorAction(wall, 3));
            var first = game.Summon(p1, game.Creature("First", 2, 5), 0);
            var second = game.Summon(p1, game.Creature("Second", 2, 5), 1);

            game.Declare(first, p2.Battlefield[0]);
            game.Declare(second, p2.Battlefield[0]);
            game.Resolve();

            Assert.AreEqual(0, wall.CurrentArmorPool);
            Assert.AreEqual(4, wall.Health);
            CollectionAssert.AreEqual(new[] { 1 }, game.History.DamageTo(wall), "Only the second hit gets past the armor, by 1");
        }

        [Test]
        public void APlayersHealth_StopsAtZero_AndTheGameEndsOnce() {
            var giantA = game.Summon(p1, game.Creature("Giant A", 15, 5), 0);
            var giantB = game.Summon(p1, game.Creature("Giant B", 15, 5), 1);

            game.Declare(giantA, p2.Battlefield[0]);
            game.Declare(giantB, p2.Battlefield[1]);
            game.Resolve();

            Assert.AreEqual(0, p2.Health);
            CollectionAssert.AreEqual(new[] { 15, 5 }, game.History.DamageTo(p2));
            CollectionAssert.AreEqual(new[] { p1 }, game.History.Winners, "The game should end once, won by player 1");
        }

        [UnityTest]
        public IEnumerator Burn_TicksOncePerTurn_UntilItRunsOut() {
            var torch = game.Summon(p1, game.Creature("Torch", 0, 10), 0);
            game.Run(new ApplyStatusEffectAction(torch, StatusEffectType.Burned, 2, 2));

            yield return game.EndTurn();
            Assert.AreEqual(8, torch.Health);

            yield return game.EndTurn();
            Assert.AreEqual(6, torch.Health);
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(torch, StatusEffectType.Burned), "The burn should have run out after 2 turns");

            yield return game.EndTurn();
            Assert.AreEqual(6, torch.Health);
            CollectionAssert.AreEqual(new[] { 2, 2 }, game.History.DamageTo(torch));
        }
    }
}
