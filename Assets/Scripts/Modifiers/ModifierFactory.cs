using UnityEngine.Events;
using static Enums;

public interface IModifierFactory {
    // Creates a permanent stat modifier
    IModifier CreateStatModifier(string name, string description, ModifiableStat stat, ModifierCalculationType calcType, int value);

    // Creates a timed stat modifier that expires after a certain number of turns
    IModifier CreateTimedStatModifier(string name, string description, ModifiableStat stat, ModifierCalculationType calcType, int value, int durationInTurns, int currentTurn);

    // Creates an event listener modifier (add more specific methods as needed)
    IModifier CreateCreatureDamagedModifier(string name, string description, UnityAction<ICreature, int> action);
    // IModifier CreateTurnEndedModifier(string name, string description, UnityAction<int> action);
    // IModifier CreateCreatureSummonedModifier(string name, string description, UnityAction<ICreature, IPlayer> action);
    // IModifier CreateCreatureDiedModifier(string name, string description, UnityAction<ICreature> action);

    // Creates a status effect modifier
    IModifier CreateStatusEffectModifier(string name, string description, StatusEffectType type, int duration, int potency, int currentTurn);

    // Armor Modifiers (Permanent only) // REMOVED - Armor is no longer a modifier type
    // IModifier CreateArmorModifier(string name, string description, int value);
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

    // Implement other factory methods for different events...
    // public IModifier CreateTurnEndedModifier(...) { return new EventSubscriberModifier(..., EffectTrigger.EndOfTurn, ...); }
    // public IModifier CreateCreatureSummonedModifier(...) { return new EventSubscriberModifier(..., EffectTrigger.OnPlay, ...); }
    // public IModifier CreateCreatureDiedModifier(...) { return new EventSubscriberModifier(..., EffectTrigger.OnDeath, ...); }

    public IModifier CreateStatusEffectModifier(string name, string description, StatusEffectType type, int duration, int potency, int currentTurn) {
        return new StatusEffectModifier(name, description, type, duration, potency, currentTurn);
    }
}
