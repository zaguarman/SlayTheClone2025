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
             Log($"Executing DamageCreatureAction: Intent to deal {damage} damage to {creature.Name} (TargetID: {creature.TargetId.ToUpper().Substring(0, 8)}, Armor: {creature.CurrentArmorPool}) from {attacker?.Name ?? "source"}",
                LogTag.Actions | LogTag.Creatures | LogTag.Combat);

            // --- Armor Check Logic ---
            int damageToArmor = Math.Min(damage, creature.CurrentArmorPool);
            int remainingDamage = damage - damageToArmor;

            if (damageToArmor > 0) {
                // Apply damage to armor pool *directly* here
                creature.ModifyArmorPool(-damageToArmor);
                Log($"DamageCreatureAction: {damageToArmor} damage absorbed by armor. {creature.Name} armor remaining: {creature.CurrentArmorPool}", LogTag.Actions | LogTag.Effects | LogTag.Combat);
            }

            if (remainingDamage > 0 && creature.Health > 0) {
                // Apply remaining damage to health using the specific health damage method
                Log($"DamageCreatureAction: Applying {remainingDamage} remaining damage to health.", LogTag.Actions | LogTag.Creatures | LogTag.Combat);
                creature.TakeHealthDamage(remainingDamage, attacker); // Pass attacker
            }
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