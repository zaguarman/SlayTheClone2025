using static DebugLogger;
using static Enums; // Add this for EffectTrigger enum

// Base class for armor modifiers (can be permanent or timed)
public abstract class ArmorModifier : BaseModifier
{
    public int Value { get; protected set; } // The amount of armor provided

    protected ArmorModifier(string name, string description, int value)
        : base(name, description)
    {
        Value = value;
        Log($"ArmorModifier '{Name}' details: Value={Value}", LogTag.Effects);
    }

    // Armor modifiers don't directly apply logic on Apply/Remove,
    // they just provide data for the ModifierManager's calculations.
    public override void Apply(object target, GameMediator mediator)
    {
        // Log($"ArmorModifier '{Name}' applied to {target}. Manager will recalculate stats.", LogTag.Effects);
    }

    public override void Remove(object target, GameMediator mediator)
    {
        // Log($"ArmorModifier '{Name}' removed from {target}. Manager will recalculate stats.", LogTag.Effects);
    }

    // Armor doesn't modify stats directly in the TryGetStatModification way
    public override bool TryGetStatModification(ModifiableStat stat, out ModifierCalculationType calcType, out int value)
    {
        calcType = ModifierCalculationType.Flat;
        value = 0;
        return false;
    }

     public override bool HandlesEvent(EffectTrigger eventType) => false; // Armor doesn't typically handle events directly
}
