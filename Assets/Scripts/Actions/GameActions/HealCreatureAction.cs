using static Enums;
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


    // Getters for executor
    public ICreature GetTarget() => target;
    public int GetAmount() => amount;

    public override string ToString() {
        return $"HealCreatureAction: Target={target?.Name} (TargetID: {target?.TargetId.ToUpper()}), Amount={amount}";
    }
    #endregion
}