using static DebugLogger;
using static Enums;

/// <summary>
/// Executor for DamagePlayerAction
/// </summary>
public class DamagePlayerActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is DamagePlayerAction damagePlayerAction))
        {
             LogError($"DamagePlayerActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // --- Get data from Action ---
        var target = damagePlayerAction.GetTargetPlayer();
        var damage = damagePlayerAction.GetDamage();

        // --- Validate ---
        if (target == null)
        {
            LogError("DamagePlayerActionExecutor: Target player is null.", LogTag.Actions | LogTag.Players);
            return;
        }

        // --- Execute Logic (moved from DamagePlayerAction.Execute) ---
        target.TakeDamage(damage);
        Log($"Executed DamagePlayerAction via Executor for Player {(target.IsPlayer1() ? "1" : "2")}, Damage: {damage}", LogTag.Actions | LogTag.Players);
    }
}
