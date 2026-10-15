using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class SummonCreatureActionExecutor : ActionExecutor<SummonCreatureAction> {
        protected override void ExecuteAction(SummonCreatureAction summonAction, ActionExecutionContext context) {
            var creature = summonAction.GetCreature();
            var owner = summonAction.GetOwner();

            if (creature == null || owner == null) {
                LogError("SummonCreatureActionExecutor: Creature or Owner is null.", LogTag.Actions | LogTag.Creatures);
                return;
            }

            var targetSlot = summonAction.GetTargetSlot();
            if (!(targetSlot is BoardSlot slot)) {
                LogError($"SummonCreatureActionExecutor: Invalid target slot type: {targetSlot?.GetType().Name ?? "null"}", LogTag.Actions | LogTag.Creatures);
                return;
            }

            Log($"Executor: Summoning Creature={creature.Name} (ID: {creature.TargetId.ShortId()}), Owner={owner.Name}, Slot={slot.TargetId.ShortId()}, FromDeck={summonAction.IsFromDeck()}", LogTag.Actions | LogTag.Creatures);

            var mediator = context.GameMediator;
            var modifierManager = context.ModifierManager;
            if (mediator == null || modifierManager == null || context.ActionsQueue == null) {
                LogError($"SummonCreatureActionExecutor: Missing dependencies in context for {creature.Name}", LogTag.Actions | LogTag.Creatures | LogTag.Initialization);
                return;
            }

            creature.SetOwner(owner); // Set owner BEFORE adding to battlefield/registering

            // A Gift needs an empty slot: given into a taken one, it fizzles and goes back to its giver's discard pile
            if (creature.IsGift && creature.DeckOwner != null && creature.DeckOwner != owner && slot.IsOccupied()) {
                Log($"Executor: {creature.Name} can't be given into {slot}: it's taken. Back to {creature.DeckOwner.Name}'s discard pile", LogTag.Actions | LogTag.Creatures);
                creature.DeckOwner.DiscardCard(creature);
                return;
            }

            // Summoning onto an occupied slot replaces its creature: it isn't killed, so no Last Will or other death effect;
            // its own Replaced effects happen instead (Golden Parachute), then it goes to its owner's discard pile, and what
            // it planned for the Day goes with it
            var replacedCreature = slot.OccupyingCreature;
            var handover = Abilities.StartHandover(replacedCreature, modifierManager); // Before it leaves: its ability works on the board
            if (replacedCreature != null) {
                Log($"Executor: {creature.Name} replaces {replacedCreature.Name}", LogTag.Actions | LogTag.Creatures);
                replacedCreature.HandleEffect(EffectTrigger.OnReplaced, context.ActionsQueue);
                context.ActionsQueue.CancelActionsOf(replacedCreature);
                modifierManager.UnregisterCreature(replacedCreature);
                (replacedCreature.Owner ?? owner).DiscardCard(replacedCreature);
            }

            slot.Place(creature);
            Log($"Summoned {creature.Name} to slot {slot.TargetId.ShortId()}", LogTag.Actions | LogTag.Creatures);
            mediator.NotifyBattlefieldStateChanged(slot.Owner ?? owner); // Views create the creature's card

            // Register after placement, then trigger OnPlay (a creature that isn't operative yet fires it later), then announce the summon
            modifierManager.RegisterCreature(creature);
            creature.EnterPlay(context.TurnClock.TurnNumber);
            Abilities.CompleteHandover(creature, handover, modifierManager);
            if (creature.ArrivesConcealed > 0) { // A new face: it stays unknown
                StatusRules.Land(context, creature, StatusEffectType.Concealed, creature.ArrivesConcealed, 0, context.TurnClock.TurnNumber);
                creature.ArrivesConcealed = 0;
            }
            Intel.OnEnteredPlay(creature, context.ModifierManager);
            if (creature.DeckOwner != null) Intel.MakeKnown(creature, creature.DeckOwner); // Its giver knows what they gave
            SlotEffectRules.OnEnter(creature, slot, context); // A Landmine goes off before the creature's entry effects
            Abilities.TryCopypasta(creature, mediator); // "When ready": only does something if it's ready now
            Log($"Executor: Triggering OnPlay effects for {creature.Name}", LogTag.Effects | LogTag.Actions);
            creature.HandleEffect(EffectTrigger.OnPlay, context.ActionsQueue);
            mediator.NotifyCreatureSummoned(creature, owner);
        }
    }
}
