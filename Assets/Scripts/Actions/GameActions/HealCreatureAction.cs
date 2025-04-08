using static Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class HealCreatureAction : IGameAction {
    private readonly ICreature target;
    private readonly int amount;

    public HealCreatureAction(ICreature target, int amount) {
        this.target = target;
        this.amount = amount;
        Log($"Created HealCreatureAction for {target?.Name} (TargetID: {target?.TargetId.ToUpper()}) with amount {amount}", LogTag.Actions | LogTag.Creatures);
    }

    public void Execute() {
        if (target == null) return;

        if (target is Creature creature) {
            // Heal should be implemented in Creature class, here's a workaround for this prototype
            int currentHealth = creature.Health;
            int newHealth = Math.Min(currentHealth + amount, 10); // Assuming 10 is max health for this prototype

            // Since we don't have a direct SetHealth method, we'll log the info
            Log($"Healing {creature.Name} (TargetID: {creature.TargetId.ToUpper()}) for {amount} (from {currentHealth} to {newHealth})",
                LogTag.Actions | LogTag.Creatures);

            // In a real implementation, we'd call something like:
            // creature.Heal(amount);
        }
    }

    public override string ToString() {
        return $"HealCreatureAction: Target={target?.Name} (TargetID: {target?.TargetId.ToUpper()}), Amount={amount}";
    }
} 