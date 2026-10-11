using static SlayTheClone.DebugLogger;

namespace SlayTheClone {
    public class DamagePlayerActionExecutor : ActionExecutor<DamagePlayerAction> {
        protected override void ExecuteAction(DamagePlayerAction damagePlayerAction, ActionExecutionContext context) {
            var targetPlayer = damagePlayerAction.GetTargetPlayer();
            var damage = damagePlayerAction.GetDamage();

            if (targetPlayer == null) {
                LogError("DamagePlayerActionExecutor: Target player is null", LogTag.Actions | LogTag.Players);
                return;
            }
            if (damage <= 0) {
                LogWarning($"DamagePlayerActionExecutor: Damage amount ({damage}) is zero or negative. Skipping.", LogTag.Actions | LogTag.Players);
                return;
            }

            var attacker = damagePlayerAction.GetAttacker();
            if (damagePlayerAction.IsCombatDamage() && context.IsCombatDamageCancelled(attacker)) {
                Log($"Executor: {attacker.Name} was killed by faster damage; its attack on {targetPlayer.Name} is cancelled.", LogTag.Actions | LogTag.Combat);
                return;
            }

            if (context.GameMediator == null) {
                LogError("DamagePlayerActionExecutor: GameMediator is null in context.", LogTag.Actions | LogTag.Initialization);
                return;
            }

            Log($"Executor: Intent to deal {damage} damage to {targetPlayer.Name}", LogTag.Actions | LogTag.Players | LogTag.Combat);

            // Player.TakeDamage handles health reduction and notification via its OnDamaged event
            targetPlayer.TakeDamage(damage);

            Log($"Executed DamagePlayerAction via Executor for {targetPlayer.Name}", LogTag.Actions | LogTag.Players);
        }
    }
}
