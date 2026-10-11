using System;
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

        /// <summary>Queues what this creature's effects with the trigger do, at most once per resolution.</summary>
        /// <param name="triggeringAttacker">Whoever damaged or killed it, for retaliation.</param>
        void HandleEffect(EffectTrigger trigger, IActionsQueue actionsQueue, ICreature triggeringAttacker = null);

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

        // Each trigger runs at most once per resolution (see ActionsQueue.IsEffectProcessed)
        public void HandleEffect(EffectTrigger trigger, IActionsQueue actionsQueue, ICreature triggeringAttacker = null) {
            if (IsDead && trigger != EffectTrigger.OnDeath) return;
            if (actionsQueue == null) {
                LogError($"Cannot handle effect {trigger} for {Name} - no ActionsQueue.", LogTag.Effects | LogTag.Creatures);
                return;
            }
            if (actionsQueue.IsEffectProcessed(TargetId, trigger)) return;

            Log($"Handling Effect: {trigger} for {Name} (TargetID: {TargetId.ShortId()})", LogTag.Creatures | LogTag.Effects);
            EffectResolver.QueueCreatureEffects(this, trigger, actionsQueue, triggeringAttacker);
            actionsQueue.MarkEffectProcessed(TargetId, trigger);
        }
    }
}
