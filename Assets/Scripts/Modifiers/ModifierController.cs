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
            LogError($"[{ownerId}] Attempted to add null modifier or modifier with null data.", LogTag.Effects);
            return false;
        }

        var existingModifier = activeModifiers.FirstOrDefault(m => m.Data.modifierId == modifierToAdd.Data.modifierId);

        if (existingModifier != null) {
            switch (modifierToAdd.Data.stackingType) {
                case ModifierStackingType.NonStackable:
                    Log($"[{ownerId}] Modifier {modifierToAdd.Data.modifierId} is NonStackable and already exists. Ignoring.", LogTag.Effects);
                    return false; // Cannot add if already exists

                case ModifierStackingType.RefreshDuration:
                    existingModifier.RemainingDuration = modifierToAdd.Data.baseDuration;
                    existingModifier.Source = modifierToAdd.Source; // Update source if refreshed
                    Log($"[{ownerId}] Refreshed duration for modifier {modifierToAdd.Data.modifierId}.", LogTag.Effects);
                    mediator?.NotifyModifierApplied(existingModifier); // Notify update
                    return true;

                case ModifierStackingType.Stackable:
                    if (existingModifier.CurrentStacks < modifierToAdd.Data.maxStacks) {
                        existingModifier.CurrentStacks++;
                        // Always refresh duration when adding a stack
                        existingModifier.RemainingDuration = modifierToAdd.Data.baseDuration;
                        existingModifier.Source = modifierToAdd.Source; // Update source
                        Log($"[{ownerId}] Incremented stacks for modifier {modifierToAdd.Data.modifierId} to {existingModifier.CurrentStacks}. Duration refreshed.", LogTag.Effects);
                        mediator?.NotifyModifierApplied(existingModifier); // Notify update
                        return true;
                    } else {
                        // Option: Refresh duration even if max stacks are reached? Yes, usually expected.
                        existingModifier.RemainingDuration = modifierToAdd.Data.baseDuration;
                        existingModifier.Source = modifierToAdd.Source;
                        Log($"[{ownerId}] Modifier {modifierToAdd.Data.modifierId} is already at max stacks ({modifierToAdd.Data.maxStacks}). Refreshed duration.", LogTag.Effects);
                        mediator?.NotifyModifierApplied(existingModifier); // Notify update even if only duration changed
                        return true; // Indicate an update happened (duration refresh)
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
        // Use Where + Sum LINQ methods for conciseness
        totalAdjustment = activeModifiers
            .SelectMany(mod => mod.Data.statAdjustments
                                  .Where(adj => adj.statType == statType && adj.adjustmentType == adjustmentType)
                                  .Select(adj => adj.value * mod.CurrentStacks) // Multiply adjustment by stacks
                       )
            .Sum();

        return totalAdjustment;
    }

    // Renamed from TickTurn to avoid confusion with TurnManager's Tick
    private void OnTurnEnded(int turnNumber) {
        // Don't process if the owner might already be destroyed/cleaned up
        if (owner == null) return;

        List<ActiveModifier> expiredModifiers = null; // Lazy initialization

        foreach (var modifier in activeModifiers) {
            // Tick only TurnBased modifiers
            if (modifier.TickTurn()) {
                // Log($"[{ownerId}] Ticked modifier {modifier.Data.modifierId}. Remaining Duration: {modifier.RemainingDuration}", LogTag.Effects | LogTag.Turns);
                if (modifier.IsExpired) {
                    if (expiredModifiers == null) expiredModifiers = new List<ActiveModifier>();
                    expiredModifiers.Add(modifier);
                }
            }
        }

        // Remove expired modifiers
        if (expiredModifiers != null) {
            foreach (var expired in expiredModifiers) {
                 Log($"[{ownerId}] Modifier {expired.Data.modifierId} expired.", LogTag.Effects | LogTag.Turns);
                 // Remove first, then notify
                 if(RemoveModifier(expired, false)) { // Use normal removal notification path
                    mediator?.NotifyModifierExpired(expired); // Specific event for expiration
                 }
            }
        }
    }
}
