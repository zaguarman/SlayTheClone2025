using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Persistent Stat Modifications (GDD glossary): base stat changes that stay on the card through the hand and the
        /// discard pile, until it's reshuffled into the deck. Use ModifierManager.ChangeBaseStats on a creature in play.
        /// </summary>
        (int attack, int health, int speed) PersistentChanges { get; }
        void ChangeBaseStats(int attack, int health, int speed);
        BoardSlot Slot { get; set; }
        /// <summary>Its Movement Keywords (GDD 2.4): how it can move besides walking and pushing.</summary>
        MovementKeyword Movement { get; }
        /// <summary>
        /// Its one ability ('Abilities Compendium'). A gained or lost ability stays on the card, through the discard pile,
        /// until it's reshuffled, like Persistent Stat Modifications. Whether it's switched on: see <see cref="Abilities.IsActive"/>.
        /// </summary>
        CreatureAbility Ability { get; set; }
        /// <summary>Its Heat Signature stacks, kept by the ModifierManager (0: none). Random and redirected attacks go to the hottest.</summary>
        int Heat { get; set; }
        /// <summary>
        /// Fog of War: the players besides its owner who know what it is. It enters play unknown; it shows itself when its
        /// summon finishes, unless it's stealthy, and a reveal shows it. Forgotten when it leaves play.
        /// </summary>
        ISet<IPlayer> KnownTo { get; }
        /// <summary>Set by the ModifierManager while a status (like Exhausted) disables its abilities: none of its effects trigger.</summary>
        bool AbilitiesDisabled { get; set; }
        /// <summary>The slot it was last in, kept after it leaves play (where effects homing on it land then).</summary>
        BoardSlot LastSlot { get; }
        /// <summary>How many times it has landed on the board this combat.</summary>
        int TimesEnteredPlay { get; }
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
        /// <summary>
        /// Red Tape: adds Days to its Deployment Time, counting down from the next Dusk after this one, like a fresh arrival.
        /// Its entry effects already happened, so they don't happen again when it's ready.
        /// </summary>
        void AddDeploymentTime(int days, int currentTurn);
        /// <summary>Counts down at each Dusk after the Day it landed in (GDD 3.2): Deployment Time N sits out N full Days.</summary>
        /// <returns>True when the countdown changed.</returns>
        bool AdvanceReadiness(int startingTurn, IActionsQueue actionsQueue);
        /// <summary>Scorched Earth held its entry effects back: they happen now if it no longer stands on Scorched Earth
        /// (it moved off, or the effect ended).</summary>
        void ReleaseHeldEntryEffects(IActionsQueue actionsQueue);

        /// <summary>
        /// Raised after something its card shows changes: health, armor, stats, statuses, readiness or death.
        /// The mediator raises it along with the matching game-wide event, so a card can listen to its own creature only.
        /// </summary>
        event Action<ICreature> Changed;
        void NotifyChanged();

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
        public (int attack, int health, int speed) PersistentChanges { get; private set; }

        // The effective stats follow once the ModifierManager recalculates them (it does when the creature enters play)
        public void ChangeBaseStats(int attack, int health, int speed) {
            BaseAttack = Math.Max(0, BaseAttack + attack);
            BaseHealth = Math.Max(0, BaseHealth + health);
            BaseSpeed = Math.Max(0, BaseSpeed + speed);
            PersistentChanges = (PersistentChanges.attack + attack, PersistentChanges.health + health, PersistentChanges.speed + speed);
        }

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
        private BoardSlot slot;
        public BoardSlot Slot {
            get => slot;
            set {
                slot = value;
                if (value != null) LastSlot = value;
            }
        }
        public BoardSlot LastSlot { get; private set; }
        public bool AbilitiesDisabled { get; set; }
        public MovementKeyword Movement { get; internal set; }
        public CreatureAbility Ability { get; set; }
        public int Heat { get; set; }
        public ISet<IPlayer> KnownTo { get; } = new HashSet<IPlayer>();
        public int TimesEnteredPlay { get; private set; }

        public int TurnsUntilOperative { get; private set; }
        public bool IsOperative => TurnsUntilOperative == 0;
        private int landedOnTurn;
        private bool entryEffectsPending; // Its entry ("When Ready") effects haven't happened yet since it entered play
        private bool entryEffectsHeld; // Scorched Earth stopped them: they wait until it's off the scorched slot

        public event Action<ICreature> Changed;
        public void NotifyChanged() => Changed?.Invoke(this);

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
            KnownTo.Clear(); // A new entrance: nobody knows what it is until it shows itself
            TimesEnteredPlay++;
            entryEffectsPending = true;
            entryEffectsHeld = false;
            CurrentArmorPool = 0; // Armor is lost when a creature leaves play (GDD 1.1 Armor), so it always enters without any
        }

        public bool AdvanceReadiness(int endedTurn, IActionsQueue actionsQueue) {
            if (IsOperative || IsDead || endedTurn <= landedOnTurn) return false;

            TurnsUntilOperative--;
            Log($"{Name} is {(IsOperative ? "now operative" : $"operative in {TurnsUntilOperative} turn(s)")}", LogTag.Creatures | LogTag.Turns);
            if (IsOperative && entryEffectsPending) {
                // Its on-play effect waited for this moment; Deployment Time ignores Abilities Disabled (GDD 3.2). On Scorched
                // Earth it waits until the creature is off the scorched slot
                entryEffectsPending = false;
                if (SlotEffectRules.SuppressesTriggeredEffects(this)) HoldEntryEffects();
                else EffectResolver.QueueCreatureEffects(this, EffectTrigger.OnPlay, actionsQueue);
            }
            return true;
        }

        private void HoldEntryEffects() {
            entryEffectsHeld = true;
            Log($"{Name}'s entry effects wait until it's off Scorched Earth", LogTag.Creatures | LogTag.Effects);
        }

        public void ReleaseHeldEntryEffects(IActionsQueue actionsQueue) {
            if (!entryEffectsHeld || IsDead || actionsQueue == null || SlotEffectRules.SuppressesTriggeredEffects(this)) return;
            entryEffectsHeld = false;
            Log($"{Name} is off Scorched Earth: its entry effects happen now", LogTag.Creatures | LogTag.Effects);
            HandleEffect(EffectTrigger.OnPlay, actionsQueue);
        }

        public void AddDeploymentTime(int days, int currentTurn) {
            if (days <= 0) return;
            TurnsUntilOperative += days;
            landedOnTurn = Math.Max(landedOnTurn, currentTurn); // Not counted down at this Day's Dusk
        }

        public void UpdateEffectiveStats(int newEffectiveAttack, int newEffectiveMaxHealth, int newEffectiveSpeed) {
            int oldMaxHealth = effectiveMaxHealth;

            effectiveAttack = Math.Max(0, newEffectiveAttack);
            effectiveMaxHealth = Math.Max(0, newEffectiveMaxHealth);
            effectiveSpeed = Math.Max(0, newEffectiveSpeed);

            // A Health change moves current Health with the maximum, both ways (GDD 3.3): +2 then losing it takes the 2 back,
            // which can bring the creature to 0. Whoever changed the stat checks for that (see CheckLethalAction).
            if (!IsDead) {
                currentHealth += effectiveMaxHealth - oldMaxHealth;
            }

            currentHealth = Math.Clamp(currentHealth, 0, effectiveMaxHealth);
            CurrentArmorPool = Math.Min(CurrentArmorPool, MaxArmor); // A lower maximum lowers the Armor cap too
        }

        // Armor can't exceed twice the creature's maximum Health; Armor gained above that is lost (GDD 1.1 Armor)
        public int MaxArmor => 2 * MaxHealth;

        public void ModifyArmorPool(int amount) {
            CurrentArmorPool = Math.Clamp(CurrentArmorPool + amount, 0, MaxArmor);
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
            if (AbilitiesDisabled) return; // ...and while a status disables them, retaliation and death effects included (GDD 3.9)
            if (IsDead && trigger != EffectTrigger.OnDeath) return;
            if (actionsQueue == null) {
                LogError($"Cannot handle effect {trigger} for {Name} - no ActionsQueue.", LogTag.Effects | LogTag.Creatures);
                return;
            }
            if (trigger == EffectTrigger.OnPlay) entryEffectsPending = false;
            // Scorched Earth stops all its triggered effects; entry ones wait until it's off the scorched slot
            if (SlotEffectRules.SuppressesTriggeredEffects(this)) {
                if (trigger == EffectTrigger.OnPlay) HoldEntryEffects();
                else Log($"{Name}'s {trigger} effects don't trigger on Scorched Earth", LogTag.Creatures | LogTag.Effects);
                return;
            }
            Log($"Handling Effect: {trigger} for {Name} (TargetID: {TargetId.ShortId()})", LogTag.Creatures | LogTag.Effects);
            EffectResolver.QueueCreatureEffects(this, trigger, actionsQueue, triggeringAttacker);
        }
    }
}
