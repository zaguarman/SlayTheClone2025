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

        #region Blessed
        private StatusEffectModifier BlessingOf(ICreature creature) => StatusRules.Find(game.ModifierManager, creature, StatusEffectType.Blessed);

        [Test]
        public void Blessed_NegatesTheNextStatus_FriendlyOrNot_AndLosesAStackEachTime() {
            var saint = game.Summon(p1, game.Creature("Saint", 1, 5), 0);
            game.Run(new ApplyStatusEffectAction(saint, StatusEffectType.Blessed, 1, 2));
            Assert.AreEqual(2, BlessingOf(saint).Stacks);

            game.Run(new ApplyStatusEffectAction(saint, StatusEffectType.Exhausted, 1, 0));
            Assert.IsFalse(IsExhausted(saint), "The first status is negated");
            Assert.AreEqual(1, BlessingOf(saint).Stacks);

            game.Run(new ApplyStatusEffectAction(saint, StatusEffectType.Burned, 2, 1));
            Assert.IsNull(BlessingOf(saint), "Its last stack is used up");
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(saint, StatusEffectType.Burned));

            game.Run(new ApplyStatusEffectAction(saint, StatusEffectType.Exhausted, 1, 0));
            Assert.IsTrue(IsExhausted(saint), "Unwarded now");
        }

        [Test]
        public void Blessed_IsPersistent_StacksUpTo3_AndStopsDoomed() {
            var saint = game.Summon(p1, game.Creature("Saint", 1, 5), 0);
            game.Run(new ApplyStatusEffectAction(saint, StatusEffectType.Blessed, 1, 2));
            game.Run(new ApplyStatusEffectAction(saint, StatusEffectType.Blessed, 1, 2));
            Assert.AreEqual(3, BlessingOf(saint).Stacks, "Re-applying adds stacks, up to 3");

            game.StartTurn();
            game.StartTurn();
            game.StartTurn();
            Assert.AreEqual(3, BlessingOf(saint).Stacks, "Time doesn't wear it down");

            game.Run(new ApplyStatusEffectAction(saint, StatusEffectType.Doomed, 1, 0));
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(saint, StatusEffectType.Doomed), "Blessed is the one thing that stops Doomed");
            Assert.AreEqual(2, BlessingOf(saint).Stacks);
        }
        #endregion

        #region Cleanse
        [Test]
        public void ACleanse_RemovesOnlyWhatHarmsTheCreature_StatusesOrStatChangesAsItSays() {
            var patient = game.Summon(p1, game.Creature("Patient", 4, 5), 0);
            game.Run(new ApplyStatusEffectAction(patient, StatusEffectType.Burned, 2, 1));
            game.Run(new ApplyStatusEffectAction(patient, StatusEffectType.Blessed, 1, 3)); // After the Burn, so it blocks nothing
            game.Run(new ModifyAction(patient, 2, modifyAttack: true, modifyHealth: false, modifySpeed: false));
            game.Run(new ModifyAction(patient, -1, modifyAttack: true, modifyHealth: false, modifySpeed: false));
            Assert.AreEqual(5, patient.Attack);

            game.Run(new CleanseAction(patient, CleanseTarget.Statuses));
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(patient, StatusEffectType.Burned), "Harmful statuses go");
            Assert.IsNotNull(BlessingOf(patient), "Blessed is good for it: a Cleanse leaves it");
            Assert.AreEqual(5, patient.Attack, "Statuses only: the stat changes stay");

            game.Run(new CleanseAction(patient, CleanseTarget.StatModifications));
            Assert.AreEqual(6, patient.Attack, "The -1 goes, the +2 stays");
        }

        [Test]
        public void APurge_RemovesGoodAndBadAlike_ButNeverPersistentChanges() {
            var target = game.Summon(p1, game.Creature("Target", 2, 5), 0);
            game.Run(new ApplyStatusEffectAction(target, StatusEffectType.Blessed, 1, 3));
            game.Run(new ModifyAction(target, 2, modifyAttack: true, modifyHealth: false, modifySpeed: false));
            game.ModifierManager.ChangeBaseStats(target, 1, 0, 0); // A Persistent Stat Modification

            game.Run(new CleanseAction(target, CleanseTarget.Both, purge: true));

            Assert.IsNull(BlessingOf(target), "Blessed loses all its stacks");
            Assert.AreEqual(3, target.Attack, "The temporary +2 is gone, the persistent +1 stays");
        }

        [Test]
        public void Heresy_CleansingDoomed_PassesItToTheWeakestFriendlyCreature_WithItsCountdown() {
            var heretic = game.Summon(p1, game.Creature("Heretic", 2, 5), 0);
            var vessel = game.Summon(p1, game.Creature("Vessel", 0, 3), 1); // Its Burn takes 1 before the cleanse
            game.Summon(p1, game.Creature("Sturdy", 0, 9), 2);
            game.Run(new ApplyStatusEffectAction(vessel, StatusEffectType.Burned, 3, 1));
            game.Run(new ApplyStatusEffectAction(heretic, StatusEffectType.Doomed, 3, 0));
            game.StartTurn();
            game.StartTurn();
            Assert.AreEqual(3, heretic.Attack, "It grew once before being cleansed");

            game.Run(new CleanseAction(heretic, CleanseTarget.Statuses));
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(heretic, StatusEffectType.Doomed), "The heretic is freed");
            Assert.AreEqual(3, heretic.Attack, "...and keeps the Attack it gained");
            Assert.IsTrue(game.ModifierManager.HasStatusEffect(vessel, StatusEffectType.Doomed), "The lowest Health creature on its side takes it");
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(vessel, StatusEffectType.Burned), "The vessel loses its other statuses");

            game.StartTurn();
            Assert.IsFalse(vessel.IsDead, "One Day left");
            game.StartTurn(); // The third Day after it was first applied ends: the original countdown runs out
            Assert.IsTrue(vessel.IsDead, "The sentence kept its remaining Days");
            Assert.IsFalse(heretic.IsDead);
        }

        [Test]
        public void Heresy_Fizzles_WhenTheWeakestCreatureIsDoomedAlready() {
            var heretic = game.Summon(p1, game.Creature("Heretic", 1, 9), 0);
            var condemned = game.Summon(p1, game.Creature("Condemned", 0, 2), 1);
            game.Run(new ApplyStatusEffectAction(condemned, StatusEffectType.Doomed, 3, 0));
            game.Run(new ApplyStatusEffectAction(heretic, StatusEffectType.Doomed, 3, 0));

            game.Run(new CleanseAction(heretic, CleanseTarget.Statuses));

            Assert.IsTrue(game.ModifierManager.HasStatusEffect(heretic, StatusEffectType.Doomed), "Nothing happens: it stays Doomed");
            Assert.IsFalse(heretic.IsDead);
        }

        [Test]
        public void HeatSignature_DrawsRandomlyTargetedAttacks_ToTheHottestCreature() {
            game.Summon(p2, game.Creature("Cold", 0, 10), 0);
            var warm = game.Summon(p2, game.Creature("Warm", 0, 10), 1);
            var hot = game.Summon(p2, game.Creature("Hot", 0, 10), 2);
            game.Run(new ApplyStatusEffectAction(warm, StatusEffectType.HeatSignature, 1, 1));
            game.Run(new ApplyStatusEffectAction(hot, StatusEffectType.HeatSignature, 1, 1));
            game.Run(new ApplyStatusEffectAction(hot, StatusEffectType.HeatSignature, 1, 2));
            Assert.AreEqual(3, hot.Heat, "Heat stacks add up");

            var scatter = game.Spell("Scatter", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.EnemyCreatures, TargetModifier.Random));
            for (int i = 0; i < 3; i++) {
                game.Play(game.GiveCard(p1, scatter), p1, null);
                game.Resolve();
            }
            Assert.AreEqual(4, hot.Health, "Every random hit lands on the most Heat");
            Assert.AreEqual(10, warm.Health);

            game.StartTurn();
            game.StartTurn();
            Assert.AreEqual(3, hot.Heat, "Persistent: it doesn't decay");
        }

        [Test]
        public void Heresy_WithNobodyToPassItTo_CarriesTheSentenceOutNow() {
            var heretic = game.Summon(p1, game.Creature("Heretic", 3, 5), 0);
            game.Run(new ApplyStatusEffectAction(heretic, StatusEffectType.Doomed, 3, 0));

            game.Run(new CleanseAction(heretic, CleanseTarget.Statuses));

            Assert.IsTrue(heretic.IsDead);
            CollectionAssert.AreEqual(new[] { 3 }, game.History.DamageTo(p2), "Last Rites first, on the empty slot opposite");
        }

        [Test]
        public void Heresy_OntoABlessedCreature_UsesAStack_AndTheSentenceIsNegated() {
            var heretic = game.Summon(p1, game.Creature("Heretic", 1, 5), 0);
            var saint = game.Summon(p1, game.Creature("Saint", 0, 2), 1);
            game.Run(new ApplyStatusEffectAction(saint, StatusEffectType.Blessed, 1, 2));
            game.Run(new ApplyStatusEffectAction(heretic, StatusEffectType.Doomed, 1, 0));

            game.Run(new CleanseAction(heretic, CleanseTarget.Statuses));

            Assert.IsFalse(game.ModifierManager.HasStatusEffect(heretic, StatusEffectType.Doomed));
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(saint, StatusEffectType.Doomed));
            Assert.AreEqual(1, BlessingOf(saint).Stacks);
            game.StartTurn();
            game.StartTurn();
            Assert.IsFalse(heretic.IsDead || saint.IsDead, "Nobody is doomed any more");
        }
        #endregion

        #region Deployment Time and timed stat changes (the Red Tape and YOLO cards)
        [Test]
        public void RedTape_AddsDeploymentTime_TheCreatureSitsOutTheRestOfTheDayAndTheNext_WithoutRedoingItsEntry() {
            var ready = game.Summon(p1, game.Creature("Clerk", 3, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.Enemy)), 0);
            Assert.AreEqual(Player.MaxHealth - 1, p2.Health, "Its entry effect, once");
            var redTape = game.GiveCard(p2, game.Spell("Red Tape", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.DelayDeployment, 1, TargetType.EnemyCreatures)));
            game.Declare(ready, p2.Battlefield[0]);

            game.Play(redTape, p2, p1.Battlefield[0]);
            game.Resolve(); // Spells before attacks
            Assert.AreEqual(Player.MaxHealth - 1, p2.Health, "Its planned attack is cancelled");
            Assert.IsFalse(game.ModifierManager.CanAct(ready));

            game.StartTurn();
            Assert.IsFalse(ready.IsOperative, "Not counted down at the Dusk of the Day it was added");
            game.StartTurn();
            Assert.IsTrue(ready.IsOperative);
            Assert.AreEqual(Player.MaxHealth - 1, p2.Health, "Its entry effect doesn't happen again");
        }

        [Test]
        public void AStatChangeForThisDay_EndsAtThatDaysDusk() {
            var hero = game.Summon(p1, game.Creature("Hero", 1, 5), 0);
            game.Run(new ModifyAction(hero, 4, true, false, false, durationInTurns: 1));
            Assert.AreEqual(5, hero.Attack);

            game.StartTurn();
            Assert.AreEqual(1, hero.Attack);
        }
        #endregion
    }
}
