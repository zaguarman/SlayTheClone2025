using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class PlayerUITests
{
    private const string TestSceneName = "Balatro-Feel";
    private const string BlankSceneName = "TestBlankScene"; // Name of your empty scene

    private GameManager gameManager;
    private GameMediator gameMediator;
    private GameReferences gameReferences;
    private GameUI gameUI;

    // SetupSceneAndWait remains the same as the previous version

    private IEnumerator SetupSceneAndWait()
    {
        // --- 1. Load the Scene ---
        Debug.Log($"[Test] Loading scene: {TestSceneName}...");
        // Ensure the previous scene is unloaded properly if coming from another test
        yield return SceneManager.LoadSceneAsync(TestSceneName, LoadSceneMode.Single);
        Debug.Log($"[Test] Scene {TestSceneName} loaded.");

        // --- 2. Find Core Components in the Loaded Scene ---
        yield return null; // Allow a frame for objects to potentially Awake/Start

        gameManager = Object.FindObjectOfType<GameManager>();
        gameMediator = Object.FindObjectOfType<GameMediator>();
        gameReferences = Object.FindObjectOfType<GameReferences>();
        gameUI = Object.FindObjectOfType<GameUI>();

        Assert.IsNotNull(gameManager, $"GameManager not found in scene '{TestSceneName}'.");
        Assert.IsNotNull(gameMediator, $"GameMediator not found in scene '{TestSceneName}'.");
        Assert.IsNotNull(gameReferences, $"GameReferences not found in scene '{TestSceneName}'.");
        Assert.IsNotNull(gameUI, $"GameUI not found in scene '{TestSceneName}'.");

        // --- 3. Wait for Initialization ---
        Debug.Log("[Test] Waiting for scene components to initialize...");
        // Add timeouts to prevent infinite waits if initialization fails
        float timeout = 10f; float time = 0f;
        yield return new WaitUntil(() => (gameReferences?.IsInitialized ?? false) || (time += Time.deltaTime) > timeout); Assert.IsTrue(time <= timeout, "Timeout waiting for GameReferences");
        Debug.Log("[Test] GameReferences Initialized."); time = 0f;
        yield return new WaitUntil(() => (gameMediator?.IsInitialized ?? false) || (time += Time.deltaTime) > timeout); Assert.IsTrue(time <= timeout, "Timeout waiting for GameMediator");
        Debug.Log("[Test] GameMediator Initialized."); time = 0f;
        yield return new WaitUntil(() => (gameManager?.IsInitialized ?? false) || (time += Time.deltaTime) > timeout); Assert.IsTrue(time <= timeout, "Timeout waiting for GameManager");
        Debug.Log("[Test] GameManager Initialized."); time = 0f;
        yield return new WaitUntil(() => (gameUI?.IsInitialized ?? false) || (time += Time.deltaTime) > timeout); Assert.IsTrue(time <= timeout, "Timeout waiting for GameUI");
        Debug.Log("[Test] GameUI Initialized.");

        yield return null;
        Debug.Log("[Test] Scene setup complete.");

        // --- 4. Final Checks ---
        Assert.IsTrue(gameReferences.AreReferencesValid(), "GameReferences in the scene has missing inspector assignments.");
        Assert.IsNotNull(gameManager.Player1, "GameManager did not initialize Player1 in the scene.");
        Assert.IsNotNull(gameReferences.player1References.playerUI, "Player 1 UI reference missing in GameReferences (Scene Inspector).");
        Assert.IsTrue(gameReferences.player1References.playerUI.IsInitialized, "Player 1 UI did not initialize within the scene.");
        Assert.IsNotNull(gameReferences.player1References.healthText, "Player 1 Health Text reference missing in GameReferences (Scene Inspector).");
    }
    
    // --- THE TESTS (Unchanged) ---

    [UnityTest]
    public IEnumerator PlayerUI_Initialization_UpdatesHealthText()
    {
        // Arrange
        yield return SetupSceneAndWait();

        // Act

        // Assert
        TextMeshProUGUI healthText = gameReferences.player1References.healthText;
        Assert.IsNotNull(healthText, "HealthText component not found in scene.");
        Assert.AreEqual($"Health: {gameManager.Player1.Health}", healthText.text, "Initial health text is incorrect based on scene setup.");
    }

    [UnityTest]
    public IEnumerator PlayerUI_TakeDamage_UpdatesHealthText()
    {
        // Arrange
        yield return SetupSceneAndWait();

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

        yield return null;

        // Assert
        Debug.Log($"[Test] Expected Health Text: 'Health: {expectedHealth}', Actual: '{healthText.text}'");
        Assert.AreEqual($"Health: {expectedHealth}", healthText.text, "Health text did not update correctly after taking damage.");
    }
}
