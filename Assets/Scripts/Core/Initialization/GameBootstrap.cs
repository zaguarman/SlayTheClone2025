using System.Collections;
using UnityEngine;
using static DebugLogger;

public class GameBootstrap : MonoBehaviour
{
    private bool hasInitialized = false; // Prevent multiple initializations

    private void Start()
    {
        if (!hasInitialized)
        {
            StartCoroutine(InitializeGame());
        }
    }

    private IEnumerator InitializeGame()
    {
        hasInitialized = true;
        Log("--- Starting Game Initialization Sequence ---", LogTag.Initialization);

        // --- Order is important ---

        // 1. Initialize References
        // Find GameReferences in the scene directly (no longer using Singleton pattern)
        var gameReferences = FindObjectOfType<GameReferences>();
        if (gameReferences == null)
        {
            LogError("GameReferences component not found in scene! Initialization cannot proceed.", LogTag.Initialization);
            yield break;
        }
        if (!gameReferences.IsInitialized)
        {
            gameReferences.Initialize(); // Initialize it if not already
            yield return new WaitUntil(() => gameReferences.IsInitialized);
        }
        Log("GameReferences initialized", LogTag.Initialization);
        if (!gameReferences.AreReferencesValid())
        {
             LogError("GameReferences are invalid after initialization! Aborting.", LogTag.Initialization);
             yield break;
        }

        // 2. Initialize Mediator
        // Assuming GameMediator is in the scene and uses Singleton pattern for access
        var gameMediator = GameMediator.Instance;
         if (gameMediator == null || !FindObjectOfType<GameMediator>()) {
             LogError("GameMediator instance not found in scene! Initialization cannot proceed.", LogTag.Initialization);
             yield break;
         }
        if (!gameMediator.IsInitialized)
        {
            gameMediator.Initialize();
            yield return new WaitUntil(() => gameMediator.IsInitialized);
        }
        Log("GameMediator initialized", LogTag.Initialization);

        // 3. Find and Initialize TurnManager (No longer using Singleton pattern)
        var turnManager = FindObjectOfType<TurnManager>();
        if (turnManager == null) {
            LogError("TurnManager component not found in scene! Initialization cannot proceed.", LogTag.Initialization);
            yield break;
        }
        // TurnManager will be initialized later with GameManager
        Log("TurnManager component found", LogTag.Initialization);


        // 4. Find and Initialize GameManager (Injecting Dependencies)
        var gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null)
        {
             LogError("GameManager component not found in scene! Initialization cannot proceed.", LogTag.Initialization);
             yield break;
        }
        if (!gameManager.IsInitialized)
        {
            IModifierFactory modFactory = new SimpleModifierFactory(); // Create factory instance
            // Call the Initialize method with dependencies
            gameManager.Initialize(gameMediator, gameReferences, turnManager, modFactory);
            yield return new WaitUntil(() => gameManager.IsInitialized);
        }
        Log("GameManager initialized", LogTag.Initialization);

        // Now initialize TurnManager with the initialized GameManager
        turnManager.Initialize(gameManager, gameMediator);
        Log("TurnManager initialized with dependencies", LogTag.Initialization);

        // 5. Initialize GameUI (Depends on GameManager being initialized)
        var gameUI = FindObjectOfType<GameUI>();
        if (gameUI == null) {
            LogError("GameUI component not found in scene! Initialization cannot proceed.", LogTag.Initialization);
            yield break;
        }
        if (!gameUI.IsInitialized)
        {
            // Pass the dependencies explicitly to GameUI
            gameUI.Initialize(gameMediator, gameReferences);
            yield return new WaitUntil(() => gameUI.IsInitialized);
        }
        Log("GameUI initialized", LogTag.Initialization);

        Log("--- Game Initialization Sequence Complete ---", LogTag.Initialization);
    }
}