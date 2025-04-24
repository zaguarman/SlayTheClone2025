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

    public override void Play(IPlayer owner, IActionsQueue context, ITarget target = null) {
        Log($"Playing spell {Name} (TargetID: {TargetId.ToUpper().Substring(0, 8)}) with {actions.Count} actions", LogTag.Cards | LogTag.Actions);

        if (target == null) {
            target = GetDefaultTarget(owner);
        }

        foreach (var action in actions) {
            CreateGameActionFromSpell(action, target, owner, context);
        }
    }

    private ITarget GetDefaultTarget(IPlayer owner) {
        if (defaultTargetType == TargetType.Player) {
            return owner;
        }

        if (defaultTargetType == TargetType.Enemy) {
            return owner.Opponent;
        }

        TargetModifier targetModifier = TargetModifier.None;
        if (actions.Count > 0) {
            targetModifier = actions[0].TargetModifier;
        }

        var targets = TargetingSystem.GetValidTargets(owner, defaultTargetType, targetModifier);
        return targets.FirstOrDefault();
    }

    private void CreateGameActionFromSpell(SpellAction spellAction, ITarget target, IPlayer owner, IActionsQueue context) {
        bool modifyAttack = spellAction?.ModifyAttack ?? true;
        bool modifyHealth = spellAction?.ModifyHealth ?? true;
        bool modifySpeed = spellAction?.ModifySpeed ?? false;
        TargetModifier targetModifier = spellAction?.TargetModifier ?? TargetModifier.None;

        List<ITarget> targetsToProcess = new List<ITarget>();
        if (target == null || targetModifier.HasFlag(TargetModifier.Random) || targetModifier.HasFlag(TargetModifier.AllSameTypeTargets)) {
            targetsToProcess.AddRange(TargetingSystem.GetValidTargets(owner, spellAction.TargetType, targetModifier));
            Log($"Spell action resolved to {targetsToProcess.Count} targets for {spellAction.TargetType}/{targetModifier}.", LogTag.Actions | LogTag.Effects);
        } else {
            targetsToProcess.Add(target);
        }

        foreach(var currentTarget in targetsToProcess) {
            switch (spellAction.ActionType) {
                case ActionType.Damage:
                    CreateDamageAction(spellAction.Value, currentTarget, context);
                    break;
                case ActionType.Heal:
                    CreateHealAction(spellAction.Value, currentTarget, context);
                    break;
                case ActionType.Draw:
                    IPlayer drawTargetPlayer = currentTarget as IPlayer ?? owner;
                    if(spellAction.TargetType == TargetType.Enemy && owner.Opponent != null) drawTargetPlayer = owner.Opponent;
                    if(spellAction.TargetType == TargetType.Player) drawTargetPlayer = owner;
                    CreateDrawAction(spellAction.Value, drawTargetPlayer, context);
                    break;
                case ActionType.ModifyStat:
                    CreatemodifyAction(spellAction.Value, currentTarget, context, modifyAttack, modifyHealth, modifySpeed);
                    break;
                case ActionType.ApplyStatus:
                    CreateApplyStatusAction(currentTarget, context, spellAction.StatusEffectToApply, spellAction.StatusDuration, spellAction.StatusPotency);
                    break;
                case ActionType.Armor:
                    CreateModifyArmorAction(currentTarget, context, spellAction.Value);
                    break;
                case ActionType.Stun: // Map Stun to ApplyStatus Paralyzed for now
                     Log($"Spell: Mapping Stun action to ApplyStatus (Paralyzed, Dur:{spellAction.Value})", LogTag.Actions);
                     CreateApplyStatusAction(currentTarget, context, StatusEffectType.Paralyzed, spellAction.Value, 0);
                     break;
                case ActionType.Summon:
                    LogWarning($"Summon action for spells not implemented yet.", LogTag.Actions);
                    break;
            }
        }
    }

    private void CreateDamageAction(int value, ITarget target, IActionsQueue context) {
        if (target is ICreature creature) {
            Log($"Spell: Queueing DamageCreatureAction for {value} to {creature.Name}", LogTag.Actions);
            context.AddAction(new DamageCreatureAction(creature, value));
        } else if (target is IPlayer player) {
            Log($"Spell: Queueing DamagePlayerAction for {value} to Player {(player.IsPlayer1() ? "1" : "2")}", LogTag.Actions);
            context.AddAction(new DamagePlayerAction(player, value));
        } else {
            LogWarning($"Spell Damage: Invalid target type {target?.GetType().Name}", LogTag.Actions);
        }
    }

    private void CreateHealAction(int value, ITarget target, IActionsQueue context) {
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

    private void CreateDrawAction(int value, IPlayer player, IActionsQueue context) {
        Log($"Spell: Queueing DrawCardsAction for {value} cards for Player {(player.IsPlayer1() ? "1" : "2")}", LogTag.Actions);
        context.AddAction(new DrawCardsAction(player, value));
    }

    private void CreatemodifyAction(int value, ITarget target, IActionsQueue context, bool modifyAttack, bool modifyHealth, bool modifySpeed = false) {
        if (target is ICreature creature) {
            string buffDesc = DescribeModificationForLog(value, modifyAttack, modifyHealth, modifySpeed);
            Log($"Spell: Queueing ModifyAction for {buffDesc} to {creature.Name}", LogTag.Actions);
            context.AddAction(new ModifyAction(creature, value, modifyAttack, modifyHealth, modifySpeed, 0));
        } else {
            LogWarning($"Spell ModifyStat: Invalid target type {target?.GetType().Name}", LogTag.Actions);
        }
    }

    private void CreateApplyStatusAction(ITarget target, IActionsQueue context, StatusEffectType statusType, int duration, int potency) {
        if (target is ICreature creature) {
            Log($"Spell: Queueing ApplyStatusEffectAction ({statusType}, Dur:{duration}, Pot:{potency}) to {creature.Name}", LogTag.Actions);
            context.AddAction(new ApplyStatusEffectAction(creature, statusType, duration, potency));
        } else {
            LogWarning($"Spell ApplyStatus: Invalid target type {target?.GetType().Name}", LogTag.Actions);
        }
    }

    private void CreateModifyArmorAction(ITarget target, IActionsQueue context, int value)
    {
        if (target is ICreature creature)
        {
            Log($"Spell: Queueing ModifyArmorAction (Value:{value}) to {creature.Name}", LogTag.Actions | LogTag.Effects);
            context.AddAction(new ModifyArmorAction(creature, value));
        } else {
            LogWarning($"Spell ModifyArmor: Invalid target type {target?.GetType().Name}. Only Creatures can receive armor.", LogTag.Actions | LogTag.Effects);
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

public class SpellAction {
    public ActionType ActionType { get; }
    public int Value { get; }
    public TargetType TargetType { get; }
    public TargetModifier TargetModifier { get; }

    public bool ModifyAttack { get; }
    public bool ModifyHealth { get; }
    public bool ModifySpeed { get; }

    public StatusEffectType StatusEffectToApply { get; }
    public int StatusDuration { get; }
    public int StatusPotency { get; }

    public SpellAction(ActionType actionType, int value, TargetType targetType, TargetModifier modifier = TargetModifier.None) {
        ActionType = actionType; Value = value; TargetType = targetType; TargetModifier = modifier;
        ModifyAttack = false; ModifyHealth = false; ModifySpeed = false;
        StatusEffectToApply = StatusEffectType.None; StatusDuration = 0; StatusPotency = 0;
    }

    public SpellAction(ActionType actionType, int value, TargetType targetType, bool modifyAttack, bool modifyHealth, bool modifySpeed = false, TargetModifier modifier = TargetModifier.None) {
         if (actionType != ActionType.ModifyStat) throw new System.ArgumentException("Incorrect constructor for non-ModifyStat action.");
        ActionType = actionType; Value = value; TargetType = targetType; TargetModifier = modifier;
        ModifyAttack = modifyAttack; ModifyHealth = modifyHealth; ModifySpeed = modifySpeed;
        StatusEffectToApply = StatusEffectType.None; StatusDuration = 0; StatusPotency = 0;
    }

    public SpellAction(ActionType actionType, TargetType targetType, StatusEffectType status, int duration, int potency, TargetModifier modifier = TargetModifier.None) {
        if (actionType != ActionType.ApplyStatus) throw new System.ArgumentException("Incorrect constructor for non-ApplyStatus action.");
        ActionType = actionType; TargetType = targetType; TargetModifier = modifier;
        StatusEffectToApply = status; StatusDuration = duration; StatusPotency = potency;
        Value = 0; ModifyAttack = false; ModifyHealth = false; ModifySpeed = false;
    }
}