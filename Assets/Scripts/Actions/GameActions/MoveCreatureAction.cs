using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class MoveCreatureAction : IGameAction {
    #region Fields & Properties
    private readonly ICreature creature;
    private readonly ITarget fromSlot;
    private readonly ITarget toSlot;
    private readonly IPlayer player;
    public ICreature GetCreature() => creature;
    public ITarget GetFromSlot() => fromSlot;
    public ITarget GetToSlot() => toSlot;
    #endregion

    #region Constructor
    public MoveCreatureAction(ICreature creature, ITarget fromSlot, ITarget toSlot, IPlayer player) {
        this.creature = creature;
        this.fromSlot = fromSlot;
        this.toSlot = toSlot;
        this.player = player;
        Log($"Created MoveCreatureAction for {creature?.Name} (TargetID: {creature?.TargetId.ToUpper()}) from slot {fromSlot?.TargetId.ToUpper()} to {toSlot?.TargetId.ToUpper()}",
            LogTag.Actions | LogTag.Creatures);
    }
    #endregion

    #region Methods
    public void Execute() {
        if (creature == null || fromSlot == null || toSlot == null || player == null) {
            LogError("Cannot execute MoveCreatureAction - one or more required components are null", LogTag.Actions);
            return;
        }

        if (fromSlot is BattlefieldSlot fromBattlefieldSlot && toSlot is BattlefieldSlot toBattlefieldSlot) {
            if (toBattlefieldSlot.IsOccupied()) {
                HandleSwap(fromBattlefieldSlot, toBattlefieldSlot);
            } else {
                HandleMove(fromBattlefieldSlot, toBattlefieldSlot);
            }
        } else {
            LogError("Cannot execute MoveCreatureAction - invalid slot types", LogTag.Actions);
        }
    }

    private void HandleSwap(BattlefieldSlot fromSlot, BattlefieldSlot toSlot) {
        // Get the card controllers for both creatures
        var fromCard = fromSlot.OccupyingCard;
        var toCard = toSlot.OccupyingCard;

        // Remove both creatures from their slots
        fromSlot.ClearSlot(false);
        toSlot.ClearSlot(false);

        // Assign the creatures to their new slots
        if (fromCard != null) {
            toSlot.AssignCreature(fromCard);
        }
        if (toCard != null) {
            fromSlot.AssignCreature(toCard);
        }

        Log($"Swapped {creature.Name} (TargetID: {creature.TargetId.ToUpper()}) with {toSlot.OccupyingCreature?.Name ?? "Unknown"} (TargetID: {toSlot.OccupyingCreature?.TargetId.ToUpper() ?? "UNKNOWN"})",
            LogTag.Actions | LogTag.Creatures);
    }

    private void HandleMove(BattlefieldSlot fromSlot, BattlefieldSlot toSlot) {
        // Get the card controller for the creature
        var card = fromSlot.OccupyingCard;

        // Remove creature from current slot
        fromSlot.ClearSlot(false);

        // Place creature in new slot
        if (card != null) {
            toSlot.AssignCreature(card);
        }

        Log($"Moved {creature.Name} (TargetID: {creature.TargetId.ToUpper()}) from slot {fromSlot.TargetId.ToUpper()} to {toSlot.TargetId.ToUpper()}",
            LogTag.Actions | LogTag.Creatures);
    }

    public override string ToString() {
        return $"MoveCreatureAction: Creature={creature?.Name} (TargetID: {creature?.TargetId.ToUpper()}), FromSlot={fromSlot?.TargetId.ToUpper()}, ToSlot={toSlot?.TargetId.ToUpper()}";
    }
    #endregion
} 