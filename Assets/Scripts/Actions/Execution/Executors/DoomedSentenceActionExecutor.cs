using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class DoomedSentenceActionExecutor : ActionExecutor<DoomedSentenceAction> {
        protected override void ExecuteAction(DoomedSentenceAction sentence, ActionExecutionContext context) {
            var creature = sentence.GetCreature();
            if (creature == null || creature.IsDead || creature.Slot == null) return;

            // Last Rites: one final attack on the slot opposite it, with its current Attack, even if it Cannot Act
            var opposite = creature.Owner?.Opponent?.Battlefield;
            int index = creature.Slot.Index;
            if (opposite != null && index < opposite.Count) {
                Log($"{creature.Name}'s sentence is carried out: Last Rites on {opposite[index]}", LogTag.Creatures | LogTag.Combat | LogTag.Effects);
                context.ActionsQueue.AddAction(new BattlefieldCombatAction(creature, opposite[index], isLastRites: true));
            }

            // ...then it is destroyed, whatever its Health
            context.ActionsQueue.AddAction(new DestroyCreatureAction(creature, unconditional: true));
        }
    }
}
