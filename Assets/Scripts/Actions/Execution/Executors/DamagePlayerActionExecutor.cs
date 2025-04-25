using static DebugLogger;
using static Enums;
using System;

/// <summary>
/// Executor for DamagePlayerAction
/// </summary>
public class DamagePlayerActionExecutor : IActionExecutor {
    public void Execute(IGameAction action, ActionExecutionContext context) {
        if (!(action is DamagePlayerAction damagePlayerAction)) {
            LogError($"DamagePlayerActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // --- Get data from Action ---
        var targetPlayer = damagePlayerAction.GetTargetPlayer();
        var damage = damagePlayerAction.GetDamage();

        // --- Validate ---
        if (targetPlayer == null) {
            LogError("DamagePlayerActionExecutor: Target player is null", LogTag.Actions | LogTag.Players);
            return;
        }
        if (damage <= 0) {
             LogWarning($"DamagePlayerActionExecutor: Damage amount ({damage}) is zero or negative. Skipping.", LogTag.Actions | LogTag.Players);
             return;
        }

        // --- Get Dependencies from Context (Optional, but good practice) ---
        var mediator = context.GameMediator;
        if (mediator == null) {
            LogError("DamagePlayerActionExecutor: GameMediator is null in context.", LogTag.Actions | LogTag.Initialization);
            return; // Cannot proceed if mediator is somehow null
        }

        // --- Execute Logic ---
        Log($"Executor: Intent to deal {damage} damage to {(targetPlayer.IsPlayer1 ? "Player 1" : "Player 2")}", LogTag.Actions | LogTag.Players | LogTag.Combat);

        // Player.TakeDamage handles health reduction and notification via its OnDamaged event
        targetPlayer.TakeDamage(damage);

        Log($"Executed DamagePlayerAction via Executor for {(targetPlayer.IsPlayer1 ? "P1" : "P2")}", LogTag.Actions | LogTag.Players);
    }
}
