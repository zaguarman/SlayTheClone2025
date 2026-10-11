using System;

namespace SlayTheClone {
    // Enum to define how a stat modifier calculates its effect
    public enum ModifierCalculationType {
        Flat,       // Adds a flat value (e.g., +5 Attack)
        Percentage  // Adds a percentage of the *current* value (e.g., +10% Attack) - adjust if base needed
    }

    // Enum to represent stats that can be modified
    public enum ModifiableStat {
        Attack,
        Health, // Represents Max Health modifications
        Speed
    }

    // Interface for all modifiers
    public interface IModifier {
        Guid Id { get; }
        string Name { get; }
        string Description { get; }

        // Called when the modifier is applied to a creature
        void Apply(ICreature target, IGameMediator mediator);

        // Called when the modifier is removed from a creature
        void Remove(ICreature target, IGameMediator mediator);

        // Method for StatModifiers to declare their effect without applying it
        // Returns true if this modifier affects the given stat
        bool TryGetStatModification(ModifiableStat stat, out ModifierCalculationType calcType, out int value);
    }
}
