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

    public void Execute()
    {
        if (targetCreature == null)
        {
            LogWarning($"ModifyArmorAction: Target creature is null. Cannot modify armor.", LogTag.Actions | LogTag.Effects);
            return;
        }

        // Directly call the creature's method to modify its armor pool
        targetCreature.ModifyArmorPool(amount);
        Log($"Executed ModifyArmorAction: Modified armor for {targetCreature.Name} by {amount}. New Armor Pool: {targetCreature.CurrentArmorPool}", LogTag.Actions | LogTag.Effects | LogTag.Creatures);
    }

     public override string ToString() {
        return $"ModifyArmorAction: Target={targetCreature?.Name}, Amount={amount}";
    }
}
