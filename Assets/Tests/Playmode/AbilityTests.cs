using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Each ability trigger fires when it should, exactly once, and never when it shouldn't.
    /// Runs the real rules without the scene (see <see cref="TestGame"/>); turn tests use the real turn flow.
    /// </summary>
    public class AbilityTests {
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
        public void OnPlay_OnDeath_OnDamage_FireExactlyWhenTheyShould() {
            var wolf = game.GiveCard(p1, game.Creature("Wolf", 2, 3, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.Enemy)));
            var golemData = game.Creature("Golem", 3, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 5, TargetType.Enemy));
            golemData.turnsUntilOperative = 2;

            game.Run(new PlayCardAction(wolf, p1, p1.Battlefield[0]));
            var golem = game.Summon(p1, golemData, 1);
            CollectionAssert.AreEqual(new[] { 2 }, game.History.DamageTo(p2), "Only the operative wolf fires when it lands");

            game.StartTurn(); // Deployment Time skips the Dusk of the Day the creature arrived
            game.StartTurn();
            Assert.IsFalse(golem.IsOperative);
            CollectionAssert.AreEqual(new[] { 2 }, game.History.DamageTo(p2), "The golem fired before it was operative");

            game.StartTurn(); // Deployment Time 2: it sat out 2 full Days after the Day it arrived
            Assert.IsTrue(golem.IsOperative);
            CollectionAssert.AreEqual(new[] { 2, 5 }, game.History.DamageTo(p2), "The golem should fire when it becomes operative");

            game.StartTurn();
            CollectionAssert.AreEqual(new[] { 2, 5 }, game.History.DamageTo(p2), "An on-play effect fired again on a later turn");

            var martyr = game.Summon(p1, game.Creature("Martyr", 1, 2, 1, Effect(EffectTrigger.OnDeath, ActionType.Damage, 3, TargetType.Enemy)), 2);

            game.Run(new DamageCreatureAction(martyr, 5));
            game.Run(new DamageCreatureAction(martyr, 5)); // Hit again after it died

            Assert.IsTrue(martyr.IsDead);
            CollectionAssert.AreEqual(new[] { martyr }, game.History.Deaths, "The creature should die exactly once");
            CollectionAssert.AreEqual(new[] { 2, 5, 3 }, game.History.DamageTo(p2), "The death effect should fire exactly once");
            Assert.AreEqual(1, p1.Deck.GetDiscardPilePreview().Count(c => c.CardId == martyr.CardId), "Discarded more than once");

            var medic = game.Summon(p1, game.Creature("Medic", 0, 5, 1, Effect(EffectTrigger.OnDamage, ActionType.Heal, 1, TargetType.Player)), 3);
            game.Run(new DamagePlayerAction(p1, 5));

            game.Run(new DamageCreatureAction(medic, 1));
            Assert.AreEqual(Player.MaxHealth - 4, p1.Health, "A hit the creature survives should fire its on-damage effect");

            game.Run(new ModifyArmorAction(medic, 3));
            game.Run(new DamageCreatureAction(medic, 2));
            Assert.AreEqual(Player.MaxHealth - 3, p1.Health, "So should a hit its armor takes");

            game.Run(new DamageCreatureAction(medic, 10));
            Assert.IsTrue(medic.IsDead);
            Assert.AreEqual(Player.MaxHealth - 2, p1.Health, "And the killing blow: retaliation happens even as it dies");

            // Replaced (Golden Parachute): a summon onto its slot sets it off, and its Last Will doesn't: it isn't killed
            var parachuteData = game.Creature("Parachute", 1, 3, 1, Effect(EffectTrigger.OnReplaced, ActionType.Damage, 2, TargetType.Enemy),
                Effect(EffectTrigger.OnDeath, ActionType.Damage, 5, TargetType.Enemy));
            game.Summon(p1, parachuteData, 4);
            int before = p2.Health;
            game.Summon(p1, game.Creature("Successor", 1, 3), 4);
            Assert.AreEqual(before - 2, p2.Health, "Replaced: its effect happens, its Last Will doesn't");
        }

        [UnityTest]
        public IEnumerator EndOfTurn_FiresAtTheEndOfEveryTurn_AndSelfEffectsOnlyHitTheCreatureItself() {
            var bystander = game.Summon(p1, game.Creature("Bystander", 1, 5), 0);
            var phoenix = game.Summon(p1, game.Creature("Phoenix", 1, 5, 1, Effect(EffectTrigger.EndOfTurn, ActionType.Damage, 2, TargetType.Self)), 1);

            yield return game.EndTurn();
            Assert.AreEqual(3, phoenix.Health);

            yield return game.EndTurn();
            Assert.AreEqual(1, phoenix.Health);
            CollectionAssert.AreEqual(new[] { 2, 2 }, game.History.DamageTo(phoenix), "Once per turn");
            Assert.AreEqual(5, bystander.Health, "Self means the creature itself");
            Assert.AreEqual(Player.MaxHealth, p1.Health, "Self means the creature itself");
            Assert.AreEqual(Player.MaxHealth, p2.Health, "Self means the creature itself");
        }
    }
}
