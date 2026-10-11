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
            var gameMediator = new GameMediator();

            // 3. GameManager builds the game and starts the turn flow
            if (!TryFind(out TurnManager turnManager)) yield break;
            if (!TryFind(out GameManager gameManager)) yield break;
            if (!gameManager.IsInitialized) {
                gameManager.Initialize(gameMediator, gameReferences.GetPlayer1DeckCards(), gameReferences.GetPlayer2DeckCards(),
                    turnManager, new SimpleModifierFactory(), gameReferences.Player2Name);
                yield return new WaitUntil(() => gameManager.IsInitialized);
            }
            Log("GameManager initialized", LogTag.Initialization);

            // 4. GameUI, with what every card face needs and the views of both sides of the table
            var cardViews = new CardViews(gameReferences.GetCardPrefab().GetComponent<CardController>(),
                gameReferences.Player1CardColor, gameReferences.Player2CardColor, gameMediator, gameManager);
            var tableViews = new TableViews(gameReferences.Player1References, gameReferences.Player2References);
            if (!TryFind(out GameUI gameUI)) yield break;
            if (!gameUI.IsInitialized) {
                gameUI.Initialize(gameMediator, gameReferences, gameManager, cardViews, tableViews);
                yield return new WaitUntil(() => gameUI.IsInitialized);
            }
            Log("GameUI initialized", LogTag.Initialization);

            // 5. Resolution playback: the turn waits on it so effects play one after another
            var presenter = ResolutionPresenter.Create(gameReferences.transform);
            presenter.Initialize(gameMediator, gameManager, cardViews, tableViews);
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
