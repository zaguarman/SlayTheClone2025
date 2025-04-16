using System.Collections.Generic;
using static DebugLogger;
using Enums;
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


    public void AddAction(ActionType actionType, int value, TargetType targetType, TargetModifier targetModifier) {
        actions.Add(new SpellAction(actionType, value, targetType, targetModifier));
    }


    public override void Play(IPlayer owner, ActionsQueue context, ITarget target = null) {
        Log($"Playing spell {Name} (TargetID: {TargetId.ToUpper()}) with {actions.Count} actions", LogTag.Cards | LogTag.Actions);

        // If no target is specified, use the default target type
        if (target == null) {
            target = GetDefaultTarget(owner);
        }

        // Execute each action
        foreach (var action in actions) {
            CreateGameAction(action.ActionType, action.Value, target, owner, context);
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

    private void CreateGameAction(ActionType actionType, int value, ITarget target, IPlayer owner, ActionsQueue context) {
        // Find the corresponding SpellAction to get target modifier if needed
        SpellAction spellAction = actions.FirstOrDefault(a => a.ActionType == actionType);
        TargetModifier targetModifier = spellAction?.TargetModifier ?? TargetModifier.None;

        // If we have a target type and a target modifier, we need to get appropriate targets
        if (spellAction != null && targetModifier != TargetModifier.None) {
            // If no target is provided or we have a random modifier, get targets from the targeting system
            if (target == null || targetModifier.HasFlag(TargetModifier.Random)) {
                var targets = TargetingSystem.GetValidTargets(owner, spellAction.TargetType, targetModifier);
                target = targets.FirstOrDefault();
            }
        }

        switch (actionType) {
            case ActionType.Damage:
                CreateDamageAction(value, target, context);
                break;

            case ActionType.Heal:
                CreateHealAction(value, target, context);
                break;

            case ActionType.Draw:
                CreateDrawAction(value, owner, context);
                break;

            case ActionType.Summon:
                // Not implemented in this prototype
                break;
        }
    }

    private void CreateDamageAction(int value, ITarget target, ActionsQueue context) {
        if (target is ICreature creature) {
            Log($"Creating direct damage action for {value} to creature {creature.Name}", LogTag.Cards | LogTag.Actions);
            context.AddAction(new DirectDamageAction(creature, value));
        } else if (target is IPlayer player) {
            Log($"Creating damage action for {value} to player {(player.IsPlayer1() ? "1" : "2")}", LogTag.Cards | LogTag.Actions);
            context.AddAction(new DamagePlayerAction(player, value));
        }
    }

    private void CreateHealAction(int value, ITarget target, ActionsQueue context) {
        if (target is ICreature creature) {
            Log($"Creating heal action for {value} to creature {creature.Name}", LogTag.Cards | LogTag.Actions);
            context.AddAction(new HealCreatureAction(creature, value));
        } else if (target is IPlayer player) {
            Log($"Creating heal action for {value} to player {(player.IsPlayer1() ? "1" : "2")}", LogTag.Cards | LogTag.Actions);
            context.AddAction(new HealPlayerAction(player, value));
        }
    }

    private void CreateDrawAction(int value, IPlayer player, ActionsQueue context) {
        Log($"Creating draw action for {value} cards", LogTag.Cards | LogTag.Actions);
        context.AddAction(new DrawCardsAction(player, value));
    }
}

// Simple container for spell action data
public class SpellAction {
    public ActionType ActionType { get; }
    public int Value { get; }
    public TargetType TargetType { get; }
    public TargetModifier TargetModifier { get; }


    public SpellAction(ActionType actionType, int value, TargetType targetType) {
        ActionType = actionType;
        Value = value;
        TargetType = targetType;
        TargetModifier = TargetModifier.None;

    }



    public SpellAction(ActionType actionType, int value, TargetType targetType, TargetModifier targetModifier) {
        ActionType = actionType;
        Value = value;
        TargetType = targetType;
        TargetModifier = targetModifier;

    }


}