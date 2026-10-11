using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SlayTheClone.Enums;
using Object = UnityEngine.Object;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The game's rules wired together with no scene and no UI, the same way GameManager builds them:
    /// two players with their boards and decks, the action queue, combat and modifiers. There is no turn flow;
    /// tests queue actions and resolve them. Randomness is seeded, so every run plays out the same.
    /// </summary>
    public sealed class TestGame : IDisposable {
        public const int RandomSeed = 1234;

        private readonly GameObject host; // Holds the mediator, which is a MonoBehaviour
        private readonly List<Object> createdData = new List<Object>();

        public Player Player1 { get; }
        public Player Player2 { get; }
        public GameMediator Mediator { get; }
        public TurnClock TurnClock { get; } = new TurnClock();
        public IActionsQueue ActionsQueue { get; }
        public IWeatherSystem WeatherSystem { get; }
        public ICardDealingService CardDealingService { get; }
        public IModifierManager ModifierManager { get; }
        public IBattlefieldCombatHandler CombatHandler { get; }

        public TestGame() {
            GameRandom.Reset(RandomSeed);

            host = new GameObject(nameof(TestGame)) { hideFlags = HideFlags.HideAndDontSave };
            Mediator = host.AddComponent<GameMediator>();
            Mediator.Initialize();

            Player1 = new Player("Player 1", isPlayer1: true);
            Player2 = new Player("Player 2", isPlayer1: false);
            Player1.Opponent = Player2;
            Player2.Opponent = Player1;
            Mediator.RegisterPlayer(Player1);
            Mediator.RegisterPlayer(Player2);

            CardDealingService = new CardDealingService(Mediator);
            WeatherSystem = new WeatherSystem(Mediator);
            ModifierManager = new ModifierManager(Mediator, new SimpleModifierFactory());
            ActionsQueue = new ActionsQueue(Mediator, WeatherSystem, CardDealingService, ModifierManager, TurnClock, ActionExecutors.CreateRegistry());
            CombatHandler = new BattlefieldCombatHandler(ActionsQueue, ModifierManager);
            Player1.Initialize(Mediator, CardDealingService);
            Player2.Initialize(Mediator, CardDealingService);
        }

        public void Dispose() {
            ModifierManager.Cleanup();
            ActionsQueue.Cleanup();
            Object.DestroyImmediate(host);
            foreach (var data in createdData) Object.DestroyImmediate(data);
        }

        #region Card definitions
        public CreatureData Creature(string name, int attack, int health, int speed = 1, params CardEffect[] effects) {
            var data = Track(ScriptableObject.CreateInstance<CreatureData>(), name, effects);
            data.attack = attack;
            data.health = health;
            data.speed = speed;
            return data;
        }

        public SpellData Spell(string name, TargetType defaultTarget, params CardEffect[] effects) {
            var data = Track(ScriptableObject.CreateInstance<SpellData>(), name, effects);
            data.defaultTargetType = defaultTarget;
            return data;
        }

        public static CardEffect Effect(EffectTrigger trigger, ActionType action, int value, TargetType target,
            TargetModifier modifier = TargetModifier.None) {
            return new CardEffect {
                trigger = trigger,
                actions = new List<EffectAction> {
                    new EffectAction { actionType = action, value = value, targetType = target, targetModifier = modifier }
                }
            };
        }

        private T Track<T>(T data, string name, CardEffect[] effects) where T : CardData {
            data.name = name;
            data.cardName = name;
            data.effects = effects.ToList();
            createdData.Add(data);
            return data;
        }
        #endregion

        #region Playing
        public void SetDecks(List<CardData> player1Deck, List<CardData> player2Deck) {
            CardDealingService.InitializeDecks(Player1, player1Deck, Player2, player2Deck);
        }

        /// <summary>Queues an action and resolves the whole queue, as the end of a turn does.</summary>
        public void Run(IGameAction action) {
            ActionsQueue.AddAction(action);
            Resolve();
        }

        public void Resolve() => ActionsQueue.ResolveActions();

        /// <summary>Starts the next turn as the turn flow does: creatures get closer to operative, then start-of-turn effects run.</summary>
        public void StartTurn() {
            TurnClock.Advance();
            ModifierManager.ProcessStartOfTurn(TurnClock.TurnNumber, ActionsQueue);
            Resolve();
        }

        /// <summary>Puts a new creature straight onto a player's board, as a summon effect would.</summary>
        public ICreature Summon(Player owner, CreatureData data, int slot) {
            var creature = (ICreature)CardFactory.CreateCard(data);
            Run(new SummonCreatureAction(creature, owner, owner.Battlefield[slot], fromDeck: true));
            return creature;
        }

        public ICard InHand(Player player, string name) {
            var card = player.Hand.FirstOrDefault(c => c.Name == name);
            NUnit.Framework.Assert.IsNotNull(card, $"{player.Name} has no {name} in hand");
            return card;
        }
        #endregion
    }
}
