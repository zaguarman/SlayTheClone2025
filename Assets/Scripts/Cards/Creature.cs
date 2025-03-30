using static DebugLogger;
using static Enums;
using System.Linq;

public interface ICreature : ICard {
    int Attack { get; }
    int Health { get; }
    BattlefieldSlot Slot { get; set; }
    void TakeDamage(int damage);
    IPlayer Owner { get; }
    void SetOwner(IPlayer owner);
}

public class Creature : Card, ICreature {
    public int Attack { get; private set; }
    public int Health { get; private set; }
    private bool isDead = false;
    public IPlayer Owner { get; private set; }
    public BattlefieldSlot Slot { get; set; }

    private ICreature lastAttacker;

    public Creature(string name, int attack, int health) : base(name) {
        Attack = attack;
        Health = health;
    }

    public void SetOwner(IPlayer owner) {
        Owner = owner;
    }

    public override void Play(IPlayer owner, ActionsQueue context, ITarget target = null) {
        Log($"Playing {Name} with {Effects.Count} effects", LogTag.Creatures | LogTag.Cards | LogTag.Actions);
        Owner = owner;
        context.AddAction(new SummonCreatureAction(this, owner, target));
    }

    public void TakeDamage(int damage) {
        TakeDamage(damage, null);
    }

    internal void TakeDamage(int damage, ICreature attacker) {
        if (isDead) return;

        lastAttacker = attacker;
        Health = System.Math.Max(0, Health - damage);
        Log($"{Name} took {damage} damage, health now: {Health}. Has {Effects.Count} effects",
            LogTag.Creatures | LogTag.Combat);

        var gameManager = GameManager.Instance;
        if (gameManager?.ActionsQueue != null) {
            Log($"Processing OnDamage effects for {Name}", LogTag.Creatures | LogTag.Effects);
            HandleEffect(EffectTrigger.OnDamage, gameManager.ActionsQueue);
        }

        if (Health <= 0 && !isDead) {
            isDead = true;
            Owner?.RemoveFromBattlefield(this);
            Log($"Creature died: {Name}", LogTag.Creatures);
            GameMediator.Instance?.NotifyCreatureDied(this);
        }

        GameMediator.Instance?.NotifyCreatureDamaged(this, damage);
        lastAttacker = null;
    }

    public void HandleEffect(EffectTrigger trigger, ActionsQueue actionsQueue) {
        Log($"Checking effects for {Name} (ID: {TargetId}) with trigger: {trigger}", LogTag.Effects);
        Log($"Effects count: {Effects.Count}", LogTag.Effects);

        foreach (var effect in Effects) {
            Log($"Effect found: {effect.trigger}", LogTag.Effects);
        }

        // Skip if this effect was already handled in current resolution chain
        if (actionsQueue.IsEffectProcessed(TargetId, trigger)) {
            Log($"Skipping already processed {trigger} effect for {Name}", LogTag.Effects);
            return;
        }

        Log($"Handling {trigger} effect for {Name} with {Effects.Count} effects", LogTag.Creatures | LogTag.Effects);

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
                        // Additional cases can be added for other action types
                }
            }
        }

        actionsQueue.MarkEffectProcessed(TargetId, trigger);
        Log($"Marked {trigger} effect as processed for {Name}", LogTag.Effects);
    }

    private void ProcessDamageEffect(EffectAction action, ActionsQueue actionsQueue) {
        if (Owner == null) {
            LogError($"Cannot handle damage effect for {Name} - Owner is null", LogTag.Creatures | LogTag.Effects);
            return;
        }

        Log($"Processing damage effect for {Name}. TargetType: {action.targetType}, Damage: {action.value}",
            LogTag.Creatures | LogTag.Actions);

        // Handle retaliatory damage
        if (lastAttacker != null && action.targetType == TargetType.AllCreatures && Effects.Any(e => e.trigger == EffectTrigger.OnDamage)) {
            Log($"Targeting attacker {lastAttacker.Name} for retaliation damage",
                LogTag.Creatures | LogTag.Actions);
            actionsQueue.AddAction(new DirectDamageAction(lastAttacker, action.value, this));
            return;
        }

        // Handle normal targeting
        var targets = TargetingSystem.GetValidTargets(Owner, action.targetType);
        Log($"Found {targets.Count} targets for {Name}'s damage effect",
            LogTag.Creatures | LogTag.Actions);

        foreach (var target in targets) {
            if (target is BattlefieldSlot slot) {
                if (!slot.IsOccupied()) continue;
                Log($"Adding DirectDamageAction - Source: {Name}, Target: {slot.OccupyingCreature.Name}, Damage: {action.value}",
                    LogTag.Creatures | LogTag.Actions | LogTag.Combat);
                actionsQueue.AddAction(new DirectDamageAction(slot.OccupyingCreature, action.value, this));
            } else if (target is IPlayer player) {
                Log($"Adding DamagePlayerAction - Target: Player {(player.IsPlayer1() ? "1" : "2")}, Damage: {action.value}",
                    LogTag.Creatures | LogTag.Actions | LogTag.Players | LogTag.Combat);
                actionsQueue.AddAction(new DamagePlayerAction(player, action.value));
            } else if (target is ICreature creature) {
                Log($"Adding DirectDamageAction - Source: {Name}, Target: {creature.Name}, Damage: {action.value}",
                    LogTag.Creatures | LogTag.Actions | LogTag.Combat);
                actionsQueue.AddAction(new DirectDamageAction(creature, action.value, this));
            }
        }
    }

    private void ProcessHealEffect(EffectAction action, ActionsQueue actionsQueue) {
        if (Owner == null) {
            LogError($"Cannot handle heal effect for {Name} - Owner is null", LogTag.Creatures | LogTag.Effects);
            return;
        }

        var targets = TargetingSystem.GetValidTargets(Owner, action.targetType);
        Log($"Found {targets.Count} targets for {Name}'s heal effect",
            LogTag.Creatures | LogTag.Actions);

        foreach (var target in targets) {
            if (target is ICreature creature) {
                Log($"Adding HealCreatureAction - Target: {creature.Name}, Amount: {action.value}",
                    LogTag.Creatures | LogTag.Actions);
                actionsQueue.AddAction(new HealCreatureAction(creature, action.value));
            } else if (target is IPlayer player) {
                Log($"Adding HealPlayerAction - Target: Player {(player.IsPlayer1() ? "1" : "2")}, Amount: {action.value}",
                    LogTag.Creatures | LogTag.Actions | LogTag.Players);
                actionsQueue.AddAction(new HealPlayerAction(player, action.value));
            }
        }
    }

    private void ProcessDrawEffect(EffectAction action, ActionsQueue actionsQueue) {
        if (Owner == null) {
            LogError($"Cannot handle draw effect for {Name} - Owner is null", LogTag.Creatures | LogTag.Effects);
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
            Log($"Adding DrawCardAction - Player: {(targetPlayer.IsPlayer1() ? "1" : "2")}, Amount: {action.value}",
                LogTag.Creatures | LogTag.Actions | LogTag.Cards);
            actionsQueue.AddAction(new DrawCardAction(targetPlayer, action.value));
        }
    }
}