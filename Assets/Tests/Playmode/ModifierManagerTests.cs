using NUnit.Framework;
using System;
using System.Collections;
using System.Linq;
using UnityEngine.TestTools;
using static SlayTheClone.Enums;

namespace SlayTheClone.Tests {
    public class ModifierManagerTests {
        private GameManager gameManager;
        private TurnManager turnManager;
        private ModifierManager modifierManager;
        private IModifierFactory factory;
        private IActionsQueue actionsQueue;
        private Creature testCreatureP1;
        private Creature testCreatureP2;

        // Helper class to hold spawn results
        private class SpawnResult {
            public Creature Creature { get; set; }
            public BattlefieldSlot Slot { get; set; }
        }

        // Helper to spawn a creature and return the concrete Creature type
        private IEnumerator SpawnCreatureForTest(IPlayer player, int cardDataIndex) {
            var deckList = player.IsPlayer1 ? gameManager.GameReferences.GetPlayer1DeckCards() : gameManager.GameReferences.GetPlayer2DeckCards();
            var cardData = deckList[cardDataIndex] as CreatureData;
            Assert.IsNotNull(cardData, $"Card data at index {cardDataIndex} is not CreatureData.");

            ICard cardToSpawn = CardFactory.CreateCard(cardData);
            Assert.IsNotNull(cardToSpawn, "Failed to create creature card instance.");
            ICreature creatureToSpawn = cardToSpawn as ICreature;
            Assert.IsNotNull(creatureToSpawn, "Card is not a creature.");

            BattlefieldSlot targetSlot = player.Battlefield.First(s => !s.IsOccupied());
            Assert.IsNotNull(targetSlot, $"No empty slot for Player {(player.IsPlayer1 ? "1" : "2")} to spawn creature.");

            actionsQueue.AddAction(new SummonCreatureAction(creatureToSpawn, player, targetSlot, false));
            actionsQueue.ResolveActions();
            yield return null;

            Creature spawnedCreature = targetSlot.OccupyingCreature as Creature; // Cast to concrete type
            Assert.IsNotNull(spawnedCreature, "Spawned creature is not a concrete Creature type.");

            var result = new SpawnResult { Creature = spawnedCreature, Slot = targetSlot };
            yield return result;
        }

        // Use the helper for setup
        [UnitySetUp]
        public IEnumerator Setup() {
            // First set up the core game systems
            bool setupComplete = false;
            GameManager gameManager = null;
            GameMediator gameMediator = null;
            GameReferences gameReferences = null;
            TurnManager turnManager = null;
            ModifierManager modifierManager = null;

            yield return TestSetupHelper.SetupSceneAndWait((gm, med, refs, tm, mm) => {
                gameManager = gm;
                gameMediator = med;
                gameReferences = refs;
                turnManager = tm;
                modifierManager = mm;
                setupComplete = true;
            });

            Assert.IsTrue(setupComplete, "Test setup did not complete");

            // Store references
            this.gameManager = gameManager;
            this.turnManager = turnManager;
            this.modifierManager = modifierManager;
            factory = modifierManager.ModifierFactory;
            actionsQueue = gameManager.ActionsQueue;

            // Now spawn creatures
            var spawnP1 = SpawnCreatureForTest(gameManager.Player1, 0);
            while (spawnP1.MoveNext()) {
                if (spawnP1.Current is SpawnResult result) {
                    testCreatureP1 = result.Creature;
                }
                else {
                    yield return spawnP1.Current;
                }
            }

            var spawnP2 = SpawnCreatureForTest(gameManager.Player2, 0);
            while (spawnP2.MoveNext()) {
                if (spawnP2.Current is SpawnResult result) {
                    testCreatureP2 = result.Creature;
                }
                else {
                    yield return spawnP2.Current;
                }
            }

            Assert.IsNotNull(testCreatureP1, "Failed to spawn test creature for Player 1");
            Assert.IsNotNull(testCreatureP2, "Failed to spawn test creature for Player 2");
        }

        [TearDown]
        public void Teardown() {
            // Cleanup logic if needed, often handled by scene reload
            gameManager = null;
            turnManager = null;
            modifierManager = null;
            factory = null;
            actionsQueue = null;
            testCreatureP1 = null;
            testCreatureP2 = null;
        }

        // --- Existing Tests (Adapted) ---

        [UnityTest]
        public IEnumerator ModifierManager_ApplyFlatAttackModifier_IncreasesAttack() {
            // Arrange
            int initialAttack = testCreatureP1.Attack;
            int modifierValue = 5;
            IModifier attackMod = factory.CreateStatModifier("Test Attack Buff", "+5 Attack", ModifiableStat.Attack, ModifierCalculationType.Flat, modifierValue);

            // Act
            modifierManager.ApplyModifier(testCreatureP1, attackMod);
            yield return null;

            // Assert
            Assert.AreEqual(initialAttack + modifierValue, testCreatureP1.Attack, $"Initial: {initialAttack}");
            Assert.IsTrue(modifierManager.HasModifier(testCreatureP1, m => m.Id == attackMod.Id));
        }

        [UnityTest]
        public IEnumerator ModifierManager_ApplyTimedHealthModifier_ExpiresAfterTurns() {
            // Arrange
            int initialMaxHealth = testCreatureP1.MaxHealth;
            int modifierValue = 10;
            int duration = 2;
            int startTurn = turnManager.TurnNumber;
            IModifier timedHealthMod = factory.CreateTimedStatModifier(
                "Test Timed Health Buff", "+10 Max Health (2 Turns)", ModifiableStat.Health, ModifierCalculationType.Flat, modifierValue, duration, startTurn
            );

            // Act & Assert Apply
            modifierManager.ApplyModifier(testCreatureP1, timedHealthMod);
            yield return null;
            Assert.AreEqual(initialMaxHealth + modifierValue, testCreatureP1.MaxHealth);
            Assert.IsTrue(modifierManager.HasModifier(testCreatureP1, m => m.Id == timedHealthMod.Id));

            // Act & Assert Turns Before Expiry
            for (int i = 0; i < duration - 1; i++) {
                turnManager.EndTurn();
                yield return null; // Wait for ProcessEndOfTurn
                Assert.IsTrue(modifierManager.HasModifier(testCreatureP1, m => m.Id == timedHealthMod.Id), $"Expired after {i + 1} turn(s).");
                Assert.AreEqual(initialMaxHealth + modifierValue, testCreatureP1.MaxHealth);
            }

            // Act & Assert Expiry
            turnManager.EndTurn();
            yield return null; // Wait for ProcessEndOfTurn
            Assert.IsFalse(modifierManager.HasModifier(testCreatureP1, m => m.Id == timedHealthMod.Id));
            Assert.AreEqual(initialMaxHealth, testCreatureP1.MaxHealth);
        }

        [UnityTest]
        public IEnumerator ModifierManager_ApplyParalyzeStatus_PreventsActionsAndExpires() {
            // Arrange
            int duration = 1;
            int startTurn = turnManager.TurnNumber;
            IModifier paralyzeMod = factory.CreateStatusEffectModifier(
                "Test Paralyze", "Paralyzed (1 Turn)", StatusEffectType.Paralyzed, duration, 0, startTurn
            );

            // Act & Assert Apply
            modifierManager.ApplyModifier(testCreatureP1, paralyzeMod);
            yield return null;
            Assert.IsTrue(modifierManager.HasStatusEffect(testCreatureP1, StatusEffectType.Paralyzed));
            Assert.IsTrue(modifierManager.AreActionsPrevented(testCreatureP1));
            Assert.IsTrue(modifierManager.HasModifier(testCreatureP1, m => m.Id == paralyzeMod.Id));

            // Act & Assert Expiry
            turnManager.EndTurn();
            yield return null; // Wait for ProcessEndOfTurn
            Assert.IsFalse(modifierManager.HasStatusEffect(testCreatureP1, StatusEffectType.Paralyzed));
            Assert.IsFalse(modifierManager.AreActionsPrevented(testCreatureP1));
            Assert.IsFalse(modifierManager.HasModifier(testCreatureP1, m => m.Id == paralyzeMod.Id));
        }

        // --- NEW TESTS ---

        [UnityTest]
        public IEnumerator ModifierManager_ApplyPercentageAttackModifier_CalculatesCorrectly() {
            // Arrange
            int baseAttack = testCreatureP1.BaseAttack; // Use base for reliable calculation start
            int percentageIncrease = 50; // +50%
            IModifier percentAttackMod = factory.CreateStatModifier(
                "Test Percent Attack Buff", "+50% Attack", ModifiableStat.Attack, ModifierCalculationType.Percentage, percentageIncrease
            );
            int expectedAttack = (int)Math.Round(baseAttack * (1.0f + (percentageIncrease / 100.0f)));

            // Act
            modifierManager.ApplyModifier(testCreatureP1, percentAttackMod);
            yield return null;

            // Assert
            Assert.AreEqual(expectedAttack, testCreatureP1.Attack, $"Base: {baseAttack}, Expected: {expectedAttack}");
            Assert.IsTrue(modifierManager.HasModifier(testCreatureP1, m => m.Id == percentAttackMod.Id));
        }

        [UnityTest]
        public IEnumerator ModifierManager_ExplicitlyRemoveModifier_RevertsStat() {
            // Arrange
            int initialSpeed = testCreatureP1.Speed;
            int modifierValue = -2;
            int duration = 5;
            int startTurn = turnManager.TurnNumber;
            IModifier timedSpeedMod = factory.CreateTimedStatModifier(
                "Test Slow Debuff", "-2 Speed (5 Turns)", ModifiableStat.Speed, ModifierCalculationType.Flat, modifierValue, duration, startTurn
            );

            // Act & Assert Apply
            modifierManager.ApplyModifier(testCreatureP1, timedSpeedMod);
            yield return null;
            int expectedSpeedAfterApply = Math.Max(0, initialSpeed + modifierValue);
            Assert.AreEqual(expectedSpeedAfterApply, testCreatureP1.Speed);
            Assert.IsTrue(modifierManager.HasModifier(testCreatureP1, m => m.Id == timedSpeedMod.Id));

            // Act: Explicit Remove
            modifierManager.RemoveModifier(testCreatureP1, timedSpeedMod);
            yield return null;

            // Assert: Removed Correctly
            Assert.IsFalse(modifierManager.HasModifier(testCreatureP1, m => m.Id == timedSpeedMod.Id));
            Assert.AreEqual(initialSpeed, testCreatureP1.Speed);
        }

        [UnityTest]
        public IEnumerator ModifierManager_StackFlatAndPercentageAttack_CalculatesCorrectly() {
            // Arrange
            int baseAttack = testCreatureP1.BaseAttack;
            int flatValue = 5;
            int percentValue = 20; // +20%
            IModifier flatMod = factory.CreateStatModifier("Flat Att +5", "+5 Attack", ModifiableStat.Attack, ModifierCalculationType.Flat, flatValue);
            IModifier percMod = factory.CreateStatModifier("Perc Att +20%", "+20% Attack", ModifiableStat.Attack, ModifierCalculationType.Percentage, percentValue);
            // Expected: (Base + Flat) * Percentage
            int expectedAttack = (int)Math.Round((baseAttack + flatValue) * (1.0f + (percentValue / 100.0f)));

            // Act
            modifierManager.ApplyModifier(testCreatureP1, flatMod);
            modifierManager.ApplyModifier(testCreatureP1, percMod);
            yield return null; // Allow recalculation

            // Assert
            Assert.AreEqual(expectedAttack, testCreatureP1.Attack, $"Base: {baseAttack}, Expected: {expectedAttack}");
            Assert.IsTrue(modifierManager.HasModifier(testCreatureP1, m => m.Id == flatMod.Id));
            Assert.IsTrue(modifierManager.HasModifier(testCreatureP1, m => m.Id == percMod.Id));
        }
    }
}
