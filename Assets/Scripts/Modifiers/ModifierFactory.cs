using static SlayTheClone.Enums;

namespace SlayTheClone {
    public interface IModifierFactory {
        // Creates a permanent stat modifier
        IModifier CreateStatModifier(string name, string description, ModifiableStat stat, ModifierCalculationType calcType, int value);

        // Creates a timed stat modifier that expires after a certain number of turns
        IModifier CreateTimedStatModifier(string name, string description, ModifiableStat stat, ModifierCalculationType calcType, int value, int durationInTurns, int currentTurn);

        // Creates a status effect modifier
        IModifier CreateStatusEffectModifier(string name, string description, StatusEffectType type, int duration, int potency, int currentTurn);
    }

    public static class StatModifiers {
        /// <summary>
        /// Applies one modifier per selected stat (attack, max health, speed). A zero value changes nothing.
        /// A positive duration makes the modifiers expire after that many turns.
        /// </summary>
        public static void Apply(IModifierManager manager, IModifierFactory factory, ICreature target, int value,
            bool modifyAttack, bool modifyHealth, bool modifySpeed,
            ModifierCalculationType calcType, int duration, int currentTurn) {
            if (value == 0) return;

            if (modifyAttack) ApplyOne(manager, factory, target, ModifiableStat.Attack, value, calcType, duration, currentTurn);
            if (modifyHealth) ApplyOne(manager, factory, target, ModifiableStat.Health, value, calcType, duration, currentTurn);
            if (modifySpeed) ApplyOne(manager, factory, target, ModifiableStat.Speed, value, calcType, duration, currentTurn);
        }

        private static void ApplyOne(IModifierManager manager, IModifierFactory factory, ICreature target, ModifiableStat stat,
            int value, ModifierCalculationType calcType, int duration, int currentTurn) {
            string statLabel = stat == ModifiableStat.Health ? "Max Health" : stat.ToString();
            string sign = calcType == ModifierCalculationType.Flat && value >= 0 ? "+" : "";
            string suffix = calcType == ModifierCalculationType.Percentage ? "%" : "";
            string name = $"{stat} Modify ({calcType} {value}){(duration > 0 ? $" [{duration}t]" : "")}";
            string description = $"{sign}{value}{suffix} {statLabel}{(duration > 0 ? $" ({duration} turns)" : "")}";

            IModifier modifier = duration > 0
                ? factory.CreateTimedStatModifier(name, description, stat, calcType, value, duration, currentTurn)
                : factory.CreateStatModifier(name, description, stat, calcType, value);
            manager.ApplyModifier(target, modifier);
        }
    }

    public class SimpleModifierFactory : IModifierFactory {
        public IModifier CreateStatModifier(string name, string description, ModifiableStat stat, ModifierCalculationType calcType, int value) {
            return new StatModifier(name, description, stat, calcType, value);
        }

        public IModifier CreateTimedStatModifier(string name, string description, ModifiableStat stat, ModifierCalculationType calcType, int value, int durationInTurns, int currentTurn) {
            return new TimedStatModifier(name, description, stat, calcType, value, durationInTurns, currentTurn);
        }

        public IModifier CreateStatusEffectModifier(string name, string description, StatusEffectType type, int duration, int potency, int currentTurn) {
            return new StatusEffectModifier(name, description, type, duration, potency, currentTurn);
        }
    }
}
