using Enums;
using static DebugLogger;
using UnityEngine;
using System;

public class HealCreatureAction : IGameAction {
    #region Fields & Properties
    private readonly ICreature target;
    private readonly int amount;
    #endregion

    #region Constructor
    public HealCreatureAction(ICreature target, int amount) {
        this.target = target;
        this.amount = amount;
        Log($"Created HealCreatureAction for {target?.Name} (TargetID: {target?.TargetId.ToUpper()}) with amount {amount}", LogTag.Actions | LogTag.Creatures);
    }
    #endregion

    #region Methods
    public void Execute() {
        if (target == null || amount <= 0) return;

        // Use the Heal method on the Creature instance
        if (target is Creature creatureImpl) {
            creatureImpl.Heal(amount);
             // Logging is now handled inside Creature.Heal
             // Log($"Executed HealCreatureAction: Healed {creatureImpl.Name} ({creatureImpl.TargetId}) for {amount}", LogTag.Actions | LogTag.Creatures);
        } else {
             LogError($"HealCreatureAction target {target.Name} ({target.TargetId}) is not a Creature implementation.", LogTag.Actions);
        }
    }

    public override string ToString() {
        return $"HealCreatureAction: Target={target?.Name} (TargetID: {target?.TargetId.ToUpper()}), Amount={amount}";
    }
    #endregion
}