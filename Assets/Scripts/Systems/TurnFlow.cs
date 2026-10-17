using System;
using System.Collections;
using System.Collections.Generic;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// The turn flow, without a scene: ending a turn resolves what both sides planned and end-of-turn effects, discards
    /// and redraws hands, then runs start-of-turn effects. <see cref="TurnManager"/> runs it on screen, a frame at a time,
    /// waiting for the playback; games without a screen (the AI tournament) run it all at once, on any thread.
    /// </summary>
    public sealed class TurnFlow {
        private readonly IReadOnlyList<IPlayer> players;
        private readonly IGameMediator gameMediator;
        private readonly IModifierManager modifierManager;
        private readonly IActionsQueue actionsQueue;
        private readonly TurnClock clock;
        private readonly ITrendSystem trends;

        /// <summary>Optional: plays each resolved action before the next one runs.</summary>
        public IResolutionPresenter Presenter { get; set; }
        public int TurnNumber => clock.TurnNumber;
        public bool DrawsWaitForNextDay => clock.DrawsWaitForNextDay;
        public bool IsResolving { get; private set; }

        public TurnFlow(IReadOnlyList<IPlayer> players, IActionsQueue actionsQueue, IModifierManager modifierManager,
            IGameMediator mediator, TurnClock clock, ITrendSystem trends) {
            this.players = players ?? throw new ArgumentNullException(nameof(players));
            this.actionsQueue = actionsQueue ?? throw new ArgumentNullException(nameof(actionsQueue));
            this.modifierManager = modifierManager ?? throw new ArgumentNullException(nameof(modifierManager));
            gameMediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.trends = trends ?? throw new ArgumentNullException(nameof(trends));
        }

        /// <summary>Ends the turn at once, without waiting for any playback.</summary>
        public void EndTurnImmediately() {
            if (IsResolving || gameMediator.IsGameOver) return;
            RunToEnd(EndTurnRoutine());
        }

        private static void RunToEnd(IEnumerator routine) {
            while (routine.MoveNext()) {
                if (routine.Current is IEnumerator inner) RunToEnd(inner);
            }
        }

        /// <summary>Ends the turn one action at a time, waiting for the presenter after each; IsResolving stays true until it finishes.</summary>
        public IEnumerator EndTurnRoutine() {
            if (gameMediator.IsGameOver) yield break; // The combat is over: no more turns
            IsResolving = true;
            try {
                int endedTurn = clock.TurnNumber;
                Log($"--- Ending Turn {endedTurn} ---", LogTag.Turns);

                // --- Phase 1: Resolution (GDD 3.6): what both sides planned this Day. It's still this Day,
                // so what resolves now is dated today (statuses applied now skip this Day's Dusk) ---
                clock.DrawsWaitForNextDay = true; // This Day's hand is about to be discarded: cards drawn from now on come with the next one
                actionsQueue.BriefWatchdogs(); // The Briefing (GDD 3.7), before anything resolves
                yield return ResolveQueuedActions();
                if (gameMediator.IsGameOver) yield break; // Someone fell: the combat ends here

                // --- Phase 2: Dusk, for the Day that just resolved: Armor fades, statuses tick, Deployment Time counts down ---
                modifierManager.ProcessEndOfTurn(endedTurn, actionsQueue);
                SlotEffectRules.ProcessEndOfTurn(players, endedTurn, gameMediator, actionsQueue);
                trends.QueueDuskEffects(actionsQueue, endedTurn);
                trends.ProcessDusk(endedTurn); // A Trend ends after its last Day
                modifierManager.RefreshConditionalStats(); // On Trend loses its boost with it
                yield return ResolveQueuedActions();
                Grace.ProcessDusk(players, endedTurn, gameMediator); // Still at 0 when Grace runs out: it's over
                if (gameMediator.IsGameOver) yield break;

                // The next Day begins
                clock.Advance();
                int turnNumber = clock.TurnNumber;

                // --- Phase 2: Turn Transition & Mandatory Actions ---
                foreach (var player in players) {
                    actionsQueue.AddAction(new DiscardHandAction(player));
                }
                clock.DrawsWaitForNextDay = false;
                foreach (var player in players) {
                    // The Day's hand: up to 3 creatures, 2 more spells (5 at most), and the extra draws; ExtraDraws can be
                    // negative (Supply Chain Attack): a smaller hand, never less than none
                    actionsQueue.AddAction(new DrawCardsAction(player, player.ExtraDraws + trends.ExtraDraws(player), dealsDay: true));
                    player.ExtraDraws = 0;
                }
                yield return ResolveQueuedActions();

                // Notify AFTER mandatory actions, BEFORE Start-of-Turn effects
                gameMediator.NotifyTurnEnded(endedTurn); // Notify that the PREVIOUS turn number has ENDED

                // --- Phase 3: Start of Turn Processing (for the new turn) ---
                GoneDark.ProcessDawn(players, turnNumber, actionsQueue); // Creatures that went dark come back first
                Prophecy.ProcessDawn(players, turnNumber, actionsQueue); // Then what was foretold comes true
                Policy.ProcessDawn(players, actionsQueue); // The Policies in force happen again
                Relics.ProcessDawn(players, actionsQueue);
                modifierManager.ProcessStartOfTurn(turnNumber, actionsQueue);
                trends.QueueDayEffects(actionsQueue);
                yield return ResolveQueuedActions();
                actionsQueue.ReplayLoops(); // Groundhog Day: the looping creatures' plans are made before anyone else's

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
            if (Presenter != null) {
                yield return Presenter.WaitUntilIdle();
            }
        }
    }
}
