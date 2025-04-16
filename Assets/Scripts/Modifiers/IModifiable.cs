using Enums;

public interface IModifiable {
    ModifierController ModifierController { get; }
    // Unique ID for this specific instance
    string GetModifiableId();
    // Base stat value before any modifiers
    int GetBaseStat(StatType statType);
    // Current stat value including modifiers
    int GetCurrentStat(StatType statType);
    // Optional: A way to get the associated GameObject for UI purposes if needed
    // GameObject GetGameObject();
}
