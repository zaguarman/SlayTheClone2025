using static DebugLogger;
using static Enums;
using UnityEngine; // Added for ScriptableObject

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

        Log($"Executor: Summoning Creature={creature.Name} (ID: {creature.TargetId.ToUpper().Substring(0,8)}), Owner={(owner.IsPlayer1 ? "P1" : "P2")}, Slot={slot.TargetId.ToUpper().Substring(0,8)}, FromDeck={fromDeck}", LogTag.Actions | LogTag.Creatures);

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

        // --- Execute Logic ---
        creature.SetOwner(owner); // Set owner BEFORE adding to battlefield/registering

        // Summoning onto an occupied slot replaces its creature (no death effects); it goes to its owner's discard pile
        var replacedCreature = slot.OccupyingCreature;
        if (replacedCreature != null)
        {
            Log($"Executor: {creature.Name} replaces {replacedCreature.Name}", LogTag.Actions | LogTag.Creatures);
            modifierManager.UnregisterCreature(replacedCreature);
            (replacedCreature.Owner ?? owner).DiscardCard(replacedCreature);
        }
        else if (slot.IsOccupied())
        {
            slot.ClearSlot(true); // Card without a creature; just clear it
        }

        // Find original CardData (important for UI and potential resets)
        CardData originalData = creature.SourceData ?? owner.Deck?.FindOriginalCardDataById(creature.CardId);
        // Fallback if not found in deck (e.g., token or special summon)
        if (originalData == null && creature is ICreature creatureInstance)
        {
            LogWarning($"Could not find original CardData for {creature.Name} (ID: {creature.CardId}). Creating temp data.", LogTag.Cards);
            var tempData = ScriptableObject.CreateInstance<CreatureData>();
            tempData.cardId = creature.CardId;
            tempData.cardName = creature.Name;
            tempData.description = creature.Description;
            tempData.attack = creatureInstance.BaseAttack;
            tempData.health = creatureInstance.BaseHealth;
            tempData.speed = creatureInstance.BaseSpeed;
            // We cannot reliably get effects here without original CardData
            originalData = tempData;
        }

        // Create Controller and assign to slot
        var cardController = CardFactory.CreateCardController(creature, originalData, owner, slot.transform, mediator, references, context.GameManager);
        if (cardController != null)
        {
            slot.AssignCreature(cardController); // Assigns creature AND sets creature.Slot
            Log($"Summoned {creature.Name} to slot {slot.TargetId.ToUpper().Substring(0,8)}", LogTag.Actions | LogTag.Creatures);

            // Register with ModifierManager AFTER assigning to slot
            modifierManager.RegisterCreature(creature as Creature); // Requires concrete type

            // Trigger OnPlay Effects AFTER registration and placement
            Log($"Executor: Triggering OnPlay effects for {creature.Name}", LogTag.Effects | LogTag.Actions);
            creature.HandleEffect(EffectTrigger.OnPlay, context); // Pass the context

            // Notify AFTER everything is set up
            mediator.NotifyCreatureSummoned(creature, owner);
        }
        else
        {
            LogError($"SummonCreatureActionExecutor: Failed to create card controller for {creature.Name}", LogTag.Actions | LogTag.Creatures);
            // If controller creation fails, we should probably unregister the creature if it was registered
            modifierManager.UnregisterCreature(creature);
        }
    }
}
