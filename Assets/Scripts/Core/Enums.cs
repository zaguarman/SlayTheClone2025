
namespace Enums {
    public enum CardType { Creature, Spell, Enchantment }
    public enum EffectType { Immediate, Triggered, Continuous, Timed }
    public enum EffectTrigger { OnPlay, OnDeath, OnDamage, StartOfTurn, EndOfTurn }
    // Updated ActionType
    public enum ActionType {
        Damage, Heal, Draw, Summon, Armor, Stun, // Removed Buff
        ApplyModifier, RemoveModifier // New modifier actions
    }
    public enum TargetType { Player, Enemy, AllCreatures, FriendlyCreatures, EnemyCreatures, Self }

    [System.Flags]
    public enum TargetModifier {
        None = 0,
        WithAdjacent = 1 << 0,
        AllSameTypeTargets = 1 << 1,
        Random = 1 << 2,
        Chained = 1 << 3,
    }


    // Modifier System
    public enum ModifierDurationType { Permanent, TurnBased }
    public enum ModifierStackingType { Stackable, RefreshDuration, NonStackable }
    public enum StatType { Attack, Health, MaxHealth }
    public enum StatAdjustmentType { Flat, Percentage }
}