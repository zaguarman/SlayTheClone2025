using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// The turn flow: ending a turn resolves end-of-turn effects, discards and redraws hands, then runs
    /// start-of-turn effects, playing each action back through the presenter. Built by GameManager with
    /// exactly the systems it drives.
    /// </summary>
    public class TurnManager : MonoBehaviour, ITurnManager {
        public bool IsInitialized { get; private set; }

        #region Fields & Properties
        private IReadOnlyList<IPlayer> players;
        private IGameMediator gameMediator;
        private IModifierManager modifierManager;
        private IActionsQueue actionsQueue;
        private TurnClock clock;
        private IResolutionPresenter presenter; // Optional: plays each resolved action before the next one runs

        public int TurnNumber => clock?.TurnNumber ?? 0;
        public bool IsResolving { get; private set; }
        #endregion

        public void Initialize(IReadOnlyList<IPlayer> players, IActionsQueue actionsQueue, IModifierManager modifierManager,
            IGameMediator mediator, TurnClock clock) {
            this.players = players ?? throw new ArgumentNullException(nameof(players));
            this.actionsQueue = actionsQueue ?? throw new ArgumentNullException(nameof(actionsQueue));
            this.modifierManager = modifierManager ?? throw new ArgumentNullException(nameof(modifierManager));
            gameMediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            IsInitialized = true;
            Log("TurnManager Initialized with dependencies.", LogTag.Initialization | LogTag.Turns);
        }

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
            if (!IsInitialized) {
                LogError("TurnManager.EndTurn called before it was initialized.", LogTag.Turns | LogTag.Initialization);
                return;
            }
            StartCoroutine(EndTurnRoutine());
        }

        private IEnumerator EndTurnRoutine() {
            IsResolving = true;
            try {
                int endedTurn = clock.TurnNumber;
                Log($"--- Ending Turn {endedTurn} ---", LogTag.Turns);

                // --- Phase 1: Resolution (GDD 3.6): what both sides planned this Day. It's still this Day,
                // so what resolves now is dated today (statuses applied now skip this Day's Dusk) ---
                yield return ResolveQueuedActions();

                // --- Phase 2: Dusk, for the Day that just resolved: Armor fades, statuses tick, Deployment Time counts down ---
                modifierManager.ProcessEndOfTurn(endedTurn, actionsQueue);
                yield return ResolveQueuedActions();

                // The next Day begins
                clock.Advance();
                int turnNumber = clock.TurnNumber;

                // --- Phase 2: Turn Transition & Mandatory Actions ---
                foreach (var player in players) {
                    actionsQueue.AddAction(new DiscardHandAction(player));
                }
                foreach (var player in players) {
                    actionsQueue.AddAction(new DrawCardsAction(player, player.CardsToDraw));
                }
                yield return ResolveQueuedActions();

                // Notify AFTER mandatory actions, BEFORE Start-of-Turn effects
                gameMediator.NotifyTurnEnded(endedTurn); // Notify that the PREVIOUS turn number has ENDED

                // --- Phase 3: Start of Turn Processing (for the new turn) ---
                modifierManager.ProcessStartOfTurn(turnNumber, actionsQueue);
                yield return ResolveQueuedActions();

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
