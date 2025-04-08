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
        yield return new WaitUntil(() => GameReferences.Instance.IsInitialized);
        Log("GameReferences initialized", LogTag.Initialization);

        // Initialize mediator after references
        initManager.RegisterComponent(GameMediator.Instance);
        initManager.InitializeComponents();
        yield return new WaitUntil(() => GameMediator.Instance.IsInitialized);
        Log("GameMediator initialized", LogTag.Initialization);

        // Initialize GameManager next
        initManager.RegisterComponent(GameManager.Instance);
        initManager.InitializeComponents();
        yield return new WaitUntil(() => GameManager.Instance.IsInitialized);
        Log("GameManager initialized", LogTag.Initialization);

        // Initialize UI last since it depends on GameManager
        initManager.RegisterComponent(GameUI.Instance);
        initManager.InitializeComponents();
        yield return new WaitUntil(() => GameUI.Instance.IsInitialized);
        Log("GameUI initialized", LogTag.Initialization);

        Log("All components initialized successfully", LogTag.Initialization);
    }
}