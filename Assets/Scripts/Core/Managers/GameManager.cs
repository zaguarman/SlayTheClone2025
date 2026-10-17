using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Zeitgeist.DebugLogger;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// What the UI can reach of a running game: the players and the systems it shows or sends input to.
    /// Rules code never gets this; each rules object is handed only the systems it uses (see GameManager.Initialize).
    /// </summary>
    public interface IGameManager {
        bool IsInitialized { get; }
        IPlayer Player1 { get; }
        IPlayer Player2 { get; }
        IActionsQueue ActionsQueue { get; }
        IBattlefieldCombatHandler CombatHandler { get; }
        IModifierManager ModifierManager { get; }
        ICardDealingService CardDealingService { get; }
        ITrendSystem TrendSystem { get; }
        ITurnManager TurnManager { get; }
    }

    /// <summary>The turn flow, as the UI sees it.</summary>
    public interface ITurnManager : ITurnClock {
        bool IsInitialized { get; }
        bool IsResolving { get; } // True while an end of turn is being resolved and played back
        void EndTurn();
    }

    public interface IModifierManager {
        void RegisterCreature(ICreature creature);
        void UnregisterCreature(ICreature creature);
        void ApplyModifier(ICreature target, IModifier modifier);
        void RemoveModifier(ICreature target, IModifier modifier);
        IEnumerable<IModifier> GetActiveModifiersFor(ICreature creature);
        /// <summary>Remembers that this card's effect made this modifier on the creature (null: a change to the card itself).</summary>
        void Attach(ICreature creature, ICard origin, IModifier modifier, IPlayer owner);
        /// <summary>The cards whose effects are on the creature now, oldest first, each once (see <see cref="Attachment"/>).</summary>
        IReadOnlyList<Attachment> AttachmentsOf(ICreature creature);
        /// <summary>What this card tucked under the creature is doing to it now: the modifiers it put there that still last.</summary>
        IReadOnlyList<IModifier> ModifiersFrom(ICreature creature, ICard origin);
        bool HasStatusEffect(ICreature creature, StatusEffectType statusType);
        bool HasModifier(ICreature creature, Predicate<IModifier> predicate);
        /// <summary>
        /// Whether the creature can attack or move by itself: it has to be alive, operative and free of statuses that
        /// prevent actions. Logs why not. Being swapped by another creature doesn't need this.
        /// </summary>
        bool CanAct(ICreature creature);
        bool CanMove(ICreature creature); // Moving itself: deploying is fine, Exhausted or Rooted isn't
        /// <summary>Whether the creature takes +1 damage from every hit (Exposed, also part of Exhausted).</summary>
        bool IsExposed(ICreature creature);
        void RecalculateStats(ICreature creature);
        /// <summary>
        /// Recalculates the creatures whose Attack hangs on the game around them (Grudge: the fallen; On Trend: a Trend). Run
        /// after every action, so the numbers on the cards are always the ones that count.
        /// </summary>
        void RefreshConditionalStats();
        /// <summary>The game's Trends, for the abilities that ask (On Trend); null in games that have none.</summary>
        ITrendSystem Trends { get; set; }
        /// <summary>A Persistent Stat Modification on a creature in play: its base stats change and stay on the card.</summary>
        void ChangeBaseStats(ICreature creature, int attack, int health, int speed);
        void ProcessEndOfTurn(int endedTurnNumber, IActionsQueue actionsQueue);
        void ProcessStartOfTurn(int startingTurnNumber, IActionsQueue actionsQueue);
        void Cleanup();

        IModifierFactory ModifierFactory { get; }
    }

    public interface IActionsQueue {
        void AddAction(IGameAction action);
        IDisposable QueueingAbilityOf(ICreature creature, EffectTrigger trigger); // Actions queued inside are that ability's effects
        IDisposable QueueingEnvironment(); // Actions queued inside are triggered Slot Effects or Traps: they resolve before anything else
        /// <summary>The active Watchdogs, oldest first (GDD 3.11).</summary>
        IReadOnlyList<Watchdog> Watchdogs { get; }
        /// <summary>The creatures' statuses and stat changes, for rules that ask what a creature is under.</summary>
        IModifierManager Modifiers { get; }
        /// <summary>The Day it is (a part of a spell that waits counts its Days from it).</summary>
        int Today { get; }
        Watchdog AddWatchdog(WatchdogSpec spec, IPlayer controller, ICreature watchedCreature, int days);
        int DiscardPending(); // Amen: every action still queued is gone; returns how many
        /// <summary>Every Watchdog this player controls is gone (GoldenEye).</summary>
        void WipeWatchdogs(IPlayer controller);
        /// <summary>The Briefing: before the actions of the Day resolve, Briefing Watchdogs look at the whole queue once.</summary>
        void BriefWatchdogs();
        void CancelActionsOf(ICreature creature); // A creature marked for destruction loses its pending non-retaliatory actions
        /// <summary>A Reveal showed this pending action to the viewer (GDD 2.5): it isn't hidden from them any more.</summary>
        void RevealTo(IPlayer viewer, IGameAction action);
        bool IsRevealedTo(IPlayer viewer, IGameAction action);
        void ResolveActions();
        System.Collections.IEnumerator ResolveActionsStepByStep(Func<System.Collections.IEnumerator> afterEachAction);
        int GetPendingActionsCount();
        IReadOnlyCollection<IGameAction> GetPendingActions();
        ResolutionLayer LayerOfPending(IGameAction action);
        bool HasActiveAction(string creatureId);
        IGameAction GetActiveAction(string creatureId);
        bool CancelDeclaredAction(string creatureId); // Removes the creature's declared intent (move, attack or swap) from the queue
        void StartLoop(ICreature creature, IGameAction planned = null); // Groundhog Day: it repeats today's plan (or the one given) each Day while Looping
        void ReplayLoops();                 // At Dawn: the looping creatures' plans are made for them
        bool IsLooping(ICreature creature);
        bool CancelPlay(ICard card); // Takes back a card played this Day that hasn't resolved yet; it stays in the hand
        void Cleanup();
        // Events
        UnityEngine.Events.UnityEvent OnActionsQueued { get; }
        UnityEngine.Events.UnityEvent OnActionsResolved { get; }
        UnityEngine.Events.UnityEvent<IGameAction> OnActionExecuting { get; }
    }

    public interface IBattlefieldCombatHandler {
        void HandleCreatureCombat(ICreature attacker, BoardSlot targetSlot);
        void ResetAttackingCreatures();
        bool HasCreatureAttacked(ITarget creature);
        bool CancelAttack(ICreature attacker);
        BoardSlot GetTargetedSlot(ITarget attacker);
    }

    /// <summary>
    /// Builds the game at startup (players, decks, and the rules systems, each handed only what it uses),
    /// then starts the turn flow. After that it is just the UI's way into the running game.
    /// </summary>
    public class GameManager : MonoBehaviour, IGameManager {
        [Tooltip("0 picks a new seed every game. Any other value replays the same shuffles and random picks.")]
        [SerializeField] private int randomSeed;

        public bool IsInitialized { get; private set; }

        /// <summary>The seed the next Initialize uses (0: a new one). Set before it, e.g. by a game played without a screen.</summary>
        public int RandomSeed { get => randomSeed; set => randomSeed = value; }

        public Player Player1 { get; private set; }
        public Player Player2 { get; private set; }
        IPlayer IGameManager.Player1 => Player1;
        IPlayer IGameManager.Player2 => Player2;

        [ShowInInspector]
        public IActionsQueue ActionsQueue { get; private set; }
        public IBattlefieldCombatHandler CombatHandler { get; private set; }
        [ShowInInspector, BoxGroup("Systems")]
        public IModifierManager ModifierManager { get; private set; }
        public ICardDealingService CardDealingService { get; private set; }
        public ITrendSystem TrendSystem { get; private set; }
        public ITurnManager TurnManager { get; private set; }

        [ShowInInspector, BoxGroup("Hands"), PropertyOrder]
        [ListDrawerSettings]
        public List<string> Player1Hand => DescribeHand(Player1);

        [ShowInInspector, BoxGroup("Hands"), PropertyOrder]
        [ListDrawerSettings]
        public List<string> Player2Hand => DescribeHand(Player2);

        private static List<string> DescribeHand(IPlayer player) => player?.Hand
            .Select((card, index) => $"Card {index + 1}: {card.Name}")
            .ToList() ?? new List<string>();

        public void Initialize(IGameMediator mediator, List<CardData> player1Cards, List<CardData> player2Cards, TurnManager turnManager, IModifierFactory modifierFactory,
            string player2Name = MatchSetup.DefaultOpponentName) {
            if (IsInitialized) {
                LogWarning("GameManager Initialize called but already initialized.", LogTag.Initialization);
                return;
            }
            if (turnManager == null) throw new ArgumentNullException(nameof(turnManager));

            // The players and the rules systems, each given only what it uses (see GameSession)
            int seed = randomSeed != 0 ? randomSeed : Environment.TickCount;
            var session = new GameSession(mediator, player1Cards, player2Cards, modifierFactory, seed, player2Name,
                MatchSetup.PlayerHealth ?? Player.StartingHealth, MatchSetup.OpponentHealth ?? Player.StartingHealth); // An adventure's Health
            Player1 = session.Player1;
            Player2 = session.Player2;
            CardDealingService = session.CardDealingService;
            TrendSystem = session.TrendSystem;
            ModifierManager = session.ModifierManager;
            ActionsQueue = session.ActionsQueue;
            CombatHandler = session.CombatHandler;

            // The turn flow drives the rules; it never needs this manager
            turnManager.Initialize(session.Players, ActionsQueue, ModifierManager, mediator, session.Clock, TrendSystem);
            TurnManager = turnManager;

            IsInitialized = true;

            session.Equip(Player1, MatchSetup.PlayerRelics, MatchSetup.PlayerPotions.Select(p => p.ToCardData())); // An adventure's kit
            session.Equip(Player2, MatchSetup.OpponentRelics, null); // An elite's or a boss's relics
            session.DealOpeningHands();
            recorder = StartRecording(session, mediator as GameMediator, seed, player1Cards, player2Cards);
            Log("GameManager Initialization complete.", LogTag.Initialization);
        }

        private GameRecorder recorder;

        // The telemetry's record of this game (see GameRecorder): who plays what, and an adventure's room
        private GameRecorder StartRecording(GameSession session, GameMediator mediator, int seed, List<CardData> cards1, List<CardData> cards2) {
            string Who(IPlayer player) => MatchSetup.PlayedByAI(player) ? $"AI {MatchSetup.AIFor(player).Name}" : "human";
            bool adventure = AdventureRun.Fighting != null;
            var room = AdventureRun.Fighting;
            return GameRecorder.Begin(session, mediator,
                mode: adventure ? AdventureAutopilot.On ? "autopilot" : "adventure" : "match", seed,
                new GameRecorder.Seat { Name = Player1.Name, Deck = MatchSetup.PlayerDeck?.GetDeckName() ?? "Scene deck", PlayedBy = Who(Player1),
                    Cards = cards1, Relics = string.Join(", ", Player1.Relics.Select(Relics.NameOf)) },
                new GameRecorder.Seat { Name = Player2.Name, Deck = MatchSetup.OpponentDeck?.GetDeckName() ?? "Scene deck", PlayedBy = Who(Player2),
                    Cards = cards2, Relics = string.Join(", ", Player2.Relics.Select(Relics.NameOf)) },
                adventure ? new AdventureRecord {
                    runId = AdventureRun.RunId, faction = Umbrellas.Name(AdventureRun.Umbrella), room = room.Type.ToString(),
                    row = room.Row + 1, fightsWonBefore = AdventureRun.FightsWon, ascension = AdventureRun.Ascension
                } : null,
                Who);
        }

        protected void OnDestroy() {
            recorder?.Abandon(); // Left before the end: saved as not ended (a finished game was saved already)
            ModifierManager?.Cleanup();
            ActionsQueue?.Cleanup();
            IsInitialized = false;
        }
    }
}
