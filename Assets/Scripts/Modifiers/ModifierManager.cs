using System;
using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class ModifierManager : IModifierManager {
        #region Fields & Properties
        private readonly IGameMediator mediator;
        private readonly IModifierFactory modifierFactory;
        private readonly ITurnManager turnManager;   // Added

        public IModifierFactory ModifierFactory => modifierFactory;

        private readonly Dictionary<string, List<IModifier>> activeModifiers = new Dictionary<string, List<IModifier>>();
        private readonly Dictionary<string, Creature> creatures = new Dictionary<string, Creature>();
        #endregion

        #region Constructor
        // Updated constructor to accept ITurnManager only (no longer needs IActionsQueue)
        public ModifierManager(IGameMediator mediator, IModifierFactory factory, ITurnManager turnManager) {
            this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            modifierFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.turnManager = turnManager ?? throw new ArgumentNullException(nameof(turnManager));

            Log("ModifierManager initialized.", LogTag.Initialization | LogTag.Effects);
        }
        #endregion

        #region Creature Registration
        public void RegisterCreature(Creature creature) {
            if (creature == null) return;
            if (!creatures.ContainsKey(creature.TargetId)) {
                creatures.Add(creature.TargetId, creature);
                activeModifiers[creature.TargetId] = new List<IModifier>();
                Log($"ModifierManager: Registered Creature '{creature.Name}' (TargetID: {creature.TargetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);
                RecalculateStats(creature);
            }
        }

        // Method signature now accepts ICreature
        public void UnregisterCreature(ICreature creature) {
            if (creature == null) return;

            // Use TargetId directly from ICreature
            if (creatures.Remove(creature.TargetId)) {
                Log($"ModifierManager: Unregistered Creature '{creature.Name}' (TargetID: {creature.TargetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);

                if (activeModifiers.TryGetValue(creature.TargetId, out var mods)) {
                    var modifiersToRemove = mods.ToList();
                    foreach (var mod in modifiersToRemove) {
                        // RemoveModifier takes object, so ICreature is fine here
                        RemoveModifier(creature, mod);
                    }
                    activeModifiers.Remove(creature.TargetId);
                }
            }
        }
        #endregion

        #region Modifier Management
        public IEnumerable<IModifier> GetActiveModifiersFor(Creature creature) {
            if (creature == null) return Enumerable.Empty<IModifier>();

            if (activeModifiers.TryGetValue(creature.TargetId, out var creatureMods)) {
                return creatureMods.AsReadOnly();
            }

            return Enumerable.Empty<IModifier>();
        }

        public void ApplyModifier(object target, IModifier modifier) {
            if (target == null || modifier == null) return;

            Creature creatureTarget = target as Creature;

            if (creatureTarget != null) {
                string targetId = creatureTarget.TargetId;
                if (!creatures.ContainsKey(targetId)) {
                    LogWarning($"ModifierManager: Cannot apply modifier '{modifier.Name}' to unregistered creature '{creatureTarget.Name}' (TargetID: {targetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);
                    return;
                }

                if (!activeModifiers.ContainsKey(targetId)) {
                    activeModifiers[targetId] = new List<IModifier>();
                    LogWarning($"ModifierManager: Modifier list was missing for creature '{creatureTarget.Name}', re-initialized.", LogTag.Effects | LogTag.Creatures);
                }

                if (!activeModifiers[targetId].Any(m => m.Id == modifier.Id)) {
                    activeModifiers[targetId].Add(modifier);
                    modifier.Apply(target, mediator);
                    Log($"ModifierManager: Applied modifier '{modifier.Name}' to {creatureTarget.Name} (TargetID: {targetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);
                    if (modifier is StatusEffectModifier status) {
                        mediator.NotifyCreatureStatusChanged(creatureTarget, status.EffectType, true);
                    }

                    if (AffectsStats(modifier)) {
                        RecalculateStats(creatureTarget);
                    }
                }
                else {
                    Log($"ModifierManager: Modifier '{modifier.Name}' (ID: {modifier.Id.ToString().ShortId()}) is already applied to {creatureTarget.Name}.", LogTag.Effects | LogTag.Creatures);
                }
            }
            else {
                LogWarning($"ModifierManager: Cannot apply modifier to target of type {target.GetType().Name}. Only Creature currently supported.", LogTag.Effects);
            }
        }

        public void RemoveModifier(object target, IModifier modifier) {
            if (target == null || modifier == null) return;

            Creature creatureTarget = target as Creature;

            if (creatureTarget != null) {
                string targetId = creatureTarget.TargetId;
                if (activeModifiers.TryGetValue(targetId, out var modifierList)) {
                    IModifier existingModifier = modifierList.FirstOrDefault(m => m.Id == modifier.Id);
                    if (existingModifier != null) {
                        bool removed = modifierList.Remove(existingModifier);
                        if (removed) {
                            existingModifier.Remove(target, mediator);
                            Log($"ModifierManager: Removed modifier '{existingModifier.Name}' from {creatureTarget.Name} (TargetID: {targetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);
                            if (existingModifier is StatusEffectModifier status) {
                                mediator.NotifyCreatureStatusChanged(creatureTarget, status.EffectType, false);
                            }

                            if (AffectsStats(existingModifier) && creatures.ContainsKey(targetId)) {
                                RecalculateStats(creatureTarget);
                            }
                        }
                    }
                    else {
                        LogWarning($"ModifierManager: Modifier '{modifier.Name}' (ID: {modifier.Id.ToString().ShortId()}) not found on {creatureTarget.Name}.", LogTag.Effects | LogTag.Creatures);
                    }
                }
                else {
                    LogWarning($"ModifierManager: No modifier list found for {creatureTarget.Name} when trying to remove '{modifier.Name}'.", LogTag.Effects | LogTag.Creatures);
                }
            }
            else {
                LogWarning($"ModifierManager: Cannot remove modifier from target of type {target.GetType().Name}. Only Creature currently supported.", LogTag.Effects);
            }
        }


        private static bool AffectsStats(IModifier modifier) {
            foreach (ModifiableStat stat in Enum.GetValues(typeof(ModifiableStat))) {
                if (modifier.TryGetStatModification(stat, out _, out _)) return true;
            }
            return false;
        }
        #endregion

        #region Status Effect Queries
        public bool HasModifier(Creature creature, Predicate<IModifier> predicate) {
            if (creature == null || predicate == null) return false;
            if (activeModifiers.TryGetValue(creature.TargetId, out var mods)) {
                return mods.Any(m => predicate(m));
            }
            return false;
        }

        public bool HasStatusEffect(Creature creature, StatusEffectType statusType) {
            if (creature == null || statusType == StatusEffectType.None) return false;

            if (activeModifiers.TryGetValue(creature.TargetId, out var mods)) {
                return mods.OfType<StatusEffectModifier>().Any(mod => mod.EffectType == statusType);
            }
            return false;
        }

        public bool AreActionsPrevented(Creature creature) {
            if (creature == null) return false;

            if (activeModifiers.TryGetValue(creature.TargetId, out var mods)) {
                foreach (var mod in mods.OfType<StatusEffectModifier>()) {
                    if (mod.PreventsActions()) {
                        Log($"Actions for {creature.Name} prevented by StatusEffect '{mod.Name}' ({mod.EffectType})", LogTag.Effects | LogTag.Creatures | LogTag.Combat);
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion

        #region Turn Processing

        // UPDATED: Process Start of Turn now accepts actionsQueue parameter
        public void ProcessStartOfTurn(int startingTurnNumber, IActionsQueue actionsQueue) {
            if (actionsQueue == null) {
                LogError("ActionsQueue is null in ProcessStartOfTurn!", LogTag.Effects | LogTag.Turns);
                return;
            }

            Log($"ModifierManager: Processing start of turn {startingTurnNumber}", LogTag.Effects | LogTag.Turns);

            // Iterate through all registered creatures
            // ToList: a creature can be unregistered while its effects run
            foreach (var creature in creatures.Values.ToList()) {
                if (creature == null || creature.IsDead) continue; // Skip dead creatures

                // Trigger StartOfTurn effects for the creature
                creature.HandleTurnBasedEffect(EffectTrigger.StartOfTurn, actionsQueue, this, turnManager);
            }
            Log($"ModifierManager: Start of turn {startingTurnNumber} processing complete.", LogTag.Effects | LogTag.Turns);
        }

        // UPDATED: Process End of Turn now accepts actionsQueue parameter
        public void ProcessEndOfTurn(int endedTurnNumber, IActionsQueue actionsQueue) {
            if (actionsQueue == null) {
                LogError("ActionsQueue is null in ProcessEndOfTurn!", LogTag.Effects | LogTag.Turns);
                return;
            }

            Log($"ModifierManager: Processing end of turn {endedTurnNumber}", LogTag.Effects | LogTag.Turns);
            int nextTurnNumber = endedTurnNumber + 1; // The turn that is about to start

            // Use ToList on dictionary keys to avoid modification during iteration issues
            foreach (var targetId in activeModifiers.Keys.ToList()) {
                if (!creatures.TryGetValue(targetId, out Creature creature) || creature.IsDead) {
                    continue;
                }

                var modifiers = activeModifiers[targetId];

                // Burn ticks for every turn it was active, including the one in which it expires
                foreach (var burn in modifiers.OfType<StatusEffectModifier>()
                             .Where(s => s.EffectType == StatusEffectType.Burned && s.Potency > 0)) {
                    Log($"{creature.Name} burns for {burn.Potency} (StatusEffect '{burn.Name}')", LogTag.Effects | LogTag.Turns);
                    actionsQueue.AddAction(new DamageCreatureAction(creature, burn.Potency, creature));
                }

                // Remove expired modifiers; RemoveModifier recalculates stats and notifies when needed
                var expiredModifiers = modifiers
                    .Where(m => m is ITimedModifier timedMod && timedMod.HasExpired(nextTurnNumber)) // Check against the upcoming turn
                    .ToList();
                foreach (var expiredMod in expiredModifiers) {
                    RemoveModifier(creature, expiredMod);
                }

                // Trigger EndOfTurn effects AFTER handling expirations for the turn that just ended
                Log($"Triggering EndOfTurn effects for {creature.Name} (End of Turn {endedTurnNumber})", LogTag.Effects | LogTag.Turns);
                creature.HandleTurnBasedEffect(EffectTrigger.EndOfTurn, actionsQueue, this, turnManager);
            }

            Log($"ModifierManager: End of turn {endedTurnNumber} processing complete.", LogTag.Effects | LogTag.Turns);
        }
        #endregion

        #region Stat Calculation
        public void RecalculateStats(Creature creature) {
            if (creature == null || !creatures.ContainsKey(creature.TargetId)) {
                return;
            }

            var mods = GetActiveModifiersFor(creature).ToList();

            int finalAttack = Math.Max(0, CalculateStat(creature.BaseAttack, mods, ModifiableStat.Attack));
            int finalMaxHealth = Math.Max(1, CalculateStat(creature.BaseHealth, mods, ModifiableStat.Health)); // Keep health >= 1
            int finalSpeed = Math.Max(0, CalculateStat(creature.BaseSpeed, mods, ModifiableStat.Speed));

            var before = (creature.Attack, creature.MaxHealth, creature.Speed, creature.Health);

            // Update creature's internal effective stats
            creature.UpdateEffectiveStats(finalAttack, finalMaxHealth, finalSpeed);

            Log($"ModifierManager: Stats recalculated for '{creature.Name}' - Attack: {finalAttack}, MaxHealth: {finalMaxHealth}, Speed: {finalSpeed}", LogTag.Effects | LogTag.Creatures);

            // Only announce real changes; recalculating on registration or on a status expiring changes nothing
            if (before != (creature.Attack, creature.MaxHealth, creature.Speed, creature.Health)) {
                mediator?.NotifyCreatureStatsChanged(creature);
            }
        }

        // Flat modifiers are added first, then percentage modifiers multiply the result
        private static int CalculateStat(int baseValue, List<IModifier> mods, ModifiableStat stat) {
            int flatTotal = 0;
            float multiplier = 1.0f;
            foreach (var mod in mods) {
                if (!mod.TryGetStatModification(stat, out var calcType, out var value)) continue;

                if (calcType == ModifierCalculationType.Flat) flatTotal += value;
                else if (calcType == ModifierCalculationType.Percentage) multiplier *= 1.0f + (value / 100.0f);
            }
            return (int)Math.Round((baseValue + flatTotal) * multiplier);
        }
        #endregion

        #region Cleanup
        public void Cleanup() {
            // Clear internal collections
            activeModifiers.Clear();
            creatures.Clear();

            Log("ModifierManager cleaned up.", LogTag.Initialization | LogTag.Effects);
        }
        #endregion
    }
}
