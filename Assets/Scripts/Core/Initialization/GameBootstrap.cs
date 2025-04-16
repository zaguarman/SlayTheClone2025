using System.Collections;
using UnityEngine;
using static DebugLogger;

public class GameBootstrap : MonoBehaviour {
    private void Start() {
        StartCoroutine(InitializeGame());
    }

    private IEnumerator InitializeGame() {
        var initManager = InitializationManager.Instance;

        // Initialize references first
        initManager.RegisterComponent(GameReferences.Instance);
        initManager.InitializeComponents();
        yield return new WaitUntil(() => GameReferences.Instance != null && GameReferences.Instance.IsInitialized);
        Log("GameReferences initialized", LogTag.Initialization);

        // Initialize mediator after references
        initManager.RegisterComponent(GameMediator.Instance);
        initManager.InitializeComponents();
        yield return new WaitUntil(() => GameMediator.Instance != null && GameMediator.Instance.IsInitialized);
        Log("GameMediator initialized", LogTag.Initialization);

        // Initialize GameManager core next
        initManager.RegisterComponent(GameManager.Instance);
        initManager.InitializeComponents();
        yield return new WaitUntil(() => GameManager.Instance != null && GameManager.Instance.IsInitialized);
        Log("GameManager core initialized", LogTag.Initialization);

        // Initialize UI last since it depends on GameManager and Populates Player Battlefields
        initManager.RegisterComponent(GameUI.Instance);
        initManager.InitializeComponents();
        yield return new WaitUntil(() => GameUI.Instance != null && GameUI.Instance.IsInitialized);
        Log("GameUI initialized", LogTag.Initialization);

        // --- Call GameManager's final setup steps AFTER UI is ready ---
        if (GameManager.Instance != null) {
            GameManager.Instance.CompleteGameSetup();
            Log("GameManager final setup completed", LogTag.Initialization);
        } else {
             LogError("GameManager instance is null after UI initialization!", LogTag.Initialization);
        }
        // --- End final setup call ---

        Log("All components initialized successfully", LogTag.Initialization);
    }
}