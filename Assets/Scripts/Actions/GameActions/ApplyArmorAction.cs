// File: Scripts/Actions/GameActions/ApplyArmorAction.cs
using static DebugLogger;
using static Enums;
using System;

public class ApplyArmorAction : IGameAction
{
    private readonly ITarget target; // Can be ICreature
    private readonly int armorValue;
    private readonly int duration; // 0 for permanent, > 0 for timed

    public ApplyArmorAction(ITarget target, int value, int duration)
    {
        this.target = target;
        this.armorValue = value;
        this.duration = duration; // Use Math.Max(1, duration) if 0 should mean 1 turn
        Log($"Created ApplyArmorAction: Target={target?.TargetId.ToUpper().Substring(0,8)}, Value={value}, Duration={duration}", LogTag.Actions | LogTag.Effects);
    }

    public void Execute()
    {
        if (!(target is ICreature creatureTarget))
        {
            LogWarning($"ApplyArmorAction: Target is not a Creature ({target?.GetType().Name}). Cannot apply armor.", LogTag.Actions | LogTag.Effects);
            return;
        }

        if (armorValue <= 0)
        {
            LogWarning($"ApplyArmorAction: Invalid armor value ({armorValue}).", LogTag.Actions | LogTag.Effects);
            return;
        }

        // Directly modify the armor pool using the new method
        creatureTarget.ModifyArmorPool(armorValue);
        Log($"ApplyArmorAction: Added {armorValue} armor to {creatureTarget.Name}. New armor pool: {creatureTarget.CurrentArmorPool}", LogTag.Actions | LogTag.Effects);
    }

     public override string ToString() {
        // Duration is no longer relevant
        return $"ApplyArmorAction: Target={target?.TargetId.ToUpper().Substring(0,8)}, Value={armorValue}";
    }
}
