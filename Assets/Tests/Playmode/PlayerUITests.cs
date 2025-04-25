using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using TMPro;

public class PlayerUITests
{
    private GameManager _gameManager;
    private GameReferences _gameReferences;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        yield return TestSetupHelper.SetupSceneAndWait((gm, med, refs, tm, mm) =>
        {
            _gameManager = gm;
            _gameReferences = refs;
        });
    }

    [TearDown]
    public void Teardown()
    {
        _gameManager = null;
        _gameReferences = null;
    }

    [UnityTest]
    public IEnumerator PlayerUI_Initialization_UpdatesHealthText()
    {
        // Arrange - setup already done in Setup()

        // Act - initialization already happened in scene setup

        // Assert
        TextMeshProUGUI healthText = _gameReferences.player1References.healthText;
        Assert.IsNotNull(healthText, "HealthText component not found in scene.");
        Assert.AreEqual($"Health: {_gameManager.Player1.Health}", healthText.text,
            "Initial health text is incorrect based on scene setup.");

        yield return null;
    }
}