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
            Assert.AreEqual(Player.StartingHealth - 4, p1.Health, "A hit the creature survives should fire its on-damage effect");

            game.Run(new ModifyArmorAction(medic, 3));
            game.Run(new DamageCreatureAction(medic, 2));
            Assert.AreEqual(Player.StartingHealth - 3, p1.Health, "So should a hit its armor takes");

            game.Run(new DamageCreatureAction(medic, 10));
            Assert.IsTrue(medic.IsDead);
            Assert.AreEqual(Player.StartingHealth - 2, p1.Health, "And the killing blow: retaliation happens even as it dies");

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
            Assert.AreEqual(Player.StartingHealth, p1.Health, "Self means the creature itself");
            Assert.AreEqual(Player.StartingHealth, p2.Health, "Self means the creature itself");
        }

        // The Pokémon picks (the user, 2026-10-17): Wonder Room, Ditto, Trick (as stats) and Imprison
        [Test]
        public void PokemonPicks_LiminalSpace_MonkeySee_FreakyFriday_AndTheDMCA() {
            // Liminal Space: Attack and Health trade places for everyone, newcomers too, and back when it ends
            var wall = game.Summon(p1, game.Creature("Wall", 1, 7), 0);
            var cannon = game.Summon(p2, game.Creature("Cannon", 6, 2), 0);
            game.Run(new SetTrendAction(Trend.LiminalSpace, 2, p1));
            Assert.AreEqual((7, 1), (wall.Attack, wall.Health));
            Assert.AreEqual((2, 6), (cannon.Attack, cannon.Health));
            var late = game.Summon(p1, game.Creature("Late", 2, 5), 1);
            Assert.AreEqual((5, 2), (late.Attack, late.Health), "A creature entering while it trends is traded too");
            game.StartTurn();
            Assert.AreEqual(Trend.LiminalSpace, game.TrendSystem.Current);
            game.StartTurn();
            Assert.AreEqual(Trend.None, game.TrendSystem.Current, "Over after its 2 Days");
            Assert.AreEqual((1, 7), (wall.Attack, wall.Health), "...and everyone trades back");
            Assert.AreEqual((2, 5), (late.Attack, late.Health));

            // Monkey See, Monkey Do: a fresh copy of the target in the slot facing it, on the caster's side
            game.Dispose();
            SetUp();
            var model = game.Summon(p2, game.Creature("Model", 4, 4, 2), 2);
            var old = game.Summon(p1, game.Creature("Old", 1, 1), 2);
            game.Run(new MimicAction(model, p1));
            var copy = p1.Battlefield[2].OccupyingCreature;
            Assert.IsNotNull(copy, "A copy appeared");
            Assert.AreNotSame(old, copy, "Whatever stood there is discarded");
            Assert.AreEqual(("Model", 4, 4, true), (copy.Name, copy.Attack, copy.Health, copy.IsToken));

            // Freaky Friday: the two trade Attack and Health, current Health with the body
            var tall = game.Summon(p1, game.Creature("Tall", 1, 8), 3);
            var sharp = game.Summon(p2, game.Creature("Sharp", 5, 3), 3);
            game.Run(TargetedActions.Damage(sharp, 1));
            game.Run(new TradeStatsAction(tall, sharp));
            Assert.AreEqual((5, 2, 3), (tall.Attack, tall.Health, tall.MaxHealth), "The wall wakes up as the hurt cannon");
            Assert.AreEqual((1, 8, 8), (sharp.Attack, sharp.Health, sharp.MaxHealth), "...and the cannon as the wall");

            // The DMCA: the enemy can't play a card that shares its name with a creature on the board
            game.Run(new SealAction(p2, 3));
            var twin = game.GiveCard(p2, game.Creature("Model", 4, 4, 2));
            game.Run(new PlayCardAction(twin, p2, p2.Battlefield[4]));
            Assert.IsNull(p2.Battlefield[4].OccupyingCreature, "Refused: a Model is already on the board");
            CollectionAssert.Contains(p2.Hand, twin, "It stays in the hand");
            var nobody = game.GiveCard(p2, game.Creature("Nobody Yet", 1, 1));
            game.Run(new PlayCardAction(nobody, p2, p2.Battlefield[4]));
            Assert.IsNotNull(p2.Battlefield[4].OccupyingCreature, "A name not on the board plays as ever");
        }
    }
}
