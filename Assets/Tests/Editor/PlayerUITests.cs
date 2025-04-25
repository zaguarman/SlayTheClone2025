using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement; // Use Editor Scene Manager

[TestFixture]
public class PlayerUITests_Editor
{
    private const string TestSceneName = "Balatro-Feel"; // Use the actual scene path
    private Scene _testScene;

    [OneTimeSetUp]
    public void LoadScene()
    {
        // Load the scene additively in the editor to inspect it
        _testScene = EditorSceneManager.OpenScene("Assets/Scenes/" + TestSceneName + ".unity", OpenSceneMode.Additive);
        Assert.IsTrue(_testScene.IsValid(), $"Failed to load scene: {TestSceneName}");
    }

    [OneTimeTearDown]
    public void UnloadScene()
    {
        if (_testScene.IsValid())
        {
            EditorSceneManager.CloseScene(_testScene, true);
        }
    }

    [Test]
    public void PlayerUI_Initialization_ReferencesCorrectHealthText()
    {
        // Arrange: Find components within the loaded scene
        // FindObjectOfType works in editor tests on loaded scenes
        GameManager gameManager = Object.FindObjectOfType<GameManager>();
        GameReferences gameReferences = Object.FindObjectOfType<GameReferences>();

        Assert.IsNotNull(gameManager, "GameManager not found in the scene.");
        Assert.IsNotNull(gameReferences, "GameReferences not found in the scene.");

        // We can't fully *run* initialization in editor tests easily,
        // but we can check if the references *are assigned* in the inspector.
        PlayerUI player1UI = gameReferences.GetPlayer1UI();
        TextMeshProUGUI healthTextRef = gameReferences.player1References.healthText; // Get the assigned ref

        // Assert
        Assert.IsNotNull(player1UI, "Player1UI reference missing in GameReferences.");
        Assert.IsNotNull(healthTextRef, "Player1 HealthText reference missing in GameReferences.");

        // Optional: Check if the HealthText is actually a child of the PlayerUI object
        bool isChild = false;
        if (player1UI != null && healthTextRef != null)
        {
            isChild = healthTextRef.transform.IsChildOf(player1UI.transform);
        }
         Assert.IsTrue(isChild, "Player1 HealthText is not a child of Player1UI GameObject in the scene hierarchy.");

        // Note: We cannot easily test the *result* of Player.SetHealthText in editor mode
        // without running the game's initialization logic. This test verifies the scene setup.
    }
}
