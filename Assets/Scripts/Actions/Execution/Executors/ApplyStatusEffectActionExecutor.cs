using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class ApplyStatusEffectActionExecutor : ActionExecutor<ApplyStatusEffectAction> {
        protected override void ExecuteAction(ApplyStatusEffectAction statusAction, ActionExecutionContext context) {
            var targetCreature = statusAction.GetTargetCreature();
            var statusType = statusAction.GetStatusType();

            if (targetCreature == null || statusType == StatusEffectType.None) {
                LogWarning("ApplyStatusEffectActionExecutor: Target creature null or status type is None.", LogTag.Actions | LogTag.Effects);
                return;
            }

            // Return to Sender: an enemy status bounces back to the creature that sent it, once; one from a spell (or a creature
            // no longer in play) to a random creature on the caster's side
            var controller = statusAction.Controller;
            bool enemy = controller != null && targetCreature.Owner != null && controller != targetCreature.Owner;
            if (enemy && Abilities.Defends(targetCreature, CreatureAbility.ReturnToSender, statusAction.Source)) {
                var bounceTo = Abilities.BounceTarget(statusAction.Source, controller, atRandom: true);
                Log($"{targetCreature.Name}'s Return to Sender sends {statusType} back to {bounceTo?.Name ?? "nobody (it fails)"}", LogTag.Effects);
                if (bounceTo == null) return;
                targetCreature = bounceTo;
            }

            var landed = StatusRules.Land(context, targetCreature, statusType, statusAction.GetDuration(), statusAction.GetPotency(),
                context.TurnClock.TurnNumber);
            if (landed != null) {
                Log($"Executed ApplyStatusEffectAction via Executor: Applied {statusType} to {targetCreature.Name}", LogTag.Actions | LogTag.Effects);
            }
        }
    }
}
