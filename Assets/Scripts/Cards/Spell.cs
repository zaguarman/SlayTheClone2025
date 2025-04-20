using System.Collections.Generic;
using static DebugLogger;
using static Enums;
using System.Linq;

public class Spell : Card {
    private readonly List<SpellAction> actions = new List<SpellAction>();
    private readonly TargetType defaultTargetType;

    public TargetType DefaultTargetType => defaultTargetType;

    public Spell(string name, TargetType defaultTargetType) : base(name) {
        this.defaultTargetType = defaultTargetType;
    }

    // Constructor with cardId parameter
    public Spell(string name, TargetType defaultTargetType, string cardId) : base(name, cardId) {
        this.defaultTargetType = defaultTargetType;
    }

    public void AddAction(ActionType actionType, int value, TargetType targetType) {
        actions.Add(new SpellAction(actionType, value, targetType));
    }

    public void AddAction(ActionType actionType, int value, TargetType targetType, TargetModifier targetModifier = TargetModifier.None) {
        actions.Add(new SpellAction(actionType, value, targetType, targetModifier));
    }

    public void AddAction(ActionType actionType, int value, TargetType targetType, bool modifyAttack, bool modifyHealth, bool modifySpeed = false, TargetModifier targetModifier = TargetModifier.None) {
        actions.Add(new SpellAction(actionType, value, targetType, modifyAttack, modifyHealth, modifySpeed, targetModifier));
    }

    public void AddAction(ActionType actionType, TargetType targetType, StatusEffectType statusType, int duration, int potency, TargetModifier targetModifier = TargetModifier.None) {
        actions.Add(new SpellAction(actionType, targetType, statusType, duration, potency, targetModifier));
    }

    public void ClearActions() {
        actions.Clear();
    }

    public override void Play(IPlayer owner, ActionsQueue context, ITarget target = null) {
        Log($"Playing spell {Name} (TargetID: {TargetId.ToUpper().Substring(0, 8)}) with {actions.Count} actions", LogTag.Cards | LogTag.Actions);

        // If no target is specified, use the default target type
        if (target == null) {
            target = GetDefaultTarget(owner);
        }

        // Execute each action defined for the spell
        foreach (var action in actions) {
            // Spells generally have IMMEDIATE effects.
            // So, we queue GameActions rather than applying modifiers directly here.
            // The GameActions (like BuffCreatureAction, ApplyStatusEffectAction) will then apply the modifiers.
            CreateGameActionFromSpell(action, target, owner, context);
        }
    }

    private ITarget GetDefaultTarget(IPlayer owner) {
        // If the default target type is Player, return the owner
        if (defaultTargetType == TargetType.Player) {
            return owner;
        }

        // If the default target type is Enemy, return the opponent
        if (defaultTargetType == TargetType.Enemy) {
            return owner.Opponent;
        }

        // Get the target modifier from the first action (if any)
        TargetModifier targetModifier = TargetModifier.None;
        if (actions.Count > 0) {
            targetModifier = actions[0].TargetModifier;
        }

        // Otherwise use the targeting system to get valid targets
        var targets = TargetingSystem.GetValidTargets(owner, defaultTargetType, targetModifier);
        return targets.FirstOrDefault();
    }

    // Renamed from CreateGameAction to avoid confusion with IGameAction interface
    private void CreateGameActionFromSpell(SpellAction spellAction, ITarget target, IPlayer owner, ActionsQueue context) {
        // Find the corresponding SpellAction to get buff flags and target modifier if needed
        bool modifyAttack = spellAction?.ModifyAttack ?? true;
        bool modifyHealth = spellAction?.ModifyHealth ?? true;
        bool modifySpeed = spellAction?.ModifySpeed ?? false; // Default to false for speed
        TargetModifier targetModifier = spellAction?.TargetModifier ?? TargetModifier.None;

        // Resolve target if needed (e.g., random)
        if (target == null || targetModifier.HasFlag(TargetModifier.Random)) {
            var targets = TargetingSystem.GetValidTargets(owner, spellAction.TargetType, targetModifier);
            target = targets.FirstOrDefault(); // Get the first (potentially random) target
        }

        // Based on SpellAction.ActionType, queue the appropriate IGameAction
        switch (spellAction.ActionType) {
            case ActionType.Damage:
                CreateDamageAction(spellAction.Value, target, context); // Queues DamageCreature/PlayerAction
                break;
            case ActionType.Heal:
                CreateHealAction(spellAction.Value, target, context); // Queues HealCreature/PlayerAction
                break;
            case ActionType.Draw:
                CreateDrawAction(spellAction.Value, owner, context); // Queues DrawCardsAction
                break;
            case ActionType.ModifyStat:
                // Queue ModifyAction - it will handle applying the modifier
                CreatemodifyAction(spellAction.Value, target, context, modifyAttack, modifyHealth, modifySpeed);
                break;
            case ActionType.ApplyStatus:
                // Queue ApplyStatusEffectAction - it will handle applying the modifier
                CreateApplyStatusAction(target, context, spellAction.StatusEffectToApply, spellAction.StatusDuration, spellAction.StatusPotency);
                break;
            case ActionType.Summon:
                // Not implemented in this prototype
                LogWarning($"Summon action for spells not implemented yet.", LogTag.Actions);
                break;
            case ActionType.Armor:
            case ActionType.Stun:
                LogWarning($"{spellAction.ActionType} action for spells not fully implemented yet.", LogTag.Actions);
                // Could map these to Buff/ApplyStatus actions if desired
                break;
        }
    }

    // --- Action Queuing Methods (mostly unchanged) ---
    private void CreateDamageAction(int value, ITarget target, ActionsQueue context) {
        if (target is ICreature creature) {
            Log($"Spell: Queueing DamageCreatureAction for {value} to {creature.Name}", LogTag.Actions);
            context.AddAction(new DamageCreatureAction(creature, value)); // Use consolidated action
        } else if (target is IPlayer player) {
            Log($"Spell: Queueing DamagePlayerAction for {value} to Player {(player.IsPlayer1() ? "1" : "2")}", LogTag.Actions);
            context.AddAction(new DamagePlayerAction(player, value));
        } else {
            LogWarning($"Spell Damage: Invalid target type {target?.GetType().Name}", LogTag.Actions);
        }
    }

    private void CreateHealAction(int value, ITarget target, ActionsQueue context) {
        if (target is ICreature creature) {
            Log($"Spell: Queueing HealCreatureAction for {value} to {creature.Name}", LogTag.Actions);
            context.AddAction(new HealCreatureAction(creature, value));
        } else if (target is IPlayer player) {
            Log($"Spell: Queueing HealPlayerAction for {value} to Player {(player.IsPlayer1() ? "1" : "2")}", LogTag.Actions);
            context.AddAction(new HealPlayerAction(player, value));
        } else {
            LogWarning($"Spell Heal: Invalid target type {target?.GetType().Name}", LogTag.Actions);
        }
    }

    private void CreateDrawAction(int value, IPlayer player, ActionsQueue context) {
        Log($"Spell: Queueing DrawCardsAction for {value} cards for Player {(player.IsPlayer1() ? "1" : "2")}", LogTag.Actions);
        context.AddAction(new DrawCardsAction(player, value));
    }

    // Updated CreatemodifyAction to queue the action with modifySpeed parameter
    private void CreatemodifyAction(int value, ITarget target, ActionsQueue context, bool modifyAttack, bool modifyHealth, bool modifySpeed = false) {
        if (target is ICreature creature) {
            string buffDesc = DescribeModificationForLog(value, modifyAttack, modifyHealth, modifySpeed);
            Log($"Spell: Queueing ModifyAction for {buffDesc} to {creature.Name}", LogTag.Actions);
            // Assuming spell buffs are permanent unless specified otherwise in data
            context.AddAction(new ModifyAction(creature, value, modifyAttack, modifyHealth, modifySpeed, 0)); // Default duration 0
        } else {
            LogWarning($"Spell ModifyStat: Invalid target type {target?.GetType().Name}", LogTag.Actions);
        }
    }

    // --- NEW: Method to queue ApplyStatusEffectAction ---
    private void CreateApplyStatusAction(ITarget target, ActionsQueue context, StatusEffectType statusType, int duration, int potency) {
        if (target is ICreature creature) {
            Log($"Spell: Queueing ApplyStatusEffectAction ({statusType}, Dur:{duration}, Pot:{potency}) to {creature.Name}", LogTag.Actions);
            context.AddAction(new ApplyStatusEffectAction(creature, statusType, duration, potency));
        } else {
            LogWarning($"Spell ApplyStatus: Invalid target type {target?.GetType().Name}", LogTag.Actions);
        }
    }

    private string DescribeModificationForLog(int value, bool modifyAttack, bool modifyHealth, bool modifySpeed) {
        List<string> modifiers = new List<string>();
        if (modifyAttack) modifiers.Add($"+{value} Atk");
        if (modifyHealth) modifiers.Add($"+{value} HP");
        if (modifySpeed) modifiers.Add($"+{value} Spd");

        return modifiers.Count > 0 ? string.Join(" & ", modifiers) : "No effect";
    }
}

// --- Update SpellAction Class ---
public class SpellAction {
    public ActionType ActionType { get; }
    public int Value { get; } // Keep for damage, heal, draw amounts
    public TargetType TargetType { get; }
    public TargetModifier TargetModifier { get; }
    // Modifiers Specific
    public bool ModifyAttack { get; }
    public bool ModifyHealth { get; }
    public bool ModifySpeed { get; }
    // ApplyStatus Specific
    public StatusEffectType StatusEffectToApply { get; }
    public int StatusDuration { get; }
    public int StatusPotency { get; }

    // Constructor for simple actions
    public SpellAction(ActionType actionType, int value, TargetType targetType, TargetModifier modifier = TargetModifier.None) {
        ActionType = actionType; Value = value; TargetType = targetType; TargetModifier = modifier;
        // Set defaults for others
        ModifyAttack = false; ModifyHealth = false; ModifySpeed = false;
        StatusEffectToApply = StatusEffectType.None; StatusDuration = 0; StatusPotency = 0;
    }

    // Constructor for Modify actions
    public SpellAction(ActionType actionType, int value, TargetType targetType, bool modifyAttack, bool modifyHealth, bool modifySpeed = false, TargetModifier modifier = TargetModifier.None) {
         if (actionType != ActionType.ModifyStat) throw new System.ArgumentException("Incorrect constructor for non-ModifyStat action.");
        ActionType = actionType; Value = value; TargetType = targetType; TargetModifier = modifier;
        ModifyAttack = modifyAttack; ModifyHealth = modifyHealth; ModifySpeed = modifySpeed;
        // Set defaults for others
        StatusEffectToApply = StatusEffectType.None; StatusDuration = 0; StatusPotency = 0;
    }

    // Constructor for ApplyStatus actions
    public SpellAction(ActionType actionType, TargetType targetType, StatusEffectType status, int duration, int potency, TargetModifier modifier = TargetModifier.None) {
        if (actionType != ActionType.ApplyStatus) throw new System.ArgumentException("Incorrect constructor for non-ApplyStatus action.");
        ActionType = actionType; TargetType = targetType; TargetModifier = modifier;
        StatusEffectToApply = status; StatusDuration = duration; StatusPotency = potency;
        // Set defaults for others
        Value = 0; ModifyAttack = false; ModifyHealth = false; ModifySpeed = false;
    }
}