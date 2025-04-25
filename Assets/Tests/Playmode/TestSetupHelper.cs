using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Linq;
using UnityEngine.SceneManagement;

public static class TestSetupHelper
{
    public const string TestSceneName = "Balatro-Feel"; // Ensure this matches your scene name

    public static IEnumerator SetupSceneAndWait(System.Action<GameManager, GameMediator, GameReferences, TurnManager, ModifierManager> onSetupComplete)
    {
        // --- 1. Load the Scene ---
        Debug.Log($"[TestSetup] Loading scene: {TestSceneName}...");
        var loadOperation = SceneManager.LoadSceneAsync(TestSceneName, LoadSceneMode.Single);
        float loadTimeout = 15f; float loadTime = 0f;
        while (!loadOperation.isDone && loadTime < loadTimeout)
        {
            loadTime += Time.unscaledDeltaTime; // Use unscaled time for loading
            yield return null;
        }
        Assert.IsTrue(loadOperation.isDone, $"Timeout loading scene '{TestSceneName}'.");
        Debug.Log($"[TestSetup] Scene {TestSceneName} loaded.");

        // --- 2. Find Core Components ---
        yield return null; // Allow a frame for Awake

        GameManager gameManager = Object.FindObjectOfType<GameManager>();
        GameMediator gameMediator = Object.FindObjectOfType<GameMediator>();
        GameReferences gameReferences = Object.FindObjectOfType<GameReferences>();
        GameUI gameUI = Object.FindObjectOfType<GameUI>(); // Still useful to wait for
        TurnManager turnManager = Object.FindObjectOfType<TurnManager>();

        Assert.IsNotNull(gameManager, $"GameManager not found in scene '{TestSceneName}'.");
        Assert.IsNotNull(gameMediator, $"GameMediator not found in scene '{TestSceneName}'.");
        Assert.IsNotNull(gameReferences, $"GameReferences not found in scene '{TestSceneName}'.");
        Assert.IsNotNull(gameUI, $"GameUI not found in scene '{TestSceneName}'.");
        Assert.IsNotNull(turnManager, $"TurnManager not found in scene '{TestSceneName}'.");

        // --- 3. Wait for Initialization ---
        Debug.Log("[TestSetup] Waiting for scene components to initialize...");
        float initTimeout = 10f; float initTime = 0f;

        yield return new WaitUntil(() => gameReferences.IsInitialized || (initTime += Time.deltaTime) > initTimeout);
        Assert.IsTrue(gameReferences.IsInitialized, "Timeout waiting for GameReferences");
        Debug.Log($"[TestSetup] GameReferences Initialized (Refs Valid: {gameReferences.AreReferencesValid()}).");
        Assert.IsTrue(gameReferences.AreReferencesValid(), "GameReferences invalid after initialization.");
        initTime = 0f; // Reset timer for next component

        yield return new WaitUntil(() => gameMediator.IsInitialized || (initTime += Time.deltaTime) > initTimeout);
        Assert.IsTrue(gameMediator.IsInitialized, "Timeout waiting for GameMediator");
        Debug.Log("[TestSetup] GameMediator Initialized.");
        initTime = 0f;

        // GameManager initialization depends on others, so it might take slightly longer
        yield return new WaitUntil(() => gameManager.IsInitialized || (initTime += Time.deltaTime) > initTimeout + 2f); // Slightly longer timeout
        Assert.IsTrue(gameManager.IsInitialized, "Timeout waiting for GameManager");
        Debug.Log("[TestSetup] GameManager Initialized.");
        initTime = 0f;

        // TurnManager is initialized by GameManager/Bootstrap
        yield return new WaitUntil(() => turnManager.IsInitialized || (initTime += Time.deltaTime) > initTimeout);
        Assert.IsTrue(turnManager.IsInitialized, "Timeout waiting for TurnManager");
        Debug.Log("[TestSetup] TurnManager Initialized.");
        initTime = 0f;

        // GameUI depends on GameManager
        yield return new WaitUntil(() => gameUI.IsInitialized || (initTime += Time.deltaTime) > initTimeout);
        Assert.IsTrue(gameUI.IsInitialized, "Timeout waiting for GameUI");
        Debug.Log("[TestSetup] GameUI Initialized.");

        // --- 4. Obtain ModifierManager ---
        ModifierManager modifierManager = gameManager.ModifierManager as ModifierManager;
        Assert.IsNotNull(modifierManager, "ModifierManager is null after GameManager initialized.");
        Debug.Log("[TestSetup] ModifierManager reference obtained.");

        // --- 5. Wait for Creature Placement & Action Resolution ---
        // GameManager.PlaceInitialCreatures is called by GameUI.Initialize
        // Wait a few frames for potential Summon actions to resolve
        Debug.Log("[TestSetup] Waiting for initial creature placement/actions...");
        yield return new WaitForSeconds(0.2f); // Adjust delay if needed

        // --- 6. Final Validation ---
        Assert.IsNotNull(gameManager.Player1, "GameManager did not initialize Player1.");
        Assert.IsNotNull(gameManager.Player1.Battlefield, "Player 1 Battlefield is null.");
        // Ensure *some* creature exists after setup for tests that need one
        bool creaturePlaced = gameManager.Player1.Battlefield.Any(slot => slot.IsOccupied());
        if (!creaturePlaced) {
             creaturePlaced = gameManager.Player2.Battlefield.Any(slot => slot.IsOccupied());
        }
        Assert.IsTrue(creaturePlaced, "No creatures found on either battlefield after setup. Check GameManager.PlaceInitialCreatures and scene decks.");

        Debug.Log("[TestSetup] Scene setup complete.");

        // --- 7. Callback ---
        onSetupComplete?.Invoke(gameManager, gameMediator, gameReferences, turnManager, modifierManager);
    }

    // Helper to get the first available creature for testing
    public static Creature GetFirstAvailableCreature(IPlayer player, ModifierManager modifierManager)
    {
        Assert.IsNotNull(player, "Player is null when trying to find a creature.");
        Assert.IsNotNull(player.Battlefield, "Player's battlefield is null.");
        Assert.IsNotNull(modifierManager, "ModifierManager is null when trying to get creature.");

        var occupiedSlot = player.Battlefield.FirstOrDefault(slot => slot.IsOccupied() && slot.OccupyingCreature != null);
        Assert.IsNotNull(occupiedSlot, $"No occupied slot with a creature found for player {(player.IsPlayer1 ? "1" : "2")}.");

        var creature = occupiedSlot.OccupyingCreature as Creature;
        Assert.IsNotNull(creature, "Occupying entity is not a concrete Creature.");

        // Ensure stats are calculated before returning (important!)
        modifierManager.RecalculateStats(creature);
        Debug.Log($"[TestHelper] Found creature: {creature.Name} (Atk: {creature.Attack}, HP: {creature.Health}/{creature.MaxHealth}, Spd: {creature.Speed})");

        return creature;
    }
}
