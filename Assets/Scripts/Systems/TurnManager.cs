using System.Collections.Generic;
using UnityEngine;
using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    /// <summary>
    /// The turn flow on screen: ending a turn runs the <see cref="TurnFlow"/> a frame at a time, playing each action back
    /// through the presenter. Built by GameManager with exactly the systems it drives.
    /// </summary>
    public class TurnManager : MonoBehaviour, ITurnManager {
        public bool IsInitialized { get; private set; }

        private TurnFlow flow;
        private IResolutionPresenter presenter; // Optional: plays each resolved action before the next one runs

        public int TurnNumber => flow?.TurnNumber ?? 0;
        public bool DrawsWaitForNextDay => flow?.DrawsWaitForNextDay ?? false;
        public bool IsResolving => flow?.IsResolving ?? false;

        public void Initialize(IReadOnlyList<IPlayer> players, IActionsQueue actionsQueue, IModifierManager modifierManager,
            IGameMediator mediator, TurnClock clock, ITrendSystem trends) {
            flow = new TurnFlow(players, actionsQueue, modifierManager, mediator, clock, trends) { Presenter = presenter };
            IsInitialized = true;
            Log("TurnManager Initialized with dependencies.", LogTag.Initialization | LogTag.Turns);
        }

        public void SetPresenter(IResolutionPresenter presenter) {
            this.presenter = presenter;
            if (flow != null) flow.Presenter = presenter;
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
            StartCoroutine(flow.EndTurnRoutine());
        }

        /// <summary>Ends the turn at once, without waiting for any playback.</summary>
        public void EndTurnImmediately() {
            if (IsResolving || !IsInitialized) return;
            flow.EndTurnImmediately();
        }
    }
}
