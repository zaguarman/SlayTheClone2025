using static DebugLogger;
using static Enums;
using System.Linq; // Added for LINQ operations
using System; // Added for Math operations

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
            // Allow null targetSlot for cancellation purposes, but log if attacker is null
            if (attacker == null) {
                 LogError($"BattlefieldCombatActionExecutor: Attacker is null.", LogTag.Actions | LogTag.Combat);
                 return;
            }
            // If only targetSlot is null, it might be a cancellation, don't log error, just proceed quietly
             Log($"BattlefieldCombatActionExecutor: TargetSlot is null for attacker {attacker.Name}. Assuming cancellation or invalid target.", LogTag.Actions | LogTag.Combat);
             // We might want to explicitly handle cancellation differently later, but for now, let it pass
        }


        // Get Dependencies from Context
        var actionsQueue = context.ActionsQueue;
        if (actionsQueue == null)
        {
            LogError("BattlefieldCombatActionExecutor: ActionsQueue is null in context.", LogTag.Actions | LogTag.Combat);
            return;
        }

        // --- Execute Logic ---
        ITarget primaryTarget = null;
        int primaryDamage = attacker.Attack; // Store base attack damage

        // 1. Queue Primary Damage Action (Only if targetSlot is valid)
        if (targetSlot != null) // <<< Added null check
        {
            if (targetSlot.IsOccupied())
            {
                var targetCreature = targetSlot.OccupyingCreature;
                if (targetCreature != null)
                {
                    primaryTarget = targetCreature;
                    Log($"Executor: Creature {attacker.Name} attacking creature {targetCreature.Name}", LogTag.Combat);
                    actionsQueue.AddAction(new DamageCreatureAction(targetCreature, primaryDamage, attacker));
                }
            }
            else
            {
                var targetPlayer = attacker.Owner?.Opponent;
                if (targetPlayer != null)
                {
                    primaryTarget = targetPlayer;
                    Log($"Executor: Creature {attacker.Name} attacking player {(targetPlayer.IsPlayer1 ? "1" : "2")}", LogTag.Combat);
                    actionsQueue.AddAction(new DamagePlayerAction(targetPlayer, primaryDamage));
                }
            }
        } // <<< End of targetSlot null check

        // 2. Check Attacker's Effects (Spread, Chain, etc.) - Only if primary target was determined
        if (primaryTarget != null && attacker is Creature creatureAttacker)
        {
            // Check for effects *on the attacker* that trigger *when dealing damage*
            var onDamageEffects = creatureAttacker.Effects
                .Where(e => e.trigger == EffectTrigger.OnDamage || e.trigger == EffectTrigger.OnPlay) // Consider OnPlay for immediate effects on attack
                .SelectMany(e => e.actions)
                .ToList();

             // --- DEBUG LOG ---
             Log($"Executor: Attacker {attacker.Name} found {onDamageEffects.Count} OnDamage/OnPlay effect actions.", LogTag.Combat | LogTag.Effects);
             // --- END DEBUG LOG ---

            foreach (var effectAction in onDamageEffects)
            {
                 // --- DEBUG LOG ---
                 Log($"Executor: Checking effect action -> Type={effectAction.actionType}, Modifier={effectAction.targetModifier}, Value={effectAction.value}", LogTag.Combat | LogTag.Effects);
                 // --- END DEBUG LOG ---

                // --- Specific Chain Lightning Logic ---
                if (effectAction.targetModifier.HasFlag(TargetModifier.Chained))
                {
                     // --- DEBUG LOG ---
                     Log($"Executor: Entering Chain Logic for {attacker.Name}...", LogTag.Combat | LogTag.Effects);
                     // --- END DEBUG LOG ---

                    // Ensure the primary target is a creature for chaining
                    if (primaryTarget is ICreature hitCreature)
                    {
                        int chainDamage = effectAction.value > 0 ? effectAction.value : primaryDamage; // Use effect value or attack damage
                        Log($"Executor: Attacker {attacker.Name} applying CHAIN effect (Value: {chainDamage}) starting from {hitCreature.Name}", LogTag.Combat | LogTag.Effects);

                        // Get chain targets starting from the creature that was hit
                        var chainTargets = TargetingSystem.GetDirectionalChainTargets(hitCreature, 2); // Max 2 jumps

                        Log($"Executor: Found {chainTargets.Count} chain targets.", LogTag.Combat | LogTag.Effects);

                        // Queue damage actions for chain targets
                        int currentDamage = chainDamage;
                        foreach (var chainTarget in chainTargets)
                        {
                            if (currentDamage <= 0) break;

                            Log($"Executor: Queueing Chained DamageAction: Target={chainTarget.Name}, Damage={currentDamage}, Attacker={attacker.Name}", LogTag.Effects | LogTag.Combat | LogTag.Actions);
                            actionsQueue.AddAction(new DamageCreatureAction(chainTarget, currentDamage, attacker));
                            currentDamage /= 2; // Halve damage for the next jump
                        }
                    }
                    else
                    {
                        LogWarning($"Executor: Cannot apply Chained effect from {attacker.Name} because primary target was not a creature.", LogTag.Combat | LogTag.Effects);
                    }
                }
                // --- Handle other Spread Modifiers (excluding Chained) ---
                else if (effectAction.targetModifier != TargetModifier.None)
                {
                    // Use effect value if specified, otherwise use attacker's attack
                    int spreadDamage = effectAction.value > 0 ? effectAction.value : primaryDamage;
                    if (spreadDamage > 0) // Only apply if there's damage to spread
                    {
                         // --- DEBUG LOG ---
                         Log($"Executor: Entering Spread Logic for {attacker.Name} (Modifier: {effectAction.targetModifier})...", LogTag.Combat | LogTag.Effects);
                         // --- END DEBUG LOG ---
                        Log($"Executor: Attacker {attacker.Name} applying SPREAD effect (Modifier: {effectAction.targetModifier}, Value: {spreadDamage})", LogTag.Combat | LogTag.Effects);
                        // SpreadDamageEffect.ApplySpreadDamage(primaryTarget, spreadDamage, attacker, effectAction.targetModifier, actionsQueue);
                         // The above call might be redundant if GetAdditionalTargets handles everything. Let's test without it first.
                         // Re-enable if needed, or integrate the logic directly.
                         var additionalSpreadTargets = TargetingSystem.GetAdditionalTargets(attacker.Owner, primaryTarget, effectAction.targetModifier);
                         Log($"Executor: Found {additionalSpreadTargets.Count} spread targets for modifier {effectAction.targetModifier}.", LogTag.Combat | LogTag.Effects);
                         foreach(var spreadTarget in additionalSpreadTargets)
                         {
                             if(spreadTarget is ICreature creatureTarget)
                             {
                                 Log($"Executor: Queueing Spread DamageAction: Target={creatureTarget.Name}, Damage={spreadDamage}, Attacker={attacker.Name}", LogTag.Effects | LogTag.Combat | LogTag.Actions);
                                 actionsQueue.AddAction(new DamageCreatureAction(creatureTarget, spreadDamage, attacker));
                             }
                             else if (spreadTarget is IPlayer playerTarget)
                             {
                                 Log($"Executor: Queueing Spread DamagePlayerAction: Target={(playerTarget.IsPlayer1 ? "P1" : "P2")}, Damage={spreadDamage}, Attacker={attacker.Name}", LogTag.Effects | LogTag.Combat | LogTag.Actions);
                                 actionsQueue.AddAction(new DamagePlayerAction(playerTarget, spreadDamage));
                             }
                         }
                    }
                }
                // Effects without modifiers (like simple bonus damage) are not handled here,
                // they should be part of ModifyStat or other specific effect actions.
            }
        }

        Log($"Executed BattlefieldCombatAction via Executor for {attacker.Name}", LogTag.Actions | LogTag.Combat);
    }
}
