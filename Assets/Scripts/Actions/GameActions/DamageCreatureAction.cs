using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

// Consolidated damage action for creatures
public class DamageCreatureAction : IGameAction {
    #region Fields & Properties
    private readonly ICreature target;
    private readonly int damage;
    private readonly ICreature attacker; // Renamed from source for clarity in combat context
    // Removed isDirectDamage flag

    public ICreature GetTarget() => target;
    public ICreature GetAttacker() => attacker; // Changed getter name
    public int GetDamage() => damage;
    #endregion

    #region Constructor
    // Simplified constructor
    public DamageCreatureAction(ICreature target, int damage, ICreature attacker = null) {
        this.target = target;
        this.damage = damage;
        this.attacker = attacker; // Use attacker field
        Log($"Created DamageCreatureAction for {target?.Name} (TargetID: {target?.TargetId.ToUpper().Substring(0, 8)}) with {damage} damage from {attacker?.Name ?? "source"}",
            LogTag.Actions | LogTag.Creatures);
    }
    #endregion

    #region Methods
    public override string ToString() {
        // Updated ToString
        return $"DamageCreatureAction: Target={target?.Name}({target?.TargetId.ToUpper().Substring(0, 8)}), Dmg={damage}, Attacker={attacker?.Name ?? "Source"}";
    }
    #endregion
}