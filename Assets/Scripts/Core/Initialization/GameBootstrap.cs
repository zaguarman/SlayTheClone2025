using System.Collections;
using UnityEngine;
using static DebugLogger;

public class GameBootstrap : MonoBehaviour {
    private void Start() {
        StartCoroutine(InitializeGame());
    }

    private IEnumerator InitializeGame() {
        var initManager = InitializationManager.Instance;

        // --- Step 1: Register ALL components ---
        Log("Registering components...", LogTag.Initialization);
        // Ensure singletons are found or created before registering
        if (GameReferences.Instance == null) yield return null; // Wait a frame if needed
        initManager.RegisterComponent(GameReferences.Instance);

        if (GameMediator.Instance == null) yield return null;
        initManager.RegisterComponent(GameMediator.Instance);

        if (GameManager.Instance == null) yield return null;
        initManager.RegisterComponent(GameManager.Instance);

        if (GameUI.Instance == null) yield return null;
        initManager.RegisterComponent(GameUI.Instance);
        Log("All components registered.", LogTag.Initialization);

        // --- Step 2: Initialize all registered components IN ORDER ---
        Log("Initializing components...", LogTag.Initialization);
        initManager.InitializeComponents(); // Call InitializeComponents ONCE

        // --- Step 3: Wait for CRITICAL components to be fully initialized ---
        // We need GameManager and GameUI initialized, AND Player Battlefields populated
        Log("Waiting for GameManager, GameUI, and Player Battlefields initialization...", LogTag.Initialization);
        yield return new WaitUntil(() =>
            initManager.IsComponentInitialized<GameManager>() &&
            initManager.IsComponentInitialized<GameUI>() &&
            GameManager.Instance != null && // Ensure GM exists before accessing players
            GameManager.Instance.Player1 != null && GameManager.Instance.Player1.IsBattlefieldInitialized &&
            GameManager.Instance.Player2 != null && GameManager.Instance.Player2.IsBattlefieldInitialized
        );
        Log("GameManager, GameUI, and Player Battlefields initialized.", LogTag.Initialization);

        // --- ADD EXTRA LOGGING ---
        bool gmInstanceExists = GameManager.Instance != null;
        // *** Check the actual IsInitialized flag now ***
        bool gmIsInitialized = gmInstanceExists && GameManager.Instance.IsInitialized;
        bool player1Exists = gmInstanceExists && GameManager.Instance.Player1 != null;
        bool player2Exists = gmInstanceExists && GameManager.Instance.Player2 != null;
        bool p1BattlefieldInit = player1Exists && GameManager.Instance.Player1.IsBattlefieldInitialized;
        bool p2BattlefieldInit = player2Exists && GameManager.Instance.Player2.IsBattlefieldInitialized;
        Log($"Pre-CompleteGameSetup state: GM Exists={gmInstanceExists}, GM Initialized={gmIsInitialized}, P1={player1Exists}, P2={player2Exists}, P1BF={p1BattlefieldInit}, P2BF={p2BattlefieldInit}", LogTag.Initialization);

        // --- Step 4: Complete GameManager setup (deals cards, places creatures) ---
        // This relies on Players being initialized (in GM init) and UI being ready (Battlefields populated in GameUI init)
        if (GameManager.Instance != null && GameManager.Instance.IsInitialized) {
            Log("Calling GameManager.CompleteGameSetup...", LogTag.Initialization);
            GameManager.Instance.CompleteGameSetup();
            Log("GameManager final setup completed.", LogTag.Initialization);
        } else {
             // This error should no longer occur with the fix
             LogError($"GameManager instance is null or not initialized ({GameManager.Instance?.IsInitialized}) before CompleteGameSetup!", LogTag.Initialization);
        }

        Log("Game Bootstrap sequence finished.", LogTag.Initialization);
    }
}