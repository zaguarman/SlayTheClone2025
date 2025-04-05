using static DebugLogger;
using static Enums;
using System.Linq;
using System;

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

    // Constructor with cardId parameter
    public Creature(string name, int attack, int health, string cardId) : base(name, cardId) {
        Attack = attack;
        Health = health;
    }

    public void SetOwner(IPlayer owner) {
        Owner = owner;
    }

    public override void Play(IPlayer owner, ActionsQueue context, ITarget target = null) {
        Log($"Playing {Name} (TargetID: {TargetId.ToUpper()}) with {Effects.Count} effects", LogTag.Creatures | LogTag.Cards | LogTag.Actions);
        Owner = owner;

        // Check if the creature is in the player's hand to determine if we need to remove it from the deck
        bool fromHand = owner.Hand.Contains(this);

        context.AddAction(new SummonCreatureAction(this, owner, target, fromHand));
    }

    public void TakeDamage(int damage) {
        TakeDamage(damage, null);
    }

    internal void TakeDamage(int damage, ICreature attacker) {
        if (isDead) return;

        lastAttacker = attacker;
        Health = System.Math.Max(0, Health - damage);

        // Updated log format to include target IDs
        if (attacker != null) {
            Log($"{attacker.Name} (TargetID: {attacker.TargetId.ToUpper()}) dealt {damage} damage to {Name} (TargetID: {TargetId.ToUpper()}), health now: {Health}. Has {Effects.Count} effects",
                LogTag.Creatures | LogTag.Combat);
        } else {
            Log($"{Name} (TargetID: {TargetId.ToUpper()}) took {damage} damage, health now: {Health}. Has {Effects.Count} effects",
                LogTag.Creatures | LogTag.Combat);
        }

        var gameManager = GameManager.Instance;
        if (gameManager?.ActionsQueue != null) {
            Log($"Processing OnDamage effects for {Name} (TargetID: {TargetId.ToUpper()})", LogTag.Creatures | LogTag.Effects);
            HandleEffect(EffectTrigger.OnDamage, gameManager.ActionsQueue);
        }

        if (Health <= 0 && !isDead) {
            isDead = true;

            // Notify that the creature died (for event listeners)
            GameMediator.Instance?.NotifyCreatureDied(this);

            // Remove from battlefield (which will handle adding to discard pile)
            if (Owner != null) {
                // When a creature dies, we want to add it to the discard pile but not destroy the card
                Owner.RemoveFromBattlefield(this, false);
            }

            Log($"Creature died: {Name} (TargetID: {TargetId.ToUpper()})", LogTag.Creatures);
        }

        GameMediator.Instance?.NotifyCreatureDamaged(this, damage);
        lastAttacker = null;
    }

    public void HandleEffect(EffectTrigger trigger, ActionsQueue actionsQueue) {
        Log($"Checking effects for {Name} (TargetID: {TargetId.ToUpper()}) with trigger: {trigger}", LogTag.Effects);
        Log($"Effects count: {Effects.Count}", LogTag.Effects);

        foreach (var effect in Effects) {
            Log($"Effect found: {effect.trigger}", LogTag.Effects);
        }

        // Skip if this effect was already handled in current resolution chain
        if (actionsQueue.IsEffectProcessed(TargetId, trigger)) {
            Log($"Skipping already processed {trigger} effect for {Name} (TargetID: {TargetId.ToUpper()})", LogTag.Effects);
            return;
        }

        Log($"Handling {trigger} effect for {Name} (TargetID: {TargetId.ToUpper()}) with {Effects.Count} effects", LogTag.Creatures | LogTag.Effects);

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
                    case ActionType.Summon:
                        ProcessSummonEffect(action, actionsQueue);
                        break;
                        // Additional cases can be added for other action types
                }
            }
        }

        actionsQueue.MarkEffectProcessed(TargetId, trigger);
        Log($"Marked {trigger} effect as processed for {Name} (TargetID: {TargetId.ToUpper()})", LogTag.Effects);
    }

    private void ProcessDamageEffect(EffectAction action, ActionsQueue actionsQueue) {
        if (Owner == null) {
            LogError($"Cannot handle damage effect for {Name} (TargetID: {TargetId.ToUpper()}) - Owner is null", LogTag.Creatures | LogTag.Effects);
            return;
        }

        Log($"Processing damage effect for {Name} (TargetID: {TargetId.ToUpper()}). TargetType: {action.targetType}, Damage: {action.value}",
            LogTag.Creatures | LogTag.Actions);

        // Handle retaliatory damage
        if (lastAttacker != null && action.targetType == TargetType.AllCreatures && Effects.Any(e => e.trigger == EffectTrigger.OnDamage)) {
            Log($"Targeting attacker {lastAttacker.Name} (TargetID: {lastAttacker.TargetId.ToUpper()}) for retaliation damage",
                LogTag.Creatures | LogTag.Actions);
            actionsQueue.AddAction(new DirectDamageAction(lastAttacker, action.value, this));
            return;
        }

        // Handle normal targeting
        var targets = TargetingSystem.GetValidTargets(Owner, action.targetType);
        Log($"Found {targets.Count} targets for {Name} (TargetID: {TargetId.ToUpper()})'s damage effect",
            LogTag.Creatures | LogTag.Actions);

        foreach (var target in targets) {
            if (target is BattlefieldSlot slot) {
                if (!slot.IsOccupied()) continue;
                Log($"Adding DirectDamageAction - Source: {Name} (TargetID: {TargetId.ToUpper()}), Target: {slot.OccupyingCreature.Name} (TargetID: {slot.OccupyingCreature.TargetId.ToUpper()}), Damage: {action.value}",
                    LogTag.Creatures | LogTag.Actions | LogTag.Combat);
                actionsQueue.AddAction(new DirectDamageAction(slot.OccupyingCreature, action.value, this));
            } else if (target is IPlayer player) {
                Log($"Adding DamagePlayerAction - Source: {Name} (TargetID: {TargetId.ToUpper()}), Target: Player {(player.IsPlayer1() ? "1" : "2")} (TargetID: {player.TargetId.ToUpper()}), Damage: {action.value}",
                    LogTag.Creatures | LogTag.Actions | LogTag.Players | LogTag.Combat);
                actionsQueue.AddAction(new DamagePlayerAction(player, action.value));
            } else if (target is ICreature creature) {
                Log($"Adding DirectDamageAction - Source: {Name} (TargetID: {TargetId.ToUpper()}), Target: {creature.Name} (TargetID: {creature.TargetId.ToUpper()}), Damage: {action.value}",
                    LogTag.Creatures | LogTag.Actions | LogTag.Combat);
                actionsQueue.AddAction(new DirectDamageAction(creature, action.value, this));
            }
        }
    }

    private void ProcessHealEffect(EffectAction action, ActionsQueue actionsQueue) {
        if (Owner == null) {
            LogError($"Cannot handle heal effect for {Name} (TargetID: {TargetId.ToUpper()}) - Owner is null", LogTag.Creatures | LogTag.Effects);
            return;
        }

        var targets = TargetingSystem.GetValidTargets(Owner, action.targetType);
        Log($"Found {targets.Count} targets for {Name} (TargetID: {TargetId.ToUpper()})'s heal effect",
            LogTag.Creatures | LogTag.Actions);

        foreach (var target in targets) {
            if (target is ICreature creature) {
                Log($"Adding HealCreatureAction - Source: {Name} (TargetID: {TargetId.ToUpper()}), Target: {creature.Name} (TargetID: {creature.TargetId.ToUpper()}), Amount: {action.value}",
                    LogTag.Creatures | LogTag.Actions);
                actionsQueue.AddAction(new HealCreatureAction(creature, action.value));
            } else if (target is IPlayer player) {
                Log($"Adding HealPlayerAction - Source: {Name} (TargetID: {TargetId.ToUpper()}), Target: Player {(player.IsPlayer1() ? "1" : "2")} (TargetID: {player.TargetId.ToUpper()}), Amount: {action.value}",
                    LogTag.Creatures | LogTag.Actions | LogTag.Players);
                actionsQueue.AddAction(new HealPlayerAction(player, action.value));
            }
        }
    }

    private void ProcessDrawEffect(EffectAction action, ActionsQueue actionsQueue) {
        if (Owner == null) {
            LogError($"Cannot handle draw effect for {Name} (TargetID: {TargetId.ToUpper()}) - Owner is null", LogTag.Creatures | LogTag.Effects);
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
            Log($"Adding DrawCardAction - Source: {Name} (TargetID: {TargetId.ToUpper()}), Player: {(targetPlayer.IsPlayer1() ? "1" : "2")} (TargetID: {targetPlayer.TargetId.ToUpper()}), Amount: {action.value}",
                LogTag.Creatures | LogTag.Actions | LogTag.Cards);
            actionsQueue.AddAction(new DrawCardAction(targetPlayer, action.value));
        }
    }

    private void ProcessSummonEffect(EffectAction action, ActionsQueue actionsQueue) {
        if (Owner == null) {
            LogError($"Cannot handle summon effect for {Name} (TargetID: {TargetId.ToUpper()}) - Owner is null", LogTag.Creatures | LogTag.Effects);
            return;
        }

        Log($"Processing summon effect for {Name} (TargetID: {TargetId.ToUpper()}). TargetType: {action.targetType}, Value: {action.value}",
            LogTag.Creatures | LogTag.Actions);

        // Handle summoning based on targeting
        IPlayer targetPlayer = Owner;
        if (action.targetType == TargetType.Enemy) {
            targetPlayer = Owner.Opponent;
        } else if (action.targetType != TargetType.Player && action.targetType != TargetType.FriendlyCreatures) {
            LogWarning($"Unexpected target type for summon effect: {action.targetType}", LogTag.Effects);
            return;
        }

        // Get available creature cards from the deck
        var gameManager = GameManager.Instance;
        if (gameManager?.cardDealingService == null) return;

        var deckCards = gameManager.cardDealingService.GetDeckPreview(targetPlayer);
        var creaturesInDeck = deckCards.Where(c => c is ICreature).ToList();

        if (creaturesInDeck.Count == 0) {
            Log($"No creatures available in {(targetPlayer.IsPlayer1() ? "Player 1" : "Player 2")} (TargetID: {targetPlayer.TargetId.ToUpper()})'s deck to summon",
                LogTag.Creatures | LogTag.Cards);
            return;
        }

        // Find valid slots
        var validSlots = targetPlayer.Battlefield.Where(s => !s.IsOccupied()).ToList();
        if (validSlots.Count == 0) {
            Log($"No valid slots available for {(targetPlayer.IsPlayer1() ? "Player 1" : "Player 2")} (TargetID: {targetPlayer.TargetId.ToUpper()}) to summon creatures",
                LogTag.Creatures | LogTag.Cards);
            return;
        }

        // Determine how many creatures to summon (up to value, but limited by available slots/creatures)
        int countToSummon = Math.Min(action.value, Math.Min(validSlots.Count, creaturesInDeck.Count));
        var random = new System.Random();

        for (int i = 0; i < countToSummon; i++) {
            // Select a random creature from the deck
            int creatureIndex = random.Next(creaturesInDeck.Count);
            var creatureToSummon = creaturesInDeck[creatureIndex] as ICreature;
            if (creatureToSummon == null) continue;

            // Remove it from our local list to avoid duplicates
            creaturesInDeck.RemoveAt(creatureIndex);

            // Select a random valid slot
            int slotIndex = random.Next(validSlots.Count);
            var slot = validSlots[slotIndex];
            validSlots.RemoveAt(slotIndex);

            // Add summon action (with fromDeck=true since we're summoning from deck)
            actionsQueue.AddAction(new SummonCreatureAction(creatureToSummon, targetPlayer, slot, true));

            Log($"Queued summon effect for {creatureToSummon.Name} (TargetID: {creatureToSummon.TargetId.ToUpper()}) to slot (TargetID: {slot.TargetId.ToUpper()})",
                LogTag.Creatures | LogTag.Effects | LogTag.Actions);
        }
    }
}