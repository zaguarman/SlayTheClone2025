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
            public bool Retaliatory; // A reaction to what was done to its creature: its death doesn't cancel it
            public bool Environment; // A triggered Slot Effect or Trap: resolves before anything else (GDD 3.7)
            public bool Watched;     // A Watchdog already had its look at it (only one triggers per action)
            public int Reordered;    // Moved by a Watchdog inside its layer: -1 earlier, +1 later
        }

        private class Scope : IDisposable {
            private readonly Action onDispose;
            public Scope(Action onDispose) => this.onDispose = onDispose;
            public void Dispose() => onDispose();
        }

        // A creature's ability queueing what it does when it triggers (see QueueingAbilityOf)
        private class AbilityScope : IDisposable {
            public ICreature Creature;
            public ResolutionLayer Layer;
            public bool ResolvesWithItsTrigger; // Lands as part of the action that triggered it, like an entry effect
            public bool Retaliatory;
            public Action OnDispose;
            public void Dispose() => OnDispose();
        }

        private readonly List<Entry> waiting = new List<Entry>();  // Waiting for their layer
        private readonly List<Entry> resolvingNow = new List<Entry>(); // What the action being resolved does, ahead of everything else
        private int nextResolvingNowIndex; // Keeps an action's effects in order, and their own effects right after each of them
        private Entry executing;
        private readonly Stack<AbilityScope> abilities = new Stack<AbilityScope>();
        private int environmentScopes; // Open QueueingEnvironment scopes
        private readonly List<Watchdog> watchdogs = new List<Watchdog>(); // Oldest first
        private int watchdogSequence;
        private readonly Dictionary<IPlayer, HashSet<IGameAction>> revealed = new Dictionary<IPlayer, HashSet<IGameAction>>(); // By viewer
        private bool resolving;
        private bool lastTakenWasPartOfAction; // TakeNextAction's pick came from resolvingNow
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
            resolvingNow.Concat(waiting.OrderBy(e => !e.Environment).ThenBy(e => e.Layer).ThenBy(e => e, InLayerOrder)).Select(e => e.Action).ToList().AsReadOnly();

        /// <summary>The layer a pending action waits in (GDD 3.7), or the one it would take if it isn't queued.</summary>
        public ResolutionLayer LayerOfPending(IGameAction action) =>
            resolvingNow.Concat(waiting).FirstOrDefault(e => e.Action == action)?.Layer ?? LayerOf(action);

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
            ITrendSystem trendSystem,
            ICardDealingService cardDealingService,
            IModifierManager modifierManager,
            ITurnClock turnClock,
            Dictionary<Type, IActionExecutor> actionExecutors) {
            this.gameMediator = gameMediator ?? throw new ArgumentNullException(nameof(gameMediator));
            this.actionExecutors = actionExecutors ?? throw new ArgumentNullException(nameof(actionExecutors));
            executionContext = new ActionExecutionContext(gameMediator, cardDealingService, trendSystem, modifierManager, this, turnClock);

            Log("ActionsQueue initialized with Strategy Executors.", LogTag.Initialization);
        }
        #endregion

        #region Layers and order
        /// <summary>The layer an action resolves in when it is queued on its own (GDD 3.7).</summary>
        public static ResolutionLayer LayerOf(IGameAction action) => action switch {
            MoveCreatureAction => ResolutionLayer.Movement,
            SummonCreatureAction or ReappearAction => ResolutionLayer.Summon,
            PlayCardAction play => play.GetCard() is ICreature ? ResolutionLayer.Summon : ResolutionLayer.Spell,
            PlaySpellAction => ResolutionLayer.Spell,
            UseAbilityAction use when Abilities.LandsWithSpells(use.Creature?.Ability ?? Enums.CreatureAbility.None) => ResolutionLayer.Spell,
            _ => ResolutionLayer.AbilityAndAttack
        };

        // The creature whose Speed and stats order the action: the one acting, entering or striking
        private static ICreature SourceOf(IGameAction action) => action switch {
            MoveCreatureAction move => move.GetCreature(),
            SummonCreatureAction summon => summon.GetCreature(),
            PlayCardAction play => play.GetCard() as ICreature,
            BattlefieldCombatAction attack => attack.GetAttacker(),
            UseAbilityAction use => use.Creature,
            DamageCreatureAction damage => damage.GetAttacker(),
            DamagePlayerAction damage => damage.GetAttacker(),
            _ => null
        };

        private Comparer<Entry> inLayerOrder;
        private Comparer<Entry> InLayerOrder => inLayerOrder ??= Comparer<Entry>.Create(CompareInLayer);

        // GDD 3.7: higher Speed first (lower first in Rush Hour); at the same Speed, one side's actions in the order its player
        // planned them, and between the two sides lowest Health + Armor, then lowest base Attack, then random.
        // Moves resolve in the order they were planned, spells have no Speed and resolve in random order,
        // and actions no creature made come after the creatures', in the order they were queued.
        // A Watchdog's Reorder moves an action before or after the rest of its layer.
        private int CompareInLayer(Entry a, Entry b) {
            if (a.Layer == ResolutionLayer.Movement) return a.Sequence.CompareTo(b.Sequence);

            int byWatchdog = a.Reordered.CompareTo(b.Reordered); // Watchdog Reorder: before or after the rest of its layer
            if (byWatchdog != 0) return byWatchdog;

            if (a.Layer == ResolutionLayer.Spell) return Tiebreak(a.RandomKey.CompareTo(b.RandomKey), a, b);

            if (a.Source == null || b.Source == null) {
                if (a.Source != b.Source) return a.Source == null ? 1 : -1;
                return a.Sequence.CompareTo(b.Sequence);
            }
            if (a.Source == b.Source) return a.Sequence.CompareTo(b.Sequence); // A creature's actions keep their order

            int bySpeed = b.Source.Speed.CompareTo(a.Source.Speed);
            if (executionContext.TrendSystem.SlowestFirst) bySpeed = -bySpeed; // Rush Hour
            if (bySpeed != 0) return bySpeed;
            if (a.Source.Owner == b.Source.Owner) return a.Sequence.CompareTo(b.Sequence); // Your own ties: as you planned them
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
                BattlefieldCombatAction combatAction => combatAction.GetAttacker()?.TargetId,
                UseAbilityAction use => use.Creature?.TargetId,
                MoveCreatureAction moveAction => moveAction.GetCreature()?.TargetId,
                _ => null
            };
        }

        /// <summary>
        /// Everything queued until the scope is disposed is what <paramref name="creature"/>'s ability does on <paramref name="trigger"/>.
        /// Entry effects land at once, as part of the summon that triggered them (like Pokémon's Intimidate), or in the Summon layer
        /// when the creature becomes ready at Dawn. OnDamage and OnAboutToDie also resolve right away (GDD 3.9).
        /// Every other ability waits its turn in the Ability and Attack layer.
        /// </summary>
        public IDisposable QueueingAbilityOf(ICreature creature, EffectTrigger trigger) {
            var scope = new AbilityScope {
                Creature = creature,
                Layer = trigger == EffectTrigger.OnPlay ? ResolutionLayer.Summon : ResolutionLayer.AbilityAndAttack,
                ResolvesWithItsTrigger = trigger is EffectTrigger.OnPlay or EffectTrigger.OnDamage or EffectTrigger.OnAboutToDie or EffectTrigger.OnRevealed,
                Retaliatory = trigger is EffectTrigger.OnDamage or EffectTrigger.OnAboutToDie or EffectTrigger.OnDeath or EffectTrigger.OnRevealed
            };
            scope.OnDispose = () => {
                if (abilities.Count > 0 && abilities.Peek() == scope) abilities.Pop();
            };
            abilities.Push(scope);
            return scope;
        }

        /// <summary>
        /// Everything queued until the scope is disposed is an environment action: a triggered Slot Effect or Trap (GDD 3.7).
        /// It resolves immediately, right after the action that set it off (or first, if none is resolving), before anything else.
        /// </summary>
        public IDisposable QueueingEnvironment() {
            environmentScopes++;
            return new Scope(() => environmentScopes--);
        }

        public void AddAction(IGameAction action) {
            if (action is PlayCardAction costly && !DiscardCost.TryPay(costly, this)) return; // Can't pay its discard cost: it isn't played
            if (action is PlayCardAction offering && !Sacrifice.CanPay(offering.GetCard(), offering.GetOwner())) { // Nobody to sacrifice
                Log($"{offering.GetCard()?.Name} needs {Sacrifice.Of(offering.GetCard())} creature(s) to sacrifice: it isn't played", LogTag.Actions | LogTag.Cards);
                return;
            }
            if (action is PlayCardAction doomed && !DoomCost.CanPay(doomed.GetCard(), doomed.GetOwner(), Modifiers)) { // Nobody to doom
                Log($"{doomed.GetCard()?.Name} needs a creature that isn't Doomed yet to doom: it isn't played", LogTag.Actions | LogTag.Cards);
                return;
            }
            if (executing == null && !resolving && IsInstant(action)) {
                ResolveInstantly(action);
                return;
            }

            bool environment = environmentScopes > 0;
            var ability = environment ? null : abilities.Count > 0 ? abilities.Peek() : null;
            bool partOfExecutingAction = executing != null && (environment || ability == null || ability.ResolvesWithItsTrigger);

            var entry = new Entry {
                Action = action,
                Layer = partOfExecutingAction ? executing.Layer : environment ? ResolutionLayer.AbilityAndAttack : ability?.Layer ?? LayerOf(action),
                Source = environment ? null : ability?.Creature ?? SourceOf(action),
                Sequence = sequence++,
                RandomKey = GameRandom.Next(int.MaxValue),
                Retaliatory = ability?.Retaliatory ?? false,
                Environment = environment
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
        // Information is instant (GDD 2.5): a card that only changes what its caster can see resolves when it's played,
        // during Planning, outside the order of the Day
        private static bool IsInstant(IGameAction action) =>
            action is PlayCardAction play && play.GetCard() is Spell spell && EffectResolver.IsInstant(spell);

        private void ResolveInstantly(IGameAction action) {
            Log($"{action.GetType().Name} is instant: resolving it now (Queue ID: {QueueId})", LogTag.Actions);
            var entry = new Entry { Action = action, Layer = LayerOf(action), Sequence = sequence++, Watched = true };
            // Information is outside the order of the Day, but an Intelligence Watchdog still sees it (Glomar Response)
            DropFinishedWatchdogs();
            var glomar = watchdogs.FirstOrDefault(w => w.Spec.timing == WatchdogTiming.OnWatch && w.Spec.watches == WatchedAction.Intelligence
                                                       && w.Spec.effect == WatchdogEffect.Nullify && Sees(w, entry) && w.Matches(action, null));
            if (glomar != null) {
                glomar.SpendCharge();
                WatchdogRules.LogTrigger(glomar, action, "nullified: they learn nothing");
                if (action is PlayCardAction play) play.GetOwner()?.DiscardCard(play.GetCard()); // Spent for nothing
                OnActionsQueued.Invoke();
                gameMediator.NotifyActionsQueueChanged();
                return;
            }
            actionsResolved = 0;
            Run(entry);
            while (resolvingNow.Count > 0 && ExecuteNextAction()) { } // What it does, and nothing else queued
            OnActionsQueued.Invoke();
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
            resolving = true;
            actionsResolved = 0;
            currentLayer = ResolutionLayer.Movement;
            Log($"Resolving actions. Initial queue size: {GetPendingActionsCount()} (Queue ID: {QueueId})", LogTag.Actions);
        }

        // What the action being resolved does comes first; otherwise the best action of the earliest layer not yet finished.
        // When only layers that already finished have actions left, the layers run again from Movement.
        private Entry TakeNextAction() {
            lastTakenWasPartOfAction = resolvingNow.Count > 0;
            if (resolvingNow.Count > 0) {
                var now = resolvingNow[0];
                resolvingNow.RemoveAt(0);
                return now;
            }
            if (waiting.Count == 0) return null;

            // Environment actions queued while nothing was resolving go before everything, in random order
            var environment = waiting.Where(e => e.Environment).OrderBy(e => e.RandomKey).FirstOrDefault();
            if (environment != null) {
                waiting.Remove(environment);
                return environment;
            }

            if (!waiting.Any(e => e.Layer >= currentLayer)) {
                Log($"Actions queued for layers that already resolved; running the layers again (Queue ID: {QueueId})", LogTag.Actions);
                currentLayer = ResolutionLayer.Movement;
            }
            currentLayer = waiting.Where(e => e.Layer >= currentLayer).Min(e => e.Layer);
            var next = currentLayer == ResolutionLayer.Movement
                ? NextMove()
                : waiting.Where(e => e.Layer == currentLayer).OrderBy(e => e, InLayerOrder).First();
            waiting.Remove(next);
            return next;
        }

        // Automatic Move Order (GDD 2.4): players choose where their creatures go, never in what order. The first planned
        // move that works right now goes next; a creature in the way that is planned to leave gets to leave first, and is
        // never pushed or swapped while it still means to go. If no move works, the first planned goes (and stops short).
        private Entry NextMove() {
            var moves = waiting.Where(e => e.Layer == ResolutionLayer.Movement).OrderBy(e => e.Sequence).ToList();
            var leaving = moves.Select(e => e.Action).OfType<MoveCreatureAction>().Select(m => m.GetCreature())
                .Where(c => c != null && executionContext.ModifierManager.CanAct(c)).ToHashSet();

            bool Works(Entry entry) => !(entry.Action is MoveCreatureAction move)
                || MovementRules.WorksNow(MovementRules.Resolve(move.GetCreature(), move.GetToSlot(), move.Kind), leaving);
            return moves.FirstOrDefault(Works) ?? moves.First();
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
            actionsResolved++;

            // On Watch Watchdogs look at each action just before it resolves; what an action does as part of itself isn't watched
            if (!lastTakenWasPartOfAction && !entry.Watched && !entry.Environment && OnWatch(entry)) {
                gameMediator.NotifyActionsQueueChanged();
                return true;
            }
            Run(entry);
            return true;
        }

        private void Run(Entry entry) {
            var action = entry.Action;

            string activeCreatureId = GetActiveCreatureId(action);
            if (activeCreatureId != null && activeCreatureActions.TryGetValue(activeCreatureId, out var active) && active == entry) {
                activeCreatureActions.Remove(activeCreatureId);
            }

            if (!actionExecutors.TryGetValue(action.GetType(), out var executor)) {
                LogError($"No executor registered for action type {action.GetType().Name}. Skipping action. (Queue ID: {QueueId})", LogTag.Actions);
                return;
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
        }

        private void EndResolution() {
            resolving = false;
            activeCreatureActions.Clear();
            revealed.Clear(); // What was revealed has resolved

            // ActionsQueueChanged was already raised after the last action ran
            if (actionsResolved > 0) {
                OnActionsResolved.Invoke();
            }
            Log($"Action resolution complete (Queue ID: {QueueId})", LogTag.Actions);
        }
        #endregion

        #region Watchdogs
        public IModifierManager Modifiers => executionContext.ModifierManager;

        public IReadOnlyList<Watchdog> Watchdogs =>
            watchdogs.Where(w => !w.IsSpent && !w.HasExpired(executionContext.TurnClock.TurnNumber)).ToList().AsReadOnly();

        /// <summary>A Watchdog becomes active (GDD 3.11). From now on it watches; a Briefing one briefs from tomorrow.</summary>
        public void AddWatchdog(WatchdogSpec spec, IPlayer controller, ICreature watchedCreature, int days) {
            var watchdog = new Watchdog(spec, controller, watchedCreature, executionContext.TurnClock.TurnNumber, days, watchdogSequence++);
            watchdogs.Add(watchdog);
            Log($"Active: {watchdog}", LogTag.Actions | LogTag.Effects);
            gameMediator.NotifyActionsQueueChanged();
        }

        public void WipeWatchdogs(IPlayer controller) {
            int wiped = watchdogs.RemoveAll(w => w.Controller == controller);
            Log($"{controller?.Name}'s Watchdogs are wiped out ({wiped})", LogTag.Actions | LogTag.Effects);
            gameMediator.NotifyActionsQueueChanged();
        }

        private void DropFinishedWatchdogs() {
            int turn = executionContext.TurnClock.TurnNumber;
            watchdogs.RemoveAll(w => w.IsSpent || w.HasExpired(turn));
        }

        // GDD 3.11: a Watchdog can't match an action its controller can't see (Concealed), and ignores Encrypted creatures'
        // actions without spending a charge
        private bool Sees(Watchdog watchdog, Entry entry) {
            var modifiers = executionContext.ModifierManager;
            return !Intel.IsEncrypted(WatchdogRules.ActorOf(entry.Action, entry.Source), modifiers)
                && Intel.CanSee(watchdog.Controller, entry.Action, this, modifiers);
        }

        public void RevealTo(IPlayer viewer, IGameAction action) {
            if (viewer == null || action == null) return;
            if (!revealed.TryGetValue(viewer, out var seen)) revealed[viewer] = seen = new HashSet<IGameAction>();
            seen.Add(action);
        }

        public bool IsRevealedTo(IPlayer viewer, IGameAction action) =>
            viewer != null && revealed.TryGetValue(viewer, out var seen) && seen.Contains(action);

        // The oldest On Watch Watchdog that matches acts on the action. True if the action doesn't resolve now
        private bool OnWatch(Entry entry) {
            entry.Watched = true;
            DropFinishedWatchdogs();
            var watchdog = watchdogs.FirstOrDefault(w => w.Spec.timing == WatchdogTiming.OnWatch && Sees(w, entry) && w.Matches(entry.Action, entry.Source));
            if (watchdog == null) return false;

            watchdog.SpendCharge();
            switch (watchdog.Spec.effect) {
                case WatchdogEffect.Nullify:
                    WatchdogRules.LogTrigger(watchdog, entry.Action, "nullified");
                    return true; // It resolves with no effect
                case WatchdogEffect.Backfire: // Nullified, and the attacker takes its own Attack instead, now
                    var backfire = WatchdogRules.Backfire(entry.Action);
                    WatchdogRules.LogTrigger(watchdog, entry.Action, "backfired");
                    if (backfire == null) return true;
                    entry.Action = backfire;
                    return false;
                case WatchdogEffect.Reorder: // On Watch can only move an action later
                    WatchdogRules.LogTrigger(watchdog, entry.Action, "moved to the end of its layer");
                    entry.Reordered = 1;
                    waiting.Add(entry);
                    return true;
                case WatchdogEffect.Redirect:
                    entry.Action = WatchdogRules.Redirect(entry.Action, entry.Source);
                    WatchdogRules.LogTrigger(watchdog, entry.Action, "redirected");
                    return false;
                default: // Reveal: its controller sees it
                    RevealTo(watchdog.Controller, entry.Action);
                    WatchdogRules.LogTrigger(watchdog, entry.Action, "revealed");
                    return false;
            }
        }

        /// <summary>
        /// The Briefing (GDD 3.7): before the actions of the Day resolve, each Briefing Watchdog (active since an earlier Day)
        /// looks at the whole queue once, picks the first, the strongest or every matching action, and changes them.
        /// Each action it changes uses 1 charge.
        /// </summary>
        public void BriefWatchdogs() {
            DropFinishedWatchdogs();
            int turn = executionContext.TurnClock.TurnNumber;
            foreach (var watchdog in watchdogs.Where(w => w.CanBrief(turn)).ToList()) {
                var matches = waiting.Where(e => !e.Watched && !e.Environment && Sees(watchdog, e) && watchdog.Matches(e.Action, e.Source))
                    .OrderBy(e => e.Layer).ThenBy(e => e, InLayerOrder).ToList();
                if (matches.Count == 0) continue;

                // Each action it changes uses a charge: "every" means every match it still has charges for
                var picked = watchdog.Spec.pick switch {
                    BriefingPick.Every => matches.Take(watchdog.ChargesLeft).ToList(),
                    BriefingPick.Strongest => matches.OrderByDescending(e => WatchdogRules.StrengthOf(e.Action)).Take(1).ToList(),
                    _ => matches.Take(1).ToList()
                };
                foreach (var entry in picked) {
                    watchdog.SpendCharge();
                    entry.Watched = true;
                    switch (watchdog.Spec.effect) {
                        case WatchdogEffect.Nullify:
                            WatchdogRules.LogTrigger(watchdog, entry.Action, "nullified at the Briefing");
                            waiting.Remove(entry);
                            string activeCreatureId = GetActiveCreatureId(entry.Action);
                            if (activeCreatureId != null && activeCreatureActions.TryGetValue(activeCreatureId, out var active) && active == entry) {
                                activeCreatureActions.Remove(activeCreatureId);
                            }
                            break;
                        case WatchdogEffect.Backfire: // In its place, when it would have struck: its own Attack, to itself
                            var backfire = WatchdogRules.Backfire(entry.Action);
                            WatchdogRules.LogTrigger(watchdog, entry.Action, "backfires at the Briefing");
                            if (backfire == null) {
                                waiting.Remove(entry);
                                break;
                            }
                            string attackerId = GetActiveCreatureId(entry.Action);
                            if (attackerId != null && activeCreatureActions.TryGetValue(attackerId, out var planned) && planned == entry) activeCreatureActions.Remove(attackerId);
                            entry.Action = backfire;
                            break;
                        case WatchdogEffect.Reorder:
                            entry.Reordered = watchdog.Spec.moveEarlier ? -1 : 1;
                            WatchdogRules.LogTrigger(watchdog, entry.Action, watchdog.Spec.moveEarlier ? "moved first in its layer" : "moved last in its layer");
                            break;
                        case WatchdogEffect.Redirect:
                            entry.Action = WatchdogRules.Redirect(entry.Action, entry.Source);
                            WatchdogRules.LogTrigger(watchdog, entry.Action, "redirected at the Briefing");
                            break;
                        default:
                            RevealTo(watchdog.Controller, entry.Action);
                            WatchdogRules.LogTrigger(watchdog, entry.Action, "revealed at the Briefing");
                            break;
                    }
                }
            }
            DropFinishedWatchdogs();
            gameMediator.NotifyActionsQueueChanged();
        }
        #endregion

        #region Dying creatures
        /// <summary>
        /// A creature marked for destruction loses its pending actions (GDD 3.9): its attack, move or ability, and earlier triggers.
        /// Its retaliation (OnDamage, OnAboutToDie, OnDied) still happens.
        /// </summary>
        public void CancelActionsOf(ICreature creature) {
            if (creature == null) return;

            var cancelled = waiting.Where(e => e.Source == creature && !e.Retaliatory).ToList();
            if (cancelled.Count == 0) return;

            foreach (var entry in cancelled) {
                waiting.Remove(entry);
                string activeCreatureId = GetActiveCreatureId(entry.Action);
                if (activeCreatureId != null && activeCreatureActions.TryGetValue(activeCreatureId, out var active) && active == entry) {
                    activeCreatureActions.Remove(activeCreatureId);
                }
                Log($"Cancelled {entry.Action.GetType().Name}: {creature.Name} is about to die (Queue ID: {QueueId})", LogTag.Actions);
            }
            gameMediator.NotifyActionsQueueChanged();
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

        /// <summary>Takes back a card played this Day that hasn't resolved yet. It never left the hand, so nothing else changes.</summary>
        public bool CancelPlay(ICard card) {
            if (card == null) return false;

            var entry = waiting.FirstOrDefault(e => e.Action is PlayCardAction play && play.GetCard() == card);
            if (entry == null) return false;

            waiting.Remove(entry);
            if (entry.Action is PlayCardAction taken) DiscardCost.Refund(taken);
            Log($"Cancelled the play of {card.Name} (Queue ID: {QueueId})", LogTag.Actions | LogTag.Cards);
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
            watchdogs.Clear();
            revealed.Clear();
            OnActionsQueued.RemoveAllListeners();
            OnActionsResolved.RemoveAllListeners();
            OnActionExecuting.RemoveAllListeners();
            Log($"Actions queue cleaned up (Queue ID: {QueueId})", LogTag.Actions);
        }
        #endregion
    }
}
