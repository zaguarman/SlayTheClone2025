using UnityEngine.Events;
using static Enums;

public interface IModifierFactory {
    // Creates a permanent stat modifier
    IModifier CreateStatModifier(string name, string description, ModifiableStat stat, ModifierCalculationType calcType, int value);

    // Creates a timed stat modifier that expires after a certain number of turns
    IModifier CreateTimedStatModifier(string name, string description, ModifiableStat stat, ModifierCalculationType calcType, int value, int durationInTurns, int currentTurn);

    // Creates an event listener modifier (add more specific methods as needed)
    IModifier CreateCreatureDamagedModifier(string name, string description, UnityAction<ICreature, int> action);

    // Creates a status effect modifier
    IModifier CreateStatusEffectModifier(string name, string description, StatusEffectType type, int duration, int potency, int currentTurn);
}

public class SimpleModifierFactory : IModifierFactory {
    public IModifier CreateStatModifier(string name, string description, ModifiableStat stat, ModifierCalculationType calcType, int value) {
        return new StatModifier(name, description, stat, calcType, value);
    }

    public IModifier CreateTimedStatModifier(string name, string description, ModifiableStat stat, ModifierCalculationType calcType, int value, int durationInTurns, int currentTurn) {
        return new TimedStatModifier(name, description, stat, calcType, value, durationInTurns, currentTurn);
    }

    public IModifier CreateCreatureDamagedModifier(string name, string description, UnityAction<ICreature, int> action) {
        return new EventSubscriberModifier(name, description, EffectTrigger.OnDamage, action);
    }

    public IModifier CreateStatusEffectModifier(string name, string description, StatusEffectType type, int duration, int potency, int currentTurn) {
        return new StatusEffectModifier(name, description, type, duration, potency, currentTurn);
    }
}
