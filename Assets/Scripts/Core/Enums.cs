
using System; // Add this for Flags attribute

public class Enums {
    // Card/Effect Enums
    public enum CardType { Creature, Spell, Enchantment }
    public enum EffectType { Immediate, Triggered, Continuous, Timed, Status } // Added Status
    public enum EffectTrigger { OnPlay, OnDeath, OnDamage, StartOfTurn, EndOfTurn, ActionAttempted } // Added ActionAttempted
    public enum ActionType { Damage, Heal, Draw, Summon, Armor, Stun, ModifyStat, ApplyStatus } // Renamed Buff to ModifyStat
    public enum TargetType { Player, Enemy, AllCreatures, FriendlyCreatures, EnemyCreatures, Self }

    public enum StatusEffectType {
        None,
        Burned,     // Takes damage at end of turn
        Paralyzed,  // Cannot perform actions (attack/move)
        Frozen,     // Similar to Paralyzed, potentially longer duration or different interactions
        Poisoned    // Takes increasing damage over time (more complex, start simple)
    }

    // Targeting Modifiers
    [Flags] // Make sure System is imported
    public enum TargetModifier {
        None = 0,
        WithAdjacent = 1 << 0,
        AllSameTypeTargets = 1 << 1,
        Random = 1 << 2,
        Chained = 1 << 3,
    }

    // Weather Types
    public enum WeatherType {
        Clear,
        Rainy,
        Sunny
    }
}