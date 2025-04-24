using static DebugLogger;
using static Enums;
using System;

public class ModifyArmorAction : IGameAction
{
    private readonly ICreature targetCreature;
    private readonly int amount; // Positive to add armor, negative to remove

    public ModifyArmorAction(ICreature target, int amount)
    {
        if (target == null)
        {
            LogWarning($"ModifyArmorAction: Target creature is null. Action cannot be created.", LogTag.Actions | LogTag.Effects);
            // Optional: throw an exception or handle gracefully
        }
        this.targetCreature = target;
        this.amount = amount;
        Log($"Created ModifyArmorAction: Target={target?.Name}, Amount={amount}", LogTag.Actions | LogTag.Effects);
    }



    // Getters for executor
    public ICreature GetTargetCreature() => targetCreature;
    public int GetAmount() => amount;

     public override string ToString() {
        return $"ModifyArmorAction: Target={targetCreature?.Name}, Amount={amount}";
    }
}
