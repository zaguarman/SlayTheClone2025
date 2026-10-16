using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static SlayTheClone.Enums;
using Object = UnityEngine.Object;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The game's rules wired together with no scene and no UI, the same way GameManager builds them:
    /// two players with their boards and decks, the action queue, combat and modifiers. Tests queue actions and
    /// resolve them; in play mode <see cref="EndTurn"/> also runs the real turn flow. Randomness is seeded, so every
    /// run plays out the same. <see cref="History"/> records what happened.
    /// </summary>
    public sealed class TestGame : IDisposable {
        public const int RandomSeed = 1234;

        private readonly TestCards cards = new TestCards();
        private TurnManager turnManager;
        private bool decksSet;

        public Player Player1 { get; }
        public Player Player2 { get; }
        public GameMediator Mediator { get; } = new GameMediator();
        public TurnClock TurnClock { get; } = new TurnClock();
        public IActionsQueue ActionsQueue { get; }
        public ITrendSystem TrendSystem { get; }
        public ICardDealingService CardDealingService { get; }
        public IModifierManager ModifierManager { get; }
        public IBattlefieldCombatHandler CombatHandler { get; }
        public ResolutionHistory History { get; }

        public TestGame() {
            GameRandom.Reset(RandomSeed);

            // As in GameManager.Initialize
            Player1 = new Player("Player 1", isPlayer1: true) { CardsToDraw = 5 };
            Player2 = new Player("Player 2", isPlayer1: false) { CardsToDraw = 5 };
            Player1.Opponent = Player2;
            Player2.Opponent = Player1;

            CardDealingService = new CardDealingService(Mediator);
            TrendSystem = new TrendSystem();
            ModifierManager = new ModifierManager(Mediator, new SimpleModifierFactory()) { Trends = TrendSystem };
            ActionsQueue = new ActionsQueue(Mediator, TrendSystem, CardDealingService, ModifierManager, TurnClock, ActionExecutors.CreateRegistry());
            CombatHandler = new BattlefieldCombatHandler(ActionsQueue, ModifierManager);
            Player1.Initialize(Mediator, CardDealingService);
            Player2.Initialize(Mediator, CardDealingService);

            History = new ResolutionHistory(ActionsQueue, Mediator);
        }

        public void Dispose() {
            if (turnManager != null) Object.DestroyImmediate(turnManager.gameObject);
            ModifierManager.Cleanup();
            ActionsQueue.Cleanup();
            cards.Dispose();
        }

        #region Card definitions
        public CreatureData Creature(string name, int attack, int health, int speed = 1, params CardEffect[] effects) =>
            cards.Creature(name, attack, health, speed, effects);

        public SpellData Spell(string name, TargetType defaultTarget, params CardEffect[] effects) =>
            cards.Spell(name, defaultTarget, effects);

        /// <summary>A deck of plain 1/1 creatures with no effects.</summary>
        public List<CardData> Fillers(int count, string name = "Filler") => cards.Fillers(count, name);

        public static CardEffect Effect(EffectTrigger trigger, ActionType action, int value, TargetType target,
            TargetModifier modifier = TargetModifier.None) => TestCards.Effect(trigger, action, value, target, modifier);
        #endregion

        #region Playing
        public void SetDecks(List<CardData> player1Deck, List<CardData> player2Deck) {
            CardDealingService.InitializeDecks(Player1, player1Deck, Player2, player2Deck);
            decksSet = true;
        }

        /// <summary>Queues an action and resolves the whole queue, as the end of a turn does.</summary>
        public void Run(IGameAction action) {
            ActionsQueue.AddAction(action);
            Resolve();
        }

        public void Resolve() => ActionsQueue.ResolveActions();

        /// <summary>What dropping a card from the hand on a slot does: plans its play, aimed the way the board aims it.</summary>
        public void Play(ICard card, Player caster, BoardSlot slot) =>
            ActionsQueue.AddAction(new PlayCardAction(card, caster, EffectResolver.AimAt(card, caster, slot, ActionsQueue)));

        /// <summary>
        /// Ends the Day and starts the next as the turn flow does, without the hands: Dusk (Armor fades, statuses tick,
        /// Deployment Time counts down), then the new Day's start-of-turn effects.
        /// </summary>
        public void StartTurn() {
            ModifierManager.ProcessEndOfTurn(TurnClock.TurnNumber, ActionsQueue);
            SlotEffectRules.ProcessEndOfTurn(new IPlayer[] { Player1, Player2 }, TurnClock.TurnNumber, Mediator);
            TrendSystem.QueueDuskEffects(ActionsQueue);
            TrendSystem.ProcessDusk(TurnClock.TurnNumber);
            Resolve();
            Grace.ProcessDusk(new IPlayer[] { Player1, Player2 }, TurnClock.TurnNumber, Mediator);
            TurnClock.Advance();
            GoneDark.ProcessDawn(new IPlayer[] { Player1, Player2 }, TurnClock.TurnNumber, ActionsQueue);
            Prophecy.ProcessDawn(new IPlayer[] { Player1, Player2 }, TurnClock.TurnNumber, ActionsQueue);
            Policy.ProcessDawn(new IPlayer[] { Player1, Player2 }, ActionsQueue);
            ModifierManager.ProcessStartOfTurn(TurnClock.TurnNumber, ActionsQueue);
            TrendSystem.QueueDayEffects(ActionsQueue);
            Resolve();
        }

        /// <summary>
        /// Play mode only: ends the turn with the real <see cref="SlayTheClone.TurnManager"/>, which resolves what was queued and
        /// end-of-turn effects, discards and redraws both hands, then runs start-of-turn effects. Plain decks are dealt if none were set.
        /// </summary>
        public IEnumerator EndTurn() {
            if (!decksSet) SetDecks(Fillers(10), Fillers(10));
            if (turnManager == null) {
                turnManager = new GameObject("Test TurnManager").AddComponent<TurnManager>();
                turnManager.Initialize(new IPlayer[] { Player1, Player2 }, ActionsQueue, ModifierManager, Mediator, TurnClock, TrendSystem);
            }

            int turn = TurnClock.TurnNumber;
            turnManager.EndTurn();
            yield return Wait.For(() => !turnManager.IsResolving, "the turn to resolve", 10f);
            Assert.AreEqual(turn + 1, TurnClock.TurnNumber, "The turn didn't advance");
        }

        /// <summary>Puts a new creature straight onto a player's board, as a summon effect would.</summary>
        public ICreature Summon(Player owner, CreatureData data, int slot) {
            var creature = (ICreature)CardFactory.CreateCard(data);
            Run(new SummonCreatureAction(creature, owner, owner.Battlefield[slot], fromDeck: true));
            return creature;
        }

        /// <summary>Puts a new card in a player's hand.</summary>
        public ICard GiveCard(Player player, CardData data) {
            var card = CardFactory.CreateCard(data);
            player.AddToHand(card);
            CollectionAssert.Contains(player.Hand, card, $"{player.Name}'s hand is full");
            return card;
        }

        public ICard InHand(Player player, string name) {
            var card = player.Hand.FirstOrDefault(c => c.Name == name);
            Assert.IsNotNull(card, $"{player.Name} has no {name} in hand");
            return card;
        }

        /// <summary>Declares an attack the way dragging a creature onto an enemy slot does. It resolves with the rest of the queue.</summary>
        public void Declare(ICreature attacker, BoardSlot target) => CombatHandler.HandleCreatureCombat(attacker, target);

        /// <summary>Declares an attack and resolves it.</summary>
        public void Attack(ICreature attacker, BoardSlot target) {
            Declare(attacker, target);
            Resolve();
        }
        #endregion
    }
}
