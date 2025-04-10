using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

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
        Log($"Created DirectDamageAction for {target?.Name} (TargetID: {target?.TargetId.ToUpper()}) with {damage} damage from {source?.Name ?? "direct source"}",
            LogTag.Actions | LogTag.Creatures);
    }

    public void Execute() {
        if (target == null) return;

        if (target is Creature creature) {
            // Apply direct damage to the creature
            creature.TakeDamage(damage, source);
            Log($"Applied {damage} direct damage to {creature.Name} (TargetID: {creature.TargetId.ToUpper()}) from {source?.Name ?? "direct source"} (TargetID: {creature.TargetId.ToUpper()})",
                LogTag.Actions | LogTag.Creatures);
        }
    }

    public override string ToString() {
        return $"DirectDamageAction: Target={target?.Name} (TargetID: {target?.TargetId.ToUpper()}), Damage={damage}, Source={source?.Name ?? "None"}";
    }
} 