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
    public void Execute() {
        if (target == null || target.Health <= 0) return; // Check if target exists and is alive

        if (target is Creature creature) {
            // Apply damage to the creature
            Log($"Executing DamageCreatureAction: Applying {damage} damage to {creature.Name} (TargetID: {creature.TargetId.ToUpper().Substring(0, 8)}) from {attacker?.Name ?? "source"}",
                LogTag.Actions | LogTag.Creatures | LogTag.Combat);
            creature.TakeDamage(damage, attacker); // Pass attacker
        } else {
            LogWarning($"DamageCreatureAction: Target {target.Name} is not a concrete Creature.", LogTag.Actions | LogTag.Creatures);
        }
    }

    public override string ToString() {
        // Updated ToString
        return $"DamageCreatureAction: Target={target?.Name}({target?.TargetId.ToUpper().Substring(0, 8)}), Dmg={damage}, Attacker={attacker?.Name ?? "Source"}";
    }
    #endregion
}