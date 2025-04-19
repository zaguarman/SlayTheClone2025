using System;
using static Enums; // Assuming your Enums class is accessible

// Enum to define how a stat modifier calculates its effect
public enum ModifierCalculationType
{
    Flat,       // Adds a flat value (e.g., +5 Attack)
    Percentage  // Adds a percentage of the *current* value (e.g., +10% Attack) - adjust if base needed
}

// Enum to represent stats that can be modified
public enum ModifiableStat
{
    Attack,
    Health // Represents Max Health modifications
    // Add other stats like Speed, Cost, etc. if needed
}

// Interface for all modifiers
public interface IModifier
{
    Guid Id { get; }
    string Name { get; }
    string Description { get; }

    // Called when the modifier is applied to a target (Creature, Slot, etc.)
    void Apply(object target, GameMediator mediator);

    // Called when the modifier is removed from a target
    void Remove(object target, GameMediator mediator);

    // Method for StatModifiers to declare their effect without applying it
    // Returns true if this modifier affects the given stat
    bool TryGetStatModification(ModifiableStat stat, out ModifierCalculationType calcType, out int value);

    // Method for EventSubscriberModifiers to declare their interest in an event
    bool HandlesEvent(EffectTrigger eventType); // Using existing EffectTrigger for events
}
