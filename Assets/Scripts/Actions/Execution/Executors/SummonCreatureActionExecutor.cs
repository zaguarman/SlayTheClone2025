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

            // Picked out of the deck: it leaves the deck now (this very copy); drawn, played or discarded since, nothing to summon
            if (summonAction.PickedFromDeck) {
                if (!owner.Deck.GetDeckPreview().Contains(creature)) {
                    Log($"Executor: {creature.Name} left the deck since it was picked: nothing to summon", LogTag.Actions | LogTag.Creatures);
                    return;
                }
                owner.Deck.RemoveCard(creature);
            }

            creature.SetOwner(owner); // Set owner BEFORE adding to battlefield/registering

            // A Gift needs an empty slot: given into a taken one, it fizzles and goes back to its giver's discard pile
            if (creature.IsGift && creature.DeckOwner != null && creature.DeckOwner != owner && slot.IsOccupied()) {
                Log($"Executor: {creature.Name} can't be given into {slot}: it's taken. Back to {creature.DeckOwner.Name}'s discard pile", LogTag.Actions | LogTag.Creatures);
                creature.DeckOwner.DiscardCard(creature);
                return;
            }

            // An Irreplaceable creature there (it moved in after the summon was planned): the summon doesn't happen, and the
            // card goes back to its owner's hand (the discard pile if it's full)
            if (!Abilities.CanBeReplaced(slot.OccupyingCreature) && slot.OccupyingCreature != creature) {
                Log($"Executor: {slot.OccupyingCreature.Name} is Irreplaceable: {creature.Name} goes back", LogTag.Actions | LogTag.Creatures);
                owner.AddToHand(creature);
                if (!owner.Hand.Contains(creature)) owner.DiscardCard(creature);
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
            Relics.OnEnter(creature, context); // An adventure's relics: Coffee, Kevlar, Protein Shake
            // A card that comes back with lasting Health cuts as big as its Health (Generational Trauma, then Hit and Run or
            // Extraction) enters at 0: it dies at once (GDD 3.3), its Last Will included
            if (creature.Health <= 0) context.ActionsQueue.AddAction(new CheckLethalAction(creature));
            // A building going up (Ministry for Equality): Rooted while it deploys, so nothing moves it until it's ready
            if ((creature.SourceData as CreatureData)?.rootedWhileDeploying == true && creature.TurnsUntilOperative > 0) {
                StatusRules.Land(context, creature, StatusEffectType.Rooted, creature.TurnsUntilOperative, 0, context.TurnClock.TurnNumber);
            }
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
