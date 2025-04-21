using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using TMPro; // Required for TextMeshProUGUI
using UnityEngine.SceneManagement; // Required for scene loading

public class PlayerUITests {
    private const string TestSceneName = "Balatro-Feel"; // Your scene name

    private GameManager gameManager;
    private GameMediator gameMediator;
    private GameReferences gameReferences;
    private GameUI gameUI; // We need GameUI to confirm its initialization

    // --- Test Setup Helper ---
    // Loads the specified scene and waits for core systems to initialize within that scene.
    private IEnumerator SetupSceneAndWait() {
        // --- 1. Load the Scene ---
        Debug.Log($"[Test] Loading scene: {TestSceneName}...");
        var loadOperation = SceneManager.LoadSceneAsync(TestSceneName, LoadSceneMode.Single);
        // Wait until the scene is fully loaded
        while (!loadOperation.isDone) {
            yield return null;
        }
        Debug.Log($"[Test] Scene {TestSceneName} loaded.");

        // --- 2. Find Core Components in the Loaded Scene ---
        // Allow some frames for Awake/Start methods in the scene to run
        yield return null;
        yield return null;

        gameManager = Object.FindObjectOfType<GameManager>();
        gameMediator = Object.FindObjectOfType<GameMediator>();
        gameReferences = Object.FindObjectOfType<GameReferences>();
        gameUI = Object.FindObjectOfType<GameUI>(); // Find GameUI as well

        Assert.IsNotNull(gameManager, $"GameManager not found in scene '{TestSceneName}'.");
        Assert.IsNotNull(gameMediator, $"GameMediator not found in scene '{TestSceneName}'.");
        Assert.IsNotNull(gameReferences, $"GameReferences not found in scene '{TestSceneName}'.");
        Assert.IsNotNull(gameUI, $"GameUI not found in scene '{TestSceneName}'.");

        // --- 3. Wait for Initialization to Complete (driven by the scene's setup) ---
        Debug.Log("[Test] Waiting for scene components to initialize...");
        yield return new WaitUntil(() => gameReferences != null && gameReferences.IsInitialized);
        Debug.Log("[Test] GameReferences Initialized.");
        yield return new WaitUntil(() => gameMediator != null && gameMediator.IsInitialized);
        Debug.Log("[Test] GameMediator Initialized.");
        yield return new WaitUntil(() => gameManager != null && gameManager.IsInitialized);
        Debug.Log("[Test] GameManager Initialized.");
        yield return new WaitUntil(() => gameUI != null && gameUI.IsInitialized); // Wait for GameUI too
        Debug.Log("[Test] GameUI Initialized.");

        // Allow another frame just in case
        yield return null;

        Debug.Log("[Test] Scene setup complete.");

        // --- 4. Final Checks (Crucial for Scene-Based Tests) ---
        Assert.IsTrue(gameReferences.AreReferencesValid(), "GameReferences in the scene has missing inspector assignments.");
        Assert.IsNotNull(gameManager.Player1, "GameManager did not initialize Player1 in the scene.");
        Assert.IsNotNull(gameReferences.player1References.playerUI, "Player 1 UI reference missing in GameReferences (Scene Inspector).");
        Assert.IsTrue(gameReferences.player1References.playerUI.IsInitialized, "Player 1 UI did not initialize within the scene.");
        Assert.IsNotNull(gameReferences.player1References.healthText, "Player 1 Health Text reference missing in GameReferences (Scene Inspector).");
    }

    // --- Test Teardown Helper ---
    // Optional: Load an empty scene afterwards if tests interfere,
    // but often the test runner handles scene unloading.
    [TearDown]
    public void Teardown() {
        // Resetting static singletons might be necessary if tests affect each other.
        // For scene-based tests, teardown is often less critical as the next test reloads.
        // If issues arise, consider loading a blank scene here.
        // SceneManager.LoadScene("EmptyTestScene");
        gameManager = null;
        gameMediator = null;
        gameReferences = null;
        gameUI = null;
        Debug.Log("[Test] Teardown complete.");
    }


    // --- THE TESTS ( Largely unchanged, but rely on scene setup ) ---

    [UnityTest]
    public IEnumerator PlayerUI_Initialization_UpdatesHealthText() {
        // Arrange
        yield return SetupSceneAndWait(); // Use the scene loading setup

        // Act
        // Initialization is handled by the scene's startup logic and verified in SetupSceneAndWait

        // Assert
        TextMeshProUGUI healthText = gameReferences.player1References.healthText;
        Assert.IsNotNull(healthText, "HealthText component not found in scene.");

        // Check if the initial health text is correct (value comes from the scene's initialized player)
        Assert.AreEqual($"Health: {gameManager.Player1.Health}", healthText.text, "Initial health text is incorrect based on scene setup.");
    }

    [UnityTest]
    public IEnumerator PlayerUI_TakeDamage_UpdatesHealthText() {
        // Arrange
        yield return SetupSceneAndWait(); // Use the scene loading setup

        // Get references from the loaded scene's managers/components
        IPlayer player1 = gameManager.Player1;
        TextMeshProUGUI healthText = gameReferences.player1References.healthText;
        int initialHealth = player1.Health;
        int damageAmount = 5;
        int expectedHealth = initialHealth - damageAmount;

        Assert.IsNotNull(player1, "Player1 is null after scene setup.");
        Assert.IsNotNull(healthText, "HealthText is null after scene setup.");

        // Act
        Debug.Log($"[Test] Applying {damageAmount} damage to Player 1 (Initial Health: {initialHealth})...");
        player1.TakeDamage(damageAmount);
        Debug.Log($"[Test] Player 1 Health after TakeDamage call: {player1.Health}");

        // Wait for UI update cycle (Events should propagate within the scene)
        yield return null; // Wait one frame for events and UI updates

        // Assert
        Debug.Log($"[Test] Expected Health Text: 'Health: {expectedHealth}', Actual: '{healthText.text}'");
        Assert.AreEqual($"Health: {expectedHealth}", healthText.text, "Health text did not update correctly after taking damage.");
    }
}