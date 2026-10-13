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

        // An empty board for the next part of a test
        private void Fresh() {
            game.Dispose();
            SetUp();
        }

        private static ModifyAction LoseHealth(ICreature creature, int amount, int days = 0) =>
            new ModifyAction(creature, -amount, modifyAttack: false, modifyHealth: true, modifySpeed: false, durationInTurns: days);

        [Test]
        public void DeathSequence_Saves_CancelledAttacks_DeathEffects_AndTheGameEndsOnce() {
            game.SetDecks(game.Fillers(5), game.Fillers(5));
            var save = Effect(EffectTrigger.OnAboutToDie, ActionType.Heal, 1, TargetType.Self);
            save.oncePerCombat = true;
            var plotArmorData = game.Creature("Plot Armor", 2, 2, 2, save);
            var hero = game.Summon(p1, plotArmorData, 0);
            game.Run(new DamageCreatureAction(hero, 5));
            Assert.AreEqual(1, hero.Health, "OnAboutToDie saves it from a lethal hit");
            game.Run(new DamageCreatureAction(hero, 5));
            Assert.IsTrue(hero.IsDead, "Once per combat: not twice");
            game.CardDealingService.RecycleDiscardPile(p1);
            var again = (ICreature)p1.Deck.GetDeckPreview().Single(c => c.Name == "Plot Armor");
            game.Run(new SummonCreatureAction(again, p1, p1.Battlefield[1], fromDeck: true));
            game.Run(new DamageCreatureAction(again, 5));
            Assert.IsTrue(again.IsDead, "Reshuffling doesn't give the use back");
            StringAssert.Contains("Rebirth", Keywords.Plain(CardTextFormatter.GetRulesText(plotArmorData)), "A recurring pattern is printed as its keyword");

            var frail = game.Summon(p1, game.Creature("Frail", 1, 3, 1, Effect(EffectTrigger.OnDeath, ActionType.Damage, 2, TargetType.Enemy)), 2);
            game.Run(LoseHealth(frail, 3));
            Assert.IsTrue(frail.IsDead, "Losing max Health to 0 kills it");
            Assert.IsNull(p1.Battlefield[2].OccupyingCreature);
            CollectionAssert.AreEqual(new[] { 2 }, game.History.DamageTo(p2), "...and its OnDied effect fires");

            Fresh();
            var fast = game.Summon(p1, game.Creature("Fast", 5, 5, speed: 2), 0);
            var spiky = game.Summon(p2, game.Creature("Spiky", 1, 3, 1, Effect(EffectTrigger.OnDamage, ActionType.Damage, 0, TargetType.Enemy)), 0);
            game.Declare(spiky, p1.Battlefield[0]);
            game.Declare(fast, p2.Battlefield[0]);
            game.Resolve();
            Assert.IsTrue(spiky.IsDead);
            Assert.AreEqual(1, game.History.ExecutedKinds().Count(kind => kind == "BattlefieldCombat"), "The dying creature's attack is cancelled, not resolved");
            Assert.AreEqual(4, fast.Health, "Its Counterattack still hits the attacker, for its Attack");

            // Counterattack answers attacks only: hitting back isn't an attack (two of them never trade blows forever), nor is a spell
            var counter = Effect(EffectTrigger.OnDamage, ActionType.Damage, 0, TargetType.Enemy);
            var hedgehog = game.Summon(p1, game.Creature("Hedgehog", 2, 10, 1, counter), 4);
            var porcupine = game.Summon(p2, game.Creature("Porcupine", 3, 10, 1, counter), 4);
            game.Declare(hedgehog, p2.Battlefield[4]);
            game.Resolve();
            Assert.AreEqual(8, porcupine.Health);
            Assert.AreEqual(7, hedgehog.Health, "Hit back once, for the Porcupine's Attack, and not answered");
            game.Run(new DamageCreatureAction(porcupine, 1));
            Assert.AreEqual(7, hedgehog.Health, "Damage that isn't an attack isn't answered");

            var giantA = game.Summon(p1, game.Creature("Giant A", 15, 5), 1);
            var giantB = game.Summon(p1, game.Creature("Giant B", 15, 5), 2);
            game.Declare(giantA, p2.Battlefield[1]);
            game.Declare(giantB, p2.Battlefield[2]);
            game.Resolve();
            Assert.AreEqual(0, p2.Health);
            CollectionAssert.AreEqual(new[] { 15, 5 }, game.History.DamageTo(p2), "A player's Health stops at 0");
            CollectionAssert.AreEqual(new[] { p1 }, game.History.Winners, "The game ends once, won by player 1");
        }

        [Test]
        public void ArmorAndHealthLoss_ArmorTakesHitsFirst_IsCapped_IsLostLeavingPlay_HealthLossIgnoresIt() {
            var wall = game.Summon(p2, game.Creature("Wall", 0, 5), 0);
            game.Run(new ModifyArmorAction(wall, 3));
            var first = game.Summon(p1, game.Creature("First", 2, 5), 0);
            var second = game.Summon(p1, game.Creature("Second", 2, 5), 1);
            game.Declare(first, p2.Battlefield[0]);
            game.Declare(second, p2.Battlefield[0]);
            game.Resolve();
            Assert.AreEqual(0, wall.CurrentArmorPool);
            CollectionAssert.AreEqual(new[] { 1 }, game.History.DamageTo(wall), "Armor is used up across the Day's hits: only 1 gets past");

            var guard = game.Summon(p2, game.Creature("Guard", 0, 3), 1);
            game.Run(new ModifyArmorAction(guard, 10));
            Assert.AreEqual(6, guard.CurrentArmorPool, "Armor above twice the max Health is lost");
            game.Run(LoseHealth(guard, 2));
            Assert.AreEqual(2, guard.CurrentArmorPool, "Losing max Health lowers the cap, and the Armor with it");
            guard.EnterPlay(game.TurnClock.TurnNumber); // What landing on the board again does
            Assert.AreEqual(0, guard.CurrentArmorPool, "A creature always enters play without Armor");

            var spiky = game.Summon(p1, game.Creature("Spiky", 0, 5, 1, Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.Enemy)), 2);
            game.Run(new ModifyArmorAction(spiky, 3));
            game.Run(LoseHealth(spiky, 2));
            Assert.AreEqual(3, spiky.Health);
            Assert.AreEqual(3, spiky.MaxHealth, "Health Loss lowers max and current Health");
            Assert.AreEqual(3, spiky.CurrentArmorPool, "Armor doesn't absorb it");
            Assert.AreEqual(Player.MaxHealth, p2.Health, "It isn't damage: OnDamaged doesn't trigger");
            game.Summon(p1, game.Creature("Replacement", 1, 1), 2); // Summoning over it discards it
            var discarded = (ICreature)p1.Deck.GetDiscardPilePreview().Single(c => c.Name == "Spiky");
            Assert.AreEqual(3, discarded.BaseHealth, "Permanent unless it says otherwise: it stays on the card");

            var tiny = game.Summon(p1, game.Creature("Tiny", 0, 1), 3);
            game.Run(new ModifyArmorAction(tiny, 2));
            game.Run(LoseHealth(tiny, 1));
            Assert.IsTrue(tiny.IsDead, "Health Loss kills at 0 whatever the Armor");
        }

        [UnityTest]
        public IEnumerator OverTime_ArmorFades_BurnTicks_AndTimedHealthChangesEnd() {
            var wall = game.Summon(p2, game.Creature("Wall", 0, 5), 0);
            game.Run(new ModifyArmorAction(wall, 3));
            var poker = game.Summon(p1, game.Creature("Poker", 1, 5), 0);
            var torch = game.Summon(p1, game.Creature("Torch", 0, 10), 1);
            game.Run(new ApplyStatusEffectAction(torch, StatusEffectType.Burned, 2, 2));
            var pumped = game.Summon(p1, game.Creature("Pumped", 1, 3), 2);
            game.Run(new ModifyAction(pumped, 2, modifyAttack: false, modifyHealth: true, modifySpeed: false, durationInTurns: 1));
            Assert.AreEqual(5, pumped.Health, "+2 Health raises current Health with the maximum");
            game.Run(new DamageCreatureAction(pumped, 4));
            var drained = game.Summon(p1, game.Creature("Drained", 0, 5), 3);
            game.Run(LoseHealth(drained, 2, days: 1));
            Assert.AreEqual(3, drained.Health);
            Assert.AreEqual(5, drained.BaseHealth, "A timed Health Loss isn't a Persistent Stat Modification");

            game.Declare(poker, p2.Battlefield[0]);
            yield return game.EndTurn();
            Assert.AreEqual(1, wall.CurrentArmorPool, "The hit takes 1 Armor during the Day, then Dusk fades 1 more");
            Assert.AreEqual(5, wall.Health, "The hit landed on the full 3 Armor, before it faded");
            Assert.AreEqual(10, torch.Health, "Every status skips the Day it's applied (Status Timing)");
            Assert.IsTrue(pumped.IsDead, "The Health buff wearing off took its Health back: it was all that kept it alive");
            Assert.AreEqual(5, drained.Health, "The timed Health Loss ended: max and current Health go back up");

            yield return game.EndTurn();
            Assert.AreEqual(8, torch.Health);
            yield return game.EndTurn();
            Assert.AreEqual(6, torch.Health);
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(torch, StatusEffectType.Burned), "The burn runs out after 2 ticks");
            Assert.AreEqual(0, wall.CurrentArmorPool, "Armor never fades below 0");
            yield return game.EndTurn();
            CollectionAssert.AreEqual(new[] { 2, 2 }, game.History.DamageTo(torch));
        }
    }
}
