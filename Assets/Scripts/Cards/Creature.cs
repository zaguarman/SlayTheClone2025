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
    void TakeHealthDamage(int healthDamage, ICreature attacker);
    void ModifyArmorPool(int amount);
    IPlayer Owner { get; }
    void SetOwner(IPlayer owner);
    void UpdateEffectiveStats(int newEffectiveAttack, int newEffectiveMaxHealth, int newEffectiveSpeed);
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
    private bool isDead = false;

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
        BaseSpeed = Math.Max(0, speed);
        currentHealth = health;
        _effectiveAttack = attack;
        _effectiveMaxHealth = health;
        _effectiveSpeed = BaseSpeed;
        CurrentArmorPool = 0;
    }

    public void UpdateEffectiveStats(int newEffectiveAttack, int newEffectiveMaxHealth, int newEffectiveSpeed)
    {
        int oldMaxHealth = _effectiveMaxHealth;

        _effectiveAttack = Math.Max(0, newEffectiveAttack);
        _effectiveMaxHealth = Math.Max(1, newEffectiveMaxHealth);
        _effectiveSpeed = Math.Max(0, newEffectiveSpeed);

        int healthIncrease = _effectiveMaxHealth - oldMaxHealth;
        if (healthIncrease > 0)
        {
            currentHealth += healthIncrease;
        }

        currentHealth = Math.Min(currentHealth, _effectiveMaxHealth);
        currentHealth = Math.Max(0, currentHealth);
    }

    public void ModifyArmorPool(int amount) {
        int previousArmor = CurrentArmorPool;
        CurrentArmorPool += amount;
        CurrentArmorPool = Math.Max(0, CurrentArmorPool);
        Log($"{Name} armor changed by {amount}. Previous: {previousArmor}, New: {CurrentArmorPool}", LogTag.Effects | LogTag.Creatures | LogTag.Combat);
        GameMediator.Instance?.NotifyCreatureArmorChanged(this, CurrentArmorPool);
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

    public void TakeHealthDamage(int healthDamage, ICreature attacker) {
        if (isDead || healthDamage <= 0) return;

        lastAttacker = attacker;
        int previousHealth = currentHealth;

        int actualDamageDealt = Math.Min(healthDamage, currentHealth);
        currentHealth -= actualDamageDealt;
        currentHealth = Math.Max(0, currentHealth);

        string attackerName = attacker != null ? $"{attacker.Name} (ID: {attacker.TargetId.ToUpper().Substring(0, 8)})" : "Source";
        string damageLog = $"{attackerName} dealt {actualDamageDealt} damage directly to {Name}'s health (ID: {TargetId.ToUpper().Substring(0, 8)}). ";
        damageLog += $"Health: {previousHealth} -> {Health}.";
        Log(damageLog, LogTag.Creatures | LogTag.Combat | LogTag.Effects);

        var gameManager = GameManager.Instance;
        if (gameManager?.ActionsQueue != null) {
            HandleEffect(EffectTrigger.OnDamage, gameManager.ActionsQueue);
        }

        GameMediator.Instance?.NotifyCreatureDamaged(this, actualDamageDealt);

        if (Health <= 0 && !isDead) {
            Die();
        }

        lastAttacker = null;
    }

    private void Die()
    {
        isDead = true;
        Log($"Creature died: {Name} (TargetID: {TargetId.ToUpper()})", LogTag.Creatures);

        var gameManager = GameManager.Instance;
        if (gameManager?.ActionsQueue != null) {
            HandleEffect(EffectTrigger.OnDeath, gameManager.ActionsQueue);
        }

        GameMediator.Instance?.NotifyCreatureDied(this);
        gameManager?.ModifierManager?.UnregisterCreature(this);
        Owner?.RemoveFromBattlefield(this, false);
    }

    public void HandleEffect(EffectTrigger trigger, IActionsQueue actionsQueue) {
        var gameManager = GameManager.Instance;
        if (gameManager == null) {
            LogError($"Cannot handle effect {trigger} for {Name} - GameManager instance is null.", LogTag.Effects | LogTag.Creatures);
            return;
        }
        var modifierManager = gameManager.ModifierManager;
        var factory = modifierManager?.ModifierFactory;

        if (actionsQueue.IsEffectProcessed(TargetId, trigger)) {
            return;
        }

        Log($"Handling {trigger} effect for {Name} (TargetID: {TargetId.ToUpper().Substring(0,8)}) with {Effects.Count} effects", LogTag.Creatures | LogTag.Effects);

        foreach (var effect in Effects.Where(e => e.trigger == trigger).ToList()) {
            Log($"-- Processing Effect: Trigger={effect.trigger}, Type={effect.effectType}, Actions={effect.actions.Count}", LogTag.Effects);
            foreach (var action in effect.actions) {
                 Log($"---- Action: Type={action.actionType}, Target={action.targetType}, Value={action.value}, Status={action.statusEffectToApply}", LogTag.Effects);

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
                    case ActionType.ModifyStat:
                        ProcessModifyStatModifier(action, modifierManager, factory, action.modifySpeed);
                        break;
                    case ActionType.ApplyStatus:
                        ProcessApplyStatusModifier(action, modifierManager, factory);
                        break;
                    case ActionType.Stun:
                        action.statusEffectToApply = StatusEffectType.Paralyzed;
                        action.statusDuration = action.value;
                        action.statusPotency = 0;
                        ProcessApplyStatusModifier(action, modifierManager, factory);
                        break;
                    case ActionType.Armor:
                        ProcessModifyArmorAction(action, actionsQueue);
                        break;
                }
            }
        }

        actionsQueue.MarkEffectProcessed(TargetId, trigger);
        Log($"Marked {trigger} effect as processed for {Name} (TargetID: {TargetId.ToUpper().Substring(0,8)})", LogTag.Effects);
    }

    private void ProcessDamageEffect(EffectAction action, IActionsQueue actionsQueue) {
        if (Owner == null && action.targetType != TargetType.Self && lastAttacker == null) {
            LogError($"Damage Effect: Cannot target others for {Name} - Owner is null and not self/retaliation.", LogTag.Effects);
            return;
        }

        if (lastAttacker != null && action.targetType == TargetType.AllCreatures && Effects.Any(e => e.trigger == EffectTrigger.OnDamage)) {
            Log($"Queueing Retaliation DamageAction: Attacker={lastAttacker.Name}, Damage={action.value}", LogTag.Effects);
            actionsQueue.AddAction(new DamageCreatureAction(lastAttacker, action.value, this));
            return;
        }

        if (action.targetType == TargetType.Self) {
             Log($"Queueing Self DamageAction: Target={Name}, Damage={action.value}", LogTag.Effects);
            actionsQueue.AddAction(new DamageCreatureAction(this, action.value, this));
            return;
        }

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

    private void ProcessSummonEffect(EffectAction action, IActionsQueue actionsQueue) {
         if (Owner == null) {
             LogError($"Summon Effect: Cannot process for {Name} - Owner is null.", LogTag.Effects);
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

        var gameManager = GameManager.Instance;
        if (gameManager?.CardDealingService == null) return;

        var deckCards = gameManager.CardDealingService.GetDeckPreview(targetPlayer);
        var creaturesInDeck = deckCards.Where(c => c is ICreature).ToList();

        if (creaturesInDeck.Count == 0) {
            Log($"No creatures available in {(targetPlayer.IsPlayer1() ? "Player 1" : "Player 2")} (TargetID: {targetPlayer.TargetId.ToUpper()})'s deck to summon",
                LogTag.Creatures | LogTag.Cards);
            return;
        }

        var validSlots = targetPlayer.Battlefield.Where(s => !s.IsOccupied()).ToList();
        if (validSlots.Count == 0) {
            Log($"No valid slots available for {(targetPlayer.IsPlayer1() ? "Player 1" : "Player 2")} (TargetID: {targetPlayer.TargetId.ToUpper()}) to summon creatures",
                LogTag.Creatures | LogTag.Cards);
            return;
        }

        int countToSummon = Math.Min(action.value, Math.Min(validSlots.Count, creaturesInDeck.Count));
        var random = new System.Random();

        for (int i = 0; i < countToSummon; i++) {
            int creatureIndex = random.Next(creaturesInDeck.Count);
            var creatureToSummon = creaturesInDeck[creatureIndex] as ICreature;
            if (creatureToSummon == null) continue;

            creaturesInDeck.RemoveAt(creatureIndex);

            int slotIndex = random.Next(validSlots.Count);
            var slot = validSlots[slotIndex];
            validSlots.RemoveAt(slotIndex);

            actionsQueue.AddAction(new SummonCreatureAction(creatureToSummon, targetPlayer, slot, true));

             Log($"Queued summon effect for {creatureToSummon.Name} (TargetID: {creatureToSummon.TargetId.ToUpper()}) to slot (TargetID: {slot.TargetId.ToUpper()})",
                LogTag.Creatures | LogTag.Effects | LogTag.Actions);
        }
    }

    // --- NEW: Methods to apply modifiers ---

    // --- Updated ProcessModifyStatModifier to include modifySpeed ---
    private void ProcessModifyStatModifier(EffectAction action, IModifierManager manager, IModifierFactory factory, bool modifySpeed, ModifierCalculationType calcType = ModifierCalculationType.Flat, int? forcedDuration = null) {
         if (manager == null || factory == null) {
             LogError("ModifyStat Modifier: ModifierManager or Factory is null.", LogTag.Effects | LogTag.Creatures);
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

        int currentTurn = GameManager.Instance?.TurnManager?.TurnNumber ?? 0;

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

    private void ProcessApplyStatusModifier(EffectAction action, IModifierManager manager, IModifierFactory factory) {
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