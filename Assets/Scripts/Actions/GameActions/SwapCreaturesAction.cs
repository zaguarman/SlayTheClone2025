using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class SwapCreaturesAction : IGameAction {
    private readonly ICreature fromCreature;
    private readonly ICreature toCreature;
    private readonly BattlefieldSlot fromSlot;
    private readonly BattlefieldSlot toSlot;

    public ICreature GetCreature1() => fromCreature;
    public ICreature GetCreature2() => toCreature;

    public SwapCreaturesAction(ICreature fromCreature, ICreature toCreature, BattlefieldSlot fromSlot, BattlefieldSlot toSlot) {
        this.fromCreature = fromCreature;
        this.toCreature = toCreature;
        this.fromSlot = fromSlot;
        this.toSlot = toSlot;
        Log($"Created SwapCreaturesAction: {fromCreature?.Name ?? "Unknown"} (TargetID: {fromCreature?.TargetId.ToUpper() ?? "UNKNOWN"}) swapping with {toCreature?.Name ?? "Unknown"} (TargetID: {toCreature?.TargetId.ToUpper() ?? "UNKNOWN"})", LogTag.Actions | LogTag.Creatures);
    }

    public void Execute() {
        if (fromSlot == null || toSlot == null) {
            LogError("Cannot execute swap action - one or both slots are null", LogTag.Actions);
            return;
        }

        // Get the card controllers for both creatures
        var fromCard = fromSlot.OccupyingCard;
        var toCard = toSlot.OccupyingCard;

        // Clear both slots
        fromSlot.ClearSlot(false);
        toSlot.ClearSlot(false);

        // Assign the creatures to their new slots
        if (fromCard != null) {
            toSlot.AssignCreature(fromCard);
        }
        if (toCard != null) {
            fromSlot.AssignCreature(toCard);
        }

        Log($"Executed swap action: {fromCreature?.Name ?? "Unknown"} (TargetID: {fromCreature?.TargetId.ToUpper() ?? "UNKNOWN"}) swapped with {toCreature?.Name ?? "Unknown"} (TargetID: {toCreature?.TargetId.ToUpper() ?? "UNKNOWN"})", LogTag.Actions | LogTag.Creatures);
    }

    public override string ToString() {
        return $"SwapCreaturesAction: From={fromCreature?.Name ?? "Unknown"} (TargetID: {fromCreature?.TargetId.ToUpper() ?? "UNKNOWN"}), To={toCreature?.Name ?? "Unknown"} (TargetID: {toCreature?.TargetId.ToUpper() ?? "UNKNOWN"})";
    }
} 