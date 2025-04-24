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
        var targetCreature = damageAction.GetTarget();
        var damage = damageAction.GetDamage();
        var attacker = damageAction.GetAttacker(); // Can be null

        // Validate target
        if (targetCreature == null || targetCreature.IsDead) {
             // LogWarning($"DamageCreatureActionExecutor: Target {targetCreature?.Name ?? "null"} is null or already dead.", LogTag.Actions | LogTag.Creatures);
             return;
        }

        // Get Dependencies from Context
        var mediator = context.GameMediator;
        var modifierManager = context.ModifierManager;
        var actionsQueue = context.ActionsQueue; // Get queue for potential effects

        if (mediator == null || modifierManager == null || actionsQueue == null)
        {
             LogError($"DamageCreatureActionExecutor: Missing dependencies in context for {targetCreature.Name}.", LogTag.Actions | LogTag.Creatures | LogTag.Initialization);
            return;
        }

        // --- Execute Logic ---
        Log($"Executor: Intent to deal {damage} damage to {targetCreature.Name} (Armor: {targetCreature.CurrentArmorPool}) from {attacker?.Name ?? "source"}", LogTag.Actions | LogTag.Creatures | LogTag.Combat);

        int damageToArmor = Math.Min(damage, targetCreature.CurrentArmorPool);
        int remainingDamage = damage - damageToArmor;

        // Apply damage to armor pool (Use ModifyArmorAction for consistency and potential triggers)
        if (damageToArmor > 0)
        {
            actionsQueue.AddAction(new ModifyArmorAction(targetCreature, -damageToArmor));
            // Log($"Executor: Queued ModifyArmorAction: {-damageToArmor} damage to armor. {targetCreature.Name} armor remaining (before action resolves): {targetCreature.CurrentArmorPool}", LogTag.Actions | LogTag.Effects | LogTag.Combat);
        }

        // Apply remaining damage to health
        int actualHealthDamage = 0;
        if (remainingDamage > 0 && !targetCreature.IsDead) // Check IsDead again after armor reduction potentially resolves
        {
            Log($"Executor: Applying {remainingDamage} remaining damage to health.", LogTag.Actions | LogTag.Creatures | LogTag.Combat);
            actualHealthDamage = targetCreature.TakeHealthDamage(remainingDamage); // Call internal method
        }

        // Notify Mediator AFTER damage is applied
        if (actualHealthDamage > 0) {
             mediator.NotifyCreatureDamaged(targetCreature, actualHealthDamage);
        }

        // Trigger OnDamage Effects AFTER damage calculation and notification
        // Check IsDead before triggering OnDamage
        if (actualHealthDamage > 0 && !targetCreature.IsDead)
        {
            Log($"Executor: Triggering OnDamage effects for {targetCreature.Name}", LogTag.Effects | LogTag.Actions);
            targetCreature.HandleEffect(EffectTrigger.OnDamage, context); // Pass context
        }

        // Check for Death AFTER applying damage and triggering OnDamage effects
        if (targetCreature.Health <= 0 && !targetCreature.IsDead)
        {
            Log($"Executor: {targetCreature.Name} died as a result of damage.", LogTag.Creatures | LogTag.Combat | LogTag.Actions);
            targetCreature.MarkAsDead(); // Mark internally first

            // Notify Mediator
            mediator.NotifyCreatureDied(targetCreature);

            // Trigger OnDeath Effects
            Log($"Executor: Triggering OnDeath effects for {targetCreature.Name}", LogTag.Effects | LogTag.Actions);
            targetCreature.HandleEffect(EffectTrigger.OnDeath, context); // Pass context

            // Unregister from ModifierManager
            modifierManager.UnregisterCreature(targetCreature as Creature); // Requires concrete type

            // Remove from Battlefield (let Player handle this)
            targetCreature.Owner?.RemoveFromBattlefield(targetCreature, false); // destroyCard = false (already handled by Died event?) - Keep false for now
        }

        Log($"Executed DamageCreatureAction via Executor for {targetCreature.Name}", LogTag.Actions | LogTag.Creatures | LogTag.Combat);
    }
}
