using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

[assembly: InternalsVisibleTo("Tests")]

namespace SlayTheClone {
    public interface ICreature : ICard {
        int Attack { get; }
        int Health { get; }
        int Speed { get; }
        int MaxHealth { get; }
        int CurrentArmorPool { get; }
        int BaseAttack { get; }
        int BaseHealth { get; }
        int BaseSpeed { get; }
        BoardSlot Slot { get; set; }
        // Only changes health; death and its consequences are handled by the caller
        int TakeHealthDamage(int healthDamage); // Returns actual damage dealt
        void ModifyArmorPool(int amount);
        IPlayer Owner { get; }
        void SetOwner(IPlayer owner);
        void UpdateEffectiveStats(int newEffectiveAttack, int newEffectiveMaxHealth, int newEffectiveSpeed);
        bool IsDead { get; }
        void MarkAsDead();

        void HandleEffect(EffectTrigger trigger, ActionExecutionContext context);
        void HandleTurnBasedEffect(EffectTrigger trigger, IActionsQueue actionsQueue, IModifierManager modifierManager, ITurnManager turnManager);

        /// <summary>
        /// Heals the creature by the specified amount, up to its maximum health.
        /// </summary>
        /// <param name="amount">The amount to heal.</param>
        /// <returns>The actual amount the creature was healed.</returns>
        int Heal(int amount);
    }

    public class Creature : Card, ICreature {
        public int BaseAttack { get; private set; }
        public int BaseHealth { get; private set; }
        public int BaseSpeed { get; private set; }

        private int effectiveAttack;
        private int effectiveMaxHealth;
        private int effectiveSpeed;
        public int CurrentArmorPool { get; private set; }

        private int currentHealth;
        public bool IsDead { get; private set; }

        public int Attack => effectiveAttack;
        public int MaxHealth => effectiveMaxHealth;
        public int Speed => effectiveSpeed;
        public int Health => currentHealth;

        public IPlayer Owner { get; private set; }
        public BoardSlot Slot { get; set; }

        private IGameMediator gameMediator;

        public Creature(string name, int attack, int health, int speed, string cardId) : base(name, cardId) {
            BaseAttack = attack;
            BaseHealth = health;
            BaseSpeed = Math.Max(1, speed);
            currentHealth = health;
            effectiveAttack = attack;
            effectiveMaxHealth = health;
            effectiveSpeed = BaseSpeed;
            CurrentArmorPool = 0;
            IsDead = false;
        }

        public void UpdateEffectiveStats(int newEffectiveAttack, int newEffectiveMaxHealth, int newEffectiveSpeed) {
            int oldMaxHealth = effectiveMaxHealth;

            effectiveAttack = Math.Max(0, newEffectiveAttack);
            effectiveMaxHealth = Math.Max(1, newEffectiveMaxHealth);
            effectiveSpeed = Math.Max(0, newEffectiveSpeed);

            // A living creature gains the extra max health as current health
            int healthIncrease = effectiveMaxHealth - oldMaxHealth;
            if (healthIncrease > 0 && !IsDead) {
                currentHealth += healthIncrease;
            }

            currentHealth = Math.Clamp(currentHealth, 0, effectiveMaxHealth);
        }

        public void ModifyArmorPool(int amount) {
            CurrentArmorPool = Math.Max(0, CurrentArmorPool + amount);
        }

        public void SetOwner(IPlayer owner) {
            Owner = owner;
            if (owner?.GameMediator != null) {
                gameMediator = owner.GameMediator;
            }
        }

        public override void Play(IPlayer owner, IActionsQueue context, ITarget target = null) {
            Log($"Playing {Name} (TargetID: {TargetId.ToUpper()}) with {Effects.Count} effects", LogTag.Creatures | LogTag.Cards | LogTag.Actions);
            Owner = owner;

            bool fromHand = owner?.Hand.Contains(this) ?? false;
            context.AddAction(new SummonCreatureAction(this, owner, target, !fromHand));
        }

        public int TakeHealthDamage(int healthDamage) {
            if (IsDead || healthDamage <= 0) return 0;

            int previousHealth = currentHealth;
            int actualDamageDealt = Math.Min(healthDamage, currentHealth);
            currentHealth -= actualDamageDealt;

            Log($"Creature {Name} (ID: {TargetId.ShortId()}) took {actualDamageDealt} health damage. Health: {previousHealth} -> {Health}.", LogTag.Creatures | LogTag.Combat | LogTag.Effects);
            return actualDamageDealt;
        }

        public void MarkAsDead() {
            if (IsDead) return;

            IsDead = true;
            Log($"Creature marked as dead: {Name} (TargetID: {TargetId.ToUpper()})", LogTag.Creatures);
        }

        /// <summary>
        /// Heals the creature by the specified amount, up to its maximum health.
        /// </summary>
        /// <param name="amount">The amount to heal.</param>
        /// <returns>The actual amount the creature was healed.</returns>
        public int Heal(int amount) {
            if (IsDead || amount <= 0) return 0;

            int previousHealth = currentHealth;
            int actualHealAmount = Math.Min(amount, MaxHealth - currentHealth);
            if (actualHealAmount <= 0) return 0; // Already at full health

            currentHealth += actualHealAmount;
            Log($"Creature {Name} healed for {actualHealAmount} (requested {amount}). Health: {previousHealth} -> {Health}/{MaxHealth}",
                LogTag.Creatures | LogTag.Effects);

            // HealCreatureActionExecutor notifies the mediator
            return actualHealAmount;
        }

        // Effects triggered while actions resolve
        public void HandleEffect(EffectTrigger trigger, ActionExecutionContext context) {
            if (IsDead && trigger != EffectTrigger.OnDeath) return;

            if (context == null) {
                LogError($"Cannot handle effect {trigger} for {Name} - ActionExecutionContext is null.", LogTag.Effects | LogTag.Creatures);
                return;
            }
            if (context.ActionsQueue == null) {
                LogError($"Cannot handle effect {trigger} for {Name} - ActionsQueue missing from context.", LogTag.Effects | LogTag.Creatures | LogTag.Initialization);
                return;
            }

            ProcessEffectsOnce(trigger, context, LogTag.Creatures | LogTag.Effects);
        }

        // Effects triggered by turn progression; builds its own context from the owner's systems
        public void HandleTurnBasedEffect(EffectTrigger trigger, IActionsQueue actionsQueue, IModifierManager modifierManager, ITurnManager turnManager) {
            if (IsDead) return;

            if (actionsQueue == null || modifierManager == null || turnManager == null) {
                LogError($"Cannot handle turn effect {trigger} for {Name} - Missing dependencies.", LogTag.Effects | LogTag.Creatures | LogTag.Initialization);
                return;
            }

            var ownerManager = Owner?.GameManager;
            if (ownerManager == null) {
                LogError($"Cannot handle turn effect {trigger} for {Name} - Owner or Owner.GameManager is null.", LogTag.Effects | LogTag.Creatures | LogTag.Initialization);
                return;
            }

            var references = ownerManager.GameReferences;
            var cardDealingService = ownerManager.CardDealingService;
            var weatherSystem = ownerManager.WeatherSystem;
            var combatHandler = ownerManager.CombatHandler;
            var factory = modifierManager.ModifierFactory;
            if (references == null || cardDealingService == null || weatherSystem == null || combatHandler == null || factory == null) {
                LogError($"Cannot handle turn effect {trigger} for {Name} - Failed to retrieve dependencies from GameManager.", LogTag.Effects | LogTag.Creatures | LogTag.Initialization);
                return;
            }

            if (actionsQueue.IsEffectProcessed(TargetId, trigger)) return;

            var turnContext = new ActionExecutionContext(
                gameMediator,
                references,
                cardDealingService,
                weatherSystem,
                modifierManager,
                factory,
                actionsQueue,
                combatHandler,
                turnManager,
                ownerManager);

            ProcessEffectsOnce(trigger, turnContext, LogTag.Creatures | LogTag.Effects | LogTag.Turns);
        }

        // Each trigger runs at most once per resolution (see ActionsQueue.IsEffectProcessed)
        private void ProcessEffectsOnce(EffectTrigger trigger, ActionExecutionContext context, LogTag tags) {
            var actionsQueue = context.ActionsQueue;
            if (actionsQueue.IsEffectProcessed(TargetId, trigger)) return;

            Log($"Handling Effect: {trigger} for {Name} (TargetID: {TargetId.ShortId()}) with {Effects.Count} effects", tags);
            ProcessEffects(trigger, context);

            actionsQueue.MarkEffectProcessed(TargetId, trigger);
            Log($"Marked Effect {trigger} as processed for {Name} (TargetID: {TargetId.ShortId()})", tags);
        }

        private void ProcessEffects(EffectTrigger trigger, ActionExecutionContext context) {
            var actionsQueue = context.ActionsQueue;
            var modifierManager = context.ModifierManager;
            var factory = context.ModifierFactory;
            var turnManager = context.TurnManager;
            if (actionsQueue == null || modifierManager == null || factory == null || turnManager == null) {
                LogError($"Internal Error: Missing core dependencies in context for ProcessEffects for {Name} ({trigger}).", LogTag.Effects | LogTag.Creatures | LogTag.Initialization);
                return;
            }

            foreach (var effect in Effects.Where(e => e.trigger == trigger).ToList()) {
                Log($"-- Processing Effect: Trigger={effect.trigger}, Type={effect.effectType}, Actions={effect.actions.Count}", LogTag.Effects);
                foreach (var action in effect.actions) {
                    Log($"---- Action: Type={action.actionType}, Target={action.targetType}, Value={action.value}, Status={action.statusEffectToApply}", LogTag.Effects);

                    switch (action.actionType) {
                        case ActionType.Damage:
                            ProcessDamageEffect(action, trigger, context);
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
                            ProcessModifyStatEffect(action, modifierManager, factory, turnManager);
                            break;
                        case ActionType.ApplyStatus:
                            ProcessApplyStatusEffect(action, action.statusEffectToApply, action.statusDuration, action.statusPotency, modifierManager, factory, turnManager);
                            break;
                        case ActionType.Stun: // A stun paralyzes for its value in turns
                            ProcessApplyStatusEffect(action, StatusEffectType.Paralyzed, action.value, 0, modifierManager, factory, turnManager);
                            break;
                        case ActionType.Armor:
                            ProcessModifyArmorEffect(action, actionsQueue);
                            break;
                    }
                }
            }
        }

        // The targets of one of this creature's effect actions. Callers check Owner for non-self targets.
        private List<ITarget> ResolveEffectTargets(EffectAction action) {
            if (action.targetType == TargetType.Self) return new List<ITarget> { this };
            return TargetingSystem.GetValidTargets(Owner, action.targetType, action.targetModifier);
        }

        private bool CanTargetOthers(EffectAction action, string effectName) {
            if (Owner != null || action.targetType == TargetType.Self) return true;
            LogError($"{effectName}: Cannot target others for {Name} - Owner is null and not self.", LogTag.Effects);
            return false;
        }

        private void ProcessDamageEffect(EffectAction action, EffectTrigger trigger, ActionExecutionContext context) {
            if (!CanTargetOthers(action, "Damage Effect")) return;

            // Chain damage is dealt by BattlefieldCombatActionExecutor when this creature attacks
            if (action.targetModifier == TargetModifier.Chained) {
                Log($"Skipping Chained effect in ProcessDamageEffect for {Name} - now handled by BattlefieldCombatActionExecutor", LogTag.Effects | LogTag.Combat);
                return;
            }

            List<ITarget> targets;
            ICreature originalAttacker = context.TriggeringAttacker;
            // Retaliation: an enemy-targeted OnDamage effect hits whoever damaged this creature
            if (trigger == EffectTrigger.OnDamage && originalAttacker != null && action.targetType == TargetType.Enemy) {
                targets = new List<ITarget> { originalAttacker };
                Log($"Retaliation Damage Effect: Targeting attacker {originalAttacker.Name}", LogTag.Effects | LogTag.Combat);
            }
            else {
                targets = ResolveEffectTargets(action);
            }

            Log($"Damage Effect ({Name}): Found {targets.Count} target(s) for {action.targetType}/{action.targetModifier}.", LogTag.Effects);
            foreach (var target in targets) {
                // Damage to players from effects carries no source, so it isn't ordered by this creature's speed
                var source = target is ICreature ? this : null;
                var damageAction = TargetedActions.Damage(target, action.value, source);
                if (damageAction == null) continue;

                Log($"Queueing {damageAction.GetType().Name}: Damage={action.value}, Source={Name}", LogTag.Effects | LogTag.Actions);
                context.ActionsQueue.AddAction(damageAction);
            }
        }

        private void ProcessHealEffect(EffectAction action, IActionsQueue actionsQueue) {
            if (!CanTargetOthers(action, "Heal Effect")) return;

            var targets = ResolveEffectTargets(action);
            Log($"Heal Effect: Found {targets.Count} targets for {action.targetType}/{action.targetModifier}.", LogTag.Effects);
            foreach (var target in targets) {
                var healAction = TargetedActions.Heal(target, action.value);
                if (healAction != null) actionsQueue.AddAction(healAction);
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

            IPlayer targetPlayer = Owner;
            if (action.targetType == TargetType.Enemy) {
                targetPlayer = Owner.Opponent;
            }
            else if (action.targetType != TargetType.Player && action.targetType != TargetType.FriendlyCreatures) {
                LogWarning($"Unexpected target type for summon effect: {action.targetType}", LogTag.Effects);
                return;
            }

            SummonFromDeckEffect.QueueRandomSummons(targetPlayer, action.value, actionsQueue);
        }

        // Modifiers are applied directly (not queued)
        private void ProcessModifyStatEffect(EffectAction action, IModifierManager manager, IModifierFactory factory, ITurnManager turnManager) {
            if (Owner == null && action.targetType != TargetType.Self) return;

            foreach (var target in ResolveEffectTargets(action)) {
                if (target is Creature creatureTarget) {
                    Log($"Applying Stat Modifier to {creatureTarget.Name}: A={action.modifyAttack}, H={action.modifyHealth}, S={action.modifySpeed}, Val={action.value}", LogTag.Effects);
                    StatModifiers.Apply(manager, factory, creatureTarget, action.value,
                        action.modifyAttack, action.modifyHealth, action.modifySpeed,
                        ModifierCalculationType.Flat, duration: 0, turnManager.TurnNumber);
                }
            }
        }

        private void ProcessApplyStatusEffect(EffectAction action, StatusEffectType statusType, int duration, int potency,
            IModifierManager manager, IModifierFactory factory, ITurnManager turnManager) {
            if (!CanTargetOthers(action, "ApplyStatus Modifier")) return;

            if (statusType == StatusEffectType.None || duration <= 0) {
                LogWarning($"ApplyStatus Modifier: Invalid status type ({statusType}) or duration ({duration}) for action.", LogTag.Effects);
                return;
            }

            var targets = ResolveEffectTargets(action);
            Log($"ApplyStatus Modifier: Found {targets.Count} targets for {action.targetType}/{action.targetModifier}.", LogTag.Effects);

            foreach (var target in targets) {
                if (target is Creature creatureTarget) {
                    Log($"Applying Status Modifier to {creatureTarget.Name}: Type={statusType}, Dur={duration}, Pot={potency}", LogTag.Effects);
                    IModifier statusModifier = factory.CreateStatusEffectModifier(
                        $"{statusType} Effect ({Name})",
                        $"Applies {statusType} for {duration} turns (Potency: {potency})",
                        statusType,
                        duration,
                        potency,
                        turnManager.TurnNumber);
                    manager.ApplyModifier(creatureTarget, statusModifier);
                }
            }
        }

        private void ProcessModifyArmorEffect(EffectAction action, IActionsQueue actionsQueue) {
            if (!CanTargetOthers(action, "ModifyArmor Action")) return;
            if (action.value == 0) return;

            var targets = ResolveEffectTargets(action);
            Log($"ModifyArmor Action: Found {targets.Count} targets for {action.targetType}/{action.targetModifier}.", LogTag.Effects);

            foreach (var target in targets) {
                if (target is ICreature creatureTarget) {
                    actionsQueue.AddAction(new ModifyArmorAction(creatureTarget, action.value));
                }
            }
        }
    }
}
