using System;
using System.Collections.Generic;

namespace SlayTheClone {
    /// <summary>
    /// Context object passed to executors, providing necessary services
    /// </summary>
    public class ActionExecutionContext {
        public IGameMediator GameMediator { get; }
        public IGameReferences GameReferences { get; }
        public ICardDealingService CardDealingService { get; }
        public IWeatherSystem WeatherSystem { get; }
        public IModifierManager ModifierManager { get; }
        public IModifierFactory ModifierFactory { get; }
        public IActionsQueue ActionsQueue { get; } // Actions might need to queue more actions
        public IBattlefieldCombatHandler CombatHandler { get; } // Needed for combat/spread damage
        public ITurnManager TurnManager { get; } // Added for turn-based effects
        public IGameManager GameManager { get; } // Added for card creation

        /// <summary>
        /// Stores the creature that triggered the current action being processed,
        /// particularly useful for OnDamage effects to know the attacker.
        /// Set temporarily by the relevant executor.
        /// </summary>
        public ICreature TriggeringAttacker { get; set; }

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

        /// <summary>
        /// Constructor to initialize the context with all required dependencies
        /// </summary>
        public ActionExecutionContext(
            IGameMediator mediator,
            IGameReferences references,
            ICardDealingService cardDealingService,
            IWeatherSystem weatherSystem,
            IModifierManager modifierManager,
            IModifierFactory modifierFactory,
            IActionsQueue actionsQueue,
            IBattlefieldCombatHandler combatHandler,
            ITurnManager turnManager,
            IGameManager gameManager = null) {
            GameMediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            GameReferences = references ?? throw new ArgumentNullException(nameof(references));
            CardDealingService = cardDealingService ?? throw new ArgumentNullException(nameof(cardDealingService));
            WeatherSystem = weatherSystem ?? throw new ArgumentNullException(nameof(weatherSystem));
            ModifierManager = modifierManager ?? throw new ArgumentNullException(nameof(modifierManager));
            ModifierFactory = modifierFactory ?? throw new ArgumentNullException(nameof(modifierFactory));
            ActionsQueue = actionsQueue ?? throw new ArgumentNullException(nameof(actionsQueue));
            CombatHandler = combatHandler ?? throw new ArgumentNullException(nameof(combatHandler));
            TurnManager = turnManager ?? throw new ArgumentNullException(nameof(turnManager));
            GameManager = gameManager; // Can be null
            TriggeringAttacker = null; // Initialize to null
        }
    }
}
