using System.Collections.Generic;
using static DebugLogger;
using static Enums;

public class Spell : Card {
    public int SpellPower { get; private set; }
    public TargetType DefaultTargetType { get; private set; }

    public Spell(string name, int spellPower, TargetType defaultTargetType) : base(name) {
        SpellPower = spellPower;
        DefaultTargetType = defaultTargetType;
    }

    public override void Play(IPlayer owner, ActionsQueue context, ITarget target = null) {
        Log($"Playing spell {Name} with {Effects.Count} effects", LogTag.Cards | LogTag.Actions);

        // Process each effect and create appropriate actions
        foreach (var effect in Effects) {
            ProcessSpellEffect(effect, owner, context, target);
        }
    }

    private void ProcessSpellEffect(CardEffect effect, IPlayer owner, ActionsQueue context, ITarget target) {
        Log($"Processing spell effect with trigger {effect.trigger}", LogTag.Cards | LogTag.Effects);

        foreach (var action in effect.actions) {
            // Determine targets based on action's target type or the provided target
            var targets = target != null
                ? new List<ITarget> { target }
                : TargetingSystem.GetValidTargets(owner, action.targetType);

            foreach (var actionTarget in targets) {
                CreateActionForTarget(action, actionTarget, owner, context);
            }
        }
    }

    private void CreateActionForTarget(EffectAction action, ITarget target, IPlayer owner, ActionsQueue context) {
        switch (action.actionType) {
            case ActionType.Damage:
                CreateDamageAction(action.value, target, owner, context);
                break;

            case ActionType.Heal:
                CreateHealAction(action.value, target, owner, context);
                break;

            case ActionType.Draw:
                CreateDrawAction(action.value, owner, context);
                break;

            case ActionType.Summon:
                // Summoning would be handled differently, not implemented in this prototype
                break;
        }
    }

    private void CreateDamageAction(int value, ITarget target, IPlayer owner, ActionsQueue context) {
        if (target is ICreature creature) {
            Log($"Creating direct damage action for {value} to creature {creature.Name}", LogTag.Cards | LogTag.Actions);
            context.AddAction(new DirectDamageAction(creature, value));
        } else if (target is IPlayer player) {
            Log($"Creating damage action for {value} to player {(player.IsPlayer1() ? "1" : "2")}", LogTag.Cards | LogTag.Actions);
            context.AddAction(new DamagePlayerAction(player, value));
        }
    }

    private void CreateHealAction(int value, ITarget target, IPlayer owner, ActionsQueue context) {
        // Heal action would be implemented here
        // Not included in this prototype
    }

    private void CreateDrawAction(int value, IPlayer owner, ActionsQueue context) {
        Log($"Creating draw action for {value} cards", LogTag.Cards | LogTag.Actions);
        context.AddAction(new DrawCardAction(owner, value));
    }
}