using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class MarkCombatTargetAction : IGameAction {
    private readonly ICreature attacker;
    private readonly BattlefieldSlot targetSlot;
    public ICreature GetAttacker() => attacker;
    public BattlefieldSlot GetTargetSlot() => targetSlot;

    public MarkCombatTargetAction(ICreature attacker, ITarget targetSlot) {
        this.attacker = attacker;
        this.targetSlot = targetSlot as BattlefieldSlot;
        Log($"Created MarkCombatTargetAction for {attacker?.Name} (TargetID: {attacker?.TargetId.ToUpper()}) targeting slot {targetSlot?.TargetId.ToUpper()}",
            LogTag.Actions | LogTag.Combat);
    }

    public void Execute() {
        if (attacker == null || targetSlot == null) {
            LogError("Cannot execute MarkCombatTargetAction - attacker or target slot is null", LogTag.Actions);
            return;
        }

        // Mark the target slot for combat
        targetSlot.MarkForCombat();
        Log($"Marked slot {targetSlot.TargetId.ToUpper()} as combat target for {attacker.Name} (TargetID: {attacker.TargetId.ToUpper()})",
            LogTag.Actions | LogTag.Combat);
    }

    public override string ToString() {
        return $"MarkCombatTargetAction: Attacker={attacker?.Name} (TargetID: {attacker?.TargetId.ToUpper()}), TargetSlot={targetSlot?.TargetId.ToUpper()}";
    }
} 