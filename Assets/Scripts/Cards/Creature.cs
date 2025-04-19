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

    public void HandleEffect(EffectTrigger trigger, ActionsQueue actionsQueue) {
        Log($"Checking effects for {Name} (TargetID: {TargetId.ToUpper()}) with trigger: {trigger}", LogTag.Effects);
        Log($"Effects count: {Effects.Count}", LogTag.Effects);

        foreach (var effect in Effects) {
            Log($"Effect found: {effect.trigger}", LogTag.Effects);
        }

        // Skip if this effect was already handled in current resolution chain
        if (actionsQueue.IsEffectProcessed(TargetId, trigger)) {
            Log($"Skipping already processed {trigger} effect for {Name} (TargetID: {TargetId.ToUpper()})", LogTag.Effects);
            return;
        }

        Log($"Handling {trigger} effect for {Name} (TargetID: {TargetId.ToUpper()}) with {Effects.Count} effects", LogTag.Creatures | LogTag.Effects);

        foreach (var effect in Effects.Where(e => e.trigger == trigger)) {
            Log($"Processing effect - Trigger: {effect.trigger}, Actions: {effect.actions.Count}",
                LogTag.Creatures | LogTag.Effects);

            foreach (var action in effect.actions) {
                Log($"Processing action - Type: {action.actionType}, Value: {action.value}, Target: {action.targetType}",
                    LogTag.Creatures | LogTag.Effects);

                switch (action.actionType) {
                    case ActionType.Damage:
                        ProcessDamageEffect(action, actionsQueue);
                        break;
                    case ActionType.Heal:
                        ProcessHealEffect(action, actionsQueue);
                        break;
                    case ActionType.Draw:
                        ProcessDrawEffect(action, actionsQueue);
                        break;
                    case ActionType.Summon:
                        ProcessSummonEffect(action, actionsQueue);
                        break;
                    case ActionType.Buff:
                        ProcessBuffEffect(action, actionsQueue);
                        break;
                        // Additional cases can be added for other action types
                }
            }
        }

        actionsQueue.MarkEffectProcessed(TargetId, trigger);
        Log($"Marked {trigger} effect as processed for {Name} (TargetID: {TargetId.ToUpper()})", LogTag.Effects);
    }

    private void ProcessDamageEffect(EffectAction action, ActionsQueue actionsQueue) {
        // Handle retaliatory damage that doesn't need Owner
        if (lastAttacker != null && action.targetType == TargetType.AllCreatures && Effects.Any(e => e.trigger == EffectTrigger.OnDamage)) {
            Log($"Targeting attacker {lastAttacker.Name} (TargetID: {lastAttacker.TargetId.ToUpper()}) for retaliation damage",
                LogTag.Creatures | LogTag.Actions);
            actionsQueue.AddAction(new DirectDamageAction(lastAttacker, action.value, this));
            return;
        }

        // Special case for Self target - can self-harm without Owner
        if (action.targetType == TargetType.Self) {
            Log($"Adding DirectDamageAction - Source: {Name} (TargetID: {TargetId.ToUpper()}), Target: Self, Damage: {action.value}",
                LogTag.Creatures | LogTag.Actions);
            actionsQueue.AddAction(new DirectDamageAction(this, action.value, this));
            return;
        }

        // For targeting other entities, we need Owner
        if (Owner == null) {
            LogError($"Cannot handle damage effect for {Name} (TargetID: {TargetId.ToUpper()}) - Owner is null", LogTag.Creatures | LogTag.Effects);
            return;
        }

        Log($"Processing damage effect for {Name} (TargetID: {TargetId.ToUpper()}). TargetType: {action.targetType}, Damage: {action.value}",
            LogTag.Creatures | LogTag.Actions);

        // Handle normal targeting
        var targets = TargetingSystem.GetValidTargets(Owner, action.targetType);
        Log($"Found {targets.Count} targets for {Name} (TargetID: {TargetId.ToUpper()})'s damage effect",
            LogTag.Creatures | LogTag.Actions);

        foreach (var target in targets) {
            if (target is BattlefieldSlot slot) {
                if (!slot.IsOccupied()) continue;
                Log($"Adding DirectDamageAction - Source: {Name} (TargetID: {TargetId.ToUpper()}), Target: {slot.OccupyingCreature.Name} (TargetID: {slot.OccupyingCreature.TargetId.ToUpper()}), Damage: {action.value}",
                    LogTag.Creatures | LogTag.Actions | LogTag.Combat);
                actionsQueue.AddAction(new DirectDamageAction(slot.OccupyingCreature, action.value, this));
            } else if (target is IPlayer player) {
                Log($"Adding DamagePlayerAction - Source: {Name} (TargetID: {TargetId.ToUpper()}), Target: Player {(player.IsPlayer1() ? "1" : "2")} (TargetID: {player.TargetId.ToUpper()}), Damage: {action.value}",
                    LogTag.Creatures | LogTag.Actions | LogTag.Players | LogTag.Combat);
                actionsQueue.AddAction(new DamagePlayerAction(player, action.value));
            } else if (target is ICreature creature) {
                Log($"Adding DirectDamageAction - Source: {Name} (TargetID: {TargetId.ToUpper()}), Target: {creature.Name} (TargetID: {creature.TargetId.ToUpper()}), Damage: {action.value}",
                    LogTag.Creatures | LogTag.Actions | LogTag.Combat);
                actionsQueue.AddAction(new DirectDamageAction(creature, action.value, this));
            }
        }
    }

    private void ProcessHealEffect(EffectAction action, ActionsQueue actionsQueue) {
        // Special case for Self target - we can heal ourselves even if Owner is null
        if (action.targetType == TargetType.Self) {
            Log($"Adding HealCreatureAction - Source: {Name} (TargetID: {TargetId.ToUpper()}), Target: Self, Amount: {action.value}",
                LogTag.Creatures | LogTag.Actions);
            actionsQueue.AddAction(new HealCreatureAction(this, action.value));
            return;
        }

        // For other target types, we need Owner
        if (Owner == null) {
            LogError($"Cannot handle heal effect for {Name} (TargetID: {TargetId.ToUpper()}) - Owner is null", LogTag.Creatures | LogTag.Effects);
            return;
        }

        var targets = TargetingSystem.GetValidTargets(Owner, action.targetType);
        Log($"Found {targets.Count} targets for {Name} (TargetID: {TargetId.ToUpper()})'s heal effect",
            LogTag.Creatures | LogTag.Actions);

        foreach (var target in targets) {
            if (target is ICreature creature) {
                Log($"Adding HealCreatureAction - Source: {Name} (TargetID: {TargetId.ToUpper()}), Target: {creature.Name} (TargetID: {creature.TargetId.ToUpper()}), Amount: {action.value}",
                    LogTag.Creatures | LogTag.Actions);
                actionsQueue.AddAction(new HealCreatureAction(creature, action.value));
            } else if (target is IPlayer player) {
                Log($"Adding HealPlayerAction - Source: {Name} (TargetID: {TargetId.ToUpper()}), Target: Player {(player.IsPlayer1() ? "1" : "2")} (TargetID: {player.TargetId.ToUpper()}), Amount: {action.value}",
                    LogTag.Creatures | LogTag.Actions | LogTag.Players);
                actionsQueue.AddAction(new HealPlayerAction(player, action.value));
            }
        }
    }

    private void ProcessDrawEffect(EffectAction action, ActionsQueue actionsQueue) {
        if (Owner == null) {
            LogError($"Cannot handle draw effect for {Name} (TargetID: {TargetId.ToUpper()}) - Owner is null", LogTag.Creatures | LogTag.Effects);
            return;
        }

        // Draw effects typically target the owner or their opponent
        IPlayer targetPlayer = null;
        switch (action.targetType) {
            case TargetType.Player:
                targetPlayer = Owner;
                break;
            case TargetType.Enemy:
                targetPlayer = Owner.Opponent;
                break;
            default:
                LogWarning($"Unexpected target type for draw effect: {action.targetType}", LogTag.Effects);
                return;
        }

        if (targetPlayer != null) {
            Log($"Adding DrawCardsAction - Source: {Name} (TargetID: {TargetId.ToUpper()}), Player: {(targetPlayer.IsPlayer1() ? "1" : "2")} (TargetID: {targetPlayer.TargetId.ToUpper()}), Amount: {action.value}",
                LogTag.Creatures | LogTag.Actions | LogTag.Cards);
            actionsQueue.AddAction(new DrawCardsAction(targetPlayer, action.value));
        }
    }

    private void ProcessSummonEffect(EffectAction action, ActionsQueue actionsQueue) {
        if (Owner == null) {
            LogError($"Cannot handle summon effect for {Name} (TargetID: {TargetId.ToUpper()}) - Owner is null", LogTag.Creatures | LogTag.Effects);
            return;
        }

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
    }

    private void ProcessBuffEffect(EffectAction action, ActionsQueue actionsQueue) {
        // Get buff flags directly from the action
        bool buffAttack = action.buffAttack;
        bool buffHealth = action.buffHealth;

        string buffDescription = "";
        if (buffAttack && buffHealth) {
            buffDescription = $"+{action.value}/+{action.value}";
        } else if (buffAttack) {
            buffDescription = $"+{action.value} attack";
        } else if (buffHealth) {
            buffDescription = $"+{action.value} health";
        }

        Log($"Processing buff effect for {Name} (TargetID: {TargetId.ToUpper()}). TargetType: {action.targetType}, Value: {buffDescription}",
            LogTag.Creatures | LogTag.Actions | LogTag.Effects);

        // Check if this is the Abyssal Cucumber card (which gives +2 attack for 2 turns)
        int durationInTurns = 0; // Default: permanent buff
        if (Name == "Abyssal Cucumber" && action.value == 2 && buffAttack && !buffHealth) {
            durationInTurns = 2; // Set duration to 2 turns for Abyssal Cucumber
            Log($"Detected Abyssal Cucumber buff: Setting duration to {durationInTurns} turns", LogTag.Creatures | LogTag.Effects);
        }

        // For self-targeting, we can buff ourselves even if Owner is null
        if (action.targetType == TargetType.Self) {
            if (buffAttack || buffHealth) {
                actionsQueue.AddAction(new BuffCreatureAction(this, action.value, action.value, buffAttack, buffHealth, durationInTurns));
            }
            return;
        }

        // For other target types, we need Owner
        if (Owner == null) {
            LogError($"Cannot handle buff effect for {Name} (TargetID: {TargetId.ToUpper()}) - Owner is null", LogTag.Creatures | LogTag.Effects);
            return;
        }

        // Get valid targets based on targeting type
        var targets = TargetingSystem.GetValidTargets(Owner, action.targetType, action.targetModifier);
        Log($"Found {targets.Count} targets for {Name} (TargetID: {TargetId.ToUpper()})'s buff effect",
            LogTag.Creatures | LogTag.Actions | LogTag.Effects);

        foreach (var target in targets) {
            if (target is ICreature creature) {
                string durationText = durationInTurns > 0 ? $" for {durationInTurns} turns" : "";
                Log($"Buffing creature {creature.Name} (TargetID: {creature.TargetId.ToUpper()}) with {buffDescription}{durationText}",
                    LogTag.Creatures | LogTag.Actions | LogTag.Effects);

                // Add buff action to the queue with specific buff flags and duration
                actionsQueue.AddAction(new BuffCreatureAction(creature, action.value, action.value, buffAttack, buffHealth, durationInTurns));
            }
        }
    }
}