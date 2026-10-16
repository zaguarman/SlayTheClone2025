using System;
using System.Linq;
using System.Collections.Generic;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// One game's rules, built without a scene: the two players and their decks, and every rules system, each handed only
    /// what it uses. <see cref="GameManager"/> builds one for the game on screen; games without a screen (the AI tournament)
    /// build their own, on any thread. The seed makes its shuffles and random picks replay exactly.
    /// </summary>
    public sealed class GameSession {
        public const int OpeningHand = 5;

        public Player Player1 { get; }
        public Player Player2 { get; }
        public TurnClock Clock { get; } = new TurnClock();
        public ICardDealingService CardDealingService { get; }
        public ITrendSystem TrendSystem { get; }
        public IModifierManager ModifierManager { get; }
        public IActionsQueue ActionsQueue { get; }
        public IBattlefieldCombatHandler CombatHandler { get; }
        public IReadOnlyList<IPlayer> Players => new IPlayer[] { Player1, Player2 };

        public GameSession(IGameMediator mediator, List<CardData> player1Cards, List<CardData> player2Cards, IModifierFactory modifierFactory,
            int seed, string player2Name = MatchSetup.DefaultOpponentName, int health1 = Player.StartingHealth, int health2 = Player.StartingHealth) {
            if (mediator == null) throw new ArgumentNullException(nameof(mediator));
            if (player1Cards == null) throw new ArgumentNullException(nameof(player1Cards));
            if (player2Cards == null) throw new ArgumentNullException(nameof(player2Cards));
            if (modifierFactory == null) throw new ArgumentNullException(nameof(modifierFactory));

            GameRandom.Reset(seed);
            Log($"Random seed {seed} (set it on GameManager to replay this game)", LogTag.Initialization);

            Player1 = new Player(MatchSetup.DefaultPlayerName, isPlayer1: true, health1) { CardsToDraw = OpeningHand };
            Player2 = new Player(player2Name, isPlayer1: false, health2) { CardsToDraw = OpeningHand };
            Player1.Opponent = Player2;
            Player2.Opponent = Player1;

            CardDealingService = new CardDealingService(mediator);
            TrendSystem = new TrendSystem();
            ModifierManager = new ModifierManager(mediator, modifierFactory) { Trends = TrendSystem };
            ActionsQueue = new ActionsQueue(mediator, TrendSystem, CardDealingService, ModifierManager, Clock, ActionExecutors.CreateRegistry());
            CombatHandler = new BattlefieldCombatHandler(ActionsQueue, ModifierManager);
            Player1.Initialize(mediator, CardDealingService);
            Player2.Initialize(mediator, CardDealingService);

            CardDealingService.InitializeDecks(Player1, player1Cards, Player2, player2Cards);
            Log($"Decks initialized with {player1Cards.Count} cards for {Player1.Name} and {player2Cards.Count} cards for {Player2.Name}", LogTag.Cards | LogTag.Initialization);
        }

        /// <summary>The turn flow for these players and systems, run without a screen.</summary>
        public TurnFlow CreateTurnFlow(IGameMediator mediator) =>
            new TurnFlow(Players, ActionsQueue, ModifierManager, mediator, Clock, TrendSystem);

        public void DealOpeningHands() {
            CardDealingService.DealInitialHands(Player1, Player2, OpeningHand);
            foreach (var player in new[] { Player1, Player2 }) {
                if (player.Relics.Contains(Relic.MemoNote)) CardDealingService.DrawCards(player, 1); // One more spell (or a creature, with none)
                foreach (var potion in potions.TryGetValue(player, out var carried) ? carried : new List<CardData>()) {
                    player.AddToHand(CardFactory.CreateCard(potion)); // The potions carried in: in the hand from the start
                }
            }
        }

        private readonly Dictionary<IPlayer, List<CardData>> potions = new Dictionary<IPlayer, List<CardData>>();

        /// <summary>An adventurer's kit, before the opening hands: the relics they carry, and the potions that join their hand.</summary>
        public void Equip(IPlayer player, IEnumerable<Relic> relics, IEnumerable<CardData> carried) {
            foreach (var relic in relics ?? Enumerable.Empty<Relic>()) player.Relics.Add(relic);
            potions[player] = (carried ?? Enumerable.Empty<CardData>()).Where(p => p != null).ToList();
        }

        /// <summary>Lets go of what the systems hold on to, when the game is over.</summary>
        public void Cleanup() {
            ModifierManager.Cleanup();
            ActionsQueue.Cleanup();
        }
    }
}
