using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Status effects start when the 'Status Effects Compendium' says, do what it says, and end on time.
    /// Runs the real rules without the scene (see <see cref="TestGame"/>).
    /// </summary>
    public class StatusTests {
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

        private bool IsExhausted(ICreature creature) => game.ModifierManager.HasStatusEffect(creature, StatusEffectType.Exhausted);

        #region Exhausted
        [UnityTest]
        public IEnumerator Exhausted_CancelsWhatTheCreaturePlanned_AndStunsItForTheRestOfTheDayAndAllOfTheNext() {
            var brute = game.Summon(p1, game.Creature("Brute", 4, 5), 0);
            var stunner = game.Spell("Stunner", TargetType.EnemyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Stun, 1, TargetType.EnemyCreatures));

            game.Declare(brute, p2.Battlefield[0]); // An empty slot: the attack would hit player 2
            game.Play(game.GiveCard(p2, stunner), p2, p1.Battlefield[0]);
            yield return game.EndTurn(); // Spells land before attacks
            Assert.AreEqual(Player.MaxHealth, p2.Health, "Being stunned cancels the attack it planned");
            Assert.IsTrue(IsExhausted(brute), "It skipped the Dusk of the Day it was applied");

            Assert.IsFalse(game.ModifierManager.CanAct(brute), "It's stunned for all of the next Day");
            game.Declare(brute, p2.Battlefield[0]);
            yield return game.EndTurn();
            Assert.AreEqual(Player.MaxHealth, p2.Health, "It couldn't be given an intent");
            Assert.IsFalse(IsExhausted(brute), "It ends at the Dusk of the Day after it was applied");

            Assert.IsTrue(game.ModifierManager.CanAct(brute));
        }

        [Test]
        public void Exhausted_DisablesAbilities_AndTheCreatureTakesOneMoreDamageFromEveryHit() {
            var spiky = game.Summon(p1, game.Creature("Spiky", 0, 10, 1, Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.Enemy)), 0);
            var hitter = game.Summon(p2, game.Creature("Hitter", 2, 5), 0);
            game.Run(new ApplyStatusEffectAction(spiky, StatusEffectType.Exhausted, 1, 0));

            game.Run(new DamageCreatureAction(spiky, 2, hitter));

            Assert.AreEqual(7, spiky.Health, "Exposed: 2 damage becomes 3");
            Assert.AreEqual(5, hitter.Health, "Its OnDamage retaliation is disabled");
        }

        [UnityTest]
        public IEnumerator ReStunning_RefreshesExhausted_ItNeverStacksIntoALongerStun() {
            var target = game.Summon(p1, game.Creature("Target", 1, 5), 0);
            game.Run(new ApplyStatusEffectAction(target, StatusEffectType.Exhausted, 1, 0));
            yield return game.EndTurn();

            game.Run(new ApplyStatusEffectAction(target, StatusEffectType.Exhausted, 1, 0)); // The next Day
            Assert.AreEqual(1, game.ModifierManager.GetActiveModifiersFor(target).Count(), "One Exhausted, refreshed");

            yield return game.EndTurn();
            Assert.IsTrue(IsExhausted(target), "Refreshed: it now lasts through the Day after the re-stun");

            yield return game.EndTurn();
            Assert.IsFalse(IsExhausted(target));
            Assert.IsTrue(game.ModifierManager.CanAct(target));
        }
        #endregion
    }
}
