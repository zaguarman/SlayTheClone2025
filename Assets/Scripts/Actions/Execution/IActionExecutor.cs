using System;
using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    /// <summary>
    /// Interface for executing a game action using the Strategy pattern
    /// </summary>
    public interface IActionExecutor {
        /// <summary>The action type this executor runs; used as its key in the executor registry.</summary>
        Type ActionType { get; }

        /// <summary>
        /// Executes the action using the provided context
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <param name="context">The execution context providing necessary services</param>
        void Execute(IGameAction action, ActionExecutionContext context);
    }

    /// <summary>
    /// Base class for executors: checks the action type once, so each executor only implements its own action.
    /// </summary>
    public abstract class ActionExecutor<TAction> : IActionExecutor where TAction : class, IGameAction {
        public Type ActionType => typeof(TAction);

        public void Execute(IGameAction action, ActionExecutionContext context) {
            if (!(action is TAction typedAction)) {
                LogError($"{GetType().Name}: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
                return;
            }
            ExecuteAction(typedAction, context);
        }

        protected abstract void ExecuteAction(TAction action, ActionExecutionContext context);

        /// <summary>Redraws the battlefields of both owners, once when they are the same player.</summary>
        protected static void NotifyBattlefieldsChanged(ActionExecutionContext context, IPlayer firstOwner, IPlayer secondOwner) {
            if (firstOwner != null) context.GameMediator.NotifyBattlefieldStateChanged(firstOwner);
            if (secondOwner != null && secondOwner != firstOwner) context.GameMediator.NotifyBattlefieldStateChanged(secondOwner);
        }
    }
}
