using System.Collections;
using UnityEngine;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class GameBootstrap : MonoBehaviour {
        private bool hasInitialized = false;

        private void Start() {
            if (!hasInitialized) {
                StartCoroutine(InitializeGame());
            }
        }

        // Order matters: each system is initialized with the ones before it
        private IEnumerator InitializeGame() {
            hasInitialized = true;
            Log("--- Starting Game Initialization Sequence ---", LogTag.Initialization);

            // 1. References
            if (!TryFind(out GameReferences gameReferences)) yield break;
            if (!gameReferences.IsInitialized) {
                gameReferences.Initialize();
                yield return new WaitUntil(() => gameReferences.IsInitialized);
            }
            Log("GameReferences initialized", LogTag.Initialization);
            if (!gameReferences.AreReferencesValid()) {
                LogError("GameReferences are invalid after initialization! Aborting.", LogTag.Initialization);
                yield break;
            }

            // 2. Mediator
            if (!TryFind(out GameMediator gameMediator)) yield break;
            if (!gameMediator.IsInitialized) {
                gameMediator.Initialize();
                yield return new WaitUntil(() => gameMediator.IsInitialized);
            }
            Log("GameMediator initialized", LogTag.Initialization);

            // 3. TurnManager: initialized after GameManager, which it depends on
            if (!TryFind(out TurnManager turnManager)) yield break;
            Log("TurnManager component found", LogTag.Initialization);

            // 4. GameManager
            if (!TryFind(out GameManager gameManager)) yield break;
            if (!gameManager.IsInitialized) {
                gameManager.Initialize(gameMediator, gameReferences, turnManager, new SimpleModifierFactory());
                yield return new WaitUntil(() => gameManager.IsInitialized);
            }
            Log("GameManager initialized", LogTag.Initialization);

            turnManager.Initialize(gameManager, gameMediator);
            Log("TurnManager initialized with dependencies", LogTag.Initialization);

            // 5. GameUI
            if (!TryFind(out GameUI gameUI)) yield break;
            if (!gameUI.IsInitialized) {
                gameUI.Initialize(gameMediator, gameReferences, gameManager);
                yield return new WaitUntil(() => gameUI.IsInitialized);
            }
            Log("GameUI initialized", LogTag.Initialization);

            // 6. Resolution playback: the turn waits on it so effects play one after another
            var presenter = ResolutionPresenter.Create(gameReferences.transform);
            presenter.Initialize(gameMediator, gameReferences, gameManager);
            turnManager.SetPresenter(presenter);
            Log("ResolutionPresenter initialized", LogTag.Initialization);

            Log("--- Game Initialization Sequence Complete ---", LogTag.Initialization);
        }

        private static bool TryFind<T>(out T component) where T : Object {
            component = FindObjectOfType<T>();
            if (component == null) {
                LogError($"{typeof(T).Name} component not found in scene! Initialization cannot proceed.", LogTag.Initialization);
                return false;
            }
            return true;
        }
    }
}
