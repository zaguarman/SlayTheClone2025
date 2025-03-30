using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public interface IGameAction { void Execute(); }

public class DiscardHandAction : IGameAction {
    private readonly IPlayer player;

    public DiscardHandAction(IPlayer player) {
        this.player = player;
        Log($"Created DiscardHandAction for {(player.IsPlayer1() ? "Player 1" : "Player 2")}", LogTag.Actions | LogTag.Cards);
    }

    public void Execute() {
        if (player == null) {
            LogError("Cannot execute discard hand action - player is null", LogTag.Actions | LogTag.Cards);
            return;
        }

        // Discard all cards in the player's hand
        player.DiscardHand();
        Log($"Executed discard hand action for {(player.IsPlayer1() ? "Player 1" : "Player 2")}", LogTag.Actions | LogTag.Cards);
    }

    public override string ToString() {
        return $"DiscardHandAction: Player={(player?.IsPlayer1() == true ? "1" : "2")}";
    }
}

// Replacing the old DrawCardAction with this more flexible version
public class DrawCardsAction : IGameAction {
    private readonly IPlayer player;
    private readonly int amount;

    public DrawCardsAction(IPlayer player, int amount = 1) {
        this.player = player;
        this.amount = amount;
        Log($"Created DrawCardsAction for {(player.IsPlayer1() ? "Player 1" : "Player 2")} to draw {amount} cards", LogTag.Actions | LogTag.Cards);
    }

    public void Execute() {
        if (player == null) {
            LogError("Cannot execute draw cards action - player is null", LogTag.Actions | LogTag.Cards);
            return;
        }

        var cardDealingService = GameManager.Instance?.cardDealingService;
        if (cardDealingService == null) {
            LogError("Cannot execute draw cards action - card dealing service not available", LogTag.Actions | LogTag.Cards);
            return;
        }

        // Draw the specified number of cards
        cardDealingService.DrawCards(player, amount);
        Log($"Executed draw cards action for {(player.IsPlayer1() ? "Player 1" : "Player 2")} - drew up to {amount} cards", LogTag.Actions | LogTag.Cards);
    }

    public override string ToString() {
        return $"DrawCardsAction: Player={(player?.IsPlayer1() == true ? "1" : "2")}, Amount={amount}";
    }
}

public class DrawCardAction : IGameAction {
    private readonly IPlayer player;
    private readonly int amount;

    public DrawCardAction(IPlayer player, int amount = 1) {
        this.player = player;
        this.amount = amount;
        Log($"Created DrawCardAction for {(player.IsPlayer1() ? "Player 1" : "Player 2")} to draw {amount} card(s)",
            LogTag.Actions | LogTag.Cards);
    }

    public void Execute() {
        if (player == null) {
            LogError("Cannot execute draw action - player is null", LogTag.Actions | LogTag.Cards);
            return;
        }

        int cardsDrawn = 0;

        for (int i = 0; i < amount; i++) {
            // Check if we've hit the hand size limit
            if (player.Hand.Count >= Player.MAX_HAND_SIZE) {
                Log($"Draw stopped: {(player.IsPlayer1() ? "Player 1" : "Player 2")} has reached the maximum hand size ({Player.MAX_HAND_SIZE})",
                    LogTag.Actions | LogTag.Cards);
                break;
            }

            player.DrawCard();
            cardsDrawn++;
        }

        int cardsMissed = amount - cardsDrawn;
        if (cardsMissed > 0) {
            Log($"{(player.IsPlayer1() ? "Player 1" : "Player 2")} could not draw {cardsMissed} card(s) due to hand size limit",
                LogTag.Actions | LogTag.Cards);
        }

        Log($"Executed draw of {cardsDrawn}/{amount} card(s) for {(player.IsPlayer1() ? "Player 1" : "Player 2")}",
            LogTag.Actions | LogTag.Cards);
    }

    public override string ToString() {
        return $"DrawCardAction: Player={(player.IsPlayer1() ? "1" : "2")}, Amount={amount}";
    }
}

public class PlaySpellAction : IGameAction {
    private readonly Spell spell;
    private readonly IPlayer owner;
    private readonly ITarget target;

    public PlaySpellAction(Spell spell, IPlayer owner, ITarget target = null) {
        this.spell = spell;
        this.owner = owner;
        this.target = target;
        Log($"Created PlaySpellAction for {spell.Name}", LogTag.Actions | LogTag.Cards);
    }

    public void Execute() {
        if (spell == null || owner == null) {
            LogError("Cannot execute PlaySpellAction - spell or owner is null", LogTag.Actions);
            return;
        }

        // Discard the spell from hand if it hasn't been already
        if (owner.Hand.Contains(spell)) {
            owner.DiscardCard(spell);
        }

        // Process spell effects
        spell.Play(owner, GameManager.Instance.ActionsQueue, target);

        Log($"Executed PlaySpellAction for {spell.Name}", LogTag.Actions | LogTag.Cards);
    }

    public override string ToString() {
        return $"PlaySpellAction: Spell={spell?.Name}, Owner={(owner?.IsPlayer1() == true ? "Player 1" : "Player 2")}, Target={target?.TargetId}";
    }
}

public class ChangeWeatherAction : IGameAction {
    private readonly WeatherType targetWeather;

    public ChangeWeatherAction(WeatherType targetWeather) {
        this.targetWeather = targetWeather;
        Log($"Created ChangeWeatherAction to {targetWeather}", LogTag.Actions | LogTag.Effects);
    }

    public void Execute() {
        var weatherSystem = GameManager.Instance?.WeatherSystem;
        if (weatherSystem == null) {
            LogError("Cannot execute change weather action - weather system is null", LogTag.Actions);
            return;
        }

        weatherSystem.SetWeather(targetWeather);
        Log($"Changed weather to {targetWeather}", LogTag.Actions | LogTag.Effects);
    }

    public override string ToString() {
        return $"ChangeWeatherAction: TargetWeather={targetWeather}";
    }
}

public class HealCreatureAction : IGameAction {
    private readonly ICreature target;
    private readonly int amount;

    public HealCreatureAction(ICreature target, int amount) {
        this.target = target;
        this.amount = amount;
        Log($"Created HealCreatureAction for {target?.Name} with amount {amount}", LogTag.Actions | LogTag.Creatures);
    }

    public void Execute() {
        if (target == null) return;

        if (target is Creature creature) {
            // Heal should be implemented in Creature class, here's a workaround for this prototype
            int currentHealth = creature.Health;
            int newHealth = Math.Min(currentHealth + amount, 10); // Assuming 10 is max health for this prototype

            // Since we don't have a direct SetHealth method, we'll log the info
            Log($"Healing {creature.Name} for {amount} (from {currentHealth} to {newHealth})",
                LogTag.Actions | LogTag.Creatures);

            // In a real implementation, we'd call something like:
            // creature.Heal(amount);
        }
    }

    public override string ToString() {
        return $"HealCreatureAction: Target={target?.Name}, Amount={amount}";
    }
}

public class HealPlayerAction : IGameAction {
    private readonly IPlayer target;
    private readonly int amount;

    public HealPlayerAction(IPlayer target, int amount) {
        this.target = target;
        this.amount = amount;
        Log($"Created HealPlayerAction for {(target?.IsPlayer1() == true ? "Player 1" : "Player 2")} with amount {amount}",
            LogTag.Actions | LogTag.Players);
    }

    public void Execute() {
        if (target == null) return;

        // Heal should be implemented in Player class, here's a workaround for this prototype
        int currentHealth = target.Health;
        int maxHealth = 20; // Assuming 20 is max health for this prototype
        int newHealth = Math.Min(currentHealth + amount, maxHealth);

        // Since we don't have a direct SetHealth method, we'll log the info
        Log($"Healing {(target.IsPlayer1() ? "Player 1" : "Player 2")} for {amount} (from {currentHealth} to {newHealth})",
            LogTag.Actions | LogTag.Players);

        // In a real implementation, we'd call something like:
        // target.Heal(amount);
    }

    public override string ToString() {
        return $"HealPlayerAction: Target={(target?.IsPlayer1() == true ? "Player 1" : "Player 2")}, Amount={amount}";
    }
}

public class SummonCreatureAction : IGameAction {
    private readonly ICreature creature;
    private readonly IPlayer owner;
    private readonly ITarget target;

    public SummonCreatureAction(ICreature creature, IPlayer owner, ITarget target = null) {
        this.creature = creature;
        this.owner = owner;
        this.target = target;
        Log($"Created SummonCreatureAction for {creature.Name} targeting slot {target.TargetId} with {creature.Effects.Count} effects", LogTag.Actions | LogTag.Creatures);
    }

    public void Execute() {
        // 1. Set owner first
        creature.SetOwner(owner);

        // 2. Add to battlefield before processing effects
        owner.AddToBattlefield(creature, target);

        // 3. Process effects after battlefield placement
        if (creature is Creature c) {
            Log($"Processing OnPlay effects for {c.Name} with {c.Effects.Count} effects",
                LogTag.Actions | LogTag.Effects);
            c.HandleEffect(EffectTrigger.OnPlay, GameManager.Instance.ActionsQueue);
        }
    }

    public override string ToString() {
        return $"SummonCreatureAction: Creature={creature?.Name}, Player={(owner.IsPlayer1() ? "1" : "2")}, Target={target?.TargetId}";
    }
}

public class DamagePlayerAction : IGameAction {
    private IPlayer target;
    private int damage;
    public IPlayer GetTargetPlayer() => target;
    public int GetDamage() => damage;

    public DamagePlayerAction(IPlayer target, int damage) {
        this.target = target;
        this.damage = damage;
        Log($"Created - Target: {target}, Damage: {damage}", LogTag.Actions | LogTag.Players | LogTag.Combat);
    }

    public void Execute() {
        target.TakeDamage(damage);
        Log($"{damage} damage dealt to {target}", LogTag.Actions | LogTag.Players | LogTag.Combat);
    }

    public override string ToString() {
        return $"DamagePlayerAction: Target Player={(target.IsPlayer1() ? "1" : "2")}, Damage={damage}";
    }
}

public class DamageCreatureAction : IGameAction {
    private readonly ICreature target;
    private readonly int damage;
    private readonly ICreature attacker;
    private readonly bool isDirectDamage;
    public ICreature GetTarget() => target;
    public ICreature GetAttacker() => attacker;
    public int GetDamage() => damage;

    public DamageCreatureAction(ICreature target, int damage, ICreature attacker = null, bool isDirectDamage = false) {
        this.target = target;
        this.damage = damage;
        this.attacker = attacker;
        this.isDirectDamage = isDirectDamage;
        Log($"Created DamageAction - Target: {target?.Name}, Damage: {damage}, Attacker: {attacker?.Name}, DirectDamage: {isDirectDamage}",
            LogTag.Actions | LogTag.Creatures | LogTag.Combat);
    }

    public void Execute() {
        if (target == null) return;

        var weatherSystem = GameManager.Instance?.WeatherSystem;
        float modifier = weatherSystem?.GetDamageModifier(isDirectDamage) ?? 0f;
        int modifiedDamage = damage;

        // Apply weather modifiers
        if (modifier != 0f) {
            modifiedDamage = Mathf.Max(0, modifiedDamage + Mathf.RoundToInt(modifier));
            Log($"Weather modified damage from {damage} to {modifiedDamage} (modifier: {modifier})",
                LogTag.Actions | LogTag.Combat | LogTag.Effects);
        }

        Log($"Executing DamageAction - Target: {target.Name}, Original Damage: {damage}, Modified Damage: {modifiedDamage}, Weather Modifier: {modifier}, Attacker: {attacker?.Name}",
            LogTag.Actions | LogTag.Creatures | LogTag.Combat);

        if (target is Creature creatureTarget) {
            creatureTarget.TakeDamage(modifiedDamage, attacker);
        } else {
            target.TakeDamage(modifiedDamage);
        }
    }

    public override string ToString() {
        return $"DamageCreatureAction: Target={target?.Name}, Damage={damage}, Attacker={attacker?.Name}, DirectDamage={isDirectDamage}";
    }
}

public class DirectDamageAction : IGameAction {
    private readonly ICreature target;
    private readonly int damage;
    private readonly ICreature source;
    public ICreature GetTarget() => target;
    public ICreature GetSource() => source;
    public int GetDamage() => damage;

    public DirectDamageAction(ICreature target, int damage, ICreature source = null) {
        this.target = target;
        this.damage = damage;
        this.source = source;
        Log($"Created DirectDamageAction - Source: {source?.Name}, Target: {target?.Name}, Damage: {damage}",
            LogTag.Actions | LogTag.Creatures | LogTag.Combat);
    }

    public void Execute() {
        if (target == null) return;

        var weatherSystem = GameManager.Instance?.WeatherSystem;
        float modifier = weatherSystem?.GetDamageModifier(true) ?? 0f; // true for direct damage
        int modifiedDamage = damage;

        // Apply weather modifiers
        if (modifier != 0f) {
            modifiedDamage = Mathf.Max(0, modifiedDamage + Mathf.RoundToInt(modifier));
            Log($"Weather modified direct damage from {damage} to {modifiedDamage} (modifier: {modifier})",
                LogTag.Actions | LogTag.Combat | LogTag.Effects);
        }

        Log($"Executing DirectDamageAction - Source: {source?.Name}, Target: {target.Name}, Original Damage: {damage}, Modified Damage: {modifiedDamage}, Weather Modifier: {modifier}",
            LogTag.Actions | LogTag.Creatures | LogTag.Combat);

        if (target is Creature creatureTarget) {
            creatureTarget.TakeDamage(modifiedDamage, source);
        } else {
            target.TakeDamage(modifiedDamage);
        }
    }

    public override string ToString() {
        return $"DirectDamageAction: Source={source?.Name}, Target={target?.Name}, Damage={damage}";
    }
}
public class SwapCreaturesAction : IGameAction {
    private readonly ICreature fromCreature;
    private readonly ICreature toCreature;
    private readonly ITarget fromSlot;
    private readonly ITarget toSlot;
    private readonly IPlayer owner;
    // Added methods to help with arrow creation
    public ICreature GetCreature1() => fromCreature;
    public ICreature GetCreature2() => toCreature;


    public SwapCreaturesAction(ICreature fromCreature, ICreature toCreature, ITarget fromSlot, ITarget toSlot, IPlayer owner) {
        this.fromCreature = fromCreature;
        this.toCreature = toCreature;
        this.fromSlot = fromSlot;
        this.toSlot = toSlot;
        this.owner = owner;
        Log($"Created SwapCreaturesAction between {fromCreature.Name} (slot {fromSlot}) and {toCreature.Name} (slot {toSlot})",
            LogTag.Actions | LogTag.Creatures);
    }

    public void Execute() {
        if (fromCreature == null || toCreature == null || owner == null) {
            LogError("Cannot execute swap - one or more components are null", LogTag.Actions | LogTag.Creatures);
            return;
        }

        owner.RemoveFromBattlefield(fromCreature);
        owner.RemoveFromBattlefield(toCreature);

        owner.AddToBattlefield(fromCreature, toSlot);
        owner.AddToBattlefield(toCreature, fromSlot);

        Log($"Executed swap between {fromCreature.Name} and {toCreature.Name}", LogTag.Actions | LogTag.Creatures);
    }

    public override string ToString() {
        return $"SwapCreaturesAction: From={fromCreature?.Name} (Slot={fromSlot}), To={toCreature?.Name} (Slot={toSlot})";
    }
}

public class PlayCardAction : IGameAction {
    private readonly ICard card;
    private readonly IPlayer owner;
    private readonly ITarget target;

    public PlayCardAction(ICard card, IPlayer owner, ITarget target) {
        this.card = card;
        this.owner = owner;
        this.target = target;
        Log($"Created PlayCardAction for {card.Name} targeting {target?.TargetId}", LogTag.Actions | LogTag.Cards);
    }

    public void Execute() {
        if (card == null || owner == null) {
            LogError("Cannot execute PlayCardAction - card or owner is null", LogTag.Actions);
            return;
        }

        // Discard the card from hand instead of just removing it
        owner.DiscardCard(card);

        // Process based on card type
        if (card is Spell spell) {
            // Create a specific spell action for better tracking
            GameManager.Instance.ActionsQueue.AddAction(new PlaySpellAction(spell, owner, target));
        } else {
            // Process any immediate effects for other card types
            card.Play(owner, GameManager.Instance.ActionsQueue, target);
        }

        Log($"Executed PlayCardAction for {card.Name}", LogTag.Actions | LogTag.Cards);
    }

    public override string ToString() {
        return $"PlayCardAction: Card={card?.Name}, Owner={(owner?.IsPlayer1() == true ? "Player 1" : "Player 2")}, Target={target?.TargetId}";
    }
}

public class MarkCombatTargetAction : IGameAction {
    private readonly ICreature attacker;
    private readonly BattlefieldSlot targetSlot;
    public ICreature GetAttacker() => attacker;
    public BattlefieldSlot GetTargetSlot() => targetSlot;

    public MarkCombatTargetAction(ICreature attacker, ITarget targetSlot) {
        this.attacker = attacker;
        this.targetSlot = (BattlefieldSlot)targetSlot;
        Log($"Created MarkCombatTargetAction: {attacker?.Name} targeting slot {targetSlot?.TargetId}", LogTag.Actions | LogTag.Combat);
    }

    public void Execute() {
        if (attacker == null || targetSlot == null) {
            LogError("Cannot execute combat action - attacker or target slot is null", LogTag.Actions | LogTag.Combat);
            return;
        }

        if (targetSlot.IsOccupied()) {
            var targetCreature = targetSlot.OccupyingCreature;
            if (targetCreature != null) {
                Log($"Creature {attacker.Name} attacking creature {targetCreature.Name}", LogTag.Combat);
                var damageAction = new DamageCreatureAction(targetCreature, attacker.Attack, attacker);
                GameManager.Instance.ActionsQueue.AddAction(damageAction);
            }
        } else {
            var targetPlayer = attacker.Owner?.Opponent;
            if (targetPlayer != null) {
                Log($"Creature {attacker.Name} attacking player {(targetPlayer.IsPlayer1() ? "1" : "2")}", LogTag.Combat);
                GameManager.Instance.ActionsQueue.AddAction(
                    new DamagePlayerAction(targetPlayer, attacker.Attack)
                );
            }
        }
    }

    public override string ToString() {
        return $"MarkCombatTargetAction: Attacker={attacker?.Name}, TargetSlot={targetSlot?.TargetId}";
    }
}

public class MoveCreatureAction : IGameAction {
    private readonly ICreature creature;
    private readonly ITarget fromSlot;
    private readonly ITarget toSlot;
    private readonly IPlayer player;
    // Getter methods for arrow visualization
    public ICreature GetCreature() => creature;
    public ITarget GetFromSlot() => fromSlot;
    public ITarget GetToSlot() => toSlot;

    public MoveCreatureAction(ICreature creature, ITarget fromSlot, ITarget toSlot, IPlayer player) {
        this.creature = creature;
        this.fromSlot = fromSlot;
        this.toSlot = toSlot;
        this.player = player;
    }

    public void Execute() {
        if (creature == null || player == null) {
            LogError("Invalid move action - creature or player is null", LogTag.Actions);
            return;
        }

        var fromSlotComponent = (BattlefieldSlot)fromSlot;
        var toSlotComponent = (BattlefieldSlot)toSlot;

        // Get the current creature in the target slot
        var targetCreature = toSlotComponent.OccupyingCreature;

        if (targetCreature != null) {
            HandleSwap(fromSlotComponent, toSlotComponent);
        } else {
            HandleMove(fromSlotComponent, toSlotComponent);
        }

        Log($"Executed move action for {creature.Name} from slot {fromSlot} to slot {toSlot}",
            LogTag.Actions | LogTag.Creatures);
    }

    private void HandleSwap(BattlefieldSlot fromSlot, BattlefieldSlot toSlot) {
        // Get the CardControllers from both slots
        var fromController = fromSlot.OccupyingCard;
        var toController = toSlot.OccupyingCard;

        // Clear both slots without destroying the CardControllers
        fromSlot.ClearSlot(false); // Pass false to avoid destroying the controller
        toSlot.ClearSlot(false);

        // Assign controllers to the opposite slots
        fromSlot.AssignCreature(toController);
        toSlot.AssignCreature(fromController);

        // Update the Slot references on the creatures
        if (fromController != null && fromController.GetLinkedCreature() != null) {
            fromController.GetLinkedCreature().Slot = toSlot;
        }
        if (toController != null && toController.GetLinkedCreature() != null) {
            toController.GetLinkedCreature().Slot = fromSlot;
        }

        GameMediator.Instance?.NotifyBattlefieldStateChanged(player);
    }

    private void HandleMove(BattlefieldSlot fromSlot, BattlefieldSlot toSlot) {
        var fromController = fromSlot.OccupyingCard;

        // Clear the target slot if occupied (optional, based on game rules)
        if (toSlot.IsOccupied()) {
            toSlot.ClearSlot(true); // Destroy existing if necessary
        }

        // Move the controller to the new slot
        fromSlot.ClearSlot(false); // Don't destroy
        toSlot.AssignCreature(fromController);

        // Update the creature's Slot reference
        if (fromController != null && fromController.GetLinkedCreature() != null) {
            fromController.GetLinkedCreature().Slot = toSlot;
        }

        GameMediator.Instance?.NotifyBattlefieldStateChanged(player);
    }

    public override string ToString() {
        return $"MoveCreatureAction: Creature={creature?.Name}, FromSlot={fromSlot?.TargetId}, ToSlot={toSlot?.TargetId}";
    }
}