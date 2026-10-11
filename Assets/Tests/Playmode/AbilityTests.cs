using System.Collections;
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
        public void OnPlay_FiresOnceWhenTheCreatureLands() {
            var wolf = game.GiveCard(p1, game.Creature("Wolf", 2, 3, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.Enemy)));

            game.Run(new PlayCardAction(wolf, p1, p1.Battlefield[0]));
            Assert.AreEqual(Player.MaxHealth - 2, p2.Health);

            game.StartTurn();
            game.StartTurn();
            CollectionAssert.AreEqual(new[] { 2 }, game.History.DamageTo(p2), "The on-play effect fired again on a later turn");
        }

        [Test]
        public void OnPlay_OfACreatureThatIsNotOperativeYet_WaitsForIt_ThenFiresOnce() {
            var golemData = game.Creature("Golem", 3, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.Enemy));
            golemData.turnsUntilOperative = 2;

            var golem = game.Summon(p1, golemData, 0);
            Assert.AreEqual(Player.MaxHealth, p2.Health, "Fired before the creature was operative");

            game.StartTurn();
            Assert.IsFalse(golem.IsOperative);
            Assert.AreEqual(Player.MaxHealth, p2.Health, "Fired before the creature was operative");

            game.StartTurn();
            Assert.IsTrue(golem.IsOperative);
            Assert.AreEqual(Player.MaxHealth - 2, p2.Health, "Didn't fire when the creature became operative");

            game.StartTurn();
            CollectionAssert.AreEqual(new[] { 2 }, game.History.DamageTo(p2), "Fired more than once");
        }

        [Test]
        public void OnDeath_FiresOnceWhenTheCreatureDies() {
            var martyr = game.Summon(p1, game.Creature("Martyr", 1, 2, 1, Effect(EffectTrigger.OnDeath, ActionType.Damage, 3, TargetType.Enemy)), 0);

            game.Run(new DamageCreatureAction(martyr, 5));
            game.Run(new DamageCreatureAction(martyr, 5)); // Hit again after it died

            Assert.IsTrue(martyr.IsDead);
            CollectionAssert.AreEqual(new[] { martyr }, game.History.Deaths, "The creature should die exactly once");
            CollectionAssert.AreEqual(new[] { 3 }, game.History.DamageTo(p2), "The death effect should fire exactly once");
        }

        [Test]
        public void OnDeath_DoesNotFireForACreatureThatIsReplaced() {
            game.Summon(p1, game.Creature("Martyr", 1, 2, 1, Effect(EffectTrigger.OnDeath, ActionType.Damage, 3, TargetType.Enemy)), 0);

            game.Summon(p1, game.Creature("Newcomer", 1, 2), 0);

            CollectionAssert.IsEmpty(game.History.Deaths, "Being replaced isn't dying");
            Assert.AreEqual(Player.MaxHealth, p2.Health, "The replaced creature's death effect fired");
        }

        [Test]
        public void OnDamage_FiresWhenTheCreatureSurvivesAHit_ButNotWhenArmorTakesItOrOnTheKillingBlow() {
            var medic = game.Summon(p1, game.Creature("Medic", 0, 5, 1, Effect(EffectTrigger.OnDamage, ActionType.Heal, 1, TargetType.Player)), 0);
            game.Run(new DamagePlayerAction(p1, 5));

            game.Run(new DamageCreatureAction(medic, 1));
            Assert.AreEqual(Player.MaxHealth - 4, p1.Health, "A hit the creature survives should fire its on-damage effect");

            game.Run(new ModifyArmorAction(medic, 3));
            game.Run(new DamageCreatureAction(medic, 2));
            Assert.AreEqual(Player.MaxHealth - 4, p1.Health, "A hit its armor takes shouldn't");

            game.Run(new DamageCreatureAction(medic, 10));
            Assert.IsTrue(medic.IsDead);
            Assert.AreEqual(Player.MaxHealth - 4, p1.Health, "The killing blow shouldn't either");
        }

        [Test]
        public void OnDamage_StrikesBackAtTheCreatureThatDealtTheDamage() {
            var thorns = game.Summon(p2, game.Creature("Thorns", 0, 6, 1, Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.Enemy)), 0);
            var hound = game.Summon(p1, game.Creature("Hound", 2, 5), 0);

            game.Attack(hound, p2.Battlefield[0]);

            Assert.AreEqual(4, thorns.Health);
            CollectionAssert.AreEqual(new[] { 1 }, game.History.DamageTo(hound), "The attacker should be struck back once");
            Assert.AreEqual(Player.MaxHealth, p1.Health, "The strike back hits the attacker, not its player");
        }

        [Test]
        public void StartOfTurn_FiresEveryTurn_OnceTheCreatureIsOperative() {
            game.SetDecks(game.Fillers(10), game.Fillers(10));
            game.Summon(p1, game.Creature("Owl", 1, 3, 1, Effect(EffectTrigger.StartOfTurn, ActionType.Draw, 1, TargetType.Player)), 0);
            var sleepyData = game.Creature("Sleepy Owl", 1, 3, 1, Effect(EffectTrigger.StartOfTurn, ActionType.Draw, 1, TargetType.Player));
            sleepyData.turnsUntilOperative = 2;
            game.Summon(p2, sleepyData, 0);

            game.StartTurn();
            Assert.AreEqual(1, p1.Hand.Count, "An operative creature's start-of-turn effect should fire");
            Assert.AreEqual(0, p2.Hand.Count, "A creature that isn't operative yet has its effects off");

            game.StartTurn();
            Assert.AreEqual(2, p1.Hand.Count, "It fires every turn");
            Assert.AreEqual(1, p2.Hand.Count, "A creature takes part in the turn it becomes operative");
        }

        [UnityTest]
        public IEnumerator EndOfTurn_FiresAtTheEndOfEveryTurn() {
            var phoenix = game.Summon(p1, game.Creature("Phoenix", 1, 5, 1, Effect(EffectTrigger.EndOfTurn, ActionType.Damage, 2, TargetType.Self)), 0);

            yield return game.EndTurn();
            Assert.AreEqual(3, phoenix.Health);

            yield return game.EndTurn();
            Assert.AreEqual(1, phoenix.Health);
            CollectionAssert.AreEqual(new[] { 2, 2 }, game.History.DamageTo(phoenix), "Once per turn");
        }

        [Test]
        public void SelfEffects_OnlyAffectTheCreatureItself() {
            var bystander = game.Summon(p1, game.Creature("Bystander", 1, 5), 0);
            var falcon = game.Summon(p1, game.Creature("Falcon", 3, 3, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.Self)), 1);

            Assert.AreEqual(2, falcon.Health);
            Assert.AreEqual(5, bystander.Health);
            Assert.AreEqual(Player.MaxHealth, p1.Health);
            Assert.AreEqual(Player.MaxHealth, p2.Health);
        }

        [Test]
        public void ASpellWithNoEffects_DrawsACard() {
            game.SetDecks(game.Fillers(5), game.Fillers(5));
            var scroll = game.GiveCard(p1, game.Spell("Blank Scroll", TargetType.Player));

            game.Run(new PlayCardAction(scroll, p1, p1.Battlefield[0]));

            Assert.AreEqual(1, p1.Hand.Count);
            Assert.AreNotSame(scroll, p1.Hand[0]);
        }

        [Test]
        public void AParalyzingDeathEffect_ParalyzesEveryEnemyCreature() {
            var jelly = game.Summon(p1, game.Creature("Jelly", 1, 1, 1, Status(EffectTrigger.OnDeath, StatusEffectType.Paralyzed, 1, TargetType.EnemyCreatures)), 0);
            var enemyA = game.Summon(p2, game.Creature("Enemy A", 1, 3), 0);
            var enemyB = game.Summon(p2, game.Creature("Enemy B", 1, 3), 4);

            game.Run(new DamageCreatureAction(jelly, 5));

            Assert.IsTrue(game.ModifierManager.HasStatusEffect(enemyA, StatusEffectType.Paralyzed));
            Assert.IsTrue(game.ModifierManager.HasStatusEffect(enemyB, StatusEffectType.Paralyzed));
            Assert.IsFalse(game.ModifierManager.CanAct(enemyA));
        }

        [UnityTest, Category(TestCategories.KnownBug), Description("#49: burn damage counts the burning creature as its own attacker")]
        public IEnumerator Burn_DoesNotMakeACreatureThatStrikesBackHitItself() {
            var spiky = game.Summon(p1, game.Creature("Spiky", 1, 5, 1, Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.Enemy)), 0);
            game.Run(new ApplyStatusEffectAction(spiky, StatusEffectType.Burned, 2, 1));

            yield return game.EndTurn();

            Assert.AreEqual(4, spiky.Health, "One burn tick of 1 should cost 1 health");
        }
    }
}
