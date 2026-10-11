using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

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
        public void AnAttack_DealsItsDamageExactlyOnce() {
            var hound = game.Summon(p1, game.Creature("Hound", 3, 5), 0);
            var ox = game.Summon(p2, game.Creature("Ox", 0, 10), 0);

            game.Attack(hound, p2.Battlefield[0]);
            CollectionAssert.AreEqual(new[] { 3 }, game.History.DamageTo(ox));
            Assert.AreEqual(7, ox.Health);

            game.Resolve(); // The attack was spent; resolving again does nothing
            Assert.AreEqual(7, ox.Health);
        }

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
        public void ACreature_DiesOnce_EvenWhenAnotherHitArrivesAfterItDied() {
            var mouse = game.Summon(p2, game.Creature("Mouse", 0, 3), 0);
            var first = game.Summon(p1, game.Creature("First", 5, 5), 0);
            var second = game.Summon(p1, game.Creature("Second", 5, 5), 1);

            game.Declare(first, p2.Battlefield[0]);
            game.Declare(second, p2.Battlefield[0]);
            game.Resolve();

            CollectionAssert.AreEqual(new[] { mouse }, game.History.Deaths);
            CollectionAssert.AreEqual(new[] { 3 }, game.History.DamageTo(mouse), "Health damage stops at what the creature had");
            Assert.AreEqual(1, p2.Deck.GetDiscardPilePreview().Count(c => c.CardId == mouse.CardId), "Discarded more than once");
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

        [Test]
        public void Healing_StopsAtMaxHealth_AndDoesNothingForTheDead() {
            var patient = game.Summon(p1, game.Creature("Patient", 0, 5), 0);

            game.Run(new DamageCreatureAction(patient, 3));
            game.Run(new HealCreatureAction(patient, 10));
            Assert.AreEqual(5, patient.Health);
            CollectionAssert.AreEqual(new[] { 3 }, game.History.HealingOf(patient), "Only the health actually restored counts");

            game.Run(new DamageCreatureAction(patient, 9));
            game.Run(new HealCreatureAction(patient, 5));
            Assert.IsTrue(patient.IsDead);
            CollectionAssert.AreEqual(new[] { 3 }, game.History.HealingOf(patient), "A dead creature can't be healed");

            game.Run(new HealPlayerAction(p1, 5));
            Assert.AreEqual(Player.MaxHealth, p1.Health);
            CollectionAssert.IsEmpty(game.History.HealingOf(p1), "A player at full health isn't healed");
        }

        [Test]
        public void ChainDamage_HitsEachCreatureInTheChainOnce_HalvingEachJump() {
            var eel = game.Summon(p1, game.Creature("Eel", 3, 4, 1,
                Effect(EffectTrigger.OnDamage, ActionType.Damage, 4, TargetType.EnemyCreatures, TargetModifier.Chained)), 0);
            var crabA = game.Summon(p2, game.Creature("Crab A", 0, 10), 1);
            var crabB = game.Summon(p2, game.Creature("Crab B", 0, 10), 2);
            var crabC = game.Summon(p2, game.Creature("Crab C", 0, 10), 3);

            game.Attack(eel, p2.Battlefield[1]);

            CollectionAssert.AreEqual(new[] { 3 }, game.History.DamageTo(crabA), "The attack itself");
            CollectionAssert.AreEqual(new[] { 4 }, game.History.DamageTo(crabB), "First jump");
            CollectionAssert.AreEqual(new[] { 2 }, game.History.DamageTo(crabC), "Second jump, halved");
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

        [Test]
        public void AnAttack_HitsWhateverIsInTheTargetSlotWhenItResolves() {
            var hound = game.Summon(p1, game.Creature("Hound", 3, 5), 0);
            var dodger = game.Summon(p2, game.Creature("Dodger", 0, 5), 0);

            game.Declare(hound, p2.Battlefield[0]);
            game.ActionsQueue.AddAction(new MoveCreatureAction(dodger, p2.Battlefield[0], p2.Battlefield[1], p2));
            game.Resolve();

            CollectionAssert.IsEmpty(game.History.DamageTo(dodger), "Moves resolve before attacks, so the dodger got out of the way");
            Assert.AreEqual(Player.MaxHealth - 3, p2.Health, "The attack hits the now empty slot's player");
        }

        [Test]
        public void AnAttackHits_ForTheAttackersAttackWhenTheHitLands() {
            // Like Abyssal Cucumber: it grows when damaged, and is damaged by a hit that lands before its own
            var cucumber = game.Summon(p1, game.Creature("Cucumber", 1, 4, 1, Effect(EffectTrigger.OnDamage, ActionType.ModifyStat, 2, TargetType.Self)), 0);
            var pebble = game.Summon(p2, game.Creature("Pebble", 1, 5), 0);

            game.Declare(pebble, p1.Battlefield[0]);
            game.Declare(cucumber, p2.Battlefield[1]); // An empty slot: the hit goes to player 2
            game.Resolve();

            Assert.AreEqual(3, cucumber.Attack);
            CollectionAssert.AreEqual(new[] { 3 }, game.History.DamageTo(p2), "The hit should use the attack the cucumber had when it landed");
        }

        [Test]
        public void ASpreadingAttack_HitsItsNeighboursForTheAttackersAttackWhenTheHitsLand() {
            var cleaver = game.Summon(p1, game.Creature("Cleaver", 2, 6, 1,
                Effect(EffectTrigger.OnDamage, ActionType.Damage, 0, TargetType.EnemyCreatures, TargetModifier.WithAdjacent)), 0);
            var enemies = new[] { 0, 1, 2 }.Select(i => game.Summon(p2, game.Creature($"Enemy {i}", 0, 10), i)).ToList();

            game.Declare(cleaver, p2.Battlefield[1]);
            game.ActionsQueue.AddAction(new ModifyAction(cleaver, 3, modifyAttack: true, modifyHealth: false, modifySpeed: false));
            game.Resolve();

            foreach (var enemy in enemies) CollectionAssert.AreEqual(new[] { 5 }, game.History.DamageTo(enemy), enemy.Name);
        }

        [Test, Category(TestCategories.KnownBug), Description("#43: CanAct doesn't check that the creature is still on the board")]
        public void ACreatureReplacedBeforeItsAttackResolves_DoesNotAttack() {
            var veteran = game.Summon(p1, game.Creature("Veteran", 3, 3), 0);
            var recruit = game.GiveCard(p1, game.Creature("Recruit", 1, 1));

            game.Declare(veteran, p2.Battlefield[0]);
            game.ActionsQueue.AddAction(new PlayCardAction(recruit, p1, p1.Battlefield[0]));
            game.Resolve();

            Assert.AreSame(recruit, p1.Battlefield[0].OccupyingCreature);
            Assert.AreEqual(Player.MaxHealth, p2.Health, "The veteran left the board, so its attack shouldn't land");
        }
    }
}
