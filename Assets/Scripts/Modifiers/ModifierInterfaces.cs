using System;

namespace Zeitgeist {
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

    /// <summary>
    /// A card whose effect is on a creature (Magic's enchantments): the status or stat change it made, and who played it.
    /// The board shows the card tucked under the creature's while its effect lasts. No modifier: a change to the card itself
    /// (Health Loss), there until the creature leaves play.
    /// </summary>
    public class Attachment {
        public ICard Card { get; }
        public IModifier Modifier { get; }
        public IPlayer Owner { get; }

        public Attachment(ICard card, IModifier modifier, IPlayer owner) {
            Card = card;
            Modifier = modifier;
            Owner = owner;
        }
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
