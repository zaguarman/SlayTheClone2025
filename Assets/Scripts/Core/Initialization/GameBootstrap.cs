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
        // Assuming GameReferences is in the scene and uses Singleton pattern for access
        var gameReferences = GameReferences.Instance;
        if (gameReferences == null || !FindObjectOfType<GameReferences>())
        {
            LogError("GameReferences instance not found in scene! Initialization cannot proceed.", LogTag.Initialization);
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

        // 3. Initialize TurnManager (Assuming Singleton for now)
        var turnManager = TurnManager.Instance;
        if (turnManager == null || !FindObjectOfType<TurnManager>()) {
             LogError("TurnManager instance not found in scene! Initialization cannot proceed.", LogTag.Initialization);
             yield break;
         }
        // TurnManager likely doesn't have an explicit Initialize method in this structure
        Log("TurnManager instance accessed", LogTag.Initialization);


        // 4. Find and Initialize GameManager (Injecting Dependencies)
        var gameManager = GameManager.Instance; // Use instance property to find it
        if (gameManager == null || !FindObjectOfType<GameManager>())
        {
             LogError("GameManager instance not found in scene! Initialization cannot proceed.", LogTag.Initialization);
             yield break;
        }
        if (!gameManager.IsInitialized)
        {
            IModifierFactory modFactory = new SimpleModifierFactory(); // Create factory instance
            // Call the NEW Initialize method with dependencies
            gameManager.Initialize(gameMediator, gameReferences, turnManager, modFactory);
            yield return new WaitUntil(() => gameManager.IsInitialized);
        }
        Log("GameManager initialized", LogTag.Initialization);

        // 5. Initialize GameUI (Depends on GameManager being initialized)
        var gameUI = GameUI.Instance;
         if (gameUI == null || !FindObjectOfType<GameUI>()) {
             LogError("GameUI instance not found in scene! Initialization cannot proceed.", LogTag.Initialization);
             yield break;
         }
        if (!gameUI.IsInitialized)
        {
            // GameUI's Initialize now implicitly gets dependencies from initialized Singletons
            gameUI.Initialize(); // GameUI's Initialize handles getting its dependencies
            yield return new WaitUntil(() => gameUI.IsInitialized);
        }
        Log("GameUI initialized", LogTag.Initialization);

        Log("--- Game Initialization Sequence Complete ---", LogTag.Initialization);
    }
}