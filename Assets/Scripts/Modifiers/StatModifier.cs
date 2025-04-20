using static DebugLogger;

// Represents a modifier that changes a creature's stats
public class StatModifier : BaseModifier {
    public ModifiableStat StatToModify { get; }
    public ModifierCalculationType CalculationType { get; }
    public int Value { get; } // The value of the modification (e.g., +5, or +10 for 10%)

    public StatModifier(string name, string description, ModifiableStat stat, ModifierCalculationType calcType, int value)
        : base(name, description) {
        StatToModify = stat;
        CalculationType = calcType;
        Value = value;
        Log($"StatModifier '{Name}' details: Modifies {stat} ({calcType}) by {value}", LogTag.Effects);
    }

    // Stat modifiers don't need complex Apply/Remove if the Manager handles recalculation.
    // They primarily provide data via TryGetStatModification.
    public override void Apply(object target, GameMediator mediator) {
        // Log($"StatModifier '{Name}' applied to {target}. Manager will recalculate stats.", LogTag.Effects);
        // No direct action needed here. The ModifierManager will query this modifier during recalculation.
    }

    public override void Remove(object target, GameMediator mediator) {
        // Log($"StatModifier '{Name}' removed from {target}. Manager will recalculate stats.", LogTag.Effects);
        // No direct action needed here. The ModifierManager will trigger recalculation.
    }

    public override bool TryGetStatModification(ModifiableStat stat, out ModifierCalculationType calcType, out int value) {
        if (stat == StatToModify) {
            calcType = CalculationType;
            value = Value;
            return true;
        }

        // Default values if this modifier doesn't affect the requested stat
        calcType = ModifierCalculationType.Flat;
        value = 0;
        return false;
    }
}
