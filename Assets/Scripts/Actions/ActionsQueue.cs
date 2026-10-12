using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>The layers every resolution pass goes through, in this order (GDD 3.7 Resolution Order).</summary>
    public enum ResolutionLayer { Movement, Summon, Spell, AbilityAndAttack }

    /// <summary>
    /// Resolves the Day's actions layer by layer (GDD 3.7): moves, then summons, then spells, then abilities and attacks.
    /// What an action does (an attack's hit, a spell's effects, a creature's entry effects) resolves as part of it, right away;
    /// abilities that trigger along the way wait in their layer. Actions queued for a layer that already finished
    /// make the layers run again until nothing is left.
    /// </summary>
    public class ActionsQueue : IActionsQueue {

        #region Fields
        // A safety net against effects that keep triggering each other forever (e.g. two creatures that retaliate
        // and grow stronger from being hit). A normal turn resolves far fewer actions.
        public const int MaxActionsPerResolution = 200;

        private class Entry {
            public IGameAction Action;
            public ResolutionLayer Layer;
            public ICreature Source; // Orders the action by its Speed and stats; null for actions no creature made
            public int Sequence;     // When it was queued
            public int RandomKey;    // The last tie-breaker, drawn once so the order doesn't shift while it waits
        }

        // A creature's ability queueing what it does when it triggers (see QueueingAbilityOf)
        private class AbilityScope : IDisposable {
            public ICreature Creature;
            public ResolutionLayer Layer;
            public bool ResolvesWithItsTrigger; // Lands as part of the action that triggered it, like an entry effect
            public Action OnDispose;
            public void Dispose() => OnDispose();
        }

        private readonly List<Entry> waiting = new List<Entry>();  // Waiting for their layer
        private readonly List<Entry> resolvingNow = new List<Entry>(); // What the action being resolved does, ahead of everything else
        private int nextResolvingNowIndex; // Keeps an action's effects in order, and their own effects right after each of them
        private Entry executing;
        private readonly Stack<AbilityScope> abilities = new Stack<AbilityScope>();
        private ResolutionLayer currentLayer;
        private int sequence;

        private readonly Dictionary<string, Entry> activeCreatureActions = new Dictionary<string, Entry>();
        private int actionsResolved; // In the current resolution

        private readonly IGameMediator gameMediator;

        private readonly Dictionary<Type, IActionExecutor> actionExecutors;
        private readonly ActionExecutionContext executionContext;
        #endregion

        #region Properties
        private int QueueId => GetHashCode(); // Tells queues apart in the logs

        public int GetPendingActionsCount() => resolvingNow.Count + waiting.Count;

        /// <summary>The queued actions, in the order they are expected to resolve.</summary>
        public IReadOnlyCollection<IGameAction> GetPendingActions() =>
            resolvingNow.Concat(waiting.OrderBy(e => e.Layer).ThenBy(e => e, InLayerOrder)).Select(e => e.Action).ToList().AsReadOnly();

        [ShowInInspector, ReadOnly]
        private List<string> Descriptions => GetPendingActions().Select(a => a.ToString()).ToList();
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

        #region Layers and order
        /// <summary>The layer an action resolves in when it is queued on its own (GDD 3.7).</summary>
        public static ResolutionLayer LayerOf(IGameAction action) => action switch {
            MoveCreatureAction or SwapCreaturesAction => ResolutionLayer.Movement,
            SummonCreatureAction => ResolutionLayer.Summon,
            PlayCardAction play => play.GetCard() is ICreature ? ResolutionLayer.Summon : ResolutionLayer.Spell,
            PlaySpellAction => ResolutionLayer.Spell,
            _ => ResolutionLayer.AbilityAndAttack
        };

        // The creature whose Speed and stats order the action: the one acting, entering or striking
        private static ICreature SourceOf(IGameAction action) => action switch {
            MoveCreatureAction move => move.GetCreature(),
            SwapCreaturesAction swap => swap.GetCreature1(),
            SummonCreatureAction summon => summon.GetCreature(),
            PlayCardAction play => play.GetCard() as ICreature,
            BattlefieldCombatAction attack => attack.GetAttacker(),
            DamageCreatureAction damage => damage.GetAttacker(),
            DamagePlayerAction damage => damage.GetAttacker(),
            _ => null
        };

        private static readonly Comparer<Entry> InLayerOrder = Comparer<Entry>.Create(CompareInLayer);

        // GDD 3.7: higher Speed first, then lowest Health + Armor, then lowest base Attack, then random.
        // Moves resolve in the order they were planned, spells have no Speed and resolve in random order,
        // and actions no creature made come after the creatures', in the order they were queued.
        private static int CompareInLayer(Entry a, Entry b) {
            if (a.Layer == ResolutionLayer.Movement) return a.Sequence.CompareTo(b.Sequence);
            if (a.Layer == ResolutionLayer.Spell) return Tiebreak(a.RandomKey.CompareTo(b.RandomKey), a, b);

            if (a.Source == null || b.Source == null) {
                if (a.Source != b.Source) return a.Source == null ? 1 : -1;
                return a.Sequence.CompareTo(b.Sequence);
            }
            if (a.Source == b.Source) return a.Sequence.CompareTo(b.Sequence); // A creature's actions keep their order

            int bySpeed = b.Source.Speed.CompareTo(a.Source.Speed);
            if (bySpeed != 0) return bySpeed;
            int byToughness = (a.Source.Health + a.Source.CurrentArmorPool).CompareTo(b.Source.Health + b.Source.CurrentArmorPool);
            if (byToughness != 0) return byToughness;
            int byAttack = a.Source.BaseAttack.CompareTo(b.Source.BaseAttack);
            if (byAttack != 0) return byAttack;
            return Tiebreak(a.RandomKey.CompareTo(b.RandomKey), a, b);
        }

        private static int Tiebreak(int byRandom, Entry a, Entry b) => byRandom != 0 ? byRandom : a.Sequence.CompareTo(b.Sequence);
        #endregion

        #region Queueing
        // A creature has one intent a Day (GDD 3.6): a move, an attack or a swap. They share its key,
        // so declaring a new one replaces the old.
        // Damage and effect actions must never be keyed by their source, or multiple hits from the same
        // creature (AoE effects, spread/chain, retaliation) would replace each other.
        private string GetActiveCreatureId(IGameAction action) {
            return action switch {
                SwapCreaturesAction swapAction => swapAction.GetCreature1()?.TargetId,
                BattlefieldCombatAction combatAction => combatAction.GetAttacker()?.TargetId,
                MoveCreatureAction moveAction => moveAction.GetCreature()?.TargetId,
                _ => null
            };
        }

        /// <summary>
        /// Everything queued until the scope is disposed is what <paramref name="creature"/>'s ability does on <paramref name="trigger"/>.
        /// Entry effects land at once, as part of the summon that triggered them (like Pokémon's Intimidate), or in the Summon layer
        /// when the creature becomes ready at Dawn. Every other ability waits its turn in the Ability and Attack layer.
        /// </summary>
        public IDisposable QueueingAbilityOf(ICreature creature, EffectTrigger trigger) {
            bool entry = trigger == EffectTrigger.OnPlay;
            var scope = new AbilityScope {
                Creature = creature,
                Layer = entry ? ResolutionLayer.Summon : ResolutionLayer.AbilityAndAttack,
                ResolvesWithItsTrigger = entry
            };
            scope.OnDispose = () => {
                if (abilities.Count > 0 && abilities.Peek() == scope) abilities.Pop();
            };
            abilities.Push(scope);
            return scope;
        }

        public void AddAction(IGameAction action) {
            var ability = abilities.Count > 0 ? abilities.Peek() : null;
            bool partOfExecutingAction = executing != null && (ability == null || ability.ResolvesWithItsTrigger);

            var entry = new Entry {
                Action = action,
                Layer = partOfExecutingAction ? executing.Layer : ability?.Layer ?? LayerOf(action),
                Source = ability?.Creature ?? SourceOf(action),
                Sequence = sequence++,
                RandomKey = GameRandom.Next(int.MaxValue)
            };

            if (partOfExecutingAction) {
                resolvingNow.Insert(nextResolvingNowIndex++, entry);
                Log($"Queued {action.GetType().Name} to resolve as part of {executing.Action.GetType().Name} (Queue ID: {QueueId})", LogTag.Actions);
            }
            else {
                string activeCreatureId = GetActiveCreatureId(action);
                if (activeCreatureId != null) {
                    if (activeCreatureActions.TryGetValue(activeCreatureId, out var existing)) {
                        Log($"Replacing existing action for creature (TargetID: {activeCreatureId.ToUpper()})", LogTag.Actions);
                        waiting.Remove(existing);
                    }
                    activeCreatureActions[activeCreatureId] = entry;
                }
                waiting.Add(entry);
                Log($"Queued {action.GetType().Name} in the {entry.Layer} layer (Queue ID: {QueueId})", LogTag.Actions);
            }
            Log($"Actions in queue: {GetPendingActionsCount()} (Queue ID: {QueueId})", LogTag.Actions);

            // Always invoke events/notify mediator after successfully adding an action
            OnActionsQueued.Invoke();
            gameMediator.NotifyActionsQueueChanged();
        }
        #endregion

        #region Resolution
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
            currentLayer = ResolutionLayer.Movement;
            Log($"Resolving actions. Initial queue size: {GetPendingActionsCount()} (Queue ID: {QueueId})", LogTag.Actions);
        }

        // What the action being resolved does comes first; otherwise the best action of the earliest layer not yet finished.
        // When only layers that already finished have actions left, the layers run again from Movement.
        private Entry TakeNextAction() {
            if (resolvingNow.Count > 0) {
                var now = resolvingNow[0];
                resolvingNow.RemoveAt(0);
                return now;
            }
            if (waiting.Count == 0) return null;

            if (!waiting.Any(e => e.Layer >= currentLayer)) {
                Log($"Actions queued for layers that already resolved; running the layers again (Queue ID: {QueueId})", LogTag.Actions);
                currentLayer = ResolutionLayer.Movement;
            }
            currentLayer = waiting.Where(e => e.Layer >= currentLayer).Min(e => e.Layer);
            var next = waiting.Where(e => e.Layer == currentLayer).OrderBy(e => e, InLayerOrder).First();
            waiting.Remove(next);
            return next;
        }

        /// <returns>False once the queue is empty.</returns>
        private bool ExecuteNextAction() {
            if (GetPendingActionsCount() == 0) return false;

            if (actionsResolved >= MaxActionsPerResolution) {
                LogError($"Stopped resolving after {MaxActionsPerResolution} actions, probably effects triggering each other endlessly. " +
                    $"Dropped the {GetPendingActionsCount()} actions still queued, starting with {GetPendingActions().First()}. (Queue ID: {QueueId})", LogTag.Actions);
                resolvingNow.Clear();
                waiting.Clear();
                gameMediator.NotifyActionsQueueChanged();
                return false;
            }

            var entry = TakeNextAction();
            var action = entry.Action;
            actionsResolved++;

            string activeCreatureId = GetActiveCreatureId(action);
            if (activeCreatureId != null && activeCreatureActions.TryGetValue(activeCreatureId, out var active) && active == entry) {
                activeCreatureActions.Remove(activeCreatureId);
            }

            if (!actionExecutors.TryGetValue(action.GetType(), out var executor)) {
                LogError($"No executor registered for action type {action.GetType().Name}. Skipping action. (Queue ID: {QueueId})", LogTag.Actions);
                return true;
            }

            Log($"Processing action: {action.GetType().Name} via {executor.GetType().Name} in the {entry.Layer} layer (Queue ID: {QueueId})", LogTag.Actions);
            OnActionExecuting.Invoke(action);
            executing = entry;
            nextResolvingNowIndex = 0;
            try {
                executor.Execute(action, executionContext);
            }
            catch (Exception ex) {
                // The turn goes on, but the failure is reported in full: its type, the action, and the original stack trace
                UnityEngine.Debug.LogException(new InvalidOperationException($"{executor.GetType().Name} failed on {action} (Queue ID: {QueueId})", ex));
            }
            finally {
                executing = null;
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
        #endregion

        #region Declared intents
        public bool HasActiveAction(string creatureId) {
            return activeCreatureActions.ContainsKey(creatureId);
        }

        public bool CancelDeclaredAction(string creatureId) {
            if (creatureId == null || !activeCreatureActions.TryGetValue(creatureId, out var entry)) return false;

            activeCreatureActions.Remove(creatureId);
            waiting.Remove(entry);
            Log($"Cancelled declared action {entry.Action.GetType().Name} (Queue ID: {QueueId})", LogTag.Actions);
            OnActionsQueued.Invoke();
            gameMediator.NotifyActionsQueueChanged();
            return true;
        }

        public IGameAction GetActiveAction(string creatureId) {
            return activeCreatureActions.TryGetValue(creatureId, out var entry) ? entry.Action : null;
        }
        #endregion

        #region Cleanup
        public void Cleanup() {
            resolvingNow.Clear();
            waiting.Clear();
            activeCreatureActions.Clear();
            abilities.Clear();
            OnActionsQueued.RemoveAllListeners();
            OnActionsResolved.RemoveAllListeners();
            OnActionExecuting.RemoveAllListeners();
            Log($"Actions queue cleaned up (Queue ID: {QueueId})", LogTag.Actions);
        }
        #endregion
    }
}
