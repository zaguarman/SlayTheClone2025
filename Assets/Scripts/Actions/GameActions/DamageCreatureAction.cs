using Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class DamageCreatureAction : IGameAction {
    #region Fields & Properties
    private readonly ICreature target;
    private readonly int damage;
    private readonly ICreature attacker;
    private readonly bool isDirectDamage;
    public ICreature GetTarget() => target;
    public ICreature GetAttacker() => attacker;
    public int GetDamage() => damage;
    #endregion

    #region Constructor
    public DamageCreatureAction(ICreature target, int damage, ICreature attacker = null, bool isDirectDamage = false) {
        this.target = target;
        this.damage = damage;
        this.attacker = attacker;
        this.isDirectDamage = isDirectDamage;
        Log($"Created DamageCreatureAction for {target?.Name} (TargetID: {target?.TargetId.ToUpper()}) with {damage} damage from {attacker?.Name ?? "direct source"}",
            LogTag.Actions | LogTag.Creatures);
    }
    #endregion

    #region Methods
    public void Execute() {
        if (target == null) return;

        if (target is Creature creature) {
            // Apply damage to the creature
            creature.TakeDamage(damage, attacker);
            Log($"Applied {damage} damage to {creature.Name} (TargetID: {creature.TargetId.ToUpper()}) from {attacker?.Name ?? "direct source"}",
                LogTag.Actions | LogTag.Creatures);
        }
    }

    public override string ToString() {
        return $"DamageCreatureAction: Target={target?.Name} (TargetID: {target?.TargetId.ToUpper()}), Damage={damage}, Attacker={attacker?.Name ?? "None"}, IsDirect={isDirectDamage}";
    }
    #endregion
} 