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

        #region Doomed
        [Test]
        public void Doomed_GrowsEachDawn_ThenLastRitesHitsTheOppositeSlot_AndItDies() {
            game.SetDecks(game.Fillers(5), game.Fillers(5));
            var condemned = game.Summon(p1, game.Creature("Condemned", 2, 5, 1,
                Effect(EffectTrigger.OnAboutToDie, ActionType.Heal, 5, TargetType.Self),
                Effect(EffectTrigger.OnDeath, ActionType.Damage, 1, TargetType.Enemy)), 1);
            var opposite = game.Summon(p2, game.Creature("Opposite", 0, 10), 1);
            game.Run(new ApplyStatusEffectAction(condemned, StatusEffectType.Doomed, 2, 0));

            game.StartTurn();
            Assert.AreEqual(2, condemned.Attack, "Like every status, it skips the Day it's applied");
            game.StartTurn();
            Assert.AreEqual(3, condemned.Attack, "+1 Attack at the Dawn of the Day after next");
            Assert.IsFalse(condemned.IsDead);

            game.StartTurn(); // Its 2 Days run out at this Dusk
            Assert.AreEqual(7, opposite.Health, "Last Rites hits the slot opposite with its current Attack");
            Assert.IsTrue(condemned.IsDead, "Nothing saves it, not even its own OnAboutToDie heal");
            CollectionAssert.AreEqual(new[] { 1 }, game.History.DamageTo(p2), "It's a real death: its OnDied effect fires");

            var discarded = (ICreature)p1.Deck.GetDiscardPilePreview().Single(c => c.Name == "Condemned");
            Assert.AreEqual(3, discarded.BaseAttack, "The Attack it gained is a Persistent Stat Modification: it stays on the card");

            game.CardDealingService.RecycleDiscardPile(p1);
            var reshuffled = (ICreature)p1.Deck.GetDeckPreview().Single(c => c.Name == "Condemned");
            Assert.AreEqual(2, reshuffled.BaseAttack, "Reshuffling resets the card");
        }

        [Test]
        public void Doomed_WipesOtherStatuses_AndNothingNewLands_NotEvenAnotherDoomed() {
            var vessel = game.Summon(p1, game.Creature("Vessel", 1, 10), 0);
            game.Run(new ApplyStatusEffectAction(vessel, StatusEffectType.Burned, 3, 1));
            game.Run(new ApplyStatusEffectAction(vessel, StatusEffectType.Doomed, 2, 0));
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(vessel, StatusEffectType.Burned), "Doomed removes every other status");

            game.Run(new ApplyStatusEffectAction(vessel, StatusEffectType.Exhausted, 1, 0));
            game.Run(new ApplyStatusEffectAction(vessel, StatusEffectType.Doomed, 9, 0));
            Assert.IsFalse(IsExhausted(vessel), "A Doomed creature is immune to new statuses");
            Assert.AreEqual(1, game.ModifierManager.GetActiveModifiersFor(vessel).Count(), "Only the first Doomed");

            game.StartTurn();
            game.StartTurn();
            game.StartTurn();
            Assert.IsTrue(vessel.IsDead, "The second Doomed didn't extend the sentence");
        }

        [Test]
        public void LastRites_StrikesEvenIfTheCreatureCannotAct() {
            var deploying = game.Creature("Deploying", 4, 5);
            deploying.turnsUntilOperative = 9;
            var condemned = game.Summon(p1, deploying, 0);
            game.Run(new ApplyStatusEffectAction(condemned, StatusEffectType.Doomed, 1, 0));
            Assert.IsFalse(game.ModifierManager.CanAct(condemned));

            game.StartTurn();
            game.StartTurn();
            Assert.IsTrue(condemned.IsDead);
            CollectionAssert.AreEqual(new[] { 4 }, game.History.DamageTo(p2), "Its Last Rites hit the empty slot opposite: player 2");
        }
        #endregion
    }
}
