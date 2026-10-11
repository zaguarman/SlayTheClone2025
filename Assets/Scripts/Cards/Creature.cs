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

        /// <summary>
        /// Turns left before the creature is operative. Until then it can't attack or move by itself (another creature
        /// can still swap with it) and its effects are off; its on-play effect waits for the moment it becomes operative.
        /// </summary>
        int TurnsUntilOperative { get; }
        bool IsOperative { get; }
        /// <summary>Called when the creature lands on the board; its countdown starts from this turn.</summary>
        void EnterPlay(int turn);
        /// <summary>Counts down at the start of each turn after the one it landed in.</summary>
        /// <returns>True when the countdown changed.</returns>
        bool AdvanceReadiness(int startingTurn, IActionsQueue actionsQueue);

        /// <summary>Queues what this creature's effects with the trigger do, if it is operative.</summary>
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

        public int TurnsUntilOperative { get; private set; }
        public bool IsOperative => TurnsUntilOperative == 0;
        private int landedOnTurn;

        public Creature(string name, int attack, int health, int speed, string cardId, int turnsUntilOperative = 0) : base(name, cardId) {
            BaseAttack = attack;
            BaseHealth = health;
            BaseSpeed = Math.Max(1, speed);
            currentHealth = health;
            effectiveAttack = attack;
            effectiveMaxHealth = health;
            effectiveSpeed = BaseSpeed;
            CurrentArmorPool = 0;
            IsDead = false;
            TurnsUntilOperative = Math.Max(0, turnsUntilOperative);
        }

        public void EnterPlay(int turn) {
            landedOnTurn = turn;
        }

        public bool AdvanceReadiness(int startingTurn, IActionsQueue actionsQueue) {
            if (IsOperative || IsDead || startingTurn <= landedOnTurn) return false;

            TurnsUntilOperative--;
            Log($"{Name} is {(IsOperative ? "now operative" : $"operative in {TurnsUntilOperative} turn(s)")}", LogTag.Creatures | LogTag.Turns);
            if (IsOperative) {
                HandleEffect(EffectTrigger.OnPlay, actionsQueue); // Its on-play effect waited for this moment
            }
            return true;
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

        // Fires every time the trigger happens; ActionsQueue.MaxActionsPerResolution stops effects that trigger each other endlessly
        public void HandleEffect(EffectTrigger trigger, IActionsQueue actionsQueue, ICreature triggeringAttacker = null) {
            if (!IsOperative) return; // Its effects are off until it is operative
            if (IsDead && trigger != EffectTrigger.OnDeath) return;
            if (actionsQueue == null) {
                LogError($"Cannot handle effect {trigger} for {Name} - no ActionsQueue.", LogTag.Effects | LogTag.Creatures);
                return;
            }
            Log($"Handling Effect: {trigger} for {Name} (TargetID: {TargetId.ShortId()})", LogTag.Creatures | LogTag.Effects);
            EffectResolver.QueueCreatureEffects(this, trigger, actionsQueue, triggeringAttacker);
        }
    }
}
