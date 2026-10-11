using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    public class Spell : Card {
        private readonly List<SpellAction> actions = new List<SpellAction>();
        private readonly TargetType defaultTargetType;

        public TargetType DefaultTargetType => defaultTargetType;

        public Spell(string name, TargetType defaultTargetType, string cardId = null) : base(name, cardId) {
            this.defaultTargetType = defaultTargetType;
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

        public override void Play(IPlayer owner, IActionsQueue context, ITarget target = null) {
            Log($"Playing spell {Name} (TargetID: {TargetId.ShortId()}) with {actions.Count} actions", LogTag.Cards | LogTag.Actions);

            if (target == null) {
                target = GetDefaultTarget(owner);
            }

            foreach (var action in actions) {
                CreateGameActionFromSpell(action, target, owner, context);
            }
        }

        private ITarget GetDefaultTarget(IPlayer owner) {
            if (defaultTargetType == TargetType.Player) return owner;
            if (defaultTargetType == TargetType.Enemy) return owner.Opponent;

            TargetModifier targetModifier = actions.Count > 0 ? actions[0].TargetModifier : TargetModifier.None;
            return TargetingSystem.GetValidTargets(owner, defaultTargetType, targetModifier).FirstOrDefault();
        }

        private void CreateGameActionFromSpell(SpellAction spellAction, ITarget target, IPlayer owner, IActionsQueue context) {
            TargetModifier targetModifier = spellAction.TargetModifier;

            var targetsToProcess = new List<ITarget>();
            if (target == null || targetModifier.HasFlag(TargetModifier.Random) || targetModifier.HasFlag(TargetModifier.AllSameTypeTargets)) {
                targetsToProcess.AddRange(TargetingSystem.GetValidTargets(owner, spellAction.TargetType, targetModifier));
                Log($"Spell action resolved to {targetsToProcess.Count} targets for {spellAction.TargetType}/{targetModifier}.", LogTag.Actions | LogTag.Effects);
            }
            else {
                var resolvedTarget = ResolveTarget(target, spellAction.ActionType);
                if (resolvedTarget == null) {
                    Log($"Spell {Name}: {spellAction.ActionType} cannot apply to {target.GetType().Name}; it does nothing.", LogTag.Actions | LogTag.Effects);
                    return;
                }
                targetsToProcess.Add(resolvedTarget);
            }

            foreach (var currentTarget in targetsToProcess) {
                switch (spellAction.ActionType) {
                    case ActionType.Damage:
                        QueueForTarget(TargetedActions.Damage(currentTarget, spellAction.Value), currentTarget, context, "Damage");
                        break;
                    case ActionType.Heal:
                        QueueForTarget(TargetedActions.Heal(currentTarget, spellAction.Value), currentTarget, context, "Heal");
                        break;
                    case ActionType.Draw:
                        if (currentTarget is IPlayer drawingPlayer) {
                            QueueForTarget(new DrawCardsAction(drawingPlayer, spellAction.Value), currentTarget, context, "Draw");
                        }
                        break;
                    case ActionType.ModifyStat:
                        var modifyAction = currentTarget is ICreature buffed
                            ? new ModifyAction(buffed, spellAction.Value, spellAction.ModifyAttack, spellAction.ModifyHealth, spellAction.ModifySpeed)
                            : null;
                        QueueForTarget(modifyAction, currentTarget, context, "ModifyStat");
                        break;
                    case ActionType.ApplyStatus:
                        QueueStatus(currentTarget, context, spellAction.StatusEffectToApply, spellAction.StatusDuration, spellAction.StatusPotency);
                        break;
                    case ActionType.Stun: // A stun paralyzes for its value in turns
                        QueueStatus(currentTarget, context, StatusEffectType.Paralyzed, spellAction.Value, 0);
                        break;
                    case ActionType.Armor:
                        var armorAction = currentTarget is ICreature armored ? new ModifyArmorAction(armored, spellAction.Value) : null;
                        QueueForTarget(armorAction, currentTarget, context, "ModifyArmor");
                        break;
                    case ActionType.Summon:
                        if (currentTarget is IPlayer summoningPlayer) {
                            SummonFromDeckEffect.QueueRandomSummons(summoningPlayer, spellAction.Value, context);
                        }
                        break;
                }
            }
        }

        // A spell dropped on a battlefield slot hits the slot's creature when the action can affect creatures,
        // otherwise the player who owns the slot. Returns null when the action applies to neither (the spell does nothing).
        private static ITarget ResolveTarget(ITarget target, ActionType actionType) {
            switch (target) {
                case BoardSlot slot:
                    if (slot.OccupyingCreature != null && CanAffectCreatures(actionType)) return slot.OccupyingCreature;
                    return CanAffectPlayers(actionType) ? slot.Owner : null;
                case ICreature:
                    return CanAffectCreatures(actionType) ? target : null;
                case IPlayer:
                    return CanAffectPlayers(actionType) ? target : null;
                default:
                    return null;
            }
        }

        private static bool CanAffectCreatures(ActionType actionType) =>
            actionType is ActionType.Damage or ActionType.Heal or ActionType.ModifyStat
                or ActionType.ApplyStatus or ActionType.Armor or ActionType.Stun;

        private static bool CanAffectPlayers(ActionType actionType) =>
            actionType is ActionType.Damage or ActionType.Heal or ActionType.Draw or ActionType.Summon;

        private void QueueStatus(ITarget target, IActionsQueue context, StatusEffectType statusType, int duration, int potency) {
            var statusAction = target is ICreature creature ? new ApplyStatusEffectAction(creature, statusType, duration, potency) : null;
            QueueForTarget(statusAction, target, context, "ApplyStatus");
        }

        // A null action means the target can't receive this kind of effect
        private void QueueForTarget(IGameAction action, ITarget target, IActionsQueue context, string effectName) {
            if (action == null) {
                LogWarning($"Spell {effectName}: Invalid target type {target?.GetType().Name}", LogTag.Actions);
                return;
            }

            Log($"Spell {Name}: Queueing {action}", LogTag.Actions);
            context.AddAction(action);
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

        public SpellAction(ActionType actionType, int value, TargetType targetType, TargetModifier modifier = TargetModifier.None)
            : this(actionType, value, targetType, modifier, false, false, false, StatusEffectType.None, 0, 0) { }

        public SpellAction(ActionType actionType, int value, TargetType targetType, bool modifyAttack, bool modifyHealth, bool modifySpeed = false, TargetModifier modifier = TargetModifier.None)
            : this(actionType, value, targetType, modifier, modifyAttack, modifyHealth, modifySpeed, StatusEffectType.None, 0, 0) {
            if (actionType != ActionType.ModifyStat) throw new System.ArgumentException("Incorrect constructor for non-ModifyStat action.");
        }

        public SpellAction(ActionType actionType, TargetType targetType, StatusEffectType status, int duration, int potency, TargetModifier modifier = TargetModifier.None)
            : this(actionType, 0, targetType, modifier, false, false, false, status, duration, potency) {
            if (actionType != ActionType.ApplyStatus) throw new System.ArgumentException("Incorrect constructor for non-ApplyStatus action.");
        }

        private SpellAction(ActionType actionType, int value, TargetType targetType, TargetModifier modifier,
            bool modifyAttack, bool modifyHealth, bool modifySpeed,
            StatusEffectType status, int duration, int potency) {
            ActionType = actionType;
            Value = value;
            TargetType = targetType;
            TargetModifier = modifier;
            ModifyAttack = modifyAttack;
            ModifyHealth = modifyHealth;
            ModifySpeed = modifySpeed;
            StatusEffectToApply = status;
            StatusDuration = duration;
            StatusPotency = potency;
        }
    }
}
