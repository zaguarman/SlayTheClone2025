using static DebugLogger;
using static Enums;

/// <summary>
/// Executor for BattlefieldCombatAction
/// </summary>
public class BattlefieldCombatActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is BattlefieldCombatAction combatAction))
        {
            LogError($"BattlefieldCombatActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // Get data from Action
        var attacker = combatAction.GetAttacker();
        var targetSlot = combatAction.GetTargetSlot();

        // Validate
        if (attacker == null || targetSlot == null)
        {
            LogError($"BattlefieldCombatActionExecutor: Attacker or TargetSlot is null.", LogTag.Actions | LogTag.Combat);
            return;
        }

        // Get Dependencies from Context
        var actionsQueue = context.ActionsQueue;
        if (actionsQueue == null)
        {
            LogError("BattlefieldCombatActionExecutor: ActionsQueue is null in context.", LogTag.Actions | LogTag.Combat);
            return;
        }

        // Execute Logic
        ITarget primaryTarget = null;

        if (targetSlot.IsOccupied())
        {
            var targetCreature = targetSlot.OccupyingCreature;
            if (targetCreature != null)
            {
                primaryTarget = targetCreature;
                Log($"Executor: Creature {attacker.Name} attacking creature {targetCreature.Name}", LogTag.Combat);
                // Queue DamageCreatureAction
                actionsQueue.AddAction(new DamageCreatureAction(targetCreature, attacker.Attack, attacker));
            }
        }
        else
        {
            var targetPlayer = attacker.Owner?.Opponent;
            if (targetPlayer != null)
            {
                primaryTarget = targetPlayer;
                Log($"Executor: Creature {attacker.Name} attacking player {(targetPlayer.IsPlayer1 ? "1" : "2")}", LogTag.Combat);
                // Queue DamagePlayerAction
                actionsQueue.AddAction(new DamagePlayerAction(targetPlayer, attacker.Attack));
            }
        }

        // Check and apply spread damage (uses SpreadDamageEffect helper)
        if (primaryTarget != null && attacker is Creature creature)
        {
            foreach (var effect in creature.Effects)
            {
                foreach (var effectAction in effect.actions)
                {
                    if (effectAction.targetModifier != TargetModifier.None)
                    {
                        Log($"Executor: Applying spread damage effect (modifier {effectAction.targetModifier}) for {attacker.Name}", LogTag.Combat | LogTag.Effects);
                        // Pass queue from context
                        SpreadDamageEffect.ApplySpreadDamage(primaryTarget, attacker.Attack, attacker, effectAction.targetModifier, actionsQueue);
                    }
                }
            }
        }

        Log($"Executed BattlefieldCombatAction via Executor for {attacker.Name}", LogTag.Actions | LogTag.Combat);
    }
}
