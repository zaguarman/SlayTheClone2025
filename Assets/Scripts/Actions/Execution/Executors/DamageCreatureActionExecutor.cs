using static DebugLogger;
using static Enums;
using System;

/// <summary>
/// Executor for DamageCreatureAction
/// </summary>
public class DamageCreatureActionExecutor : IActionExecutor
{
    public void Execute(IGameAction action, ActionExecutionContext context)
    {
        if (!(action is DamageCreatureAction damageAction))
        {
            LogError($"DamageCreatureActionExecutor: Received incorrect action type {action?.GetType().Name}", LogTag.Actions);
            return;
        }

        // Get data from Action
        var target = damageAction.GetTarget();
        var damage = damageAction.GetDamage();
        var attacker = damageAction.GetAttacker();

        // Validate
        if (target == null || target.Health <= 0) return; // Check if target exists and is alive

        // Execute Logic
        if (target is Creature creature)
        {
            Log($"Executor: Intent to deal {damage} damage to {creature.Name} (Armor: {creature.CurrentArmorPool}) from {attacker?.Name ?? "source"}", LogTag.Actions | LogTag.Creatures | LogTag.Combat);

            int damageToArmor = Math.Min(damage, creature.CurrentArmorPool);
            int remainingDamage = damage - damageToArmor;

            if (damageToArmor > 0)
            {
                creature.ModifyArmorPool(-damageToArmor); // Apply damage to armor pool
                Log($"Executor: {damageToArmor} damage absorbed by armor. {creature.Name} armor remaining: {creature.CurrentArmorPool}", LogTag.Actions | LogTag.Effects | LogTag.Combat);
            }

            if (remainingDamage > 0 && creature.Health > 0)
            {
                Log($"Executor: Applying {remainingDamage} remaining damage to health.", LogTag.Actions | LogTag.Creatures | LogTag.Combat);
                creature.TakeHealthDamage(remainingDamage, attacker); // Apply remaining damage to health
            }

            Log($"Executed DamageCreatureAction via Executor for {target.Name}", LogTag.Actions | LogTag.Creatures | LogTag.Combat);
        }
        else
        {
            LogWarning($"DamageCreatureActionExecutor: Target {target.Name} is not a concrete Creature.", LogTag.Actions | LogTag.Creatures);
        }
    }
}
