using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class SummonCreatureAction : IGameAction {
    private readonly ICreature creature;
    private readonly IPlayer owner;
    private readonly ITarget targetSlot; // Target is expected to be a BattlefieldSlot
    private readonly bool fromDeck;

    public SummonCreatureAction(ICreature creature, IPlayer owner, ITarget target, bool fromDeck = false) {
        this.creature = creature;
        this.owner = owner;
        this.targetSlot = target; // Keep ITarget for flexibility, but expect BattlefieldSlot
        this.fromDeck = fromDeck;
        // Log($"Created SummonCreatureAction for {creature?.Name} targeting slot {target?.TargetId} with {creature?.Effects.Count ?? 0} effects (fromDeck: {fromDeck})",
        //     LogTag.Actions | LogTag.Creatures);
    }

    public void Execute() {
        if (creature == null || owner == null) {
            LogError("Cannot execute SummonCreatureAction - creature or owner is null", LogTag.Actions | LogTag.Creatures);
            return;
        }
         if (!(targetSlot is BattlefieldSlot slot)) {
            LogError($"Cannot summon creature {creature.Name} - invalid target slot type: {targetSlot?.GetType().Name ?? "null"}", LogTag.Actions | LogTag.Creatures);
            return;
        }

        Log($"Executing SummonCreatureAction: Creature={creature.Name} (ID: {creature.TargetId.ToUpper().Substring(0,8)}), Owner={(owner.IsPlayer1() ? "P1" : "P2")}, Slot={slot.TargetId.ToUpper().Substring(0,8)}, FromDeck={fromDeck}", LogTag.Actions | LogTag.Creatures);

        // Ensure owner is set (might be redundant but safe)
        creature.SetOwner(owner);

        // Remove from hand if summoned from hand
        if (!fromDeck && owner.Hand.Contains(creature)) {
            // DiscardCard handles removing from hand and adding to discard (if applicable later)
             owner.DiscardCard(creature);
            // Log($"Removed {creature.Name} from owner's hand.", LogTag.Actions | LogTag.Cards);
        }
        // If summoned from deck, the CardDealingService should have already removed it.

        // --- Core Summon Logic ---
        // 1. Clear the target slot if occupied (handle potential replacement logic if needed)
        if (slot.IsOccupied()) {
             LogWarning($"Target slot {slot.TargetId} for {creature.Name} is already occupied by {slot.OccupyingCreature?.Name}. Replacing...", LogTag.Actions | LogTag.Creatures);
            // Consider if replacement should trigger "OnDeath" for the old creature
             owner.RemoveFromBattlefield(slot.OccupyingCreature, true); // Destroy the old card controller
        }

        // 2. Create CardController and assign to slot
        // The CardFactory now returns the controller directly
        var cardController = CardFactory.CreateCardController(creature, owner, slot.transform);
        if (cardController != null) {
            slot.AssignCreature(cardController); // Assigns controller and sets creature.Slot
            Log($"Summoned {creature.Name} to slot {slot.TargetId.ToUpper().Substring(0,8)}", LogTag.Actions | LogTag.Creatures);

            // 3. --- REGISTER WITH MODIFIER MANAGER ---
            GameManager.Instance?.ModifierManager?.RegisterCreature(creature as Creature); // Cast to concrete type if needed

            // 4. Trigger OnPlay CardEffects (existing system)
            if (creature is Creature creatureImpl) {
                // Log($"Triggering OnPlay CardEffects for {creature.Name}", LogTag.Actions | LogTag.Creatures | LogTag.Effects);
                creatureImpl.HandleEffect(EffectTrigger.OnPlay, GameManager.Instance.ActionsQueue);
            }

             // 5. Notify GameMediator AFTER registration and effects
             GameMediator.Instance?.NotifyCreatureSummoned(creature, owner);

        } else {
            LogError($"Failed to create card controller for {creature.Name}", LogTag.Actions | LogTag.Creatures);
        }
    }

    public override string ToString() {
         string slotIdStr = (targetSlot is BattlefieldSlot s) ? s.TargetId.ToUpper().Substring(0,8) : targetSlot?.TargetId ?? "UNKNOWN";
        return $"SummonCreatureAction: Cr={creature?.Name}({creature?.TargetId.ToUpper().Substring(0,8)}), Own={(owner?.IsPlayer1() == true ? "P1" : "P2")}, Slot={slotIdStr}, Deck={fromDeck}";
    }
}