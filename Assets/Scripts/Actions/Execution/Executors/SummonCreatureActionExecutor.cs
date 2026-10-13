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

            // Summoning onto an occupied slot replaces its creature (no death effects); it goes to its owner's discard pile
            var replacedCreature = slot.OccupyingCreature;
            var batonPass = Abilities.StartBatonPass(replacedCreature, modifierManager); // Before it leaves: its ability works on the board
            if (replacedCreature != null) {
                Log($"Executor: {creature.Name} replaces {replacedCreature.Name}", LogTag.Actions | LogTag.Creatures);
                modifierManager.UnregisterCreature(replacedCreature);
                (replacedCreature.Owner ?? owner).DiscardCard(replacedCreature);
            }

            slot.Place(creature);
            Log($"Summoned {creature.Name} to slot {slot.TargetId.ShortId()}", LogTag.Actions | LogTag.Creatures);
            mediator.NotifyBattlefieldStateChanged(slot.Owner ?? owner); // Views create the creature's card

            // Register after placement, then trigger OnPlay (a creature that isn't operative yet fires it later), then announce the summon
            modifierManager.RegisterCreature(creature);
            creature.EnterPlay(context.TurnClock.TurnNumber);
            Abilities.CompleteBatonPass(creature, batonPass, modifierManager);
            Intel.OnEnteredPlay(creature, context.ModifierManager);
            SlotEffectRules.OnEnter(creature, slot, context); // A Landmine goes off before the creature's entry effects
            Abilities.TryCopypasta(creature, mediator); // "When ready": only does something if it's ready now
            Log($"Executor: Triggering OnPlay effects for {creature.Name}", LogTag.Effects | LogTag.Actions);
            creature.HandleEffect(EffectTrigger.OnPlay, context.ActionsQueue);
            mediator.NotifyCreatureSummoned(creature, owner);
        }
    }
}
