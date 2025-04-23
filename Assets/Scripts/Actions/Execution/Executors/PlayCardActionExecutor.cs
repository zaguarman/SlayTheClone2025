using static DebugLogger;
using static Enums;

/// <summary>
/// Executor for PlayCardAction
/// </summary>
public class PlayCardActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is PlayCardAction playAction))
        {
            LogError($"PlayCardActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // Get data from Action
        var card = playAction.GetCard();
        var owner = playAction.GetOwner();
        var target = playAction.GetTarget();

        // Validate
        if (card == null || owner == null)
        {
            LogError("PlayCardActionExecutor: Card or Owner is null.", LogTag.Actions);
            return;
        }

        // Get Dependencies from Context
        var actionsQueue = context.ActionsQueue;
        if (actionsQueue == null)
        {
            LogError("PlayCardActionExecutor: ActionsQueue is null in context.", LogTag.Actions);
            return;
        }

        // Execute Logic
        if (!owner.Hand.Contains(card))
        {
            LogError($"PlayCardActionExecutor: Cannot play card - {card.Name} is not in {owner.TargetId}'s hand", LogTag.Actions);
            return;
        }

        owner.DiscardCard(card); // Remove the card from hand

        // Queue the appropriate next action
        if (card is Spell spell)
        {
            actionsQueue.AddAction(new PlaySpellAction(spell, owner, target));
        }
        else if (card is ICreature creature)
        {
            actionsQueue.AddAction(new SummonCreatureAction(creature, owner, target));
        }

        Log($"Executed PlayCardAction via Executor for {card.Name}", LogTag.Actions | LogTag.Cards);
    }
}
