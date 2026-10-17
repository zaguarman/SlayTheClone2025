using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
    public class DamagePlayerActionExecutor : ActionExecutor<DamagePlayerAction> {
        protected override void ExecuteAction(DamagePlayerAction damagePlayerAction, ActionExecutionContext context) {
            var targetPlayer = damagePlayerAction.GetTargetPlayer();
            var damage = damagePlayerAction.GetDamage();
            if (damage > 0 && Abilities.IsAmbushing(damagePlayerAction.GetAttacker(), context.ModifierManager)) damage *= 2;
            if (damage > 0 && Abilities.IsOnTheBrink(damagePlayerAction.GetAttacker())) damage *= 2;
            if (damage > 0 && Abilities.IsMartyred(damagePlayerAction.GetAttacker(), context.ModifierManager)) damage *= 2;

            if (targetPlayer == null) {
                LogError("DamagePlayerActionExecutor: Target player is null", LogTag.Actions | LogTag.Players);
                return;
            }
            if (damage <= 0) {
                Log($"DamagePlayerActionExecutor: no damage to {targetPlayer.Name} (an attacker brought to 0 Attack, say)", LogTag.Actions | LogTag.Players);
                return;
            }

            var mediator = context.GameMediator;
            if (mediator == null) {
                LogError("DamagePlayerActionExecutor: GameMediator is null in context.", LogTag.Actions | LogTag.Initialization);
                return;
            }

            Log($"Executor: Intent to deal {damage} damage to {targetPlayer.Name}", LogTag.Actions | LogTag.Players | LogTag.Combat);

            int actualDamage = targetPlayer.TakeDamage(damage);
            if (actualDamage > 0) {
                mediator.NotifyPlayerDamaged(targetPlayer, actualDamage); // Also ends the game when health reaches 0
            }

            Log($"Executed DamagePlayerAction via Executor for {targetPlayer.Name}", LogTag.Actions | LogTag.Players);
        }
    }
}
