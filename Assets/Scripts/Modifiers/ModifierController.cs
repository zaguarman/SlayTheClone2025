using System.Collections.Generic;
using System.Linq;
using Enums;
using static DebugLogger;

// This can be a MonoBehaviour attached to Creatures/Slots,
// or a plain C# class managed by them. Let's go with plain C# for flexibility.
public class ModifierController {
    private readonly IModifiable owner;
    private readonly GameMediator mediator;
    private readonly List<ActiveModifier> activeModifiers = new List<ActiveModifier>();
    private readonly string ownerId; // Cache owner ID for logging

    public IReadOnlyList<ActiveModifier> ActiveModifiers => activeModifiers.AsReadOnly();

    public ModifierController(IModifiable owner, GameMediator mediator) {
        this.owner = owner;
        this.mediator = mediator;
        this.ownerId = owner.GetModifiableId(); // Cache ID
        // Subscribe to turn tick event (using TurnEnded for now)
        mediator?.AddTurnEndedListener(OnTurnEnded);
        Log($"ModifierController initialized for {ownerId}", LogTag.Initialization | LogTag.Effects);
    }

    // Call this when the owner is destroyed or the controller is no longer needed
    public void Cleanup() {
        Log($"Cleaning up ModifierController for {ownerId}", LogTag.Effects);
        mediator?.RemoveTurnEndedListener(OnTurnEnded);
        // Clear modifiers and potentially notify UI if needed
        var modifiersToRemove = new List<ActiveModifier>(activeModifiers);
        foreach(var mod in modifiersToRemove) {
            RemoveModifier(mod, true); // Force remove without expiration event
        }
        activeModifiers.Clear();
    }

    public bool AddModifier(ActiveModifier modifierToAdd) {
        if (modifierToAdd == null || modifierToAdd.Data == null) {
            LogError($"[{ownerId}] Attempted to add null modifier or modifier with null definition.", LogTag.Effects);
            return false;
        }

        var data = modifierToAdd.Data; // Use the definition
        var existingModifier = activeModifiers.FirstOrDefault(m => m.Data.modifierId == data.modifierId);

        if (existingModifier != null) {
            // --- Use ModifierDefinition properties for logic ---
            if (data.IsNonStackable) // maxStacks == 0
            {
                Log($"[{ownerId}] Modifier {data.modifierId} is NonStackable and already exists. Ignoring.", LogTag.Effects);
                return false;
            }
            else if (data.IsRefreshOnly) // maxStacks == 1
            {
                if (existingModifier.Data.IsTurnBased) // Only refresh duration if it's turn-based
                {
                    existingModifier.RemainingDuration = data.duration;
                }
                existingModifier.Source = modifierToAdd.Source; // Update source
                Log($"[{ownerId}] Refreshed duration (if applicable) for modifier {data.modifierId}.", LogTag.Effects);
                mediator?.NotifyModifierApplied(existingModifier); // Notify update
                return true;
            }
            else // IsStackable (maxStacks > 1 or -1)
            {
                bool canAddStack = data.HasInfiniteStacks || existingModifier.CurrentStacks < data.maxStacks;

                if (canAddStack) {
                    existingModifier.CurrentStacks++;
                    if (existingModifier.Data.IsTurnBased) // Always refresh duration when adding a stack if turn-based
                    {
                        existingModifier.RemainingDuration = data.duration;
                    }
                    existingModifier.Source = modifierToAdd.Source; // Update source
                    Log($"[{ownerId}] Incremented stacks for modifier {data.modifierId} to {existingModifier.CurrentStacks}. Duration refreshed (if applicable).", LogTag.Effects);
                    mediator?.NotifyModifierApplied(existingModifier); // Notify update
                    return true;
                } else {
                    // Max stacks reached (and not infinite)
                    // Still refresh duration if it's turn-based
                    if (existingModifier.Data.IsTurnBased)
                    {
                        existingModifier.RemainingDuration = data.duration;
                    }
                    existingModifier.Source = modifierToAdd.Source; // Update source
                    Log($"[{ownerId}] Modifier {data.modifierId} is already at max stacks ({data.maxStacks}). Refreshed duration (if applicable).", LogTag.Effects);
                    mediator?.NotifyModifierApplied(existingModifier); // Notify update even if only duration changed
                    return true; // Indicate an update happened
                }
            }
        }

        // If no existing modifier, add the new one
        activeModifiers.Add(modifierToAdd);
        Log($"[{ownerId}] Applied new modifier {modifierToAdd.Data.modifierId} (Stacks: {modifierToAdd.CurrentStacks}, Duration: {modifierToAdd.RemainingDuration}).", LogTag.Effects);
        mediator?.NotifyModifierApplied(modifierToAdd);
        return true;
    }

    public bool RemoveModifier(string modifierIdToRemove) {
        var modifierToRemove = activeModifiers.FirstOrDefault(m => m.Data.modifierId == modifierIdToRemove);
        if (modifierToRemove != null) {
            return RemoveModifier(modifierToRemove, false); // Don't force, use normal removal notification
        }
        return false;
    }

    // Internal method to handle removal logic and notification
    // `forceRemoval` bypasses Expiration event, used during Cleanup
    private bool RemoveModifier(ActiveModifier modifierInstance, bool forceRemoval) {
        if (activeModifiers.Remove(modifierInstance)) {
            Log($"[{ownerId}] Removed modifier {modifierInstance.Data.modifierId}.", LogTag.Effects);
            if (!forceRemoval) {
                 mediator?.NotifyModifierRemoved(modifierInstance);
            }
            // Recalculate owner stats if necessary (often handled by events/UI updates)
            return true;
        }
        return false;
    }

    public float GetTotalStatAdjustment(StatType statType, StatAdjustmentType adjustmentType) {
        float totalAdjustment = 0;
        totalAdjustment = activeModifiers
            .SelectMany(mod => mod.Data.statAdjustments // Access definition data
                                  .Where(adj => adj.statType == statType && adj.adjustmentType == adjustmentType)
                                  .Select(adj => adj.value * mod.CurrentStacks) // Apply stacks
                       )
            .Sum();
        return totalAdjustment;
    }

    private void OnTurnEnded(int turnNumber) {
        if (owner == null) return;

        List<ActiveModifier> expiredModifiers = null;

        // Iterate backwards for safe removal if needed directly (though we collect first)
        for (int i = activeModifiers.Count - 1; i >= 0; i--)
        {
            var modifier = activeModifiers[i];
            if (modifier.TickTurn()) { // TickTurn now checks if it's turn-based internally
                if (modifier.IsExpired) {
                    if (expiredModifiers == null) expiredModifiers = new List<ActiveModifier>();
                    expiredModifiers.Add(modifier);
                }
            }
        }

        if (expiredModifiers != null) {
            foreach (var expired in expiredModifiers) {
                Log($"[{ownerId}] Modifier {expired.Data.modifierId} expired.", LogTag.Effects | LogTag.Turns);
                if (RemoveModifier(expired, false)) {
                    mediator?.NotifyModifierExpired(expired);
                }
            }
        }
    }
}
