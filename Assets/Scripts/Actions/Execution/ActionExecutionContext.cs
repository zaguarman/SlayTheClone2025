using System;
using System.Collections.Generic;

namespace SlayTheClone {
    /// <summary>
    /// Context object passed to executors, providing necessary services
    /// </summary>
    public class ActionExecutionContext {
        public IGameMediator GameMediator { get; }
        public ICardDealingService CardDealingService { get; }
        public IWorldSystem WorldSystem { get; }
        public IModifierManager ModifierManager { get; }
        public IModifierFactory ModifierFactory => ModifierManager.ModifierFactory;
        public IActionsQueue ActionsQueue { get; } // Actions might need to queue more actions
        public ITurnClock TurnClock { get; } // Timed modifiers count their turns from it

        public ActionExecutionContext(
            IGameMediator mediator,
            ICardDealingService cardDealingService,
            IWorldSystem worldSystem,
            IModifierManager modifierManager,
            IActionsQueue actionsQueue,
            ITurnClock turnClock) {
            GameMediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            CardDealingService = cardDealingService ?? throw new ArgumentNullException(nameof(cardDealingService));
            WorldSystem = worldSystem ?? throw new ArgumentNullException(nameof(worldSystem));
            ModifierManager = modifierManager ?? throw new ArgumentNullException(nameof(modifierManager));
            ActionsQueue = actionsQueue ?? throw new ArgumentNullException(nameof(actionsQueue));
            TurnClock = turnClock ?? throw new ArgumentNullException(nameof(turnClock));
        }
    }
}
