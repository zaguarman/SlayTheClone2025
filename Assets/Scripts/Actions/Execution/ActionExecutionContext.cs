using System;
using System.Collections.Generic;

namespace SlayTheClone {
    /// <summary>
    /// Context object passed to executors, providing necessary services
    /// </summary>
    public class ActionExecutionContext {
        public IGameMediator GameMediator { get; }
        public ICardDealingService CardDealingService { get; }
        public IWeatherSystem WeatherSystem { get; }
        public IModifierManager ModifierManager { get; }
        public IModifierFactory ModifierFactory => ModifierManager.ModifierFactory;
        public IActionsQueue ActionsQueue { get; } // Actions might need to queue more actions
        public ITurnClock TurnClock { get; } // Timed modifiers count their turns from it

        /// <summary>
        /// Speed of the damage that killed each creature during the current resolution chain, keyed by TargetId.
        /// A creature killed by faster damage loses its own pending combat damage; equal speed resolves simultaneously.
        /// Cleared by the ActionsQueue at the start of each top-level resolution.
        /// </summary>
        public Dictionary<string, int> KillingSpeedByCreatureId { get; } = new Dictionary<string, int>();

        /// <summary>
        /// True if combat damage from this attacker must be dropped because the attacker was killed
        /// by strictly faster damage (or before this resolution chain) and never got to strike.
        /// </summary>
        public bool IsCombatDamageCancelled(ICreature attacker) {
            if (attacker == null || !attacker.IsDead) return false;
            return !KillingSpeedByCreatureId.TryGetValue(attacker.TargetId, out int killingSpeed)
                || killingSpeed > attacker.Speed;
        }

        public ActionExecutionContext(
            IGameMediator mediator,
            ICardDealingService cardDealingService,
            IWeatherSystem weatherSystem,
            IModifierManager modifierManager,
            IActionsQueue actionsQueue,
            ITurnClock turnClock) {
            GameMediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            CardDealingService = cardDealingService ?? throw new ArgumentNullException(nameof(cardDealingService));
            WeatherSystem = weatherSystem ?? throw new ArgumentNullException(nameof(weatherSystem));
            ModifierManager = modifierManager ?? throw new ArgumentNullException(nameof(modifierManager));
            ActionsQueue = actionsQueue ?? throw new ArgumentNullException(nameof(actionsQueue));
            TurnClock = turnClock ?? throw new ArgumentNullException(nameof(turnClock));
        }
    }
}
