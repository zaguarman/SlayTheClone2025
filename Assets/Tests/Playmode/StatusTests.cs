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

        // An empty board for the next part of a test
        private void Fresh() {
            game.Dispose();
            SetUp();
        }

        private bool IsExhausted(ICreature creature) => game.ModifierManager.HasStatusEffect(creature, StatusEffectType.Exhausted);
        private bool IsDoomed(ICreature creature) => game.ModifierManager.HasStatusEffect(creature, StatusEffectType.Doomed);
        private StatusEffectModifier BlessingOf(ICreature creature) => StatusRules.Find(game.ModifierManager, creature, StatusEffectType.Blessed);

        [UnityTest]
        public IEnumerator Exhausted_CancelsThePlan_StunsForTheRestOfTheDayAndTheNext_Refreshes_AndExposes() {
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

            game.Run(new ApplyStatusEffectAction(brute, StatusEffectType.Exhausted, 1, 0));
            yield return game.EndTurn();
            game.Run(new ApplyStatusEffectAction(brute, StatusEffectType.Exhausted, 1, 0)); // The next Day
            Assert.AreEqual(1, game.ModifierManager.GetActiveModifiersFor(brute).Count(), "Re-stunning refreshes it, it never stacks");
            yield return game.EndTurn();
            Assert.IsTrue(IsExhausted(brute), "Refreshed: it now lasts through the Day after the re-stun");
            yield return game.EndTurn();
            Assert.IsFalse(IsExhausted(brute));

            var spiky = game.Summon(p1, game.Creature("Spiky", 0, 10, 1, Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.Enemy)), 1);
            var hitter = game.Summon(p2, game.Creature("Hitter", 2, 5), 1);
            game.Run(new ApplyStatusEffectAction(spiky, StatusEffectType.Exhausted, 1, 0));
            game.Run(new DamageCreatureAction(spiky, 2, hitter));
            Assert.AreEqual(7, spiky.Health, "Exposed: 2 damage becomes 3");
            Assert.AreEqual(5, hitter.Health, "Its OnDamage retaliation is disabled");
        }

        [Test]
        public void Doomed_WipesAndBlocksStatuses_GrowsEachDawn_ThenLastRites_AndItDiesForReal() {
            game.SetDecks(game.Fillers(5), game.Fillers(5));
            var condemned = game.Summon(p1, game.Creature("Condemned", 2, 5, 1,
                Effect(EffectTrigger.OnAboutToDie, ActionType.Heal, 5, TargetType.Self),
                Effect(EffectTrigger.OnDeath, ActionType.Damage, 1, TargetType.Enemy)), 1);
            var opposite = game.Summon(p2, game.Creature("Opposite", 0, 10), 1);
            game.Run(new ApplyStatusEffectAction(condemned, StatusEffectType.Burned, 3, 1));
            game.Run(new ApplyStatusEffectAction(condemned, StatusEffectType.Doomed, 2, 0));
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(condemned, StatusEffectType.Burned), "Doomed removes every other status");
            game.Run(new ApplyStatusEffectAction(condemned, StatusEffectType.Exhausted, 1, 0));
            game.Run(new ApplyStatusEffectAction(condemned, StatusEffectType.Doomed, 9, 0));
            Assert.AreEqual(1, game.ModifierManager.GetActiveModifiersFor(condemned).Count(), "Immune to new statuses, another Doomed included");

            game.StartTurn();
            Assert.AreEqual(2, condemned.Attack, "Like every status, it skips the Day it's applied");
            game.StartTurn();
            Assert.AreEqual(3, condemned.Attack, "+1 Attack at the Dawn of the Day after next");
            game.StartTurn(); // Its 2 Days run out at this Dusk (the second Doomed didn't extend it)
            Assert.AreEqual(7, opposite.Health, "Last Rites hits the slot opposite with its current Attack");
            Assert.IsTrue(condemned.IsDead, "Nothing saves it, not even its own OnAboutToDie heal");
            CollectionAssert.AreEqual(new[] { 1 }, game.History.DamageTo(p2), "It's a real death: its OnDied effect fires");

            var discarded = (ICreature)p1.Deck.GetDiscardPilePreview().Single(c => c.Name == "Condemned");
            Assert.AreEqual(3, discarded.BaseAttack, "The Attack it gained is a Persistent Stat Modification: it stays on the card");
            game.CardDealingService.RecycleDiscardPile(p1);
            var reshuffled = (ICreature)p1.Deck.GetDeckPreview().Single(c => c.Name == "Condemned");
            Assert.AreEqual(2, reshuffled.BaseAttack, "Reshuffling resets the card");

            Fresh();
            var deploying = game.Creature("Deploying", 4, 5);
            deploying.turnsUntilOperative = 9;
            var waiting = game.Summon(p1, deploying, 0);
            game.Run(new ApplyStatusEffectAction(waiting, StatusEffectType.Doomed, 1, 0));
            game.StartTurn();
            game.StartTurn();
            Assert.IsTrue(waiting.IsDead);
            CollectionAssert.AreEqual(new[] { 4 }, game.History.DamageTo(p2), "Last Rites strikes even if it Cannot Act: the empty slot opposite is player 2");

            // A Kill Order: the creature is destroyed when it runs out, at the Dusk two Days later. It's secret: its own side
            // doesn't see it, not on the card, not landing, not in what their AI sees; the side that gave it does
            Fresh();
            var mark = game.Summon(p2, game.Creature("Mark", 5, 30), 0);
            var order = new ApplyStatusEffectAction(mark, StatusEffectType.KillOrder, 2, 0, controller: p1);
            Assert.IsFalse(Intel.CanSee(p2, order, game.ActionsQueue, game.ModifierManager), "Its victim's side never sees it landing");
            Assert.IsTrue(Intel.CanSee(p1, order, game.ActionsQueue, game.ModifierManager));
            game.Run(order);
            Assert.IsFalse(new AIView(p2, game.ActionsQueue).Mine.Single().Has(StatusEffectType.KillOrder), "Their AI can't see it on its own creature");
            Assert.IsTrue(new AIView(p1, game.ActionsQueue).Theirs.Single().Has(StatusEffectType.KillOrder), "Whoever gave it can");
            game.StartTurn();
            game.StartTurn();
            Assert.IsFalse(mark.IsDead, "Like every status, it skips the Day it lands");
            game.StartTurn();
            Assert.IsTrue(mark.IsDead, "At the Dusk two Days later: its 30 Health don't matter");
            var spared = game.Summon(p2, game.Creature("Spared", 1, 3), 1);
            game.Run(new ApplyStatusEffectAction(spared, StatusEffectType.KillOrder, 1, 0, controller: p1));
            game.Run(new ApplyStatusEffectAction(spared, StatusEffectType.Untouchable, 3, 0, controller: p2));
            game.StartTurn();
            game.StartTurn();
            Assert.IsFalse(spared.IsDead, "Untouchable when it runs out: it lives");

            // Brainwashed: when it runs out, it changes sides for good and loses its ability
            var turncoat = game.Summon(p2, game.Creature("Turncoat", 3, 4), 3);
            turncoat.Ability = CreatureAbility.Recoil;
            game.Run(new ApplyStatusEffectAction(turncoat, StatusEffectType.Brainwashed, 2, 0, controller: p1));
            game.StartTurn();
            game.StartTurn();
            Assert.AreSame(p2, turncoat.Owner, "It skips the Day it lands...");
            game.StartTurn();
            Assert.AreSame(turncoat, p1.Battlefield[3].OccupyingCreature, "...then at the Dusk two Days later it comes over, to the slot facing it");
            Assert.AreEqual(CreatureAbility.None, turncoat.Ability, "Its ability is lost");
        }

        [Test]
        public void Blessed_NegatesTheNextStatuses_OneStackEach_IsPersistent_UpTo3_AndStopsDoomed() {
            var saint = game.Summon(p1, game.Creature("Saint", 1, 5), 0);
            game.Run(new ApplyStatusEffectAction(saint, StatusEffectType.Blessed, 1, 2));
            game.Run(new ApplyStatusEffectAction(saint, StatusEffectType.Exhausted, 1, 0));
            Assert.IsFalse(IsExhausted(saint), "The next status is negated, friendly or not");
            Assert.AreEqual(1, BlessingOf(saint).Stacks);
            game.Run(new ApplyStatusEffectAction(saint, StatusEffectType.Burned, 2, 1));
            Assert.IsNull(BlessingOf(saint), "Its last stack is used up");
            game.Run(new ApplyStatusEffectAction(saint, StatusEffectType.Exhausted, 1, 0));
            Assert.IsTrue(IsExhausted(saint), "Unwarded now");

            var pope = game.Summon(p1, game.Creature("Pope", 1, 5), 1);
            game.Run(new ApplyStatusEffectAction(pope, StatusEffectType.Blessed, 1, 2));
            game.Run(new ApplyStatusEffectAction(pope, StatusEffectType.Blessed, 1, 2));
            Assert.AreEqual(3, BlessingOf(pope).Stacks, "Re-applying adds stacks, up to 3");
            game.StartTurn();
            game.StartTurn();
            game.StartTurn();
            Assert.AreEqual(3, BlessingOf(pope).Stacks, "Time doesn't wear it down");
            game.Run(new ApplyStatusEffectAction(pope, StatusEffectType.Doomed, 1, 0));
            Assert.IsFalse(IsDoomed(pope), "Blessed is the one thing that stops Doomed");
            Assert.AreEqual(2, BlessingOf(pope).Stacks);
        }

        [Test]
        public void CleanseAndPurge_RemoveWhatTheySay_PersistentChangesStay() {
            var patient = game.Summon(p1, game.Creature("Patient", 4, 5), 0);
            game.Run(new ApplyStatusEffectAction(patient, StatusEffectType.Burned, 2, 1));
            game.Run(new ApplyStatusEffectAction(patient, StatusEffectType.Blessed, 1, 3)); // After the Burn, so it blocks nothing
            game.Run(new ModifyAction(patient, 2, modifyAttack: true, modifyHealth: false, modifySpeed: false));
            game.Run(new ModifyAction(patient, -1, modifyAttack: true, modifyHealth: false, modifySpeed: false));
            game.ModifierManager.ChangeBaseStats(patient, 1, 0, 0); // A Persistent Stat Modification
            Assert.AreEqual(6, patient.Attack);

            game.Run(new CleanseAction(patient, CleanseTarget.Statuses));
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(patient, StatusEffectType.Burned), "A Cleanse removes harmful statuses");
            Assert.IsNotNull(BlessingOf(patient), "Blessed is good for it: a Cleanse leaves it");
            Assert.AreEqual(6, patient.Attack, "Statuses only: the stat changes stay");
            game.Run(new CleanseAction(patient, CleanseTarget.StatModifications));
            Assert.AreEqual(7, patient.Attack, "The -1 goes, the +2 stays");

            game.Run(new CleanseAction(patient, CleanseTarget.Both, purge: true));
            Assert.IsNull(BlessingOf(patient), "A Purge removes good and bad: Blessed loses all its stacks");
            Assert.AreEqual(5, patient.Attack, "The temporary +2 is gone, the persistent +1 stays");
        }

        [Test]
        public void Heresy_CleansingDoomed_PassesItToTheWeakestAlly_FizzlesOrStrikesNow() {
            var heretic = game.Summon(p1, game.Creature("Heretic", 2, 5), 0);
            var vessel = game.Summon(p1, game.Creature("Vessel", 0, 3), 1); // Its Burn takes 1 before the cleanse
            game.Summon(p1, game.Creature("Sturdy", 0, 9), 2);
            game.Run(new ApplyStatusEffectAction(vessel, StatusEffectType.Burned, 3, 1));
            game.Run(new ApplyStatusEffectAction(heretic, StatusEffectType.Doomed, 3, 0));
            game.StartTurn();
            game.StartTurn();
            game.Run(new CleanseAction(heretic, CleanseTarget.Statuses));
            Assert.IsFalse(IsDoomed(heretic), "The heretic is freed");
            Assert.AreEqual(3, heretic.Attack, "...and keeps the Attack it gained");
            Assert.IsTrue(IsDoomed(vessel), "The lowest Health creature on its side takes it");
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(vessel, StatusEffectType.Burned), "The vessel loses its other statuses");
            game.StartTurn();
            Assert.IsFalse(vessel.IsDead, "One Day left");
            game.StartTurn();
            Assert.IsTrue(vessel.IsDead, "The sentence kept its remaining Days");

            Fresh();
            var stubborn = game.Summon(p1, game.Creature("Stubborn", 1, 9), 0);
            var condemned = game.Summon(p1, game.Creature("Condemned", 0, 2), 1);
            game.Run(new ApplyStatusEffectAction(condemned, StatusEffectType.Doomed, 3, 0));
            game.Run(new ApplyStatusEffectAction(stubborn, StatusEffectType.Doomed, 3, 0));
            game.Run(new CleanseAction(stubborn, CleanseTarget.Statuses));
            Assert.IsTrue(IsDoomed(stubborn), "The weakest is Doomed already: it fizzles");

            var saint = game.Summon(p2, game.Creature("Saint", 0, 2), 1);
            var cleric = game.Summon(p2, game.Creature("Cleric", 1, 5), 0);
            game.Run(new ApplyStatusEffectAction(saint, StatusEffectType.Blessed, 1, 2));
            game.Run(new ApplyStatusEffectAction(cleric, StatusEffectType.Doomed, 1, 0));
            game.Run(new CleanseAction(cleric, CleanseTarget.Statuses));
            Assert.IsFalse(IsDoomed(cleric) || IsDoomed(saint), "Onto a Blessed creature: the sentence is negated");
            Assert.AreEqual(1, BlessingOf(saint).Stacks);

            game.Run(new DestroyCreatureAction(saint, unconditional: true));
            game.Run(new DestroyCreatureAction(cleric, unconditional: true));
            var loner = game.Summon(p2, game.Creature("Loner", 3, 5), 4);
            game.Run(new ApplyStatusEffectAction(loner, StatusEffectType.Doomed, 3, 0));
            game.Run(new CleanseAction(loner, CleanseTarget.Statuses));
            Assert.IsTrue(loner.IsDead, "Nobody to pass it to: the sentence is carried out now");
            CollectionAssert.AreEqual(new[] { 3 }, game.History.DamageTo(p1), "Last Rites first, on the empty slot opposite");
        }

        [Test]
        public void Timing_RedTape_ThisDayBuffs_AndPersistentHeat() {
            var clerk = game.Summon(p1, game.Creature("Clerk", 3, 5, 1, Effect(EffectTrigger.OnPlay, ActionType.Damage, 1, TargetType.Enemy)), 0);
            var hero = game.Summon(p1, game.Creature("Hero", 1, 5), 1);
            var redTape = game.GiveCard(p2, game.Spell("Red Tape", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.DelayDeployment, 1, TargetType.EnemyCreatures)));
            game.Declare(clerk, p2.Battlefield[0]);
            game.Play(redTape, p2, p1.Battlefield[0]);
            game.Resolve(); // Spells before attacks
            Assert.AreEqual(Player.MaxHealth - 1, p2.Health, "Red Tape cancels its planned attack");
            Assert.IsFalse(game.ModifierManager.CanAct(clerk));
            game.Run(new ModifyAction(hero, 4, true, false, false, durationInTurns: 1));
            Assert.AreEqual(5, hero.Attack);

            game.StartTurn();
            Assert.IsFalse(clerk.IsOperative, "Not counted down at the Dusk of the Day it was added");
            Assert.AreEqual(1, hero.Attack, "A stat change for this Day ends at its Dusk");
            game.StartTurn();
            Assert.IsTrue(clerk.IsOperative);
            Assert.AreEqual(Player.MaxHealth - 1, p2.Health, "Its entry effect doesn't happen again");

            Fresh();
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
            game.Play(game.GiveCard(p1, game.Spell("Nap", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Stun, 1, TargetType.EnemyCreatures, TargetModifier.Random))), p1, null);
            game.Resolve();
            Assert.IsTrue(game.ModifierManager.HasStatusEffect(hot, StatusEffectType.Exhausted), "...and every other random effect too");
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(warm, StatusEffectType.Exhausted));
            var mine = game.Summon(p1, game.Creature("Mine", 0, 10), 0);
            game.Run(new ApplyStatusEffectAction(mine, StatusEffectType.HeatSignature, 1, 5));
            game.Play(game.GiveCard(p1, scatter), p1, null);
            game.Resolve();
            Assert.AreEqual(8, mine.Health, "Random damage is drawn from both sides: the caster's own hotter creature takes it");
            Assert.AreEqual(4, hot.Health);
            game.StartTurn();
            game.StartTurn();
            Assert.AreEqual(3, hot.Heat, "Persistent: it doesn't decay");
        }
    }
}
