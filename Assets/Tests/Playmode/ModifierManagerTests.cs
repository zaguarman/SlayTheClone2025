using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Linq;
using UnityEngine.SceneManagement;
using static Enums; // Make sure Enums are accessible
using System;      // For Guid
using System.Collections.Generic; // For List

public class ModifierManagerTests {
    private const string TestSceneName = "Balatro-Feel"; // Ensure this matches your scene name

    private GameManager gameManager;
    private GameMediator gameMediator;
    private GameReferences gameReferences;
    private GameUI gameUI;
    private TurnManager turnManager;
    private ModifierManager modifierManager;

    // --- Test Setup Helper ---
    private IEnumerator SetupSceneAndWait() {
        // --- 1. Load the Scene ---
        Debug.Log($"[Test] Loading scene: {TestSceneName}...");
        var loadOperation = SceneManager.LoadSceneAsync(TestSceneName, LoadSceneMode.Single);
        while (!loadOperation.isDone) {
            yield return null;
        }
        Debug.Log($"[Test] Scene {TestSceneName} loaded.");

        // --- 2. Find Core Components in the Loaded Scene ---
        yield return null; // Allow a frame for Awake
        yield return null; // Allow a frame for Start

        gameManager = UnityEngine.Object.FindObjectOfType<GameManager>();
        gameMediator = UnityEngine.Object.FindObjectOfType<GameMediator>();
        gameReferences = UnityEngine.Object.FindObjectOfType<GameReferences>();
        gameUI = UnityEngine.Object.FindObjectOfType<GameUI>();
        turnManager = UnityEngine.Object.FindObjectOfType<TurnManager>(); // Find TurnManager

        // Assert core singletons are found
        Assert.IsNotNull(gameManager, $"GameManager not found in scene '{TestSceneName}'. Make sure it's present and active.");
        Assert.IsNotNull(gameMediator, $"GameMediator not found in scene '{TestSceneName}'. Make sure it's present and active.");
        Assert.IsNotNull(gameReferences, $"GameReferences not found in scene '{TestSceneName}'. Make sure it's present and active.");
        Assert.IsNotNull(gameUI, $"GameUI not found in scene '{TestSceneName}'. Make sure it's present and active.");
        Assert.IsNotNull(turnManager, $"TurnManager not found in scene '{TestSceneName}'. Make sure it's present and active.");

        // --- 3. Wait for Initialization to Complete ---
        Debug.Log("[Test] Waiting for scene components to initialize...");
        yield return new WaitUntil(() => gameReferences != null && gameReferences.IsInitialized);
        Debug.Log("[Test] GameReferences Initialized.");
        yield return new WaitUntil(() => gameMediator != null && gameMediator.IsInitialized);
        Debug.Log("[Test] GameMediator Initialized.");
        yield return new WaitUntil(() => gameManager != null && gameManager.IsInitialized);
        Debug.Log("[Test] GameManager Initialized.");
        // Get ModifierManager *after* GameManager is initialized
        modifierManager = gameManager.ModifierManager;
        Assert.IsNotNull(modifierManager, "ModifierManager is null after GameManager initialized.");
        Debug.Log("[Test] ModifierManager reference obtained.");
        yield return new WaitUntil(() => gameUI != null && gameUI.IsInitialized);
        Debug.Log("[Test] GameUI Initialized.");

        // --- 4. Wait for Initial Creature Placement ---
        // The GameManager's Initialize likely places creatures. Wait a few frames for actions to potentially resolve.
        yield return null;
        yield return null;
        yield return null;

        Debug.Log("[Test] Scene setup presumed complete.");

        // --- 5. Final Checks ---
        Assert.IsTrue(gameReferences.AreReferencesValid(), "GameReferences in the scene has missing inspector assignments.");
        Assert.IsNotNull(gameManager.Player1, "GameManager did not initialize Player1 in the scene.");
        Assert.IsNotNull(gameManager.Player1.Battlefield, "Player 1 Battlefield is null.");
        Assert.IsTrue(gameManager.Player1.Battlefield.Any(), "Player 1 Battlefield is empty after setup.");
        // Ensure at least one creature was placed for testing
        Assert.IsTrue(gameManager.Player1.Battlefield.Any(slot => slot.IsOccupied()), "No creature found on Player 1's battlefield after setup. Check GameManager.PlaceInitialCreatures.");
    }

    // Helper to get the first available creature for testing
    private Creature GetFirstAvailableCreature(IPlayer player) {
        Assert.IsNotNull(player, "Player is null when trying to find a creature.");
        Assert.IsNotNull(player.Battlefield, "Player's battlefield is null.");

        var occupiedSlot = player.Battlefield.FirstOrDefault(slot => slot.IsOccupied() && slot.OccupyingCreature != null);
        Assert.IsNotNull(occupiedSlot, $"No occupied slot with a creature found for player {(player.IsPlayer1() ? "1" : "2")}.");

        var creature = occupiedSlot.OccupyingCreature as Creature;
        Assert.IsNotNull(creature, "Occupying entity is not a concrete Creature.");

        // Ensure stats are calculated before returning
        modifierManager.RecalculateStats(creature);

        return creature;
    }

    // --- TESTS ---

    [UnityTest]
    public IEnumerator ModifierManager_ApplyFlatAttackModifier_IncreasesAttack() {
        // Arrange
        yield return SetupSceneAndWait();
        Creature creature = GetFirstAvailableCreature(gameManager.Player1);
        int initialAttack = creature.Attack;
        int modifierValue = 5;
        IModifier attackMod = modifierManager._modifierFactory.CreateStatModifier("Test Attack Buff", "+5 Attack", ModifiableStat.Attack, ModifierCalculationType.Flat, modifierValue);

        // Act
        modifierManager.ApplyModifier(creature, attackMod);
        yield return null; // Allow frame for potential recalculation/events

        // Assert
        Assert.AreEqual(initialAttack + modifierValue, creature.Attack, $"Creature attack did not increase correctly. Initial: {initialAttack}, Expected: {initialAttack + modifierValue}, Actual: {creature.Attack}");
        Assert.IsTrue(modifierManager.GetActiveModifiersFor(creature).Any(m => m.Id == attackMod.Id), "Attack modifier was not found in the active list.");
    }

    [UnityTest]
    public IEnumerator ModifierManager_ApplyTimedHealthModifier_ExpiresAfterTurns() {
        // Arrange
        yield return SetupSceneAndWait();
        Creature creature = GetFirstAvailableCreature(gameManager.Player1);
        int initialMaxHealth = creature.MaxHealth;
        int modifierValue = 10;
        int duration = 2;
        int startTurn = turnManager.TurnNumber;
        IModifier timedHealthMod = modifierManager._modifierFactory.CreateTimedStatModifier(
            "Test Timed Health Buff", "+10 Max Health (2 Turns)", ModifiableStat.Health, ModifierCalculationType.Flat, modifierValue, duration, startTurn
        );

        // Act: Apply Modifier
        modifierManager.ApplyModifier(creature, timedHealthMod);
        yield return null;

        // Assert: Applied Correctly
        Assert.AreEqual(initialMaxHealth + modifierValue, creature.MaxHealth, "Timed health modifier did not apply correctly initially.");
        Assert.IsTrue(modifierManager.HasModifier(creature, timedHealthMod.Id), "Timed health modifier was not active after applying.");

        // Act: Advance Turns (Duration - 1 times to reach the turn *before* expiry)
        Debug.Log($"[Test] Advancing {duration - 1} turns (Duration: {duration})...");
        for (int i = 0; i < duration - 1; i++) {
            turnManager.EndTurn();
            yield return null; // IMPORTANT: Wait a frame for ModifierManager.ProcessEndOfTurn to run
            Debug.Log($"[Test] Completed Turn {turnManager.TurnNumber}. Modifier should still be active.");
            Assert.IsTrue(modifierManager.HasModifier(creature, timedHealthMod.Id), $"Modifier expired prematurely after {i + 1} turn(s).");
            Assert.AreEqual(initialMaxHealth + modifierValue, creature.MaxHealth, $"Max Health incorrect before expiry on turn {turnManager.TurnNumber}.");

        }

        // Act: Advance one more turn (the turn it should expire)
        Debug.Log($"[Test] Advancing final turn ({duration} total). Modifier should expire now.");
        turnManager.EndTurn();
        yield return null; // IMPORTANT: Wait a frame for ModifierManager.ProcessEndOfTurn

        // Assert: Expired Correctly
        Debug.Log($"[Test] Completed Turn {turnManager.TurnNumber}. Modifier should be gone.");
        Assert.IsFalse(modifierManager.HasModifier(creature, timedHealthMod.Id), "Timed health modifier did not expire after correct duration.");
        Assert.AreEqual(initialMaxHealth, creature.MaxHealth, "Creature max health did not revert after modifier expired.");
    }

    [UnityTest]
    public IEnumerator ModifierManager_ApplyParalyzeStatus_PreventsActionsAndExpires() {
        // Arrange
        yield return SetupSceneAndWait();
        Creature creature = GetFirstAvailableCreature(gameManager.Player1);
        int duration = 1;
        int startTurn = turnManager.TurnNumber;
        IModifier paralyzeMod = modifierManager._modifierFactory.CreateStatusEffectModifier(
            "Test Paralyze", "Paralyzed (1 Turn)", StatusEffectType.Paralyzed, duration, 0, startTurn
        );

        // Act: Apply Modifier
        modifierManager.ApplyModifier(creature, paralyzeMod);
        yield return null;

        // Assert: Applied Correctly
        Assert.IsTrue(modifierManager.HasStatusEffect(creature, StatusEffectType.Paralyzed), "Paralyze status effect not detected after applying.");
        Assert.IsTrue(modifierManager.AreActionsPrevented(creature), "Actions were not prevented after applying Paralyze.");
        Assert.IsTrue(modifierManager.HasModifier(creature, paralyzeMod.Id), "Paralyze modifier instance not found after applying.");


        // Act: Advance Turns (Duration turns)
        Debug.Log($"[Test] Advancing {duration} turn(s) for Paralyze (Duration: {duration})...");
        for (int i = 0; i < duration; i++) {
            turnManager.EndTurn();
            yield return null; // Wait for ProcessEndOfTurn
            Debug.Log($"[Test] Completed Turn {turnManager.TurnNumber}.");
        }

        // Assert: Expired Correctly
        Assert.IsFalse(modifierManager.HasStatusEffect(creature, StatusEffectType.Paralyzed), "Paralyze status effect did not expire.");
        Assert.IsFalse(modifierManager.AreActionsPrevented(creature), "Actions were still prevented after Paralyze should have expired.");
        Assert.IsFalse(modifierManager.HasModifier(creature, paralyzeMod.Id), "Paralyze modifier instance still found after expiry.");
    }

    // --- NEW TESTS ---

    [UnityTest]
    public IEnumerator ModifierManager_ApplyPercentageAttackModifier_CalculatesCorrectly() {
        // Arrange
        yield return SetupSceneAndWait();
        Creature creature = GetFirstAvailableCreature(gameManager.Player1);
        int initialAttack = creature.Attack; // Attack after initial setup/mods
        int percentageIncrease = 50; // +50%
        IModifier percentAttackMod = modifierManager._modifierFactory.CreateStatModifier(
            "Test Percent Attack Buff", "+50% Attack", ModifiableStat.Attack, ModifierCalculationType.Percentage, percentageIncrease
        );
        // Expected calculation based on RecalculateStats: (Base + Flat) * Multiplier
        // Since no flat mods applied here, it's Base * Multiplier
        // Note: creature.Attack already includes initial mods, so we use creature.BaseAttack for calculation
        int expectedAttack = (int)Math.Round(creature.BaseAttack * (1.0f + (percentageIncrease / 100.0f)));
        // If there might be *other* flat mods from initial setup, recalculate based on *current* attack before applying percentage:
        // expectedAttack = (int)Math.Round(initialAttack * (1.0f + (percentageIncrease / 100.0f)));
        // Let's stick to the Base * Multiplier since that's how RecalculateStats works without flat mods.

        // Act
        modifierManager.ApplyModifier(creature, percentAttackMod);
        yield return null;

        // Assert
        Assert.AreEqual(expectedAttack, creature.Attack, $"Creature percentage attack incorrect. Base: {creature.BaseAttack}, Expected: {expectedAttack}, Actual: {creature.Attack}");
        Assert.IsTrue(modifierManager.HasModifier(creature, percentAttackMod.Id), "Percentage attack modifier was not found in the active list.");
    }

    [UnityTest]
    public IEnumerator ModifierManager_ExplicitlyRemoveModifier_RevertsStat() {
        // Arrange
        yield return SetupSceneAndWait();
        Creature creature = GetFirstAvailableCreature(gameManager.Player1);
        int initialSpeed = creature.Speed;
        int modifierValue = -2; // Apply a debuff
        int duration = 5; // Long duration so it won't expire naturally during test
        int startTurn = turnManager.TurnNumber;
        IModifier timedSpeedMod = modifierManager._modifierFactory.CreateTimedStatModifier(
            "Test Slow Debuff", "-2 Speed (5 Turns)", ModifiableStat.Speed, ModifierCalculationType.Flat, modifierValue, duration, startTurn
        );

        // Act: Apply Modifier
        modifierManager.ApplyModifier(creature, timedSpeedMod);
        yield return null;

        // Assert: Applied Correctly
        int expectedSpeedAfterApply = Math.Max(0, initialSpeed + modifierValue); // Speed can't go below 0
        Assert.AreEqual(expectedSpeedAfterApply, creature.Speed, "Timed speed modifier did not apply correctly initially.");
        Assert.IsTrue(modifierManager.HasModifier(creature, timedSpeedMod.Id), "Timed speed modifier was not active after applying.");

        // Act: Explicitly Remove Modifier
        modifierManager.RemoveModifier(creature, timedSpeedMod);
        yield return null;

        // Assert: Removed Correctly
        Assert.IsFalse(modifierManager.HasModifier(creature, timedSpeedMod.Id), "Modifier was still active after explicit removal.");
        Assert.AreEqual(initialSpeed, creature.Speed, "Creature speed did not revert after explicit modifier removal.");
    }

    [UnityTest]
    public IEnumerator ModifierManager_StackFlatAttackModifiers_CombinesCorrectly() {
        // Arrange
        yield return SetupSceneAndWait();
        Creature creature = GetFirstAvailableCreature(gameManager.Player1);
        int initialAttack = creature.Attack; // Attack after initial setup/mods
        int mod1Value = 3;
        int mod2Value = 2;
        IModifier attackMod1 = modifierManager._modifierFactory.CreateStatModifier("Test Attack Buff 1", "+3 Attack", ModifiableStat.Attack, ModifierCalculationType.Flat, mod1Value);
        IModifier attackMod2 = modifierManager._modifierFactory.CreateStatModifier("Test Attack Buff 2", "+2 Attack", ModifiableStat.Attack, ModifierCalculationType.Flat, mod2Value);
        int expectedAttack = initialAttack + mod1Value + mod2Value;

        // Act: Apply both modifiers
        modifierManager.ApplyModifier(creature, attackMod1);
        modifierManager.ApplyModifier(creature, attackMod2);
        yield return null; // Allow recalculation

        // Assert
        List<IModifier> activeMods = modifierManager.GetActiveModifiersFor(creature).ToList();
        Assert.AreEqual(expectedAttack, creature.Attack, $"Stacked attack incorrect. Initial: {initialAttack}, Expected: {expectedAttack}, Actual: {creature.Attack}");
        Assert.IsTrue(activeMods.Any(m => m.Id == attackMod1.Id), "First attack modifier was not found in the active list.");
        Assert.IsTrue(activeMods.Any(m => m.Id == attackMod2.Id), "Second attack modifier was not found in the active list.");
        // Optionally check the count if you know exactly how many *other* modifiers might be present initially
        // Assert.GreaterOrEqual(activeMods.Count, 2, "Expected at least two active modifiers.");
    }

    // --- End NEW TESTS ---

}