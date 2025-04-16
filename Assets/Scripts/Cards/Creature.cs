using static DebugLogger;
using Enums; 
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public interface ICreature : ICard {
    int Attack { get; }
    int Health { get; }
    int BaseAttack { get; }
    int BaseHealth { get; }
    BattlefieldSlot Slot { get; set; }
    void TakeDamage(int damage);
    IPlayer Owner { get; }
    void SetOwner(IPlayer owner);
}

// Implement IModifiable and potentially IModifierSource
public class Creature : Card, ICreature, IModifiable, IModifierSource {
    // Base stats (never change after initialization)
    public int BaseAttack { get; private set; }
    public int BaseHealth { get; private set; }

    // Modifier Controller
    public ModifierController ModifierController { get; private set; }

    // Current calculated stats (calculated properties)
    public int Attack => CalculateStat(StatType.Attack);
    public int Health => CalculateStat(StatType.Health); // Represents CURRENT health clamped by MaxHealth
    public int MaxHealth => CalculateStat(StatType.MaxHealth); // Represents calculated MAX health

    private int currentHealth; // Tracks damage taken below BaseHealth + MaxHealth modifiers
    private bool isDead = false;
    public IPlayer Owner { get; private set; }
    public BattlefieldSlot Slot { get; set; }

    private ICreature lastAttacker;

    // Constructor updated to initialize ModifierController
    public Creature(string name, int attack, int health, string cardId) : base(name, cardId) {
        BaseAttack = attack;
        BaseHealth = health;
        currentHealth = health; // Start at full base health
        // Assuming GameMediator is accessible, e.g., via Singleton
        ModifierController = new ModifierController(this, GameMediator.Instance);
        Log($"Creature {Name} ({TargetId}) created. BaseStats: {BaseAttack}/{BaseHealth}. MC Initialized.", LogTag.Initialization | LogTag.Creatures);
    }

    // Keep old constructor for compatibility, but call the main one
    public Creature(string name, int attack, int health) : this(name, attack, health, null) { }

    // --- IModifiable Implementation ---
    public string GetModifiableId() => TargetId;

    public int GetBaseStat(StatType statType) {
        switch (statType) {
            case StatType.Attack: return BaseAttack;
            case StatType.Health: return BaseHealth; // Base value for current health is max base
            case StatType.MaxHealth: return BaseHealth; // Base value for max health calculation
            default:
                 LogWarning($"[{TargetId}] GetBaseStat called for unhandled StatType: {statType}", LogTag.Creatures);
                 return 0;
        }
    }

    public int GetCurrentStat(StatType statType) {
        // This method now directly calls the calculation logic
        return CalculateStat(statType);
    }
    // --- End IModifiable ---

    // --- IModifierSource Implementation ---
    public string GetSourceName() => Name;
    public string GetSourceId() => TargetId; // Instance ID for creature on field
    // --- End IModifierSource ---


    private int CalculateStat(StatType statType) {
        // Use MaxHealth as base for current Health calculation if needed, but generally,
        // currentHealth field tracks damage, and the property clamps it.
        int baseValue = GetBaseStat(statType);

        // --- Stat Calculation ---
        float flatBonus = 0;
        // float percentMultiplier = 1.0f; // Add if using percentage mods

        // Apply self modifiers
        flatBonus += ModifierController.GetTotalStatAdjustment(statType, StatAdjustmentType.Flat);
        // percentMultiplier += ModifierController.GetTotalStatAdjustment(statType, StatAdjustmentType.Percentage);

        // Apply slot modifiers (check if slot exists and is modifiable)
        if (Slot is IModifiable slotModifiable && slotModifiable.ModifierController != null) {
            flatBonus += slotModifiable.ModifierController.GetTotalStatAdjustment(statType, StatAdjustmentType.Flat);
            // percentMultiplier += slotModifiable.ModifierController.GetTotalStatAdjustment(statType, StatAdjustmentType.Percentage);
        } else if (Slot != null && !(Slot is IModifiable)) {
             // This case should ideally not happen if slots are always IModifiable
             LogWarning($"Slot {Slot.TargetId} is not IModifiable, cannot apply slot modifiers.", LogTag.Effects);
        }

        // Calculate final value: (Base * Percent) + Flat
        // int calculatedValue = Mathf.RoundToInt((baseValue * percentMultiplier) + flatBonus);
        int calculatedValue = Mathf.RoundToInt(baseValue + flatBonus); // Simplified for Flat only

        // --- Stat Clamping ---
        if (statType == StatType.Health) {
            // For current health, clamp between 0 and the calculated MaxHealth
            int calculatedMaxHealth = CalculateStat(StatType.MaxHealth); // Recursively calculate MaxHealth
            return Mathf.Clamp(currentHealth, 0, calculatedMaxHealth);
        } else if (statType == StatType.MaxHealth) {
            // Max health should be at least 1
             return Mathf.Max(1, calculatedValue);
        } else {
             // Attack and other stats should be non-negative
             return Mathf.Max(0, calculatedValue);
        }
    }

    // Modifier methods have been removed in favor of the ModifierController system

    public void SetOwner(IPlayer owner) {
        Owner = owner;
    }

    public override void Play(IPlayer owner, ActionsQueue context, ITarget target = null) {
        Log($"Playing {Name} (TargetID: {TargetId.ToUpper()}) with {Effects.Count} effects", LogTag.Creatures | LogTag.Cards | LogTag.Actions);
        Owner = owner;

        // Check if the creature is in the player's hand to determine if we need to remove it from the deck
        bool fromHand = owner.Hand.Contains(this);

        context.AddAction(new SummonCreatureAction(this, owner, target, fromHand));
    }

    public void TakeDamage(int damage) {
        TakeDamage(damage, null);
    }

    internal void TakeDamage(int damage, ICreature attacker) {
        if (isDead) return;

        lastAttacker = attacker; // Store attacker for effects like retaliate

        // --- Apply Damage Reduction/Increase Modifiers (Future Enhancement) ---
        // Example: float damageReduction = ModifierController.GetTotalStatAdjustment(StatType.DamageReduction, ...);
        // int actualDamage = Mathf.Max(0, damage - (int)damageReduction);
        int actualDamage = damage; // Keep it simple for now
        // --- End Damage Modifiers ---

        if(actualDamage <= 0) { // No damage dealt after modifiers
            lastAttacker = null; // Clear attacker if no damage taken
            return;
        }

        int healthBeforeDamage = Health; // Get calculated health before applying damage
        currentHealth -= actualDamage; // Reduce the tracker

        // Log damage dealt
        string attackerName = attacker != null ? $"{attacker.Name}({attacker.TargetId})" : "direct source";
        Log($"{attackerName} dealt {actualDamage} damage to {Name}({TargetId}). Health: {Health}/{MaxHealth}", LogTag.Creatures | LogTag.Combat); // Use calculated Health/MaxHealth properties

        // Trigger OnDamage effects (if any)
        var gameManager = GameManager.Instance;
        if (gameManager?.ActionsQueue != null) {
            HandleEffect(EffectTrigger.OnDamage, gameManager.ActionsQueue);
        }

        // Notify Mediator about the damage AFTER applying it
        GameMediator.Instance?.NotifyCreatureDamaged(this, actualDamage);

        // Check for death AFTER notifying about damage
        if (Health <= 0 && !isDead) {
            Die();
        }

        lastAttacker = null; // Clear attacker after processing
    }

    // Centralized death logic
    private void Die() {
        isDead = true;
        Log($"Creature died: {Name} ({TargetId})", LogTag.Creatures);

        // Trigger OnDeath effects BEFORE removing from battlefield or notifying Mediator
        var gameManager = GameManager.Instance;
        if (gameManager?.ActionsQueue != null) {
            HandleEffect(EffectTrigger.OnDeath, gameManager.ActionsQueue);
            // Note: Actions queued here will resolve later in the queue sequence
        }

        // Notify Mediator AFTER triggering effects
        GameMediator.Instance?.NotifyCreatureDied(this);

        // Remove from battlefield (adds to discard)
        if (Owner != null) {
            Owner.RemoveFromBattlefield(this, false); // Add to discard, don't destroy GameObject yet
        } else {
            LogWarning($"[{TargetId}] Creature died but has no Owner. Cannot remove from battlefield or add to discard.", LogTag.Creatures);
        }

        // Cleanup modifier controller as the final step
        ModifierController?.Cleanup();
    }

    // Method for healing
    public void Heal(int amount) {
        if (isDead || amount <= 0) return;

        int calculatedMaxHealth = MaxHealth; // Use property which includes modifiers
        int healthBeforeHeal = Health;     // Use property for current health

        // Heal up to the current calculated maximum health
        currentHealth = Mathf.Min(currentHealth + amount, calculatedMaxHealth);

        // Check if health actually changed (using the Health property)
        if (Health > healthBeforeHeal) {
            int healedAmount = Health - healthBeforeHeal;
            Log($"{Name} healed for {healedAmount}. Health: {Health}/{calculatedMaxHealth}", LogTag.Creatures | LogTag.Effects);
            // Notify game state changed to update UI
            GameMediator.Instance?.NotifyGameStateChanged();
        }
    }

    public void HandleEffect(EffectTrigger trigger, ActionsQueue actionsQueue) {
        // Skip if this effect was already handled in current resolution chain
        if (actionsQueue.IsEffectProcessed(TargetId, trigger)) {
            // Log($"Skipping already processed {trigger} effect for {Name} ({TargetId})", LogTag.Effects);
            return;
        }

        // Find effects matching the trigger
        var effectsToTrigger = Effects?.Where(e => e.trigger == trigger).ToList();
        if (effectsToTrigger == null || effectsToTrigger.Count == 0) {
            return; // No matching effects
        }

        Log($"Handling {trigger} effect for {Name} ({TargetId}) with {effectsToTrigger.Count} effect(s)", LogTag.Creatures | LogTag.Effects);

        foreach (var effect in effectsToTrigger) {
            Log($"-- Processing Effect: Trigger={effect.trigger}, Actions={effect.actions.Count}", LogTag.Effects);
            foreach (var action in effect.actions) {
                Log($"---- Processing Action: Type={action.actionType}, Value={action.value}, Target={action.targetType}, Modifier={action.targetModifier}", LogTag.Effects);
                // Delegate action processing based on type
                ProcessActionFromEffect(action, actionsQueue);
            }
        }

        // Mark this trigger as processed for this creature in this resolution chain
        actionsQueue.MarkEffectProcessed(TargetId, trigger);
    }

    // Helper method to queue actions based on EffectAction data
    private void ProcessActionFromEffect(EffectAction action, ActionsQueue actionsQueue) {
        // --- Target Resolution (based on effect action's targetType and Owner) ---
        List<ITarget> resolvedTargets = new List<ITarget>();
        bool requiresOwner = !(action.targetType == TargetType.Self || (action.targetType == TargetType.AllCreatures && action.actionType == ActionType.Damage && lastAttacker != null));

        if (requiresOwner && Owner == null) {
             LogError($"[{TargetId}] Cannot process effect action {action.actionType} targeting {action.targetType} - Owner is null.", LogTag.Effects);
             return;
        }

        // Get potential targets based on the action's target type
        resolvedTargets = TargetingSystem.GetValidTargets(Owner, action.targetType, action.targetModifier);

        // Special case for retaliation (target the last attacker)
        if (action.targetType == TargetType.AllCreatures && action.actionType == ActionType.Damage && lastAttacker != null) {
             resolvedTargets.Clear();
             resolvedTargets.Add(lastAttacker);
             Log($"[{TargetId}] Effect targets last attacker: {lastAttacker.Name}", LogTag.Effects);
        }
        // Special case for Self target
        else if (action.targetType == TargetType.Self) {
             resolvedTargets.Clear();
             resolvedTargets.Add(this);
        }
        // --- End Target Resolution ---

        if (resolvedTargets.Count == 0 && action.targetType != TargetType.Player && action.targetType != TargetType.Enemy) {
            // Don't warn if targeting players specifically, as they might be the intended target anyway
            // LogWarning($"[{TargetId}] No valid targets found for effect action {action.actionType} targeting {action.targetType}.", LogTag.Effects);
            // return; // Allow actions like Draw Player even if no creatures match
        }


        // --- Queue Game Actions ---
        foreach (var target in resolvedTargets) {
            QueueGameActionForTarget(action, target, actionsQueue);
        }

        // Handle actions targeting Players directly if targetType indicates Player/Enemy
         if (action.targetType == TargetType.Player && Owner != null) {
             QueueGameActionForTarget(action, Owner, actionsQueue);
         } else if (action.targetType == TargetType.Enemy && Owner?.Opponent != null) {
             QueueGameActionForTarget(action, Owner.Opponent, actionsQueue);
         }
        // --- End Queue Game Actions ---
    }

    // Queues the appropriate IGameAction based on the EffectAction and resolved target
    private void QueueGameActionForTarget(EffectAction effectAction, ITarget target, ActionsQueue actionsQueue) {
         if (target == null) return; // Should not happen if resolvedTargets filters nulls

         switch (effectAction.actionType) {
             case ActionType.Damage:
                 if (target is ICreature damageCreatureTarget) {
                     // Use 'this' (the creature executing the effect) as the source
                     actionsQueue.AddAction(new DirectDamageAction(damageCreatureTarget, effectAction.value, this));
                 } else if (target is IPlayer damagePlayerTarget) {
                     actionsQueue.AddAction(new DamagePlayerAction(damagePlayerTarget, effectAction.value));
                 }
                 break;

             case ActionType.Heal:
                 if (target is ICreature healCreatureTarget) {
                     actionsQueue.AddAction(new HealCreatureAction(healCreatureTarget, effectAction.value));
                 } else if (target is IPlayer healPlayerTarget) {
                     actionsQueue.AddAction(new HealPlayerAction(healPlayerTarget, effectAction.value));
                 }
                 break;

             case ActionType.Draw:
                 if (target is IPlayer drawPlayerTarget) {
                     actionsQueue.AddAction(new DrawCardsAction(drawPlayerTarget, effectAction.value));
                 }
                 break;

             case ActionType.ApplyModifier:
                 if (effectAction.modifierToApply != null) {
                      // Pass 'this' (the creature) as the IModifierSource
                     actionsQueue.AddAction(new ApplyModifierAction(target, effectAction.modifierToApply, this));
                 } else {
                     LogError($"[{TargetId}] ApplyModifier effect action is missing ModifierData reference.", LogTag.Effects);
                 }
                 break;

              case ActionType.RemoveModifier:
                 // Need a way to specify which modifier to remove in EffectAction
                 // e.g., add a string field 'modifierIdToRemove' to EffectAction
                 // string modId = effectAction.modifierIdToRemove;
                 // if (!string.IsNullOrEmpty(modId)) {
                 //    actionsQueue.AddAction(new RemoveModifierAction(target, modId));
                 // } else {
                 //    LogError($"[{TargetId}] RemoveModifier effect action is missing modifierIdToRemove.", LogTag.Effects);
                 // }
                 LogWarning("RemoveModifier action type from effects not fully implemented yet.", LogTag.Effects);
                 break;

             case ActionType.Summon:
                // Summon logic needs refinement. Does it summon specific creatures? From where?
                LogWarning("Summon action type from effects not fully implemented yet.", LogTag.Effects);
                // ProcessSummonEffect(effectAction, actionsQueue); // Call old logic if needed
                break;

             case ActionType.Buff: // Legacy Buff action
                // Buff action type is deprecated, use ApplyModifier instead
                LogWarning($"[{TargetId}] Buff action type is deprecated. Use ApplyModifier instead.", LogTag.Effects);
                break;

             // Add cases for other ActionTypes like Stun, Armor, etc.
             default:
                  LogWarning($"[{TargetId}] Unhandled action type in ProcessActionFromEffect: {effectAction.actionType}", LogTag.Effects);
                  break;
         }
    }
}