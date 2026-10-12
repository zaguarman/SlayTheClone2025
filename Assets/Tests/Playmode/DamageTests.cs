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

        #region Death sequence (GDD 3.9)
        [Test]
        public void OnAboutToDie_CanSaveACreatureFromALethalHit() {
            var survivor = game.Summon(p1, game.Creature("Survivor", 1, 3, 1, Effect(EffectTrigger.OnAboutToDie, ActionType.Heal, 2, TargetType.Self)), 0);

            game.Run(new DamageCreatureAction(survivor, 5));

            Assert.IsFalse(survivor.IsDead, "Its OnAboutToDie heal should save it");
            Assert.AreEqual(2, survivor.Health);
            Assert.AreSame(survivor, p1.Battlefield[0].OccupyingCreature);
            CollectionAssert.IsEmpty(game.History.Deaths);
        }

        [Test]
        public void ACreatureAboutToDie_LosesItsPendingAttack_ButStillRetaliates() {
            var fast = game.Summon(p1, game.Creature("Fast", 5, 5, speed: 2), 0);
            var spiky = game.Summon(p2, game.Creature("Spiky", 5, 3, 1, Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.Enemy)), 0);
            game.History.Clear();

            game.Declare(spiky, p1.Battlefield[0]);
            game.Declare(fast, p2.Battlefield[0]);
            game.Resolve();

            Assert.IsTrue(spiky.IsDead);
            Assert.AreEqual(1, game.History.ExecutedKinds().Count(kind => kind == "BattlefieldCombat"), "The dying creature's attack should be cancelled, not resolved");
            Assert.AreEqual(4, fast.Health, "Its retaliation still hits the attacker");
        }

        [Test]
        public void LosingMaxHealthToZero_KillsTheCreature_AndItsDeathEffectFires() {
            var frail = game.Summon(p1, game.Creature("Frail", 1, 3, 1, Effect(EffectTrigger.OnDeath, ActionType.Damage, 2, TargetType.Enemy)), 0);

            game.Run(new ModifyAction(frail, -3, modifyAttack: false, modifyHealth: true, modifySpeed: false));

            Assert.IsTrue(frail.IsDead);
            Assert.IsNull(p1.Battlefield[0].OccupyingCreature);
            CollectionAssert.AreEqual(new[] { frail }, game.History.Deaths);
            CollectionAssert.AreEqual(new[] { 2 }, game.History.DamageTo(p2), "Its OnDied effect should fire");
        }

        [UnityTest]
        public IEnumerator AHealthBuffWearingOff_TakesItsHealthBack_AndCanKill() {
            var pumped = game.Summon(p1, game.Creature("Pumped", 1, 3), 0);
            game.Run(new ModifyAction(pumped, 2, modifyAttack: false, modifyHealth: true, modifySpeed: false, durationInTurns: 1));
            Assert.AreEqual(5, pumped.Health, "+2 Health raises current Health with the maximum");

            game.Run(new DamageCreatureAction(pumped, 4));
            Assert.AreEqual(1, pumped.Health);

            yield return game.EndTurn(); // The buff wears off: 1 - 2 Health
            Assert.IsTrue(pumped.IsDead, "The buff was all that kept it alive");
        }
        #endregion

        #region Health Loss
        [Test]
        public void HealthLoss_LowersMaxAndCurrentHealth_IgnoresArmor_IsntDamage_AndStaysOnTheCard() {
            var spiky = game.Summon(p1, game.Creature("Spiky", 0, 5, 1, Effect(EffectTrigger.OnDamage, ActionType.Damage, 1, TargetType.Enemy)), 0);
            game.Run(new ModifyArmorAction(spiky, 3));

            game.Run(new ModifyAction(spiky, -2, modifyAttack: false, modifyHealth: true, modifySpeed: false));

            Assert.AreEqual(3, spiky.MaxHealth);
            Assert.AreEqual(3, spiky.Health);
            Assert.AreEqual(3, spiky.CurrentArmorPool, "Armor doesn't absorb Health Loss");
            Assert.AreEqual(Player.MaxHealth, p2.Health, "It isn't damage: OnDamaged doesn't trigger");

            game.Summon(p1, game.Creature("Replacement", 1, 1), 0); // Summoning over it discards it
            var discarded = (ICreature)p1.Deck.GetDiscardPilePreview().Single(c => c.Name == "Spiky");
            Assert.AreEqual(3, discarded.BaseHealth, "Permanent unless it says otherwise: it stays on the card");
        }

        [Test]
        public void HealthLoss_KillsAt0_WhateverTheArmor() {
            var wall = game.Summon(p1, game.Creature("Wall", 0, 1), 0);
            game.Run(new ModifyArmorAction(wall, 2));

            game.Run(new ModifyAction(wall, -1, modifyAttack: false, modifyHealth: true, modifySpeed: false));

            Assert.IsTrue(wall.IsDead);
        }

        [Test]
        public void ATimedHealthLoss_GivesTheHealthBackWhenItEnds_AndLeavesTheCardAlone() {
            var target = game.Summon(p1, game.Creature("Target", 0, 5), 0);

            game.Run(new ModifyAction(target, -2, modifyAttack: false, modifyHealth: true, modifySpeed: false, durationInTurns: 1));
            Assert.AreEqual(3, target.Health);
            Assert.AreEqual(5, target.BaseHealth, "A timed -X Health isn't a Persistent Stat Modification");

            game.StartTurn();
            Assert.AreEqual(5, target.MaxHealth);
            Assert.AreEqual(5, target.Health, "When it ends, maximum and current Health both go back up");
        }
        #endregion

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
        public void Armor_IsCappedAtTwiceMaxHealth_AndALowerMaxLowersIt() {
            var guard = game.Summon(p1, game.Creature("Guard", 0, 3), 0);

            game.Run(new ModifyArmorAction(guard, 10));
            Assert.AreEqual(6, guard.CurrentArmorPool, "Armor above twice the max Health is lost");

            game.Run(new ModifyAction(guard, -2, modifyAttack: false, modifyHealth: true, modifySpeed: false));
            Assert.AreEqual(1, guard.MaxHealth);
            Assert.AreEqual(2, guard.CurrentArmorPool, "Losing max Health lowers the cap, and the Armor with it");
        }

        [UnityTest]
        public IEnumerator Armor_FadesBy1AtDusk_AfterTheDaysHitsLand() {
            var wall = game.Summon(p2, game.Creature("Wall", 0, 5), 0);
            game.Run(new ModifyArmorAction(wall, 3));
            var poker = game.Summon(p1, game.Creature("Poker", 1, 5), 0);

            game.Declare(poker, p2.Battlefield[0]);
            yield return game.EndTurn();

            Assert.AreEqual(1, wall.CurrentArmorPool, "The hit takes 1 Armor during the Day, then Dusk fades 1 more");
            Assert.AreEqual(5, wall.Health, "The hit landed on the full 3 Armor, before it faded");

            yield return game.EndTurn();
            yield return game.EndTurn();
            Assert.AreEqual(0, wall.CurrentArmorPool, "Armor never fades below 0");
        }

        [Test]
        public void Armor_IsLostWhenACreatureLeavesPlay() {
            var bouncer = game.Summon(p1, game.Creature("Bouncer", 0, 5), 0);
            game.Run(new ModifyArmorAction(bouncer, 4));

            bouncer.EnterPlay(game.TurnClock.TurnNumber); // What landing on the board again does

            Assert.AreEqual(0, bouncer.CurrentArmorPool, "A creature always enters play without Armor");
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
        public IEnumerator Burn_TicksOnceADay_FromTheDayAfterItsApplied_UntilItRunsOut() {
            var torch = game.Summon(p1, game.Creature("Torch", 0, 10), 0);
            game.Run(new ApplyStatusEffectAction(torch, StatusEffectType.Burned, 2, 2));

            yield return game.EndTurn();
            Assert.AreEqual(10, torch.Health, "Every status skips the Day it's applied (Status Timing)");

            yield return game.EndTurn();
            Assert.AreEqual(8, torch.Health);

            yield return game.EndTurn();
            Assert.AreEqual(6, torch.Health);
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(torch, StatusEffectType.Burned), "The burn should have run out after 2 ticks");

            yield return game.EndTurn();
            Assert.AreEqual(6, torch.Health);
            CollectionAssert.AreEqual(new[] { 2, 2 }, game.History.DamageTo(torch));
        }
    }
}
