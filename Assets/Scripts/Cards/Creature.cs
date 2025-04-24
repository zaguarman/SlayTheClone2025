using static DebugLogger;
using static Enums;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Tests")]

public interface ICreature : ICard {
    int Attack { get; }
    int Health { get; }
    int Speed { get; }
    int MaxHealth { get; }
    int CurrentArmorPool { get; }
    int BaseAttack { get; }
    int BaseHealth { get; }
    int BaseSpeed { get; }
    BattlefieldSlot Slot { get; set; }
    // Keep internal damage modification, but consequences are external
    int TakeHealthDamage(int healthDamage); // Returns actual damage dealt
    void ModifyArmorPool(int amount);
    IPlayer Owner { get; }
    void SetOwner(IPlayer owner);
    void UpdateEffectiveStats(int newEffectiveAttack, int newEffectiveMaxHealth, int newEffectiveSpeed);
    bool IsDead { get; } // Expose IsDead flag
    void MarkAsDead(); // Method to explicitly mark as dead

    // Effect Handling Methods
    void HandleEffect(EffectTrigger trigger, ActionExecutionContext context);
    void HandleTurnBasedEffect(EffectTrigger trigger, IActionsQueue actionsQueue, IModifierManager modifierManager, ITurnManager turnManager);
}

public class Creature : Card, ICreature {
    public int BaseAttack { get; private set; }
    public int BaseHealth { get; private set; }
    public int BaseSpeed { get; private set; }

    private int _effectiveAttack;
    private int _effectiveMaxHealth;
    private int _effectiveSpeed;
    public int CurrentArmorPool { get; private set; }

    private int currentHealth;
    public bool IsDead { get; private set; } = false; // Use property

    public int Attack => _effectiveAttack;
    public int MaxHealth => _effectiveMaxHealth;
    public int Speed => _effectiveSpeed;
    public int Health => currentHealth;

    public IPlayer Owner { get; private set; }
    public BattlefieldSlot Slot { get; set; }

    private ICreature lastAttacker;

    public Creature(string name, int attack, int health, int speed, string cardId) : base(name, cardId) {
        BaseAttack = attack;
        BaseHealth = health;
        BaseSpeed = Math.Max(1, speed); // Ensure minimum speed 1
        currentHealth = health;
        _effectiveAttack = attack;
        _effectiveMaxHealth = health;
        _effectiveSpeed = BaseSpeed;
        CurrentArmorPool = 0;
        IsDead = false; // Explicitly false on creation
    }

    public void UpdateEffectiveStats(int newEffectiveAttack, int newEffectiveMaxHealth, int newEffectiveSpeed)
    {
        int oldMaxHealth = _effectiveMaxHealth;

        _effectiveAttack = Math.Max(0, newEffectiveAttack);
        _effectiveMaxHealth = Math.Max(1, newEffectiveMaxHealth); // Ensure max health is at least 1
        _effectiveSpeed = Math.Max(0, newEffectiveSpeed);

        int healthIncrease = _effectiveMaxHealth - oldMaxHealth;
        // Only increase current health if max health increased AND creature is not dead
        if (healthIncrease > 0 && !IsDead)
        {
            currentHealth += healthIncrease;
        }

        // Clamp current health between 0 and the new max health
        currentHealth = Math.Clamp(currentHealth, 0, _effectiveMaxHealth);

        // If health was 0 and max health increased, it stays at 0 unless explicitly healed.
    }

    public void ModifyArmorPool(int amount) {
        int previousArmor = CurrentArmorPool;
        CurrentArmorPool += amount;
        CurrentArmorPool = Math.Max(0, CurrentArmorPool);
        // Log removed, notification handled by executor or mediator
        // GameMediator.Instance?.NotifyCreatureArmorChanged(this, CurrentArmorPool); // REMOVED
    }

    public void SetOwner(IPlayer owner) {
        Owner = owner;
    }

    public override void Play(IPlayer owner, IActionsQueue context, ITarget target = null) {
        Log($"Playing {Name} (TargetID: {TargetId.ToUpper()}) with {Effects.Count} effects", LogTag.Creatures | LogTag.Cards | LogTag.Actions);
        Owner = owner;

        bool fromHand = owner?.Hand.Contains(this) ?? false;
        context.AddAction(new SummonCreatureAction(this, owner, target, !fromHand));
    }

    // Modified: Only applies damage, returns actual damage dealt. Consequences handled externally.
    public int TakeHealthDamage(int healthDamage) {
        if (IsDead || healthDamage <= 0) return 0;

        int previousHealth = currentHealth;
        int actualDamageDealt = Math.Min(healthDamage, currentHealth);
        currentHealth -= actualDamageDealt;
        currentHealth = Math.Max(0, currentHealth); // Ensure health doesn't go below 0

        string damageLog = $"Creature {Name} (ID: {TargetId.ToUpper().Substring(0, 8)}) took {actualDamageDealt} health damage. ";
        damageLog += $"Health: {previousHealth} -> {Health}.";
        Log(damageLog, LogTag.Creatures | LogTag.Combat | LogTag.Effects);

        // REMOVED external calls:
        // HandleEffect(EffectTrigger.OnDamage, ...)
        // GameMediator.Instance?.NotifyCreatureDamaged(...)
        // if (Health <= 0 && !isDead) { Die(); }

        return actualDamageDealt; // Return how much damage was actually dealt
    }

    // Modified: Only marks the creature as dead internally. Consequences handled externally.
    public void MarkAsDead()
    {
        if (IsDead) return; // Already dead

        IsDead = true;
        Log($"Creature marked as dead: {Name} (TargetID: {TargetId.ToUpper()})", LogTag.Creatures);

        // REMOVED external calls:
        // HandleEffect(EffectTrigger.OnDeath, ...)
        // GameMediator.Instance?.NotifyCreatureDied(...)
        // gameManager?.ModifierManager?.UnregisterCreature(this)
        // Owner?.RemoveFromBattlefield(this, false);
    }

    // Handles effects triggered by actions (needs full context)
    public void HandleEffect(EffectTrigger trigger, ActionExecutionContext context)
    {
         if (IsDead && trigger != EffectTrigger.OnDeath) return; // Don't trigger most effects if dead

        if (context == null) {
            LogError($"Cannot handle effect {trigger} for {Name} - ActionExecutionContext is null.", LogTag.Effects | LogTag.Creatures);
            return;
        }

        // Use context dependencies
        var actionsQueue = context.ActionsQueue;
        var modifierManager = context.ModifierManager;
        var factory = context.ModifierFactory;
        var turnManager = context.TurnManager; // Get TurnManager from context

        if (actionsQueue == null || modifierManager == null || factory == null || turnManager == null) {
             LogError($"Cannot handle effect {trigger} for {Name} - Missing dependencies in context.", LogTag.Effects | LogTag.Creatures | LogTag.Initialization);
             return;
        }

        if (actionsQueue.IsEffectProcessed(TargetId, trigger)) {
            return;
        }

        Log($"Handling Action Effect: {trigger} for {Name} (TargetID: {TargetId.ToUpper().Substring(0,8)}) with {Effects.Count} effects", LogTag.Creatures | LogTag.Effects);

        ProcessEffectsInternal(trigger, actionsQueue, modifierManager, factory, turnManager, null); // Pass null for lastAttacker

        actionsQueue.MarkEffectProcessed(TargetId, trigger);
        Log($"Marked Action Effect {trigger} as processed for {Name} (TargetID: {TargetId.ToUpper().Substring(0,8)})", LogTag.Effects);
    }

    // Handles effects triggered by turn progression (needs specific interfaces)
    public void HandleTurnBasedEffect(EffectTrigger trigger, IActionsQueue actionsQueue, IModifierManager modifierManager, ITurnManager turnManager)
    {
        if (IsDead) return; // Don't trigger turn effects if dead

        if (actionsQueue == null || modifierManager == null || turnManager == null) {
             LogError($"Cannot handle turn effect {trigger} for {Name} - Missing dependencies.", LogTag.Effects | LogTag.Creatures | LogTag.Initialization);
             return;
        }

        // Check IsEffectProcessed using the provided actionsQueue
        if (actionsQueue.IsEffectProcessed(TargetId, trigger)) {
             // Log($"Turn-based effect {trigger} for {Name} already processed this cycle.", LogTag.Effects | LogTag.Turns);
            return;
        }

        Log($"Handling Turn Effect: {trigger} for {Name} (TargetID: {TargetId.ToUpper().Substring(0,8)}) with {Effects.Count} effects", LogTag.Creatures | LogTag.Effects | LogTag.Turns);

        ProcessEffectsInternal(trigger, actionsQueue, modifierManager, modifierManager.ModifierFactory, turnManager, null); // Pass null for lastAttacker

        // Mark processed AFTER handling to allow multiple instances in one turn cycle if needed (though currently cleared each cycle)
        actionsQueue.MarkEffectProcessed(TargetId, trigger);
        Log($"Marked Turn Effect {trigger} as processed for {Name} (TargetID: {TargetId.ToUpper().Substring(0,8)})", LogTag.Effects | LogTag.Turns);
    }


    // --- Internal Helper for Processing Effects ---
    private void ProcessEffectsInternal(EffectTrigger trigger, IActionsQueue actionsQueue, IModifierManager modifierManager, IModifierFactory factory, ITurnManager turnManager, ICreature lastAttacker)
    {
        if (factory == null) factory = modifierManager?.ModifierFactory; // Ensure factory is available

        if (actionsQueue == null || modifierManager == null || factory == null || turnManager == null) {
            LogError($"Internal Error: Missing dependencies in ProcessEffectsInternal for {Name} ({trigger}).", LogTag.Effects | LogTag.Creatures | LogTag.Initialization);
            return;
        }

        foreach (var effect in Effects.Where(e => e.trigger == trigger).ToList()) {
            Log($"-- Processing Effect: Trigger={effect.trigger}, Type={effect.effectType}, Actions={effect.actions.Count}", LogTag.Effects);
            foreach (var action in effect.actions) {
                 Log($"---- Action: Type={action.actionType}, Target={action.targetType}, Value={action.value}, Status={action.statusEffectToApply}", LogTag.Effects);

                switch (action.actionType) {
                    case ActionType.Damage:
                        ProcessDamageEffect(action, actionsQueue, lastAttacker); // Pass lastAttacker if needed
                        break;
                    case ActionType.Heal:
                        ProcessHealEffect(action, actionsQueue);
                        break;
                    case ActionType.Draw:
                        ProcessDrawEffect(action, actionsQueue);
                        break;
                    case ActionType.Summon:
                        ProcessSummonEffect(action, actionsQueue, modifierManager); // Need MM to check deck
                        break;
                    case ActionType.ModifyStat:
                        ProcessModifyStatModifier(action, modifierManager, factory, turnManager, action.modifySpeed); // Pass turnManager
                        break;
                    case ActionType.ApplyStatus:
                        ProcessApplyStatusModifier(action, modifierManager, factory, turnManager); // Pass turnManager
                        break;
                    case ActionType.Stun: // Convert Stun to Paralyzed status effect
                        action.statusEffectToApply = StatusEffectType.Paralyzed;
                        action.statusDuration = action.value;
                        action.statusPotency = 0; // Potency not relevant for Paralyzed
                        ProcessApplyStatusModifier(action, modifierManager, factory, turnManager); // Pass turnManager
                        break;
                    case ActionType.Armor:
                        ProcessModifyArmorAction(action, actionsQueue);
                        break;
                }
            }
        }
    }

    private void ProcessDamageEffect(EffectAction action, IActionsQueue actionsQueue, ICreature lastAttacker) {
        if (Owner == null && action.targetType != TargetType.Self && lastAttacker == null) {
            LogError($"Damage Effect: Cannot target others for {Name} - Owner is null and not self/retaliation.", LogTag.Effects);
            return;
        }

        // Retaliation logic - check if trigger was OnDamage and lastAttacker exists
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

        // Other Targets
        if (Owner == null) return; // Need owner for other targets
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

    private void ProcessHealEffect(EffectAction action, IActionsQueue actionsQueue) {
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

     private void ProcessDrawEffect(EffectAction action, IActionsQueue actionsQueue) {
         if (Owner == null) {
              LogError($"Draw Effect: Cannot process for {Name} - Owner is null.", LogTag.Effects);
             return;
         }
        IPlayer targetPlayer = action.targetType == TargetType.Enemy ? Owner.Opponent : Owner;
        if (targetPlayer != null) {
            actionsQueue.AddAction(new DrawCardsAction(targetPlayer, action.value));
        }
    }

    // Updated to use injected ModifierManager to get deck (needs CardDealingService access)
    private void ProcessSummonEffect(EffectAction action, IActionsQueue actionsQueue, IModifierManager modifierManager) {
        if (Owner == null) {
             LogError($"Summon Effect: Cannot process for {Name} - Owner is null.", LogTag.Effects);
             return;
        }

        // Need access to CardDealingService - How? ModifierManager doesn't have it.
        // TEMPORARY WORKAROUND: Use GameManager.Instance ONLY for CardDealingService here.
        // TODO: Refactor CardDealingService dependency. Maybe pass it into HandleEffect context?
        var cardDealingService = GameManager.Instance?.CardDealingService;
        if (cardDealingService == null)
        {
             LogError($"Summon Effect failed for {Name}: CardDealingService instance not found.", LogTag.Effects | LogTag.Initialization);
             return;
        }

        Log($"Processing summon effect for {Name} (TargetID: {TargetId.ToUpper()}). TargetType: {action.targetType}, Value: {action.value}",
           LogTag.Creatures | LogTag.Actions);

        IPlayer targetPlayer = Owner;
        if (action.targetType == TargetType.Enemy) {
            targetPlayer = Owner.Opponent;
        } else if (action.targetType != TargetType.Player && action.targetType != TargetType.FriendlyCreatures) {
            LogWarning($"Unexpected target type for summon effect: {action.targetType}", LogTag.Effects);
            return;
        }

        var deckCards = cardDealingService.GetDeckPreview(targetPlayer);
        var creaturesInDeck = deckCards.Where(c => c is ICreature).ToList();

        if (creaturesInDeck.Count == 0) {
            Log($"No creatures available in {(targetPlayer.IsPlayer1 ? "Player 1" : "Player 2")} (TargetID: {targetPlayer.TargetId.ToUpper()})'s deck to summon",
                LogTag.Creatures | LogTag.Cards);
            return;
        }

        var validSlots = targetPlayer.Battlefield.Where(s => !s.IsOccupied()).ToList();
        if (validSlots.Count == 0) {
            Log($"No valid slots available for {(targetPlayer.IsPlayer1 ? "Player 1" : "Player 2")} (TargetID: {targetPlayer.TargetId.ToUpper()}) to summon creatures",
                LogTag.Creatures | LogTag.Cards);
            return;
        }

        int countToSummon = Math.Min(action.value, Math.Min(validSlots.Count, creaturesInDeck.Count));
        var random = new System.Random();

        for (int i = 0; i < countToSummon; i++) {
            int creatureIndex = random.Next(creaturesInDeck.Count);
            var creatureToSummon = creaturesInDeck[creatureIndex] as ICreature;
            if (creatureToSummon == null) continue;

            creaturesInDeck.RemoveAt(creatureIndex); // Remove the selected creature from the temp list

            // IMPORTANT: Also remove the card from the actual deck using the service
            cardDealingService.RemoveCardFromDeck(targetPlayer, creatureToSummon);

            int slotIndex = random.Next(validSlots.Count);
            var slot = validSlots[slotIndex];
            validSlots.RemoveAt(slotIndex); // Remove the selected slot from the temp list

            actionsQueue.AddAction(new SummonCreatureAction(creatureToSummon, targetPlayer, slot, true));

             Log($"Queued summon effect for {creatureToSummon.Name} (TargetID: {creatureToSummon.TargetId.ToUpper()}) to slot (TargetID: {slot.TargetId.ToUpper()})",
                LogTag.Creatures | LogTag.Effects | LogTag.Actions);
        }
    }

    // --- NEW: Methods to apply modifiers ---

    // Updated to use injected manager/factory/turnManager
    private void ProcessModifyStatModifier(EffectAction action, IModifierManager manager, IModifierFactory factory, ITurnManager turnManager, bool modifySpeed, ModifierCalculationType calcType = ModifierCalculationType.Flat, int? forcedDuration = null) {
         if (manager == null || factory == null || turnManager == null) {
             LogError("ModifyStat Modifier: Manager, Factory or TurnManager is null.", LogTag.Effects | LogTag.Creatures);
             return;
         }
          if (Owner == null && action.targetType != TargetType.Self) return;

        bool modifyAttack = action.modifyAttack;
        bool modifyHealth = action.modifyHealth;
        // modifySpeed is now passed as parameter
        int value = action.value;
        int duration = forcedDuration ?? 0;

        List<ITarget> targets;
        if (action.targetType == TargetType.Self) {
             targets = new List<ITarget> { this };
         } else {
              if (Owner == null) return; // Need owner for non-self targets
             targets = TargetingSystem.GetValidTargets(Owner, action.targetType, action.targetModifier);
         }
         // Log($"ModifyStat Modifier: Found {targets.Count} targets for {action.targetType}/{action.targetModifier}.", LogTag.Effects);

        int currentTurn = turnManager.TurnNumber; // Get current turn from injected manager

        foreach (var target in targets) {
            if (target is Creature creatureTarget) {
                Log($"Applying Stat Modifier to {creatureTarget.Name}: A={modifyAttack}, H={modifyHealth}, S={modifySpeed}, Val={value}, Dur={duration}", LogTag.Effects);

                 if (modifyAttack && value != 0) {
                    string modName = $"Attack Modify ({calcType} {value}){(duration > 0 ? $" [{duration}t]" : "")}";
                    string modDesc = $"{(value >= 0 ? "+" : "")}{value} Attack{(duration > 0 ? $" ({duration} turns)" : "")}";
                     IModifier mod = duration > 0
                         ? factory.CreateTimedStatModifier(modName, modDesc, ModifiableStat.Attack, calcType, value, duration, currentTurn)
                         : factory.CreateStatModifier(modName, modDesc, ModifiableStat.Attack, calcType, value);
                     manager.ApplyModifier(creatureTarget, mod);
                 }
                 if (modifyHealth && value != 0) {
                    string modName = $"Health Modify ({calcType} {value}){(duration > 0 ? $" [{duration}t]" : "")}";
                    string modDesc = $"{(value >= 0 ? "+" : "")}{value} Max Health{(duration > 0 ? $" ({duration} turns)" : "")}";
                     IModifier mod = duration > 0
                        ? factory.CreateTimedStatModifier(modName, modDesc, ModifiableStat.Health, calcType, value, duration, currentTurn)
                        : factory.CreateStatModifier(modName, modDesc, ModifiableStat.Health, calcType, value);
                     manager.ApplyModifier(creatureTarget, mod);
                 }
                // --- Add Speed Modification ---
                 if (modifySpeed && value != 0) {
                    string modName = $"Speed Modify ({calcType} {value}){(duration > 0 ? $" [{duration}t]" : "")}";
                    string modDesc = $"{(value >= 0 ? "+" : "")}{value} Speed{(duration > 0 ? $" ({duration} turns)" : "")}";
                    IModifier mod = duration > 0
                        ? factory.CreateTimedStatModifier(modName, modDesc, ModifiableStat.Speed, calcType, value, duration, currentTurn)
                        : factory.CreateStatModifier(modName, modDesc, ModifiableStat.Speed, calcType, value);
                    manager.ApplyModifier(creatureTarget, mod);
                 }
                 // --- End Speed Buff ---
             }
        }
    }

    // Updated to use injected manager/factory/turnManager
    private void ProcessApplyStatusModifier(EffectAction action, IModifierManager manager, IModifierFactory factory, ITurnManager turnManager) {
         if (manager == null || factory == null || turnManager == null) {
             LogError("ApplyStatus Modifier: Manager, Factory or TurnManager is null.", LogTag.Effects | LogTag.Creatures);
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
              if (Owner == null) return; // Need owner for non-self targets
             targets = TargetingSystem.GetValidTargets(Owner, action.targetType, action.targetModifier);
         }
         Log($"ApplyStatus Modifier: Found {targets.Count} targets for {action.targetType}/{action.targetModifier}.", LogTag.Effects);

         int currentTurn = turnManager.TurnNumber; // Get current turn from injected manager

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

    // --- NEW: Method to queue ModifyArmorAction ---
    private void ProcessModifyArmorAction(EffectAction action, IActionsQueue actionsQueue) {

        if (Owner == null && action.targetType != TargetType.Self)
        {
            LogError($"ModifyArmor Action: Cannot target others for {Name} - Owner is null and not self.", LogTag.Effects);
            return;
        }

        int value = action.value;
        if (value == 0) return; // Don't queue if amount is zero

        List<ITarget> targets;
        if (action.targetType == TargetType.Self) {
            targets = new List<ITarget> { this };
        } else {
            targets = TargetingSystem.GetValidTargets(Owner, action.targetType, action.targetModifier);
        }
        Log($"ModifyArmor Action: Found {targets.Count} targets for {action.targetType}/{action.targetModifier}.", LogTag.Effects);

        foreach (var target in targets) {
            if (target is ICreature creatureTarget) {
                Log($"Queueing ModifyArmorAction for {creatureTarget.Name}: Amount={value}", LogTag.Effects | LogTag.Actions);
                actionsQueue.AddAction(new ModifyArmorAction(creatureTarget, value));
            }
        }
    }
}