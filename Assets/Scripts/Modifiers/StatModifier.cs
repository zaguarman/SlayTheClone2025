using static Zeitgeist.DebugLogger;

namespace Zeitgeist {
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

        // Nothing to do on apply or remove: ModifierManager reads TryGetStatModification when it recalculates stats
        public override void Apply(ICreature target, IGameMediator mediator) { }

        public override void Remove(ICreature target, IGameMediator mediator) { }

        public override bool TryGetStatModification(ModifiableStat stat, out ModifierCalculationType calcType, out int value) {
            if (stat != StatToModify) return base.TryGetStatModification(stat, out calcType, out value);

            calcType = CalculationType;
            value = Value;
            return true;
        }
    }
}
