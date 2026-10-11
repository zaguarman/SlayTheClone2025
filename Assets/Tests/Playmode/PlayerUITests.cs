using NUnit.Framework;
using System.Collections;
using TMPro;
using UnityEngine.TestTools;

namespace SlayTheClone.Tests {
    public class PlayerUITests {
        private GameManager gameManager;
        private GameReferences gameReferences;

        [UnitySetUp]
        public IEnumerator Setup() {
            yield return TestSetupHelper.SetupSceneAndWait((gm, med, refs, tm, mm) => {
                gameManager = gm;
                gameReferences = refs;
            });
        }

        [TearDown]
        public void Teardown() {
            gameManager = null;
            gameReferences = null;
        }

        [UnityTest]
        public IEnumerator PlayerUI_Initialization_UpdatesHealthText() {
            // Arrange - setup already done in Setup()

            // Act - initialization already happened in scene setup

            // Assert
            TextMeshProUGUI healthText = gameReferences.Player1References.HealthText;
            Assert.IsNotNull(healthText, "HealthText component not found in scene.");
            Assert.AreEqual($"Health: {gameManager.Player1.Health}", healthText.text,
                "Initial health text is incorrect based on scene setup.");

            yield return null;
        }
    }
}
