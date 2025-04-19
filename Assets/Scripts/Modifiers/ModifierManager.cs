using System;
using System.Collections.Generic;
using System.Linq;
using static DebugLogger;
using static Enums; // Add this for StatusEffectType enum

public class ModifierManager
{
    #region Fields & Properties
    private readonly GameMediator _mediator;
    public readonly IModifierFactory _modifierFactory;

    // Tracks active modifiers: Key = TargetId of Creature (or Slot later), Value = List of modifiers
    private readonly Dictionary<string, List<IModifier>> _activeModifiers = new Dictionary<string, List<IModifier>>();

    // Tracks registered creatures for quick lookup
    private readonly Dictionary<string, Creature> _creatures = new Dictionary<string, Creature>();
    #endregion

    #region Constructor
    public ModifierManager(GameMediator mediator, IModifierFactory factory)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _modifierFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        _mediator.AddTurnEndedListener(ProcessEndOfTurn); // Subscribe manager itself to TurnEnded
        Log("ModifierManager initialized and subscribed to TurnEnded.", LogTag.Initialization | LogTag.Effects);
    }
    #endregion

    #region Creature Registration
    // Register a creature when it enters play
    public void RegisterCreature(Creature creature)
    {
        if (creature == null) return;
        if (!_creatures.ContainsKey(creature.TargetId))
        {
            _creatures.Add(creature.TargetId, creature);
            _activeModifiers[creature.TargetId] = new List<IModifier>(); // Initialize modifier list
            Log($"ModifierManager: Registered Creature '{creature.Name}' (TargetID: {creature.TargetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);
            RecalculateStats(creature); // Calculate initial stats
        }
    }

    // Unregister a creature when it leaves play (e.g., dies)
    public void UnregisterCreature(Creature creature)
    {
        if (creature == null) return;
        if (_creatures.Remove(creature.TargetId))
        {
            Log($"ModifierManager: Unregistered Creature '{creature.Name}' (TargetID: {creature.TargetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);
            // Remove any modifiers associated with this creature
            if (_activeModifiers.TryGetValue(creature.TargetId, out var mods))
            {
                // Important: Remove modifiers in reverse or copy list to avoid issues during iteration
                var modifiersToRemove = mods.ToList();
                foreach (var mod in modifiersToRemove)
                {
                    // Use the RemoveModifier logic, passing the creature instance itself as the target
                    RemoveModifier(creature, mod);
                }
                _activeModifiers.Remove(creature.TargetId); // Clean up dictionary entry
            }
        }
    }

    #endregion

    #region Modifier Management
    // Get all modifiers affecting a specific creature
    // TODO: Extend this later to include modifiers from the creature's slot
    public IEnumerable<IModifier> GetActiveModifiersFor(Creature creature)
    {
        if (creature == null) return Enumerable.Empty<IModifier>();

        if (_activeModifiers.TryGetValue(creature.TargetId, out var creatureMods))
        {
            return creatureMods.AsReadOnly(); // Return read-only view
        }

        return Enumerable.Empty<IModifier>();
    }

    // Apply a modifier to a target (currently only Creature supported)
    public void ApplyModifier(object target, IModifier modifier)
    {
        if (target == null || modifier == null) return;

        Creature creatureTarget = target as Creature;
        // TODO: Add support for BattlefieldSlot target later

        if (creatureTarget != null)
        {
            string targetId = creatureTarget.TargetId;
            if (!_creatures.ContainsKey(targetId))
            {
                LogWarning($"ModifierManager: Cannot apply modifier '{modifier.Name}' to unregistered creature '{creatureTarget.Name}' (TargetID: {targetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);
                return;
            }

            if (!_activeModifiers.ContainsKey(targetId)) // Should have been created on RegisterCreature
            {
                 _activeModifiers[targetId] = new List<IModifier>();
                 LogWarning($"ModifierManager: Modifier list was missing for creature '{creatureTarget.Name}', re-initialized.", LogTag.Effects | LogTag.Creatures);
            }

            // Avoid duplicate applications based on Modifier's unique ID
            if (!_activeModifiers[targetId].Any(m => m.Id == modifier.Id))
            {
                _activeModifiers[targetId].Add(modifier);
                modifier.Apply(target, _mediator); // Let the modifier perform its setup (e.g., subscribe)
                Log($"ModifierManager: Applied modifier '{modifier.Name}' to {creatureTarget.Name} (TargetID: {targetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);

                // Trigger recalculation if it's a stat modifier
                if (modifier.TryGetStatModification(ModifiableStat.Attack, out _, out _) || modifier.TryGetStatModification(ModifiableStat.Health, out _, out _))
                {
                    RecalculateStats(creatureTarget);
                }
            }
            else
            {
                Log($"ModifierManager: Modifier '{modifier.Name}' (ID: {modifier.Id.ToString().ToUpper().Substring(0,8)}) is already applied to {creatureTarget.Name}.", LogTag.Effects | LogTag.Creatures);
            }
        }
        else
        {
            LogWarning($"ModifierManager: Cannot apply modifier to target of type {target.GetType().Name}. Only Creature currently supported.", LogTag.Effects);
        }
    }

    // Remove a specific modifier instance from a target
    public void RemoveModifier(object target, IModifier modifier)
    {
        if (target == null || modifier == null) return;

        Creature creatureTarget = target as Creature;
        // TODO: Add support for BattlefieldSlot target later

        if (creatureTarget != null)
        {
             string targetId = creatureTarget.TargetId;
            if (_activeModifiers.TryGetValue(targetId, out var modifierList))
            {
                // Find the specific instance by ID
                IModifier existingModifier = modifierList.FirstOrDefault(m => m.Id == modifier.Id);
                if (existingModifier != null)
                {
                    bool removed = modifierList.Remove(existingModifier);
                    if (removed)
                    {
                        existingModifier.Remove(target, _mediator); // Let the modifier clean up (e.g., unsubscribe)
                        Log($"ModifierManager: Removed modifier '{existingModifier.Name}' from {creatureTarget.Name} (TargetID: {targetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);

                        // Trigger recalculation if it was a stat modifier
                        if (existingModifier.TryGetStatModification(ModifiableStat.Attack, out _, out _) || existingModifier.TryGetStatModification(ModifiableStat.Health, out _, out _))
                        {
                             // Ensure creature is still registered before recalculating
                            if (_creatures.ContainsKey(targetId))
                            {
                                RecalculateStats(creatureTarget);
                            }
                        }
                    }

                    if (modifierList.Count == 0)
                    {
                        // Optionally remove the entry if no modifiers remain, though RegisterCreature initializes it
                        // _activeModifiers.Remove(targetId);
                    }
                }
                else
                {
                    LogWarning($"ModifierManager: Modifier '{modifier.Name}' (ID: {modifier.Id.ToString().ToUpper().Substring(0,8)}) not found on {creatureTarget.Name}.", LogTag.Effects | LogTag.Creatures);
                }
            }
             else
             {
                LogWarning($"ModifierManager: No modifier list found for {creatureTarget.Name} when trying to remove '{modifier.Name}'.", LogTag.Effects | LogTag.Creatures);
             }
        }
         else
        {
            LogWarning($"ModifierManager: Cannot remove modifier from target of type {target.GetType().Name}. Only Creature currently supported.", LogTag.Effects);
        }
    }

    #endregion

    #region Status Effect Queries
    // Method to check if a creature has a specific modifier instance by ID
    public bool HasModifier(Creature creature, Guid modifierId)
    {
        if (creature == null) return false;
        if (_activeModifiers.TryGetValue(creature.TargetId, out var mods))
        {
            return mods.Any(m => m.Id == modifierId);
        }
        return false;
    }


    // --- New Helper Method to Query Status Effects ---
    public bool HasStatusEffect(Creature creature, StatusEffectType statusType)
    {
        if (creature == null || statusType == StatusEffectType.None) return false;

        if (_activeModifiers.TryGetValue(creature.TargetId, out var mods))
        {
            return mods.OfType<StatusEffectModifier>().Any(mod => mod.EffectType == statusType);
        }
        return false;
    }

    // --- New Helper Method to Query if Actions are Prevented ---
    public bool AreActionsPrevented(Creature creature)
    {
        if (creature == null) return false;

        if (_activeModifiers.TryGetValue(creature.TargetId, out var mods))
        {
            // Check all StatusEffectModifiers on the creature
            foreach (var mod in mods.OfType<StatusEffectModifier>())
            {
                if (mod.PreventsActions())
                {
                    Log($"Actions for {creature.Name} prevented by StatusEffect '{mod.Name}' ({mod.EffectType})", LogTag.Effects | LogTag.Creatures | LogTag.Combat);
                    return true;
                }
            }
        }
        return false;
    }
    // --- End Helper Methods ---
    #endregion

    #region Turn Processing
    // Process end of turn - now triggered by GameMediator event
    // Parameter is the turn number that just *ended*
    public void ProcessEndOfTurn(int endedTurnNumber)
    {
        Log($"ModifierManager: Processing end of turn {endedTurnNumber}", LogTag.Effects | LogTag.Turns);
        int nextTurnNumber = endedTurnNumber + 1; // The turn number we use for expiration checks

        // Create a dictionary to track which creatures need recalculation (if any status caused stat changes)
        Dictionary<string, Creature> creaturesToRecalculate = new Dictionary<string, Creature>();

        // Check all active modifiers for expiration using the *next* turn number
        foreach (var kvp in _activeModifiers.ToList())
        {
            string targetId = kvp.Key;
            var modifiers = kvp.Value;

            if (!_creatures.TryGetValue(targetId, out Creature creature))
                continue;

            var expiredModifiers = new List<IModifier>();
            foreach (var modifier in modifiers)
            {
                // Check if it's a timed modifier (Stat or Status) and has expired
                if (modifier is ITimedModifier timedMod && timedMod.HasExpired(nextTurnNumber)) // Check against next turn
                {
                    expiredModifiers.Add(modifier);
                    // Logging moved into HasExpired methods
                }
            }

            bool needsRecalculation = false;
            foreach (var expiredMod in expiredModifiers)
            {
                RemoveModifier(creature, expiredMod);
                // If the expired mod affected stats, mark for recalc
                if (expiredMod.TryGetStatModification(ModifiableStat.Attack, out _, out _) || expiredMod.TryGetStatModification(ModifiableStat.Health, out _, out _))
                {
                     needsRecalculation = true;
                }
            }

            if (needsRecalculation && !creaturesToRecalculate.ContainsKey(targetId))
            {
                creaturesToRecalculate.Add(targetId, creature);
            }
        }

        // Recalculate stats for affected creatures AFTER removing expired mods
        foreach (var creature in creaturesToRecalculate.Values)
        {
            RecalculateStats(creature);
        }

        Log($"ModifierManager: End of turn {endedTurnNumber} processing complete.", LogTag.Effects | LogTag.Turns);
    }

    #endregion

    #region Stat Calculation
    // Central Stat Calculation Logic
    public void RecalculateStats(Creature creature)
    {
        if (creature == null || !_creatures.ContainsKey(creature.TargetId))
        {
            // Don't log warning here, might be called after unregistration naturally
            // LogWarning($"ModifierManager: Cannot recalculate stats for null or unregistered creature.", LogTag.Effects | LogTag.Creatures);
            return;
        }

        // Log($"ModifierManager: Recalculating stats for '{creature.Name}' (TargetID: {creature.TargetId.ToUpper()})...", LogTag.Effects | LogTag.Creatures);
        var mods = GetActiveModifiersFor(creature).ToList(); // Get all relevant mods

        // --- Calculation Order: Base + Flat Mods, then apply Percentage Mods ---

        // 1. Calculate Flat Modifications
        int flatAttackMod = 0;
        int flatHealthMod = 0; // Affects Max Health

        foreach (var mod in mods)
        {
            if (mod.TryGetStatModification(ModifiableStat.Attack, out var calcType, out var value))
            {
                if (calcType == ModifierCalculationType.Flat) flatAttackMod += value;
            }
            if (mod.TryGetStatModification(ModifiableStat.Health, out calcType, out value))
            {
                if (calcType == ModifierCalculationType.Flat) flatHealthMod += value;
            }
        }

        int attackAfterFlat = creature.BaseAttack + flatAttackMod;
        int healthAfterFlat = creature.BaseHealth + flatHealthMod; // Max Health after flat mods

        // 2. Calculate Percentage Modifications
        // Apply percentages multiplicatively based on the value *after* flat mods
        // Percentage mods are stored as integers (e.g., 10 for 10%), convert to float multiplier (1.10)
        float attackMultiplier = 1.0f;
        float healthMultiplier = 1.0f; // Max Health multiplier

        foreach (var mod in mods)
        {
             if (mod.TryGetStatModification(ModifiableStat.Attack, out var calcType, out var value))
            {
                 if (calcType == ModifierCalculationType.Percentage)
                 {
                     attackMultiplier *= (1.0f + (value / 100.0f));
                 }
            }
            if (mod.TryGetStatModification(ModifiableStat.Health, out calcType, out value))
            {
                 if (calcType == ModifierCalculationType.Percentage)
                 {
                     healthMultiplier *= (1.0f + (value / 100.0f));
                 }
            }
        }

        // 3. Final Calculation
        int finalAttack = (int)Math.Round(attackAfterFlat * attackMultiplier);
        int finalMaxHealth = (int)Math.Round(healthAfterFlat * healthMultiplier);

        // Ensure stats don't go below reasonable minimums
        finalAttack = Math.Max(0, finalAttack); // Attack shouldn't be negative
        finalMaxHealth = Math.Max(1, finalMaxHealth); // Max Health should be at least 1

        // 4. Update the creature's effective stats
        creature.UpdateEffectiveStats(finalAttack, finalMaxHealth);

        // Log($"ModifierManager: Stats recalculated for '{creature.Name}' - Attack: {finalAttack}, MaxHealth: {finalMaxHealth}", LogTag.Effects | LogTag.Creatures);

        // Notify UI or other systems if needed (CreatureDamaged with 0 damage is a common pattern)
        _mediator?.NotifyCreatureDamaged(creature, 0);
    }
    #endregion

    #region Cleanup
    // Cleanup subscription on destroy
     public void Cleanup()
     {
        _mediator?.RemoveTurnEndedListener(ProcessEndOfTurn);
        Log("ModifierManager cleaned up TurnEnded subscription.", LogTag.Initialization | LogTag.Effects);
     }
     #endregion
}
