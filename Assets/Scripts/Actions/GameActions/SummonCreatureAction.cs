using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class SummonCreatureAction : IGameAction {
    private readonly ICreature creature;
    private readonly IPlayer owner;
    private readonly ITarget target;
    private readonly bool fromDeck;

    public SummonCreatureAction(ICreature creature, IPlayer owner, ITarget target = null, bool fromDeck = false) {
        this.creature = creature;
        this.owner = owner;
        this.target = target;
        this.fromDeck = fromDeck;
        Log($"Created SummonCreatureAction for {creature.Name} targeting slot {target?.TargetId} with {creature.Effects.Count} effects (fromDeck: {fromDeck})",
            LogTag.Actions | LogTag.Creatures);
    }

    public void Execute() {
        if (creature == null || owner == null) {
            LogError("Cannot execute SummonCreatureAction - creature or owner is null", LogTag.Actions);
            return;
        }

        // If the creature is in the hand, remove it
        if (!fromDeck && owner.Hand.Contains(creature)) {
            owner.DiscardCard(creature);
        }

        // Summon the creature to the battlefield
        if (target is BattlefieldSlot slot) {
            // Create a new card controller for the creature
            var cardController = CardFactory.CreateCardController(creature, owner, slot.transform);
            if (cardController != null) {
                slot.AssignCreature(cardController);
                Log($"Summoned {creature.Name} to slot {slot.TargetId}", LogTag.Actions | LogTag.Creatures);
            } else {
                LogError($"Failed to create card controller for {creature.Name}", LogTag.Actions);
            }
        } else {
            LogError($"Cannot summon creature - invalid target slot", LogTag.Actions);
        }
    }

    public override string ToString() {
        return $"SummonCreatureAction: Creature={creature?.Name} (TargetID: {creature?.TargetId.ToUpper()}), Owner={(owner?.IsPlayer1() == true ? "Player 1" : "Player 2")} (TargetID: {owner?.TargetId.ToUpper()}), Target={target?.TargetId.ToUpper()}, FromDeck={fromDeck}";
    }
} 