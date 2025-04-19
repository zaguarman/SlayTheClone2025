using static DebugLogger;
using static Enums;
using System.Linq;
using System;
using System.Collections.Generic;

public interface ICreature : ICard {
    int Attack { get; } // Now returns effective attack
    int Health { get; } // Now returns current health
    int MaxHealth { get; } // New property for effective max health
    int BaseAttack { get; }
    int BaseHealth { get; }
    BattlefieldSlot Slot { get; set; }
    void TakeDamage(int damage);
    void TakeDamage(int damage, ICreature attacker); // Keep internal version accessible
    IPlayer Owner { get; }
    void SetOwner(IPlayer owner);
}

public class Creature : Card, ICreature {
    // Base stats (read-only after initialization)
    public int BaseAttack { get; private set; }
    public int BaseHealth { get; private set; }

    // Effective stats (calculated and set by ModifierManager)
    private int _effectiveAttack;
    private int _effectiveMaxHealth;

    // Current health tracking
    private int currentHealth;
    private bool isDead = false;

    // Public properties accessing calculated/current stats
    public int Attack => _effectiveAttack;
    public int MaxHealth => _effectiveMaxHealth;
    public int Health => currentHealth; // Current health is tracked separately

    public IPlayer Owner { get; private set; }
    public BattlefieldSlot Slot { get; set; }

    private ICreature lastAttacker; // Keep for OnDamage effect context

    // Constructor: Initializes base stats and sets effective stats initially
    public Creature(string name, int attack, int health) : base(name) {
        BaseAttack = attack;
        BaseHealth = health;
        currentHealth = health;
        _effectiveAttack = attack; // Initial effective stats match base stats
        _effectiveMaxHealth = health;
    }

    // Constructor with cardId parameter
    public Creature(string name, int attack, int health, string cardId) : base(name, cardId) {
        BaseAttack = attack;
        BaseHealth = health;
        currentHealth = health;
        _effectiveAttack = attack; // Initial effective stats match base stats
        _effectiveMaxHealth = health;
    }

    // Internal method for ModifierManager to update calculated stats
    internal void UpdateEffectiveStats(int newEffectiveAttack, int newEffectiveMaxHealth)
    {
        _effectiveAttack = Math.Max(0, newEffectiveAttack); // Ensure non-negative
        _effectiveMaxHealth = Math.Max(1, newEffectiveMaxHealth); // Ensure at least 1 max health

        // Clamp current health to the new max health
        currentHealth = Math.Min(currentHealth, _effectiveMaxHealth);

        // Note: We don't automatically heal to max here. Healing is a separate action/effect.
    }

    public void SetOwner(IPlayer owner) {
        Owner = owner;
    }

    public override void Play(IPlayer owner, ActionsQueue context, ITarget target = null) {
        Log($"Playing {Name} (TargetID: {TargetId.ToUpper()}) with {Effects.Count} effects", LogTag.Creatures | LogTag.Cards | LogTag.Actions);
        Owner = owner;

        // Determine if summoning from hand or deck (important for SummonCreatureAction)
        bool fromHand = owner?.Hand.Contains(this) ?? false;

        // Add SummonCreatureAction to the queue
        // The action itself will handle registration with ModifierManager
        context.AddAction(new SummonCreatureAction(this, owner, target, !fromHand)); // fromDeck = !fromHand
    }

    public void TakeDamage(int damage) {
        TakeDamage(damage, null);
    }

    public void TakeDamage(int damage, ICreature attacker) {
        if (isDead || damage <= 0) return;

        lastAttacker = attacker; // Store attacker for potential OnDamage effects
        int previousHealth = currentHealth;
        currentHealth = Math.Max(0, currentHealth - damage);
        int actualDamage = previousHealth - currentHealth; // Calculate actual damage dealt

        // Log damage
        string attackerName = attacker != null ? $"{attacker.Name} (ID: {attacker.TargetId.ToUpper().Substring(0, 8)})" : "Source";
        Log($"{attackerName} dealt {actualDamage} damage to {Name} (ID: {TargetId.ToUpper().Substring(0, 8)}), health now: {Health}/{MaxHealth}. Has {Effects.Count} CardEffects.",
            LogTag.Creatures | LogTag.Combat);

        // Trigger built-in CardData effects (kept separate from Modifier system)
        var gameManager = GameManager.Instance;
        if (gameManager?.ActionsQueue != null) {
            // Log($"Processing OnDamage CardEffects for {Name} (TargetID: {TargetId.ToUpper()})", LogTag.Creatures | LogTag.Effects);
            HandleEffect(EffectTrigger.OnDamage, gameManager.ActionsQueue);
        }

        // Notify Mediator about damage (for UI, event modifiers, etc.)
        GameMediator.Instance?.NotifyCreatureDamaged(this, actualDamage);

        // Check for death AFTER notifying and handling effects
        if (Health <= 0 && !isDead) {
            Die();
        }

        lastAttacker = null; // Clear attacker context after processing
    }

    private void Die()
    {
        isDead = true;
        Log($"Creature died: {Name} (TargetID: {TargetId.ToUpper()})", LogTag.Creatures);

        // Trigger OnDeath CardEffects FIRST
        var gameManager = GameManager.Instance;
        if (gameManager?.ActionsQueue != null) {
            // Log($"Processing OnDeath CardEffects for {Name} (TargetID: {TargetId.ToUpper()})", LogTag.Creatures | LogTag.Effects);
            HandleEffect(EffectTrigger.OnDeath, gameManager.ActionsQueue);
        }

        // Notify Mediator about death SECOND (after OnDeath effects)
        GameMediator.Instance?.NotifyCreatureDied(this); // For UI, game state checks, etc.

        // Unregister from ModifierManager THIRD
        gameManager?.ModifierManager?.UnregisterCreature(this);

        // Remove from Owner's battlefield LAST (this might trigger UI updates)
        Owner?.RemoveFromBattlefield(this, false); // false = don't destroy, add to discard
    }

    // --- Updated HandleEffect ---
    public void HandleEffect(EffectTrigger trigger, ActionsQueue actionsQueue) {
        // Use GameManager.Instance to access systems if Owner might be null (e.g., OnDeath effects)
        var gameManager = GameManager.Instance;
        if (gameManager == null) {
            LogError($"Cannot handle effect {trigger} for {Name} - GameManager instance is null.", LogTag.Effects | LogTag.Creatures);
            return;
        }
        var modifierManager = gameManager.ModifierManager;
        var factory = modifierManager?._modifierFactory; // Access factory

        // Check if processed
        if (actionsQueue.IsEffectProcessed(TargetId, trigger)) {
            // Log($"Skipping already processed {trigger} effect for {Name} (TargetID: {TargetId.ToUpper()})", LogTag.Effects);
            return;
        }

        Log($"Handling {trigger} effect for {Name} (TargetID: {TargetId.ToUpper().Substring(0,8)}) with {Effects.Count} effects", LogTag.Creatures | LogTag.Effects);

        foreach (var effect in Effects.Where(e => e.trigger == trigger)) {
            Log($"-- Processing Effect: Trigger={effect.trigger}, Type={effect.effectType}, Actions={effect.actions.Count}", LogTag.Effects);
            foreach (var action in effect.actions) {
                 Log($"---- Action: Type={action.actionType}, Target={action.targetType}, Value={action.value}, Status={action.statusEffectToApply}", LogTag.Effects);

                // --- Refactored Logic ---
                switch (action.actionType) {
                    case ActionType.Damage:
                        // Immediate damage (OnPlay, OnDeath) - Queue action directly
                        ProcessDamageEffect(action, actionsQueue);
                        break;
                    case ActionType.Heal:
                        // Immediate heal (OnPlay, OnDeath) - Queue action directly
                        ProcessHealEffect(action, actionsQueue);
                        break;
                    case ActionType.Draw:
                        // Immediate draw - Queue action directly
                        ProcessDrawEffect(action, actionsQueue);
                        break;
                    case ActionType.Summon:
                        // Immediate summon - Queue action directly
                        ProcessSummonEffect(action, actionsQueue);
                        break;
                    case ActionType.Buff:
                        // Apply as a Modifier via ModifierManager
                        ProcessBuffModifier(action, modifierManager, factory);
                        break;
                    case ActionType.ApplyStatus:
                         // Apply as a Modifier via ModifierManager
                        ProcessApplyStatusModifier(action, modifierManager, factory);
                        break;
                    // Add cases for ActionType.Armor, ActionType.Stun etc.
                    // Armor might apply a temporary StatModifier for a "Defense" stat or reduce incoming damage via an event modifier.
                    // Stun might apply a StatusEffectModifier (e.g., Paralyzed).
                     case ActionType.Stun: // Example: Map Stun to Paralyzed Status
                        action.statusEffectToApply = StatusEffectType.Paralyzed;
                        action.statusDuration = action.value; // Use 'value' for duration
                        action.statusPotency = 0;
                        ProcessApplyStatusModifier(action, modifierManager, factory);
                        break;
                    case ActionType.Armor: // Example: Apply temporary Health buff (like temporary HP)
                         // For simplicity, treat Armor as a timed health buff for now.
                         // A more complex system could have a separate Armor stat or damage reduction modifier.
                         action.buffAttack = false;
                         action.buffHealth = true;
                         // Let's assume armor lasts 1 turn by default if not specified
                         int armorDuration = 1; // Could be part of EffectAction data later
                         ProcessBuffModifier(action, modifierManager, factory, ModifierCalculationType.Flat, armorDuration); // Pass duration
                        break;
                }
            }
        }

        actionsQueue.MarkEffectProcessed(TargetId, trigger); // Mark as processed *after* handling all actions for this trigger
        Log($"Marked {trigger} effect as processed for {Name} (TargetID: {TargetId.ToUpper().Substring(0,8)})", LogTag.Effects);
    }

    // --- Keep existing direct action queuing methods (Damage, Heal, Draw, Summon) ---
    // These are now primarily for immediate effects (OnPlay, OnDeath)
    private void ProcessDamageEffect(EffectAction action, ActionsQueue actionsQueue) {
         // Ensure Owner exists if targeting others
        if (Owner == null && action.targetType != TargetType.Self && lastAttacker == null) {
            LogError($"Damage Effect: Cannot target others for {Name} - Owner is null and not self/retaliation.", LogTag.Effects);
            return;
        }

        // Retaliation
        if (lastAttacker != null && action.targetType == TargetType.AllCreatures && Effects.Any(e => e.trigger == EffectTrigger.OnDamage)) {
            Log($"Queueing Retaliation DamageAction: Attacker={lastAttacker.Name}, Damage={action.value}", LogTag.Effects);
            actionsQueue.AddAction(new DamageCreatureAction(lastAttacker, action.value, this));
            return;
        }
         // Self Damage
        if (action.targetType == TargetType.Self) {
             Log($"Queueing Self DamageAction: Target={Name}, Damage={action.value}", LogTag.Effects);
            actionsQueue.AddAction(new DamageCreatureAction(this, action.value, this));
            return;
        }

        // Normal Targeting (Requires Owner)
        var targets = TargetingSystem.GetValidTargets(Owner, action.targetType, action.targetModifier);
         Log($"Damage Effect: Found {targets.Count} targets for {action.targetType}/{action.targetModifier}.", LogTag.Effects);
        foreach (var target in targets) {
            if (target is ICreature creatureTarget) {
                actionsQueue.AddAction(new DamageCreatureAction(creatureTarget, action.value, this));
            } else if (target is IPlayer playerTarget) {
                actionsQueue.AddAction(new DamagePlayerAction(playerTarget, action.value));
            }
        }
    }

    private void ProcessHealEffect(EffectAction action, ActionsQueue actionsQueue) {
        if (Owner == null && action.targetType != TargetType.Self) {
             LogError($"Heal Effect: Cannot target others for {Name} - Owner is null and not self.", LogTag.Effects);
            return;
        }

        if (action.targetType == TargetType.Self) {
             actionsQueue.AddAction(new HealCreatureAction(this, action.value));
             return;
        }

        var targets = TargetingSystem.GetValidTargets(Owner, action.targetType, action.targetModifier);
        Log($"Heal Effect: Found {targets.Count} targets for {action.targetType}/{action.targetModifier}.", LogTag.Effects);
        foreach (var target in targets) {
            if (target is ICreature creatureTarget) {
                actionsQueue.AddAction(new HealCreatureAction(creatureTarget, action.value));
            } else if (target is IPlayer playerTarget) {
                actionsQueue.AddAction(new HealPlayerAction(playerTarget, action.value));
            }
        }
    }

     private void ProcessDrawEffect(EffectAction action, ActionsQueue actionsQueue) {
         if (Owner == null) {
              LogError($"Draw Effect: Cannot process for {Name} - Owner is null.", LogTag.Effects);
             return;
         }
        IPlayer targetPlayer = action.targetType == TargetType.Enemy ? Owner.Opponent : Owner;
        if (targetPlayer != null) {
            actionsQueue.AddAction(new DrawCardsAction(targetPlayer, action.value));
        }
    }

    private void ProcessSummonEffect(EffectAction action, ActionsQueue actionsQueue) {
         if (Owner == null) {
             LogError($"Summon Effect: Cannot process for {Name} - Owner is null.", LogTag.Effects);
             return;
         }
         // Existing logic... (finding creatures, finding slots, queuing action)
         // ...
         Log($"Processing summon effect for {Name} (TargetID: {TargetId.ToUpper()}). TargetType: {action.targetType}, Value: {action.value}",
            LogTag.Creatures | LogTag.Actions);

        // Handle summoning based on targeting
        IPlayer targetPlayer = Owner;
        if (action.targetType == TargetType.Enemy) {
            targetPlayer = Owner.Opponent;
        } else if (action.targetType != TargetType.Player && action.targetType != TargetType.FriendlyCreatures) {
            LogWarning($"Unexpected target type for summon effect: {action.targetType}", LogTag.Effects);
            return;
        }

        // Get available creature cards from the deck
        var gameManager = GameManager.Instance;
        if (gameManager?.cardDealingService == null) return;

        var deckCards = gameManager.cardDealingService.GetDeckPreview(targetPlayer);
        var creaturesInDeck = deckCards.Where(c => c is ICreature).ToList();

        if (creaturesInDeck.Count == 0) {
            Log($"No creatures available in {(targetPlayer.IsPlayer1() ? "Player 1" : "Player 2")} (TargetID: {targetPlayer.TargetId.ToUpper()})'s deck to summon",
                LogTag.Creatures | LogTag.Cards);
            return;
        }

        // Find valid slots
        var validSlots = targetPlayer.Battlefield.Where(s => !s.IsOccupied()).ToList();
        if (validSlots.Count == 0) {
            Log($"No valid slots available for {(targetPlayer.IsPlayer1() ? "Player 1" : "Player 2")} (TargetID: {targetPlayer.TargetId.ToUpper()}) to summon creatures",
                LogTag.Creatures | LogTag.Cards);
            return;
        }

        // Determine how many creatures to summon (up to value, but limited by available slots/creatures)
        int countToSummon = Math.Min(action.value, Math.Min(validSlots.Count, creaturesInDeck.Count));
        var random = new System.Random();

        for (int i = 0; i < countToSummon; i++) {
            // Select a random creature from the deck
            int creatureIndex = random.Next(creaturesInDeck.Count);
            var creatureToSummon = creaturesInDeck[creatureIndex] as ICreature;
            if (creatureToSummon == null) continue;

            // Remove it from our local list to avoid duplicates
            creaturesInDeck.RemoveAt(creatureIndex);

            // Select a random valid slot
            int slotIndex = random.Next(validSlots.Count);
            var slot = validSlots[slotIndex];
            validSlots.RemoveAt(slotIndex);

            // Add summon action (with fromDeck=true since we're summoning from deck)
            actionsQueue.AddAction(new SummonCreatureAction(creatureToSummon, targetPlayer, slot, true));

             Log($"Queued summon effect for {creatureToSummon.Name} (TargetID: {creatureToSummon.TargetId.ToUpper()}) to slot (TargetID: {slot.TargetId.ToUpper()})",
                LogTag.Creatures | LogTag.Effects | LogTag.Actions);
        }
         // ...
    }

    // --- NEW: Methods to apply modifiers ---

    // Optional parameters for calcType and duration added for flexibility (e.g., Armor mapping)
    private void ProcessBuffModifier(EffectAction action, ModifierManager manager, IModifierFactory factory, ModifierCalculationType calcType = ModifierCalculationType.Flat, int? forcedDuration = null) {
        if (manager == null || factory == null) {
            LogError("Buff Modifier: ModifierManager or Factory is null.", LogTag.Effects | LogTag.Creatures);
            return;
        }
        if (Owner == null && action.targetType != TargetType.Self) {
             LogError($"Buff Modifier: Cannot target others for {Name} - Owner is null and not self.", LogTag.Effects);
            return;
        }

        bool buffAttack = action.buffAttack;
        bool buffHealth = action.buffHealth;
        int value = action.value;
        int duration = forcedDuration ?? 0; // Use forced duration if provided, else 0 (permanent)

         // Special duration logic (Example: Abyssal Cucumber) - This could be data-driven
        if (Name == "Abyssal Cucumber" && value == 2 && buffAttack && !buffHealth && forcedDuration == null) {
            duration = 2;
             Log($"Applying Abyssal Cucumber specific duration: {duration} turns", LogTag.Effects);
        }

         // Determine targets
         List<ITarget> targets;
         if (action.targetType == TargetType.Self) {
             targets = new List<ITarget> { this };
         } else {
             targets = TargetingSystem.GetValidTargets(Owner, action.targetType, action.targetModifier);
         }
          Log($"Buff Modifier: Found {targets.Count} targets for {action.targetType}/{action.targetModifier}.", LogTag.Effects);

         int currentTurn = GameManager.Instance.TurnManager.TurnNumber;

         foreach (var target in targets) {
             if (target is Creature creatureTarget) {
                Log($"Applying Buff Modifier to {creatureTarget.Name}: Attack={buffAttack}, Health={buffHealth}, Val={value}, Dur={duration}", LogTag.Effects);

                 // Apply Attack Buff
                 if (buffAttack && value != 0) {
                     string modName = $"Attack Buff ({calcType} {value})";
                     string modDesc = $"{(value >= 0 ? "+" : "")}{value} Attack";
                     IModifier attackMod = duration > 0
                         ? factory.CreateTimedStatModifier(modName, modDesc, ModifiableStat.Attack, calcType, value, duration, currentTurn)
                         : factory.CreateStatModifier(modName, modDesc, ModifiableStat.Attack, calcType, value);
                     manager.ApplyModifier(creatureTarget, attackMod);
                 }
                 // Apply Health Buff
                 if (buffHealth && value != 0) {
                     string modName = $"Health Buff ({calcType} {value})";
                     string modDesc = $"{(value >= 0 ? "+" : "")}{value} Max Health";
                     IModifier healthMod = duration > 0
                        ? factory.CreateTimedStatModifier(modName, modDesc, ModifiableStat.Health, calcType, value, duration, currentTurn)
                        : factory.CreateStatModifier(modName, modDesc, ModifiableStat.Health, calcType, value);
                     manager.ApplyModifier(creatureTarget, healthMod);
                 }
             }
         }
    }

    private void ProcessApplyStatusModifier(EffectAction action, ModifierManager manager, IModifierFactory factory) {
         if (manager == null || factory == null) {
             LogError("ApplyStatus Modifier: ModifierManager or Factory is null.", LogTag.Effects | LogTag.Creatures);
             return;
         }
         if (Owner == null && action.targetType != TargetType.Self) {
              LogError($"ApplyStatus Modifier: Cannot target others for {Name} - Owner is null and not self.", LogTag.Effects);
             return;
         }

         StatusEffectType statusType = action.statusEffectToApply;
         int duration = action.statusDuration;
         int potency = action.statusPotency;

         if (statusType == StatusEffectType.None || duration <= 0) {
             LogWarning($"ApplyStatus Modifier: Invalid status type ({statusType}) or duration ({duration}) for action.", LogTag.Effects);
             return;
         }

         // Determine targets
         List<ITarget> targets;
          if (action.targetType == TargetType.Self) {
             targets = new List<ITarget> { this };
         } else {
             targets = TargetingSystem.GetValidTargets(Owner, action.targetType, action.targetModifier);
         }
         Log($"ApplyStatus Modifier: Found {targets.Count} targets for {action.targetType}/{action.targetModifier}.", LogTag.Effects);

         int currentTurn = GameManager.Instance.TurnManager.TurnNumber;

         foreach (var target in targets) {
             if (target is Creature creatureTarget) {
                 Log($"Applying Status Modifier to {creatureTarget.Name}: Type={statusType}, Dur={duration}, Pot={potency}", LogTag.Effects);

                 string effectName = $"{statusType} Effect ({Name})"; // Include source name
                 string effectDescription = $"Applies {statusType} for {duration} turns (Potency: {potency})";

                 IModifier statusModifier = factory.CreateStatusEffectModifier(
                     effectName,
                     effectDescription,
                     statusType,
                     duration,
                     potency,
                     currentTurn
                 );
                 manager.ApplyModifier(creatureTarget, statusModifier);
             }
         }
    }
    // --- End New Method ---
}