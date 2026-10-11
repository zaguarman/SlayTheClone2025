using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class ActionsQueue : IActionsQueue {

        #region Fields
        // A safety net against effects that keep triggering each other forever (e.g. two creatures that retaliate
        // and grow stronger from being hit). A normal turn resolves far fewer actions.
        public const int MaxActionsPerResolution = 200;

        private readonly List<IGameAction> actionsList = new List<IGameAction>();
        private readonly Dictionary<string, IGameAction> activeCreatureActions = new Dictionary<string, IGameAction>();
        private int actionsResolved; // In the current resolution

        private readonly IGameMediator gameMediator;

        private readonly Dictionary<Type, IActionExecutor> actionExecutors;
        private readonly ActionExecutionContext executionContext;
        #endregion

        #region Properties
        private int QueueId => GetHashCode(); // Tells queues apart in the logs

        public int GetPendingActionsCount() => actionsList.Count;

        public IReadOnlyCollection<IGameAction> GetPendingActions() => actionsList.AsReadOnly();

        [ShowInInspector, ReadOnly]
        private List<string> Descriptions {
            get {
                var descriptions = new List<string>();
                foreach (var action in actionsList) {
                    descriptions.Add(action.ToString());
                }
                return descriptions;
            }
        }
        #endregion

        #region Events
        public UnityEvent OnActionsQueued { get; } = new UnityEvent();
        public UnityEvent OnActionsResolved { get; } = new UnityEvent();
        public UnityEvent<IGameAction> OnActionExecuting { get; } = new UnityEvent<IGameAction>(); // Raised just before an action runs
        #endregion

        #region Constructor
        public ActionsQueue(
            IGameMediator gameMediator,
            IWeatherSystem weatherSystem,
            ICardDealingService cardDealingService,
            IModifierManager modifierManager,
            ITurnClock turnClock,
            Dictionary<Type, IActionExecutor> actionExecutors) {
            this.gameMediator = gameMediator ?? throw new ArgumentNullException(nameof(gameMediator));
            this.actionExecutors = actionExecutors ?? throw new ArgumentNullException(nameof(actionExecutors));
            executionContext = new ActionExecutionContext(gameMediator, cardDealingService, weatherSystem, modifierManager, this, turnClock);

            Log("ActionsQueue initialized with Strategy Executors.", LogTag.Initialization);
        }
        #endregion

        #region Methods
        private int GetActionPriority(IGameAction action) {
            return action switch {
                SummonCreatureAction => -1,
                // Stat changes and statuses take effect before anything else waiting, as if applied on the spot
                MoveCreatureAction or SwapCreaturesAction or ModifyAction or ApplyStatusEffectAction => 0,
                PlayCardAction => 1,
                BattlefieldCombatAction => 4,
                DamageCreatureAction or DamagePlayerAction => 5,
                DiscardHandAction => 6,
                DrawCardsAction => 7,
                _ => 8
            };
        }

        private int GetActionSpeed(IGameAction action) {
            int speed = 1;

            ICreature creature = action switch {
                BattlefieldCombatAction combatAction => combatAction.GetAttacker(),
                DamageCreatureAction damageAction => damageAction.GetAttacker(),
                DamagePlayerAction damagePlayerAction => damagePlayerAction.GetAttacker(),
                ModifyAction modifyAction => modifyAction.GetTarget() as ICreature,
                _ => null
            };

            if (creature != null) {
                speed = creature.Speed;
            }

            return speed;
        }

        // Only a creature's declared action (one per creature, replaceable by re-declaring) is tracked here.
        // Damage and effect actions must never be keyed by their source, or multiple hits from the same
        // creature (AoE effects, spread/chain, retaliation) would replace each other.
        private string GetActiveCreatureId(IGameAction action) {
            return action switch {
                SwapCreaturesAction swapAction => swapAction.GetCreature1()?.TargetId,
                BattlefieldCombatAction combatAction => combatAction.GetAttacker()?.TargetId,
                _ => null
            };
        }

        public void AddAction(IGameAction action) {
            string activeCreatureId = GetActiveCreatureId(action);

            if (activeCreatureId != null) {
                if (activeCreatureActions.TryGetValue(activeCreatureId, out var existingAction)) {
                    Log($"Replacing existing action for creature (TargetID: {activeCreatureId.ToUpper()})", LogTag.Actions);
                    actionsList.Remove(existingAction); // Make sure to remove the correct instance
                }
                activeCreatureActions[activeCreatureId] = action;
            }

            InsertActionWithPriority(action);
            Log($"Added action to queue: {action.GetType().Name} (Queue ID: {QueueId})", LogTag.Actions);
            Log($"Actions in queue: {actionsList.Count} (Queue ID: {QueueId})", LogTag.Actions);

            // Always invoke events/notify mediator after successfully adding an action
            OnActionsQueued.Invoke();
            gameMediator.NotifyActionsQueueChanged();
        }

        private void InsertActionWithPriority(IGameAction action) {
            int priority = GetActionPriority(action);
            int speed = GetActionSpeed(action);
            int insertIndex = actionsList.Count;

            // First sort by priority (lower priority values go first)
            // Then within same priority, sort by speed (higher speed values go first)
            for (int i = 0; i < actionsList.Count; i++) {
                int existingPriority = GetActionPriority(actionsList[i]);

                if (existingPriority > priority) {
                    // Found a higher priority number (lower priority), insert before it
                    insertIndex = i;
                    break;
                }
                else if (existingPriority == priority) {
                    // Same priority, check speed
                    int existingSpeed = GetActionSpeed(actionsList[i]);

                    if (existingSpeed < speed) {
                        // Found a lower speed within same priority, insert before it
                        insertIndex = i;
                        break;
                    }
                }
            }

            actionsList.Insert(insertIndex, action);
            Log($"Inserted action {action.GetType().Name} at priority {priority}, speed {speed}, position {insertIndex} (Queue ID: {QueueId})", LogTag.Actions);
        }

        /// <summary>
        /// Resolves every queued action immediately, including those the resolved actions queue in turn.
        /// </summary>
        public void ResolveActions() {
            BeginResolution();
            while (ExecuteNextAction()) { }
            EndResolution();
        }

        /// <summary>
        /// Resolves the queued actions one at a time, running <paramref name="afterEachAction"/>
        /// (e.g. waiting for its animations) before moving on to the next one.
        /// </summary>
        public IEnumerator ResolveActionsStepByStep(Func<IEnumerator> afterEachAction) {
            BeginResolution();
            while (ExecuteNextAction()) {
                yield return afterEachAction();
            }
            EndResolution();
        }

        private void BeginResolution() {
            actionsResolved = 0;
            executionContext.KillingSpeedByCreatureId.Clear();
            Log($"Resolving actions. Initial queue size: {actionsList.Count} (Queue ID: {QueueId})", LogTag.Actions);
        }

        /// <returns>False once the queue is empty.</returns>
        private bool ExecuteNextAction() {
            if (actionsList.Count == 0) return false;

            if (actionsResolved >= MaxActionsPerResolution) {
                LogError($"Stopped resolving after {MaxActionsPerResolution} actions, probably effects triggering each other endlessly. " +
                    $"Dropped the {actionsList.Count} actions still queued, starting with {actionsList[0]}. (Queue ID: {QueueId})", LogTag.Actions);
                actionsList.Clear();
                gameMediator.NotifyActionsQueueChanged();
                return false;
            }

            var action = actionsList[0];
            actionsList.RemoveAt(0);
            actionsResolved++;

            string activeCreatureId = GetActiveCreatureId(action);
            if (activeCreatureId != null) {
                activeCreatureActions.Remove(activeCreatureId);
            }

            if (!actionExecutors.TryGetValue(action.GetType(), out var executor)) {
                LogError($"No executor registered for action type {action.GetType().Name}. Skipping action. (Queue ID: {QueueId})", LogTag.Actions);
                return true;
            }

            Log($"Processing action: {action.GetType().Name} via {executor.GetType().Name} (Queue ID: {QueueId})", LogTag.Actions);
            OnActionExecuting.Invoke(action);
            try {
                executor.Execute(action, executionContext);
            }
            catch (Exception ex) {
                LogError($"Error executing action {action.GetType().Name} via {executor.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogTag.Actions);
            }

            gameMediator.NotifyActionsQueueChanged();
            return true;
        }

        private void EndResolution() {
            activeCreatureActions.Clear();

            // ActionsQueueChanged was already raised after the last action ran
            if (actionsResolved > 0) {
                OnActionsResolved.Invoke();
            }
            Log($"Action resolution complete (Queue ID: {QueueId})", LogTag.Actions);
        }

        public bool HasActiveAction(string creatureId) {
            return activeCreatureActions.ContainsKey(creatureId);
        }

        public bool CancelDeclaredAction(string creatureId) {
            if (creatureId == null || !activeCreatureActions.TryGetValue(creatureId, out var action)) return false;

            activeCreatureActions.Remove(creatureId);
            actionsList.Remove(action);
            Log($"Cancelled declared action {action.GetType().Name} (Queue ID: {QueueId})", LogTag.Actions);
            OnActionsQueued.Invoke();
            gameMediator.NotifyActionsQueueChanged();
            return true;
        }

        public IGameAction GetActiveAction(string creatureId) {
            activeCreatureActions.TryGetValue(creatureId, out var action);
            return action;
        }

        #endregion

        #region Cleanup
        public void Cleanup() {
            actionsList.Clear();
            activeCreatureActions.Clear();
            OnActionsQueued.RemoveAllListeners();
            OnActionsResolved.RemoveAllListeners();
            OnActionExecuting.RemoveAllListeners();
            Log($"Actions queue cleaned up (Queue ID: {QueueId})", LogTag.Actions);
        }
        #endregion
    }
}
