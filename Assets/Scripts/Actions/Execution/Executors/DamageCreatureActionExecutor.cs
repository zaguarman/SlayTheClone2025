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

        if (damageAction.IsCombatDamage() && context.IsCombatDamageCancelled(attacker)) {
            Log($"Executor: {attacker.Name} was killed by faster damage; its attack on {targetCreature.Name} is cancelled.", LogTag.Actions | LogTag.Combat);
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

        // Armor is spent immediately so the next hit in the same resolution sees what is left
        if (damageToArmor > 0)
        {
            targetCreature.ModifyArmorPool(-damageToArmor);
            mediator.NotifyCreatureArmorChanged(targetCreature, targetCreature.CurrentArmorPool);
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
            // Set the attacker in the context before triggering effects
            context.TriggeringAttacker = attacker;
            targetCreature.HandleEffect(EffectTrigger.OnDamage, context); // Pass context
            // Reset attacker in context (good practice)
            context.TriggeringAttacker = null;
        }

        // Check for Death AFTER applying damage and triggering OnDamage effects
        if (targetCreature.Health <= 0 && !targetCreature.IsDead)
        {
            Log($"Executor: {targetCreature.Name} died as a result of damage.", LogTag.Creatures | LogTag.Combat | LogTag.Actions);
            targetCreature.MarkAsDead(); // Mark internally first
            // Same speed the queue used to order this action (see ActionsQueue.GetActionSpeed)
            context.KillingSpeedByCreatureId[targetCreature.TargetId] = attacker?.Speed ?? 1;

            // Notify Mediator
            mediator.NotifyCreatureDied(targetCreature);

            // Trigger OnDeath Effects
            Log($"Executor: Triggering OnDeath effects for {targetCreature.Name}", LogTag.Effects | LogTag.Actions);
            // Set the attacker in the context before triggering effects (could be relevant for some OnDeath)
            context.TriggeringAttacker = attacker;
            targetCreature.HandleEffect(EffectTrigger.OnDeath, context); // Pass context
            // Reset attacker in context
            context.TriggeringAttacker = null;

            // Unregister from ModifierManager - NO CAST NEEDED
            modifierManager.UnregisterCreature(targetCreature);

            targetCreature.Owner?.DiscardCard(targetCreature);
        }

        Log($"Executed DamageCreatureAction via Executor for {targetCreature.Name}", LogTag.Actions | LogTag.Creatures | LogTag.Combat);
    }
}
