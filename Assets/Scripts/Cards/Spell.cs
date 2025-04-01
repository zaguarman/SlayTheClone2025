using System.Collections.Generic;
using static DebugLogger;
using static Enums;

public class Spell : Card {
    public TargetType DefaultTargetType { get; private set; }
    private List<SpellAction> spellActions = new List<SpellAction>();

    public Spell(string name, TargetType defaultTargetType) : base(name) {
        DefaultTargetType = defaultTargetType;
    }

    // Constructor with cardId parameter
    public Spell(string name, TargetType defaultTargetType, string cardId) : base(name, cardId) {
        DefaultTargetType = defaultTargetType;
    }

    public void AddAction(ActionType actionType, int value, TargetType targetType) {
        spellActions.Add(new SpellAction(actionType, value, targetType));
    }

    public override void Play(IPlayer owner, ActionsQueue context, ITarget target = null) {
        Log($"Playing spell {Name} with {spellActions.Count} actions", LogTag.Cards | LogTag.Actions);

        // Process each spell action
        foreach (var spellAction in spellActions) {
            ProcessSpellAction(spellAction, owner, context, target);
        }
    }

    private void ProcessSpellAction(SpellAction spellAction, IPlayer owner, ActionsQueue context, ITarget specificTarget = null) {
        // Determine targets based on the targeting strategy
        var targets = DetermineTargets(spellAction.TargetType, owner, specificTarget);

        // Create game actions for each target
        foreach (var target in targets) {
            CreateGameAction(spellAction.ActionType, spellAction.Value, target, owner, context);
        }
    }

    private List<ITarget> DetermineTargets(TargetType targetType, IPlayer owner, ITarget specificTarget) {
        // If a specific target was provided when playing the card, use it for ALL_TARGETS type
        if (specificTarget != null && targetType == TargetType.AllCreatures) {
            return new List<ITarget> { specificTarget };
        }

        // Otherwise use the targeting system to get valid targets
        return TargetingSystem.GetValidTargets(owner, targetType);
    }

    private void CreateGameAction(ActionType actionType, int value, ITarget target, IPlayer owner, ActionsQueue context) {
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
        context.AddAction(new DrawCardAction(player, value));
    }
}

// Simple container for spell action data
public class SpellAction {
    public ActionType ActionType { get; }
    public int Value { get; }
    public TargetType TargetType { get; }

    public SpellAction(ActionType actionType, int value, TargetType targetType) {
        ActionType = actionType;
        Value = value;
        TargetType = targetType;
    }
}