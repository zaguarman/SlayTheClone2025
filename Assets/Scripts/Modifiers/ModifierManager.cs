using System;
using System.Collections.Generic;
using System.Linq;
using static DebugLogger;
using static Enums;

public class ModifierManager : IModifierManager
{
    #region Fields & Properties
    private readonly IGameMediator _mediator;
    private readonly IModifierFactory _modifierFactory;

    public IModifierFactory ModifierFactory => _modifierFactory;

    private readonly Dictionary<string, List<IModifier>> _activeModifiers = new Dictionary<string, List<IModifier>>();
    private readonly Dictionary<string, Creature> _creatures = new Dictionary<string, Creature>();
    #endregion

    #region Constructor
    public ModifierManager(IGameMediator mediator, IModifierFactory factory)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _modifierFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        _mediator.AddTurnEndedListener(ProcessEndOfTurn); // Subscribe manager itself to TurnEnded
        Log("ModifierManager initialized and subscribed to TurnEnded.", LogTag.Initialization | LogTag.Effects);
    }
    #endregion

    #region Creature Registration
    public void RegisterCreature(Creature creature)
    {
        if (creature == null) return;
        if (!_creatures.ContainsKey(creature.TargetId))
        {
            _creatures.Add(creature.TargetId, creature);
            _activeModifiers[creature.TargetId] = new List<IModifier>();
            Log($"ModifierManager: Registered Creature '{creature.Name}' (TargetID: {creature.TargetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);
            RecalculateStats(creature);
        }
    }

    public void UnregisterCreature(Creature creature)
    {
        if (creature == null) return;
        if (_creatures.Remove(creature.TargetId))
        {
            Log($"ModifierManager: Unregistered Creature '{creature.Name}' (TargetID: {creature.TargetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);

            if (_activeModifiers.TryGetValue(creature.TargetId, out var mods))
            {
                var modifiersToRemove = mods.ToList();
                foreach (var mod in modifiersToRemove)
                {
                    RemoveModifier(creature, mod);
                }
                _activeModifiers.Remove(creature.TargetId);
            }
        }
    }
    #endregion

    #region Modifier Management
    public IEnumerable<IModifier> GetActiveModifiersFor(Creature creature)
    {
        if (creature == null) return Enumerable.Empty<IModifier>();

        if (_activeModifiers.TryGetValue(creature.TargetId, out var creatureMods))
        {
            return creatureMods.AsReadOnly();
        }

        return Enumerable.Empty<IModifier>();
    }

    public void ApplyModifier(object target, IModifier modifier)
    {
        if (target == null || modifier == null) return;

        Creature creatureTarget = target as Creature;

        if (creatureTarget != null)
        {
            string targetId = creatureTarget.TargetId;
            if (!_creatures.ContainsKey(targetId))
            {
                LogWarning($"ModifierManager: Cannot apply modifier '{modifier.Name}' to unregistered creature '{creatureTarget.Name}' (TargetID: {targetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);
                return;
            }

            if (!_activeModifiers.ContainsKey(targetId))
            {
                 _activeModifiers[targetId] = new List<IModifier>();
                 LogWarning($"ModifierManager: Modifier list was missing for creature '{creatureTarget.Name}', re-initialized.", LogTag.Effects | LogTag.Creatures);
            }

            if (!_activeModifiers[targetId].Any(m => m.Id == modifier.Id))
            {
                _activeModifiers[targetId].Add(modifier);
                modifier.Apply(target, _mediator);
                Log($"ModifierManager: Applied modifier '{modifier.Name}' to {creatureTarget.Name} (TargetID: {targetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);

                if (modifier.TryGetStatModification(ModifiableStat.Attack, out _, out _) ||
                    modifier.TryGetStatModification(ModifiableStat.Health, out _, out _) ||
                    modifier.TryGetStatModification(ModifiableStat.Speed, out _, out _) ||
                    false)
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

    public void RemoveModifier(object target, IModifier modifier)
    {
        if (target == null || modifier == null) return;

        Creature creatureTarget = target as Creature;

        if (creatureTarget != null)
        {
             string targetId = creatureTarget.TargetId;
            if (_activeModifiers.TryGetValue(targetId, out var modifierList))
            {
                IModifier existingModifier = modifierList.FirstOrDefault(m => m.Id == modifier.Id);
                if (existingModifier != null)
                {
                    bool removed = modifierList.Remove(existingModifier);
                    if (removed)
                    {
                        existingModifier.Remove(target, _mediator);
                        Log($"ModifierManager: Removed modifier '{existingModifier.Name}' from {creatureTarget.Name} (TargetID: {targetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);

                        if (existingModifier.TryGetStatModification(ModifiableStat.Attack, out _, out _) ||
                            existingModifier.TryGetStatModification(ModifiableStat.Health, out _, out _) ||
                            existingModifier.TryGetStatModification(ModifiableStat.Speed, out _, out _) ||
                            false)
                        {
                            if (_creatures.ContainsKey(targetId))
                            {
                                RecalculateStats(creatureTarget);
                            }
                        }
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
    public bool HasModifier(Creature creature, Guid modifierId)
    {
        if (creature == null) return false;
        if (_activeModifiers.TryGetValue(creature.TargetId, out var mods))
        {
            return mods.Any(m => m.Id == modifierId);
        }
        return false;
    }

    public bool HasModifier(Creature creature, Predicate<IModifier> predicate)
    {
        if (creature == null || predicate == null) return false;
        if (_activeModifiers.TryGetValue(creature.TargetId, out var mods))
        {
            return mods.Any(m => predicate(m));
        }
        return false;
    }

    public bool HasStatusEffect(Creature creature, StatusEffectType statusType)
    {
        if (creature == null || statusType == StatusEffectType.None) return false;

        if (_activeModifiers.TryGetValue(creature.TargetId, out var mods))
        {
            return mods.OfType<StatusEffectModifier>().Any(mod => mod.EffectType == statusType);
        }
        return false;
    }

    public bool AreActionsPrevented(Creature creature)
    {
        if (creature == null) return false;

        if (_activeModifiers.TryGetValue(creature.TargetId, out var mods))
        {
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
    #endregion

    #region Turn Processing
    public void ProcessEndOfTurn(int endedTurnNumber)
    {
        Log($"ModifierManager: Processing end of turn {endedTurnNumber}", LogTag.Effects | LogTag.Turns);
        int nextTurnNumber = endedTurnNumber + 1;

        Dictionary<string, Creature> creaturesToRecalculate = new Dictionary<string, Creature>();

        foreach (var kvp in _activeModifiers.ToList())
        {
            string targetId = kvp.Key;
            var modifiers = kvp.Value;

            if (!_creatures.TryGetValue(targetId, out Creature creature))
                continue;

            var expiredModifiers = new List<IModifier>();
            foreach (var modifier in modifiers)
            {
                if (modifier is ITimedModifier timedMod && timedMod.HasExpired(nextTurnNumber))
                {
                    expiredModifiers.Add(modifier);
                }
            }

            bool needsRecalculation = false;
            foreach (var expiredMod in expiredModifiers)
            {
                RemoveModifier(creature, expiredMod);
                if (expiredMod.TryGetStatModification(ModifiableStat.Attack, out _, out _) ||
                    expiredMod.TryGetStatModification(ModifiableStat.Health, out _, out _) ||
                    expiredMod.TryGetStatModification(ModifiableStat.Speed, out _, out _) ||
                    false)
                {
                     needsRecalculation = true;
                }
            }

            if (needsRecalculation && !creaturesToRecalculate.ContainsKey(targetId))
            {
                creaturesToRecalculate.Add(targetId, creature);
            }
        }

        foreach (var creature in creaturesToRecalculate.Values)
        {
            RecalculateStats(creature);
        }

        Log($"ModifierManager: End of turn {endedTurnNumber} processing complete.", LogTag.Effects | LogTag.Turns);
    }
    #endregion

    #region Stat Calculation
    public void RecalculateStats(Creature creature)
    {
        if (creature == null || !_creatures.ContainsKey(creature.TargetId)) {
            return;
        }

        var mods = GetActiveModifiersFor(creature).ToList();

        int flatAttackMod = 0;
        int flatHealthMod = 0;
        int flatSpeedMod = 0;

        foreach (var mod in mods) {
             if (mod.TryGetStatModification(ModifiableStat.Attack, out var calcType, out var value)) {
                 if (calcType == ModifierCalculationType.Flat) flatAttackMod += value;
             }
             if (mod.TryGetStatModification(ModifiableStat.Health, out calcType, out value)) {
                 if (calcType == ModifierCalculationType.Flat) flatHealthMod += value;
             }
             if (mod.TryGetStatModification(ModifiableStat.Speed, out calcType, out value)) {
                 if (calcType == ModifierCalculationType.Flat) flatSpeedMod += value;
             }
        }
        int attackAfterFlat = creature.BaseAttack + flatAttackMod;
        int healthAfterFlat = creature.BaseHealth + flatHealthMod;
        int speedAfterFlat = creature.BaseSpeed + flatSpeedMod;

        float attackMultiplier = 1.0f;
        float healthMultiplier = 1.0f;
        float speedMultiplier = 1.0f;

        foreach (var mod in mods) {
              if (mod.TryGetStatModification(ModifiableStat.Attack, out var calcType, out var value)) {
                  if (calcType == ModifierCalculationType.Percentage) attackMultiplier *= (1.0f + (value / 100.0f));
              }
              if (mod.TryGetStatModification(ModifiableStat.Health, out calcType, out value)) {
                  if (calcType == ModifierCalculationType.Percentage) healthMultiplier *= (1.0f + (value / 100.0f));
              }
              if (mod.TryGetStatModification(ModifiableStat.Speed, out calcType, out value)) {
                  if (calcType == ModifierCalculationType.Percentage) speedMultiplier *= (1.0f + (value / 100.0f));
              }
        }
        int finalAttack = (int)Math.Round(attackAfterFlat * attackMultiplier);
        int finalMaxHealth = (int)Math.Round(healthAfterFlat * healthMultiplier);
        int finalSpeed = (int)Math.Round(speedAfterFlat * speedMultiplier);

        finalAttack = Math.Max(0, finalAttack);
        finalMaxHealth = Math.Max(1, finalMaxHealth);
        finalSpeed = Math.Max(0, finalSpeed);

        creature.UpdateEffectiveStats(finalAttack, finalMaxHealth, finalSpeed);

        Log($"ModifierManager: Stats recalculated for '{creature.Name}' - Attack: {finalAttack}, MaxHealth: {finalMaxHealth}, Speed: {finalSpeed}", LogTag.Effects | LogTag.Creatures);
        _mediator?.NotifyCreatureDamaged(creature, 0);
    }
    #endregion

    #region Cleanup
    public void Cleanup()
    {
        _mediator?.RemoveTurnEndedListener(ProcessEndOfTurn);
        Log("ModifierManager cleaned up TurnEnded subscription.", LogTag.Initialization | LogTag.Effects);
    }
    #endregion
}
