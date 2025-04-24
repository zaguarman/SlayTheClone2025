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



    // Getters for executor
    public BattlefieldSlot GetFromSlot() => fromSlot;
    public BattlefieldSlot GetToSlot() => toSlot;

    public override string ToString() {
        return $"SwapCreaturesAction: From={fromCreature?.Name ?? "Unknown"} (TargetID: {fromCreature?.TargetId.ToUpper() ?? "UNKNOWN"}), To={toCreature?.Name ?? "Unknown"} (TargetID: {toCreature?.TargetId.ToUpper() ?? "UNKNOWN"})";
    }
}