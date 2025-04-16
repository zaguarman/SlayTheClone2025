using UnityEngine;

namespace ModifierSystem {
    // Renamed enums to avoid conflict if they were in the global Enums class
    public enum ModifierDurationType { Permanent, TurnBased }
    public enum ModifierStackingType { Stackable, RefreshDuration, NonStackable }
    public enum StatType { Attack, Health, MaxHealth /* Add others like Movement, Cost, etc. */ }
    public enum StatAdjustmentType { Flat, Percentage } // Start with Flat
}
