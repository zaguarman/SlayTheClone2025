using System.Collections;
using UnityEngine;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    // Interface remains the same
    // public interface ITurnManager { ... }

    // TurnManager depends on GameManager for access to other systems
    public class TurnManager : MonoBehaviour, ITurnManager {
        // Singleton pattern removed
        // Implement IsInitialized property
        public bool IsInitialized { get; private set; }

        #region Fields & Properties
        private int turnNumber = 0;
        private IGameManager gameManager; // Store as interface
        private IGameMediator gameMediator; // Store as interface
        private IModifierManager modifierManager; // Store as interface
        private IActionsQueue actionsQueue; // Store as interface
        private IResolutionPresenter presenter; // Optional: plays each resolved action before the next one runs

        public int TurnNumber => turnNumber;
        public bool IsResolving { get; private set; }
        #endregion

        #region Unity Lifecycle

        // No Start() method - Initialization driven by GameBootstrap calling Initialize
        public void Initialize(IGameManager gameManager, IGameMediator mediator) {
            if (gameManager == null || mediator == null) {
                LogError("TurnManager initialization failed: GameManager or GameMediator is null.", LogTag.Initialization | LogTag.Turns);
                enabled = false;
                return;
            }
            this.gameManager = gameManager;
            gameMediator = mediator;
            // Get other systems from GameManager *after* it's initialized
            modifierManager = gameManager.ModifierManager;
            actionsQueue = gameManager.ActionsQueue;

            if (modifierManager == null || actionsQueue == null) {
                LogError("TurnManager initialization failed: ModifierManager or ActionsQueue not ready via GameManager.", LogTag.Initialization | LogTag.Turns);
                enabled = false;
                return;
            }

            turnNumber = 0; // Reset turn number on initialization
            IsInitialized = true; // Mark as initialized
            Log("TurnManager Initialized with dependencies.", LogTag.Initialization | LogTag.Turns);
        }
        #endregion

        #region Methods
        public void SetPresenter(IResolutionPresenter presenter) {
            this.presenter = presenter;
        }

        /// <summary>
        /// Ends the turn. Resolution runs over several frames, one action at a time, so the presenter
        /// can show what each action did; IsResolving stays true until it finishes.
        /// </summary>
        public void EndTurn() {
            if (IsResolving) return;
            if (gameManager == null || gameMediator == null || modifierManager == null || actionsQueue == null) {
                LogError("TurnManager.EndTurn called before fully initialized or dependencies lost!", LogTag.Turns | LogTag.Initialization);
                return;
            }
            StartCoroutine(EndTurnRoutine());
        }

        private IEnumerator EndTurnRoutine() {
            IsResolving = true;
            try {
                int endedTurn = turnNumber; // Store the turn number that just ended
                turnNumber++; // Increment turn number for the new turn
                Log($"--- Ending Turn {endedTurn}, Starting Turn {turnNumber} ---", LogTag.Turns);

                // --- Phase 1: End of Turn Processing (for the turn that just finished) ---
                modifierManager.ProcessEndOfTurn(endedTurn, actionsQueue);
                yield return ResolveQueuedActions();

                // --- Phase 2: Turn Transition & Mandatory Actions ---
                foreach (var player in new[] { gameManager.Player1, gameManager.Player2 }) {
                    gameManager.DiscardHand(player);
                }
                foreach (var player in new[] { gameManager.Player1, gameManager.Player2 }) {
                    gameManager.DrawCardsForPlayer(player, player.CardsToDraw);
                }
                yield return ResolveQueuedActions();

                // Notify AFTER mandatory actions, BEFORE Start-of-Turn effects
                gameMediator.NotifyTurnEnded(endedTurn); // Notify that the PREVIOUS turn number has ENDED

                // --- Phase 3: Start of Turn Processing (for the new turn) ---
                modifierManager.ProcessStartOfTurn(turnNumber, actionsQueue);
                yield return ResolveQueuedActions();

                // --- Phase 4: Finalize ---
                gameMediator.NotifyGameStateChanged();
                Log($"--- Turn {turnNumber} Started ---", LogTag.Turns);
            }
            finally {
                IsResolving = false;
            }
        }

        private IEnumerator ResolveQueuedActions() {
            yield return actionsQueue.ResolveActionsStepByStep(WaitForPresentation);
        }

        private IEnumerator WaitForPresentation() {
            if (presenter != null) {
                yield return presenter.WaitUntilIdle();
            }
        }
        #endregion
    }
}
