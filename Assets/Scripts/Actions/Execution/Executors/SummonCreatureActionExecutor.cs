using static DebugLogger;
using static Enums;

/// <summary>
/// Executor for SummonCreatureAction
/// </summary>
public class SummonCreatureActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is SummonCreatureAction summonAction))
        {
            LogError($"SummonCreatureActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // Get data from Action
        var creature = summonAction.GetCreature();
        var owner = summonAction.GetOwner();
        var targetSlot = summonAction.GetTargetSlot();
        var fromDeck = summonAction.IsFromDeck();

        // Validate
        if (creature == null || owner == null)
        {
            LogError("SummonCreatureActionExecutor: Creature or Owner is null.", LogTag.Actions | LogTag.Creatures);
            return;
        }

        if (!(targetSlot is BattlefieldSlot slot))
        {
            LogError($"SummonCreatureActionExecutor: Invalid target slot type: {targetSlot?.GetType().Name ?? "null"}", LogTag.Actions | LogTag.Creatures);
            return;
        }

        Log($"Executor: Summoning Creature={creature.Name} (ID: {creature.TargetId.ToUpper().Substring(0,8)}), Owner={(owner.IsPlayer1() ? "P1" : "P2")}, Slot={slot.TargetId.ToUpper().Substring(0,8)}, FromDeck={fromDeck}", LogTag.Actions | LogTag.Creatures);

        // Get Dependencies from Context
        var mediator = context.GameMediator;
        var references = context.GameReferences;
        var modifierManager = context.ModifierManager;
        var actionsQueue = context.ActionsQueue;

        if (mediator == null || references == null || modifierManager == null || actionsQueue == null)
        {
            LogError($"SummonCreatureActionExecutor: Missing dependencies in context for {creature.Name}", LogTag.Actions | LogTag.Creatures | LogTag.Initialization);
            return;
        }

        // Execute Logic
        creature.SetOwner(owner);

        if (!fromDeck && owner.Hand.Contains(creature))
        {
            owner.DiscardCard(creature);
        }

        if (slot.IsOccupied())
        {
            LogWarning($"Target slot {slot.TargetId} for {creature.Name} is already occupied by {slot.OccupyingCreature?.Name}. Replacing...", LogTag.Actions | LogTag.Creatures);
            owner.RemoveFromBattlefield(slot.OccupyingCreature, true);
        }

        var cardController = CardFactory.CreateCardController(creature, owner, slot.transform, mediator, references);
        if (cardController != null)
        {
            slot.AssignCreature(cardController);
            Log($"Summoned {creature.Name} to slot {slot.TargetId.ToUpper().Substring(0,8)}", LogTag.Actions | LogTag.Creatures);

            modifierManager.RegisterCreature(creature as Creature);

            if (creature is Creature creatureInstance)
            {
                creatureInstance.HandleEffect(EffectTrigger.OnPlay, actionsQueue);
            }

            mediator.NotifyCreatureSummoned(creature, owner);
        }
        else
        {
            LogError($"SummonCreatureActionExecutor: Failed to create card controller for {creature.Name}", LogTag.Actions | LogTag.Creatures);
        }
    }
}
