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

        public IModifierFactory ModifierFactory => modifierFactory;

        private readonly Dictionary<string, List<IModifier>> activeModifiers = new Dictionary<string, List<IModifier>>();
        private readonly Dictionary<string, ICreature> creatures = new Dictionary<string, ICreature>();
        #endregion

        #region Constructor
        public ModifierManager(IGameMediator mediator, IModifierFactory factory) {
            this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            modifierFactory = factory ?? throw new ArgumentNullException(nameof(factory));

            Log("ModifierManager initialized.", LogTag.Initialization | LogTag.Effects);
        }
        #endregion

        #region Creature Registration
        public void RegisterCreature(ICreature creature) {
            if (creature == null) return;
            if (!creatures.ContainsKey(creature.TargetId)) {
                creatures.Add(creature.TargetId, creature);
                activeModifiers[creature.TargetId] = new List<IModifier>();
                Log($"ModifierManager: Registered Creature '{creature.Name}' (TargetID: {creature.TargetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);
                RecalculateStats(creature);
            }
        }

        public void UnregisterCreature(ICreature creature) {
            if (creature == null) return;

            if (creatures.Remove(creature.TargetId)) {
                Log($"ModifierManager: Unregistered Creature '{creature.Name}' (TargetID: {creature.TargetId.ToUpper()}).", LogTag.Effects | LogTag.Creatures);

                if (activeModifiers.TryGetValue(creature.TargetId, out var mods)) {
                    var modifiersToRemove = mods.ToList();
                    foreach (var mod in modifiersToRemove) {
                        RemoveModifier(creature, mod);
                    }
                    activeModifiers.Remove(creature.TargetId);
                }
            }
        }
        #endregion

        #region Modifier Management
        public IEnumerable<IModifier> GetActiveModifiersFor(ICreature creature) {
            if (creature == null) return Enumerable.Empty<IModifier>();

            if (activeModifiers.TryGetValue(creature.TargetId, out var creatureMods)) {
                return creatureMods.AsReadOnly();
            }

            return Enumerable.Empty<IModifier>();
        }

        public void ApplyModifier(ICreature creatureTarget, IModifier modifier) {
            if (creatureTarget == null || modifier == null) return;

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
                modifier.Apply(creatureTarget, mediator);
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

        public void RemoveModifier(ICreature creatureTarget, IModifier modifier) {
            if (creatureTarget == null || modifier == null) return;

            string targetId = creatureTarget.TargetId;
            if (activeModifiers.TryGetValue(targetId, out var modifierList)) {
                IModifier existingModifier = modifierList.FirstOrDefault(m => m.Id == modifier.Id);
                if (existingModifier != null) {
                    bool removed = modifierList.Remove(existingModifier);
                    if (removed) {
                        existingModifier.Remove(creatureTarget, mediator);
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


        private static bool AffectsStats(IModifier modifier) {
            foreach (ModifiableStat stat in Enum.GetValues(typeof(ModifiableStat))) {
                if (modifier.TryGetStatModification(stat, out _, out _)) return true;
            }
            return false;
        }
        #endregion

        #region Status Effect Queries
        public bool HasModifier(ICreature creature, Predicate<IModifier> predicate) {
            if (creature == null || predicate == null) return false;
            if (activeModifiers.TryGetValue(creature.TargetId, out var mods)) {
                return mods.Any(m => predicate(m));
            }
            return false;
        }

        public bool HasStatusEffect(ICreature creature, StatusEffectType statusType) {
            if (creature == null || statusType == StatusEffectType.None) return false;

            if (activeModifiers.TryGetValue(creature.TargetId, out var mods)) {
                return mods.OfType<StatusEffectModifier>().Any(mod => mod.EffectType == statusType);
            }
            return false;
        }

        public bool CanAct(ICreature creature) {
            if (creature == null || creature.IsDead) return false;

            // A creature that left the board (replaced, or killed and discarded) has nothing left to do this turn
            if (creature.Slot == null) {
                Log($"{creature.Name} can't act: it is no longer on the board.", LogTag.Creatures | LogTag.Actions);
                return false;
            }

            if (!creature.IsOperative) {
                Log($"{creature.Name} can't act yet: operative in {creature.TurnsUntilOperative} turn(s).", LogTag.Creatures | LogTag.Actions);
                return false;
            }

            var preventing = GetActiveModifiersFor(creature).OfType<StatusEffectModifier>().FirstOrDefault(s => s.PreventsActions());
            if (preventing != null) {
                Log($"{creature.Name} can't act: {preventing.EffectType} (StatusEffect '{preventing.Name}').", LogTag.Creatures | LogTag.Actions | LogTag.Effects);
                return false;
            }
            return true;
        }
        #endregion

        #region Turn Processing

        public void ProcessStartOfTurn(int startingTurnNumber, IActionsQueue actionsQueue) {
            if (actionsQueue == null) {
                LogError("ActionsQueue is null in ProcessStartOfTurn!", LogTag.Effects | LogTag.Turns);
                return;
            }

            Log($"ModifierManager: Processing start of turn {startingTurnNumber}", LogTag.Effects | LogTag.Turns);

            // ToList: a creature can be unregistered while its effects run
            foreach (var creature in creatures.Values.ToList()) {
                if (creature == null || creature.IsDead) continue;

                // Creatures get a turn closer to operative first, so one that just became operative takes part in this turn
                if (creature.AdvanceReadiness(startingTurnNumber, actionsQueue)) {
                    mediator.NotifyCreatureStatsChanged(creature);
                }
                creature.HandleEffect(EffectTrigger.StartOfTurn, actionsQueue);
            }
            Log($"ModifierManager: Start of turn {startingTurnNumber} processing complete.", LogTag.Effects | LogTag.Turns);
        }

        public void ProcessEndOfTurn(int endedTurnNumber, IActionsQueue actionsQueue) {
            if (actionsQueue == null) {
                LogError("ActionsQueue is null in ProcessEndOfTurn!", LogTag.Effects | LogTag.Turns);
                return;
            }

            Log($"ModifierManager: Processing end of turn {endedTurnNumber}", LogTag.Effects | LogTag.Turns);
            int nextTurnNumber = endedTurnNumber + 1; // The turn that is about to start

            // Use ToList on dictionary keys to avoid modification during iteration issues
            foreach (var targetId in activeModifiers.Keys.ToList()) {
                if (!creatures.TryGetValue(targetId, out ICreature creature) || creature.IsDead) {
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
                creature.HandleEffect(EffectTrigger.EndOfTurn, actionsQueue);
            }

            Log($"ModifierManager: End of turn {endedTurnNumber} processing complete.", LogTag.Effects | LogTag.Turns);
        }
        #endregion

        #region Stat Calculation
        public void RecalculateStats(ICreature creature) {
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
