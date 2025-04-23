using static DebugLogger;
using static Enums;

/// <summary>
/// Executor for PlaySpellAction
/// </summary>
public class PlaySpellActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is PlaySpellAction spellAction))
        {
            LogError($"PlaySpellActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // Get data from Action
        var spell = spellAction.GetSpell();
        var owner = spellAction.GetOwner();
        var target = spellAction.GetTarget();

        // Validate
        if (spell == null || owner == null)
        {
            LogError("PlaySpellActionExecutor: Spell or owner is null", LogTag.Actions);
            return;
        }

        // Get Dependencies from Context
        var actionsQueue = context.ActionsQueue;
        if (actionsQueue == null)
        {
            LogError($"PlaySpellActionExecutor: ActionsQueue is null in context. Cannot play spell {spell.Name}.", LogTag.Actions | LogTag.Cards);
            return;
        }

        // Execute Logic
        if (owner.Hand.Contains(spell))
        {
            owner.DiscardCard(spell); // Ensure it's removed from hand
        }

        // Process spell effects by calling the spell's Play method
        // The spell itself will queue the specific game actions
        spell.Play(owner, actionsQueue, target);

        Log($"Executed PlaySpellAction via Executor for {spell.Name}", LogTag.Actions | LogTag.Cards);
    }
}
